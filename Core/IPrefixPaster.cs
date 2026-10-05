namespace Whiteboard.Core;

/// <summary>前置き文字列を前面の窓の入力欄に貼り付ける。</summary>
public interface IPrefixPaster
{
    Task PasteAsync(string text);
}
