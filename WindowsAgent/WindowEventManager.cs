using MSFSPopoutPanelManager.DomainModel.Profile;
using MSFSPopoutPanelManager.DomainModel.Setting;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace MSFSPopoutPanelManager.WindowsAgent
{
    public class WindowEventManager
    {
        private static PInvoke.WinEventProc _winEvent;      // keep this as static to prevent garbage collect or the app will crash
        private static IntPtr _winEventHook;
        private static int? _prevShowWinCmd;
        
        public static UserProfile ActiveProfile { private get; set; }

        public static ApplicationSetting ApplicationSetting { private get; set; }

        public static void HookWinEvent()
        {
            Debug.WriteLine($"Main window handle ---- {WindowProcessManager.AppProcess.Handle.ToString("X")}");

            if (ActiveProfile?.PanelConfigs == null)
                return;

            UnhookWinEvent();
            Debug.WriteLine("Executing HookWinEvent()...");

            var isRequiredPanelConfiguration = false;
            var isUsedNonTouchPanelRefocusLogic = false;

            if ((!ActiveProfile.IsLocked || (ActiveProfile.IsLocked && ApplicationSetting.PopOutSetting.EnablePanelResetWhenLocked))
                && ActiveProfile.PanelConfigs.Any(p => p.IsPopOutSuccess != null && (bool)p.IsPopOutSuccess))
                isRequiredPanelConfiguration = true;

            if (ActiveProfile.PanelConfigs.Any(p => p.AutoGameRefocus && !p.TouchEnabled && p.IsPopOutSuccess != null && (bool)p.IsPopOutSuccess)
                && !ActiveProfile.PanelConfigs.All(p => p.TouchEnabled)
                && ApplicationSetting.RefocusSetting.RefocusGameWindow.IsEnabled)
                isUsedNonTouchPanelRefocusLogic = true;

            uint winEventMin, winEventMax;

            if (isRequiredPanelConfiguration && isUsedNonTouchPanelRefocusLogic)
            {
                winEventMin = PInvokeConstant.EVENT_SYSTEM_CAPTURESTART;
                winEventMax = PInvokeConstant.EVENT_OBJECT_STATECHANGE;
            }
            else if (isRequiredPanelConfiguration)
            {
                winEventMin = PInvokeConstant.EVENT_SYSTEM_MOVESIZEEND;
                winEventMax = PInvokeConstant.EVENT_OBJECT_STATECHANGE;
            }
            else if (isUsedNonTouchPanelRefocusLogic)
            {
                winEventMin = PInvokeConstant.EVENT_SYSTEM_CAPTURESTART;
                winEventMax = PInvokeConstant.EVENT_SYSTEM_CAPTUREEND;
            }
            else
            {
                return;
            }

            _winEvent = EventCallback;
            _winEventHook = PInvoke.SetWinEventHook(winEventMin, winEventMax, IntPtr.Zero, _winEvent, 0, 0, PInvokeConstant.WINEVENT_OUTOFCONTEXT);

            Debug.WriteLine($"WinEventMin: {winEventMin} WinEventMax: {winEventMax}");
        }

        public static void UnhookWinEvent()
        {
            _prevShowWinCmd = null;

            // Unhook all Win API events
            if (_winEventHook == IntPtr.Zero)
                return;

            Debug.WriteLine("Executing UnhookWinEvent()...");
            PInvoke.UnhookWinEvent(_winEventHook);
        }

        private static void EventCallback(IntPtr hWinEventHook, uint iEvent, IntPtr hwnd, int idObject, int idChild, int dwEventThread, int dwmsEventTime)
        {
            // Do not process POPM app events
            if (WindowProcessManager.AppProcess.Handle == hwnd)
                return;

            // check by priority to speed up comparing of escaping constraints
            if (hwnd == IntPtr.Zero || hWinEventHook != _winEventHook)
                return;
            
            switch (iEvent)
            {
                case PInvokeConstant.EVENT_SYSTEM_CAPTURESTART:     // For game refocus mouse touch down
                    if (idObject != 0)   // check by priority to speed up comparing of escaping constraints
                        return;

                    HandleEventMouseTouchDownCallBack(hwnd);
                    break;
                case PInvokeConstant.EVENT_SYSTEM_CAPTUREEND:       // For game refocus mouse touch up
                    if (idObject != 0)   // check by priority to speed up comparing of escaping constraints
                        return;

                    HandleEventMouseTouchUpCallBack(hwnd);
                    break;
                case PInvokeConstant.EVENT_SYSTEM_MOVESIZEEND:
                    if (idObject != 0)   // check by priority to speed up comparing of escaping constraints
                        return;

                    HandleEventMoveResizeCallback(hwnd);
                    break;
                case PInvokeConstant.EVENT_OBJECT_STATECHANGE:
                    if (idChild != 3 && idChild != 5)   // All pop out and built-in pop out have idChild = 3 (maximize) or idChild = 5 (closed)
                        return;

                    Debug.WriteLine($"hwnd - {hwnd.ToString("X")} / idObject - {idObject} / idChild - {idChild} / dwEventThread - {dwEventThread} / dwmsEventTime - {dwmsEventTime} ");
                    HandleEventStateChangeCallBack(hwnd);
                    break;
            }
        }

        private static void HandleEventMouseTouchDownCallBack(IntPtr hwnd)
        {
            Debug.WriteLine($"{DateTime.Now} - EventCallback_TouchDown/{hwnd.ToString("X")}");
            var panelConfig = ActiveProfile.PanelConfigs.FirstOrDefault(panel => panel.PanelHandle == hwnd);

            if (panelConfig == null)   // Should not apply any other settings if panel is full screen mode
                return;
            
            GameRefocusManager.HandleMouseDownEvent(panelConfig);
        }

        private static void HandleEventMouseTouchUpCallBack(IntPtr hwnd)
        {
            Debug.WriteLine($"{DateTime.Now} - EventCallback_TouchUp/{hwnd.ToString("X")}");
            var panelConfig = ActiveProfile.PanelConfigs.FirstOrDefault(panel => panel.PanelHandle == hwnd);

            if (panelConfig == null)   // Should not apply any other settings if panel is full screen mode
                return;
            
            GameRefocusManager.HandleMouseUpEvent(panelConfig);
        }

        private static void HandleEventMoveResizeCallback(IntPtr hwnd)
        {
            Debug.WriteLine($"{DateTime.Now} - EventCallback_MoveResize/{hwnd.ToString("X")}");
            var panelConfig = ActiveProfile.PanelConfigs.FirstOrDefault(panel => panel.PanelHandle == hwnd);

            if (panelConfig == null)   // Should not apply any other settings if panel is full screen mode
                return;

            if (ActiveProfile.IsLocked)
                WindowActionManager.MoveWindow(panelConfig.PanelHandle, panelConfig.Left, panelConfig.Top, panelConfig.Width, panelConfig.Height);          // Move window back to original location
            else
                UpdatePanelCoordinates(panelConfig);
        }

        private static void HandleEventStateChangeCallBack(IntPtr hwnd)
        {
            Debug.WriteLine($"{DateTime.Now} - EventCallback_StateChange/{hwnd.ToString("X")}");
            var panelConfig = ActiveProfile.PanelConfigs.FirstOrDefault(panel => panel.PanelHandle == hwnd && panel.PanelType is PanelType.CustomPopout or PanelType.BuiltInPopout);

            if (panelConfig == null)   // Should not apply any other settings if panel is full screen mode
                return;

            if (!ActiveProfile.IsLocked)
            {
                Thread.Sleep(300);
                UpdatePanelCoordinates(panelConfig);
                _prevShowWinCmd = null;
            }
            else
            {
                // Pop out is closed
                var rect = WindowActionManager.GetWindowRectangle(panelConfig.PanelHandle);
                if (rect is { Width: 0, Height: 0 })
                {
                    panelConfig.PanelHandle = IntPtr.MaxValue;
                    _prevShowWinCmd = null;
                    return;
                }

                var wp = new WINDOWPLACEMENT();
                wp.length = System.Runtime.InteropServices.Marshal.SizeOf(wp);
                PInvoke.GetWindowPlacement(hwnd, ref wp);

                if (_prevShowWinCmd == null)
                {
                    _prevShowWinCmd = wp.showCmd;
                    Thread.Sleep(250);
                    return;
                }

                if (panelConfig.FloatingPanel.IsEnabled && !panelConfig.IsFloating)
                    return;

                switch (wp.showCmd)
                {
                    case PInvokeConstant.SW_SHOWMAXIMIZED when _prevShowWinCmd == PInvokeConstant.SW_SHOWNORMAL:
                        PInvoke.ShowWindow(hwnd, PInvokeConstant.SW_RESTORE);
                        break;
                    case PInvokeConstant.SW_SHOWMAXIMIZED when _prevShowWinCmd == PInvokeConstant.SW_SHOWMINIMIZED:
                        PInvoke.ShowWindow(hwnd, PInvokeConstant.SW_SHOWMINIMIZED);
                        break;
                    case PInvokeConstant.SW_SHOWMINIMIZED when _prevShowWinCmd == PInvokeConstant.SW_SHOWNORMAL:
                        PInvoke.ShowWindow(hwnd, PInvokeConstant.SW_RESTORE);
                        break;
                    case PInvokeConstant.SW_SHOWMINIMIZED when _prevShowWinCmd == PInvokeConstant.SW_SHOWMAXIMIZED:
                    case PInvokeConstant.SW_SHOWNORMAL when _prevShowWinCmd == PInvokeConstant.SW_SHOWMAXIMIZED:
                        PInvoke.ShowWindow(hwnd, PInvokeConstant.SW_SHOWMAXIMIZED);
                        break;
                    case PInvokeConstant.SW_SHOWNORMAL when _prevShowWinCmd == PInvokeConstant.SW_SHOWMINIMIZED:
                        PInvoke.ShowWindow(hwnd, PInvokeConstant.SW_SHOWMINIMIZED);
                        break;
                    case PInvokeConstant.SW_SHOWNORMAL when _prevShowWinCmd == PInvokeConstant.SW_SHOWNORMAL:
                        WindowActionManager.MoveWindow(panelConfig.PanelHandle, panelConfig.Left, panelConfig.Top, panelConfig.Width, panelConfig.Height);
                        break;
                }

                _prevShowWinCmd = null;
            }
        }
        
        private static void UpdatePanelCoordinates(PanelConfig panelConfig)
        {
            var rect = WindowActionManager.GetWindowRectangle(panelConfig.PanelHandle);

            if (rect is { Width: 0, Height: 0 })        // don't set if width and height = 0
            {
                panelConfig.PanelHandle = IntPtr.MaxValue;
                return;
            }

            if (panelConfig.FloatingPanel.IsEnabled && !panelConfig.IsFloating)       // do not update coordinate if floating panel
                return;

            panelConfig.Left = rect.Left;
            panelConfig.Top = rect.Top;

            panelConfig.Width = rect.Width;
            panelConfig.Height = rect.Height;

        }
    }
}
