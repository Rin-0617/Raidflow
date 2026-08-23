namespace RaidFlow.Models;

public sealed class FullPlanExport
{
    public string Kind { get; set; } = "RaidFlow.FullPlan";

    public int FormatVersion { get; set; } = 1;

    public required RaidFlowDocument Plan { get; set; }
}

public sealed class PersonalPlanExport
{
    public string Kind { get; set; } = "RaidFlow.PersonalPlan";

    public int FormatVersion { get; set; } = 1;

    public string TimelineId { get; set; } = string.Empty;

    public string TimelineHash { get; set; } = string.Empty;

    public string ContentName { get; set; } = string.Empty;

    public string Revision { get; set; } = string.Empty;

    public PartySlot Slot { get; set; }

    public string PlayerName { get; set; } = string.Empty;

    public string Job { get; set; } = string.Empty;

    public List<PersonalAssignmentExport> Assignments { get; set; } = [];
}

public sealed class PersonalAssignmentExport
{
    public string EventId { get; set; } = string.Empty;

    public uint ActionId { get; set; }

    public float UseOffsetSeconds { get; set; }

    public string Note { get; set; } = string.Empty;
}

public sealed class ImportResult
{
    public bool Success { get; init; }

    public string Message { get; init; } = string.Empty;

    public int AddedAssignments { get; init; }

    public int ReplacedAssignments { get; init; }

    public static ImportResult Failed(string message)
    {
        return new ImportResult { Success = false, Message = message };
    }
}
