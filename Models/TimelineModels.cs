namespace RaidFlow.Models;

public enum TimelineEventType
{
    Raidwide,
    Tankbuster,
    Stack,
    Spread,
    Mechanic,
    Downtime,
    Burst,
}

public sealed class RaidFlowDocument
{
    public string TimelineId { get; set; } = Guid.NewGuid().ToString("N");

    public string ContentName { get; set; } = "新規コンテンツ";

    public string Revision { get; set; } = "v1";

    public int ContentLevel { get; set; } = 100;

    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public List<PartyMemberProfile> Party { get; set; } = [];

    public List<TimelineEvent> Events { get; set; } = [];

    public static RaidFlowDocument CreateDefault()
    {
        var plan = new RaidFlowDocument();
        plan.Party =
        [
            new() { Slot = PartySlot.MT, Job = "PLD" },
            new() { Slot = PartySlot.ST, Job = "DRK" },
            new() { Slot = PartySlot.H1, Job = "WHM" },
            new() { Slot = PartySlot.H2, Job = "SCH" },
            new() { Slot = PartySlot.D1, Job = "MNK" },
            new() { Slot = PartySlot.D2, Job = "NIN" },
            new() { Slot = PartySlot.D3, Job = "BRD" },
            new() { Slot = PartySlot.D4, Job = "RDM" },
        ];

        plan.Events =
        [
            new()
            {
                Id = "evt_0001",
                TimeSeconds = 30,
                Name = "最初の全体攻撃",
                Type = TimelineEventType.Raidwide,
            },
            new()
            {
                Id = "evt_0002",
                TimeSeconds = 75,
                Name = "タンク強攻撃",
                Type = TimelineEventType.Tankbuster,
            },
        ];

        return plan;
    }

    public bool Normalize()
    {
        var changed = false;
        var originalPartyCount = this.Party.Count;
        var normalizedParty = new List<PartyMemberProfile>();

        if (this.ContentLevel <= 0)
        {
            this.ContentLevel = 100;
            changed = true;
        }

        this.ContentLevel = Math.Clamp(this.ContentLevel, 1, 120);

        foreach (var slot in Enum.GetValues<PartySlot>())
        {
            var candidates = this.Party.Where(member => member.Slot == slot).ToList();
            var playerName = candidates
                .Select(member => member.PlayerName)
                .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
            var job = candidates
                .Select(member => member.Job)
                .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;

            normalizedParty.Add(new PartyMemberProfile
            {
                Slot = slot,
                PlayerName = playerName,
                Job = job,
            });
        }

        changed = originalPartyCount != normalizedParty.Count ||
                  this.Party.Where((member, index) =>
                      index >= normalizedParty.Count ||
                      member.Slot != normalizedParty[index].Slot ||
                      member.PlayerName != normalizedParty[index].PlayerName ||
                      member.Job != normalizedParty[index].Job).Any();
        this.Party = normalizedParty;

        foreach (var timelineEvent in this.Events)
        {
            if (string.IsNullOrWhiteSpace(timelineEvent.Id))
            {
                timelineEvent.Id = $"evt_{Guid.NewGuid():N}";
                changed = true;
            }

            if (timelineEvent.Assignments is null)
            {
                timelineEvent.Assignments = [];
                changed = true;
            }

            foreach (var assignment in timelineEvent.Assignments)
            {
                if (string.IsNullOrWhiteSpace(assignment.Id))
                {
                    assignment.Id = $"asg_{Guid.NewGuid():N}";
                    changed = true;
                }

                assignment.Job ??= string.Empty;
                assignment.Note ??= string.Empty;
            }
        }

        var orderedEvents = this.Events.OrderBy(timelineEvent => timelineEvent.TimeSeconds).ToList();
        if (!this.Events.SequenceEqual(orderedEvents))
        {
            changed = true;
        }

        this.Events = orderedEvents;
        return changed;
    }
}

public sealed class TimelineEvent
{
    public string Id { get; set; } = $"evt_{Guid.NewGuid():N}";

    public float TimeSeconds { get; set; }

    public string Name { get; set; } = "新規イベント";

    public TimelineEventType Type { get; set; } = TimelineEventType.Mechanic;

    public string Notes { get; set; } = string.Empty;

    public List<MitigationAssignment> Assignments { get; set; } = [];
}

public sealed class MitigationAssignment
{
    public string Id { get; set; } = $"asg_{Guid.NewGuid():N}";

    public PartySlot Slot { get; set; }

    public string Job { get; set; } = string.Empty;

    public uint ActionId { get; set; }

    public float UseOffsetSeconds { get; set; } = -3;

    public string Note { get; set; } = string.Empty;
}
