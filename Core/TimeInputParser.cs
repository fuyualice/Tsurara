using System.Globalization;

namespace Whiteboard.Core;

/// <summary>
/// 手入力の時刻を読み取る。「14:30」「9:05」「1430」「930」「14」の形を受け付ける。
/// 全角の数字とコロンも半角とみなす。
/// </summary>
public static class TimeInputParser
{
    public static bool TryParse(string? input, out TimeOnly time)
    {
        time = default;
        if (input is null)
        {
            return false;
        }

        var text = Normalize(input.Trim());
        int hour;
        int minute;

        var colon = text.IndexOf(':');
        if (colon >= 0)
        {
            // 「H:mm」「HH:mm」
            var h = text[..colon];
            var m = text[(colon + 1)..];
            if (h.Length is < 1 or > 2 || m.Length != 2 || !IsDigits(h) || !IsDigits(m))
            {
                return false;
            }
            hour = int.Parse(h, CultureInfo.InvariantCulture);
            minute = int.Parse(m, CultureInfo.InvariantCulture);
        }
        else
        {
            if (!IsDigits(text))
            {
                return false;
            }
            switch (text.Length)
            {
                case 1 or 2: // 「H」「HH」は正時
                    hour = int.Parse(text, CultureInfo.InvariantCulture);
                    minute = 0;
                    break;
                case 3 or 4: // 「Hmm」「HHmm」
                    hour = int.Parse(text[..^2], CultureInfo.InvariantCulture);
                    minute = int.Parse(text[^2..], CultureInfo.InvariantCulture);
                    break;
                default:
                    return false;
            }
        }

        if (hour is < 0 or > 23 || minute is < 0 or > 59)
        {
            return false;
        }
        time = new TimeOnly(hour, minute);
        return true;
    }

    private static string Normalize(string text) => new(text.Select(c => c switch
    {
        >= '０' and <= '９' => (char)('0' + (c - '０')),
        '：' => ':',
        _ => c,
    }).ToArray());

    private static bool IsDigits(string text) => text.Length > 0 && text.All(char.IsAsciiDigit);
}
