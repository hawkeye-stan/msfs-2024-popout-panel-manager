using System.Collections.Generic;

namespace MSFSPopoutPanelManager.SimConnectAgent
{
    public class SimDataDefinitions
    {
        public static List<SimConnectDataDefinition> GetRequiredDefinitions()
        {
            var definitions = new List<SimConnectDataDefinition>
            {
                new() { DefinitionId = DataDefinition.REQUIRED_DEFINITION, RequestId = DataRequest.REQUIRED_REQUEST, DataDefinitionType = DataDefinitionType.SimConnect, PropName = PropName.TrackIREnable, VariableName = "TRACK IR ENABLE", SimConnectUnit = "Bool", DataType = DataType.Float64 },
                new() { DefinitionId = DataDefinition.REQUIRED_DEFINITION, RequestId = DataRequest.REQUIRED_REQUEST, DataDefinitionType = DataDefinitionType.SimConnect, PropName = PropName.CameraState, VariableName = "CAMERA STATE", SimConnectUnit = "Number", DataType = DataType.Float64 },
                new() { DefinitionId = DataDefinition.REQUIRED_DEFINITION, RequestId = DataRequest.REQUIRED_REQUEST, DataDefinitionType = DataDefinitionType.SimConnect, PropName = PropName.CameraViewTypeAndIndex0, VariableName = "CAMERA VIEW TYPE AND INDEX:0", SimConnectUnit = "Enum", DataType = DataType.Float64 },
                new() { DefinitionId = DataDefinition.REQUIRED_DEFINITION, RequestId = DataRequest.REQUIRED_REQUEST, DataDefinitionType = DataDefinitionType.SimConnect, PropName = PropName.CameraViewTypeAndIndex1, VariableName = "CAMERA VIEW TYPE AND INDEX:1", SimConnectUnit = "Enum", DataType = DataType.Float64 },
                new() { DefinitionId = DataDefinition.REQUIRED_DEFINITION, RequestId = DataRequest.REQUIRED_REQUEST, DataDefinitionType = DataDefinitionType.SimConnect, PropName = PropName.CameraViewTypeAndIndex1Max, VariableName = "CAMERA VIEW TYPE AND INDEX MAX:1", SimConnectUnit = "Number", DataType = DataType.Float64 },
                new() { DefinitionId = DataDefinition.REQUIRED_DEFINITION, RequestId = DataRequest.REQUIRED_REQUEST, DataDefinitionType = DataDefinitionType.SimConnect, PropName = PropName.CameraViewTypeAndIndex2Max, VariableName = "CAMERA VIEW TYPE AND INDEX MAX:2", SimConnectUnit = "Number", DataType = DataType.Float64 },

            };
            return definitions;
        }
        
        public static class PropName
        {
            public static string TrackIREnable = "TrackIREnable";
            public static string CameraState = "CameraState";
            public static string CameraViewTypeAndIndex0 = "CameraViewTypeAndIndex0";
            public static string CameraViewTypeAndIndex1 = "CameraViewTypeAndIndex1";
            public static string CameraViewTypeAndIndex1Max = "CameraViewTypeAndIndex1Max";
            public static string CameraViewTypeAndIndex2Max = "CameraViewTypeAndIndex2Max";
        }

        public enum WritableVariableName
        {
            TrackIREnable,
            CameraState,
            CameraRequestAction,
            CameraViewTypeAndIndex0,
            CameraViewTypeAndIndex1
        }
    }
}
