using MaterialDesignThemes.Wpf;
using MSFSPopoutPanelManager.MainApp.AppUserControl.Dialog;
using MSFSPopoutPanelManager.Orchestration;
using Prism.Commands;
using System.Windows.Input;

namespace MSFSPopoutPanelManager.MainApp.ViewModel
{
    public class ProfileCardListViewModel : BaseViewModel
    {
        public ICommand AddProfileCommand { get; }

        public ProfileCardListViewModel(SharedStorage sharedStorage) : base(sharedStorage)
        {
            AddProfileCommand = new DelegateCommand(OnAddProfile);
        }

        private async void OnAddProfile()
        {
            var dialog = new AddProfileDialog();
            await DialogHost.Show(dialog, ROOT_DIALOG_HOST, null, dialog.ClosingEventHandler, null);
        }
    }
}
