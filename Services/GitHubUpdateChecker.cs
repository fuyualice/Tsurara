using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using Whiteboard.Core;

namespace Whiteboard.Services;

/// <summary>
/// GitHub の最新リリース（下書き・プレリリースは除く）を API で取得し、今の版より新しいかを確かめる。
/// 公開リポジトリなので認証は使わない（未認証の上限は 1 IP あたり 60 回/時）。
/// </summary>
public sealed partial class GitHubUpdateChecker : IUpdateChecker
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private readonly HttpClient _http;
    private readonly string _repository;
    private readonly Version _currentVersion;
    private readonly string _userAgent;

    /// <param name="repository">"owner/repo" の形のリポジトリ名。</param>
    /// <param name="currentVersion">今の版。</param>
    /// <param name="productName">User-Agent に使う製品名（GitHub API は User-Agent が必須）。</param>
    public GitHubUpdateChecker(HttpClient http, string repository, Version currentVersion, string productName)
    {
        if (!IsValidRepository(repository))
        {
            throw new ArgumentException($"リポジトリ名は owner/repo の形で指定してください: {repository}", nameof(repository));
        }
        _http = http;
        _repository = repository;
        _currentVersion = currentVersion;
        _userAgent = $"{productName}/{ReleaseVersion.ToDisplayString(currentVersion)}";
    }

    public static bool IsValidRepository(string? repository) =>
        repository is not null && RepositoryPattern().IsMatch(repository);

    [GeneratedRegex(@"^[A-Za-z0-9-]+/[A-Za-z0-9._-]+$")]
    private static partial Regex RepositoryPattern();

    public async Task<AvailableUpdate?> CheckAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(Timeout);

            using var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.github.com/repos/{_repository}/releases/latest");
            request.Headers.UserAgent.ParseAdd(_userAgent);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");

            // リリースが1つも無い場合は 404、上限に達した場合は 403 / 429。どれも「更新なし」として扱う
            using var response = await _http.SendAsync(request, timeout.Token);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }
            await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
            using var json = await JsonDocument.ParseAsync(stream, cancellationToken: timeout.Token);
            return Parse(json.RootElement);
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or JsonException)
        {
            return null;
        }
    }

    private AvailableUpdate? Parse(JsonElement release)
    {
        if (release.ValueKind != JsonValueKind.Object
            || IsTrue(release, "draft")
            || IsTrue(release, "prerelease")
            || GetString(release, "tag_name") is not { } tag
            || !ReleaseVersion.TryParse(tag, out var latest)
            || !ReleaseVersion.IsNewer(latest, _currentVersion))
        {
            return null;
        }
        return new AvailableUpdate(latest, ReleasePageUri(GetString(release, "html_url"), tag));
    }

    /// <summary>
    /// 返ってきたページの URL が GitHub のもので無ければ使わず、リポジトリのリリース一覧を開く。
    /// </summary>
    private Uri ReleasePageUri(string? htmlUrl, string tag)
    {
        if (Uri.TryCreate(htmlUrl, UriKind.Absolute, out var uri)
            && uri.Scheme == Uri.UriSchemeHttps
            && uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase))
        {
            return uri;
        }
        return new Uri($"https://github.com/{_repository}/releases/tag/{Uri.EscapeDataString(tag)}");
    }

    private static string? GetString(JsonElement obj, string name) =>
        obj.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static bool IsTrue(JsonElement obj, string name) =>
        obj.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;
}
