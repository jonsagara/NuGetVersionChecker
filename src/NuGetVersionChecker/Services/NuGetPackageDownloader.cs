using System.Text.Json;

namespace NuGetVersionChecker.Services;

/// <summary>
/// Downloads .nupkg files from nuget.org given a gallery URL like https://www.nuget.org/packages/{id}/{version}.
/// </summary>
public sealed class NuGetPackageDownloader(HttpClient httpClient)
{
    private const string FlatContainerBaseUrl = "https://api.nuget.org/v3-flatcontainer";

    /// <summary>
    /// Downloads the package into memory and returns it along with its resolved id and version.
    /// If the URL has no version, the latest stable version is used.
    /// </summary>
    public async Task<(string PackageId, string PackageVersion, MemoryStream Package)> DownloadAsync(string nuGetPackageUrl, CancellationToken cancellationToken = default)
    {
        (string packageId, string? packageVersion) = ParseGalleryUrl(nuGetPackageUrl);

        string lowerId = packageId.ToLowerInvariant();
        string version = packageVersion ?? await GetLatestStableVersionAsync(lowerId, cancellationToken);
        string lowerVersion = version.ToLowerInvariant();

        string nupkgUrl = $"{FlatContainerBaseUrl}/{lowerId}/{lowerVersion}/{lowerId}.{lowerVersion}.nupkg";

        using HttpResponseMessage response = await httpClient.GetAsync(nupkgUrl, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Failed to download {packageId} {version} from {nupkgUrl}: {(int)response.StatusCode} {response.ReasonPhrase}");
        }

        MemoryStream package = new();
        await response.Content.CopyToAsync(package, cancellationToken);
        package.Position = 0;

        return (packageId, version, package);
    }

    private static (string PackageId, string? PackageVersion) ParseGalleryUrl(string nuGetPackageUrl)
    {
        if (!Uri.TryCreate(nuGetPackageUrl, UriKind.Absolute, out Uri? uri))
        {
            throw new ArgumentException($"Invalid URL: {nuGetPackageUrl}", nameof(nuGetPackageUrl));
        }

        // Expected path: /packages/{id}[/{version}]
        string[] segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 2 || !segments[0].Equals("packages", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"Expected a URL like https://www.nuget.org/packages/{{id}}/{{version}}, but got: {nuGetPackageUrl}", nameof(nuGetPackageUrl));
        }

        string packageId = Uri.UnescapeDataString(segments[1]);
        string? packageVersion = segments.Length >= 3 ? Uri.UnescapeDataString(segments[2]) : null;

        return (packageId, packageVersion);
    }

    private async Task<string> GetLatestStableVersionAsync(string lowerId, CancellationToken cancellationToken)
    {
        string indexUrl = $"{FlatContainerBaseUrl}/{lowerId}/index.json";

        await using Stream indexStream = await httpClient.GetStreamAsync(indexUrl, cancellationToken);
        using JsonDocument index = await JsonDocument.ParseAsync(indexStream, cancellationToken: cancellationToken);

        // Versions are listed in ascending order; prerelease versions contain a '-'.
        string? latestStable = index.RootElement
            .GetProperty("versions")
            .EnumerateArray()
            .Select(v => v.GetString()!)
            .LastOrDefault(v => !v.Contains('-'));

        return latestStable ?? throw new InvalidOperationException($"No stable versions found for package {lowerId}.");
    }
}
