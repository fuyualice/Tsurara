using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Whiteboard.Interop;
using Whiteboard.Services;
using Whiteboard.ViewModels;

namespace Whiteboard.Views;

public partial class MainWindow : Window
{
    private readonly ForegroundSwitcher _foreground = new();
    private bool _isEditingWhen;
    private readonly DispatcherTimer _eggTimer = new() { Interval = EasterEgg.DisplayDuration };
    private int _eggClickCount;
    private long _lastEggClickTicks;
    private bool _isEggShown;

    public MainWindow(SettingsService settingsService)
    {
        InitializeComponent();
        NoActivateWindow.Attach(this);

        EggOneLine.Text = EasterEgg.OneLine;
        EggTitle.Text = EasterEgg.Title;
        EggArtist.Text = EasterEgg.Artist;
        _eggTimer.Tick += (_, _) => HideEgg(animate: true);
        // ボタンの Click はコマンドより先に届くので、表示を消してから本来の処理が動く
        AddHandler(ButtonBase.ClickEvent, new RoutedEventHandler((_, _) => HideEgg(animate: false)), handledEventsToo: true);

        // 表示名は csproj の Product から取る
        Title = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyProductAttribute>()?.Product ?? string.Empty;

        if (settingsService.LoadWindowPosition() is { } position)
        {
            SourceInitialized += (_, _) => WindowPlacement.Restore(this, position);
        }
        else
        {
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }

        Closing += (_, _) =>
        {
            if (WindowPlacement.Capture(this) is { } current)
            {
                settingsService.SaveWindowPosition(current);
            }
        };
    }

    private PaletteViewModel ViewModel => (PaletteViewModel)DataContext;

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    // 新しい版のダウンロードページを既定のブラウザで開く（自動ではダウンロードしない）
    private void Update_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.AvailableUpdate is not { } update)
        {
            return;
        }
        try
        {
            Process.Start(new ProcessStartInfo(update.ReleasePage.AbsoluteUri) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            MessageBox.Show(this, $"ブラウザを開けませんでした。次のページを開いてください。\n{update.ReleasePage}", Title);
        }
    }

    private void WhoTab_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        ((TabItem)sender).IsSelected = true;
        e.Handled = true;
    }

    // ---- When の時刻入力 ----
    // パレットは普段アクティブにならないので、入力欄をクリックしたときだけアクティブにし、
    // 確定・取り消しで元の窓（Discord）に戻す。

    private void WhenTextBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_isEditingWhen)
        {
            return;
        }
        _isEditingWhen = true;
        _foreground.Activate(this);
        WhenTextBox.Focus();
        WhenTextBox.SelectAll();
        e.Handled = true;
    }

    private void WhenTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter:
                if (ViewModel.ApplyWhenInput())
                {
                    EndWhenEditing(restoreForeground: true);
                }
                e.Handled = true;
                break;
            case Key.Escape:
                ViewModel.CancelWhenInput();
                EndWhenEditing(restoreForeground: true);
                e.Handled = true;
                break;
        }
    }

    // 入力中に入力欄以外を押した場合は、先に入力を終えて元の窓に戻す（貼り付け先がパレットにならないように）
    private void Window_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_isEditingWhen && !WhenTextBox.IsMouseOver)
        {
            ViewModel.FinishWhenInput();
            EndWhenEditing(restoreForeground: true);
        }
    }

    // ユーザーが自分で別の窓に切り替えた場合は、入力を終えるだけで窓は戻さない
    private void Window_Deactivated(object? sender, EventArgs e)
    {
        if (_isEditingWhen)
        {
            ViewModel.FinishWhenInput();
            EndWhenEditing(restoreForeground: false);
        }
    }

    private void EndWhenEditing(bool restoreForeground)
    {
        _isEditingWhen = false;
        Keyboard.ClearFocus();
        if (restoreForeground)
        {
            _foreground.RestorePrevious();
        }
        else
        {
            _foreground.Forget();
        }
    }

    // ---- プレビュー欄の隠し表示 ----
    // 窓の移動（DragMove）を妨げないよう、クリックは数えるだけで処理済みにしない。

    private void PreviewCard_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_isEggShown)
        {
            return;
        }
        var now = Environment.TickCount64;
        _eggClickCount = now - _lastEggClickTicks <= EasterEgg.MaxClickInterval.TotalMilliseconds ? _eggClickCount + 1 : 1;
        _lastEggClickTicks = now;
        if (_eggClickCount >= EasterEgg.RequiredClicks)
        {
            _eggClickCount = 0;
            ShowEgg();
        }
    }

    private void ShowEgg()
    {
        _isEggShown = true;
        var font = new FontFamily(FontFamily.BaseUri, $"{FontFamily.Source}, {EasterEgg.FallbackFontFamily}");
        EggPanel.SetValue(TextElement.FontFamilyProperty, font);

        var typeface = new Typeface(font, EggOneLine.FontStyle, EggOneLine.FontWeight, EggOneLine.FontStretch);
        var oneLine = new FormattedText(EasterEgg.OneLine, CultureInfo.CurrentUICulture, FlowDirection, typeface,
            EggOneLine.FontSize, Brushes.Black, VisualTreeHelper.GetDpi(this).PixelsPerDip);
        var fits = oneLine.WidthIncludingTrailingWhitespace <= PreviewContent.ActualWidth;
        EggOneLine.Visibility = fits ? Visibility.Visible : Visibility.Collapsed;
        EggTwoLines.Visibility = fits ? Visibility.Collapsed : Visibility.Visible;

        EggPanel.Visibility = Visibility.Visible;
        var animate = SystemParameters.ClientAreaAnimation;
        Animate(PreviewContent, UIElement.OpacityProperty, 0, animate ? 300 : 0);
        Animate(EggPanel, UIElement.OpacityProperty, 1, animate ? 600 : 0, delayMs: 200);
        if (animate)
        {
            EggTranslate.BeginAnimation(TranslateTransform.YProperty, null);
            EggTranslate.Y = 6;
        }
        Animate(EggTranslate, TranslateTransform.YProperty, 0, animate ? 600 : 0, delayMs: 200);

        _eggTimer.Start();
    }

    private void HideEgg(bool animate)
    {
        _eggTimer.Stop();
        _isEggShown = false;
        if (EggPanel.Visibility != Visibility.Visible)
        {
            return;
        }
        animate &= SystemParameters.ClientAreaAnimation;
        Animate(EggPanel, UIElement.OpacityProperty, 0, animate ? 600 : 0, completed: () =>
        {
            if (!_isEggShown)
            {
                EggPanel.Visibility = Visibility.Collapsed;
            }
        });
        Animate(PreviewContent, UIElement.OpacityProperty, 1, animate ? 500 : 0, delayMs: animate ? 300 : 0);
    }

    // durationMs が 0 のときはアニメーションせずに値をすぐ設定する
    private static void Animate(IAnimatable target, DependencyProperty property, double to, int durationMs,
        int delayMs = 0, Action? completed = null)
    {
        if (durationMs == 0)
        {
            target.BeginAnimation(property, null);
            ((DependencyObject)target).SetValue(property, to);
            completed?.Invoke();
            return;
        }
        var animation = new DoubleAnimation(to, TimeSpan.FromMilliseconds(durationMs))
        {
            BeginTime = TimeSpan.FromMilliseconds(delayMs),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
        };
        if (completed is not null)
        {
            animation.Completed += (_, _) => completed();
        }
        target.BeginAnimation(property, animation);
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => DragMove();
}
