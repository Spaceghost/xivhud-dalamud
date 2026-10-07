using XivHud.Character;

public sealed class CharacterCommandPolicyTests
{
    [Fact]
    public void ReadyCharacterRequestCanToggleRecordEvenWithHiddenCachedNativeAddon()
    {
        Assert.True(CharacterCommandPolicy.CanReplace(2, true, true, true, false, false, true));
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(1u)]
    [InlineData(3u)]
    [InlineData(4u)]   // Journal: owned by XivHud.Journal's hook on the same function.
    [InlineData(25u)]  // Armoury Chest: issued by this module and must pass through untouched.
    [InlineData(56u)]  // PvP Profile.
    [InlineData(uint.MaxValue)]
    public void EveryOtherMenuCommandRemainsNative(uint command)
    {
        Assert.False(CharacterCommandPolicy.CanReplace(command, true, true, true, false, false, true));
    }

    [Fact]
    public void EveryGateIsRequired()
    {
        Assert.False(CharacterCommandPolicy.CanReplace(2, false, true, true, false, false, true)); // off the framework thread
        Assert.False(CharacterCommandPolicy.CanReplace(2, true, false, true, false, false, true)); // adoption disabled
        Assert.False(CharacterCommandPolicy.CanReplace(2, true, true, false, false, false, true)); // record not ready
        Assert.False(CharacterCommandPolicy.CanReplace(2, true, true, true, true, false, true));   // explicit native fallback
        Assert.False(CharacterCommandPolicy.CanReplace(2, true, true, true, false, true, true));   // native window visible: let it close
        Assert.False(CharacterCommandPolicy.CanReplace(2, true, true, true, false, false, false)); // locked or not activatable
    }

    [Fact]
    public void HiddenNativeAgentDoesNotKeepFallbackLeaseAfterWindowClosedOrOpeningExpired()
    {
        Assert.True(CharacterCommandPolicy.KeepNativeBypass(false, false, false));
        Assert.True(CharacterCommandPolicy.KeepNativeBypass(true, true, true));
        Assert.True(CharacterCommandPolicy.KeepNativeBypass(true, false, true));
        Assert.False(CharacterCommandPolicy.KeepNativeBypass(false, true, false));
        Assert.False(CharacterCommandPolicy.KeepNativeBypass(false, false, true));
    }

    [Fact]
    public void MainCommandRowsMatchTheInstalledSheet()
    {
        Assert.Equal(2, CharacterMath.MainCommandCharacter);
        Assert.Equal(25, CharacterMath.MainCommandArmoury);
    }
}
