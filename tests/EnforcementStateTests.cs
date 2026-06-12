using WindowsAudioSettingsEnforcer;

namespace WindowsAudioSettingsEnforcer.Tests;

public sealed class EnforcementStateTests
{
    [Fact]
    public void Enabled_InitiallyTrue()
    {
        var state = new EnforcementState();
        Assert.True(state.Enabled);
    }

    [Fact]
    public void SetEnabled_ToFalse_ChangesValue()
    {
        var state = new EnforcementState();
        state.Enabled = false;
        Assert.False(state.Enabled);
    }

    [Fact]
    public void SetEnabled_ToTrueWhenAlreadyTrue_DoesNotFireChanged()
    {
        var state = new EnforcementState();
        var count = 0;
        state.Changed += () => count++;

        state.Enabled = true;

        Assert.Equal(0, count);
    }

    [Fact]
    public void SetEnabled_ToFalseWhenAlreadyFalse_DoesNotFireChanged()
    {
        var state = new EnforcementState();
        state.Enabled = false;
        var count = 0;
        state.Changed += () => count++;

        state.Enabled = false;

        Assert.Equal(0, count);
    }

    [Fact]
    public void SetEnabled_ToDifferentValue_FiresChanged()
    {
        var state = new EnforcementState();
        var count = 0;
        state.Changed += () => count++;

        state.Enabled = false;

        Assert.Equal(1, count);
    }

    [Fact]
    public void SetEnabled_FalseThenTrue_FiresChangedTwice()
    {
        var state = new EnforcementState();
        var count = 0;
        state.Changed += () => count++;

        state.Enabled = false;
        state.Enabled = true;

        Assert.Equal(2, count);
    }
}
