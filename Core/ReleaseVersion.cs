namespace Whiteboard.Core;

/// <summary>GitHub のリリースのタグ（"v0.2.0" など）とアプリのバージョンを比べる。</summary>
public static class ReleaseVersion
{
    /// <summary>
    /// "v0.2.0" / "0.2" / "V1.0.0.1" のようなタグを読む。足りない桁は 0 で補う。
    /// "v0.2.0-beta" のような接尾辞付きや、数字以外を含むものは読めない（false）。
    /// </summary>
    public static bool TryParse(string? tag, out Version version)
    {
        version = new Version();
        var text = tag?.Trim() ?? string.Empty;
        if (text.StartsWith('v') || text.StartsWith('V'))
        {
            text = text[1..];
        }
        if (text.Length == 0 || !text.All(c => char.IsAsciiDigit(c) || c == '.'))
        {
            return false;
        }
        if (text.IndexOf('.') < 0)
        {
            // Version.TryParse は "1" のような1桁だけの形を受け付けない
            text += ".0";
        }
        if (!Version.TryParse(text, out var parsed))
        {
            return false;
        }
        version = Normalize(parsed);
        return true;
    }

    /// <summary>latest が current より新しいか。桁数の違い（0.2 と 0.2.0.0）は同じ版として扱う。</summary>
    public static bool IsNewer(Version latest, Version current) => Normalize(latest) > Normalize(current);

    /// <summary>
    /// 4桁にそろえる。Version は省略された桁を -1 として持ち、0.2 &lt; 0.2.0 になってしまうため。
    /// </summary>
    private static Version Normalize(Version v) =>
        new(v.Major, v.Minor, Math.Max(v.Build, 0), Math.Max(v.Revision, 0));

    /// <summary>表示用。末尾の 0 の桁を省く（0.2.0.0 → 0.2.0、1.0.0.1 はそのまま）。最低3桁。</summary>
    public static string ToDisplayString(Version version)
    {
        var v = Normalize(version);
        return v.Revision > 0 ? v.ToString(4) : v.ToString(3);
    }
}
