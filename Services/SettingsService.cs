using System.Globalization;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Whiteboard.Core;

namespace Whiteboard.Services;

/// <summary>
/// 設定の読み込みと窓の位置の保存。
/// 組み込みの既定値 → exe と同じフォルダの settings.json → %APPDATA% の settings.json の順に上書きする。
/// Who の候補は exe と同じフォルダの who フォルダ（.txt 1ファイルが1タブ）から読む。
/// </summary>
public sealed partial class SettingsService : IUserSettingsStore
{
    /// <summary>
    /// %APPDATA% 配下のフォルダ名。表示名を変えても設定が引き継がれるよう、表示名とは独立した定数にする。
    /// </summary>
    public const string AppDataFolderName = "Whiteboard";

    private const string SettingsFileName = "settings.json";
    private const string WhoDirectoryName = "who";
    private const string WindowPositionFileName = "window.json";
    private const int MaxClipboardRestoreDelayMs = 5000;

    private static readonly JsonNodeOptions NodeOptions = new() { PropertyNameCaseInsensitive = true };

    private static readonly JsonDocumentOptions DocumentOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly string _appDirectory;
    private readonly string _userDirectory;

    public SettingsService()
        : this(
            AppContext.BaseDirectory,
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppDataFolderName))
    {
    }

    /// <param name="appDirectory">配布用ファイル（settings.json・who フォルダ）を置くフォルダ。通常は exe と同じフォルダ。</param>
    /// <param name="userDirectory">ユーザーごとの設定と窓の位置を保存するフォルダ。</param>
    public SettingsService(string appDirectory, string userDirectory)
    {
        _appDirectory = appDirectory;
        _userDirectory = userDirectory;
    }

    private string DefaultSettingsPath => Path.Combine(_appDirectory, SettingsFileName);

    private string WhoDirectory => Path.Combine(_appDirectory, WhoDirectoryName);

    private string UserSettingsPath => Path.Combine(_userDirectory, SettingsFileName);

    private string WindowPositionPath => Path.Combine(_userDirectory, WindowPositionFileName);

    /// <summary>
    /// 設定を読み込む。ファイルが無い・壊れている場合や、項目の値が不正な場合は、その項目だけ下の層の値を使う。
    /// </summary>
    public AppSettings Load()
    {
        var settings = new AppSettings();
        foreach (var path in new[] { DefaultSettingsPath, UserSettingsPath })
        {
            if (ReadObject(path) is { } layer)
            {
                settings = Apply(settings, layer);
            }
        }
        return settings with { WhoGroups = LoadWhoGroups() };
    }

    /// <summary>
    /// who フォルダの .txt をファイル名順に読み、1ファイルを1グループにする。
    /// フォルダが無い・読めない場合は空。
    /// </summary>
    private IReadOnlyList<WhoGroup> LoadWhoGroups()
    {
        try
        {
            if (!Directory.Exists(WhoDirectory))
            {
                return [];
            }
            return Directory.GetFiles(WhoDirectory, "*.txt")
                .OrderBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
                .Select(path => new WhoGroup(GroupName(path), ReadMembers(path)))
                .ToArray();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    /// <summary>
    /// 1行1件で読む。前後の空白を除き、空行・「#」で始まる行・重複は無視する。読めない場合は空。
    /// </summary>
    private static IReadOnlyList<string> ReadMembers(string path)
    {
        try
        {
            return File.ReadAllLines(path)
                .Select(line => line.Trim())
                .Where(line => line.Length > 0 && !line.StartsWith('#'))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    /// <summary>
    /// タブ名はファイル名（拡張子なし）。並び順を指定するための先頭の「1_」のような番号は除く。
    /// </summary>
    private static string GroupName(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        var withoutOrder = OrderPrefix().Replace(name, string.Empty);
        return withoutOrder.Length > 0 ? withoutOrder : name;
    }

    [GeneratedRegex(@"^\d+_")]
    private static partial Regex OrderPrefix();

    public bool SaveTheme(AppTheme theme) => SaveUserValue("theme", theme.ToString().ToLowerInvariant());

    public bool SaveFontSize(double fontSize) => SaveUserValue("fontSize", fontSize);

    public bool SaveShowClock(bool showClock) => SaveUserValue("showClock", showClock);

    /// <summary>
    /// %APPDATA% の settings.json に1項目を書き込む。ほかの項目はそのまま残す。
    /// ファイルが壊れている場合は、ユーザーの書いた内容を消さないよう保存しない。
    /// </summary>
    /// <returns>保存できたら true。</returns>
    private bool SaveUserValue(string key, JsonNode value)
    {
        try
        {
            var settings = File.Exists(UserSettingsPath) ? ReadObject(UserSettingsPath) : new JsonObject();
            if (settings is null)
            {
                return false;
            }
            settings[key] = value;
            Directory.CreateDirectory(_userDirectory);
            File.WriteAllText(UserSettingsPath, settings.ToJsonString(SerializerOptions));
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    public WindowPosition? LoadWindowPosition()
    {
        try
        {
            if (!File.Exists(WindowPositionPath))
            {
                return null;
            }
            var position = JsonSerializer.Deserialize<WindowPosition>(File.ReadAllText(WindowPositionPath), SerializerOptions);
            return position is { Width: > 0, Height: > 0 } ? position : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    public void SaveWindowPosition(WindowPosition position)
    {
        try
        {
            Directory.CreateDirectory(_userDirectory);
            File.WriteAllText(WindowPositionPath, JsonSerializer.Serialize(position, SerializerOptions));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // 位置を保存できなくても終了は妨げない
        }
    }

    private static AppSettings Apply(AppSettings current, JsonObject layer) => current with
    {
        TimeFormat = Get<string>(layer, "timeFormat") is { } format && IsValidTimeFormat(format) ? format : current.TimeFormat,
        Template = Get<string>(layer, "template") is { Length: > 0 } template ? template : current.Template,
        How = NonEmptyList(Get<List<string?>>(layer, "how")) ?? current.How,
        ClipboardRestoreDelayMs = Get<int?>(layer, "clipboardRestoreDelayMs") is { } delay and >= 0 and <= MaxClipboardRestoreDelayMs
            ? delay
            : current.ClipboardRestoreDelayMs,
        Theme = ParseTheme(Get<string>(layer, "theme")) ?? current.Theme,
        FontSize = Get<double?>(layer, "fontSize") is { } fontSize and >= AppSettings.MinFontSize and <= AppSettings.MaxFontSize
            ? fontSize
            : current.FontSize,
        ShowClock = Get<bool?>(layer, "showClock") ?? current.ShowClock,
        CheckForUpdates = Get<bool?>(layer, "checkForUpdates") ?? current.CheckForUpdates,
    };

    /// <summary>"system" / "light" / "dark"（大文字小文字は問わない）。数値などそれ以外は null。</summary>
    private static AppTheme? ParseTheme(string? value) =>
        Enum.GetValues<AppTheme>().Cast<AppTheme?>()
            .FirstOrDefault(t => string.Equals(t.ToString(), value, StringComparison.OrdinalIgnoreCase));

    private static JsonObject? ReadObject(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return null;
            }
            return JsonNode.Parse(File.ReadAllText(path), NodeOptions, DocumentOptions) as JsonObject;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    /// <summary>項目を読む。項目が無い・型が違う場合は null。</summary>
    private static T? Get<T>(JsonObject obj, string name)
    {
        if (!obj.TryGetPropertyValue(name, out var node) || node is null)
        {
            return default;
        }
        try
        {
            return node.Deserialize<T>(SerializerOptions);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            return default;
        }
    }

    /// <summary>空文字や null を除いたリスト。1件も残らなければ null。</summary>
    private static IReadOnlyList<string>? NonEmptyList(List<string?>? items)
    {
        var result = items?.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s!).ToArray();
        return result is { Length: > 0 } ? result : null;
    }

    private static bool IsValidTimeFormat(string format)
    {
        if (format.Length == 0)
        {
            return false;
        }
        try
        {
            DateTimeOffset.Now.ToString(format, CultureInfo.InvariantCulture);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
