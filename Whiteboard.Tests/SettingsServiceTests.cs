using System.IO;
using Whiteboard.Core;
using Whiteboard.Services;

namespace Whiteboard.Tests;

public sealed class SettingsServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "WhiteboardTests", Guid.NewGuid().ToString("N"));
    private readonly string _appDirectory;
    private readonly string _defaultPath;
    private readonly string _whoDirectory;
    private readonly string _userDirectory;

    public SettingsServiceTests()
    {
        _appDirectory = Path.Combine(_root, "app");
        _defaultPath = Path.Combine(_appDirectory, "settings.json");
        _whoDirectory = Path.Combine(_appDirectory, "who");
        _userDirectory = Path.Combine(_root, "user");
        Directory.CreateDirectory(_appDirectory);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private SettingsService CreateService() => new(_appDirectory, _userDirectory);

    private void WriteUserSettings(string json)
    {
        Directory.CreateDirectory(_userDirectory);
        File.WriteAllText(Path.Combine(_userDirectory, "settings.json"), json);
    }

    [Fact]
    public void ファイルが無ければ組み込みの既定値()
    {
        Assert.Equal(new AppSettings(), CreateService().Load(), SettingsComparer.Instance);
    }

    [Fact]
    public void ユーザー設定は既定設定を項目ごとに上書きする()
    {
        File.WriteAllText(_defaultPath, """{ "how": ["X", "Y"], "clipboardRestoreDelayMs": 200 }""");
        WriteUserSettings("""{ "how": ["Q1", "Q2"] }""");

        var settings = CreateService().Load();

        Assert.Equal(["Q1", "Q2"], settings.How);
        Assert.Equal(200, settings.ClipboardRestoreDelayMs);
        Assert.Equal("HH:mm", settings.TimeFormat);
    }

    [Fact]
    public void 壊れたファイルは無視する()
    {
        File.WriteAllText(_defaultPath, """{ "how": ["X"] }""");
        WriteUserSettings("{ これは JSON ではない");

        Assert.Equal(["X"], CreateService().Load().How);
    }

    [Fact]
    public void 不正な値の項目だけ下の層の値を使う()
    {
        WriteUserSettings("""
            {
              "timeFormat": "%",
              "template": "",
              "how": "tel",
              "clipboardRestoreDelayMs": -1
            }
            """);

        var settings = CreateService().Load();
        var defaults = new AppSettings();

        Assert.Equal(defaults.TimeFormat, settings.TimeFormat);
        Assert.Equal(defaults.Template, settings.Template);
        Assert.Equal(defaults.How, settings.How);
        Assert.Equal(defaults.ClipboardRestoreDelayMs, settings.ClipboardRestoreDelayMs);
    }

    [Fact]
    public void コメントと末尾のカンマを許す()
    {
        File.WriteAllText(_defaultPath, """
            {
              // How
              "how": ["X",],
            }
            """);

        Assert.Equal(["X"], CreateService().Load().How);
    }

    [Fact]
    public void Howの空文字は除く()
    {
        File.WriteAllText(_defaultPath, """{ "how": ["", "  ", "tel"] }""");

        Assert.Equal(["tel"], CreateService().Load().How);
    }

    private void WriteWho(string fileName, params string[] lines)
    {
        Directory.CreateDirectory(_whoDirectory);
        File.WriteAllLines(Path.Combine(_whoDirectory, fileName), lines);
    }

    [Fact]
    public void Whoは1ファイル1グループで_1行1件で読む()
    {
        WriteWho("研究室.txt", "# コメント", "田中", "", "  佐藤  ", "田中", "鈴木");

        var group = Assert.Single(CreateService().Load().WhoGroups);
        Assert.Equal("研究室", group.Name);
        Assert.Equal(["田中", "佐藤", "鈴木"], group.Members);
    }

    [Fact]
    public void グループはファイル名順で_先頭の番号はタブ名から除く()
    {
        WriteWho("2_事務.txt", "鈴木");
        WriteWho("1_研究室.txt", "田中");
        WriteWho("外部.txt", "高橋");
        WriteWho("3.txt", "伊藤");
        WriteWho("メモ.md", "無視される");

        var groups = CreateService().Load().WhoGroups;

        Assert.Equal(["研究室", "事務", "3", "外部"], groups.Select(g => g.Name));
    }

    [Fact]
    public void 空のファイルも空のグループとして読む()
    {
        WriteWho("空.txt", "# コメントだけ");

        Assert.Empty(Assert.Single(CreateService().Load().WhoGroups).Members);
    }

    [Fact]
    public void whoフォルダが無ければWhoは空()
    {
        Assert.Empty(CreateService().Load().WhoGroups);
    }

    [Theory]
    [InlineData("light", AppTheme.Light)]
    [InlineData("Dark", AppTheme.Dark)]
    [InlineData("system", AppTheme.System)]
    [InlineData("purple", AppTheme.System)]
    [InlineData("1", AppTheme.System)]
    public void テーマを読む_不正な値は既定値(string value, AppTheme expected)
    {
        WriteUserSettings($$"""{ "theme": "{{value}}" }""");

        Assert.Equal(expected, CreateService().Load().Theme);
    }

    [Fact]
    public void テーマを保存すると_ほかの項目を残したまま次回読み込める()
    {
        WriteUserSettings("""{ "how": ["X"] }""");
        var service = CreateService();

        Assert.True(service.SaveTheme(AppTheme.Dark));

        var settings = service.Load();
        Assert.Equal(AppTheme.Dark, settings.Theme);
        Assert.Equal(["X"], settings.How);
    }

    [Fact]
    public void ユーザー設定が無くてもテーマを保存できる()
    {
        Assert.True(CreateService().SaveTheme(AppTheme.Light));

        Assert.Equal(AppTheme.Light, CreateService().Load().Theme);
    }

    [Theory]
    [InlineData("18", 18)]
    [InlineData("12.5", 12.5)]
    [InlineData("9", 16)]
    [InlineData("33", 16)]
    [InlineData("\"大\"", 16)]
    public void 文字サイズを読む_範囲外や数値以外は既定値(string json, double expected)
    {
        WriteUserSettings($$"""{ "fontSize": {{json}} }""");

        Assert.Equal(expected, CreateService().Load().FontSize);
    }

    [Fact]
    public void 文字サイズとテーマを続けて保存しても両方残る()
    {
        var service = CreateService();

        service.SaveTheme(AppTheme.Dark);
        service.SaveFontSize(20);

        var settings = service.Load();
        Assert.Equal(AppTheme.Dark, settings.Theme);
        Assert.Equal(20, settings.FontSize);
    }

    [Theory]
    [InlineData("false", false)]
    [InlineData("true", true)]
    [InlineData("\"no\"", true)]
    public void 時計の表示を読む_真偽値以外は既定値(string json, bool expected)
    {
        WriteUserSettings($$"""{ "showClock": {{json}} }""");

        Assert.Equal(expected, CreateService().Load().ShowClock);
    }

    [Fact]
    public void 時計の表示を保存できる()
    {
        var service = CreateService();

        service.SaveShowClock(false);

        Assert.False(service.Load().ShowClock);
    }

    [Theory]
    [InlineData("false", false)]
    [InlineData("true", true)]
    [InlineData("0", true)]
    public void 更新の確認の有無を読む_真偽値以外は既定値(string json, bool expected)
    {
        WriteUserSettings($$"""{ "checkForUpdates": {{json}} }""");

        Assert.Equal(expected, CreateService().Load().CheckForUpdates);
    }

    [Fact]
    public void 壊れたユーザー設定は上書きしない()
    {
        WriteUserSettings("{ 壊れている");

        Assert.False(CreateService().SaveTheme(AppTheme.Dark));
        Assert.Equal("{ 壊れている", File.ReadAllText(Path.Combine(_userDirectory, "settings.json")));
    }

    [Fact]
    public void 窓の位置を保存して読み込める()
    {
        var service = CreateService();
        Assert.Null(service.LoadWindowPosition());

        service.SaveWindowPosition(new WindowPosition(-100, 200, 300, 150));

        Assert.Equal(new WindowPosition(-100, 200, 300, 150), service.LoadWindowPosition());
    }

    private sealed class SettingsComparer : IEqualityComparer<AppSettings>
    {
        public static readonly SettingsComparer Instance = new();

        public bool Equals(AppSettings? x, AppSettings? y) =>
            x is not null && y is not null
            && x.TimeFormat == y.TimeFormat
            && x.Template == y.Template
            && x.How.SequenceEqual(y.How)
            && x.WhoGroups.Count == y.WhoGroups.Count
            && x.ClipboardRestoreDelayMs == y.ClipboardRestoreDelayMs
            && x.Theme == y.Theme
            && x.FontSize == y.FontSize
            && x.ShowClock == y.ShowClock
            && x.CheckForUpdates == y.CheckForUpdates;

        public int GetHashCode(AppSettings obj) => 0;
    }
}
