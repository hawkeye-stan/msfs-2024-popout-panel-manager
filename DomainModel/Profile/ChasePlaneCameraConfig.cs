using MSFSPopoutPanelManager.Shared;

namespace MSFSPopoutPanelManager.DomainModel.Profile
{
    public class ChasePlaneCameraConfig : ObservableObject
    {
        public string Name { get; set; }

        public string Guid { get; set; }

        public string AircraftName { get; set; }
    }
}
