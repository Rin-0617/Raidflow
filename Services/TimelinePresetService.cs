using System.Text.Json;
using System.Text.Json.Serialization;
using RaidFlow.Models;

namespace RaidFlow.Services;

public static class TimelinePresetService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static IReadOnlyList<TimelinePresetSummary> LoadSummaries()
    {
        return LoadPresetFiles()
            .Select(preset => new TimelinePresetSummary(
                preset.Id,
                preset.ContentName,
                preset.Revision,
                preset.ContentLevel,
                preset.Events.Count))
            .OrderBy(summary => summary.ContentLevel)
            .ThenBy(summary => summary.ContentName, StringComparer.Ordinal)
            .ToList();
    }

    public static TimelinePresetLoadResult ApplyPreset(RaidFlowDocument plan, string presetId)
    {
        var preset = LoadPresetFiles().FirstOrDefault(candidate =>
            string.Equals(candidate.Id, presetId, StringComparison.Ordinal));
        if (preset is null)
        {
            return TimelinePresetLoadResult.Failed("プリセットTLが見つかりませんでした。");
        }

        plan.TimelineId = Guid.NewGuid().ToString("N");
        plan.ContentName = preset.ContentName;
        plan.Revision = preset.Revision;
        plan.ContentLevel = preset.ContentLevel;
        plan.Events = preset.Events
            .OrderBy(timelineEvent => timelineEvent.TimeSeconds)
            .ThenBy(timelineEvent => timelineEvent.Name, StringComparer.Ordinal)
            .Select(timelineEvent => new TimelineEvent
            {
                Id = string.IsNullOrWhiteSpace(timelineEvent.Id)
                    ? $"evt_{Guid.NewGuid():N}"
                    : timelineEvent.Id,
                TimeSeconds = timelineEvent.TimeSeconds,
                Name = timelineEvent.Name,
                Type = timelineEvent.Type,
                Notes = timelineEvent.Notes,
                Assignments = [],
            })
            .ToList();

        plan.Normalize();
        return new TimelinePresetLoadResult(
            true,
            $"{preset.ContentName} のプリセットTLを読み込みました。{plan.Events.Count}件。",
            new TimelinePresetSummary(
                preset.Id,
                preset.ContentName,
                preset.Revision,
                preset.ContentLevel,
                preset.Events.Count));
    }

    private static IReadOnlyList<TimelinePresetFile> LoadPresetFiles()
    {
        var directory = PresetDirectory();
        if (!Directory.Exists(directory))
        {
            return [];
        }

        var presets = new List<TimelinePresetFile>();
        foreach (var filePath in Directory.EnumerateFiles(directory, "*.json"))
        {
            try
            {
                var json = File.ReadAllText(filePath);
                var preset = JsonSerializer.Deserialize<TimelinePresetFile>(json, JsonOptions);
                if (preset is not null &&
                    !string.IsNullOrWhiteSpace(preset.Id) &&
                    !string.IsNullOrWhiteSpace(preset.ContentName) &&
                    preset.Events.Count > 0)
                {
                    presets.Add(preset);
                }
            }
            catch
            {
                // Ignore malformed preset files so one bad file does not block the plugin UI.
            }
        }

        return presets;
    }

    private static string PresetDirectory()
    {
        var assemblyDirectory = Path.GetDirectoryName(typeof(TimelinePresetService).Assembly.Location);
        return Path.Combine(assemblyDirectory ?? AppContext.BaseDirectory, "Data", "TimelinePresets");
    }

    private sealed class TimelinePresetFile
    {
        public string Id { get; set; } = string.Empty;

        public string ContentName { get; set; } = string.Empty;

        public string Revision { get; set; } = "preset";

        public int ContentLevel { get; set; } = 100;

        public List<TimelinePresetEvent> Events { get; set; } = [];
    }

    private sealed class TimelinePresetEvent
    {
        public string Id { get; set; } = string.Empty;

        public float TimeSeconds { get; set; }

        public string Name { get; set; } = string.Empty;

        public TimelineEventType Type { get; set; } = TimelineEventType.Mechanic;

        public string Notes { get; set; } = string.Empty;
    }
}

public sealed record TimelinePresetSummary(
    string Id,
    string ContentName,
    string Revision,
    int ContentLevel,
    int EventCount)
{
    public string DisplayName => $"{this.ContentName} / {this.Revision}";
}

public sealed record TimelinePresetLoadResult(
    bool Success,
    string Message,
    TimelinePresetSummary? Preset)
{
    public static TimelinePresetLoadResult Failed(string message)
    {
        return new TimelinePresetLoadResult(false, message, null);
    }
}
