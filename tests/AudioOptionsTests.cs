using WindowsAudioSettingsEnforcer;

namespace WindowsAudioSettingsEnforcer.Tests;

public sealed class AudioOptionsTests
{
    [Fact]
    public void VolumePercent_DefaultsTo50()
    {
        Assert.Equal(50, new AudioOptions().VolumePercent);
    }

    [Fact]
    public void Mute_DefaultsToFalse()
    {
        Assert.False(new AudioOptions().Mute);
    }

    [Fact]
    public void SectionName_IsAudio()
    {
        Assert.Equal("Audio", AudioOptions.SectionName);
    }
}
