namespace Whiteboard.Core;

/// <summary>パレットの設定欄で変えた値を、次回の起動用に保存する。</summary>
public interface IUserSettingsStore
{
    bool SaveTheme(AppTheme theme);

    bool SaveFontSize(double fontSize);

    bool SaveShowClock(bool showClock);
}
