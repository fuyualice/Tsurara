using System.Net;
using System.Net.Http;
using System.Text;
using Whiteboard.Services;

namespace Whiteboard.Tests;

public class GitHubUpdateCheckerTests
{
    private static readonly Version Current = new(0, 1, 0, 0);

    private readonly StubHandler _handler = new();

    private GitHubUpdateChecker CreateChecker() => new(new HttpClient(_handler), "owner/repo", Current, "Tsurara");

    private static string Release(string tag, bool draft = false, bool prerelease = false, string? htmlUrl = null) => $$"""
        {
          "tag_name": "{{tag}}",
          "html_url": "{{htmlUrl ?? $"https://github.com/owner/repo/releases/tag/{tag}"}}",
          "draft": {{(draft ? "true" : "false")}},
          "prerelease": {{(prerelease ? "true" : "false")}}
        }
        """;

    [Fact]
    public async Task 新しい版があれば返す()
    {
        _handler.Respond(HttpStatusCode.OK, Release("v0.2.0"));

        var update = await CreateChecker().CheckAsync();

        Assert.NotNull(update);
        Assert.Equal(new Version(0, 2, 0, 0), update.Version);
        Assert.Equal(new Uri("https://github.com/owner/repo/releases/tag/v0.2.0"), update.ReleasePage);
    }

    [Fact]
    public async Task 最新リリースのAPIをUserAgent付きで呼ぶ()
    {
        _handler.Respond(HttpStatusCode.OK, Release("v0.1.0"));

        await CreateChecker().CheckAsync();

        var request = Assert.Single(_handler.Requests);
        Assert.Equal(new Uri("https://api.github.com/repos/owner/repo/releases/latest"), request.RequestUri);
        Assert.Equal("Tsurara/0.1.0", request.Headers.UserAgent.ToString());
    }

    [Theory]
    [InlineData("v0.1.0")]
    [InlineData("v0.0.9")]
    [InlineData("v0.2.0-beta")]
    [InlineData("nightly")]
    public async Task 新しくない_または読めないタグなら返さない(string tag)
    {
        _handler.Respond(HttpStatusCode.OK, Release(tag));

        Assert.Null(await CreateChecker().CheckAsync());
    }

    [Fact]
    public async Task 下書きとプレリリースは知らせない()
    {
        _handler.Respond(HttpStatusCode.OK, Release("v0.2.0", draft: true));
        Assert.Null(await CreateChecker().CheckAsync());

        _handler.Respond(HttpStatusCode.OK, Release("v0.2.0", prerelease: true));
        Assert.Null(await CreateChecker().CheckAsync());
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task エラー応答なら返さない(HttpStatusCode status)
    {
        _handler.Respond(status, "{}");

        Assert.Null(await CreateChecker().CheckAsync());
    }

    [Fact]
    public async Task 壊れた応答や通信エラーでも例外を出さない()
    {
        _handler.Respond(HttpStatusCode.OK, "{ 壊れている");
        Assert.Null(await CreateChecker().CheckAsync());

        _handler.Respond(HttpStatusCode.OK, "[]");
        Assert.Null(await CreateChecker().CheckAsync());

        _handler.Failure = new HttpRequestException("オフライン");
        Assert.Null(await CreateChecker().CheckAsync());
    }

    [Fact]
    public async Task GitHub以外のページのURLは使わずリリースのページを開く()
    {
        _handler.Respond(HttpStatusCode.OK, Release("v0.2.0", htmlUrl: "http://example.com/evil"));

        var update = await CreateChecker().CheckAsync();

        Assert.Equal(new Uri("https://github.com/owner/repo/releases/tag/v0.2.0"), update!.ReleasePage);
    }

    [Theory]
    [InlineData("")]
    [InlineData("repo")]
    [InlineData("owner/repo/extra")]
    [InlineData("owner/re po")]
    [InlineData("../repo")]
    public void リポジトリ名はownerとrepoの形だけ受け付ける(string repository)
    {
        Assert.False(GitHubUpdateChecker.IsValidRepository(repository));
        Assert.Throws<ArgumentException>(() => new GitHubUpdateChecker(new HttpClient(_handler), repository, Current, "Tsurara"));
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private HttpStatusCode _status = HttpStatusCode.OK;
        private string _body = "{}";

        public List<HttpRequestMessage> Requests { get; } = [];

        public Exception? Failure { get; set; }

        public void Respond(HttpStatusCode status, string body)
        {
            _status = status;
            _body = body;
            Failure = null;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            if (Failure is not null)
            {
                return Task.FromException<HttpResponseMessage>(Failure);
            }
            return Task.FromResult(new HttpResponseMessage(_status)
            {
                Content = new StringContent(_body, Encoding.UTF8, "application/json"),
            });
        }
    }
}
