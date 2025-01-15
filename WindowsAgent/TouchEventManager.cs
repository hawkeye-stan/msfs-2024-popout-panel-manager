using MSFSPopoutPanelManager.DomainModel.Profile;
using MSFSPopoutPanelManager.DomainModel.Setting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace MSFSPopoutPanelManager.WindowsAgent
{
    public class TouchEventManager
    {
        private static IntPtr _hHook = IntPtr.Zero;
        private static readonly PInvoke.WindowsHookExProc CallbackDelegate = HookCallBack;
        private static bool _isTouchDownStarted = false;
        private static bool _isTouchDownCompleted = true;
        private static bool _isTouchUpCompleted = true;
        private static bool _isDragged;
        private static int _refocusedTaskIndex;

        private static readonly object Lock = new();

        private const int PANEL_MENUBAR_HEIGHT = 31;
        private const uint TOUCH_FLAG = 0xFF515700;
        private const uint WM_MOUSEMOVE = 0x0200;
        private const uint WM_LBUTTONDOWN = 0x0201;
        private const uint WM_LBUTTONUP = 0x0202;
        private const uint WM_RBUTTONDOWN = 0x0204;
        private const uint WM_RBUTTONUP = 0x0205;
        private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        private const uint MOUSEEVENTF_LEFTUP = 0x0004;

        private const int MouseClickDelay = 15;
        private static Queue<Tuple<int, int>> _queue = new Queue<Tuple<int, int>>();
        private static Tuple<int, int> _coor;

        public static UserProfile ActiveProfile { private get; set; }

        public static ApplicationSetting ApplicationSetting { private get; set; }

        public static void Hook()
        {
            Debug.WriteLine("Executing touch event manager mouse hook...");

            var curProcess = Process.GetCurrentProcess();
            var curModule = curProcess.MainModule;

            if (curModule == null) 
                return;

            var hookWindowPtr = PInvoke.GetModuleHandle(curModule.ModuleName);
            _hHook = PInvoke.SetWindowsHookEx(HookType.WH_MOUSE_LL, CallbackDelegate, hookWindowPtr, 0);
        }

        public static void UnHook()
        {
            if (_hHook != IntPtr.Zero)
            {
                Debug.WriteLine("Executing touch event manager mouse unhook...");

                PInvoke.UnhookWindowsHookEx(_hHook);
                _hHook = IntPtr.Zero;
            }
        }

        public static bool IsHooked => _hHook != IntPtr.Zero;

        private static int HookCallBack(int code, IntPtr wParam, IntPtr lParam)
        {
            if (code != 0)
                return PInvoke.CallNextHookEx(_hHook, code, wParam, lParam);

            var ptrToStructure = Marshal.PtrToStructure(lParam, typeof(MSLLHOOKSTRUCT));

            if (ptrToStructure == null)
                return 0;

            var info = (MSLLHOOKSTRUCT) ptrToStructure;
            var extraInfo = (uint)info.dwExtraInfo;
            var isTouched = (extraInfo & TOUCH_FLAG) == TOUCH_FLAG;

            // Mouse Click
            if (!isTouched)
            {
                switch ((uint)wParam)
                {
                    case WM_LBUTTONDOWN:
                    case WM_LBUTTONUP:
                        if (!_isTouchDownCompleted)
                            return 1;
                        break;
                }

                return PInvoke.CallNextHookEx(_hHook, code, wParam, lParam);
            }

            // If touch point is within pop out panel boundaries and have touch enabled
            var panelConfig = ActiveProfile.PanelConfigs.FirstOrDefault(p => p.TouchEnabled &&
                                                                            ((p.FullScreen && CheckWithinFullScreenCoordinate(p, info)) || CheckWithinWindowCoordinate(p, info)));

            if (panelConfig == null)
                return PInvoke.CallNextHookEx(_hHook, code, wParam, lParam);

            switch ((uint)wParam)
            {
                case WM_RBUTTONDOWN:
                case WM_RBUTTONUP:
                    return 1;

                case WM_LBUTTONDOWN:                                        
                    _refocusedTaskIndex++;
                    if (panelConfig.PanelType == PanelType.RefocusDisplay)
                        return 1;
                
                    Task.Run(() =>
                    {
                        Debug.WriteLine($"DX: {info.pt.X}, DY: {info.pt.Y}");

                        lock(Lock)
                        { 
                            _queue.Enqueue(new Tuple<int, int>(info.pt.X, info.pt.Y));

                            PInvoke.mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, 0); ; // focus window
                            Thread.Sleep(ApplicationSetting.TouchSetting.TouchDownUpDelay + MouseClickDelay);

                            PInvoke.mouse_event(MOUSEEVENTF_LEFTDOWN, info.pt.X, info.pt.Y, 0, 0);
                            Thread.Sleep(ApplicationSetting.TouchSetting.TouchDownUpDelay + MouseClickDelay);

                        }

                        
                    });

                    return 1;
                case WM_LBUTTONUP:
                    if (panelConfig.PanelType == PanelType.RefocusDisplay)
                    {
                        Task.Run(() =>
                        {
                            // Refocus game window
                            if (ApplicationSetting.RefocusSetting.RefocusGameWindow.IsEnabled && panelConfig.AutoGameRefocus)
                            {
                                var currentRefocusIndex = _refocusedTaskIndex;

                                Thread.Sleep(Convert.ToInt32(ApplicationSetting.RefocusSetting.RefocusGameWindow.Delay * 1000));

                                if (currentRefocusIndex == _refocusedTaskIndex)
                                {
                                    WindowActionManager.RefocusMsfsGameWindow();
                                }
                            }
                        });
                    }
                    else
                    {
                        Task.Run(() =>
                        {
                            Thread.Sleep(MouseClickDelay * 2);      // this must match amount of total exec threadsleep time during WM_LBUTTONDOWN 

                            lock (Lock)
                            {
                                _coor = _queue.Dequeue();

                                Debug.WriteLine($"UX: {_coor.Item1}, UY: {_coor.Item2}");

                                PInvoke.mouse_event(MOUSEEVENTF_LEFTUP, _coor.Item1, _coor.Item2, 0, 0);
                                Thread.Sleep(ApplicationSetting.TouchSetting.TouchDownUpDelay + MouseClickDelay);

                                PInvoke.mouse_event(MOUSEEVENTF_LEFTUP, _coor.Item1, _coor.Item2, 0, 0);

                                Debug.WriteLine("-------------------------");
                            }

                          

                            // Refocus game window
                            if (ApplicationSetting.RefocusSetting.RefocusGameWindow.IsEnabled && panelConfig.AutoGameRefocus)
                            {
                                var currentRefocusIndex = _refocusedTaskIndex;

                                Thread.Sleep(Convert.ToInt32(ApplicationSetting.RefocusSetting.RefocusGameWindow.Delay * 1000));

                                if (currentRefocusIndex == _refocusedTaskIndex)
                                {
                                    WindowActionManager.RefocusMsfsGameWindow();
                                }
                            }


                          
                        });
                    }


                    return 1;
                case WM_MOUSEMOVE:
                    //if(_isTouchDownStarted)
                    //{
                    //    _mouseMoveCount++;
                    //}


                    //if (!_isTouchDownCompleted)
                    //    break;
                    
                    //if (_isTouchDownCompleted)
                    //{
                    //    lock (Lock)
                    //    {
                    //        _isDragged = true;
                    //    }
                    //}
                    break;
            }

            return PInvoke.CallNextHookEx(_hHook, code, wParam, lParam);
        }

        private static bool CheckWithinWindowCoordinate(PanelConfig panelConfig, MSLLHOOKSTRUCT coor)
        {
            return coor.pt.X > panelConfig.Left
                   && coor.pt.X < panelConfig.Left + panelConfig.Width
                   && coor.pt.Y > panelConfig.Top + (panelConfig.HideTitlebar ? 5 : PANEL_MENUBAR_HEIGHT)
                   && coor.pt.Y < panelConfig.Top + panelConfig.Height;
        }

        private static bool CheckWithinFullScreenCoordinate(PanelConfig panelConfig, MSLLHOOKSTRUCT coor)
        {
            return coor.pt.X > panelConfig.FullScreenMonitorInfo.X
                   && coor.pt.X < panelConfig.FullScreenMonitorInfo.X + panelConfig.FullScreenMonitorInfo.Width
                   && coor.pt.Y > panelConfig.FullScreenMonitorInfo.Y
                   && coor.pt.Y < panelConfig.FullScreenMonitorInfo.Y + panelConfig.FullScreenMonitorInfo.Height;
        }
    }
}
