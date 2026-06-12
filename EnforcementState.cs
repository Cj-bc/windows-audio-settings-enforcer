namespace WindowsAudioSettingsEnforcer;

public sealed class EnforcementState
{
    private volatile bool _enabled = true;

    public event Action? Changed;

    public bool Enabled
    {
        get => _enabled;
        set
        {
            if (_enabled == value)
            {
                return;
            }

            _enabled = value;
            Changed?.Invoke();
        }
    }
}
