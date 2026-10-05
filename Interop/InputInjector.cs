using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Whiteboard.Core;

namespace Whiteboard.Interop;

/// <summary>
/// クリップボード経由で文字列を前面の窓に貼り付ける。
/// 退避 → セット → Ctrl+V → 待機 → 復元 の順に行う。Enter は送らない。
/// </summary>
public sealed class InputInjector : IPrefixPaster
{
    private const int OpenRetryCount = 10;
    private const int OpenRetryIntervalMs = 20;

    // クリップボードの所有者にする窓。所有者が無いと SetClipboardData が失敗する
    private readonly Window _owner;

    public InputInjector(Window owner)
    {
        _owner = owner;
    }

    /// <summary>Ctrl+V を送ってからクリップボードを復元するまでの待ち時間。</summary>
    public int RestoreDelayMs { get; set; } = 150;

    public async Task PasteAsync(string text)
    {
        // 1. 退避（テキスト形式のみ）
        string? saved = null;
        await WithClipboardAsync(() => saved = ReadText());

        // 2. セット（クリップボード履歴には残さない）
        await WithClipboardAsync(() => WriteText(text));

        // 3. Ctrl+V
        SendCtrlV();

        // 4. 待ってから復元
        await Task.Delay(RestoreDelayMs);
        await WithClipboardAsync(() =>
        {
            if (saved is null)
            {
                EmptyClipboard();
            }
            else
            {
                WriteText(saved);
            }
        });
    }

    /// <summary>クリップボードを開いて処理する。他アプリが使用中なら短い間隔でリトライする。</summary>
    private async Task WithClipboardAsync(Action action)
    {
        var hwnd = new WindowInteropHelper(_owner).Handle;
        for (var i = 0; ; i++)
        {
            if (OpenClipboard(hwnd))
            {
                try
                {
                    action();
                    return;
                }
                finally
                {
                    CloseClipboard();
                }
            }

            if (i >= OpenRetryCount)
            {
                throw new InvalidOperationException("クリップボードを開けませんでした（他のアプリが使用中）。");
            }
            await Task.Delay(OpenRetryIntervalMs);
        }
    }

    private static string? ReadText()
    {
        if (!IsClipboardFormatAvailable(CF_UNICODETEXT))
        {
            return null;
        }

        var handle = GetClipboardData(CF_UNICODETEXT);
        if (handle == IntPtr.Zero)
        {
            return null;
        }

        var ptr = GlobalLock(handle);
        if (ptr == IntPtr.Zero)
        {
            return null;
        }
        try
        {
            return Marshal.PtrToStringUni(ptr);
        }
        finally
        {
            GlobalUnlock(handle);
        }
    }

    /// <summary>クリップボードを空にしてテキストをセットする。履歴・クラウド同期の対象外にする。</summary>
    private static void WriteText(string text)
    {
        if (!EmptyClipboard())
        {
            throw new InvalidOperationException("クリップボードを空にできませんでした。");
        }

        var chars = (text + '\0').ToCharArray();
        SetGlobal(CF_UNICODETEXT, h => Marshal.Copy(chars, 0, h, chars.Length), chars.Length * sizeof(char));

        SetGlobal(RegisterClipboardFormat("ExcludeClipboardContentFromMonitorProcessing"), h => Marshal.WriteInt32(h, 0), sizeof(int));
        SetGlobal(RegisterClipboardFormat("CanIncludeInClipboardHistory"), h => Marshal.WriteInt32(h, 0), sizeof(int));
        SetGlobal(RegisterClipboardFormat("CanUploadToCloudClipboard"), h => Marshal.WriteInt32(h, 0), sizeof(int));
    }

    private static void SetGlobal(uint format, Action<IntPtr> write, int size)
    {
        var handle = GlobalAlloc(GMEM_MOVEABLE, (UIntPtr)size);
        if (handle == IntPtr.Zero)
        {
            throw new OutOfMemoryException();
        }

        var ptr = GlobalLock(handle);
        try
        {
            write(ptr);
        }
        finally
        {
            GlobalUnlock(handle);
        }

        // 成功した場合、メモリの所有権はシステムに移る
        if (SetClipboardData(format, handle) == IntPtr.Zero)
        {
            GlobalFree(handle);
            throw new InvalidOperationException($"クリップボードにデータをセットできませんでした（形式 {format}）。");
        }
    }

    private static void SendCtrlV()
    {
        INPUT[] inputs =
        [
            KeyInput(VK_CONTROL, keyUp: false),
            KeyInput(VK_V, keyUp: false),
            KeyInput(VK_V, keyUp: true),
            KeyInput(VK_CONTROL, keyUp: true),
        ];

        var sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
        if (sent != inputs.Length)
        {
            throw new InvalidOperationException($"SendInput に失敗しました（エラー {Marshal.GetLastWin32Error()}）。");
        }
    }

    private static INPUT KeyInput(ushort vk, bool keyUp) => new()
    {
        type = INPUT_KEYBOARD,
        u = new InputUnion
        {
            ki = new KEYBDINPUT
            {
                wVk = vk,
                dwFlags = keyUp ? KEYEVENTF_KEYUP : 0,
            },
        },
    };

    private const uint CF_UNICODETEXT = 13;
    private const uint GMEM_MOVEABLE = 0x0002;
    private const uint INPUT_KEYBOARD = 1;
    private const uint KEYEVENTF_KEYUP = 0x0002;
    private const ushort VK_CONTROL = 0x11;
    private const ushort VK_V = 0x56;

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public InputUnion u;
    }

    // 共用体のサイズを最大のメンバー（MOUSEINPUT）に合わせるため、3種類とも定義する
    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public MOUSEINPUT mi;
        [FieldOffset(0)] public KEYBDINPUT ki;
        [FieldOffset(0)] public HARDWAREINPUT hi;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HARDWAREINPUT
    {
        public uint uMsg;
        public ushort wParamL;
        public ushort wParamH;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool OpenClipboard(IntPtr hWndNewOwner);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool CloseClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool EmptyClipboard();

    [DllImport("user32.dll")]
    private static extern bool IsClipboardFormatAvailable(uint format);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr GetClipboardData(uint uFormat);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);

    [DllImport("user32.dll", EntryPoint = "RegisterClipboardFormatW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint RegisterClipboardFormat(string lpszFormat);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalAlloc(uint uFlags, UIntPtr dwBytes);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalLock(IntPtr hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalUnlock(IntPtr hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalFree(IntPtr hMem);
}
