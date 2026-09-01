using RaidFlow.Models;
using RaidFlow.Services;
using Xunit;

namespace RaidFlow.Tests;

public sealed class MitigationMacroServiceTests
{
    [Fact]
    public void BuildSlotMacrosOrdersEventsChronologicallyAndSplitsEveryFifteenLines()
    {
        var plan = RaidFlowDocument.CreateDefault();
        for (var index = 16; index >= 1; index--)
        {
            var timelineEvent = new TimelineEvent
            {
                Id = $"evt_{index}",
                TimeSeconds = index,
                Name = $"Event {index:00}",
                Type = TimelineEventType.Raidwide,
            };
            timelineEvent.Assignments.Add(new MitigationAssignment
            {
                Slot = PartySlot.MT,
                Job = "PLD",
                ActionId = (uint)(9000 + index),
            });
            timelineEvent.Assignments.Add(new MitigationAssignment
            {
                Slot = PartySlot.ST,
                Job = "DRK",
                ActionId = (uint)(8000 + index),
            });
            plan.Events.Add(timelineEvent);
        }

        var macros = MitigationMacroService.BuildSlotMacros(plan, PartySlot.MT);

        Assert.Equal(2, macros.Count);
        var firstMacroLines = macros[0].Split(Environment.NewLine);
        var secondMacroLines = macros[1].Split(Environment.NewLine);
        Assert.Equal(15, firstMacroLines.Length);
        Assert.Single(secondMacroLines);
        Assert.Equal("/p Event 01 9001", firstMacroLines[0]);
        Assert.Equal("/p Event 15 9015", firstMacroLines[^1]);
        Assert.Equal("/p Event 16 9016", secondMacroLines[0]);
    }

    [Fact]
    public void BuildSlotMacroLinesCombinesSameEventAssignmentsByUseOffset()
    {
        var plan = RaidFlowDocument.CreateDefault();
        var timelineEvent = new TimelineEvent
        {
            Id = "evt_raidwide",
            TimeSeconds = 30,
            Name = "Raidwide",
            Type = TimelineEventType.Raidwide,
        };
        timelineEvent.Assignments.Add(new MitigationAssignment
        {
            Slot = PartySlot.D3,
            Job = "BRD",
            ActionId = 9002,
            UseOffsetSeconds = -3,
        });
        timelineEvent.Assignments.Add(new MitigationAssignment
        {
            Slot = PartySlot.D3,
            Job = "BRD",
            ActionId = 9001,
            UseOffsetSeconds = -10,
        });
        plan.Events.Add(timelineEvent);

        var lines = MitigationMacroService.BuildSlotMacroLines(plan, PartySlot.D3);

        Assert.Single(lines);
        Assert.Equal("/p Raidwide 9001 / 9002", lines[0]);
    }
}
