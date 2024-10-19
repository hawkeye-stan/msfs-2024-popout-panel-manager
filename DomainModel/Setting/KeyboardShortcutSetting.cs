using MSFSPopoutPanelManager.Shared;

namespace MSFSPopoutPanelManager.DomainModel.Setting
{
    public class KeyboardShortcutSetting : ObservableObject
    {
        public bool IsEnabled { get; set; } = true;

        public string PopOutKeyboardBinding { get; set; }
    }
}
