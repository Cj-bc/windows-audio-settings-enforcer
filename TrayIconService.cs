using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace WindowsAudioSettingsEnforcer;

public sealed class TrayIconService : IHostedService
{
    private readonly EnforcementState _state;
    private readonly IOptionsMonitor<AudioOptions> _options;
    private readonly SettingsWriter _writer;
    private readonly IHostApplicationLifetime _lifetime;

    private Thread? _uiThread;
    private TrayApplicationContext? _context;
    private SynchronizationContext? _uiSyncContext;

    public TrayIconService(
        EnforcementState state,
        IOptionsMonitor<AudioOptions> options,
        SettingsWriter writer,
        IHostApplicationLifetime lifetime)
    {
        _state = state;
        _options = options;
        _writer = writer;
        _lifetime = lifetime;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        _uiThread = new Thread(() =>
        {
            try
            {
                Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                _uiSyncContext = new WindowsFormsSynchronizationContext();
                SynchronizationContext.SetSynchronizationContext(_uiSyncContext);

                _context = new TrayApplicationContext(_state, _options, _writer, _lifetime);
                started.TrySetResult();
                Application.Run(_context);
            }
            catch (Exception ex)
            {
                started.TrySetException(ex);
            }
            finally
            {
                _context?.Dispose();
            }
        })
        {
            IsBackground = true,
            Name = "TrayIcon",
        };
        _uiThread.SetApartmentState(ApartmentState.STA);
        _uiThread.Start();

        return started.Task;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        if (_context is { } context && _uiSyncContext is { } sync)
        {
            sync.Post(_ => context.ExitThread(), null);
            _uiThread?.Join(TimeSpan.FromSeconds(5));
        }

        return Task.CompletedTask;
    }

    private sealed class TrayApplicationContext : ApplicationContext
    {
        private readonly EnforcementState _state;
        private readonly IOptionsMonitor<AudioOptions> _options;
        private readonly SettingsWriter _writer;
        private readonly IHostApplicationLifetime _lifetime;

        private readonly NotifyIcon _icon;
        private readonly ToolStripMenuItem _enabledItem;
        private readonly ToolStripMenuItem _muteItem;
        private SettingsForm? _settingsForm;

        public TrayApplicationContext(
            EnforcementState state,
            IOptionsMonitor<AudioOptions> options,
            SettingsWriter writer,
            IHostApplicationLifetime lifetime)
        {
            _state = state;
            _options = options;
            _writer = writer;
            _lifetime = lifetime;

            _enabledItem = new ToolStripMenuItem("Enabled") { CheckOnClick = true };
            _enabledItem.Click += (_, _) =>
            {
                _state.Enabled = _enabledItem.Checked;
                UpdateTooltip();
            };

            _muteItem = new ToolStripMenuItem("Mute") { CheckOnClick = true };
            _muteItem.Click += (_, _) =>
                _writer.Save(_options.CurrentValue.VolumePercent, _muteItem.Checked);

            var settingsItem = new ToolStripMenuItem("Settings…");
            settingsItem.Click += (_, _) => ShowSettings();

            var exitItem = new ToolStripMenuItem("Exit");
            exitItem.Click += (_, _) => _lifetime.StopApplication();

            var menu = new ContextMenuStrip();
            menu.Items.AddRange(new ToolStripItem[]
            {
                _enabledItem,
                _muteItem,
                settingsItem,
                new ToolStripSeparator(),
                exitItem,
            });
            menu.Opening += (_, _) =>
            {
                _enabledItem.Checked = _state.Enabled;
                _muteItem.Checked = _options.CurrentValue.Mute;
            };

            _icon = new NotifyIcon
            {
                Icon = SystemIcons.Application,
                ContextMenuStrip = menu,
                Visible = true,
            };
            _icon.DoubleClick += (_, _) => ShowSettings();
            UpdateTooltip();
        }

        private void UpdateTooltip()
            => _icon.Text = _state.Enabled
                ? "Audio Settings Enforcer – Active"
                : "Audio Settings Enforcer – Paused";

        private void ShowSettings()
        {
            if (_settingsForm is { IsDisposed: false })
            {
                _settingsForm.Activate();
                return;
            }

            _settingsForm = new SettingsForm(_options.CurrentValue, _writer);
            _settingsForm.Show();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _settingsForm?.Dispose();
                _icon.Visible = false;
                _icon.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
