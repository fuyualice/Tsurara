namespace Whiteboard.Services;

/// <summary>Who の候補をまとめた1グループ（パレットの1タブ）。who フォルダの .txt 1ファイルに対応する。</summary>
public sealed record WhoGroup(string Name, IReadOnlyList<string> Members);
