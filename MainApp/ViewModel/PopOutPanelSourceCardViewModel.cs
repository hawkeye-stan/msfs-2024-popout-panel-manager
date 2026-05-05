using MSFSPopoutPanelManager.DomainModel.Profile;
using MSFSPopoutPanelManager.Orchestration;
using MSFSPopoutPanelManager.Shared;
using MSFSPopoutPanelManager.WindowsAgent;
using Prism.Commands;
using System;
using System.Linq;
using System.Windows.Input;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.ComponentModel;

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

        public ObservableRangeCollection<FixedCameraConfig> FixedCameraConfigs => _panelSourceOrchestrator.FixedCameraConfigs;

        public BehaviorSubject<bool> IsChasePlane { get; private set; } = new(false);

        public event EventHandler RebindChasePlaneCamera;

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

            IsChasePlane.OnNext(AppSettingData.ApplicationSetting.ChasePlaneSetting.IsEnabled);

            AppSettingData.ApplicationSetting.ChasePlaneSetting.PropertyChanged -= ChasePlaneSetting_PropertyChanged;
            AppSettingData.ApplicationSetting.ChasePlaneSetting.PropertyChanged += ChasePlaneSetting_PropertyChanged;

            _panelSourceOrchestrator.OnForceChasePlaneViewsRebind -= PanelSourceOrchestrator_OnForceChasePlaneViewsRebind;
            _panelSourceOrchestrator.OnForceChasePlaneViewsRebind += PanelSourceOrchestrator_OnForceChasePlaneViewsRebind;
        }

        public void SetCamera()
        {
            if(DataItem.IsEditingPanel)
                _panelSourceOrchestrator.SetCamera(DataItem);
        }

        private void ChasePlaneSetting_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName.Equals("IsEnabled", StringComparison.InvariantCultureIgnoreCase))
            {
                IsChasePlane.OnNext(AppSettingData.ApplicationSetting.ChasePlaneSetting.IsEnabled);
            }
        }

        private void PanelSourceOrchestrator_OnForceChasePlaneViewsRebind(object sender, EventArgs e)
        {
            if (AppSettingData.ApplicationSetting.ChasePlaneSetting.IsEnabled && ChasePlaneManager.ChasePlaneCameraConfigs.Count > 0)
                RebindChasePlaneCamera?.Invoke(this, null);
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

            if (AppSettingData.ApplicationSetting.ChasePlaneSetting.IsEnabled)
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

                            DataItem.ChasePlaneCameraConfigs.Add(cameraView);
                            _panelSourceOrchestrator.ForceChasePlaneViewsRebind();
                        }
                    }
                    else
                    {
                        var matchingCamera = ChasePlaneManager.ChasePlaneViews?.FirstOrDefault(x => x.Name.Equals(cameraView.Name, StringComparison.InvariantCultureIgnoreCase));

                        if (matchingCamera != null)
                        {
                            cameraView = new ChasePlaneCameraConfig()
                            {
                                Name = matchingCamera.Name,
                                Guid = matchingCamera.Guid,
                                AircraftName = FlightSimData.AircraftName
                            };

                            DataItem.ChasePlaneCameraConfigs.Add(cameraView);
                            _panelSourceOrchestrator.ForceChasePlaneViewsRebind();
                        }
                        else
                        {
                            // The camera view does not exist in this aircraft variant, use pilot view instead
                            //var pilotCamera = ChasePlaneManager.ChasePlaneViews?.FirstOrDefault(x => x.Name.Equals("pilot", StringComparison.InvariantCultureIgnoreCase));

                            //if (pilotCamera != null)
                            //{
                            //    cameraView = new ChasePlaneCameraConfig()
                            //    {
                            //        Name = pilotCamera.Name,
                            //        Guid = pilotCamera.Guid,
                            //        AircraftName = FlightSimData.AircraftName
                            //    };
                            //}   
                        }
                    }
                }
            }

            DataItem.IsSelectedPanelSource = true;
            _panelSourceOrchestrator.ShowPanelSourceForEdit(DataItem);
            _panelSourceOrchestrator.SetCamera(DataItem);
        }
    }
}
