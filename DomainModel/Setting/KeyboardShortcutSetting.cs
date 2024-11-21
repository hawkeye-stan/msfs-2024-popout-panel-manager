using MSFSPopoutPanelManager.Shared;

namespace MSFSPopoutPanelManager.DomainModel.Setting
{
    public class KeyboardShortcutSetting : ObservableObject
    {
        public bool IsEnabled { get; set; } = false;

        public string PopOutKeyboardBinding { get; set; }
    }
}
