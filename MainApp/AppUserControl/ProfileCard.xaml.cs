using Microsoft.Extensions.DependencyInjection;
using MSFSPopoutPanelManager.DomainModel.Profile;
using MSFSPopoutPanelManager.MainApp.ViewModel;
using System;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;

namespace MSFSPopoutPanelManager.MainApp.AppUserControl
{
    public partial class ProfileCard
    {
        private readonly ProfileCardViewModel _viewModel;

        public ProfileCard()
        {
            InitializeComponent();
            if (DesignerProperties.GetIsInDesignMode(new DependencyObject()))
                return;

            _viewModel = App.AppHost.Services.GetRequiredService<ProfileCardViewModel>();
            Loaded += (_, _) =>
            {
                DataContext = _viewModel;
                _viewModel.OnProfileSelected += (_, _) =>
                {
                    PopupBoxFinder.StaysOpen = false;
                    PopupBoxFinder.IsPopupOpen = false;
                };
            };

#if LOCAL || DEBUG
            //this.WrapPanelSwitchWindow.Visibility = Visibility.Visible;
#else
            this.WrapPanelSwitchWindow.Visibility = Visibility.Collapsed;
#endif
        }

        private void ToggleButtonEditProfileTitle_Click(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleButton { IsChecked: not null } toggleButton && (bool)toggleButton.IsChecked)
            {
                TxtBoxProfileTitle.Dispatcher.BeginInvoke(new Action(() => TxtBoxProfileTitle.Focus()));
                TxtBoxProfileTitle.Dispatcher.BeginInvoke(new Action(() => TxtBoxProfileTitle.SelectAll()));
            }
        }

        private void TxtBoxProfileTitle_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter)
                return;

            ToggleButtonEditProfileTitle.IsChecked = false;
            Keyboard.ClearFocus();
        }

        private void BtnPopupBoxFinder_Click(object sender, RoutedEventArgs e)
        {
            PopupBoxFinder.IsPopupOpen = !PopupBoxFinder.IsPopupOpen;
            PopupBoxFinder.StaysOpen = PopupBoxFinder.IsPopupOpen;

            if (PopupBoxFinder.IsPopupOpen)
            {
                ComboBoxSearchProfile.Text = null;
                ComboBoxSearchProfile.Focus();
            }
        }
    }
}
