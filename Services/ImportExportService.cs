using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using RaidFlow.Models;

namespace RaidFlow.Services;

public static class ImportExportService
{
    public const string FileExtension = ".radflow";
    public const string FileDialogFilter = "RaidFlow (*.radflow){.radflow},.*";

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

    public static string DefaultFullPlanFileName(RaidFlowDocument plan)
    {
        return $"{SanitizeFileName(plan.ContentName)}_{SanitizeFileName(plan.Revision)}";
    }

    public static string DefaultPersonalPlanFileName(RaidFlowDocument plan, PartySlot slot)
    {
        var profile = plan.Party.First(member => member.Slot == slot);
        var playerName = string.IsNullOrWhiteSpace(profile.PlayerName)
            ? slot.ToString()
            : profile.PlayerName;

        return $"{SanitizeFileName(plan.ContentName)}_{SanitizeFileName(plan.Revision)}_{slot}_{SanitizeFileName(profile.Job)}_{SanitizeFileName(playerName)}";
    }

    public static string SaveFullPlanToFile(RaidFlowDocument plan, string filePath)
    {
        var exportPath = EnsureFileExtension(filePath);
        File.WriteAllText(exportPath, ExportFullPlan(plan), Encoding.UTF8);
        return $"{Path.GetFileName(exportPath)} に全体プランを保存しました。";
    }

    public static string SavePersonalPlanToFile(RaidFlowDocument plan, PartySlot slot, string filePath)
    {
        var exportPath = EnsureFileExtension(filePath);
        File.WriteAllText(exportPath, ExportPersonal(plan, slot), Encoding.UTF8);
        return $"{Path.GetFileName(exportPath)} に {slot} の個人プランを保存しました。";
    }

    public static ImportResult ImportFileInto(RaidFlowDocument currentPlan, string filePath)
    {
        try
        {
            var json = File.ReadAllText(filePath, Encoding.UTF8);
            return ImportInto(currentPlan, json);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return ImportResult.Failed($"ファイル読み込みに失敗しました: {exception.Message}");
        }
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

    private static string EnsureFileExtension(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return $"RaidFlow{FileExtension}";
        }

        return string.Equals(Path.GetExtension(filePath), FileExtension, StringComparison.OrdinalIgnoreCase)
            ? filePath
            : $"{filePath}{FileExtension}";
    }

    private static string SanitizeFileName(string value)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(value
            .Select(character => invalidChars.Contains(character) ? '_' : character)
            .ToArray())
            .Trim(' ', '.', '_');

        return string.IsNullOrWhiteSpace(sanitized) ? "RaidFlow" : sanitized;
    }

    private static ImportResult ImportFullPlan(RaidFlowDocument currentPlan, string json)
    {
        var export = JsonSerializer.Deserialize<FullPlanExport>(json, JsonOptions);
        if (export?.Plan is null)
        {
            return ImportResult.Failed("全体プランのエクスポート内容が空です。");
        }

        var preservedAssignments = currentPlan.Events
            .Where(timelineEvent => timelineEvent.Assignments.Count > 0)
            .ToDictionary(
                timelineEvent => timelineEvent.Id,
                timelineEvent => timelineEvent.Assignments
                    .Select(CloneAssignment)
                    .ToList());
        var preserved = 0;

        currentPlan.TimelineId = export.Plan.TimelineId;
        currentPlan.ContentName = export.Plan.ContentName;
        currentPlan.Revision = export.Plan.Revision;
        currentPlan.UpdatedAtUtc = export.Plan.UpdatedAtUtc;
        currentPlan.Party = export.Plan.Party;
        currentPlan.Events = export.Plan.Events;

        foreach (var timelineEvent in currentPlan.Events)
        {
            if (!preservedAssignments.TryGetValue(timelineEvent.Id, out var assignments))
            {
                continue;
            }

            foreach (var assignment in assignments)
            {
                if (HasEquivalentAssignment(timelineEvent.Assignments, assignment))
                {
                    continue;
                }

                timelineEvent.Assignments.Add(assignment);
                preserved++;
            }
        }

        currentPlan.Normalize();

        return new ImportResult
        {
            Success = true,
            AddedAssignments = preserved,
            Message = preserved > 0
                ? $"{currentPlan.ContentName} の全体プランをインポートしました。既存の軽減 {preserved}件を保持しました。"
                : $"{currentPlan.ContentName} の全体プランをインポートしました。",
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

    private static MitigationAssignment CloneAssignment(MitigationAssignment assignment)
    {
        return new MitigationAssignment
        {
            Id = assignment.Id,
            Slot = assignment.Slot,
            Job = assignment.Job,
            ActionId = assignment.ActionId,
            UseOffsetSeconds = assignment.UseOffsetSeconds,
            Note = assignment.Note,
        };
    }

    private static bool HasEquivalentAssignment(IEnumerable<MitigationAssignment> assignments, MitigationAssignment candidate)
    {
        return assignments.Any(assignment =>
            assignment.Slot == candidate.Slot &&
            assignment.ActionId == candidate.ActionId &&
            Math.Abs(assignment.UseOffsetSeconds - candidate.UseOffsetSeconds) < 0.001f &&
            string.Equals(assignment.Note, candidate.Note, StringComparison.Ordinal));
    }
}
