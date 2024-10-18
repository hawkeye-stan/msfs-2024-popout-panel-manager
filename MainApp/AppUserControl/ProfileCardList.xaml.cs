using Microsoft.Extensions.DependencyInjection;
using MSFSPopoutPanelManager.MainApp.ViewModel;
using System.ComponentModel;
using System.Windows;

namespace MSFSPopoutPanelManager.MainApp.AppUserControl
{
    public partial class ProfileCardList
    {
        public ProfileCardList()
        {
            InitializeComponent();
            if (DesignerProperties.GetIsInDesignMode(new DependencyObject()))
                return;

            var viewModel = App.AppHost.Services.GetRequiredService<ProfileCardListViewModel>();
            Loaded += (_, _) => { DataContext = viewModel; };
        }
    }
}
