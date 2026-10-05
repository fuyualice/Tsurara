using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Whiteboard.Services;

namespace Whiteboard.Interop;

/// <summary>
/// 窓の位置の取得と復元。モニターごとに DPI が違っても扱えるよう、物理ピクセルでやり取りする。
/// </summary>
public static class WindowPlacement
{
    private const uint MONITOR_DEFAULTTONEAREST = 2;
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_NOACTIVATE = 0x0010;

    public static WindowPosition? Capture(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero || !GetWindowRect(hwnd, out var rect))
        {
            return null;
        }
        return new WindowPosition(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);
    }

    /// <summary>
    /// 保存した位置に移動する。最も近いモニターの作業領域からはみ出す場合は、内側に収まるよう補正する。
    /// HWND の作成後（SourceInitialized 以降）に呼ぶ。
    /// </summary>
    public static void Restore(Window window, WindowPosition position)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        var rect = new RECT
        {
            Left = position.Left,
            Top = position.Top,
            Right = position.Left + position.Width,
            Bottom = position.Top + position.Height,
        };
        var monitor = MonitorFromRect(ref rect, MONITOR_DEFAULTTONEAREST);
        var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
        if (!GetMonitorInfo(monitor, ref info))
        {
            return;
        }

        var work = info.rcWork;
        var left = Math.Clamp(position.Left, work.Left, Math.Max(work.Left, work.Right - position.Width));
        var top = Math.Clamp(position.Top, work.Top, Math.Max(work.Top, work.Bottom - position.Height));

        SetWindowPos(hwnd, IntPtr.Zero, left, top, 0, 0, SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromRect(ref RECT rect, uint flags);

    [DllImport("user32.dll", EntryPoint = "GetMonitorInfoW")]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MONITORINFO info);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hwnd, IntPtr insertAfter, int x, int y, int cx, int cy, uint flags);
}
