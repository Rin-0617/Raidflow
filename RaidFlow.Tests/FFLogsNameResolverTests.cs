using RaidFlow.Services;
using Xunit;

namespace RaidFlow.Tests;

public sealed class FFLogsNameResolverTests
{
    [Theory]
    [InlineData("_rsv_47866_-1_0_0_0_SE2DC5B04_EE2DC5B04")]
    [InlineData(" _RSV_47866_-1_0_0_0_SE2DC5B04_EE2DC5B04")]
    public void RsvNamesAreNotUsableAbilityNames(string name)
    {
        Assert.True(FFLogsNameResolver.IsRsvName(name));
        Assert.False(FFLogsNameResolver.IsUsableAbilityName(name));
    }

    [Theory]
    [InlineData("Blizzard III Blowout")]
    [InlineData("Japanese action name")]
    public void NormalNamesAreUsableAbilityNames(string name)
    {
        Assert.False(FFLogsNameResolver.IsRsvName(name));
        Assert.True(FFLogsNameResolver.IsUsableAbilityName(name));
    }

    [Fact]
    public void ResolveAbilityNamePrefersLocalizedGameDataName()
    {
        var localizedNames = new Dictionary<uint, string>
        {
            [47866] = "Lumina Japanese Name",
        };

        var name = FFLogsNameResolver.ResolveAbilityName(
            47866,
            "FFLogs Event English Name",
            localizedNames,
            "FFLogs Metadata English Name");

        Assert.Equal("Lumina Japanese Name", name);
    }

    [Fact]
    public void ResolveAbilityNameUsesLuminaNameWhenTranslatedMetadataNameIsRsv()
    {
        var localizedNames = new Dictionary<uint, string>
        {
            [47866] = "Lumina Japanese Name",
        };

        var name = FFLogsNameResolver.ResolveAbilityName(
            47866,
            "Blizzard III Blowout",
            localizedNames,
            "_rsv_47866_-1_0_0_0_SE2DC5B04_EE2DC5B04");

        Assert.Equal("Lumina Japanese Name", name);
    }

    [Fact]
    public void ResolveAbilityNameFallsBackToTranslatedMetadataWhenLuminaIsUnavailable()
    {
        var name = FFLogsNameResolver.ResolveAbilityName(
            47866,
            "FFLogs Event English Name",
            new Dictionary<uint, string>(),
            "FFLogs Metadata Name");

        Assert.Equal("FFLogs Metadata Name", name);
    }

    [Fact]
    public void ResolveAbilityNameFallsBackToEventNameWhenTranslatedMetadataAndLuminaAreUnavailable()
    {
        var localizedNames = new Dictionary<uint, string>
        {
            [47866] = "_rsv_47866_-1_0_0_0_SE2DC5B04_EE2DC5B04",
        };

        var name = FFLogsNameResolver.ResolveAbilityName(
            47866,
            "Blizzard III Blowout",
            localizedNames,
            "_rsv_47866_-1_0_0_0_SE2DC5B04_EE2DC5B04");

        Assert.Equal("Blizzard III Blowout", name);
    }
}
