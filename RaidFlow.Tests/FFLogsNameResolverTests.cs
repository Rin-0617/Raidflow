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
    [InlineData("ホーリー")]
    public void NormalNamesAreUsableAbilityNames(string name)
    {
        Assert.False(FFLogsNameResolver.IsRsvName(name));
        Assert.True(FFLogsNameResolver.IsUsableAbilityName(name));
    }

    [Fact]
    public void ResolveAbilityNamePrefersFflogsName()
    {
        var localizedNames = new Dictionary<uint, string>
        {
            [47866] = "ゲームデータ名",
        };

        var name = FFLogsNameResolver.ResolveAbilityName(
            47866,
            "FFLogs日本語名",
            localizedNames,
            "メタデータ名");

        Assert.Equal("FFLogs日本語名", name);
    }

    [Fact]
    public void ResolveAbilityNameUsesLuminaNameWhenFflogsNameIsRsv()
    {
        var localizedNames = new Dictionary<uint, string>
        {
            [47866] = "ゲームデータ名",
        };

        var name = FFLogsNameResolver.ResolveAbilityName(
            47866,
            "_rsv_47866_-1_0_0_0_SE2DC5B04_EE2DC5B04",
            localizedNames,
            "Blizzard III Blowout");

        Assert.Equal("ゲームデータ名", name);
    }

    [Fact]
    public void ResolveAbilityNameFallsBackToMetadataWhenLuminaNameIsUnavailable()
    {
        var localizedNames = new Dictionary<uint, string>
        {
            [47866] = "_rsv_47866_-1_0_0_0_SE2DC5B04_EE2DC5B04",
        };

        var name = FFLogsNameResolver.ResolveAbilityName(
            47866,
            "_rsv_47866_-1_0_0_0_SE2DC5B04_EE2DC5B04",
            localizedNames,
            "Blizzard III Blowout");

        Assert.Equal("Blizzard III Blowout", name);
    }
}
