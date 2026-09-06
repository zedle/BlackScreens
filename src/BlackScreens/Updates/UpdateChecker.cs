using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace BlackScreens.Updates;

/// <summary>A release that is newer than the running build, and the file to fetch for it.</summary>
public sealed record UpdateInfo(
    Version Version,
    string Tag,
    string AssetName,
    string AssetUrl,
    long AssetSize,
    string ReleaseUrl);

/// <summary>
/// Asks GitHub what the latest release is. This is the only outbound request BlackScreens ever
/// makes, and only when the user has turned updates on.
/// </summary>
public static class UpdateChecker
{
    private static readonly HttpClient Client = CreateClient();

    /// <summary>
    /// Turns the releases API payload into an update, or null when there is nothing newer or no
    /// asset this install can use.
    /// </summary>
    public static UpdateInfo? ParseRelease(string json, string currentVersion, InstallKind kind)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            if (root.TryGetProperty("draft", out var draft) && draft.ValueKind == JsonValueKind.True)
            {
                return null;
            }

            if (root.TryGetProperty("prerelease", out var pre) && pre.ValueKind == JsonValueKind.True)
            {
                return null;
            }

            var tag = root.TryGetProperty("tag_name", out var tagName) ? tagName.GetString() : null;
            var version = ReleaseVersions.TryParse(tag);
            if (tag is null || version is null || !ReleaseVersions.IsNewer(currentVersion, tag))
            {
                return null;
            }

            if (!root.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            var names = new List<string>();
            foreach (var asset in assets.EnumerateArray())
            {
                if (asset.TryGetProperty("name", out var name) && name.GetString() is { } text)
                {
                    names.Add(text);
                }
            }

            var wanted = UpdateTarget.PickAsset(kind, names);
            if (wanted is null)
            {
                return null;
            }

            foreach (var asset in assets.EnumerateArray())
            {
                if (!asset.TryGetProperty("name", out var name)
                    || !string.Equals(name.GetString(), wanted, StringComparison.Ordinal))
                {
                    continue;
                }

                var url = asset.TryGetProperty("browser_download_url", out var link) ? link.GetString() : null;
                if (string.IsNullOrWhiteSpace(url))
                {
                    return null;
                }

                var size = asset.TryGetProperty("size", out var bytes) && bytes.TryGetInt64(out var value)
                    ? value
                    : 0;

                var releaseUrl = root.TryGetProperty("html_url", out var page)
                    ? page.GetString() ?? UpdateTarget.ReleasesPage
                    : UpdateTarget.ReleasesPage;

                return new UpdateInfo(version, tag, wanted, url, size, releaseUrl);
            }

            return null;
        }
        catch (Exception ex)
        {
            ErrorLog.Write($"Update payload could not be read: {ex}");
            return null;
        }
    }

    public static async Task<UpdateInfo?> FetchAsync(
        string currentVersion,
        InstallKind kind,
        CancellationToken cancellationToken)
    {
        try
        {
            using var response = await Client
                .GetAsync(UpdateTarget.LatestReleaseApi, cancellationToken)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                // A repository with no releases yet answers 404. That is not an error worth logging
                // every day.
                if (response.StatusCode != System.Net.HttpStatusCode.NotFound)
                {
                    ErrorLog.Write($"Update check returned {(int)response.StatusCode}.");
                }

                return null;
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return ParseRelease(json, currentVersion, kind);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (Exception ex)
        {
            ErrorLog.Write($"Update check failed: {ex.Message}");
            return null;
        }
    }

    public static async Task<bool> DownloadAsync(
        UpdateInfo update,
        string destination,
        CancellationToken cancellationToken)
    {
        try
        {
            var folder = Path.GetDirectoryName(destination);
            if (folder is not null)
            {
                Directory.CreateDirectory(folder);
            }

            var partial = destination + ".part";
            if (File.Exists(partial))
            {
                File.Delete(partial);
            }

            using (var response = await Client
                .GetAsync(update.AssetUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false))
            {
                if (!response.IsSuccessStatusCode)
                {
                    ErrorLog.Write($"Update download returned {(int)response.StatusCode}.");
                    return false;
                }

                await using var source = await response.Content
                    .ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                await using var file = File.Create(partial);
                await source.CopyToAsync(file, cancellationToken).ConfigureAwait(false);
            }

            // A truncated download must never be handed to the installer.
            if (update.AssetSize > 0 && new FileInfo(partial).Length != update.AssetSize)
            {
                ErrorLog.Write("Update download was the wrong size and has been discarded.");
                File.Delete(partial);
                return false;
            }

            if (File.Exists(destination))
            {
                File.Delete(destination);
            }

            File.Move(partial, destination);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception ex)
        {
            ErrorLog.Write($"Update download failed: {ex.Message}");
            return false;
        }
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(10)
        };

        // GitHub rejects requests with no user agent. Nothing else identifying is sent.
        client.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("BlackScreens", Ui.AppInfo.Version));
        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

        return client;
    }
}
