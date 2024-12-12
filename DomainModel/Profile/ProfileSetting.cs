using MSFSPopoutPanelManager.Shared;

namespace MSFSPopoutPanelManager.DomainModel.Profile
{
    public class ProfileSetting : ObservableObject
    {
        public ProfileSetting()
        {
            InitializeChildPropertyChangeBinding();
        }

        public bool IncludeInGamePanels { get; set; }

        public RefocusOnDisplay RefocusOnDisplay { get; set; } = new();

        public NumPadConfig NumPadConfig { get; set; } = new();

        public SwitchWindowConfig SwitchWindowConfig { get; set; } = new();
    }
}
