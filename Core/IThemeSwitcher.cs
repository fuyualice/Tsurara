namespace Whiteboard.Core;

/// <summary>テーマを画面に反映する。</summary>
public interface IThemeSwitcher
{
    void Apply(AppTheme theme);
}
