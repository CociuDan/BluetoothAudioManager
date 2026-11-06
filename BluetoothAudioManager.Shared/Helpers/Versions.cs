namespace BluetoothAudioManager.Shared.Helpers;

/// <summary>Reads release versions, such as v0.0.3 from a release's tag.</summary>
public static class Versions
{
    /// <summary>The version in a tag or version string, or null if it isn't three numbers.</summary>
    public static Version? Parse(string? text)
    {
        var trimmed = (text ?? "").Trim().TrimStart('v', 'V');
        return trimmed.Split('.').Length == 3 && Version.TryParse(trimmed, out var version) ? version : null;
    }

    /// <summary>Whether the release is newer than the installed version. An unreadable installed version counts as older.</summary>
    public static bool IsNewer(string? release, string? installed) =>
        Parse(release) is { } releaseVersion
        && (Parse(installed) is not { } installedVersion || releaseVersion > installedVersion);
}
