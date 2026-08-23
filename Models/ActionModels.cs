namespace RaidFlow.Models;

public sealed class MitigationActionDefinition
{
    public uint ActionId { get; init; }

    public uint CanonicalActionId { get; init; }

    public string Name { get; init; } = string.Empty;

    public string ShortName { get; init; } = string.Empty;

    public string Job { get; init; } = string.Empty;

    public PartyRole Role { get; init; }

    public float DurationSeconds { get; init; }

    public float CooldownSeconds { get; init; }

    public int MaxCharges { get; init; } = 1;

    public float DefaultUseOffsetSeconds { get; init; } = -3;

    public bool IsRaidMitigation { get; init; }

    public int RequiredLevel { get; init; } = 1;

    public IReadOnlyList<MitigationActionVariant> LevelVariants { get; init; } = [];
}

public sealed class MitigationActionVariant
{
    public uint ActionId { get; init; }

    public string Name { get; init; } = string.Empty;

    public string ShortName { get; init; } = string.Empty;

    public float? DurationSeconds { get; init; }

    public float? CooldownSeconds { get; init; }

    public float? DefaultUseOffsetSeconds { get; init; }

    public int? MaxCharges { get; init; }

    public int RequiredLevel { get; init; } = 1;
}

public sealed class AssignmentView
{
    public required TimelineEvent Event { get; init; }

    public required MitigationAssignment Assignment { get; init; }

    public required MitigationActionDefinition Action { get; init; }

    public float UseTimeSeconds => this.Event.TimeSeconds + this.Assignment.UseOffsetSeconds;

    public float EndsAtSeconds => this.UseTimeSeconds + this.Action.DurationSeconds;
}

public sealed class PlanWarning
{
    public required string Message { get; init; }

    public string? EventId { get; init; }

    public PartySlot? Slot { get; init; }
}
