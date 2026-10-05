using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Whiteboard.Interop;

/// <summary>
/// キーボード入力が必要なときだけパレットを一時的にアクティブにし、終わったら元の窓に戻す。
/// </summary>
/// <remarks>
/// パレットは WS_EX_NOACTIVATE なので、クリックしただけではアクティブにならない。
/// 直前にクリックを受けたプロセスは SetForegroundWindow を呼べるので、クリック直後に呼ぶ。
/// </remarks>
public sealed class ForegroundSwitcher
{
    private IntPtr _previous;

    /// <summary>今アクティブな窓を覚えてから、パレットをアクティブにする。</summary>
    public void Activate(Window window)
    {
        var own = new WindowInteropHelper(window).Handle;
        var foreground = GetForegroundWindow();
        if (foreground != own)
        {
            _previous = foreground;
        }
        SetForegroundWindow(own);
    }

    /// <summary>覚えておいた窓をアクティブに戻す。</summary>
    public void RestorePrevious()
    {
        if (_previous != IntPtr.Zero && IsWindow(_previous))
        {
            SetForegroundWindow(_previous);
        }
        _previous = IntPtr.Zero;
    }

    /// <summary>ユーザーが自分で別の窓に切り替えた場合など、戻さずに忘れる。</summary>
    public void Forget() => _previous = IntPtr.Zero;

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern bool IsWindow(IntPtr hwnd);
}
