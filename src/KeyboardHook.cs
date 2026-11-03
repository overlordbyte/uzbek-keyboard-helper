using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace UzbekKeyboardHelper;

/// <summary>
/// Low-level keyboard hook:
///   - Right Alt (AltGr) + S/O/G/C → ş/ö/ğ/ç
///   - Right Alt (AltGr) + Shift + S/O/G/C → Ş/Ö/Ğ/Ç
///   - Right Alt boshqa holatda ham bloklanadi (AltGr ilovalarni buzmasligi uchun)
///   - Left Alt: umuman tegmaydi, odatiy ishlayveradi
/// </summary>
public sealed class KeyboardHook : IDisposable
{
    // Hook type
    private const int WH_KEYBOARD_LL = 13;

    // Window messages
    private const int WM_KEYDOWN    = 0x0100;
    private const int WM_KEYUP      = 0x0101;
    private const int WM_SYSKEYDOWN = 0x0104;
    private const int WM_SYSKEYUP   = 0x0105;

    // Virtual key codes
    private const int VK_RMENU    = 0xA5; // Right Alt (AltGr)
    private const int VK_LCONTROL = 0xA2; // Left Ctrl
    private const int VK_LSHIFT   = 0xA0;
    private const int VK_RSHIFT   = 0xA1;

    // Target keys
    private const int VK_S = 0x53;
    private const int VK_O = 0x4F;
    private const int VK_G = 0x47;
    private const int VK_C = 0x43;

    // KBDLLHOOKSTRUCT flags
    private const int LLKHF_EXTENDED = 0x01;

    // SendInput constants
    private const uint INPUT_KEYBOARD    = 1;
    private const uint KEYEVENTF_UNICODE = 0x0004;
    private const uint KEYEVENTF_KEYUP   = 0x0002;

    // Marker to identify our own injected events so we don't re-process them
    private static readonly IntPtr InjectedMarker = new(0x555A4B48); // "UZKH"

    private IntPtr _hookHandle = IntPtr.Zero;
    private readonly LowLevelKeyboardProc _proc;

    // Physical state of Right Alt
    private bool _rightAltDown;
    // Physical Shift state
    private bool _shiftDown;

    // Keys whose key-down we suppressed; we must also suppress their key-up
    private readonly HashSet<int> _suppressedKeys = [];

    public bool Enabled { get; set; } = true;

    public KeyboardHook()
    {
        _proc = HookCallback;
    }

    public void Start()
    {
        _hookHandle = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, GetModuleHandle(null), 0);
    }

    public void Stop()
    {
        if (_hookHandle != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hookHandle);
            _hookHandle = IntPtr.Zero;
        }
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode < 0 || !Enabled)
            return CallNextHookEx(_hookHandle, nCode, wParam, lParam);

        var ks = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);

        // Skip events that we injected ourselves
        if (ks.dwExtraInfo == InjectedMarker)
            return CallNextHookEx(_hookHandle, nCode, wParam, lParam);

        bool isDown     = wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN;
        bool isUp       = wParam == (IntPtr)WM_KEYUP   || wParam == (IntPtr)WM_SYSKEYUP;
        bool isExtended = (ks.flags & LLKHF_EXTENDED) != 0;

        // ----------------------------------------------------------------
        // 1. Suppress the synthetic LCtrl Windows sends alongside Right Alt (AltGr).
        //    Extended VK_LCONTROL = synthetic from AltGr.
        //    Real Left Ctrl  → VK_LCONTROL, NOT extended.
        //    Real Right Ctrl → VK_RCONTROL (0xA3), never VK_LCONTROL.
        // ----------------------------------------------------------------
        if (ks.vkCode == VK_LCONTROL && isExtended)
            return (IntPtr)1;

        // ----------------------------------------------------------------
        // 2. Track and suppress Right Alt
        // ----------------------------------------------------------------
        if (ks.vkCode == VK_RMENU)
        {
            if (isDown)
                _rightAltDown = true;
            if (isUp)
            {
                _rightAltDown = false;
                _suppressedKeys.Clear();
            }
            return (IntPtr)1; // always suppress Right Alt itself
        }

        // ----------------------------------------------------------------
        // 3. Track Shift physical state (Left Alt is NOT tracked — it works normally)
        // ----------------------------------------------------------------
        if (ks.vkCode == VK_LSHIFT || ks.vkCode == VK_RSHIFT)
        {
            _shiftDown = isDown;
            return CallNextHookEx(_hookHandle, nCode, wParam, lParam);
        }

        // ----------------------------------------------------------------
        // 4. Suppress the key-up of any key whose key-down we suppressed
        // ----------------------------------------------------------------
        if (isUp && _suppressedKeys.Remove(ks.vkCode))
            return (IntPtr)1;

        // ----------------------------------------------------------------
        // 5. Handle Right Alt + letter combinations
        // ----------------------------------------------------------------
        if (_rightAltDown && isDown)
        {
            char? ch = ks.vkCode switch
            {
                VK_S => _shiftDown ? 'Ş' : 'ş',
                VK_O => _shiftDown ? 'Ö' : 'ö',
                VK_G => _shiftDown ? 'Ğ' : 'ğ',
                VK_C => _shiftDown ? 'Ç' : 'ç',
                _    => null
            };

            if (ch.HasValue)
            {
                _suppressedKeys.Add(ks.vkCode);
                SendUnicodeChar(ch.Value);
                return (IntPtr)1; // suppress original keystroke
            }
        }

        return CallNextHookEx(_hookHandle, nCode, wParam, lParam);
    }

    /// <summary>
    /// Injects a Unicode character as key-down + key-up.
    /// All injected events carry InjectedMarker so the hook ignores them.
    /// No need to release Alt here because Right Alt is already suppressed.
    /// </summary>
    private void SendUnicodeChar(char c)
    {
        var inputs = new INPUT[2];

        inputs[0] = MakeUnicodeInput(c, 0);
        inputs[1] = MakeUnicodeInput(c, KEYEVENTF_KEYUP);

        SendInput(2, inputs, Marshal.SizeOf<INPUT>());
    }

    private INPUT MakeUnicodeInput(char c, uint extraFlags) => new()
    {
        type = INPUT_KEYBOARD,
        U = new InputUnion
        {
            ki = new KEYBDINPUT
            {
                wVk         = 0,
                wScan       = c,
                dwFlags     = KEYEVENTF_UNICODE | extraFlags,
                time        = 0,
                dwExtraInfo = InjectedMarker
            }
        }
    };

    public void Dispose() => Stop();

    // -------------------------------------------------------------------------
    // P/Invoke declarations
    // -------------------------------------------------------------------------

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(
        int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [StructLayout(LayoutKind.Sequential)]
    private struct KBDLLHOOKSTRUCT
    {
        public int    vkCode;
        public int    scanCode;
        public int    flags;
        public int    time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint   dwFlags;
        public uint   time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int    dx;
        public int    dy;
        public uint   mouseData;
        public uint   dwFlags;
        public uint   time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HARDWAREINPUT
    {
        public uint   uMsg;
        public ushort wParamL;
        public ushort wParamH;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public MOUSEINPUT    mi;
        [FieldOffset(0)] public KEYBDINPUT    ki;
        [FieldOffset(0)] public HARDWAREINPUT hi;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint       type;
        public InputUnion U;
    }
}
