namespace WindowsAudioSettingsEnforcer;

public sealed class AudioOptions
{
    public const string SectionName = "Audio";

    public int VolumePercent { get; set; } = 50;
    public bool Mute { get; set; }
}
