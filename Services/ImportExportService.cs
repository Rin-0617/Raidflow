using System.Text.Json;
using System.Text.Json.Serialization;
using RaidFlow.Models;

namespace RaidFlow.Services;

public static class ImportExportService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static string ExportFullPlan(RaidFlowDocument plan)
    {
        return JsonSerializer.Serialize(new FullPlanExport { Plan = plan }, JsonOptions);
    }

    public static string ExportPersonal(RaidFlowDocument plan, PartySlot slot)
    {
        var profile = plan.Party.First(member => member.Slot == slot);
        var export = new PersonalPlanExport
        {
            TimelineId = plan.TimelineId,
            TimelineHash = TimelineHasher.Compute(plan),
            ContentName = plan.ContentName,
            Revision = plan.Revision,
            Slot = slot,
            PlayerName = profile.PlayerName,
            Job = profile.Job,
            Assignments = plan.Events
                .SelectMany(timelineEvent => timelineEvent.Assignments
                    .Where(assignment => assignment.Slot == slot)
                    .Select(assignment => new PersonalAssignmentExport
                    {
                        EventId = timelineEvent.Id,
                        ActionId = assignment.ActionId,
                        UseOffsetSeconds = assignment.UseOffsetSeconds,
                        Note = assignment.Note,
                    }))
                .ToList(),
        };

        return JsonSerializer.Serialize(export, JsonOptions);
    }

    public static ImportResult ImportInto(RaidFlowDocument currentPlan, string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return ImportResult.Failed("インポート欄が空です。");
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var kind = document.RootElement.TryGetProperty("Kind", out var kindElement)
                ? kindElement.GetString()
                : string.Empty;

            return kind switch
            {
                "RaidFlow.FullPlan" => ImportFullPlan(currentPlan, json),
                "RaidFlow.PersonalPlan" => MergePersonalPlan(currentPlan, json),
                _ => ImportResult.Failed("RaidFlowのエクスポート形式を判別できません。"),
            };
        }
        catch (JsonException exception)
        {
            return ImportResult.Failed($"JSONが不正です: {exception.Message}");
        }
    }

    private static ImportResult ImportFullPlan(RaidFlowDocument currentPlan, string json)
    {
        var export = JsonSerializer.Deserialize<FullPlanExport>(json, JsonOptions);
        if (export?.Plan is null)
        {
            return ImportResult.Failed("全体プランのエクスポート内容が空です。");
        }

        currentPlan.TimelineId = export.Plan.TimelineId;
        currentPlan.ContentName = export.Plan.ContentName;
        currentPlan.Revision = export.Plan.Revision;
        currentPlan.UpdatedAtUtc = export.Plan.UpdatedAtUtc;
        currentPlan.Party = export.Plan.Party;
        currentPlan.Events = export.Plan.Events;
        currentPlan.Normalize();

        return new ImportResult
        {
            Success = true,
            Message = $"{currentPlan.ContentName} の全体プランをインポートしました。",
        };
    }

    private static ImportResult MergePersonalPlan(RaidFlowDocument currentPlan, string json)
    {
        var export = JsonSerializer.Deserialize<PersonalPlanExport>(json, JsonOptions);
        if (export is null)
        {
            return ImportResult.Failed("個人プランのエクスポート内容が空です。");
        }

        var message = string.Empty;
        var currentHash = TimelineHasher.Compute(currentPlan);
        if (!string.Equals(export.TimelineHash, currentHash, StringComparison.OrdinalIgnoreCase))
        {
            message = "タイムラインのハッシュが異なります。可能な範囲でイベントIDにより合成しました。";
        }

        var profile = currentPlan.Party.FirstOrDefault(member => member.Slot == export.Slot);
        if (profile is not null)
        {
            profile.PlayerName = export.PlayerName;
            profile.Job = export.Job;
        }

        var added = 0;
        var replaced = 0;
        foreach (var exportedAssignment in export.Assignments)
        {
            var timelineEvent = currentPlan.Events.FirstOrDefault(item => item.Id == exportedAssignment.EventId);
            if (timelineEvent is null)
            {
                continue;
            }

            var existing = timelineEvent.Assignments.FirstOrDefault(assignment =>
                assignment.Slot == export.Slot &&
                assignment.ActionId == exportedAssignment.ActionId);

            if (existing is not null)
            {
                existing.Job = export.Job;
                existing.UseOffsetSeconds = exportedAssignment.UseOffsetSeconds;
                existing.Note = exportedAssignment.Note;
                replaced++;
                continue;
            }

            timelineEvent.Assignments.Add(new MitigationAssignment
            {
                Slot = export.Slot,
                Job = export.Job,
                ActionId = exportedAssignment.ActionId,
                UseOffsetSeconds = exportedAssignment.UseOffsetSeconds,
                Note = exportedAssignment.Note,
            });
            added++;
        }

        return new ImportResult
        {
            Success = true,
            AddedAssignments = added,
            ReplacedAssignments = replaced,
            Message = $"{message}{export.Slot}/{export.Job} を合成: 追加 {added}件、更新 {replaced}件。",
        };
    }
}
