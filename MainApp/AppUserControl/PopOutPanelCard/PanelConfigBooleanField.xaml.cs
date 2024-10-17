using Microsoft.Extensions.DependencyInjection;
using MSFSPopoutPanelManager.DomainModel.Profile;
using MSFSPopoutPanelManager.MainApp.ViewModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;

namespace MSFSPopoutPanelManager.MainApp.AppUserControl.PopOutPanelCard
{
    /// <summary>
    /// Interaction logic for PanelConfigBooleanField.xaml
    /// </summary>
    public partial class PanelConfigBooleanField : UserControl
    {
        private readonly PanelConfigFieldViewModel _viewModel;

        public static readonly DependencyProperty DataItemProperty = DependencyProperty.Register(nameof(DataItem), typeof(PanelConfig), typeof(PanelConfigField));
        public static readonly DependencyProperty BindingPathProperty = DependencyProperty.Register(nameof(BindingPath), typeof(string), typeof(PanelConfigField));
        //public static readonly RoutedEvent SourceUpdatedEvent = EventManager.RegisterRoutedEvent("SourceUpdatedEvent", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(PanelConfigField));

        public PanelConfigBooleanField()
        {
            if (DesignerProperties.GetIsInDesignMode(new DependencyObject()))
            {
                InitializeComponent();
                return;
            }

            _viewModel = App.AppHost.Services.GetRequiredService<PanelConfigFieldViewModel>();
            Loaded += (_, _) =>
            {
                _viewModel.DataItem = DataItem;
                _viewModel.BindingPath = BindingPath;
                //_viewModel.SourceUpdatedEvent = SourceUpdatedEvent;
                this.DataContext = _viewModel;
                InitializeComponent();

                var binding = new Binding($"DataItem.{BindingPath}")
                {
                    Mode = BindingMode.TwoWay,
                    NotifyOnSourceUpdated = true
                };

                TglBtn?.SetBinding(ToggleButton.IsCheckedProperty, binding);
            };
        }

        public PanelConfig DataItem
        {
            get => (PanelConfig)GetValue(DataItemProperty);
            set => SetValue(DataItemProperty, value);
        }

        public string BindingPath
        {
            get => (string)GetValue(BindingPathProperty);
            set => SetValue(BindingPathProperty, value);
        }
    }
}
