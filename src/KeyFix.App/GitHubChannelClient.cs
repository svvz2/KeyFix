using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using KeyFix.Core;

namespace KeyFix.App;

internal sealed class GitHubChannelClient : IDisposable
{
    public const string DefaultRepositoryUrl = "https://github.com/svvz2/KeyFix";
    public const string ChannelUrl = "https://raw.githubusercontent.com/svvz2/KeyFix/main/channel.json";

    private readonly HttpClient _httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(7)
    };

    public GitHubChannelClient()
    {
        _httpClient.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("KeyFix", ThisAssemblyVersion));
        _httpClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public async Task<GitHubChannelConfig?> FetchAsync(CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.GetAsync(ChannelUrl, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var config = await JsonSerializer.DeserializeAsync(
            stream,
            GitHubChannelJsonContext.Default.GitHubChannelConfig,
            cancellationToken);

        return config is { SchemaVersion: 1 } ? config : null;
    }

    public static bool HasNewerRelease(GitHubChannelConfig config) =>
        ReleaseVersion.IsNewer(config.Update.LatestVersion, ThisAssemblyVersion);

    public static bool TryGetHttpsUri(string? value, out Uri uri)
    {
        if (Uri.TryCreate(value, UriKind.Absolute, out var parsed) &&
            string.Equals(parsed.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            uri = parsed;
            return true;
        }

        uri = new Uri(DefaultRepositoryUrl);
        return false;
    }

    private static string ThisAssemblyVersion =>
        typeof(GitHubChannelClient).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";

    public void Dispose() => _httpClient.Dispose();
}

internal sealed class GitHubChannelConfig
{
    public int SchemaVersion { get; init; }
    public GitHubProjectLinks Project { get; init; } = new();
    public GitHubSocialLinks Social { get; init; } = new();
    public GitHubAnnouncement Announcement { get; init; } = new();
    public GitHubUpdateInfo Update { get; init; } = new();
}

internal sealed class GitHubProjectLinks
{
    public string RepositoryUrl { get; init; } = GitHubChannelClient.DefaultRepositoryUrl;
    public string IssuesUrl { get; init; } = $"{GitHubChannelClient.DefaultRepositoryUrl}/issues";
    public string DiscussionsUrl { get; init; } = $"{GitHubChannelClient.DefaultRepositoryUrl}/discussions";
}

internal sealed class GitHubSocialLinks
{
    public string InstagramHandle { get; init; } = "@22r2z";
    public string InstagramUrl { get; init; } = "https://www.instagram.com/22r2z/";
    public string TelegramHandle { get; init; } = "@Civil_Sajad";
    public string TelegramUrl { get; init; } = "https://t.me/Civil_Sajad";
}

internal sealed class GitHubAnnouncement
{
    public bool Enabled { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public string ButtonLabel { get; init; } = "اقرأ التفاصيل";
    public string Url { get; init; } = string.Empty;
}

internal sealed class GitHubUpdateInfo
{
    public string LatestVersion { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public string DownloadUrl { get; init; } = string.Empty;
    public string ReleaseNotesUrl { get; init; } = string.Empty;
}

[System.Text.Json.Serialization.JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[System.Text.Json.Serialization.JsonSerializable(typeof(GitHubChannelConfig))]
internal partial class GitHubChannelJsonContext : System.Text.Json.Serialization.JsonSerializerContext;
