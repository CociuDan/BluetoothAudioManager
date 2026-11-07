using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using BluetoothAudioManager.Shared.Helpers;
using BluetoothAudioManager.Updater.Models;

namespace BluetoothAudioManager.Updater.Services;

/// <summary>Reads the repository's latest release and downloads its installer, using the token the pipeline built in.</summary>
public sealed class GitHubReleases : IDisposable
{
    private const string ApiUrl = "https://api.github.com/repos/CociuDan/BluetoothAudioManager";
    private const string LatestReleaseUrl = "https://api.github.com/repos/CociuDan/BluetoothAudioManager/releases/latest";
    private const string TokenKey = "UpdateToken";
    private const string DigestPrefix = "sha256:";
    private const string CompareNotesPrefix = "**Full Changelog**:";
    private const string ComparePathPrefix = "/CociuDan/BluetoothAudioManager/compare/";

    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromMinutes(10) };

    public GitHubReleases()
    {
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("BluetoothAudioManager-Updater");
        _http.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");

        // The repository is private, so its releases need the token. Builds made elsewhere have none.
        if (ReadToken() is { } token)
        {
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
    }

    /// <summary>
    /// The latest release's installer, or null if it has none with a checksum, such as a release made before the updater.
    /// GitHub works out each release file's checksum itself when it's uploaded.
    /// </summary>
    public async Task<ReleaseSetup?> LatestAsync(CancellationToken cancel)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, LatestReleaseUrl);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        using var response = await _http.SendAsync(request, cancel);
        response.EnsureSuccessStatusCode();

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancel));
        var version = json.RootElement.GetProperty("tag_name").GetString()?.TrimStart('v', 'V');
        if (string.IsNullOrEmpty(version))
        {
            return null;
        }

        var notes = json.RootElement.TryGetProperty("body", out var body) && body.ValueKind == JsonValueKind.String
            ? body.GetString() ?? ""
            : "";

        foreach (var asset in json.RootElement.GetProperty("assets").EnumerateArray())
        {
            if (asset.GetProperty("name").GetString() == InstallLayout.SetupFileName
                && asset.TryGetProperty("digest", out var digest)
                && digest.GetString() is { } value
                && value.StartsWith(DigestPrefix, StringComparison.OrdinalIgnoreCase)
                && asset.GetProperty("url").GetString() is { } url)
            {
                return new ReleaseSetup(version, url, value[DigestPrefix.Length..].ToLowerInvariant(), notes);
            }
        }

        return null;
    }

    /// <summary>Reads release notes, replacing GitHub's generated comparison link with commit summaries.</summary>
    public async Task<(string Version, string Notes)?> LatestReleaseNotesAsync(CancellationToken cancel)
    {
        var release = await LatestAsync(cancel);
        if (release is null)
        {
            return null;
        }

        return TryGetCompareRefs(release.Notes, out var baseRef, out var headRef)
            ? (release.Version, await CommitSummariesAsync(baseRef, headRef, cancel))
            : (release.Version, release.Notes);
    }

    private async Task<string> CommitSummariesAsync(string baseRef, string headRef, CancellationToken cancel)
    {
        var compareUrl = $"{ApiUrl}/compare/{Uri.EscapeDataString(baseRef)}...{Uri.EscapeDataString(headRef)}";
        using var request = new HttpRequestMessage(HttpMethod.Get, compareUrl);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        using var response = await _http.SendAsync(request, cancel);
        response.EnsureSuccessStatusCode();

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancel));
        var summaries = new List<string>();
        foreach (var item in json.RootElement.GetProperty("commits").EnumerateArray())
        {
            var message = item.GetProperty("commit").GetProperty("message").GetString();
            var summary = message?.Split('\n', 2)[0].Trim();
            if (!string.IsNullOrEmpty(summary))
            {
                summaries.Add($"- {summary}");
            }
        }

        return summaries.Count > 0
            ? string.Join(Environment.NewLine, summaries)
            : "No commit summaries are available for this release.";
    }

    private static bool TryGetCompareRefs(string notes, out string baseRef, out string headRef)
    {
        baseRef = "";
        headRef = "";
        var line = notes.Trim();
        if (!line.StartsWith(CompareNotesPrefix, StringComparison.Ordinal)
            || line.Contains('\n'))
        {
            return false;
        }

        var link = line[CompareNotesPrefix.Length..].Trim();
        if (!Uri.TryCreate(link, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps
            || uri.Host != "github.com"
            || !uri.AbsolutePath.StartsWith(ComparePathPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        var refs = uri.AbsolutePath[ComparePathPrefix.Length..];
        var separator = refs.IndexOf("...", StringComparison.Ordinal);
        if (separator <= 0 || separator + 3 >= refs.Length)
        {
            return false;
        }

        baseRef = Uri.UnescapeDataString(refs[..separator]);
        headRef = Uri.UnescapeDataString(refs[(separator + 3)..]);
        return !baseRef.Contains('/') && !headRef.Contains('/');
    }

    /// <summary>
    /// Downloads the installer to the file, replacing whatever was there, unless it's already this one.
    /// Throws if the download doesn't match GitHub's checksum.
    /// </summary>
    public async Task DownloadAsync(ReleaseSetup setup, string path, CancellationToken cancel)
    {
        if (await MatchesAsync(path, setup.Sha256, cancel))
        {
            return;
        }

        // Asked for through the API, as the repository is private. GitHub sends it from its file storage.
        var partial = path + ".partial";
        using (var request = new HttpRequestMessage(HttpMethod.Get, setup.Url))
        {
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/octet-stream"));
            using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancel);
            response.EnsureSuccessStatusCode();
            await using var file = File.Create(partial);
            await response.Content.CopyToAsync(file, cancel);
        }

        if (!await MatchesAsync(partial, setup.Sha256, cancel))
        {
            File.Delete(partial);
            throw new InvalidDataException($"The {setup.Version} installer doesn't match its checksum.");
        }

        File.Move(partial, path, overwrite: true);
    }

    /// <summary>Whether the file is there and matches the checksum.</summary>
    public static async Task<bool> MatchesAsync(string path, string sha256, CancellationToken cancel)
    {
        if (!File.Exists(path))
        {
            return false;
        }

        await using var file = File.OpenRead(path);
        return Convert.ToHexStringLower(await SHA256.HashDataAsync(file, cancel)) == sha256;
    }

    private static string? ReadToken() =>
        typeof(GitHubReleases).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attribute => attribute.Key == TokenKey)?.Value;

    public void Dispose() => _http.Dispose();
}
