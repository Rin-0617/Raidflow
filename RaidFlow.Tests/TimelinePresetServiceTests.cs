using RaidFlow.Models;
using RaidFlow.Services;
using Xunit;

namespace RaidFlow.Tests;

public sealed class TimelinePresetServiceTests
{
    [Fact]
    public void LoadSummariesIncludesBundledPresets()
    {
        var summaries = TimelinePresetService.LoadSummaries();

        Assert.Contains(summaries, preset => preset.ContentName == "アルカディア零式：ヘビー級1層");
        Assert.Contains(summaries, preset => preset.ContentName == "アルカディア零式：ヘビー級2層");
        Assert.Contains(summaries, preset => preset.ContentName == "アルカディア零式：ヘビー級3層");
        Assert.Contains(summaries, preset => preset.ContentName == "アルカディア零式：ヘビー級4層 前半");
        Assert.Contains(summaries, preset => preset.ContentName == "アルカディア零式：ヘビー級4層 後半");
        Assert.Contains(summaries, preset => preset.Id == "aac_heavyweight_m1_savage");
        Assert.Contains(summaries, preset => preset.Id == "aac_heavyweight_m2_savage");
        Assert.Contains(summaries, preset => preset.Id == "aac_heavyweight_m3_savage");
        Assert.Contains(summaries, preset => preset.Id == "aac_heavyweight_m4_savage_phase1");
        Assert.Contains(summaries, preset => preset.Id == "aac_heavyweight_m4_savage_phase2");
        Assert.Contains(summaries, preset => preset.ContentName == "絶アルテマウェポン破壊作戦");
        Assert.All(summaries, preset => Assert.True(preset.EventCount > 0));
    }

    [Fact]
    public void PresetsAreEmbeddedInPluginAssembly()
    {
        var resourceNames = typeof(TimelinePresetService).Assembly.GetManifestResourceNames();

        Assert.Contains(resourceNames, name => name.EndsWith(
            "the_weapon_refrain_ult.json",
            StringComparison.Ordinal));
    }

    [Fact]
    public void ChaoticFuturesPresetOffsetsImaginaryKefkaPhase()
    {
        var plan = RaidFlowDocument.CreateDefault();

        var result = TimelinePresetService.ApplyPreset(plan, "futures_rewritten_chaotic");

        Assert.True(result.Success);
        Assert.Contains(plan.Events, timelineEvent =>
            timelineEvent.Name == "ケフカ (HP25% 以下)" &&
            timelineEvent.TimeSeconds == 862);
        Assert.Contains(plan.Events, timelineEvent =>
            timelineEvent.Name == "ミッシング・ゼロ" &&
            timelineEvent.TimeSeconds == 1122);
        Assert.DoesNotContain(plan.Events, timelineEvent =>
            timelineEvent.Name.StartsWith("ミッシング・ゼロ", StringComparison.Ordinal) &&
            timelineEvent.TimeSeconds < 862);
    }

    [Fact]
    public void ChaoticFuturesPresetClassifiesKeyMitigationEvents()
    {
        var plan = RaidFlowDocument.CreateDefault();

        var result = TimelinePresetService.ApplyPreset(plan, "futures_rewritten_chaotic");

        Assert.True(result.Success);
        Assert.All(
            plan.Events.Where(timelineEvent => timelineEvent.Name == "ばりばりルインガ"),
            timelineEvent => Assert.Equal(TimelineEventType.Tankbuster, timelineEvent.Type));
        Assert.Contains(plan.Events, timelineEvent =>
            timelineEvent.Name == "裁きの光" &&
            timelineEvent.Type == TimelineEventType.Raidwide);
        Assert.Contains(plan.Events, timelineEvent =>
            timelineEvent.Name == "終末の双腕" &&
            timelineEvent.Type == TimelineEventType.Tankbuster);
        Assert.Contains(plan.Events, timelineEvent =>
            timelineEvent.Name == "びんびんビンタ" &&
            timelineEvent.Type == TimelineEventType.Stack);
        Assert.Contains(plan.Events, timelineEvent =>
            timelineEvent.Name == "狂気のオーケストラ" &&
            timelineEvent.Type == TimelineEventType.Tankbuster);
    }

    [Theory]
    [InlineData("aac_heavyweight_m1_savage", "キラーボイス", TimelineEventType.Raidwide)]
    [InlineData("aac_heavyweight_m1_savage", "ブルータルレイン", TimelineEventType.Stack)]
    [InlineData("aac_heavyweight_m2_savage", "ディープインパクト", TimelineEventType.Tankbuster)]
    [InlineData("aac_heavyweight_m2_savage", "ファイティングスピリット", TimelineEventType.Raidwide)]
    [InlineData("aac_heavyweight_m3_savage", "キング・オブ・アルカディア", TimelineEventType.Raidwide)]
    [InlineData("aac_heavyweight_m4_savage_phase2", "アルカディアン・フレイム", TimelineEventType.Raidwide)]
    [InlineData("the_unending_coil_of_bahamut", "フラッテン", TimelineEventType.Tankbuster)]
    [InlineData("the_unending_coil_of_bahamut", "ギガフレア", TimelineEventType.Raidwide)]
    [InlineData("the_unending_coil_of_bahamut", "アク・モーン", TimelineEventType.Stack)]
    [InlineData("the_epic_of_alexander", "カスケード", TimelineEventType.Raidwide)]
    [InlineData("the_epic_of_alexander", "フルイドスイング", TimelineEventType.Tankbuster)]
    [InlineData("the_epic_of_alexander", "プロティアンウェイブ", TimelineEventType.Spread)]
    [InlineData("dragonsongs_reprise", "騎竜剣ギガフレア", TimelineEventType.Raidwide)]
    [InlineData("dragonsongs_reprise", "アスカロンマイト", TimelineEventType.Tankbuster)]
    [InlineData("dragonsongs_reprise", "騎竜剣アク・モーン(DPS)", TimelineEventType.Stack)]
    [InlineData("ultimate_futures_rewritten", "絶対零度", TimelineEventType.Raidwide)]
    [InlineData("ultimate_futures_rewritten", "ブラックヘイロー", TimelineEventType.Tankbuster)]
    [InlineData("ultimate_futures_rewritten", "アク・モーン", TimelineEventType.Stack)]
    [InlineData("ultimate_omega_protocol", "ソーラレイ", TimelineEventType.Tankbuster)]
    [InlineData("ultimate_omega_protocol", "コスモメモリー", TimelineEventType.Raidwide)]
    [InlineData("ultimate_omega_protocol", "パイルピッチ", TimelineEventType.Stack)]
    public void BundledPresetsClassifyRepresentativeMitigationEvents(
        string presetId,
        string eventName,
        TimelineEventType expectedType)
    {
        var plan = RaidFlowDocument.CreateDefault();

        var result = TimelinePresetService.ApplyPreset(plan, presetId);

        Assert.True(result.Success);
        Assert.Contains(plan.Events, timelineEvent =>
            timelineEvent.Name == eventName &&
            timelineEvent.Type == expectedType);
    }

    [Fact]
    public void ApplyPresetReplacesTimelineAndKeepsParty()
    {
        var summaries = TimelinePresetService.LoadSummaries();
        var preset = summaries.First(preset => preset.ContentName == "絶もうひとつの未来");
        var plan = RaidFlowDocument.CreateDefault();
        plan.Events.Add(new TimelineEvent
        {
            Name = "Old event",
            Assignments =
            [
                new MitigationAssignment
                {
                    Slot = PartySlot.MT,
                    Job = "PLD",
                    ActionId = 7531,
                },
            ],
        });

        var result = TimelinePresetService.ApplyPreset(plan, preset.Id);

        Assert.True(result.Success);
        Assert.Equal(preset.ContentName, plan.ContentName);
        Assert.Equal(preset.Revision, plan.Revision);
        Assert.Equal(preset.ContentLevel, plan.ContentLevel);
        Assert.Equal(8, plan.Party.Count);
        Assert.DoesNotContain(plan.Events, timelineEvent => timelineEvent.Name == "Old event");
        Assert.DoesNotContain(plan.Events, timelineEvent => timelineEvent.Name == "AA");
        Assert.All(plan.Events, timelineEvent => Assert.Empty(timelineEvent.Assignments));
    }
}
