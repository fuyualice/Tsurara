using Whiteboard.Core;

namespace Whiteboard.Services;

/// <summary>アプリの設定。既定値は組み込みの値で、設定ファイルがあれば上書きされる。</summary>
public sealed record AppSettings
{
    public const double MinFontSize = 10;
    public const double MaxFontSize = 32;

    public string TimeFormat { get; init; } = "HH:mm";

    public string Template { get; init; } = "{time} {how} {from}→{to} ";

    public IReadOnlyList<string> How { get; init; } = ["tel", "mail"];

    /// <summary>Who の候補（タブごと）。settings.json ではなく、exe と同じフォルダの who フォルダから読む。</summary>
    public IReadOnlyList<WhoGroup> WhoGroups { get; init; } = [];

    public int ClipboardRestoreDelayMs { get; init; } = 150;

    public AppTheme Theme { get; init; } = AppTheme.System;

    /// <summary>パレットの文字サイズ。プレビューはこれより少し大きく表示する。</summary>
    public double FontSize { get; init; } = 16;

    /// <summary>パレットの一番上に時計を表示するか。</summary>
    public bool ShowClock { get; init; } = true;

    /// <summary>起動時に、新しい版が公開されていないか確認するか。</summary>
    public bool CheckForUpdates { get; init; } = true;
}
