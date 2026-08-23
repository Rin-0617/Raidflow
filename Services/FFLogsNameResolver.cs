namespace RaidFlow.Services;

public static class FFLogsNameResolver
{
    public static string ResolveAbilityName(
        uint abilityId,
        string eventName,
        IReadOnlyDictionary<uint, string> localizedActionNames,
        string translatedMetadataName)
    {
        var hasLocalizedName = TryGetLocalizedActionName(abilityId, localizedActionNames, out var localizedName);
        var hasEventName = IsUsableAbilityName(eventName);
        var hasMetadataName = IsUsableAbilityName(translatedMetadataName);

        if (hasLocalizedName && ContainsJapaneseText(localizedName))
        {
            return localizedName;
        }

        if (hasEventName && ContainsJapaneseText(eventName))
        {
            return eventName;
        }

        if (hasMetadataName && ContainsJapaneseText(translatedMetadataName))
        {
            return translatedMetadataName;
        }

        if (hasLocalizedName)
        {
            return localizedName;
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

    private static bool ContainsJapaneseText(string value)
    {
        return value.Any(character =>
            character is >= '\u3040' and <= '\u30ff' ||
            character is >= '\u3400' and <= '\u9fff' ||
            character is >= '\uff66' and <= '\uff9d');
    }
}
