using System.Runtime.InteropServices;
using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;

namespace WindowsAudioSettingsEnforcer;

public sealed class AudioEnforcerService : BackgroundService
{
    private enum Signal
    {
        Apply,
        DeviceChanged,
    }

    private const float VolumeEpsilon = 0.005f;
    private static readonly TimeSpan WatchdogInterval = TimeSpan.FromSeconds(5);

    private readonly ILogger<AudioEnforcerService> _logger;
    private readonly IOptionsMonitor<AudioOptions> _options;
    private readonly EnforcementState _state;
    private readonly Channel<Signal> _signals = Channel.CreateUnbounded<Signal>();

    private MMDeviceEnumerator? _enumerator;
    private MMDevice? _device;

    public AudioEnforcerService(
        ILogger<AudioEnforcerService> logger,
        IOptionsMonitor<AudioOptions> options,
        EnforcementState state)
    {
        _logger = logger;
        _options = options;
        _state = state;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var optionsChangeSubscription =
            _options.OnChange(_ => _signals.Writer.TryWrite(Signal.Apply));

        void OnStateChanged() => _signals.Writer.TryWrite(Signal.Apply);
        _state.Changed += OnStateChanged;

        _enumerator = new MMDeviceEnumerator();
        var notificationClient = new NotificationClient(_signals.Writer);
        _enumerator.RegisterEndpointNotificationCallback(notificationClient);

        try
        {
            Attach();
            var watchdogTask = RunWatchdogAsync(stoppingToken);

            await foreach (var signal in _signals.Reader.ReadAllAsync(stoppingToken))
            {
                if (signal == Signal.DeviceChanged)
                {
                    Detach();
                    Attach();
                }
                else
                {
                    ApplyIfNeeded();
                }
            }

            await watchdogTask;
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            _state.Changed -= OnStateChanged;
            _enumerator.UnregisterEndpointNotificationCallback(notificationClient);
            Detach();
            _enumerator.Dispose();
            _enumerator = null;
        }
    }

    private async Task RunWatchdogAsync(CancellationToken token)
    {
        using var timer = new PeriodicTimer(WatchdogInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(token))
            {
                _signals.Writer.TryWrite(Signal.Apply);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void Attach()
    {
        try
        {
            _device = _enumerator!.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            _device.AudioEndpointVolume.OnVolumeNotification += OnVolumeNotification;
            _logger.LogInformation("Attached to default playback device: {Device}", _device.FriendlyName);
            ApplyIfNeeded();
        }
        catch (COMException ex)
        {
            _device = null;
            _logger.LogWarning(ex, "No default playback device available; will retry.");
        }
    }

    private void Detach()
    {
        if (_device is null)
        {
            return;
        }

        try
        {
            _device.AudioEndpointVolume.OnVolumeNotification -= OnVolumeNotification;
        }
        catch (COMException)
        {
            // Device may already be gone; nothing to unsubscribe from.
        }

        _device.Dispose();
        _device = null;
    }

    // Fires on a COM callback thread; never set volume here (it would re-trigger
    // the notification). Only signal the ExecuteAsync loop.
    private void OnVolumeNotification(AudioVolumeNotificationData data)
        => _signals.Writer.TryWrite(Signal.Apply);

    private void ApplyIfNeeded()
    {
        if (!_state.Enabled)
        {
            return;
        }

        if (_device is null)
        {
            Attach();
            return;
        }

        var options = _options.CurrentValue;
        var targetVolume = Math.Clamp(options.VolumePercent, 0, 100) / 100f;

        try
        {
            var endpointVolume = _device.AudioEndpointVolume;

            if (Math.Abs(endpointVolume.MasterVolumeLevelScalar - targetVolume) > VolumeEpsilon)
            {
                endpointVolume.MasterVolumeLevelScalar = targetVolume;
                _logger.LogInformation("Volume reset to {Percent}%.", Math.Clamp(options.VolumePercent, 0, 100));
            }

            if (endpointVolume.Mute != options.Mute)
            {
                endpointVolume.Mute = options.Mute;
                _logger.LogInformation("Mute reset to {Mute}.", options.Mute);
            }
        }
        catch (COMException ex)
        {
            _logger.LogWarning(ex, "Failed to apply audio settings; detaching and retrying.");
            Detach();
        }
    }

    private sealed class NotificationClient(ChannelWriter<Signal> writer) : IMMNotificationClient
    {
        public void OnDefaultDeviceChanged(DataFlow flow, Role role, string defaultDeviceId)
        {
            if (flow == DataFlow.Render && role == Role.Multimedia)
            {
                writer.TryWrite(Signal.DeviceChanged);
            }
        }

        public void OnDeviceAdded(string pwstrDeviceId)
        {
        }

        public void OnDeviceRemoved(string deviceId)
        {
        }

        public void OnDeviceStateChanged(string deviceId, DeviceState newState)
        {
        }

        public void OnPropertyValueChanged(string pwstrDeviceId, PropertyKey key)
        {
        }
    }
}
