using Whiteboard.Core;
using Whiteboard.Services;
using Whiteboard.ViewModels;

namespace Whiteboard.Tests;

public class PaletteViewModelTests
{
    private readonly RecordingPaster _paster = new();
    private readonly FixedTimeProvider _time = new(new DateTimeOffset(2026, 10, 5, 14, 30, 0, TimeSpan.Zero));
    private readonly RecordingThemeSwitcher _themeSwitcher = new();
    private readonly RecordingSettingsStore _store = new();
    private readonly PaletteViewModel _vm;

    public PaletteViewModelTests()
    {
        var settings = new AppSettings
        {
            How = ["tel", "mail"],
            WhoGroups = [new WhoGroup("研究室", ["田中", "佐藤"]), new WhoGroup("事務", ["鈴木"])],
        };
        _vm = new PaletteViewModel(settings, _paster, _time, _themeSwitcher, _store);
    }

    private Task Tel() => _vm.ToggleHowCommand.ExecuteAsync(_vm.HowOptions[0]);

    private Task Mail() => _vm.ToggleHowCommand.ExecuteAsync(_vm.HowOptions[1]);

    private Task Who(string who) => _vm.SelectWhoCommand.ExecuteAsync(who);

    [Fact]
    public async Task HowとWho2回で貼り付けてリセットする()
    {
        await Tel();
        await Who("田中");
        Assert.Empty(_paster.Pasted);

        await Who("佐藤");

        Assert.Equal(["14:30 tel 田中→佐藤 "], _paster.Pasted);
        Assert.Null(_vm.How);
        Assert.Null(_vm.From);
        Assert.Null(_vm.To);
        Assert.All(_vm.HowOptions, o => Assert.False(o.IsSelected));
    }

    [Fact]
    public async Task Howを2つ選ぶとアンパサンドでつなぐ()
    {
        await Tel();
        await Mail();
        await Who("田中");
        await Who("佐藤");

        Assert.Equal(["14:30 tel&mail 田中→佐藤 "], _paster.Pasted);
    }

    [Fact]
    public async Task Howは押した順ではなく候補の並び順でつなぐ()
    {
        await Mail();
        await Tel();
        await Who("田中");
        await Who("佐藤");

        Assert.Equal(["14:30 tel&mail 田中→佐藤 "], _paster.Pasted);
    }

    [Fact]
    public async Task Howの途中でWhoを選んでも2回目まで貼り付けない()
    {
        await Tel();
        await Who("田中");
        await Mail();
        Assert.Empty(_paster.Pasted);

        await Who("佐藤");

        Assert.Equal(["14:30 tel&mail 田中→佐藤 "], _paster.Pasted);
    }

    [Fact]
    public async Task Howをもう一度押すと選択を外す()
    {
        await Tel();
        await Mail();
        await Tel();

        Assert.Equal("mail", _vm.How);
    }

    [Fact]
    public async Task Whoを先に2回選んだ場合はHowを押した時点で貼り付ける()
    {
        await Who("田中");
        await Who("佐藤");
        Assert.Empty(_paster.Pasted);

        await Mail();

        Assert.Equal(["14:30 mail 田中→佐藤 "], _paster.Pasted);
    }

    [Fact]
    public async Task 時刻はそろった時点のものを使う()
    {
        await Tel();
        await Who("田中");
        _time.Now = _time.Now.AddMinutes(5);
        await Who("佐藤");

        Assert.Equal(["14:35 tel 田中→佐藤 "], _paster.Pasted);
    }

    [Fact]
    public async Task 途中の状態はプレビューにだけ表示する()
    {
        await Tel();
        await Mail();
        await Who("田中");

        Assert.Equal("14:30 tel&mail 田中→＿ ", _vm.Preview);
        Assert.Empty(_paster.Pasted);
    }

    [Fact]
    public async Task クリアで選択をやり直せる()
    {
        await Tel();
        await Who("田中");
        _vm.ClearCommand.Execute(null);

        Assert.Null(_vm.How);
        Assert.Null(_vm.From);
        Assert.False(_vm.HowOptions[0].IsSelected);
        Assert.Equal("14:30 ＿ ＿→＿ ", _vm.Preview);
    }

    [Fact]
    public async Task 貼り付けに失敗したらメッセージを出してリセットする()
    {
        _paster.Failure = new InvalidOperationException("使用中");

        await Tel();
        await Who("田中");
        await Who("佐藤");

        Assert.Contains("使用中", _vm.StatusMessage);
        Assert.Null(_vm.How);
        Assert.Null(_vm.From);
    }

    [Fact]
    public async Task 貼り付け中のクリックは無視する()
    {
        var gate = new TaskCompletionSource();
        _paster.Gate = gate.Task;

        await Tel();
        await Who("田中");
        var pasting = Who("佐藤");

        await Mail();
        await Who("鈴木");
        gate.SetResult();
        await pasting;

        Assert.Equal(["14:30 tel 田中→佐藤 "], _paster.Pasted);
        Assert.Null(_vm.How);
        Assert.Null(_vm.From);
    }

    [Fact]
    public async Task fromとtoは別のタブから選べる()
    {
        await Tel();
        await Who(_vm.WhoGroups[0].Members[0]);
        await Who(_vm.WhoGroups[1].Members[0]);

        Assert.Equal(["14:30 tel 田中→鈴木 "], _paster.Pasted);
    }

    [Fact]
    public async Task 指定時刻で貼り付け_貼り付け後も保持する()
    {
        _vm.WhenInput = "930";
        Assert.True(_vm.ApplyWhenInput());
        Assert.Equal("09:30", _vm.WhenInput);
        Assert.False(_vm.IsNow);

        await Tel();
        await Who("田中");
        await Who("佐藤");
        _time.Now = _time.Now.AddMinutes(10);
        await Mail();
        await Who("佐藤");
        await Who("鈴木");

        Assert.Equal(["09:30 tel 田中→佐藤 ", "09:30 mail 佐藤→鈴木 "], _paster.Pasted);
        Assert.Equal(new TimeOnly(9, 30), _vm.FixedTime);
    }

    [Fact]
    public void 指定時刻はプレビューにも出る()
    {
        _vm.WhenInput = "8:00";
        _vm.ApplyWhenInput();

        Assert.Equal("08:00 ＿ ＿→＿ ", _vm.Preview);
    }

    [Fact]
    public async Task クリアしても指定時刻は残る()
    {
        _vm.WhenInput = "1000";
        _vm.ApplyWhenInput();
        await Tel();
        _vm.ClearCommand.Execute(null);

        Assert.Equal(new TimeOnly(10, 0), _vm.FixedTime);
    }

    [Fact]
    public async Task nowに戻すと現在時刻を使う()
    {
        _vm.WhenInput = "1000";
        _vm.ApplyWhenInput();
        _vm.UseNowCommand.Execute(null);

        await Tel();
        await Who("田中");
        await Who("佐藤");

        Assert.True(_vm.IsNow);
        Assert.Equal(["14:30 tel 田中→佐藤 "], _paster.Pasted);
    }

    [Fact]
    public void 読み取れない時刻は確定せずメッセージを出す()
    {
        _vm.WhenInput = "25:00";

        Assert.False(_vm.ApplyWhenInput());
        Assert.True(_vm.IsNow);
        Assert.NotNull(_vm.StatusMessage);
    }

    [Fact]
    public void 取り消すと最後に確定した内容に戻る()
    {
        _vm.WhenInput = "1000";
        _vm.ApplyWhenInput();
        _vm.WhenInput = "11";
        _vm.CancelWhenInput();

        Assert.Equal("10:00", _vm.WhenInput);
        Assert.Equal(new TimeOnly(10, 0), _vm.FixedTime);
    }

    [Fact]
    public void 書き換えずに入力を終えてもnowのまま()
    {
        _vm.WhenInput = "1000";
        _vm.ApplyWhenInput();
        _vm.UseNowCommand.Execute(null);

        _vm.FinishWhenInput();

        Assert.True(_vm.IsNow);
        Assert.Null(_vm.StatusMessage);
    }

    [Fact]
    public void 書き換えて入力を終えると確定し_読み取れなければ元に戻す()
    {
        _vm.WhenInput = "1100";
        _vm.FinishWhenInput();
        Assert.Equal(new TimeOnly(11, 0), _vm.FixedTime);

        _vm.WhenInput = "abc";
        _vm.FinishWhenInput();
        Assert.Equal("11:00", _vm.WhenInput);
        Assert.Equal(new TimeOnly(11, 0), _vm.FixedTime);
    }

    [Fact]
    public void 初期状態では保存済みのテーマが選ばれている()
    {
        var vm = new PaletteViewModel(new AppSettings { Theme = AppTheme.Dark }, _paster, _time, _themeSwitcher, _store);

        Assert.Equal([false, false, true], vm.ThemeOptions.Select(o => o.IsSelected));
    }

    [Fact]
    public void テーマを選ぶと反映して保存し_選択状態を切り替える()
    {
        var light = _vm.ThemeOptions.Single(o => o.Value == AppTheme.Light);

        _vm.SelectThemeCommand.Execute(light);

        Assert.Equal([AppTheme.Light], _themeSwitcher.Applied);
        Assert.Equal([AppTheme.Light], _store.Themes);
        Assert.Equal([false, true, false], _vm.ThemeOptions.Select(o => o.IsSelected));
    }

    [Fact]
    public void 文字サイズの初期値は設定の値で_一致する選択肢が選ばれている()
    {
        var vm = new PaletteViewModel(new AppSettings { FontSize = 18 }, _paster, _time, _themeSwitcher, _store);

        Assert.Equal(18, vm.FontSize);
        Assert.Equal(22, vm.PreviewFontSize);
        Assert.Equal(20, vm.ClockDateFontSize);
        Assert.Equal(40, vm.ClockTimeFontSize);
        Assert.Equal(["大"], vm.FontSizeOptions.Where(o => o.IsSelected).Select(o => o.Label));
    }

    [Fact]
    public void 選択肢にない文字サイズならどれも選ばれない()
    {
        var vm = new PaletteViewModel(new AppSettings { FontSize = 15 }, _paster, _time, _themeSwitcher, _store);

        Assert.Equal(15, vm.FontSize);
        Assert.All(vm.FontSizeOptions, o => Assert.False(o.IsSelected));
    }

    [Fact]
    public void 文字サイズを選ぶと反映して保存する()
    {
        var large = _vm.FontSizeOptions.Single(o => o.Label == "大");

        _vm.SelectFontSizeCommand.Execute(large);

        Assert.Equal(18, _vm.FontSize);
        Assert.Equal(22, _vm.PreviewFontSize);
        Assert.Equal([18.0], _store.FontSizes);
        Assert.Equal(["大"], _vm.FontSizeOptions.Where(o => o.IsSelected).Select(o => o.Label));
    }

    [Fact]
    public void プラスマイナスで1ずつ変えて保存し_選択肢と一致すれば選択状態になる()
    {
        _vm.IncreaseFontSizeCommand.Execute(null);
        Assert.Equal(17, _vm.FontSize);
        Assert.All(_vm.FontSizeOptions, o => Assert.False(o.IsSelected));

        _vm.IncreaseFontSizeCommand.Execute(null);
        Assert.Equal(18, _vm.FontSize);
        Assert.Equal(["大"], _vm.FontSizeOptions.Where(o => o.IsSelected).Select(o => o.Label));

        _vm.DecreaseFontSizeCommand.Execute(null);
        Assert.Equal([17.0, 18.0, 17.0], _store.FontSizes);
    }

    [Fact]
    public void 文字サイズの上限と下限ではプラスマイナスを押せない()
    {
        var max = new PaletteViewModel(new AppSettings { FontSize = AppSettings.MaxFontSize }, _paster, _time, _themeSwitcher, _store);
        var min = new PaletteViewModel(new AppSettings { FontSize = AppSettings.MinFontSize }, _paster, _time, _themeSwitcher, _store);

        Assert.False(max.IncreaseFontSizeCommand.CanExecute(null));
        Assert.True(max.DecreaseFontSizeCommand.CanExecute(null));
        Assert.False(min.DecreaseFontSizeCommand.CanExecute(null));
        Assert.True(min.IncreaseFontSizeCommand.CanExecute(null));
    }

    [Fact]
    public void 時計は年月日と時分秒を2段で表示する()
    {
        _time.Now = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);

        Assert.Equal("2026/01/02 (Fri)", _vm.ClockDate);
        Assert.Equal("03:04:05", _vm.ClockTime);
    }

    [Fact]
    public void 時計の表示非表示を切り替えて保存する()
    {
        Assert.True(_vm.ShowClock);
        var hide = _vm.ClockOptions.Single(o => !o.Value);

        _vm.SelectClockCommand.Execute(hide);

        Assert.False(_vm.ShowClock);
        Assert.Equal([false], _store.ShowClocks);
        Assert.Equal(["非表示"], _vm.ClockOptions.Where(o => o.IsSelected).Select(o => o.Label));
    }

    [Fact]
    public void 設定欄を開閉できる()
    {
        Assert.False(_vm.IsSettingsOpen);
        _vm.ToggleSettingsCommand.Execute(null);
        Assert.True(_vm.IsSettingsOpen);
        _vm.ToggleSettingsCommand.Execute(null);
        Assert.False(_vm.IsSettingsOpen);
    }

    [Fact]
    public async Task 新しい版があれば知らせる()
    {
        Assert.False(_vm.HasUpdate);
        var update = new AvailableUpdate(new Version(0, 2, 0), new Uri("https://github.com/owner/repo/releases/tag/v0.2.0"));

        await _vm.CheckForUpdatesAsync(new StubUpdateChecker(update));

        Assert.True(_vm.HasUpdate);
        Assert.Equal("0.2.0", _vm.AvailableUpdate!.DisplayVersion);
    }

    [Fact]
    public async Task 確認できなかったときは前の結果を残し_例外も外に出さない()
    {
        var update = new AvailableUpdate(new Version(0, 2, 0), new Uri("https://github.com/owner/repo/releases/tag/v0.2.0"));
        await _vm.CheckForUpdatesAsync(new StubUpdateChecker(update));

        await _vm.CheckForUpdatesAsync(new StubUpdateChecker(null));
        await _vm.CheckForUpdatesAsync(new StubUpdateChecker(null) { Failure = new InvalidOperationException() });

        Assert.Equal(update, _vm.AvailableUpdate);
    }

    private sealed class StubUpdateChecker(AvailableUpdate? result) : IUpdateChecker
    {
        public Exception? Failure { get; init; }

        public Task<AvailableUpdate?> CheckAsync(CancellationToken cancellationToken = default) =>
            Failure is null ? Task.FromResult(result) : Task.FromException<AvailableUpdate?>(Failure);
    }

    private sealed class RecordingThemeSwitcher : IThemeSwitcher
    {
        public List<AppTheme> Applied { get; } = [];

        public void Apply(AppTheme theme) => Applied.Add(theme);
    }

    private sealed class RecordingSettingsStore : IUserSettingsStore
    {
        public List<AppTheme> Themes { get; } = [];

        public List<double> FontSizes { get; } = [];

        public bool SaveTheme(AppTheme theme)
        {
            Themes.Add(theme);
            return true;
        }

        public List<bool> ShowClocks { get; } = [];

        public bool SaveFontSize(double fontSize)
        {
            FontSizes.Add(fontSize);
            return true;
        }

        public bool SaveShowClock(bool showClock)
        {
            ShowClocks.Add(showClock);
            return true;
        }
    }

    private sealed class RecordingPaster : IPrefixPaster
    {
        public List<string> Pasted { get; } = [];

        public Exception? Failure { get; set; }

        public Task Gate { get; set; } = Task.CompletedTask;

        public async Task PasteAsync(string text)
        {
            await Gate;
            if (Failure is not null)
            {
                throw Failure;
            }
            Pasted.Add(text);
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

        public override DateTimeOffset GetUtcNow() => Now;
    }
}
