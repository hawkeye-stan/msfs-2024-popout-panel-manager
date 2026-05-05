using MSFSPopoutPanelManager.DomainModel.Profile;
using MSFSPopoutPanelManager.MainApp.ViewModel;
using MSFSPopoutPanelManager.WindowsAgent;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Reactive.Linq;
using System;

namespace MSFSPopoutPanelManager.MainApp.AppUserControl.PopOutPanelCard
{
    public partial class PanelTargetField
    {
        private bool _isChasePlane;
        private PopOutPanelSourceCardViewModel _dataContext;

        public PanelTargetField()
        {
            InitializeComponent();

            this.ComboBoxCameraSelection.ItemsSource = null;

            this.Loaded += PanelTargetField_Loaded;
        }

        private void PanelTargetField_Loaded(object sender, RoutedEventArgs e)
        {
            _dataContext = ((PopOutPanelSourceCardViewModel) this.DataContext);

            if (_dataContext == null)
                return;

            // Subscribe to the observable to update the variable
            _dataContext.IsChasePlane.Subscribe(
                value =>
                {
                    if(value)
                    {
                        _isChasePlane = true;

                        this.ComboBoxCameraSelection.ItemsSource = ChasePlaneManager.ChasePlaneCameraConfigs;
                        System.Windows.Data.BindingOperations.EnableCollectionSynchronization(ChasePlaneManager.ChasePlaneCameraConfigs, new object());

                        if(ChasePlaneManager.ChasePlaneCameraConfigs.Count > 0)
                            BindChasePlaneCameraComboBox();
                    }
                    else
                    {
                        _isChasePlane = false;
                        this.ComboBoxCameraSelection.ItemsSource = _dataContext.FixedCameraConfigs;
                        System.Windows.Data.BindingOperations.EnableCollectionSynchronization(_dataContext.FixedCameraConfigs, new object());

                        if(_dataContext.FixedCameraConfigs.Count > 0)
                            BindFixedCameraComboBox();
                    }
                }
            );

            if (_dataContext.DataItem.IsNewlyAddedPanel)
            {
                _dataContext.DataItem.IsNewlyAddedPanel = false;
                this.ComboBoxCameraSelection.Dispatcher.Invoke(() =>
                {
                    this.ComboBoxCameraSelection.SelectedIndex = 0;
                });
            }

            ChasePlaneManager.ChasePlaneCameraConfigs.CollectionChanged -= ChasePlaneCameraConfigs_CollectionChanged;
            ChasePlaneManager.ChasePlaneCameraConfigs.CollectionChanged += ChasePlaneCameraConfigs_CollectionChanged;

            _dataContext.RebindChasePlaneCamera -= DataContext_RebindChasePlaneCamera;
            _dataContext.RebindChasePlaneCamera += DataContext_RebindChasePlaneCamera;

            _dataContext.FixedCameraConfigs.CollectionChanged -= FixedCameraConfigs_CollectionChanged;
            _dataContext.FixedCameraConfigs.CollectionChanged += FixedCameraConfigs_CollectionChanged;
        }

        private void FixedCameraConfigs_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (_dataContext.FixedCameraConfigs.Count > 0)
                BindFixedCameraComboBox();
        }

        private void DataContext_RebindChasePlaneCamera(object sender, EventArgs e)
        {
            if (ChasePlaneManager.ChasePlaneCameraConfigs.Count > 0)
                BindChasePlaneCameraComboBox();
        }

        private void ChasePlaneCameraConfigs_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (ChasePlaneManager.ChasePlaneCameraConfigs.Count > 0)
                BindChasePlaneCameraComboBox();
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
        }

        private void ComboBoxCameraSelection_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_dataContext == null || _dataContext.DataItem == null)
                return;

            if (!_dataContext.DataItem.IsEditingPanel)
                return;

            if (e.AddedItems.Count <= 0) 
                return;

            var addedItem = e.AddedItems[0];

            if (!_isChasePlane)
            {
                _dataContext.DataItem.FixedCameraConfig = (FixedCameraConfig)addedItem;
            }
            else
            {
                this.ComboBoxCameraSelection.Dispatcher.Invoke(() =>
                {
                    var item = _dataContext.DataItem.ChasePlaneCameraConfigs.FirstOrDefault(x => x.AircraftName.Equals(_dataContext.FlightSimData.AircraftName, StringComparison.InvariantCultureIgnoreCase));

                    try 
                    {
                        if (item == null)
                            _dataContext.DataItem.ChasePlaneCameraConfigs.Add((ChasePlaneCameraConfig)addedItem);
                        else
                        {
                            var itemIndex = _dataContext.DataItem.ChasePlaneCameraConfigs.IndexOf(item);
                            _dataContext.DataItem.ChasePlaneCameraConfigs[itemIndex] = (ChasePlaneCameraConfig)addedItem;
                        }
                    }
                    catch(Exception ex)
                    {
                        // Ignore: {"Cannot change ObservableCollection during a CollectionChanged event."} error for now
                    }
                    finally
                    {
                    }
                });
            }

            _dataContext.SetCamera();
        }

        private void BindChasePlaneCameraComboBox()
        {
            if (ChasePlaneManager.ChasePlaneCameraConfigs.Count == 0)
                return;

            this.ComboBoxCameraSelection.Dispatcher.Invoke(() =>
            {
                this.ComboBoxCameraSelection.ItemsSource = ChasePlaneManager.ChasePlaneCameraConfigs;

                var cameraConfig = _dataContext.DataItem?.ChasePlaneCameraConfigs?.FirstOrDefault(x => x.AircraftName.Equals(_dataContext.FlightSimData.AircraftName, StringComparison.InvariantCultureIgnoreCase));

                if (cameraConfig != null)
                {
                    var index = ChasePlaneManager.ChasePlaneCameraConfigs.ToList().FindIndex(x => x.Guid == cameraConfig.Guid && x.AircraftName.Equals(_dataContext.FlightSimData.AircraftName, StringComparison.InvariantCultureIgnoreCase));
                    this.ComboBoxCameraSelection.SelectedIndex = index == -1 ? 0 : index;
                }
                else
                {
                    this.ComboBoxCameraSelection.SelectedIndex = -1;
                }
            });
        }

        private void BindFixedCameraComboBox()
        {
            if (_dataContext.FixedCameraConfigs == null || _dataContext.FixedCameraConfigs.Count == 0)
                return;

            this.ComboBoxCameraSelection.Dispatcher.Invoke(() =>
            {
                this.ComboBoxCameraSelection.ItemsSource = _dataContext.FixedCameraConfigs;

                var index = _dataContext.FixedCameraConfigs.ToList().FindIndex(x => x.Name.Equals(_dataContext.DataItem.FixedCameraConfig.Name, System.StringComparison.InvariantCultureIgnoreCase));

                if (index != -1)
                    this.ComboBoxCameraSelection.SelectedIndex = index;
                else
                {
                    this.ComboBoxCameraSelection.SelectedIndex = 0;
                    _dataContext.DataItem.IsNewlyAddedPanel = false;
                }
            });
        }
    }
}
