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
        return LoadPresets()
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
        var preset = LoadPresets().FirstOrDefault(candidate =>
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

    private static IReadOnlyList<TimelinePresetFile> LoadPresets()
    {
        var presets = new Dictionary<string, TimelinePresetFile>(StringComparer.Ordinal);

        foreach (var preset in LoadEmbeddedPresets())
        {
            presets[preset.Id] = preset;
        }

        foreach (var preset in LoadFilePresets())
        {
            presets[preset.Id] = preset;
        }

        return presets.Values.ToList();
    }

    private static IEnumerable<TimelinePresetFile> LoadEmbeddedPresets()
    {
        var assembly = typeof(TimelinePresetService).Assembly;
        const string resourcePrefix = "RaidFlow.Data.TimelinePresets.";

        foreach (var resourceName in assembly.GetManifestResourceNames()
                     .Where(name => name.StartsWith(resourcePrefix, StringComparison.Ordinal) &&
                                    name.EndsWith(".json", StringComparison.OrdinalIgnoreCase)))
        {
            TimelinePresetFile? preset = null;
            try
            {
                using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream is null)
                {
                    continue;
                }

                using var reader = new StreamReader(stream);
                preset = DeserializePreset(reader.ReadToEnd());
            }
            catch
            {
                // Ignore malformed preset resources so one bad file does not block the plugin UI.
            }

            if (preset is not null)
            {
                yield return preset;
            }
        }
    }

    private static IEnumerable<TimelinePresetFile> LoadFilePresets()
    {
        var directory = PresetDirectory();
        if (!Directory.Exists(directory))
        {
            yield break;
        }

        foreach (var filePath in Directory.EnumerateFiles(directory, "*.json"))
        {
            TimelinePresetFile? preset = null;
            try
            {
                var json = File.ReadAllText(filePath);
                preset = DeserializePreset(json);
            }
            catch
            {
                // Ignore malformed preset files so one bad file does not block the plugin UI.
            }

            if (preset is not null)
            {
                yield return preset;
            }
        }
    }

    private static TimelinePresetFile? DeserializePreset(string json)
    {
        var preset = JsonSerializer.Deserialize<TimelinePresetFile>(json, JsonOptions);
        if (preset is null ||
            string.IsNullOrWhiteSpace(preset.Id) ||
            string.IsNullOrWhiteSpace(preset.ContentName) ||
            preset.Events.Count == 0)
        {
            return null;
        }

        return preset;
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
