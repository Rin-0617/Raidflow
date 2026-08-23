using RaidFlow.Models;
using RaidFlow.Services;
using Xunit;

namespace RaidFlow.Tests;

public sealed class ImportExportServiceTests
{
    [Fact]
    public void FullPlanExportImportsTimelineAndAssignments()
    {
        var source = RaidFlowDocument.CreateDefault();
        source.ContentName = "Test Duty";
        source.Revision = "v2";
        source.ContentLevel = 100;
        source.Events[0].Assignments.Add(new MitigationAssignment
        {
            Slot = PartySlot.MT,
            Job = "PLD",
            ActionId = 7531,
            UseOffsetSeconds = -5,
            Note = "first raidwide",
        });

        var json = ImportExportService.ExportFullPlan(source);
        var target = RaidFlowDocument.CreateDefault();

        var result = ImportExportService.ImportInto(target, json);

        Assert.True(result.Success, result.Message);
        Assert.Equal("Test Duty", target.ContentName);
        Assert.Equal("v2", target.Revision);
        Assert.Equal(source.Events.Count, target.Events.Count);
        Assert.Single(target.Events[0].Assignments);
        Assert.Equal(PartySlot.MT, target.Events[0].Assignments[0].Slot);
        Assert.Equal(7531u, target.Events[0].Assignments[0].ActionId);
    }

    [Fact]
    public void PersonalExportMergesOnlySelectedSlotAssignments()
    {
        var plan = RaidFlowDocument.CreateDefault();
        var targetEventId = plan.Events[0].Id;
        var exportedSlot = plan.Party.First(member => member.Slot == PartySlot.ST);
        exportedSlot.PlayerName = "Rin";
        exportedSlot.Job = "DRK";
        plan.Events[0].Assignments.Add(new MitigationAssignment
        {
            Slot = PartySlot.ST,
            Job = "DRK",
            ActionId = 7393,
            UseOffsetSeconds = -2,
            Note = "tank helper",
        });
        plan.Events[0].Assignments.Add(new MitigationAssignment
        {
            Slot = PartySlot.MT,
            Job = "PLD",
            ActionId = 7531,
            UseOffsetSeconds = -3,
        });

        var json = ImportExportService.ExportPersonal(plan, PartySlot.ST);
        var receivingPlan = RaidFlowDocument.CreateDefault();
        receivingPlan.Events.First(item => item.Id == targetEventId).Assignments.Add(new MitigationAssignment
        {
            Slot = PartySlot.MT,
            Job = "PLD",
            ActionId = 7531,
            UseOffsetSeconds = -3,
        });

        var result = ImportExportService.ImportInto(receivingPlan, json);

        Assert.True(result.Success, result.Message);
        Assert.Equal("Rin", receivingPlan.Party.First(member => member.Slot == PartySlot.ST).PlayerName);
        Assert.Equal("DRK", receivingPlan.Party.First(member => member.Slot == PartySlot.ST).Job);

        var assignments = receivingPlan.Events.First(item => item.Id == targetEventId).Assignments;
        Assert.Equal(2, assignments.Count);
        Assert.Contains(assignments, assignment => assignment.Slot == PartySlot.ST && assignment.ActionId == 7393);
        Assert.Contains(assignments, assignment => assignment.Slot == PartySlot.MT && assignment.ActionId == 7531);
    }

    [Fact]
    public void DefaultFileNamesUseRadflowSafeNames()
    {
        var plan = RaidFlowDocument.CreateDefault();
        plan.ContentName = "Duty:Alpha/Beta";
        plan.Revision = "v1?";

        var fullName = ImportExportService.DefaultFullPlanFileName(plan);
        var personalName = ImportExportService.DefaultPersonalPlanFileName(plan, PartySlot.MT);

        Assert.DoesNotContain(':', fullName);
        Assert.DoesNotContain('/', fullName);
        Assert.DoesNotContain('?', fullName);
        Assert.DoesNotContain(".radflow", fullName);
        Assert.Contains("MT", personalName);
    }
}
