using MSFSPopoutPanelManager.Shared;

namespace MSFSPopoutPanelManager.DomainModel.Setting
{
    public class StreamingSetting : ObservableObject
    {
        public bool IsEnabled { get; set; } = false;
        public int Port { get; set; } = 8080;
        public string HostOverride { get; set; } = "";
    }
}
