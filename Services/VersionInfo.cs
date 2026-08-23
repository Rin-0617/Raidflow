namespace RaidFlow.Services;

public static class VersionInfo
{
    public static string DisplayVersion
    {
        get
        {
            var version = typeof(VersionInfo).Assembly.GetName().Version;
            if (version is null)
            {
                return "0.0.0";
            }

            return version.Revision > 0
                ? $"{version.Major}.{version.Minor}.{version.Build}.{version.Revision}"
                : $"{version.Major}.{version.Minor}.{version.Build}";
        }
    }

    public static string WindowTitle(string title, string id)
    {
        return $"{title} v{DisplayVersion}###{id}";
    }
}
