namespace RaidFlow.Services;

public static class FFLogsNameResolver
{
    public static bool IsUsableAbilityName(string value)
    {
        return !string.IsNullOrWhiteSpace(value) && !IsRsvName(value);
    }

    public static bool IsRsvName(string value)
    {
        return value.TrimStart().StartsWith("_rsv_", StringComparison.OrdinalIgnoreCase);
    }
}
