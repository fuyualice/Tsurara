using System.Windows;
using Whiteboard.Core;

namespace Whiteboard.Views;

/// <summary>Fluent テーマ（Application.ThemeMode）を切り替える。</summary>
public sealed class ThemeSwitcher : IThemeSwitcher
{
    public void Apply(AppTheme theme)
    {
        Application.Current.ThemeMode = theme switch
        {
            AppTheme.Light => ThemeMode.Light,
            AppTheme.Dark => ThemeMode.Dark,
            _ => ThemeMode.System,
        };
    }
}
