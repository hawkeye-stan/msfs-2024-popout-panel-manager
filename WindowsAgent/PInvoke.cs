using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;

namespace MSFSPopoutPanelManager.WindowsAgent
{
    internal static class PInvokeConstant
    {
        public const int SW_SHOWNORMAL = 1;
        public const int SW_SHOWMINIMIZED = 2;
        public const int SW_SHOWMAXIMIZED = 3;
        public const int SW_SHOW = 5;
        public const int SW_MINIMIZE = 6;
        public const int SW_RESTORE = 9;

        public const uint EVENT_SYSTEM_CAPTURESTART = 0x0008;
        public const uint EVENT_SYSTEM_CAPTUREEND = 0x0009;
        public const uint EVENT_OBJECT_STATECHANGE = 0x800A;
        public const uint EVENT_SYSTEM_MOVESIZEEND = 0x000B;

        public const int SWP_NOMOVE = 0x0002;
        public const int SWP_NOSIZE = 0x0001;
        public const int SWP_ALWAYS_ON_TOP = SWP_NOMOVE | SWP_NOSIZE;

        public const int GWL_STYLE = -16;
        public const uint WS_SIZEBOX = 0x00040000;
        public const uint WS_BORDER = 0x00800000;
        public const uint WS_DLGFRAME = 0x00400000;
        public const uint WS_CAPTION = WS_BORDER | WS_DLGFRAME;

        public const int HWND_TOPMOST = -1;
        public const int HWND_NOTOPMOST = -2;

        public const uint WM_CLOSE = 0x0010;
        public const int WINEVENT_OUTOFCONTEXT = 0;
    }

    public static class PInvoke
    {
        private const int NativeTextBufferLength = 255;

        #region Window enumeration and text

        public static bool EnumWindows(CallBack callback, int lParam)
        {
            return EnumWindowsNative(callback, lParam);
        }

        public static string GetClassName(IntPtr windowHandle)
        {
            var buffer = new StringBuilder(NativeTextBufferLength);
            GetClassNameNative(windowHandle, buffer, buffer.Capacity);
            return buffer.ToString();
        }

        public static string GetWindowText(IntPtr windowHandle)
        {
            try
            {
                var buffer = new StringBuilder(NativeTextBufferLength);
                GetWindowTextNative(windowHandle, buffer, buffer.Capacity);
                return buffer.ToString();
            }
            catch
            {
                return string.Empty;
            }
        }

        public static IntPtr GetWindowHandle(string windowCaption)
        {
            var windowHandle = IntPtr.Zero;

            EnumWindows((handle, _) =>
            {
                if (GetWindowText(handle) == windowCaption)
                    windowHandle = handle;

                return true;
            }, 0);

            return windowHandle;
        }

        #endregion

        #region Window state and input

        public static bool GetCursorPos(out Point point)
        {
            return GetCursorPosNative(out point);
        }

        public static IntPtr GetWindowLong(IntPtr windowHandle, int index)
        {
            return GetWindowLongPtrNative(windowHandle, index);
        }

        public static uint GetWindowThreadProcessId(IntPtr windowHandle, IntPtr processId)
        {
            return GetWindowThreadProcessIdNative(windowHandle, processId);
        }

        public static uint GetCurrentThreadId()
        {
            return GetCurrentThreadIdNative();
        }

        public static IntPtr GetForegroundWindow()
        {
            return GetForegroundWindowNative();
        }

        public static bool AttachThreadInput(uint sourceThreadId, uint targetThreadId, bool attach)
        {
            return AttachThreadInputNative(sourceThreadId, targetThreadId, attach);
        }

        public static bool BringWindowToTop(IntPtr windowHandle)
        {
            return BringWindowToTopNative(windowHandle);
        }

        public static bool GetWindowPlacement(IntPtr windowHandle, ref WINDOWPLACEMENT placement)
        {
            return GetWindowPlacementNative(windowHandle, ref placement);
        }

        public static bool MoveWindow(IntPtr windowHandle, int x, int y, int width, int height, bool repaint)
        {
            return MoveWindowNative(windowHandle, x, y, width, height, repaint);
        }

        public static bool SendKeyboardInput(ushort virtualKey, KeyboardInputFlags flags, ushort scanCode = 0, UIntPtr extraInfo = default)
        {
            var input = new INPUT
            {
                Type = InputType.Keyboard,
                Data = new InputUnion
                {
                    Keyboard = new KEYBDINPUT
                    {
                        VirtualKey = virtualKey,
                        ScanCode = scanCode,
                        Flags = flags,
                        ExtraInfo = extraInfo
                    }
                }
            };

            return SendInputNative(1, ref input, Marshal.SizeOf<INPUT>()) == 1;
        }

        public static bool SendMouseInput(MouseInputFlags flags, int x = 0, int y = 0, uint mouseData = 0, UIntPtr extraInfo = default)
        {
            var input = new INPUT
            {
                Type = InputType.Mouse,
                Data = new InputUnion
                {
                    Mouse = new MOUSEINPUT
                    {
                        X = x,
                        Y = y,
                        MouseData = mouseData,
                        Flags = flags,
                        ExtraInfo = extraInfo
                    }
                }
            };

            return SendInputNative(1, ref input, Marshal.SizeOf<INPUT>()) == 1;
        }

        public static bool SetCursorPos(int x, int y)
        {
            return SetCursorPosNative(x, y);
        }

        public static bool SetForegroundWindow(IntPtr windowHandle)
        {
            return SetForegroundWindowNative(windowHandle);
        }

        public static IntPtr SetFocus(IntPtr windowHandle)
        {
            return SetFocusNative(windowHandle);
        }

        public static IntPtr SendMessage(IntPtr windowHandle, uint message, IntPtr wParam, IntPtr lParam)
        {
            return SendMessageNative(windowHandle, message, wParam, lParam);
        }

        public static bool ShowWindowAsync(HandleRef windowHandle, int command)
        {
            return ShowWindowAsyncNative(windowHandle, command);
        }

        public static int SetWindowLong(IntPtr windowHandle, int index, uint value)
        {
            return SetWindowLongPtrNative(windowHandle, index, new IntPtr(unchecked((int)value))).ToInt32();
        }

        public static IntPtr SetWindowPos(IntPtr windowHandle, IntPtr insertAfter, int x, int y, int width, int height, uint flags)
        {
            return SetWindowPosNative(windowHandle, insertAfter, x, y, width, height, flags);
        }

        public static bool SetWindowText(IntPtr windowHandle, string text)
        {
            return SetWindowTextNative(windowHandle, text);
        }

        public static bool ShowWindow(IntPtr windowHandle, int command)
        {
            return ShowWindowNative(windowHandle, command);
        }

        public static void SwitchToThisWindow(IntPtr windowHandle, bool turnOn)
        {
            SwitchToThisWindowNative(windowHandle, turnOn);
        }

        #endregion

        #region Hooks and modules

        public static IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr moduleHandle, WinEventProc callback, int processId, int threadId, uint flags)
        {
            return SetWinEventHookNative(eventMin, eventMax, moduleHandle, callback, processId, threadId, flags);
        }

        public static int UnhookWinEvent(IntPtr hookHandle)
        {
            return UnhookWinEventNative(hookHandle);
        }

        public static IntPtr GetModuleHandle(string moduleName)
        {
            return GetModuleHandleNative(moduleName);
        }

        public static IntPtr SetWindowsHookEx(HookType hookType, WindowsHookExProc callback, IntPtr moduleHandle, uint threadId)
        {
            return SetWindowsHookExNative(hookType, callback, moduleHandle, threadId);
        }

        public static bool UnhookWindowsHookEx(IntPtr hookHandle)
        {
            return UnhookWindowsHookExNative(hookHandle);
        }

        public static int CallNextHookEx(IntPtr hookHandle, int code, IntPtr wParam, IntPtr lParam)
        {
            return CallNextHookExNative(hookHandle, code, wParam, lParam);
        }

        #endregion

        #region DWM and geometry helpers

        public static int DwmSetWindowAttribute(IntPtr windowHandle, DwmWindowAttribute attribute, ref int value, int valueSize)
        {
            return DwmSetWindowAttributeNative(windowHandle, attribute, ref value, valueSize);
        }

        public static Rectangle GetWindowRectShadow(IntPtr windowHandle)
        {
            var shadowBounds = GetWindowRectangleDwm(windowHandle);
            GetWindowRectNative(windowHandle, out var windowBounds);

            var left = windowBounds.Left - shadowBounds.Left;
            var right = windowBounds.Right - shadowBounds.Right;
            var top = windowBounds.Top - shadowBounds.Top;
            var bottom = windowBounds.Bottom - shadowBounds.Bottom;

            return new Rectangle(left, top, right - left, bottom - top);
        }

        public static Rectangle GetClientRectangle(IntPtr windowHandle)
        {
            GetClientRectNative(windowHandle, out var rect);
            return ToRectangle(rect);
        }

        internal static Rectangle GetWindowRectangleDwm(IntPtr windowHandle)
        {
            var size = Marshal.SizeOf<RECT>();
            DwmGetWindowAttributeNative(windowHandle, (int)DwmWindowAttribute.DWMWA_EXTENDED_FRAME_BOUNDS, out var rect, size);
            return ToRectangle(rect);
        }

        private static Rectangle ToRectangle(RECT rect)
        {
            return new Rectangle(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);
        }

        #endregion

        #region Native declarations

        [DllImport("user32.dll", EntryPoint = "EnumWindows", SetLastError = true)]
        private static extern bool EnumWindowsNative(CallBack callback, int lParam);

        [DllImport("user32.dll", EntryPoint = "GetClassName", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern int GetClassNameNative(IntPtr windowHandle, StringBuilder className, int maxCount);

        [DllImport("user32.dll", EntryPoint = "GetCursorPos", SetLastError = true)]
        private static extern bool GetCursorPosNative(out Point point);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr GetWindowLongPtrNative(IntPtr windowHandle, int index);

        [DllImport("user32.dll", EntryPoint = "GetWindowThreadProcessId", SetLastError = true)]
        private static extern uint GetWindowThreadProcessIdNative(IntPtr windowHandle, IntPtr processId);

        [DllImport("kernel32.dll", EntryPoint = "GetCurrentThreadId", SetLastError = true)]
        private static extern uint GetCurrentThreadIdNative();

        [DllImport("user32.dll", EntryPoint = "GetForegroundWindow", SetLastError = true)]
        private static extern IntPtr GetForegroundWindowNative();

        [DllImport("user32.dll", EntryPoint = "AttachThreadInput", SetLastError = true)]
        private static extern bool AttachThreadInputNative(uint sourceThreadId, uint targetThreadId, bool attach);

        [DllImport("user32.dll", EntryPoint = "BringWindowToTop", SetLastError = true)]
        private static extern bool BringWindowToTopNative(IntPtr windowHandle);

        [DllImport("user32.dll", EntryPoint = "GetWindowPlacement", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetWindowPlacementNative(IntPtr windowHandle, ref WINDOWPLACEMENT placement);

        [DllImport("user32.dll", EntryPoint = "GetClientRect")]
        private static extern bool GetClientRectNative(IntPtr windowHandle, out RECT rect);

        [DllImport("user32.dll", EntryPoint = "GetWindowRect", SetLastError = true)]
        private static extern bool GetWindowRectNative(IntPtr windowHandle, out RECT rect);

        [DllImport("user32.dll", EntryPoint = "GetWindowText", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern int GetWindowTextNative(IntPtr windowHandle, StringBuilder windowText, int maxCount);

        [DllImport("user32.dll", EntryPoint = "MoveWindow", SetLastError = true)]
        private static extern bool MoveWindowNative(IntPtr windowHandle, int x, int y, int width, int height, bool repaint);

        [DllImport("user32.dll", EntryPoint = "SendInput", SetLastError = true)]
        private static extern uint SendInputNative(uint inputCount, ref INPUT inputs, int inputSize);

        [DllImport("user32.dll", EntryPoint = "SetCursorPos", SetLastError = true)]
        private static extern bool SetCursorPosNative(int x, int y);

        [DllImport("user32.dll", EntryPoint = "SetForegroundWindow", SetLastError = true)]
        private static extern bool SetForegroundWindowNative(IntPtr windowHandle);

        [DllImport("user32.dll", EntryPoint = "SetFocus", SetLastError = true)]
        private static extern IntPtr SetFocusNative(IntPtr windowHandle);

        [DllImport("user32.dll", EntryPoint = "SendMessage", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessageNative(IntPtr windowHandle, uint message, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", EntryPoint = "ShowWindowAsync", SetLastError = true)]
        private static extern bool ShowWindowAsyncNative(HandleRef windowHandle, int command);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr SetWindowLongPtrNative(IntPtr windowHandle, int index, IntPtr value);

        [DllImport("user32.dll", EntryPoint = "SetWindowPos", SetLastError = true)]
        private static extern IntPtr SetWindowPosNative(IntPtr windowHandle, IntPtr insertAfter, int x, int y, int width, int height, uint flags);

        [DllImport("user32.dll", EntryPoint = "SetWindowText", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool SetWindowTextNative(IntPtr windowHandle, string text);

        [DllImport("user32.dll", EntryPoint = "SetWinEventHook", SetLastError = true)]
        private static extern IntPtr SetWinEventHookNative(uint eventMin, uint eventMax, IntPtr moduleHandle, WinEventProc callback, int processId, int threadId, uint flags);

        [DllImport("user32.dll", EntryPoint = "ShowWindow", SetLastError = true)]
        private static extern bool ShowWindowNative(IntPtr windowHandle, int command);

        [DllImport("user32.dll", EntryPoint = "SwitchToThisWindow", SetLastError = true)]
        private static extern void SwitchToThisWindowNative(IntPtr windowHandle, bool turnOn);

        [DllImport("user32.dll", EntryPoint = "UnhookWinEvent", SetLastError = true)]
        private static extern int UnhookWinEventNative(IntPtr hookHandle);

        [DllImport("kernel32.dll", EntryPoint = "GetModuleHandleW", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr GetModuleHandleNative(string moduleName);

        [DllImport("user32.dll", EntryPoint = "SetWindowsHookExW", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr SetWindowsHookExNative(HookType hookType, WindowsHookExProc callback, IntPtr moduleHandle, uint threadId);

        [DllImport("user32.dll", EntryPoint = "UnhookWindowsHookEx", SetLastError = true)]
        private static extern bool UnhookWindowsHookExNative(IntPtr hookHandle);

        [DllImport("user32.dll", EntryPoint = "CallNextHookEx", SetLastError = true)]
        private static extern int CallNextHookExNative(IntPtr hookHandle, int code, IntPtr wParam, IntPtr lParam);

        [DllImport("dwmapi.dll", EntryPoint = "DwmGetWindowAttribute", SetLastError = true)]
        private static extern int DwmGetWindowAttributeNative(IntPtr windowHandle, int attribute, out RECT rect, int valueSize);

        [DllImport("dwmapi.dll", EntryPoint = "DwmSetWindowAttribute", PreserveSig = true)]
        private static extern int DwmSetWindowAttributeNative(IntPtr windowHandle, DwmWindowAttribute attribute, ref int value, int valueSize);

        #endregion

        public delegate int WindowsHookExProc(int code, IntPtr wParam, IntPtr lParam);

        public delegate bool CallBack(IntPtr windowHandle, int lParam);

        public delegate void WinEventProc(IntPtr hookHandle, uint eventType, IntPtr windowHandle, int objectId, int childId, int eventThreadId, int eventTime);
    }

    internal enum InputType : uint
    {
        Mouse = 0,
        Keyboard = 1
    }

    [Flags]
    public enum MouseInputFlags : uint
    {
        Move = 0x0001,
        LeftDown = 0x0002,
        LeftUp = 0x0004,
        RightDown = 0x0008,
        RightUp = 0x0010,
        Absolute = 0x8000
    }

    [Flags]
    public enum KeyboardInputFlags : uint
    {
        KeyDown = 0x0000,
        KeyUp = 0x0002,
        ExtendedKey = 0x0001,
        Unicode = 0x0004,
        ScanCode = 0x0008
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct INPUT
    {
        public InputType Type;
        public InputUnion Data;
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct InputUnion
    {
        [FieldOffset(0)]
        public MOUSEINPUT Mouse;

        [FieldOffset(0)]
        public KEYBDINPUT Keyboard;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MOUSEINPUT
    {
        public int X;
        public int Y;
        public uint MouseData;
        public MouseInputFlags Flags;
        public uint Time;
        public UIntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct KEYBDINPUT
    {
        public ushort VirtualKey;
        public ushort ScanCode;
        public KeyboardInputFlags Flags;
        public uint Time;
        public UIntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct WINDOWPLACEMENT
    {
        public int length;
        public int flags;
        public int showCmd;
        public Point ptMinPosition;
        public Point ptMaxPosition;
        public Rectangle rcNormalPosition;
        public Rectangle rcDevice;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MSLLHOOKSTRUCT
    {
        public Point pt;
        public int mouseData;
        public int flags;
        public int time;
        public UIntPtr dwExtraInfo;
    }

    public enum HookType : int
    {
        WH_GETMESSAGE = 3,
        WH_MOUSE = 7,
        WH_MOUSE_LL = 14
    }

    [Flags]
    public enum DwmWindowAttribute : uint
    {
        DWMWA_NCRENDERING_ENABLED = 1,
        DWMWA_NCRENDERING_POLICY,
        DWMWA_TRANSITIONS_FORCEDISABLED,
        DWMWA_ALLOW_NCPAINT,
        DWMWA_CAPTION_BUTTON_BOUNDS,
        DWMWA_NONCLIENT_RTL_LAYOUT,
        DWMWA_FORCE_ICONIC_REPRESENTATION,
        DWMWA_FLIP3D_POLICY,
        DWMWA_EXTENDED_FRAME_BOUNDS,
        DWMWA_HAS_ICONIC_BITMAP,
        DWMWA_DISALLOW_PEEK,
        DWMWA_EXCLUDED_FROM_PEEK,
        DWMWA_CLOAK,
        DWMWA_CLOAKED,
        DWMWA_FREEZE_REPRESENTATION,
        DWMWA_PASSIVE_UPDATE_MODE,
        DWMWA_USE_HOSTBACKDROPBRUSH,
        DWMWA_USE_IMMERSIVE_DARK_MODE = 20,
        DWMWA_WINDOW_CORNER_PREFERENCE = 33,
        DWMWA_BORDER_COLOR,
        DWMWA_CAPTION_COLOR,
        DWMWA_TEXT_COLOR,
        DWMWA_VISIBLE_FRAME_BORDER_THICKNESS,
        DWMWA_SYSTEMBACKDROP_TYPE,
        DWMWA_LAST
    }
}
