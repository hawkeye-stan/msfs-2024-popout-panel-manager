using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using WindowsInput;

namespace MSFSPopoutPanelManager.WindowsAgent
{
    public class InputEmulationManager
    {
        private const uint VK_LMENU = 0xA4;
        private const uint VK_ENT = 0x0D;
        private const uint KEY_0 = 0x30;

        private const uint NUMPAD_0 = 0x60;
        private const uint NUMPAD_1 = 0x61;
        private const uint NUMPAD_2 = 0x62;
        private const uint NUMPAD_3 = 0x63;
        private const uint NUMPAD_4 = 0x64;
        private const uint NUMPAD_5 = 0x65;
        private const uint NUMPAD_6 = 0x66;
        private const uint NUMPAD_7 = 0x67;
        private const uint NUMPAD_8 = 0x68;
        private const uint NUMPAD_9 = 0x69;
        private const uint NUMPAD_DECIMAL = 0x6E;
        private const uint NUMPAD_ADD = 0x6B;
        private const uint NUMPAD_SUBTRACT = 0x6D;
        private const uint NUMPAD_DIVIDE = 0x6F;
        private const uint NUMPAD_MULTIPLY = 0x6A;
        private const uint NUMPAD_ENTER = 0x0D;
        private const uint NUMPAD_TAB = 0x09;

        private static readonly InputSimulator InputSimulator = new ();

        public static void LeftClick(int x, int y)
        {
            PInvoke.SetCursorPos(x, y);
            Thread.Sleep(300);

            PInvoke.SendMouseInput(MouseInputFlags.LeftDown, x, y);
            Thread.Sleep(200);
            PInvoke.SendMouseInput(MouseInputFlags.LeftUp, x, y);
            Thread.Sleep(200);
        }

        public static void PrepareToPopOutPanel(int x, int y, bool isTurboMode)
        {
            PInvoke.SetForegroundWindow(WindowProcessManager.SimulatorProcess.Handle);
            Thread.Sleep(isTurboMode ? 0 : 250);

            MoveAppWindowFromLeftClickPoint(x, y);

            LeftClick(x, y);  // Left click outside the circle area to focus game window

            // Force cursor reset and focus 
            PInvoke.SetCursorPos(x, y);
            Thread.Sleep(isTurboMode ? 50 : 500);
        }

        public static void PopOutPanel(int x, int y, bool useSecondaryKeys, bool isTurbo)
        {
            if (useSecondaryKeys)
            {
                InputSimulator.Keyboard.KeyDown(WindowsInput.Native.VirtualKeyCode.LCONTROL);
                InputSimulator.Keyboard.KeyDown(WindowsInput.Native.VirtualKeyCode.RCONTROL);

                Thread.Sleep(isTurbo ? 0: 500);

                PInvoke.SendMouseInput(MouseInputFlags.LeftDown, x, y);
                Thread.Sleep(200);
                PInvoke.SendMouseInput(MouseInputFlags.LeftUp, x, y);

                InputSimulator.Keyboard.KeyUp(WindowsInput.Native.VirtualKeyCode.RCONTROL);
                InputSimulator.Keyboard.KeyUp(WindowsInput.Native.VirtualKeyCode.LCONTROL);
                Thread.Sleep(100);
                InputSimulator.Keyboard.KeyUp(WindowsInput.Native.VirtualKeyCode.RCONTROL);     // resend to make sure Ctrl key is up
                InputSimulator.Keyboard.KeyUp(WindowsInput.Native.VirtualKeyCode.LCONTROL);
            }
            else
            {
                InputSimulator.Keyboard.KeyDown(WindowsInput.Native.VirtualKeyCode.RMENU);

                Thread.Sleep(isTurbo ? 0 : 500);

                PInvoke.SendMouseInput(MouseInputFlags.LeftDown, x, y);
                Thread.Sleep(200);
                PInvoke.SendMouseInput(MouseInputFlags.LeftUp, x, y);

                InputSimulator.Keyboard.KeyUp(WindowsInput.Native.VirtualKeyCode.RMENU);
                Thread.Sleep(100);
                InputSimulator.Keyboard.KeyUp(WindowsInput.Native.VirtualKeyCode.RMENU);        // resend to make sure Alt key is up
            }
        }

        public static void LoadCustomView(string keyBinding)
        {
            Debug.WriteLine("Loading custom view...");

            if (WindowProcessManager.SimulatorProcess == null)
                return;

            var hWnd = WindowProcessManager.SimulatorProcess.Handle;
            
            PInvoke.SetForegroundWindow(hWnd);
            Thread.Sleep(200);

            var customViewKey = (uint)(Convert.ToInt32(keyBinding) + KEY_0);

            // Then load view using Alt-0
            PInvoke.SendKeyboardInput((ushort)VK_LMENU, KeyboardInputFlags.KeyDown);
            PInvoke.SendKeyboardInput((ushort)customViewKey, KeyboardInputFlags.KeyDown);
            Thread.Sleep(200);
            PInvoke.SendKeyboardInput((ushort)customViewKey, KeyboardInputFlags.KeyUp);
            PInvoke.SendKeyboardInput((ushort)VK_LMENU, KeyboardInputFlags.KeyUp);
            Thread.Sleep(200);
        }

        public static void ToggleFullScreenPanel(IntPtr hWnd)
        {
            PInvoke.SetForegroundWindow(hWnd);
            Thread.Sleep(200);

            PInvoke.SetFocus(hWnd);
            Thread.Sleep(300);

            PInvoke.SendKeyboardInput((ushort)VK_LMENU, KeyboardInputFlags.KeyDown);
            PInvoke.SendKeyboardInput((ushort)VK_ENT, KeyboardInputFlags.KeyDown);
            Thread.Sleep(200);
            PInvoke.SendKeyboardInput((ushort)VK_ENT, KeyboardInputFlags.KeyUp);
            PInvoke.SendKeyboardInput((ushort)VK_LMENU, KeyboardInputFlags.KeyUp);
            Thread.Sleep(200);
        }

        public static void NumPadClick(string numPadKey)
        {
            var hWnd = WindowProcessManager.SimulatorProcess.Handle;
            PInvoke.SetForegroundWindow(hWnd);
            Thread.Sleep(200);

            var key = NUMPAD_DECIMAL;

            switch (numPadKey.ToUpper())
            {
                case "0":
                    key = NUMPAD_0;
                    break;
                case "1":
                    key = NUMPAD_1;
                    break;
                case "2":
                    key = NUMPAD_2;
                    break;
                case "3":
                    key = NUMPAD_3;
                    break;
                case "4":
                    key = NUMPAD_4;
                    break;
                case "5":
                    key = NUMPAD_5;
                    break;
                case "6":
                    key = NUMPAD_6;
                    break;
                case "7":
                    key = NUMPAD_7;
                    break;
                case "8":
                    key = NUMPAD_8;
                    break;
                case "9":
                    key = NUMPAD_9;
                    break;
                case "DECIMAL":
                    key = NUMPAD_DECIMAL;
                    break;
                case "ADD":
                    key = NUMPAD_ADD;
                    break;
                case "SUBTRACT":
                    key = NUMPAD_SUBTRACT;
                    break;
                case "DIVIDE":
                    key = NUMPAD_DIVIDE;
                    break;
                case "MULTIPLY":
                    key = NUMPAD_MULTIPLY;
                    break;
                case "TAB":
                    key = NUMPAD_TAB;
                    break;
                case "ENTER":
                    key = NUMPAD_ENTER;
                    break;
            }

            PInvoke.SendKeyboardInput((ushort)key, KeyboardInputFlags.KeyDown);
            Thread.Sleep(200);
            PInvoke.SendKeyboardInput((ushort)key, KeyboardInputFlags.KeyUp);
        }

        private static void MoveAppWindowFromLeftClickPoint(int x, int y)
        {
            var appHandle = WindowProcessManager.AppProcess.Handle;
            var applicationRectangle = WindowActionManager.GetWindowRectangle(appHandle);

            if (IsPointWithinRectangle(x, y, applicationRectangle))
            {
                var top = y - applicationRectangle.Height - 50;
                WindowActionManager.MoveWindow(appHandle, applicationRectangle.X, top, applicationRectangle.Width, applicationRectangle.Height);
                Thread.Sleep(1000);
            }
        }

        private static bool IsPointWithinRectangle(int x, int y, Rectangle rect)
        {
            var rightEdge = rect.X + rect.Width;
            var bottomEdge = rect.Y + rect.Height;

            return x >= rect.X && x <= rightEdge && y >= rect.Y && y <= bottomEdge;
        }
    }
}