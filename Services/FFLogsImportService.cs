using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using RaidFlow.Models;

namespace RaidFlow.Services;

public static partial class FFLogsImportService
{
    private const string TokenEndpoint = "https://www.fflogs.com/oauth/token";
    private const string GraphQlEndpoint = "https://www.fflogs.com/api/v2/client";
    private const string AcceptLanguage = "ja-JP,ja;q=0.9,en;q=0.8";
    private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromSeconds(45) };
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static async Task<FFLogsImportResult> ImportTimelineAsync(
        FFLogsImportRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!TryParseReportUrl(request.ReportUrl, out var reportCode, out var urlFightId, out var parseMessage))
        {
            return FFLogsImportResult.Failed(parseMessage);
        }

        var tokenResult = await GetAccessTokenAsync(request, cancellationToken).ConfigureAwait(false);
        if (!tokenResult.Success || string.IsNullOrWhiteSpace(tokenResult.AccessToken))
        {
            return FFLogsImportResult.Failed(tokenResult.Message);
        }

        var report = await GetReportFightsAsync(reportCode, tokenResult.AccessToken, cancellationToken).ConfigureAwait(false);
        if (report.Fights.Count == 0)
        {
            return FFLogsImportResult.Failed("FFLogsレポート内にfightが見つかりませんでした。");
        }

        var requestedFightId = request.FightId > 0 ? request.FightId : urlFightId;
        var fight = SelectFight(report.Fights, requestedFightId);
        if (fight is null)
        {
            return FFLogsImportResult.Failed($"指定されたfight {requestedFightId} が見つかりませんでした。");
        }

        var metadata = await TryGetReportMetadataAsync(reportCode, tokenResult.AccessToken, cancellationToken)
            .ConfigureAwait(false);
        var damageEvents = await TryGetEnemyDamageEventsAsync(reportCode, tokenResult.AccessToken, fight, cancellationToken)
            .ConfigureAwait(false);
        var damageEventsByAbility = damageEvents
            .GroupBy(damageEvent => damageEvent.AbilityGameId)
            .ToDictionary(group => group.Key, group => group.OrderBy(item => item.Timestamp).ToList());

        var events = await GetEnemyCastEventsAsync(
                reportCode,
                tokenResult.AccessToken,
                fight,
                metadata,
                request.LocalizedActionNames,
                damageEventsByAbility,
                cancellationToken)
            .ConfigureAwait(false);

        if (events.Count == 0)
        {
            return FFLogsImportResult.Failed("敵のcastイベントを見つけられませんでした。レポートURL/fight/API権限を確認してください。");
        }

        return new FFLogsImportResult
        {
            Success = true,
            Message = $"{report.Title} / {fight.Name} から {events.Count} 件のcastを取り込みました。名前解決 {events.Count(item => !item.Name.StartsWith("Enemy Cast ", StringComparison.Ordinal))}件、種別推定 {events.Count(item => item.Type != TimelineEventType.Mechanic)}件。",
            ReportTitle = report.Title,
            FightName = fight.Name,
            FightId = fight.Id,
            AccessToken = tokenResult.AccessToken,
            AccessTokenExpiresAtUtc = tokenResult.ExpiresAtUtc,
            Events = events,
        };
    }

    private static bool TryParseReportUrl(string url, out string reportCode, out int fightId, out string message)
    {
        reportCode = string.Empty;
        fightId = 0;
        message = string.Empty;

        if (string.IsNullOrWhiteSpace(url))
        {
            message = "FFLogsのレポートURLを入力してください。";
            return false;
        }

        var normalizedUrl = url.Trim().Replace("\\&", "&");
        if (!normalizedUrl.Contains("://", StringComparison.Ordinal) &&
            normalizedUrl.Contains("fflogs.com/", StringComparison.OrdinalIgnoreCase))
        {
            normalizedUrl = $"https://{normalizedUrl}";
        }

        if (TryParseReportUri(normalizedUrl, out reportCode, out fightId))
        {
            return true;
        }

        var codeMatch = ReportCodeRegex().Match(normalizedUrl);
        if (!codeMatch.Success)
        {
            message = "URLからレポートコードを読み取れませんでした。例: https://ja.fflogs.com/reports/XXXXXXXX?fight=1";
            return false;
        }

        reportCode = codeMatch.Groups["code"].Value;

        fightId = ParseFightId(normalizedUrl);

        return true;
    }

    private static bool TryParseReportUri(string url, out string reportCode, out int fightId)
    {
        reportCode = string.Empty;
        fightId = 0;

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (!string.Equals(uri.Host, "fflogs.com", StringComparison.OrdinalIgnoreCase) &&
             !uri.Host.EndsWith(".fflogs.com", StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        var segments = uri.AbsolutePath
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var reportSegmentIndex = Array.FindIndex(segments, segment =>
            string.Equals(segment, "reports", StringComparison.OrdinalIgnoreCase));
        if (reportSegmentIndex < 0 || reportSegmentIndex + 1 >= segments.Length)
        {
            return false;
        }

        reportCode = Uri.UnescapeDataString(segments[reportSegmentIndex + 1]);
        if (!ReportCodeValueRegex().IsMatch(reportCode))
        {
            reportCode = string.Empty;
            return false;
        }

        fightId = ParseFightId($"{uri.Query}&{uri.Fragment}");
        return true;
    }

    private static int ParseFightId(string value)
    {
        var fightMatch = FightIdRegex().Match(value);
        return fightMatch.Success && int.TryParse(fightMatch.Groups["fight"].Value, out var parsedFightId)
            ? parsedFightId
            : 0;
    }

    private static async Task<TokenResult> GetAccessTokenAsync(
        FFLogsImportRequest request,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(request.AccessToken) &&
            (request.AccessTokenExpiresAtUtc == default ||
             request.AccessTokenExpiresAtUtc > DateTimeOffset.UtcNow.AddMinutes(2)))
        {
            return new TokenResult(true, string.Empty, request.AccessToken, request.AccessTokenExpiresAtUtc);
        }

        if (string.IsNullOrWhiteSpace(request.ClientId) || string.IsNullOrWhiteSpace(request.ClientSecret))
        {
            return new TokenResult(false, "Client ID/Client Secret、または有効なAccess Tokenを入力してください。", string.Empty, default);
        }

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, TokenEndpoint);
        var basicToken = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{request.ClientId}:{request.ClientSecret}"));
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Basic", basicToken);
        httpRequest.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
        });

        using var response = await HttpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return new TokenResult(false, $"FFLogsトークン取得に失敗しました。HTTP {(int)response.StatusCode}: {TrimBody(body)}", string.Empty, default);
        }

        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        var token = GetString(root, "access_token");
        if (string.IsNullOrWhiteSpace(token))
        {
            return new TokenResult(false, "FFLogsトークン応答にaccess_tokenがありませんでした。", string.Empty, default);
        }

        var expiresIn = Math.Max(60, GetInt(root, "expires_in", 7200));
        var expiresAt = DateTimeOffset.UtcNow.AddSeconds(expiresIn - 60);
        return new TokenResult(true, string.Empty, token, expiresAt);
    }

    private static async Task<ReportInfo> GetReportFightsAsync(
        string reportCode,
        string accessToken,
        CancellationToken cancellationToken)
    {
        const string query = """
            query ReportFights($code: String!) {
              reportData {
                report(code: $code) {
                  title
                  fights {
                    id
                    name
                    startTime
                    endTime
                    kill
                    encounterID
                  }
                }
              }
            }
            """;

        using var document = await SendGraphQlAsync(
                accessToken,
                query,
                new Dictionary<string, object?> { ["code"] = reportCode },
                cancellationToken)
            .ConfigureAwait(false);

        var reportElement = document.RootElement
            .GetProperty("data")
            .GetProperty("reportData")
            .GetProperty("report");

        var title = GetString(reportElement, "title");
        var fights = new List<ReportFight>();
        if (TryGetProperty(reportElement, "fights", out var fightsElement) &&
            fightsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var fightElement in fightsElement.EnumerateArray())
            {
                var id = GetInt(fightElement, "id");
                var startTime = GetLong(fightElement, "startTime");
                var endTime = GetLong(fightElement, "endTime");
                if (id <= 0 || endTime <= startTime)
                {
                    continue;
                }

                fights.Add(new ReportFight(
                    id,
                    GetString(fightElement, "name", $"Fight {id}"),
                    startTime,
                    endTime,
                    GetBool(fightElement, "kill"),
                    GetInt(fightElement, "encounterID")));
            }
        }

        return new ReportInfo(string.IsNullOrWhiteSpace(title) ? reportCode : title, fights);
    }

    private static async Task<ReportMetadata> TryGetReportMetadataAsync(
        string reportCode,
        string accessToken,
        CancellationToken cancellationToken)
    {
        try
        {
            var translated = await GetReportMetadataAsync(reportCode, accessToken, true, cancellationToken).ConfigureAwait(false);
            if (!translated.HasRsvAbilityNames)
            {
                return translated;
            }

            try
            {
                var fallback = await GetReportMetadataAsync(reportCode, accessToken, false, cancellationToken).ConfigureAwait(false);
                return translated.WithFallbackAbilities(fallback);
            }
            catch
            {
                return translated;
            }
        }
        catch
        {
            try
            {
                return await GetReportMetadataAsync(reportCode, accessToken, false, cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                return ReportMetadata.Empty;
            }
        }
    }

    private static async Task<ReportMetadata> GetReportMetadataAsync(
        string reportCode,
        string accessToken,
        bool translate,
        CancellationToken cancellationToken)
    {
        var masterDataField = translate ? "masterData(translate: true)" : "masterData";
        var query = $$"""
            query ReportMetadata($code: String!) {
              reportData {
                report(code: $code) {
                  {{masterDataField}} {
                    abilities {
                      gameID
                      name
                    }
                    actors {
                      id
                      name
                      type
                      subType
                    }
                  }
                }
              }
            }
            """;

        using var document = await SendGraphQlAsync(
                accessToken,
                query,
                new Dictionary<string, object?> { ["code"] = reportCode },
                cancellationToken)
            .ConfigureAwait(false);

        var reportElement = document.RootElement
            .GetProperty("data")
            .GetProperty("reportData")
            .GetProperty("report");

        if (!TryGetProperty(reportElement, "masterData", out var masterDataElement))
        {
            return ReportMetadata.Empty;
        }

        var abilities = new Dictionary<uint, ReportAbility>();
        if (TryGetProperty(masterDataElement, "abilities", out var abilitiesElement) &&
            abilitiesElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var abilityElement in abilitiesElement.EnumerateArray())
            {
                var gameId = GetUInt(abilityElement, "gameID");
                var name = GetString(abilityElement, "name");
                if (gameId != 0 && !string.IsNullOrWhiteSpace(name))
                {
                    abilities[gameId] = new ReportAbility(gameId, name);
                }
            }
        }

        var actors = new Dictionary<int, ReportActor>();
        if (TryGetProperty(masterDataElement, "actors", out var actorsElement) &&
            actorsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var actorElement in actorsElement.EnumerateArray())
            {
                var id = GetInt(actorElement, "id");
                if (id <= 0)
                {
                    continue;
                }

                actors[id] = new ReportActor(
                    id,
                    GetString(actorElement, "name"),
                    GetString(actorElement, "type"),
                    GetString(actorElement, "subType"));
            }
        }

        return new ReportMetadata(abilities, actors);
    }

    private static ReportFight? SelectFight(IReadOnlyList<ReportFight> fights, int requestedFightId)
    {
        if (requestedFightId > 0)
        {
            return fights.FirstOrDefault(fight => fight.Id == requestedFightId);
        }

        return fights.LastOrDefault(fight => fight.EncounterId > 0) ?? fights.LastOrDefault();
    }

    private static async Task<List<TimelineEvent>> GetEnemyCastEventsAsync(
        string reportCode,
        string accessToken,
        ReportFight fight,
        ReportMetadata metadata,
        IReadOnlyDictionary<uint, string> localizedActionNames,
        IReadOnlyDictionary<uint, List<DamageEvent>> damageEventsByAbility,
        CancellationToken cancellationToken)
    {
        var timelineEvents = new List<TimelineEvent>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var pageStartTime = fight.StartTime;
        var pageGuard = 0;
        var translatedEventsSupported = true;
        var requireDamageSummary = damageEventsByAbility.Count > 0;

        while (pageStartTime < fight.EndTime && pageGuard++ < 500)
        {
            var page = await GetEnemyCastEventsPageAsync(
                    reportCode,
                    accessToken,
                    fight,
                    pageStartTime,
                    translatedEventsSupported,
                    cancellationToken)
                .ConfigureAwait(false);
            if (page.TranslationUnavailable)
            {
                translatedEventsSupported = false;
            }

            using var document = page.Document;
            var eventsElement = document.RootElement
                .GetProperty("data")
                .GetProperty("reportData")
                .GetProperty("report")
                .GetProperty("events");

            if (TryGetProperty(eventsElement, "data", out var dataElement))
            {
                AddCastEvents(
                    timelineEvents,
                    seen,
                    dataElement,
                    reportCode,
                    fight,
                    metadata,
                    localizedActionNames,
                    damageEventsByAbility,
                    requireDamageSummary);
            }

            var nextPageTimestamp = GetLong(eventsElement, "nextPageTimestamp");
            if (nextPageTimestamp <= pageStartTime || nextPageTimestamp >= fight.EndTime)
            {
                break;
            }

            pageStartTime = nextPageTimestamp;
        }

        return timelineEvents
            .OrderBy(timelineEvent => timelineEvent.TimeSeconds)
            .ThenBy(timelineEvent => timelineEvent.Name)
            .ToList();
    }

    private static async Task<EventsPage> GetEnemyCastEventsPageAsync(
        string reportCode,
        string accessToken,
        ReportFight fight,
        long pageStartTime,
        bool tryTranslate,
        CancellationToken cancellationToken)
    {
        if (!tryTranslate)
        {
            return new EventsPage(
                await QueryEnemyCastEventsPageAsync(reportCode, accessToken, fight, pageStartTime, false, cancellationToken)
                    .ConfigureAwait(false),
                false);
        }

        try
        {
            return new EventsPage(
                await QueryEnemyCastEventsPageAsync(reportCode, accessToken, fight, pageStartTime, true, cancellationToken)
                    .ConfigureAwait(false),
                false);
        }
        catch (InvalidOperationException exception) when (IsTranslateArgumentFailure(exception))
        {
            return new EventsPage(
                await QueryEnemyCastEventsPageAsync(reportCode, accessToken, fight, pageStartTime, false, cancellationToken)
                    .ConfigureAwait(false),
                true);
        }
    }

    private static Task<JsonDocument> QueryEnemyCastEventsPageAsync(
        string reportCode,
        string accessToken,
        ReportFight fight,
        long pageStartTime,
        bool translate,
        CancellationToken cancellationToken)
    {
        var translateArgument = translate ? "translate: true," : string.Empty;
        var query = $$"""
            query ReportCasts($code: String!) {
              reportData {
                report(code: $code) {
                  events(
                    {{translateArgument}}
                    fightIDs: [{{fight.Id}}],
                    startTime: {{pageStartTime}},
                    endTime: {{fight.EndTime}},
                    dataType: Casts,
                    hostilityType: Enemies,
                    limit: 10000
                  ) {
                    data
                    nextPageTimestamp
                  }
                }
              }
            }
            """;

        return SendGraphQlAsync(
            accessToken,
            query,
            new Dictionary<string, object?> { ["code"] = reportCode },
            cancellationToken);
    }

    private static bool IsTranslateArgumentFailure(Exception exception)
    {
        var message = exception.Message;
        return message.Contains("translate", StringComparison.OrdinalIgnoreCase) &&
               (message.Contains("Unknown argument", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("Unknown field", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("does not exist", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("doesn't exist", StringComparison.OrdinalIgnoreCase));
    }

    private static async Task<List<DamageEvent>> TryGetEnemyDamageEventsAsync(
        string reportCode,
        string accessToken,
        ReportFight fight,
        CancellationToken cancellationToken)
    {
        try
        {
            return await GetEnemyDamageEventsAsync(reportCode, accessToken, fight, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            return [];
        }
    }

    private static async Task<List<DamageEvent>> GetEnemyDamageEventsAsync(
        string reportCode,
        string accessToken,
        ReportFight fight,
        CancellationToken cancellationToken)
    {
        var damageEvents = new List<DamageEvent>();
        var pageStartTime = fight.StartTime;
        var pageGuard = 0;

        while (pageStartTime < fight.EndTime && pageGuard++ < 500)
        {
            var query = $$"""
                query ReportDamage($code: String!) {
                  reportData {
                    report(code: $code) {
                      events(
                        fightIDs: [{{fight.Id}}],
                        startTime: {{pageStartTime}},
                        endTime: {{fight.EndTime}},
                        dataType: DamageDone,
                        hostilityType: Enemies,
                        limit: 10000
                      ) {
                        data
                        nextPageTimestamp
                      }
                    }
                  }
                }
                """;

            using var document = await SendGraphQlAsync(
                    accessToken,
                    query,
                    new Dictionary<string, object?> { ["code"] = reportCode },
                    cancellationToken)
                .ConfigureAwait(false);

            var eventsElement = document.RootElement
                .GetProperty("data")
                .GetProperty("reportData")
                .GetProperty("report")
                .GetProperty("events");

            if (TryGetProperty(eventsElement, "data", out var dataElement))
            {
                AddDamageEvents(damageEvents, dataElement);
            }

            var nextPageTimestamp = GetLong(eventsElement, "nextPageTimestamp");
            if (nextPageTimestamp <= pageStartTime || nextPageTimestamp >= fight.EndTime)
            {
                break;
            }

            pageStartTime = nextPageTimestamp;
        }

        return damageEvents;
    }

    private static async Task<JsonDocument> SendGraphQlAsync(
        string accessToken,
        string query,
        object variables,
        CancellationToken cancellationToken)
    {
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, GraphQlEndpoint);
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        httpRequest.Headers.AcceptLanguage.ParseAdd(AcceptLanguage);
        httpRequest.Content = new StringContent(
            JsonSerializer.Serialize(new { query, variables }, JsonOptions),
            Encoding.UTF8,
            "application/json");

        using var response = await HttpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"FFLogs API呼び出しに失敗しました。HTTP {(int)response.StatusCode}: {TrimBody(body)}");
        }

        var document = JsonDocument.Parse(body);
        if (TryGetProperty(document.RootElement, "errors", out var errorsElement) &&
            errorsElement.ValueKind == JsonValueKind.Array &&
            errorsElement.GetArrayLength() > 0)
        {
            var messages = errorsElement
                .EnumerateArray()
                .Select(error => GetString(error, "message"))
                .Where(message => !string.IsNullOrWhiteSpace(message));
            using (document)
            {
                throw new InvalidOperationException($"FFLogs APIエラー: {string.Join(" / ", messages)}");
            }
        }

        return document;
    }

    private static void AddCastEvents(
        List<TimelineEvent> timelineEvents,
        HashSet<string> seen,
        JsonElement dataElement,
        string reportCode,
        ReportFight fight,
        ReportMetadata metadata,
        IReadOnlyDictionary<uint, string> localizedActionNames,
        IReadOnlyDictionary<uint, List<DamageEvent>> damageEventsByAbility,
        bool requireDamageSummary)
    {
        if (dataElement.ValueKind == JsonValueKind.String)
        {
            using var parsed = JsonDocument.Parse(dataElement.GetString() ?? "[]");
            AddCastEvents(
                timelineEvents,
                seen,
                parsed.RootElement,
                reportCode,
                fight,
                metadata,
                localizedActionNames,
                damageEventsByAbility,
                requireDamageSummary);
            return;
        }

        if (dataElement.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var eventElement in dataElement.EnumerateArray())
        {
            var eventType = GetString(eventElement, "type");
            if (!string.IsNullOrWhiteSpace(eventType) &&
                !string.Equals(eventType, "cast", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(eventType, "begincast", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var timestamp = GetLong(eventElement, "timestamp");
            if (timestamp <= 0)
            {
                continue;
            }

            var abilityId = GetUInt(eventElement, "abilityGameID");
            var abilityName = string.Empty;
            if (TryGetProperty(eventElement, "ability", out var abilityElement))
            {
                abilityName = GetString(abilityElement, "name");
                abilityId = abilityId == 0 ? GetUInt(abilityElement, "guid") : abilityId;
            }

            abilityName = ResolveAbilityName(abilityId, abilityName, localizedActionNames, metadata.Abilities);
            if (string.IsNullOrWhiteSpace(abilityName))
            {
                abilityName = abilityId == 0 ? "Enemy Cast" : $"Enemy Cast {abilityId}";
            }

            var damageSummary = FindDamageSummary(abilityId, timestamp, damageEventsByAbility, metadata.Actors);
            if (requireDamageSummary && damageSummary is null)
            {
                continue;
            }

            var relativeSeconds = Math.Max(0, (timestamp - fight.StartTime) / 1000f);
            var dedupeTime = (long)Math.Round(relativeSeconds * 2f);
            var dedupeKey = $"{abilityId}:{abilityName}:{dedupeTime}";
            if (!seen.Add(dedupeKey))
            {
                continue;
            }

            timelineEvents.Add(new TimelineEvent
            {
                Id = $"evt_fflogs_{reportCode}_{fight.Id}_{timelineEvents.Count}_{timestamp}",
                TimeSeconds = relativeSeconds,
                Name = abilityName,
                Type = GuessEventType(abilityName, damageSummary, metadata.PlayerCount),
                Notes = string.Empty,
            });
        }
    }

    private static void AddDamageEvents(List<DamageEvent> damageEvents, JsonElement dataElement)
    {
        if (dataElement.ValueKind == JsonValueKind.String)
        {
            using var parsed = JsonDocument.Parse(dataElement.GetString() ?? "[]");
            AddDamageEvents(damageEvents, parsed.RootElement);
            return;
        }

        if (dataElement.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var eventElement in dataElement.EnumerateArray())
        {
            var eventType = GetString(eventElement, "type");
            if (!string.IsNullOrWhiteSpace(eventType) &&
                !string.Equals(eventType, "damage", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var timestamp = GetLong(eventElement, "timestamp");
            var abilityId = GetUInt(eventElement, "abilityGameID");
            var targetId = GetInt(eventElement, "targetID");
            if (timestamp <= 0 || abilityId == 0 || targetId <= 0)
            {
                continue;
            }

            damageEvents.Add(new DamageEvent(timestamp, abilityId, targetId));
        }
    }

    private static string ResolveAbilityName(
        uint abilityId,
        string eventAbilityName,
        IReadOnlyDictionary<uint, string> localizedActionNames,
        IReadOnlyDictionary<uint, ReportAbility> abilities)
    {
        var metadataName = abilityId != 0 && abilities.TryGetValue(abilityId, out var ability)
            ? ability.Name
            : string.Empty;

        return FFLogsNameResolver.ResolveAbilityName(
            abilityId,
            eventAbilityName,
            localizedActionNames,
            metadataName);
    }

    private static DamageSummary? FindDamageSummary(
        uint abilityId,
        long castTimestamp,
        IReadOnlyDictionary<uint, List<DamageEvent>> damageEventsByAbility,
        IReadOnlyDictionary<int, ReportActor> actors)
    {
        if (abilityId == 0 || !damageEventsByAbility.TryGetValue(abilityId, out var damageEvents))
        {
            return null;
        }

        var candidates = damageEvents
            .Where(damageEvent =>
                damageEvent.Timestamp >= castTimestamp &&
                damageEvent.Timestamp <= castTimestamp + 12000)
            .ToList();
        if (candidates.Count == 0)
        {
            return null;
        }

        var bestCluster = candidates
            .GroupBy(damageEvent => damageEvent.Timestamp / 250)
            .Select(group =>
            {
                var targetIds = group.Select(damageEvent => damageEvent.TargetId).Distinct().ToList();
                var playerTargetIds = targetIds.Where(targetId => IsPlayerActor(targetId, actors)).ToList();
                var effectiveTargetIds = playerTargetIds.Count > 0 ? playerTargetIds : targetIds;
                return new
                {
                    Timestamp = group.Min(damageEvent => damageEvent.Timestamp),
                    TargetIds = effectiveTargetIds,
                    TankTargetCount = effectiveTargetIds.Count(targetId => IsTankActor(targetId, actors)),
                };
            })
            .OrderByDescending(group => group.TargetIds.Count)
            .ThenBy(group => group.Timestamp)
            .FirstOrDefault();

        return bestCluster is null
            ? null
            : new DamageSummary(bestCluster.TargetIds.Count, bestCluster.TankTargetCount);
    }

    private static TimelineEventType GuessEventType(
        string abilityName,
        DamageSummary? damageSummary,
        int playerCount)
    {
        if (ContainsAny(abilityName, "tank", "buster", "強攻撃", "タンク"))
        {
            return TimelineEventType.Tankbuster;
        }

        if (ContainsAny(abilityName, "stack", "頭割", "頭割り"))
        {
            return TimelineEventType.Stack;
        }

        if (ContainsAny(abilityName, "spread", "散開"))
        {
            return TimelineEventType.Spread;
        }

        if (ContainsAny(abilityName, "raidwide", "raid-wide", "全体"))
        {
            return TimelineEventType.Raidwide;
        }

        if (damageSummary is not null)
        {
            var playerRatio = playerCount <= 0
                ? 0f
                : (float)damageSummary.TargetCount / playerCount;
            if (damageSummary.TargetCount >= 6 || playerRatio >= 0.7f)
            {
                return TimelineEventType.Raidwide;
            }

            if (damageSummary.TargetCount is >= 1 and <= 2 &&
                damageSummary.TankTargetCount == damageSummary.TargetCount)
            {
                return TimelineEventType.Tankbuster;
            }
        }

        return TimelineEventType.Mechanic;
    }

    private static bool IsPlayerActor(int actorId, IReadOnlyDictionary<int, ReportActor> actors)
    {
        return !actors.TryGetValue(actorId, out var actor) ||
               string.Equals(actor.Type, "Player", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsTankActor(int actorId, IReadOnlyDictionary<int, ReportActor> actors)
    {
        if (!actors.TryGetValue(actorId, out var actor))
        {
            return false;
        }

        return ContainsAny(
            actor.SubType,
            "Paladin",
            "Warrior",
            "DarkKnight",
            "Dark Knight",
            "Gunbreaker",
            "ナイト",
            "戦士",
            "暗黒騎士",
            "ガンブレイカー");
    }

    private static bool ContainsAny(string value, params string[] needles)
    {
        return needles.Any(needle => value.Contains(needle, StringComparison.OrdinalIgnoreCase));
    }

    private static bool TryGetProperty(JsonElement element, string propertyName, out JsonElement property)
    {
        if (element.ValueKind == JsonValueKind.Object &&
            element.TryGetProperty(propertyName, out property))
        {
            return true;
        }

        property = default;
        return false;
    }

    private static string GetString(JsonElement element, string propertyName, string fallback = "")
    {
        if (!TryGetProperty(element, propertyName, out var property))
        {
            return fallback;
        }

        return property.ValueKind switch
        {
            JsonValueKind.String => property.GetString() ?? fallback,
            JsonValueKind.Number => property.ToString(),
            _ => fallback,
        };
    }

    private static int GetInt(JsonElement element, string propertyName, int fallback = 0)
    {
        if (!TryGetProperty(element, propertyName, out var property))
        {
            return fallback;
        }

        if (property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out var value))
        {
            return value;
        }

        return property.ValueKind == JsonValueKind.String && int.TryParse(property.GetString(), out value)
            ? value
            : fallback;
    }

    private static long GetLong(JsonElement element, string propertyName, long fallback = 0)
    {
        if (!TryGetProperty(element, propertyName, out var property))
        {
            return fallback;
        }

        if (property.ValueKind == JsonValueKind.Number && property.TryGetInt64(out var value))
        {
            return value;
        }

        return property.ValueKind == JsonValueKind.String && long.TryParse(property.GetString(), out value)
            ? value
            : fallback;
    }

    private static uint GetUInt(JsonElement element, string propertyName, uint fallback = 0)
    {
        if (!TryGetProperty(element, propertyName, out var property))
        {
            return fallback;
        }

        if (property.ValueKind == JsonValueKind.Number && property.TryGetUInt32(out var value))
        {
            return value;
        }

        return property.ValueKind == JsonValueKind.String && uint.TryParse(property.GetString(), out value)
            ? value
            : fallback;
    }

    private static bool GetBool(JsonElement element, string propertyName)
    {
        if (!TryGetProperty(element, propertyName, out var property))
        {
            return false;
        }

        return property.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String => bool.TryParse(property.GetString(), out var value) && value,
            _ => false,
        };
    }

    private static string TrimBody(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return "(empty)";
        }

        return body.Length <= 400 ? body : $"{body[..400]}...";
    }

    [GeneratedRegex(@"fflogs\.com/reports/(?<code>[A-Za-z0-9]+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ReportCodeRegex();

    [GeneratedRegex(@"^[A-Za-z0-9]+$", RegexOptions.CultureInvariant)]
    private static partial Regex ReportCodeValueRegex();

    [GeneratedRegex(@"(?:^|[?#&])fight=(?<fight>\d+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex FightIdRegex();

    private sealed record TokenResult(bool Success, string Message, string AccessToken, DateTimeOffset ExpiresAtUtc);

    private sealed record ReportInfo(string Title, List<ReportFight> Fights);

    private sealed record ReportFight(int Id, string Name, long StartTime, long EndTime, bool Kill, int EncounterId);

    private sealed record ReportMetadata(
        IReadOnlyDictionary<uint, ReportAbility> Abilities,
        IReadOnlyDictionary<int, ReportActor> Actors)
    {
        public static ReportMetadata Empty { get; } = new(
            new Dictionary<uint, ReportAbility>(),
            new Dictionary<int, ReportActor>());

        public int PlayerCount => this.Actors.Values.Count(actor =>
            string.Equals(actor.Type, "Player", StringComparison.OrdinalIgnoreCase));

        public bool HasRsvAbilityNames => this.Abilities.Values.Any(ability =>
            FFLogsNameResolver.IsRsvName(ability.Name));

        public ReportMetadata WithFallbackAbilities(ReportMetadata fallback)
        {
            var abilities = this.Abilities.ToDictionary(
                pair => pair.Key,
                pair => pair.Value);

            foreach (var (id, fallbackAbility) in fallback.Abilities)
            {
                if (!FFLogsNameResolver.IsUsableAbilityName(fallbackAbility.Name))
                {
                    continue;
                }

                if (!abilities.TryGetValue(id, out var ability) ||
                    !FFLogsNameResolver.IsUsableAbilityName(ability.Name))
                {
                    abilities[id] = fallbackAbility;
                }
            }

            var actors = this.Actors.Count == 0 ? fallback.Actors : this.Actors;
            return new ReportMetadata(abilities, actors);
        }
    }

    private sealed record ReportAbility(uint GameId, string Name);

    private sealed record ReportActor(int Id, string Name, string Type, string SubType);

    private sealed record DamageEvent(long Timestamp, uint AbilityGameId, int TargetId);

    private sealed record DamageSummary(int TargetCount, int TankTargetCount);

    private sealed record EventsPage(JsonDocument Document, bool TranslationUnavailable);
}
