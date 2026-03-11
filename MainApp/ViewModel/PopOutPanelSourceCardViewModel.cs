using MSFSPopoutPanelManager.DomainModel.Profile;
using MSFSPopoutPanelManager.Orchestration;
using MSFSPopoutPanelManager.WindowsAgent;
using Prism.Commands;
using System;
using System.Linq;
using System.Windows.Input;

namespace MSFSPopoutPanelManager.MainApp.ViewModel
{
    public class PopOutPanelSourceCardViewModel : BaseViewModel
    {
        private readonly PanelSourceOrchestrator _panelSourceOrchestrator;
        private readonly PanelConfigurationOrchestrator _panelConfigurationOrchestrator;

        public PanelConfig DataItem { get; set; }

        public ICommand AddPanelSourceLocationCommand { get; set; }

        public DelegateCommand<string> PanelAttributeUpdatedCommand { get; set; }

        public DelegateCommand EditPanelSourceCommand { get; set; }

        public event EventHandler<CameraViewReadyEventArgs> OnChasePlaneCameraViewReady;
        public event EventHandler<FixedCameraViewReadyEventArgs> OnFixedCameraViewReady;

        public bool IsChasePlane { get { return AppSettingData.ApplicationSetting.ChasePlaneSetting.IsEnabled; } }

        public PopOutPanelSourceCardViewModel(SharedStorage sharedStorage, PanelSourceOrchestrator panelSourceOrchestrator, PanelConfigurationOrchestrator panelConfigurationOrchestrator) : base(sharedStorage)
        {
            _panelSourceOrchestrator = panelSourceOrchestrator;
            _panelConfigurationOrchestrator = panelConfigurationOrchestrator;

            AddPanelSourceLocationCommand = new DelegateCommand(OnAddPanelSourceLocation, () => ActiveProfile != null && !ActiveProfile.IsSelectingPanelSource && FlightSimData.IsInCockpit)
                                                                                .ObservesProperty(() => ActiveProfile)
                                                                                .ObservesProperty(() => ActiveProfile.IsSelectingPanelSource)
                                                                                .ObservesProperty(() => FlightSimData.IsInCockpit);

            EditPanelSourceCommand = new DelegateCommand(OnEditPanelSource, () => ActiveProfile != null && FlightSimData.IsInCockpit)
                                                                                .ObservesProperty(() => ActiveProfile)
                                                                                .ObservesProperty(() => FlightSimData.IsInCockpit);

            PanelAttributeUpdatedCommand = new DelegateCommand<string>(OnPanelAttributeUpdated);

            _panelSourceOrchestrator.OnChasePlaneCameraReady += delegate { };
            _panelSourceOrchestrator.OnChasePlaneCameraReady += (_, e) => { OnChasePlaneCameraViewReady?.Invoke(this, e); };

            _panelSourceOrchestrator.OnFixedCameraReady += delegate { };
            _panelSourceOrchestrator.OnFixedCameraReady += (_, e) => { OnFixedCameraViewReady?.Invoke(this, e); };
        }

        public void SetCamera()
        {
            if(DataItem.IsEditingPanel)
                _panelSourceOrchestrator.SetCamera(DataItem);
        }

        private void OnPanelAttributeUpdated(string commandParameter)
        {
            if (DataItem != null && commandParameter != null)
                _panelConfigurationOrchestrator.PanelConfigPropertyUpdated(DataItem.PanelHandle, (PanelConfigPropertyName)Enum.Parse(typeof(PanelConfigPropertyName), commandParameter));
        }

        private void OnAddPanelSourceLocation()
        {
            _panelSourceOrchestrator.ShowPanelSourceForEdit(DataItem);      // This is to reset active panel source

            DataItem.IsEditingPanel = true;

            _panelSourceOrchestrator.StartPanelSelectionEvent();
            _panelSourceOrchestrator.StartPanelSelection(DataItem);

            _panelSourceOrchestrator.SetCamera(DataItem);
        }

        private void OnEditPanelSource()
        {
            if (!DataItem.HasPanelSource)
                return;

            if (IsChasePlane)
            {
                // Add aircraft variant record if does not exist
                var cameraConfig = DataItem.ChasePlaneCameraConfigs.FirstOrDefault(x => x.AircraftName.Equals(FlightSimData.AircraftName, StringComparison.InvariantCultureIgnoreCase));

                if (cameraConfig == null)
                {
                    // Try to match camera record with same name
                    var cameraView = DataItem.ChasePlaneCameraConfigs.FirstOrDefault();

                    if (cameraView == null)
                    {
                        // Use pilot camera view if panel never has a camera view before
                        var pilotCamera = ChasePlaneManager.ChasePlaneViews?.FirstOrDefault(x => x.Name.Equals("pilot", StringComparison.InvariantCultureIgnoreCase));

                        if (pilotCamera != null)
                        {
                            cameraView = new ChasePlaneCameraConfig()
                            {
                                Name = pilotCamera.Name,
                                Guid = pilotCamera.Guid,
                                AircraftName = FlightSimData.AircraftName
                            };
                        }
                    }
                    else
                    {
                        var existingCamera = ChasePlaneManager.ChasePlaneViews?.FirstOrDefault(x => x.Name.Equals(cameraView.Name, StringComparison.InvariantCultureIgnoreCase));

                        if (existingCamera == null)
                        {
                            // The camera view does not exist in this aircraft variant, use pilot view instead
                            var pilotCamera = ChasePlaneManager.ChasePlaneViews?.FirstOrDefault(x => x.Name.Equals("pilot", StringComparison.InvariantCultureIgnoreCase));

                            if (pilotCamera != null)
                            {
                                cameraView = new ChasePlaneCameraConfig()
                                {
                                    Name = pilotCamera.Name,
                                    Guid = pilotCamera.Guid,
                                    AircraftName = FlightSimData.AircraftName
                                };
                            }
                        }
                        else
                        {
                            cameraView = new ChasePlaneCameraConfig()
                            {
                                Name = existingCamera.Name,
                                Guid = existingCamera.Guid,
                                AircraftName = FlightSimData.AircraftName
                            };
                        }
                    }

                    DataItem.ChasePlaneCameraConfigs.Add(cameraView);

                    OnChasePlaneCameraViewReady?.Invoke(this, null);
                }
            }

            DataItem.IsSelectedPanelSource = true;
            _panelSourceOrchestrator.ShowPanelSourceForEdit(DataItem);
            _panelSourceOrchestrator.SetCamera(DataItem);
        }
    }
}
