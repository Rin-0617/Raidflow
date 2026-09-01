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

    [Fact]
    public void TryGetActionIdFromRsvNameExtractsEmbeddedActionId()
    {
        Assert.True(FFLogsNameResolver.TryGetActionIdFromRsvName(
            "_rsv_47866_-1_0_0_0_SE2DC5B04_EE2DC5B04",
            out var actionId));
        Assert.Equal<uint>(47866, actionId);
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
            [47866] = "ばりばりルインガ",
        };

        var name = FFLogsNameResolver.ResolveAbilityName(
            47866,
            "FFLogs Event English Name",
            localizedNames,
            "FFLogs Metadata English Name");

        Assert.Equal("ばりばりルインガ", name);
    }

    [Fact]
    public void ResolveAbilityNamePrefersTranslatedEventNameOverEnglishGameData()
    {
        var localizedNames = new Dictionary<uint, string>
        {
            [47866] = "Blizzard III Blowout",
        };

        var name = FFLogsNameResolver.ResolveAbilityName(
            47866,
            "ぶりざがインパクト",
            localizedNames,
            "Blizzard III Blowout");

        Assert.Equal("ぶりざがインパクト", name);
    }

    [Fact]
    public void ResolveAbilityNameUsesLuminaNameWhenTranslatedMetadataNameIsRsv()
    {
        var localizedNames = new Dictionary<uint, string>
        {
            [47866] = "ばりばりルインガ",
        };

        var name = FFLogsNameResolver.ResolveAbilityName(
            47866,
            "Blizzard III Blowout",
            localizedNames,
            "_rsv_47866_-1_0_0_0_SE2DC5B04_EE2DC5B04");

        Assert.Equal("ばりばりルインガ", name);
    }

    [Fact]
    public void ResolveAbilityNameUsesActionIdEmbeddedInRsvNameWhenAbilityIdIsMissing()
    {
        var localizedNames = new Dictionary<uint, string>
        {
            [47866] = "ばりばりルインガ",
        };

        var name = FFLogsNameResolver.ResolveAbilityName(
            0,
            "_rsv_47866_-1_0_0_0_SE2DC5B04_EE2DC5B04",
            localizedNames,
            string.Empty);

        Assert.Equal("ばりばりルインガ", name);
    }

    [Fact]
    public void ResolveAbilityNameUsesRsvActionIdWhenAbilityIdDoesNotResolveToJapanese()
    {
        var localizedNames = new Dictionary<uint, string>
        {
            [11111] = "Blizzard III Blowout",
            [47866] = "ぼりぼりブリザガ",
        };

        var name = FFLogsNameResolver.ResolveAbilityName(
            11111,
            "Blizzard III Blowout",
            localizedNames,
            "_rsv_47866_-1_0_0_0_SE2DC5B04_EE2DC5B04");

        Assert.Equal("ぼりぼりブリザガ", name);
    }

    [Fact]
    public void ResolveAbilityNameUsesEnglishNameMappingWhenActionIdDoesNotResolve()
    {
        var localizedNames = new Dictionary<uint, string>();
        var englishToLocalizedNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Blizzard III Blowout"] = "ぼりぼりブリザガ",
        };

        var name = FFLogsNameResolver.ResolveAbilityName(
            11111,
            "Blizzard III Blowout",
            localizedNames,
            "Blizzard III Blowout",
            englishToLocalizedNames);

        Assert.Equal("ぼりぼりブリザガ", name);
    }

    [Fact]
    public void ResolveAbilityNameFallsBackToEventNameWhenLuminaIsUnavailable()
    {
        var name = FFLogsNameResolver.ResolveAbilityName(
            47866,
            "FFLogs Event English Name",
            new Dictionary<uint, string>(),
            "FFLogs Metadata Name");

        Assert.Equal("FFLogs Event English Name", name);
    }

    [Fact]
    public void ResolveAbilityNameFallsBackToMetadataWhenEventAndLuminaAreUnavailable()
    {
        var name = FFLogsNameResolver.ResolveAbilityName(
            47866,
            string.Empty,
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
