using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using BluetoothAudioManager.Updater.Models;

namespace BluetoothAudioManager.Updater.Services;

/// <summary>Reads the repository's latest release and downloads its package, using the token the pipeline built in.</summary>
public sealed class GitHubReleases : IDisposable
{
    private const string LatestReleaseUrl = "https://api.github.com/repos/CociuDan/BluetoothAudioManager/releases/latest";
    private const string TokenKey = "UpdateToken";

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

    /// <summary>The latest release's package, or null if the latest release has none, such as one made before the updater.</summary>
    public async Task<ReleasePackage?> LatestAsync(CancellationToken cancel)
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

        var packageName = $"BluetoothAudioManager-{version}.zip";
        string? packageUrl = null;
        string? checksumUrl = null;

        foreach (var asset in json.RootElement.GetProperty("assets").EnumerateArray())
        {
            var name = asset.GetProperty("name").GetString();
            var url = asset.GetProperty("url").GetString();

            if (name == packageName)
            {
                packageUrl = url;
            }
            else if (name == packageName + ".sha256")
            {
                checksumUrl = url;
            }
        }

        return packageUrl is null || checksumUrl is null ? null : new ReleasePackage(version, packageUrl, checksumUrl);
    }

    /// <summary>
    /// Downloads the package to the file, unless a copy there already matches, and checks it against the release's checksum.
    /// Throws if it doesn't match.
    /// </summary>
    public async Task DownloadAsync(ReleasePackage package, string path, CancellationToken cancel)
    {
        var expected = (await DownloadTextAsync(package.ChecksumUrl, cancel)).Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];

        if (File.Exists(path) && await HashAsync(path, cancel) == expected.ToLowerInvariant())
        {
            return;
        }

        var partial = path + ".partial";
        using (var request = AssetRequest(package.PackageUrl))
        using (var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancel))
        {
            response.EnsureSuccessStatusCode();
            await using var file = File.Create(partial);
            await response.Content.CopyToAsync(file, cancel);
        }

        if (await HashAsync(partial, cancel) != expected.ToLowerInvariant())
        {
            File.Delete(partial);
            throw new InvalidDataException($"The {package.Version} package doesn't match its checksum.");
        }

        File.Move(partial, path, overwrite: true);
    }

    private static string? ReadToken() =>
        typeof(GitHubReleases).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attribute => attribute.Key == TokenKey)?.Value;

    private static async Task<string> HashAsync(string path, CancellationToken cancel)
    {
        await using var file = File.OpenRead(path);
        return Convert.ToHexStringLower(await SHA256.HashDataAsync(file, cancel));
    }

    // A release file, asked for through the API, as the repository is private. GitHub sends it from its file storage.
    private static HttpRequestMessage AssetRequest(string url)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/octet-stream"));
        return request;
    }

    private async Task<string> DownloadTextAsync(string url, CancellationToken cancel)
    {
        using var request = AssetRequest(url);
        using var response = await _http.SendAsync(request, cancel);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadAsStringAsync(cancel)).Trim();
    }

    public void Dispose() => _http.Dispose();
}
