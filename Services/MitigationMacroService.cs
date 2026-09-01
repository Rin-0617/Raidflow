using RaidFlow.Data;
using RaidFlow.Models;

namespace RaidFlow.Services;

public static class MitigationMacroService
{
    public const int MaxLinesPerMacro = 15;

    private const int MaxMacroLineLength = 180;

    public static IReadOnlyList<string> BuildSlotMacros(RaidFlowDocument plan, PartySlot slot)
    {
        var lines = BuildSlotMacroLines(plan, slot);
        return lines
            .Chunk(MaxLinesPerMacro)
            .Select(chunk => string.Join(Environment.NewLine, chunk))
            .Where(macro => !string.IsNullOrWhiteSpace(macro))
            .ToList();
    }

    public static IReadOnlyList<string> BuildSlotMacroLines(RaidFlowDocument plan, PartySlot slot)
    {
        plan.Normalize();

        return plan.Events
            .OrderBy(timelineEvent => timelineEvent.TimeSeconds)
            .Select(timelineEvent => BuildEventMacroLine(plan, slot, timelineEvent))
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();
    }

    private static string BuildEventMacroLine(RaidFlowDocument plan, PartySlot slot, TimelineEvent timelineEvent)
    {
        var actionNames = timelineEvent.Assignments
            .Where(assignment => assignment.Slot == slot)
            .OrderBy(assignment => assignment.UseOffsetSeconds)
            .ThenBy(assignment => assignment.ActionId)
            .Select(assignment => FormatActionName(plan, assignment))
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (actionNames.Count == 0)
        {
            return string.Empty;
        }

        var line = $"/p {SanitizeMacroPart(timelineEvent.Name)} {string.Join(" / ", actionNames)}";
        return line.Length <= MaxMacroLineLength ? line : line[..MaxMacroLineLength];
    }

    private static string FormatActionName(RaidFlowDocument plan, MitigationAssignment assignment)
    {
        var action = MitigationCatalog.FindAction(assignment.ActionId, plan.ContentLevel);
        return SanitizeMacroPart(action?.ShortName ?? assignment.ActionId.ToString());
    }

    private static string SanitizeMacroPart(string value)
    {
        return value
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Trim();
    }
}
