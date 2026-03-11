using MSFSPopoutPanelManager.DomainModel.Profile;
using MSFSPopoutPanelManager.MainApp.ViewModel;
using MSFSPopoutPanelManager.Shared;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace MSFSPopoutPanelManager.MainApp.AppUserControl.PopOutPanelCard
{
    /// <summary>
    /// Interaction logic for PanelTargetField.xaml
    /// </summary>
    public partial class PanelTargetField
    {
        public ObservableRangeCollection<ChasePlaneCameraConfig> _chasePlaneCameraConfigs = new();
        public ObservableRangeCollection<FixedCameraConfig> _fixedCameraConfigs = new();

        public PanelTargetField()
        {
            InitializeComponent();

            this.ComboBoxCameraSelection.ItemsSource = null;

            this.Loaded += PanelTargetField_Loaded;
        }

        private void PanelTargetField_Loaded(object sender, RoutedEventArgs e)
        {
            var dataContext = ((PopOutPanelSourceCardViewModel) this.DataContext);

            if (dataContext == null)
                return;

            if (dataContext.IsChasePlane)
                this.ComboBoxCameraSelection.ItemsSource = _chasePlaneCameraConfigs;
            else
                this.ComboBoxCameraSelection.ItemsSource = _fixedCameraConfigs;

            dataContext.OnChasePlaneCameraViewReady += delegate { };
            dataContext.OnChasePlaneCameraViewReady += (_, e) =>
            {
                if (!dataContext.ActiveProfile.IsEditingPanelSource)
                    return;

                if (e != null)
                {
                    if (e.CameraConfigs == null || e.CameraConfigs.Count == 0)
                        return;

                    this.ComboBoxCameraSelection.ItemsSource = _chasePlaneCameraConfigs;
                    _chasePlaneCameraConfigs.Clear();
                    _chasePlaneCameraConfigs.AddRange(e.CameraConfigs);
                }

                var cameraConfig = dataContext.DataItem.ChasePlaneCameraConfigs?.FirstOrDefault(x => x.AircraftName.Equals(dataContext.FlightSimData.AircraftName, System.StringComparison.InvariantCultureIgnoreCase));

                if (cameraConfig != null)
                {
                    var index = _chasePlaneCameraConfigs.ToList().FindIndex(x => x.Guid == cameraConfig.Guid);
                    this.ComboBoxCameraSelection.SelectedIndex = index == -1 ? 0 : index;
                }
                else
                {
                    if (dataContext.DataItem.IsNewlyAddedPanel)
                    {
                        this.ComboBoxCameraSelection.SelectedIndex = 0;
                        dataContext.DataItem.IsNewlyAddedPanel = false;
                    }
                    else
                        this.ComboBoxCameraSelection.SelectedIndex = -1;
                }
            };

           dataContext.OnFixedCameraViewReady += delegate { };
           dataContext.OnFixedCameraViewReady += (_, e) =>
            {
                if (!dataContext.ActiveProfile.IsEditingPanelSource)
                    return;

                if (e.CameraConfigs == null || e.CameraConfigs.Count == 0)
                    return;

                this.ComboBoxCameraSelection.ItemsSource = _fixedCameraConfigs;
                _fixedCameraConfigs.Clear();
                _fixedCameraConfigs.AddRange(e.CameraConfigs);

                var index = e.CameraConfigs.FindIndex(x => x.Name.Equals(dataContext.DataItem.FixedCameraConfig.Name, System.StringComparison.InvariantCultureIgnoreCase));

                if (index != -1)
                    this.ComboBoxCameraSelection.SelectedIndex = index;
                else
                {
                    this.ComboBoxCameraSelection.SelectedIndex = 0;
                    dataContext.DataItem.IsNewlyAddedPanel = false;
                }
            };
        }

        private void PopupBoxCameraSelectionPrev_Clicked(object sender, RoutedEventArgs e)
        {
            var dataContext = ((PopOutPanelSourceCardViewModel)this.DataContext);

            if (dataContext == null)
                return;

            var index = this.ComboBoxCameraSelection.SelectedIndex;

            if (index == -1)
                return;

            if (index == 0)
                index = this.ComboBoxCameraSelection.Items.Count - 1;
            else
                index -= 1;

            this.ComboBoxCameraSelection.SelectedIndex = index;


            if (!dataContext.IsChasePlane)
            {
                dataContext.DataItem.FixedCameraConfig = (FixedCameraConfig)this.ComboBoxCameraSelection.SelectedItem;
            }
            else
            {
                var item = dataContext.DataItem.ChasePlaneCameraConfigs.FirstOrDefault(x => x.AircraftName.Equals(dataContext.FlightSimData.AircraftName, System.StringComparison.InvariantCultureIgnoreCase));

                if (item == null)
                    dataContext.DataItem.ChasePlaneCameraConfigs.Add((ChasePlaneCameraConfig)this.ComboBoxCameraSelection.SelectedItem);
                else
                {
                    dataContext.DataItem.ChasePlaneCameraConfigs.Remove(item);
                    dataContext.DataItem.ChasePlaneCameraConfigs.Add((ChasePlaneCameraConfig)this.ComboBoxCameraSelection.SelectedItem);
                }
            }

            dataContext.SetCamera();
        }

        private void PopupBoxCameraSelectionNext_Clicked(object sender, RoutedEventArgs e)
        {
            var dataContext = ((PopOutPanelSourceCardViewModel)this.DataContext);

            if (dataContext == null)
                return;

            var index = this.ComboBoxCameraSelection.SelectedIndex;

            if (index == -1)
                index = 0;

            if (index == this.ComboBoxCameraSelection.Items.Count - 1)
                index = 0;
            else
                index += 1;

            this.ComboBoxCameraSelection.SelectedIndex = index;


            if (!dataContext.IsChasePlane)
            {
                dataContext.DataItem.FixedCameraConfig = (FixedCameraConfig)this.ComboBoxCameraSelection.SelectedItem;
            }
            else
            {
                var item = dataContext.DataItem.ChasePlaneCameraConfigs.FirstOrDefault(x => x.AircraftName.Equals(dataContext.FlightSimData.AircraftName, System.StringComparison.InvariantCultureIgnoreCase));

                if (item == null)
                    dataContext.DataItem.ChasePlaneCameraConfigs.Add((ChasePlaneCameraConfig)this.ComboBoxCameraSelection.SelectedItem);
                else
                {
                    dataContext.DataItem.ChasePlaneCameraConfigs.Remove(item);
                    dataContext.DataItem.ChasePlaneCameraConfigs.Add((ChasePlaneCameraConfig)this.ComboBoxCameraSelection.SelectedItem);
                }
            }

            dataContext.SetCamera();
        }

        private void ComboBoxCameraSelection_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var dataContext = ((PopOutPanelSourceCardViewModel)this.DataContext);

            if (!dataContext.DataItem.IsEditingPanel)
                return;

            if (e.AddedItems.Count <= 0) 
                return;

            var addedItem = e.AddedItems[0];

            if (!dataContext.IsChasePlane)
            {
                dataContext.DataItem.FixedCameraConfig = (FixedCameraConfig)addedItem;
            }
            else
            {
                var item = dataContext.DataItem.ChasePlaneCameraConfigs.FirstOrDefault(x => x.AircraftName.Equals(dataContext.FlightSimData.AircraftName, System.StringComparison.InvariantCultureIgnoreCase));

                if (item == null)
                    dataContext.DataItem.ChasePlaneCameraConfigs.Add((ChasePlaneCameraConfig)addedItem);
                else
                {
                    dataContext.DataItem.ChasePlaneCameraConfigs.Remove(item);
                    dataContext.DataItem.ChasePlaneCameraConfigs.Add((ChasePlaneCameraConfig)addedItem);
                }
            }

            dataContext.SetCamera();
        }
    }
}
