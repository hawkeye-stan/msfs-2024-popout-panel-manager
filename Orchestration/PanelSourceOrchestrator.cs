using MSFSPopoutPanelManager.DomainModel.Profile;
using MSFSPopoutPanelManager.Shared;
using MSFSPopoutPanelManager.WindowsAgent;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Point = System.Drawing.Point;

namespace MSFSPopoutPanelManager.Orchestration
{
    public class PanelSourceOrchestrator : BaseOrchestrator
    {
        private readonly FlightSimOrchestrator _flightSimOrchestrator;
        private bool _isEditingPanelSourceLock;

        public PanelSourceOrchestrator(SharedStorage sharedStorage, FlightSimOrchestrator flightSimOrchestrator) : base(sharedStorage)
        {
            _flightSimOrchestrator = flightSimOrchestrator;

            ProfileData.OnActiveProfileChanged += (_, _) => { CloseAllPanelSource(); };

            flightSimOrchestrator.OnFlightStopped += (_, _) => { CloseAllPanelSource(); };

            flightSimOrchestrator.OnCameraViewTypeAndIndex2MaxChanged += (_, _) =>
            {
                FixedCameraConfigs.Clear();
                FixedCameraConfigs.AddRange(GetFixedCameraConfigs());
            };
        }

        internal IntPtr ApplicationHandle { get; set; }

        private UserProfile ActiveProfile => ProfileData?.ActiveProfile;

        public event EventHandler<PanelConfig> OnOverlayShowed;
        public event EventHandler<PanelConfig> OnOverlayRemoved;
        public event EventHandler OnForceChasePlaneViewsRebind;

        public ObservableRangeCollection<FixedCameraConfig> FixedCameraConfigs { get; private set; } = new();

        public void StartPanelSelectionEvent()
        {
            if (ActiveProfile.IsSelectingPanelSource)
                return;
                        
            ActiveProfile.IsSelectingPanelSource = true;
        }

        public void StartPanelSelection(PanelConfig panelConfig)
        {
            InputHookManager.OnLeftClick -= (_, e) => HandleOnPanelSelectionAdded(panelConfig, e);
            InputHookManager.OnLeftClick += (_, e) => HandleOnPanelSelectionAdded(panelConfig, e);
            InputHookManager.StartMouseHook();
        }

        public async Task StartEditPanelSources()
        {
            OnForceChasePlaneViewsRebind?.Invoke(this, null);

            // clear all number circles
            foreach (var panelConfig in ActiveProfile.PanelConfigs)
                OnOverlayRemoved?.Invoke(this, panelConfig);

            // Turn off TrackIR if TrackIR is started
            _flightSimOrchestrator.TurnOffTrackIR();

            // Connect websocket to ChasePlane API if enabled
            if (AppSettingData.ApplicationSetting.ChasePlaneSetting.IsEnabled)
            {
                await Task.Run(() =>
                {
                    var result = WorkflowStepWithMessage.Execute("Connecting to ChasePlane API", async () =>
                    {
                        if(!await ChasePlaneManager.Run(true))
                            return false;

                        var result = ChasePlaneManager.IsChasePlaneViewsReady.WaitOne(10000);
                        if (!result)
                        {
                            await ChasePlaneManager.Disconnect();
                            return false;
                        }

                        return true;
                    });

                    return result;
                });
            }
        }

        public async Task EndEditPanelSources()
        {
            // Connect websocket to ChasePlane API if enabled
            if (AppSettingData.ApplicationSetting.ChasePlaneSetting.IsEnabled)
            {
                await ChasePlaneManager.SetDefaultCamera();

                // Validate ChasePlane panel config to enable/disable start pop out button
                ActiveProfile.IsDisabledStartPopOut = !IsPanelConfigsValid();
            }
            else
            {
                _flightSimOrchestrator.ResetCameraView();
            }

            foreach (var panelConfig in ProfileData.ActiveProfile.PanelConfigs)
            {
                panelConfig.IsEditingPanel = false;
                panelConfig.IsSelectedPanelSource = false;
                OnOverlayRemoved?.Invoke(this, panelConfig);
            }

            ActiveProfile.IsSelectingPanelSource = false;

            await Task.Run(() =>
            {
                WindowActionManager.BringWindowToForeground(ApplicationHandle);

                // Turn TrackIR back on
                _flightSimOrchestrator.TurnOnTrackIR();
            });

            // End all mouse hook if active
            InputHookManager.EndMouseHook();
        }

        public void ShowPanelSourceForEdit(PanelConfig panel)
        {
            foreach (var panelConfig in ActiveProfile.PanelConfigs)
            {
                OnOverlayRemoved?.Invoke(this, panelConfig);
                panelConfig.IsEditingPanel = false;
            }

            if (panel != null)
            {
                panel.IsEditingPanel = true;

                if (panel.HasPanelSource)
                    OnOverlayShowed?.Invoke(this, panel);
            }
        }

        public void RemovePanelSource(PanelConfig panelConfig)
        {
            // Disable hooks if active
            InputHookManager.EndMouseHook();

            ProfileData.ActiveProfile.CurrentMoveResizePanelId = Guid.Empty;

            OnOverlayRemoved?.Invoke(this, panelConfig);

            ProfileData.ActiveProfile.PanelConfigs.Remove(panelConfig);
        }

        public void CloseAllPanelSource()
        {
            if (ActiveProfile != null)
            {
                ActiveProfile.IsEditingPanelSource = false;

                foreach (var panelConfig in ActiveProfile.PanelConfigs)
                    OnOverlayRemoved?.Invoke(this, panelConfig);
            }
        }

        public void SetCamera(PanelConfig panel)
        {
            if (AppSettingData.ApplicationSetting.ChasePlaneSetting.IsEnabled)
                SetChasePlaneCamera(panel);
            else
                SetMsfsCamera(panel);
        }

        public void HandleOnPanelSelectionAdded(PanelConfig panelConfig, Point e)
        {
            if (WindowActionManager.IsPointInsideAppWindow(e))
                return;

            InputHookManager.EndMouseHook();

            if (ActiveProfile == null)
                return;

            panelConfig.PanelSource.X = e.X;
            panelConfig.PanelSource.Y = e.Y;

            ProfileData.WriteProfiles();

            // Show source circle on screen
            OnOverlayShowed?.Invoke(this, panelConfig);

            // If using windows mode, save MSFS game window configuration
            if (AppSettingData.ApplicationSetting.WindowedModeSetting.AutoResizeMsfsGameWindow)
                ProfileData.SaveMsfsGameWindowConfig();

            panelConfig.IsSelectedPanelSource = false;

            ActiveProfile.IsSelectingPanelSource = false;
        }

        public bool IsPanelConfigsValid()
        {
            if (!FlightSimData.IsInCockpit)
                return false;

            if (ProfileData == null || ActiveProfile == null)
                return false;

            if (ActiveProfile.HasUnidentifiedPanelSource)
                return false;

            if (ActiveProfile.IsEditingPanelSource)
                return false;

            if (ActiveProfile.PanelConfigs.Count == 0)
                return false;

            if (ActiveProfile.PanelConfigs.Any(p => p.PanelType == PanelType.CustomPopout && p.PanelSource.X == null))
                return false;

            if (AppSettingData.ApplicationSetting.ChasePlaneSetting.IsEnabled)
            {
                var panelConfigs = ActiveProfile.PanelConfigs.TakeWhile(p => p.PanelType == PanelType.CustomPopout);

                foreach (var panelConfig in panelConfigs)
                {
                    if (!panelConfig.ChasePlaneCameraConfigs.Any(p => p.AircraftName.Equals(FlightSimData.AircraftName, StringComparison.InvariantCultureIgnoreCase)))
                        return false;
                }
            }


            return true;
        }

        private List<FixedCameraConfig> GetFixedCameraConfigs()
        {
            var configs = new List<FixedCameraConfig>()
                {
                    new() { Id = 0, Name = "Cockpit Pilot", CameraType = CameraType.Cockpit, CameraIndex = 1 },
                    new() { Id = 1, Name = "Cockpit Copilot", CameraType = CameraType.Cockpit, CameraIndex = 5 }
                };

            for (var i = 0; i < FlightSimData.CameraViewTypeAndIndex2Max; i++)
            {
                var item = new FixedCameraConfig
                {
                    Id = i + 2,
                    Name = $"Instrument {i + 1}",
                    CameraType = CameraType.Instrument,
                    CameraIndex = i
                };
                configs.Add(item);
            }

            return configs;
        }

        private void SetMsfsCamera(PanelConfig panel)
        {
            if (!FlightSimData.IsInCockpit || panel.FixedCameraConfig == null)
                return;

            if (panel.FixedCameraConfig.CameraType == CameraType.Cockpit)
            {
                _flightSimOrchestrator.ResetCameraView();
                Thread.Sleep(250);
            }

            _flightSimOrchestrator.SetFixedCamera(panel.FixedCameraConfig.CameraType, panel.FixedCameraConfig.CameraIndex);
            Thread.Sleep(250);
        }

        private async Task SetChasePlaneCamera(PanelConfig panel)
        {
            var cameraView = panel.ChasePlaneCameraConfigs?.FirstOrDefault(v => v.AircraftName.Equals(FlightSimData.AircraftName, StringComparison.InvariantCultureIgnoreCase));

            if (!FlightSimData.IsInCockpit || cameraView == null)
                return;

            if (cameraView != null)
                await ChasePlaneManager.SetCamera(cameraView.Name, cameraView.Guid);

            Thread.Sleep(250);
        }

        public void ForceChasePlaneViewsRebind()
        {
            OnForceChasePlaneViewsRebind?.Invoke(this, null);
        }
    }

    public class FixedCameraViewReadyEventArgs : EventArgs
    {
        public List<FixedCameraConfig> CameraConfigs { get; }

        public FixedCameraViewReadyEventArgs(List<FixedCameraConfig> cameraConfigs)
        {
            CameraConfigs = cameraConfigs;
        }
    }
}
