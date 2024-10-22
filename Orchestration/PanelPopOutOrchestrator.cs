using MSFSPopoutPanelManager.DomainModel.Profile;
using MSFSPopoutPanelManager.DomainModel.Setting;
using MSFSPopoutPanelManager.Shared;
using MSFSPopoutPanelManager.WindowsAgent;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace MSFSPopoutPanelManager.Orchestration
{
    public class PanelPopOutOrchestrator : BaseOrchestrator
    {
        private const int READY_TO_FLY_BUTTON_APPEARANCE_DELAY = 2000;
        private const int CAMERA_VIEW_HOME_COCKPIT_MODE = 8;
        private const int CAMERA_VIEW_CUSTOM_CAMERA = 7;

        private readonly FlightSimOrchestrator _flightSimOrchestrator;
        private readonly PanelSourceOrchestrator _panelSourceOrchestrator;
        private readonly PanelConfigurationOrchestrator _panelConfigurationOrchestrator;
        private readonly KeyboardOrchestrator _keyboardOrchestrator;

        public PanelPopOutOrchestrator(SharedStorage sharedStorage, FlightSimOrchestrator flightSimOrchestrator, PanelSourceOrchestrator panelSourceOrchestrator, PanelConfigurationOrchestrator panelConfigurationOrchestrator, KeyboardOrchestrator keyboardOrchestrator) : base(sharedStorage)
        {
            _flightSimOrchestrator = flightSimOrchestrator;
            _panelSourceOrchestrator = panelSourceOrchestrator;
            _panelConfigurationOrchestrator = panelConfigurationOrchestrator;
            _keyboardOrchestrator = keyboardOrchestrator;

            flightSimOrchestrator.OnFlightStarted += async (_, _) =>
            {
                if (AppSettingData.ApplicationSetting.AutoPopOutSetting.IsEnabled)
                    await AutoPopOut();
            };

            _keyboardOrchestrator.OnKeystrokeDetected += async (_, e) =>
            {
                if (e.KeyBinding == AppSetting.KeyboardShortcutSetting.PopOutKeyboardBinding && !ActiveProfile.IsDisabledStartPopOut)
                    await ManualPopOut();
            };
        }

        private UserProfile ActiveProfile => ProfileData?.ActiveProfile;

        private ApplicationSetting AppSetting => AppSettingData?.ApplicationSetting;

        public event EventHandler OnPopOutStarted;
        public event EventHandler OnPopOutCompleted;
        public event EventHandler<PanelConfig> OnNumPadOpened;
        public event EventHandler<PanelConfig> OnSwitchWindowOpened;

        public async Task ManualPopOut()
        {
            if (ProfileData.ActiveProfile.IsDisabledStartPopOut || !FlightSimData.IsInCockpit)
                return;

            ActiveProfile.IsDisabledStartPopOut = true;
            OnPopOutStarted?.Invoke(this, EventArgs.Empty);

            await CoreSteps();
        }

        public async Task AutoPopOut()
        {
            await Application.Current.Dispatcher.Invoke(async () =>
            {
                ProfileData.AutoSwitchProfile();

                if (ActiveProfile == null)
                    return;

                // Do not do auto pop out if no profile matches the current aircraft
                if (ActiveProfile.AircraftBindings.All(p => p != FlightSimData.AircraftName))
                    return;

                // Do not do auto pop out if no panel configs defined
                if (ActiveProfile.PanelConfigs.Count == 0)
                    return;

                // Do not do auto pop out if not all custom panels have panel source defined
                if (ActiveProfile.PanelConfigs.Count(p => p.PanelType == PanelType.CustomPopout) > 0 &&
                    ActiveProfile.PanelConfigs.Count(p => p.PanelType == PanelType.CustomPopout && p.HasPanelSource) != ActiveProfile.PanelConfigs.Count(p => p.PanelType == PanelType.CustomPopout))
                    return;

                StepReadyToFlyDelay(true);

                ActiveProfile.IsDisabledStartPopOut = true;
                OnPopOutStarted?.Invoke(this, EventArgs.Empty);

                await CoreSteps();
            });
        }

        public void ClosePopOut()
        {
            CloseAllPopOuts();

            if (ActiveProfile != null)
                ActiveProfile.IsPoppedOut = false;
        }

        private async Task CoreSteps()
        {
            if (ActiveProfile == null || ActiveProfile.IsEditingPanelSource || ActiveProfile.HasUnidentifiedPanelSource)
                return;

            StatusMessageWriter.IsEnabled = true;
            StatusMessageWriter.ClearMessage();
            StatusMessageWriter.WriteMessageWithNewLine("Pop out in progress. Please wait and do not move your mouse.", StatusMessageType.Info);

            StepPopoutPrep();

            // *** THIS MUST BE DONE FIRST. Get the built-in panel list to be configured later
            List<IntPtr> builtInPanelHandles = WindowActionManager.GetWindowsByPanelType(new List<PanelType>() { PanelType.BuiltInPopout });
            
            await StepBeforePanelPopout();

            await StepPopOutPanels(builtInPanelHandles);

            ConfigureBuiltInPanel(builtInPanelHandles);

            await StepAfterPanelPopout();

            SetupRefocusDisplay();

            StepApplyPanelConfig();

            await StepPostPopout();

            ActiveProfile.IsDisabledStartPopOut = false;
            OnPopOutCompleted?.Invoke(this, EventArgs.Empty);

            StatusMessageWriter.IsEnabled = false;
        }

        private void StepPopoutPrep()
        {
            _panelConfigurationOrchestrator.EndConfiguration();

            // Set profile pop out status
            ProfileData.ResetActiveProfile();

            // Close all existing custom pop out panels
            CloseAllPopOuts();

            // Close all panel source overlays
            _panelSourceOrchestrator.CloseAllPanelSource();
        }

        private void StepReadyToFlyDelay(bool isAutoPopOut)
        {
            if (!isAutoPopOut)
                return;

            // Ready to fly button plugin default delay
            Thread.Sleep(READY_TO_FLY_BUTTON_APPEARANCE_DELAY);

            if (AppSetting.AutoPopOutSetting.ReadyToFlyDelay == 0)
                return;

            // Extra wait for cockpit view to appear and align
            Thread.Sleep(AppSetting.AutoPopOutSetting.ReadyToFlyDelay * 1000);
        }


        private async Task StepBeforePanelPopout()
        {
            if (!ActiveProfile.HasCustomPanels)
                return;

            await Task.Run(() =>
            {
                // Set Windowed Display Mode window's configuration if needed
                if (AppSettingData.ApplicationSetting.WindowedModeSetting.AutoResizeMsfsGameWindow && WindowActionManager.IsMsfsGameInWindowedMode())
                {
                    WorkflowStepWithMessage.Execute("Moving and resizing MSFS game window", () =>
                    {
                        WindowActionManager.SetMsfsGameWindowLocation(ActiveProfile.MsfsGameWindowConfig);
                        Thread.Sleep(1000);
                    });
                }

                // Turn on power and avionics if required to pop out panels at least one (fix Cessna 208b grand caravan mod where battery is reported as on)
                if (ActiveProfile.ProfileSetting.PowerOnRequiredForColdStart)
                {
                    _flightSimOrchestrator.TurnOnPower();
                    _flightSimOrchestrator.TurnOnAvionics();
                }

                // Turn off TrackIR if TrackIR is started
                _flightSimOrchestrator.TurnOffTrackIR();

                // Turn on Active Pause
                _flightSimOrchestrator.TurnOnActivePause();

                // Reset all custom pop out panel handles
                ActiveProfile.PanelConfigs.Where(p => p.PanelType == PanelType.CustomPopout).ToList().ForEach(p => p.PanelHandle = IntPtr.MaxValue);
            });
        }

        private async Task StepAfterPanelPopout()
        {
            if (!ActiveProfile.HasCustomPanels)
                return;

            await Task.Run(() =>
            {
                if (ActiveProfile.ProfileSetting.PowerOnRequiredForColdStart)
                {
                    _flightSimOrchestrator.TurnOffAvionics();
                    _flightSimOrchestrator.TurnOffPower();
                }

                // Turn TrackIR back on
                _flightSimOrchestrator.TurnOnTrackIR();
                Thread.Sleep(500);

                // Turn on Active Pause
                _flightSimOrchestrator.TurnOffActivePause();
                Thread.Sleep(500);

                // Return to custom camera view if set
                ReturnToAfterPopOutCameraView();
            });
        }

        private async Task StepPopOutPanels(List<IntPtr> builtInPanelHandles)
        {
            await Task.Run(() =>
            {
                StatusMessageWriter.WriteMessageWithNewLine("Popping out Panels", StatusMessageType.Info);

                // Save current application location to restore it after pop out
                var appLocation = WindowActionManager.GetWindowRectangle(WindowProcessManager.AppProcess.Handle);

                // Resetting camera to default first
                _flightSimOrchestrator.ResetCameraView();
                Thread.Sleep(250);

                var customPanelPopoutIndex = 0;

                foreach (var panelConfig in ActiveProfile.PanelConfigs)
                {
                    switch (panelConfig.PanelType)
                    {
                        case PanelType.CustomPopout:
                            var success = PopOutCustomPanel(panelConfig, builtInPanelHandles, customPanelPopoutIndex);
                            if (success)
                                customPanelPopoutIndex++;
                            break;
                        case PanelType.NumPadWindow:
                            WorkflowStepWithMessage.Execute("Virtual NumPad", () => { OnNumPadOpened?.Invoke(this, panelConfig); }, true);
                            break;
                        case PanelType.SwitchWindow:
                            WorkflowStepWithMessage.Execute("Switch Window", () => { OnSwitchWindowOpened?.Invoke(this, panelConfig); }, true);
                            break;
                    }
                }

                // Restore current application location
                WindowActionManager.MoveWindow(WindowProcessManager.AppProcess.Handle, appLocation);
            });
        }

        private bool PopOutCustomPanel(PanelConfig panelConfig, List<IntPtr> builtInPanelHandles, int index)
        {
            return WorkflowStepWithMessage.Execute($"Custom Panel - {panelConfig.PanelName}", () =>
            {
                _flightSimOrchestrator.SetFixedCamera(panelConfig.FixedCameraConfig.CameraType, panelConfig.FixedCameraConfig.CameraIndex);

                panelConfig.IsSelectedPanelSource = true;

                // Panel source is always available here
                InputEmulationManager.PrepareToPopOutPanel((int)panelConfig.PanelSource.X, (int)panelConfig.PanelSource.Y, AppSetting.GeneralSetting.TurboMode);

                TryPopOutCustomPanel(panelConfig, builtInPanelHandles, index++, AppSetting.GeneralSetting.TurboMode);

                ApplyPanelLocation(panelConfig);
                panelConfig.IsSelectedPanelSource = false;

                if (panelConfig.IsPopOutSuccess != null && !(bool)panelConfig.IsPopOutSuccess)
                    return false;
                
                return true;
            }, true);
        }
        
        private void ConfigureBuiltInPanel(List<IntPtr> builtInPanelHandles)
        {
            if (!ActiveProfile.ProfileSetting.IncludeInGamePanels)
                return;

            WorkflowStepWithMessage.Execute("Configuring built-in panel", () =>
            {
                var count = 0;
                while (builtInPanelHandles.Count == 0 && count < 5)
                {
                    builtInPanelHandles = WindowActionManager.GetWindowsByPanelType(new List<PanelType>() { PanelType.BuiltInPopout });
                    count++;
                }

                foreach (var panelHandle in builtInPanelHandles)
                {
                    var panelCaption = WindowActionManager.GetWindowCaption(panelHandle);
                    var panelConfig = ActiveProfile.PanelConfigs.FirstOrDefault(p => p.PanelName == panelCaption);

                    if (panelConfig == null)
                    {
                        if (!ActiveProfile.IsLocked)
                        {
                            var rectangle = WindowActionManager.GetWindowRectangle(panelHandle);
                            panelConfig = new PanelConfig()
                            {
                                PanelHandle = panelHandle,
                                PanelType = PanelType.BuiltInPopout,
                                PanelName = panelCaption,
                                Top = rectangle.Top,
                                Left = rectangle.Left,
                                Width = rectangle.Width,
                                Height = rectangle.Height,
                                AutoGameRefocus = false
                            };

                            ActiveProfile.PanelConfigs.Add(panelConfig);
                        }
                    }
                    else
                    {
                        panelConfig.PanelHandle = panelHandle;

                        // Need to do it twice for MSFS to take this setting (MSFS issue)
                        ApplyPanelLocation(panelConfig);
                        ApplyPanelLocation(panelConfig);
                    }
                }

                // Set handles for missing built-in panels
                foreach (var panelConfig in ActiveProfile.PanelConfigs)
                {
                    if (panelConfig.PanelType == PanelType.BuiltInPopout && panelConfig.PanelHandle == IntPtr.MaxValue)
                        panelConfig.PanelHandle = IntPtr.Zero;
                }

                return !ActiveProfile.PanelConfigs.Any(p => p.PanelType == PanelType.BuiltInPopout && p.IsPopOutSuccess != null && !(bool)p.IsPopOutSuccess) &&
                       ActiveProfile.PanelConfigs.Count(p => p.PanelType == PanelType.BuiltInPopout) != 0;
            });
        }


        public void SetupRefocusDisplay()
        {
            if (!ActiveProfile.ProfileSetting.RefocusOnDisplay.IsEnabled)
                return;

            var panelConfigs = ActiveProfile.PanelConfigs.Where(p => p.PanelType == PanelType.RefocusDisplay).ToList();

            if (panelConfigs.Count == 0)
                return;

            StatusMessageWriter.WriteMessageWithNewLine("Configuring monitors for auto refocus on touch", StatusMessageType.Info);

            foreach (var panelConfig in panelConfigs)
                WorkflowStepWithMessage.Execute(panelConfig.PanelName, () => panelConfig.PanelHandle = new IntPtr(1), true);
        }

        private void StepApplyPanelConfig()
        {
            // Must apply other panel config after pop out since MSFS popping out action will overwrite panel config such as Always on Top
            foreach (var panelConfig in ActiveProfile.PanelConfigs)
            {
                ApplyPanelConfig(panelConfig);
            }
        }

        private async Task StepPostPopout()
        {
            await Task.Run(() =>
            {
                // Set profile pop out status
                ActiveProfile.IsPoppedOut = true;

                // Must use application dispatcher to dispatch UI events (winEventHook)
                _panelConfigurationOrchestrator.StartConfiguration();

                // Start touch hook
                _panelConfigurationOrchestrator.StartTouchHook();

                StatusMessageWriter.WriteMessageWithNewLine(
                    CheckForPopOutError()
                        ? "Pop out has been completed with error."
                        : "Pop out has been completed successfully.", StatusMessageType.Info);

                Thread.Sleep(1000);
            });
        }
        
        private void TryPopOutCustomPanel(PanelConfig panelConfig, List<IntPtr> builtInPanelHandles, int index, bool isTurbo)
        {
            var newHandle = IntPtr.MaxValue;

            // Try to pop out 5 times before failure with 1/2 second wait in between
            var count = 0;
            do
            {
                if(panelConfig.PanelSource.X != null && panelConfig.PanelSource.Y != null)
                    InputEmulationManager.PopOutPanel((int)panelConfig.PanelSource.X, (int)panelConfig.PanelSource.Y, AppSetting.PopOutSetting.UseLeftRightControlToPopOut, isTurbo);
                
                var latestAceAppWindowsWithCaptionHandles = WindowActionManager.GetWindowsByPanelType(new List<PanelType>() { PanelType.BuiltInPopout });

                // There should only be one handle that is not in both builtInPanelHandles vs latestAceAppWindowsWithCaptionHandles
                newHandle = latestAceAppWindowsWithCaptionHandles.Except(builtInPanelHandles).FirstOrDefault();

                if (newHandle != IntPtr.Zero)
                    break;

                Thread.Sleep(500);
                count++;
            }
            while (count < 5);
            
            if (newHandle == IntPtr.MaxValue)
            {
                panelConfig.PanelHandle = IntPtr.Zero;
                return;
            }

            // Unable to pop out panelConfig, the handle was previously popped out's handle
            if (ProfileData.ActiveProfile.PanelConfigs.Any(p => p.PanelHandle.Equals(newHandle)) || newHandle.Equals(WindowProcessManager.SimulatorProcess.Handle))
            {
                panelConfig.PanelHandle = IntPtr.Zero;
                return;
            }

            panelConfig.PanelHandle = newHandle;

            // Use MSFS panel name when naming new panel
            if (panelConfig.PanelName.Equals("Default Panel Name", StringComparison.InvariantCultureIgnoreCase) || string.IsNullOrEmpty(panelConfig.PanelName.Trim()))
                panelConfig.PanelName = WindowActionManager.GetWindowCaption(panelConfig.PanelHandle);
            
            WindowActionManager.SetWindowCaption(panelConfig.PanelHandle, $"{panelConfig.PanelName} (Custom)");

            // First time popping out
            if (panelConfig.Width == 0 && panelConfig.Height == 0)
            {
                var rect = WindowActionManager.GetWindowRectangle(panelConfig.PanelHandle);
                panelConfig.Top = 0 + index * 30;
                panelConfig.Left = 0 + index * 30;
                panelConfig.Width = rect.Width;
                panelConfig.Height = rect.Height;
            }
        }

        private void ApplyPanelLocation(PanelConfig panel)
        {
            if (panel.IsPopOutSuccess == null || !((bool)panel.IsPopOutSuccess) || panel.PanelHandle == IntPtr.Zero)
                return;

            // Apply top/left/width/height
            WindowActionManager.MoveWindow(panel.PanelHandle, panel.Left, panel.Top, panel.Width, panel.Height);
        }

        private void ApplyPanelConfig(PanelConfig panel)
        {
            if (panel.IsPopOutSuccess == null || !((bool)panel.IsPopOutSuccess) || panel.PanelHandle == IntPtr.Zero)
                return;

            if (!panel.FullScreen)
            {
                // Set title bar color
                if (AppSettingData.ApplicationSetting.PopOutSetting.PopOutTitleBarCustomization.IsEnabled)
                {
                    WindowActionManager.SetWindowTitleBarColor(panel.PanelHandle, AppSettingData.ApplicationSetting.PopOutSetting.PopOutTitleBarCustomization.HexColor);
                }

                // Apply hide title bar
                if (panel.HideTitlebar)
                {
                    WindowActionManager.ApplyHidePanelTitleBar(panel.PanelHandle, true);
                    Thread.Sleep(250);
                }

                // Apply always on top (must apply this last)
                if (panel.AlwaysOnTop)
                {
                    WindowActionManager.ApplyAlwaysOnTop(panel.PanelHandle, panel.PanelType, panel.AlwaysOnTop);
                    Thread.Sleep(250);
                }
            }

            if (panel.FullScreen && !panel.AlwaysOnTop && !panel.HideTitlebar)
            {
                Thread.Sleep(500);
                InputEmulationManager.ToggleFullScreenPanel(panel.PanelHandle);
                Thread.Sleep(250);
            }

            if (panel.FloatingPanel.IsEnabled && panel.FloatingPanel.HasKeyboardBinding && panel.FloatingPanel.IsHiddenOnStart)
            {
                panel.IsFloating = false;
                WindowActionManager.MinimizeWindow(panel.PanelHandle);
            }
        }

        private void ReturnToAfterPopOutCameraView()
        {
            if (!AppSetting.PopOutSetting.AfterPopOutCameraView.IsEnabled)
                return;

            StatusMessageWriter.WriteMessageWithNewLine("Applying cockpit view after pop out", StatusMessageType.Info);

            if (FlightSimData.CameraViewTypeAndIndex1 == CAMERA_VIEW_HOME_COCKPIT_MODE)
            {
                _flightSimOrchestrator.ResetCameraView();
            }
            else
            {
                switch (AppSetting.PopOutSetting.AfterPopOutCameraView.CameraView)
                {
                    case AfterPopOutCameraViewType.CockpitCenterView:
                        WorkflowStepWithMessage.Execute("Resetting camera view", ResetToPilotView);

                        // This does not seem to work anymore
                        // WorkflowStepWithMessage.Execute("Resetting camera view", () => ResetCockpitView(AppSetting.GeneralSetting.TurboMode), true);
                        break;
                    case AfterPopOutCameraViewType.CustomCameraView:
                        WorkflowStepWithMessage.Execute("Resetting camera view", () => ResetCockpitView(AppSetting.GeneralSetting.TurboMode), true);
                        WorkflowStepWithMessage.Execute("Loading custom camera view", () => LoadCustomView(AppSetting.PopOutSetting.AfterPopOutCameraView.KeyBinding, AppSetting.GeneralSetting.TurboMode, true), true);
                        break;
                }
            }
        }

        private bool CheckForPopOutError()
        {
            return ActiveProfile.PanelConfigs.Count(p => p.IsPopOutSuccess != null && (bool)p.IsPopOutSuccess) != ActiveProfile.PanelConfigs.Count(p => p.IsPopOutSuccess != null);
        }

        private void ResetCockpitView(bool isTurboMode)
        {
            const int retry = 5;
            for (var i = 0; i < retry; i++)
            {
                _flightSimOrchestrator.ResetCameraView();
                Thread.Sleep(isTurboMode ? 600 : 1000);  // wait for flightsimdata to be updated

                if (FlightSimData.CameraViewTypeAndIndex1 is 0 or 1)
                    break;
            }
        }

        private void ResetToPilotView()
        {
            _flightSimOrchestrator.SetFixedCamera(CameraType.Cockpit, 0);
        }

        private bool LoadCustomView(string keyBinding, bool isTurboMode, bool ignoreError)
        {
            _flightSimOrchestrator.ResetCameraView();
            Thread.Sleep(250);

            const int retry = 5;
            for(var i = 0; i < retry; i++) 
            {
                InputEmulationManager.LoadCustomView(keyBinding);
                Thread.Sleep(isTurboMode ? 1000 : 1200); // wait for flightsimdata to be updated
                if (FlightSimData.CameraViewTypeAndIndex1 == CAMERA_VIEW_CUSTOM_CAMERA)    // custom camera view enum
                    return true;
            }

            return ignoreError;
        }
    }
}

