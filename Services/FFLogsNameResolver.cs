using System.Text;

namespace RaidFlow.Services;

public static class FFLogsNameResolver
{
    public static string ResolveAbilityName(
        uint abilityId,
        string eventName,
        IReadOnlyDictionary<uint, string> localizedActionNames,
        string translatedMetadataName,
        IReadOnlyDictionary<string, string>? englishToLocalizedActionNames = null)
    {
        var localizedNames = ResolveAbilityIds(abilityId, eventName, translatedMetadataName)
            .Select(actionId => TryGetLocalizedActionName(actionId, localizedActionNames, out var localizedName)
                ? localizedName
                : string.Empty)
            .Where(IsUsableAbilityName)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var hasEventName = IsUsableAbilityName(eventName);
        var hasMetadataName = IsUsableAbilityName(translatedMetadataName);

        var localizedJapaneseName = localizedNames.FirstOrDefault(ContainsJapaneseText);
        if (!string.IsNullOrWhiteSpace(localizedJapaneseName))
        {
            return localizedJapaneseName;
        }

        if (TryGetLocalizedNameFromEnglishName(eventName, englishToLocalizedActionNames, out var eventLocalizedName) &&
            ContainsJapaneseText(eventLocalizedName))
        {
            return eventLocalizedName;
        }

        if (TryGetLocalizedNameFromEnglishName(translatedMetadataName, englishToLocalizedActionNames, out var metadataLocalizedName) &&
            ContainsJapaneseText(metadataLocalizedName))
        {
            return metadataLocalizedName;
        }

        if (hasEventName && ContainsJapaneseText(eventName))
        {
            return eventName;
        }

        if (hasMetadataName && ContainsJapaneseText(translatedMetadataName))
        {
            return translatedMetadataName;
        }

        var firstLocalizedName = localizedNames.FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(firstLocalizedName))
        {
            return firstLocalizedName;
        }

        if (hasEventName)
        {
            return eventName;
        }

        return hasMetadataName ? translatedMetadataName : string.Empty;
    }

    public static bool IsUsableAbilityName(string value)
    {
        return !string.IsNullOrWhiteSpace(value) && !IsRsvName(value);
    }

    public static bool IsRsvName(string value)
    {
        return value.TrimStart().StartsWith("_rsv_", StringComparison.OrdinalIgnoreCase);
    }

    public static bool TryGetActionIdFromRsvName(string value, out uint actionId)
    {
        actionId = 0;
        var trimmed = value.TrimStart();
        const string prefix = "_rsv_";
        if (!trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var start = prefix.Length;
        var end = start;
        while (end < trimmed.Length && char.IsDigit(trimmed[end]))
        {
            end++;
        }

        return end > start &&
               uint.TryParse(trimmed[start..end], out actionId) &&
               actionId > 0;
    }

    public static string NormalizeAbilityNameLookupKey(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormKC);
        var builder = new StringBuilder(normalized.Length);
        foreach (var character in normalized)
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
            }
        }

        return builder.ToString();
    }

    private static IEnumerable<uint> ResolveAbilityIds(uint abilityId, string eventName, string translatedMetadataName)
    {
        if (abilityId > 0)
        {
            yield return abilityId;
        }

        if (TryGetActionIdFromRsvName(eventName, out var eventActionId) && eventActionId != abilityId)
        {
            yield return eventActionId;
        }

        if (TryGetActionIdFromRsvName(translatedMetadataName, out var metadataActionId) &&
            metadataActionId != abilityId &&
            metadataActionId != eventActionId)
        {
            yield return metadataActionId;
        }
    }

    private static bool TryGetLocalizedActionName(
        uint abilityId,
        IReadOnlyDictionary<uint, string> localizedActionNames,
        out string localizedName)
    {
        localizedName = string.Empty;
        if (abilityId == 0 ||
            !localizedActionNames.TryGetValue(abilityId, out var candidate) ||
            !IsUsableAbilityName(candidate))
        {
            return false;
        }

        localizedName = candidate;
        return true;
    }

    private static bool TryGetLocalizedNameFromEnglishName(
        string abilityName,
        IReadOnlyDictionary<string, string>? englishToLocalizedActionNames,
        out string localizedName)
    {
        localizedName = string.Empty;
        if (englishToLocalizedActionNames is null ||
            string.IsNullOrWhiteSpace(abilityName))
        {
            return false;
        }

        var trimmedName = abilityName.Trim();
        if (englishToLocalizedActionNames.TryGetValue(trimmedName, out var exactCandidate) &&
            IsUsableAbilityName(exactCandidate))
        {
            localizedName = exactCandidate;
            return true;
        }

        var normalizedName = NormalizeAbilityNameLookupKey(trimmedName);
        if (normalizedName.Length == 0 ||
            !englishToLocalizedActionNames.TryGetValue(normalizedName, out var normalizedCandidate) ||
            !IsUsableAbilityName(normalizedCandidate))
        {
            return false;
        }

        localizedName = normalizedCandidate;
        return true;
    }

    private static bool ContainsJapaneseText(string value)
    {
        return value.Any(character =>
            character is >= '\u3040' and <= '\u30ff' ||
            character is >= '\u3400' and <= '\u9fff' ||
            character is >= '\uff66' and <= '\uff9d');
    }
}
