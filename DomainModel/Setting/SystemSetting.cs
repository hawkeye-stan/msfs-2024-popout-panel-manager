using MSFSPopoutPanelManager.Shared;
using System;

namespace MSFSPopoutPanelManager.DomainModel.Setting
{
    public class SystemSetting : ObservableObject
    {
        public Guid LastUsedProfileId { get; set; } = Guid.Empty;
    }
}
