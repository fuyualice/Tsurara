namespace Whiteboard.Views;

/// <summary>
/// プレビュー欄を続けてクリックしたときに表示する文字列と、その発動・表示の条件。
/// </summary>
internal static class EasterEgg
{
    public const string OneLine = "♬Shirotsu & Yuura - Beyond Time";
    public const string Title = "♬ Beyond Time";
    public const string Artist = "Shirotsu & Yuura";

    /// <summary>既定のフォントに ♬ が無い場合の代わりのフォント。</summary>
    public const string FallbackFontFamily = "Segoe UI Symbol";

    public const int RequiredClicks = 5;
    public static readonly TimeSpan MaxClickInterval = TimeSpan.FromSeconds(1.5);
    public static readonly TimeSpan DisplayDuration = TimeSpan.FromSeconds(4);
}
