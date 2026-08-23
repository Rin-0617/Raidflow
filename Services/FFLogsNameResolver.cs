namespace RaidFlow.Services;

public static class FFLogsNameResolver
{
    public static string ResolveAbilityName(
        uint abilityId,
        string eventName,
        IReadOnlyDictionary<uint, string> localizedActionNames,
        string translatedMetadataName)
    {
        if (IsUsableAbilityName(translatedMetadataName))
        {
            return translatedMetadataName;
        }

        if (IsRsvName(translatedMetadataName) &&
            TryGetLocalizedActionName(abilityId, localizedActionNames, out var localizedName))
        {
            return localizedName;
        }

        if (IsUsableAbilityName(eventName))
        {
            return eventName;
        }

        return TryGetLocalizedActionName(abilityId, localizedActionNames, out localizedName)
            ? localizedName
            : string.Empty;
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
}
