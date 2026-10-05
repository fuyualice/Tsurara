using System.Net.Http;
using System.Reflection;
using System.Windows;
using Whiteboard.Interop;
using Whiteboard.Services;
using Whiteboard.ViewModels;
using Whiteboard.Views;

namespace Whiteboard;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var settingsService = new SettingsService();
        var settings = settingsService.Load();
        var themeSwitcher = new ThemeSwitcher();
        themeSwitcher.Apply(settings.Theme);

        var window = new MainWindow(settingsService);
        var injector = new InputInjector(window) { RestoreDelayMs = settings.ClipboardRestoreDelayMs };
        var viewModel = new PaletteViewModel(settings, injector, TimeProvider.System, themeSwitcher, settingsService);
        window.DataContext = viewModel;
        window.Show();

        if (settings.CheckForUpdates && CreateUpdateChecker() is { } checker)
        {
            // 起動時に1回だけ確認する。結果を待たずにパレットを使えるようにする
            _ = viewModel.CheckForUpdatesAsync(checker);
        }
    }

    /// <summary>
    /// 確認先のリポジトリは csproj の UpdateRepository（AssemblyMetadata）から取る。
    /// 指定が無い・形がおかしい場合は確認しない。
    /// </summary>
    private static GitHubUpdateChecker? CreateUpdateChecker()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var repository = assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == "UpdateRepository")?.Value;
        if (!GitHubUpdateChecker.IsValidRepository(repository) || assembly.GetName().Version is not { } version)
        {
            return null;
        }
        var product = assembly.GetCustomAttribute<AssemblyProductAttribute>()?.Product ?? "Whiteboard";
        return new GitHubUpdateChecker(new HttpClient(), repository!, version, product);
    }
}
