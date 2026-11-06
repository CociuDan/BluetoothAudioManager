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
    private const string LatestReleaseUrl = "https://api.github.com/repos/CociuDan/BluetoothAudioManager/releases/latest";
    private const string TokenKey = "UpdateToken";
    private const string DigestPrefix = "sha256:";

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

        foreach (var asset in json.RootElement.GetProperty("assets").EnumerateArray())
        {
            if (asset.GetProperty("name").GetString() == InstallLayout.SetupFileName
                && asset.TryGetProperty("digest", out var digest)
                && digest.GetString() is { } value
                && value.StartsWith(DigestPrefix, StringComparison.OrdinalIgnoreCase)
                && asset.GetProperty("url").GetString() is { } url)
            {
                return new ReleaseSetup(version, url, value[DigestPrefix.Length..].ToLowerInvariant());
            }
        }

        return null;
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
