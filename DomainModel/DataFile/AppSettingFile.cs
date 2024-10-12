using MSFSPopoutPanelManager.DomainModel.Setting;

namespace MSFSPopoutPanelManager.DomainModel.DataFile
{
    public class AppSettingFile
    {
        public string FileVersion { get; set; } = "1.0";

        public ApplicationSetting ApplicationSetting { get; set; } = new();
    }
}
