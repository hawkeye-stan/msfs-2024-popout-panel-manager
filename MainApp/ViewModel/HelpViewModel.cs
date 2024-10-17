using MSFSPopoutPanelManager.Orchestration;
using MSFSPopoutPanelManager.WindowsAgent;
using Prism.Commands;

namespace MSFSPopoutPanelManager.MainApp.ViewModel
{
    public class HelpViewModel : BaseViewModel
    {
        private readonly HelpOrchestrator _helpOrchestrator;

        public DelegateCommand<string> HyperLinkCommand { get; private set; }

        public string ApplicationVersion { get; private set; }

        public HelpViewModel(SharedStorage sharedStorage, HelpOrchestrator helpOrchestrator) : base(sharedStorage)
        {
            _helpOrchestrator = helpOrchestrator;

            HyperLinkCommand = new DelegateCommand<string>(OnHyperLinkActivated);

#if DEBUG
            var buildConfig = " (Debug)";
#elif LOCAL
            var buildConfig = " (Local)";
#else
            var buildConfig = string.Empty;
#endif
            ApplicationVersion = $"{WindowProcessManager.GetApplicationVersion()}{buildConfig}";
        }

        private void OnHyperLinkActivated(string commandParameter)
        {
            switch (commandParameter)
            {
                case "Getting Started":
                    _helpOrchestrator.OpenGettingStarted();
                    break;
                case "User Guide":
                    _helpOrchestrator.OpenUserGuide();
                    break;
                case "License":
                    _helpOrchestrator.OpenLicense();
                    break;
                case "Version Info":
                    _helpOrchestrator.OpenVersionInfo();
                    break;
                case "Download VCC Library":
                    _helpOrchestrator.DownloadVccLibrary();
                    break;
            }
        }
    }
}
