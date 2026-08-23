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

        Assert.Contains(summaries, preset => preset.ContentName == "アルカディア零式1層");
        Assert.Contains(summaries, preset => preset.ContentName == "アルカディア零式2層");
        Assert.Contains(summaries, preset => preset.ContentName == "アルカディア零式3層");
        Assert.Contains(summaries, preset => preset.ContentName == "アルカディア零式4層 前半");
        Assert.Contains(summaries, preset => preset.ContentName == "アルカディア零式4層 後半");
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
