using RaidFlow.Data;
using RaidFlow.Models;

namespace RaidFlow.Services;

public static class PlannerValidator
{
    public static IReadOnlyList<PlanWarning> Validate(RaidFlowDocument plan)
    {
        var warnings = new List<PlanWarning>();
        var assignmentViews = BuildAssignmentViews(plan);

        foreach (var view in assignmentViews)
        {
            if (view.UseTimeSeconds > view.Event.TimeSeconds)
            {
                warnings.Add(new PlanWarning
                {
                    EventId = view.Event.Id,
                    Slot = view.Assignment.Slot,
                    Message = $"{view.Assignment.Slot} {view.Action.Name} の開始が {view.Event.Name} より後になっています。",
                });
            }

            if (view.EndsAtSeconds < view.Event.TimeSeconds)
            {
                warnings.Add(new PlanWarning
                {
                    EventId = view.Event.Id,
                    Slot = view.Assignment.Slot,
                    Message = $"{view.Assignment.Slot} {view.Action.Name} が {view.Event.Name} の前に切れます。",
                });
            }
        }

        foreach (var group in assignmentViews.GroupBy(view => new
        {
            view.Assignment.Slot,
            ActionId = view.Action.CanonicalActionId == 0 ? view.Action.ActionId : view.Action.CanonicalActionId,
        }))
        {
            var chargeRestoreTimes = new Queue<float>();
            foreach (var current in group.OrderBy(view => view.UseTimeSeconds))
            {
                while (chargeRestoreTimes.Count > 0 &&
                       chargeRestoreTimes.Peek() <= current.UseTimeSeconds + 0.01f)
                {
                    chargeRestoreTimes.Dequeue();
                }

                if (chargeRestoreTimes.Count >= Math.Max(1, current.Action.MaxCharges))
                {
                    var availableAt = chargeRestoreTimes.Peek();
                    warnings.Add(new PlanWarning
                    {
                        EventId = current.Event.Id,
                        Slot = current.Assignment.Slot,
                        Message = $"{current.Assignment.Slot} {current.Action.Name} のリキャストが {FormatSeconds(availableAt - current.UseTimeSeconds)} 足りません。",
                    });
                }

                chargeRestoreTimes.Enqueue(current.UseTimeSeconds + current.Action.CooldownSeconds);
            }
        }

        foreach (var timelineEvent in plan.Events.Where(timelineEvent => timelineEvent.Type is TimelineEventType.Raidwide or TimelineEventType.Tankbuster))
        {
            if (timelineEvent.Assignments.Count == 0)
            {
                warnings.Add(new PlanWarning
                {
                    EventId = timelineEvent.Id,
                    Message = $"{timelineEvent.Name} に軽減担当がありません。",
                });
            }
        }

        return warnings;
    }

    public static IReadOnlyList<AssignmentView> BuildAssignmentViews(RaidFlowDocument plan)
    {
        return plan.Events
            .SelectMany(timelineEvent => timelineEvent.Assignments.Select(assignment => new { timelineEvent, assignment }))
            .Select(item => new AssignmentView
            {
                Event = item.timelineEvent,
                Assignment = item.assignment,
                Action = MitigationCatalog.FindAction(item.assignment.ActionId, plan.ContentLevel) ?? new MitigationActionDefinition
                {
                    ActionId = item.assignment.ActionId,
                    CanonicalActionId = item.assignment.ActionId,
                    Name = $"アクション {item.assignment.ActionId}",
                    ShortName = item.assignment.ActionId.ToString(),
                    DurationSeconds = 0,
                    CooldownSeconds = 0,
                },
            })
            .ToList();
    }

    public static string FormatTimestamp(float seconds)
    {
        var clamped = Math.Max(0, seconds);
        var minutes = (int)(clamped / 60);
        var remainingSeconds = clamped - (minutes * 60);
        return $"{minutes:00}:{remainingSeconds:00.0}";
    }

    private static string FormatSeconds(float seconds)
    {
        return $"{Math.Ceiling(seconds):0}s";
    }
}
