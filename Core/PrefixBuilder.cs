using System.Globalization;
using System.Text.RegularExpressions;

namespace Whiteboard.Core;

/// <summary>
/// テンプレートに How・from・to・時刻を埋め込んで前置き文字列を作る。
/// </summary>
/// <remarks>
/// テンプレートでは {time} {how} {from} {to} を置き換える。
/// 値の中にプレースホルダーと同じ文字列が含まれていても二重に置き換えないよう、一度に置換する。
/// </remarks>
public sealed partial class PrefixBuilder
{
    private readonly string _template;
    private readonly string _timeFormat;

    public PrefixBuilder(string template, string timeFormat)
    {
        _template = template;
        _timeFormat = timeFormat;
    }

    public string Build(string how, string from, string to, DateTimeOffset time)
    {
        var formattedTime = time.ToString(_timeFormat, CultureInfo.InvariantCulture);

        return Placeholder().Replace(_template, match => match.Groups[1].Value switch
        {
            "time" => formattedTime,
            "how" => how,
            "from" => from,
            "to" => to,
            _ => match.Value,
        });
    }

    [GeneratedRegex(@"\{(time|how|from|to)\}")]
    private static partial Regex Placeholder();
}
