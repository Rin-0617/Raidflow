namespace RaidFlow.Models;

public sealed class FFLogsSettings
{
    public string ClientId { get; set; } = string.Empty;

    public string ClientSecret { get; set; } = string.Empty;

    public string AccessToken { get; set; } = string.Empty;

    public DateTimeOffset AccessTokenExpiresAtUtc { get; set; }

    public string ReportUrl { get; set; } = string.Empty;

    public int FightId { get; set; }

    public bool ReplaceTimelineOnImport { get; set; } = true;

    public bool ImportMitigationAssignments { get; set; } = true;

    public int MaxImportedEvents { get; set; } = 250;
}

public sealed class FFLogsImportRequest
{
    public string ClientId { get; init; } = string.Empty;

    public string ClientSecret { get; init; } = string.Empty;

    public string AccessToken { get; init; } = string.Empty;

    public DateTimeOffset AccessTokenExpiresAtUtc { get; init; }

    public string ReportUrl { get; init; } = string.Empty;

    public int FightId { get; init; }

    public int ContentLevel { get; init; } = 100;

    public IReadOnlyList<PartyMemberProfile> Party { get; init; } = [];

    public bool ImportMitigationAssignments { get; init; } = true;

    public IReadOnlyDictionary<uint, string> LocalizedActionNames { get; init; } = new Dictionary<uint, string>();

    public IReadOnlyDictionary<string, string> EnglishToLocalizedActionNames { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}

public sealed class FFLogsImportResult
{
    public bool Success { get; init; }

    public string Message { get; init; } = string.Empty;

    public string ReportTitle { get; init; } = string.Empty;

    public string FightName { get; init; } = string.Empty;

    public int FightId { get; init; }

    public int ImportedMitigationAssignments { get; init; }

    public string? AccessToken { get; init; }

    public DateTimeOffset AccessTokenExpiresAtUtc { get; init; }

    public List<TimelineEvent> Events { get; init; } = [];

    public static FFLogsImportResult Failed(string message)
    {
        return new FFLogsImportResult { Success = false, Message = message };
    }
}
