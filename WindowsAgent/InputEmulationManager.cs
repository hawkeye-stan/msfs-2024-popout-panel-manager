using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Threading;

namespace MSFSPopoutPanelManager.WindowsAgent
{
    public static class InputEmulationManager
    {
        private const ushort VirtualKeyMenu = 0xA4;
        private const ushort VirtualKeyEnter = 0x0D;
        private const ushort VirtualKeyZero = 0x30;
        private const ushort VirtualKeyLeftControl = 0xA2;
        private const ushort VirtualKeyRightControl = 0xA3;
        private const ushort VirtualKeyRightMenu = 0xA5;

        private const int FocusDelayMilliseconds = 200;
        private const int ClickDownDelayMilliseconds = 200;
        private const int ClickReleaseDelayMilliseconds = 200;
        private const int PopOutModifierDelayMilliseconds = 500;
        private const int ModifierReleaseDelayMilliseconds = 100;
        private const int CustomViewKeyDelayMilliseconds = 200;
        private const int MoveWindowDelayMilliseconds = 1000;

        private static readonly IReadOnlyDictionary<string, ushort> NumPadKeys =
            new Dictionary<string, ushort>(StringComparer.OrdinalIgnoreCase)
            {
                ["0"] = 0x60,
                ["1"] = 0x61,
                ["2"] = 0x62,
                ["3"] = 0x63,
                ["4"] = 0x64,
                ["5"] = 0x65,
                ["6"] = 0x66,
                ["7"] = 0x67,
                ["8"] = 0x68,
                ["9"] = 0x69,
                ["DECIMAL"] = 0x6E,
                ["ADD"] = 0x6B,
                ["SUBTRACT"] = 0x6D,
                ["DIVIDE"] = 0x6F,
                ["MULTIPLY"] = 0x6A,
                ["ENTER"] = VirtualKeyEnter,
                ["TAB"] = 0x09
            };

        public static void LeftClick(int x, int y)
        {
            PInvoke.SetCursorPos(x, y);
            Thread.Sleep(FocusDelayMilliseconds + ClickDownDelayMilliseconds / 2);

            SendLeftClick(x, y);
            Thread.Sleep(ClickReleaseDelayMilliseconds);
        }

        public static void PrepareToPopOutPanel(int x, int y, bool isTurboMode)
        {
            PInvoke.SetForegroundWindow(WindowProcessManager.SimulatorProcess.Handle);
            Thread.Sleep(isTurboMode ? 0 : 250);

            MoveAppWindowFromLeftClickPoint(x, y);

            // Click outside the source marker to focus the simulator.
            LeftClick(x, y);

            // Restore the source coordinates after focus changes.
            PInvoke.SetCursorPos(x, y);
            Thread.Sleep(isTurboMode ? 50 : 500);
        }

        public static void PopOutPanel(int x, int y, bool useSecondaryKeys, bool isTurbo)
        {
            if (useSecondaryKeys)
            {
                SendKeyDown(VirtualKeyLeftControl);
                SendKeyDown(VirtualKeyRightControl, KeyboardInputFlags.ExtendedKey);

                Thread.Sleep(isTurbo ? 0 : PopOutModifierDelayMilliseconds);
                SendLeftClick(x, y);

                ReleaseModifiers(
                    (VirtualKeyRightControl, KeyboardInputFlags.ExtendedKey),
                    (VirtualKeyLeftControl, KeyboardInputFlags.KeyDown));
                return;
            }

            SendKeyDown(VirtualKeyRightMenu, KeyboardInputFlags.ExtendedKey);
            Thread.Sleep(isTurbo ? 0 : PopOutModifierDelayMilliseconds);
            SendLeftClick(x, y);
            ReleaseModifiers((VirtualKeyRightMenu, KeyboardInputFlags.ExtendedKey));
        }

        public static void LoadCustomView(string keyBinding)
        {
            Debug.WriteLine("Loading custom view...");

            if (WindowProcessManager.SimulatorProcess == null)
                return;

            var simulatorHandle = WindowProcessManager.SimulatorProcess.Handle;
            PInvoke.SetForegroundWindow(simulatorHandle);
            Thread.Sleep(FocusDelayMilliseconds);

            var customViewKey = (ushort)(Convert.ToInt32(keyBinding) + VirtualKeyZero);
            SendKeyDown(VirtualKeyMenu);
            SendKeyDown(customViewKey);
            Thread.Sleep(CustomViewKeyDelayMilliseconds);
            SendKeyUp(customViewKey);
            SendKeyUp(VirtualKeyMenu);
            Thread.Sleep(CustomViewKeyDelayMilliseconds);
        }

        public static void ToggleFullScreenPanel(IntPtr windowHandle)
        {
            PInvoke.SetForegroundWindow(windowHandle);
            Thread.Sleep(FocusDelayMilliseconds);

            PInvoke.SetFocus(windowHandle);
            Thread.Sleep(300);

            SendKeyDown(VirtualKeyMenu);
            SendKeyDown(VirtualKeyEnter);
            Thread.Sleep(CustomViewKeyDelayMilliseconds);
            SendKeyUp(VirtualKeyEnter);
            SendKeyUp(VirtualKeyMenu);
            Thread.Sleep(CustomViewKeyDelayMilliseconds);
        }

        public static void NumPadClick(string numPadKey)
        {
            var simulatorHandle = WindowProcessManager.SimulatorProcess.Handle;
            PInvoke.SetForegroundWindow(simulatorHandle);
            Thread.Sleep(FocusDelayMilliseconds);

            var key = NumPadKeys.TryGetValue(numPadKey, out var mappedKey)
                ? mappedKey
                : NumPadKeys["DECIMAL"];

            SendKeyDown(key);
            Thread.Sleep(CustomViewKeyDelayMilliseconds);
            SendKeyUp(key);
        }

        private static void SendLeftClick(int x, int y)
        {
            PInvoke.SendMouseInput(MouseInputFlags.LeftDown, x, y);
            Thread.Sleep(ClickDownDelayMilliseconds);
            PInvoke.SendMouseInput(MouseInputFlags.LeftUp, x, y);
        }

        private static void SendKeyDown(ushort virtualKey, KeyboardInputFlags additionalFlags = KeyboardInputFlags.KeyDown)
        {
            PInvoke.SendKeyboardInput(virtualKey, additionalFlags);
        }

        private static void SendKeyUp(ushort virtualKey, KeyboardInputFlags additionalFlags = KeyboardInputFlags.KeyUp)
        {
            PInvoke.SendKeyboardInput(virtualKey, KeyboardInputFlags.KeyUp | additionalFlags);
        }

        private static void ReleaseModifiers(params (ushort VirtualKey, KeyboardInputFlags Flags)[] modifiers)
        {
            foreach (var modifier in modifiers)
                SendKeyUp(modifier.VirtualKey, modifier.Flags);

            Thread.Sleep(ModifierReleaseDelayMilliseconds);

            foreach (var modifier in modifiers)
                SendKeyUp(modifier.VirtualKey, modifier.Flags);
        }

        private static void MoveAppWindowFromLeftClickPoint(int x, int y)
        {
            var applicationHandle = WindowProcessManager.AppProcess.Handle;
            var applicationRectangle = WindowActionManager.GetWindowRectangle(applicationHandle);

            if (!IsPointWithinRectangle(x, y, applicationRectangle))
                return;

            var top = y - applicationRectangle.Height - 50;
            WindowActionManager.MoveWindow(
                applicationHandle,
                applicationRectangle.X,
                top,
                applicationRectangle.Width,
                applicationRectangle.Height);
            Thread.Sleep(MoveWindowDelayMilliseconds);
        }

        private static bool IsPointWithinRectangle(int x, int y, Rectangle rectangle)
        {
            return x >= rectangle.Left
                   && x <= rectangle.Right
                   && y >= rectangle.Top
                   && y <= rectangle.Bottom;
        }
    }
}
