using MSFSPopoutPanelManager.DomainModel.Profile;
using MSFSPopoutPanelManager.DomainModel.Setting;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace MSFSPopoutPanelManager.WindowsAgent
{
    public static class GameRefocusManager
    {
        private const int PanelBorderMargin = 15;
        private const int HiddenTitleBarMargin = 5;
        private const int VisibleTitleBarMargin = 50;
        private const int VisibleTitleBarRefocusMargin = 31;

        private static readonly object SyncLock = new();
        private static int _refocusRequestVersion;
        private static bool _isMouseDown;

        public static ApplicationSetting ApplicationSetting { get; set; }

        public static void HandleMouseDownEvent(PanelConfig panelConfig)
        {
            if (panelConfig == null)
                return;

            Debug.WriteLine("Handling touch down event...");

            lock (SyncLock)
            {
                if (_isMouseDown)
                    return;

                if (!IsValidMouseDownTarget(panelConfig))
                    return;

                _isMouseDown = true;
                InvalidatePendingRefocus();
            }

            Debug.WriteLine("Executing touch down event...");
        }

        public static void HandleMouseUpEvent(PanelConfig panelConfig)
        {
            if (panelConfig == null)
                return;

            Debug.WriteLine("Handling touch up event...");

            lock (SyncLock)
            {
                if (!_isMouseDown)
                    return;

                _isMouseDown = false;
            }

            Debug.WriteLine("Executing touch up event...");
            Thread.Sleep(GetTouchDownUpDelay());

            if (!ShouldRefocus(panelConfig))
                return;

            ScheduleRefocus(panelConfig);
        }

        private static bool IsValidMouseDownTarget(PanelConfig panelConfig)
        {
            if (panelConfig.PanelType == PanelType.RefocusDisplay)
                return true;

            if (!PInvoke.GetCursorPos(out var point))
                return false;

            var titleBarMargin = panelConfig.HideTitlebar
                ? HiddenTitleBarMargin
                : VisibleTitleBarMargin;

            return point.Y - panelConfig.Top >= titleBarMargin
                   && panelConfig.Top + panelConfig.Height - point.Y >= PanelBorderMargin
                   && point.X - panelConfig.Left >= PanelBorderMargin
                   && panelConfig.Left + panelConfig.Width - point.X >= PanelBorderMargin;
        }

        private static bool ShouldRefocus(PanelConfig panelConfig)
        {
            if (panelConfig.PanelType == PanelType.RefocusDisplay)
                return true;

            if (!PInvoke.GetCursorPos(out var point))
                return false;

            var titleBarMargin = panelConfig.HideTitlebar
                ? HiddenTitleBarMargin
                : VisibleTitleBarRefocusMargin;

            return point.Y - panelConfig.Top > titleBarMargin
                   && IsRefocusEnabled(panelConfig);
        }

        private static bool IsRefocusEnabled(PanelConfig panelConfig)
        {
            return ApplicationSetting?.RefocusSetting?.RefocusGameWindow?.IsEnabled == true
                   && panelConfig.AutoGameRefocus
                   && !panelConfig.TouchEnabled;
        }

        private static int GetTouchDownUpDelay()
        {
            return ApplicationSetting?.TouchSetting?.TouchDownUpDelay ?? 0;
        }

        private static void ScheduleRefocus(PanelConfig panelConfig)
        {
            int requestVersion;
            lock (SyncLock)
            {
                requestVersion = ++_refocusRequestVersion;
            }

            Task.Run(() => RefocusAfterDelay(requestVersion, panelConfig));
        }

        private static void RefocusAfterDelay(int requestVersion, PanelConfig panelConfig)
        {
            var delay = ApplicationSetting?.RefocusSetting?.RefocusGameWindow?.Delay ?? 0;
            Thread.Sleep(Convert.ToInt32(delay * 1000));

            lock (SyncLock)
            {
                if (requestVersion != _refocusRequestVersion || _isMouseDown)
                    return;
            }

            var simulatorProcess = WindowProcessManager.SimulatorProcess;
            if (simulatorProcess == null || simulatorProcess.Handle == IntPtr.Zero)
                return;

            var simulatorHandle = simulatorProcess.Handle;
            var rectangle = WindowActionManager.GetWindowRectangle(simulatorHandle);
            var centerX = rectangle.X + rectangle.Width / 2;
            var centerY = rectangle.Y + rectangle.Height / 2;

            PInvoke.SetCursorPos(centerX, centerY);
            PInvoke.SetForegroundWindow(simulatorHandle);

            //if (panelConfig.PanelType == PanelType.RefocusDisplay)
            //    InputEmulationManager.LeftClick(centerX, centerY);
        }

        private static void InvalidatePendingRefocus()
        {
            _refocusRequestVersion++;
        }
    }
}
