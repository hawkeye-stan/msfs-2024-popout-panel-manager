using MSFSPopoutPanelManager.DomainModel.Profile;
using MSFSPopoutPanelManager.WindowsAgent;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
        }

        internal IntPtr ApplicationHandle { get; set; }

        private UserProfile ActiveProfile => ProfileData?.ActiveProfile;

        public event EventHandler<PanelConfig> OnOverlayShowed;
        public event EventHandler<PanelConfig> OnOverlayRemoved;

        public void StartPanelSelectionEvent()
        {
            if (ActiveProfile.IsSelectingPanelSource)
                return;

            WindowProcessManager.SetApplicationProcess();
            ActiveProfile.IsSelectingPanelSource = true;
        }

        public void StartPanelSelection(PanelConfig panelConfig)
        {
            InputHookManager.OnLeftClick -= (_, e) => HandleOnPanelSelectionAdded(panelConfig, e);
            InputHookManager.OnLeftClick += (_, e) => HandleOnPanelSelectionAdded(panelConfig, e);
            InputHookManager.StartMouseHook();
        }

        public void StartEditPanelSources()
        {
            if (_isEditingPanelSourceLock)
                return;

            _isEditingPanelSourceLock = true;

            // Turn off TrackIR if TrackIR is started
            _flightSimOrchestrator.TurnOffTrackIR();
        }

        public async Task EndEditPanelSources()
        {
            if (!_isEditingPanelSourceLock)
                return;

            _flightSimOrchestrator.ResetCameraView();

            _isEditingPanelSourceLock = false;

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

            panel.IsEditingPanel = true;

            if (panel.HasPanelSource)
                OnOverlayShowed?.Invoke(this, panel);
        }

        public void CloseAllPanelSource()
        {
            if (ActiveProfile != null)
            {
                ActiveProfile.IsEditingPanelSource = false;
                _isEditingPanelSourceLock = false;

                foreach (var panelConfig in ActiveProfile.PanelConfigs)
                    OnOverlayRemoved?.Invoke(this, panelConfig);
            }
        }

        public void SetCamera(PanelConfig panel)
        {
            if (!FlightSimData.IsInCockpit)
                return;

            if (panel.FixedCameraConfig == null)
                return;
            
            Task.Run(() =>
            {
                if (panel.FixedCameraConfig.CameraType == CameraType.Cockpit)
                {
                    _flightSimOrchestrator.ResetCameraView();
                    Thread.Sleep(250);
                }

                _flightSimOrchestrator.SetFixedCamera(panel.FixedCameraConfig.CameraType, panel.FixedCameraConfig.CameraIndex);
            });
            Thread.Sleep(250);
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

        public void RemovePanelSource(PanelConfig panelConfig)
        {
            // Disable hooks if active
            InputHookManager.EndMouseHook();

            ProfileData.ActiveProfile.CurrentMoveResizePanelId = Guid.Empty;

            OnOverlayRemoved?.Invoke(this, panelConfig);

            ProfileData.ActiveProfile.PanelConfigs.Remove(panelConfig);
        }

        public ObservableCollection<FixedCameraConfig> GetFixedCameraConfigs()
        {
            var configs = new List<FixedCameraConfig>
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

            return new ObservableCollection<FixedCameraConfig>(configs);
        }
    }
}
