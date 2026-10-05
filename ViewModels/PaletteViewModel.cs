using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Whiteboard.Core;
using Whiteboard.Services;

namespace Whiteboard.ViewModels;

/// <summary>
/// パレットの選択状態。How を1つ以上、Who を2回（1回目が from、2回目が to）選ぶと、前置きを貼り付けてリセットする。
/// </summary>
/// <remarks>
/// How は複数選べるので、貼り付けのきっかけは Who の2回目にする。
/// Who を先に2回選んだ場合だけは、How を1つ押した時点で貼り付ける。
/// When（now か指定時刻）は、同じ時刻で何度も入力できるよう、貼り付けやクリアではリセットしない。
/// </remarks>
public sealed partial class PaletteViewModel : ObservableObject
{
    /// <summary>プレビューで未選択の項目に表示する文字。</summary>
    public const string Unselected = "＿";

    /// <summary>How を複数選んだときのつなぎ文字。</summary>
    public const string HowSeparator = "&";

    /// <summary>When の入力欄に表示する時刻の書式。</summary>
    private const string WhenInputFormat = "HH:mm";

    private readonly PrefixBuilder _builder;
    private readonly IPrefixPaster _paster;
    private readonly TimeProvider _timeProvider;
    private readonly ITimer _clockTimer;
    private readonly IThemeSwitcher _themeSwitcher;
    private readonly IUserSettingsStore _settingsStore;
    private bool _isPasting;
    private string _appliedWhenInput = string.Empty;

    /// <summary>プレビューを本体の文字サイズよりどれだけ大きくするか。</summary>
    private const double PreviewFontSizeIncrease = 4;

    /// <summary>時計の日付・時刻を本体の文字サイズよりどれだけ大きくするか。</summary>
    private const double ClockDateFontSizeIncrease = 2;
    private const double ClockTimeFontSizeIncrease = 22;

    /// <summary>［−］［＋］で文字サイズを変える幅。</summary>
    private const double FontSizeStep = 1;

    /// <summary>一番上の時計の書式（日付と時刻の2段）。</summary>
    private const string ClockDateFormat = "yyyy/MM/dd (ddd)";
    private const string ClockTimeFormat = "HH:mm:ss";

    public PaletteViewModel(
        AppSettings settings,
        IPrefixPaster paster,
        TimeProvider timeProvider,
        IThemeSwitcher themeSwitcher,
        IUserSettingsStore settingsStore)
    {
        _themeSwitcher = themeSwitcher;
        _settingsStore = settingsStore;
        ThemeOptions =
        [
            new(AppTheme.System, "システム"),
            new(AppTheme.Light, "ライト"),
            new(AppTheme.Dark, "ダーク"),
        ];
        MarkSelected(ThemeOptions, settings.Theme);
        FontSizeOptions =
        [
            new(14, "小"),
            new(16, "中"),
            new(18, "大"),
        ];
        FontSize = settings.FontSize;
        MarkSelected(FontSizeOptions, settings.FontSize);
        ClockOptions =
        [
            new(true, "表示"),
            new(false, "非表示"),
        ];
        ShowClock = settings.ShowClock;
        MarkSelected(ClockOptions, settings.ShowClock);

        _builder = new PrefixBuilder(settings.Template, settings.TimeFormat);
        _paster = paster;
        _timeProvider = timeProvider;
        HowOptions = settings.How.Select(name => new HowOptionViewModel(name)).ToArray();
        WhoGroups = settings.WhoGroups;
        WhenInput = string.Empty;

        // 放置していてもプレビューの時刻が古くならないよう、定期的に更新する
        // 時計を1秒ごとに進め、放置していてもプレビューの時刻が古くならないようにする
        _clockTimer = timeProvider.CreateTimer(
            _ =>
            {
                OnPropertyChanged(nameof(ClockDate));
                OnPropertyChanged(nameof(ClockTime));
                OnPropertyChanged(nameof(Preview));
            },
            null,
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(1));
    }

    public IReadOnlyList<HowOptionViewModel> HowOptions { get; }

    /// <summary>設定欄のテーマの選択肢（システム・ライト・ダーク）。</summary>
    public IReadOnlyList<SettingOptionViewModel<AppTheme>> ThemeOptions { get; }

    /// <summary>設定欄の文字サイズの選択肢。設定ファイルで選択肢以外の値にした場合は、どれも選択状態にならない。</summary>
    public IReadOnlyList<SettingOptionViewModel<double>> FontSizeOptions { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviewFontSize))]
    [NotifyPropertyChangedFor(nameof(ClockDateFontSize))]
    [NotifyPropertyChangedFor(nameof(ClockTimeFontSize))]
    [NotifyCanExecuteChangedFor(nameof(IncreaseFontSizeCommand))]
    [NotifyCanExecuteChangedFor(nameof(DecreaseFontSizeCommand))]
    public partial double FontSize { get; private set; }

    /// <summary>設定欄の時計の選択肢（表示・非表示）。</summary>
    public IReadOnlyList<SettingOptionViewModel<bool>> ClockOptions { get; }

    [ObservableProperty]
    public partial bool ShowClock { get; private set; }

    /// <summary>一番上の時計の日付（上段）。</summary>
    public string ClockDate => _timeProvider.GetLocalNow().ToString(ClockDateFormat, CultureInfo.InvariantCulture);

    /// <summary>一番上の時計の時刻（下段、大きく表示）。</summary>
    public string ClockTime => _timeProvider.GetLocalNow().ToString(ClockTimeFormat, CultureInfo.InvariantCulture);

    public double PreviewFontSize => FontSize + PreviewFontSizeIncrease;

    public double ClockDateFontSize => FontSize + ClockDateFontSizeIncrease;

    public double ClockTimeFontSize => FontSize + ClockTimeFontSizeIncrease;

    /// <summary>公開されている新しい版。無ければ null。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUpdate))]
    public partial AvailableUpdate? AvailableUpdate { get; private set; }

    public bool HasUpdate => AvailableUpdate is not null;

    /// <summary>設定欄を開いているか。</summary>
    [ObservableProperty]
    public partial bool IsSettingsOpen { get; private set; }

    /// <summary>Who の候補（タブごと）。from と to は別のタブから選んでもよい。</summary>
    public IReadOnlyList<WhoGroup> WhoGroups { get; }

    public bool HasWhoGroups => WhoGroups.Count > 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Preview))]
    public partial string? From { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Preview))]
    public partial string? To { get; private set; }

    [ObservableProperty]
    public partial string? StatusMessage { get; private set; }

    /// <summary>指定時刻。null なら now（そろった時点の現在時刻）を使う。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Preview))]
    [NotifyPropertyChangedFor(nameof(IsNow))]
    public partial TimeOnly? FixedTime { get; private set; }

    public bool IsNow => FixedTime is null;

    /// <summary>When の入力欄の文字列（確定前の下書きを含む）。</summary>
    [ObservableProperty]
    public partial string WhenInput { get; set; }

    /// <summary>選択中の How を候補の並び順に "&amp;" でつないだもの。未選択なら null。</summary>
    public string? How
    {
        get
        {
            var selected = HowOptions.Where(o => o.IsSelected).Select(o => o.Name).ToArray();
            return selected.Length > 0 ? string.Join(HowSeparator, selected) : null;
        }
    }

    /// <summary>組み立て途中の前置き。Discord には送らず、パレット上にだけ表示する。</summary>
    public string Preview => _builder.Build(
        How ?? Unselected,
        From ?? Unselected,
        To ?? Unselected,
        GetTime());

    /// <summary>When の入力欄の内容を指定時刻として確定する。読み取れなければ false。</summary>
    public bool ApplyWhenInput()
    {
        if (!TimeInputParser.TryParse(WhenInput, out var time))
        {
            StatusMessage = "時刻を読み取れませんでした（例: 14:30、1430）";
            return false;
        }
        FixedTime = time;
        _appliedWhenInput = WhenInput = time.ToString(WhenInputFormat, CultureInfo.InvariantCulture);
        StatusMessage = null;
        return true;
    }

    /// <summary>When の入力欄を、最後に確定した内容に戻す。</summary>
    public void CancelWhenInput() => WhenInput = _appliedWhenInput;

    /// <summary>
    /// Enter 以外で入力を終えたとき用。書き換えていれば確定を試み、読み取れなければ元に戻す。
    /// 書き換えていなければ何もしない（now のままなら now のまま）。
    /// </summary>
    public void FinishWhenInput()
    {
        if (WhenInput == _appliedWhenInput || !ApplyWhenInput())
        {
            CancelWhenInput();
        }
    }

    /// <summary>新しい版があれば <see cref="AvailableUpdate"/> に入れる。確認できなかった場合は前の結果を残す。</summary>
    public async Task CheckForUpdatesAsync(IUpdateChecker checker)
    {
        try
        {
            if (await checker.CheckAsync() is { } update)
            {
                AvailableUpdate = update;
            }
        }
        catch (Exception)
        {
            // 確認できなくてもパレットの操作には影響させない
        }
    }

    [RelayCommand]
    private void UseNow() => FixedTime = null;

    [RelayCommand]
    private void ToggleSettings() => IsSettingsOpen = !IsSettingsOpen;

    [RelayCommand]
    private void SelectTheme(SettingOptionViewModel<AppTheme> option)
    {
        MarkSelected(ThemeOptions, option.Value);
        _themeSwitcher.Apply(option.Value);
        _settingsStore.SaveTheme(option.Value);
    }

    [RelayCommand]
    private void SelectFontSize(SettingOptionViewModel<double> option) => SetFontSize(option.Value);

    [RelayCommand(CanExecute = nameof(CanIncreaseFontSize))]
    private void IncreaseFontSize() => SetFontSize(FontSize + FontSizeStep);

    private bool CanIncreaseFontSize() => FontSize + FontSizeStep <= AppSettings.MaxFontSize;

    [RelayCommand(CanExecute = nameof(CanDecreaseFontSize))]
    private void DecreaseFontSize() => SetFontSize(FontSize - FontSizeStep);

    private bool CanDecreaseFontSize() => FontSize - FontSizeStep >= AppSettings.MinFontSize;

    private void SetFontSize(double size)
    {
        FontSize = Math.Clamp(size, AppSettings.MinFontSize, AppSettings.MaxFontSize);
        MarkSelected(FontSizeOptions, FontSize);
        _settingsStore.SaveFontSize(FontSize);
    }

    [RelayCommand]
    private void SelectClock(SettingOptionViewModel<bool> option)
    {
        MarkSelected(ClockOptions, option.Value);
        ShowClock = option.Value;
        _settingsStore.SaveShowClock(option.Value);
    }

    private static void MarkSelected<T>(IEnumerable<SettingOptionViewModel<T>> options, T value)
    {
        foreach (var option in options)
        {
            option.IsSelected = EqualityComparer<T>.Default.Equals(option.Value, value);
        }
    }

    // 貼り付け中のクリックは _isPasting で無視する。
    // 実行中にボタンが無効表示にならないよう、同時実行を許可しておく。

    /// <summary>How の選択を切り替える。Who が2つそろっていれば貼り付ける。</summary>
    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task ToggleHowAsync(HowOptionViewModel option)
    {
        if (_isPasting)
        {
            return;
        }
        option.IsSelected = !option.IsSelected;
        OnPropertyChanged(nameof(How));
        OnPropertyChanged(nameof(Preview));
        await PasteIfCompleteAsync();
    }

    /// <summary>1回目は from、2回目以降は to にする。How が選ばれていれば貼り付ける。</summary>
    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task SelectWhoAsync(string who)
    {
        if (_isPasting)
        {
            return;
        }
        if (From is null)
        {
            From = who;
        }
        else
        {
            To = who;
        }
        await PasteIfCompleteAsync();
    }

    [RelayCommand]
    private void Clear()
    {
        if (_isPasting)
        {
            return;
        }
        Reset();
    }

    private async Task PasteIfCompleteAsync()
    {
        if (How is not { } how || From is null || To is null)
        {
            return;
        }

        // now の場合は、そろった時点の時刻を使う
        var text = _builder.Build(how, From, To, GetTime());

        _isPasting = true;
        try
        {
            await _paster.PasteAsync(text);
            StatusMessage = null;
        }
        catch (Exception ex)
        {
            StatusMessage = $"貼り付けに失敗しました: {ex.Message}";
        }
        finally
        {
            _isPasting = false;
            Reset();
        }
    }

    /// <summary>指定時刻があれば今日のその時刻、無ければ現在時刻。</summary>
    private DateTimeOffset GetTime()
    {
        var now = _timeProvider.GetLocalNow();
        return FixedTime is { } time
            ? new DateTimeOffset(now.Date + time.ToTimeSpan(), now.Offset)
            : now;
    }

    /// <summary>How・Who の選択を外す。When はそのまま残す。</summary>
    private void Reset()
    {
        foreach (var option in HowOptions)
        {
            option.IsSelected = false;
        }
        OnPropertyChanged(nameof(How));
        From = null;
        To = null;
        OnPropertyChanged(nameof(Preview));
    }
}
