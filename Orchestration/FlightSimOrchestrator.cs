using MSFSPopoutPanelManager.DomainModel.Profile;
using MSFSPopoutPanelManager.Shared;
using MSFSPopoutPanelManager.SimConnectAgent;
using MSFSPopoutPanelManager.WindowsAgent;
using System;
using System.Collections.Generic;
using System.Threading;

namespace MSFSPopoutPanelManager.Orchestration
{
    public class FlightSimOrchestrator : BaseOrchestrator
    {
        private const int MSFS_GAME_EXIT_DETECTION_INTERVAL = 3000;
        private System.Timers.Timer _msfsGameExitDetectionTimer;
        private SimConnectProvider _simConnectProvider;

        private bool _isTurnedOnPower;
        private bool _isTurnedOnAvionics;

        public FlightSimOrchestrator(SharedStorage sharedStorage) : base(sharedStorage)
        {
            _simConnectProvider = new SimConnectProvider();
        }

        public event EventHandler OnSimulatorExited;

        public event EventHandler OnFlightStarted;

        public event EventHandler OnFlightStopped;

        public void StartSimConnectServer()
        {
            _simConnectProvider ??= new SimConnectProvider();

            _simConnectProvider.OnConnected += (_, _) =>
            {
                FlightSimData.IsSimConnectActive = true;
                FlightSimData.IsSimulatorStarted = true;
                WindowProcessManager.GetSimulatorProcess();     // refresh simulator process
                DetectMsfsExit();
            };

            _simConnectProvider.OnDisconnected += (_, _) =>
            {
                FlightSimData.IsSimConnectDataReceived = false;
                FlightSimData.IsSimConnectActive = false;
                WindowProcessManager.GetSimulatorProcess();     // refresh simulator process
                FlightSimData.Reset();
            };

            _simConnectProvider.OnException += (_, _) =>
            {
                FlightSimData.IsSimConnectDataReceived = false;
                FlightSimData.IsSimConnectActive = false;
            };

            _simConnectProvider.OnSimConnectDataRequiredRefreshed += (_, e) =>
            {
                MapRequiredSimConnectData(e);
            };

            _simConnectProvider.OnActiveAircraftChanged += (_, e) =>
            {
                var aircraftName = string.IsNullOrEmpty(e) ? null : e;
                if (FlightSimData.AircraftName != aircraftName)
                {
                    FlightSimData.AircraftName = aircraftName;
                    ProfileData.RefreshProfile();
                }
            };

            _simConnectProvider.OnFlightStarted += HandleOnFlightStarted;
            _simConnectProvider.OnFlightStopped += HandleOnFlightStopped;
            _simConnectProvider.OnIsInCockpitChanged += (_, e) =>
            {
                FlightSimData.IsInCockpit = e;
                if (e)
                    FlightSimData.IsFlightStarted = true;
            };
            _simConnectProvider.Start();
        }

        public void EndSimConnectServer(bool appExit)
        {
            _simConnectProvider.Stop(appExit);
            _simConnectProvider = null;
        }

        public void TurnOnTrackIR()
        {
            if (_simConnectProvider == null)
                return;

            if (!AppSettingData.ApplicationSetting.TrackIRSetting.AutoDisableTrackIR)
                return;

            WorkflowStepWithMessage.Execute("Turning on TrackIR", () =>
            {
                var count = 0;
                do
                {
                    _simConnectProvider.TurnOnTrackIR();
                    Thread.Sleep(500);
                    count++;
                }
                while (!FlightSimData.TrackIRStatus && count < 5);

                return FlightSimData.TrackIRStatus;
            });
        }

        public void TurnOffTrackIR()
        {
            if (_simConnectProvider == null)
                return;

            if (!AppSettingData.ApplicationSetting.TrackIRSetting.AutoDisableTrackIR)
                return;

            WorkflowStepWithMessage.Execute("Turning off TrackIR", () =>
            {
                var count = 0;
                do
                {
                    _simConnectProvider.TurnOffTrackIR();
                    Thread.Sleep(500);
                    count++;
                }
                while (FlightSimData.TrackIRStatus && count < 5);

                return !FlightSimData.TrackIRStatus;
            });
        }

        public void TurnOnActivePause()
        {
            if (_simConnectProvider == null)
                return;

            if (!AppSettingData.ApplicationSetting.PopOutSetting.AutoActivePause)
                return;

            WorkflowStepWithMessage.Execute("Turning on active pause", () =>
            {
                _simConnectProvider.TurnOnActivePause();
            });
        }

        public void TurnOffActivePause()
        {
            if (_simConnectProvider == null)
                return;

            if (!AppSettingData.ApplicationSetting.PopOutSetting.AutoActivePause)
                return;

            WorkflowStepWithMessage.Execute("Turning off active pause", () =>
            {
                _simConnectProvider.TurnOffActivePause();
            });
        }
        
        public void ResetCameraView()
        {
            _simConnectProvider.SetCameraRequestAction(1);
        }

        public void SetFixedCamera(CameraType cameraType, int index)
        {
            if (FlightSimData.CameraState != CameraState.Cockpit)
            {
                _simConnectProvider.SetCameraState(CameraState.Cockpit);
                Thread.Sleep(250);
                ResetCameraView();
                Thread.Sleep(250);
            }

            _simConnectProvider.SetCameraViewTypeAndIndex0(Convert.ToInt32(cameraType));
            Thread.Sleep(250);
            _simConnectProvider.SetCameraViewTypeAndIndex1(index);
        }

        private void HandleOnFlightStarted(object sender, EventArgs e)
        {
            OnFlightStarted?.Invoke(this, EventArgs.Empty);

            FlightSimData.IsFlightStarted = true;
        }

        private void HandleOnFlightStopped(object sender, EventArgs e)
        {
            ProfileData.ResetActiveProfile();

            OnFlightStopped?.Invoke(this, EventArgs.Empty);

            CloseAllPopOuts();

            FlightSimData.IsFlightStarted = false;
        }

        private void DetectMsfsExit()
        {
            _msfsGameExitDetectionTimer = new System.Timers.Timer();
            _msfsGameExitDetectionTimer.Interval = MSFS_GAME_EXIT_DETECTION_INTERVAL;
            _msfsGameExitDetectionTimer.Enabled = true;
            _msfsGameExitDetectionTimer.Elapsed += (_, _) =>
            {
                WindowProcessManager.GetSimulatorProcess();
                if (WindowProcessManager.SimulatorProcess != null) 
                    return;

                if (AppSettingData.ApplicationSetting.GeneralSetting.AutoClose)
                {
                    OnSimulatorExited?.Invoke(this, EventArgs.Empty);
                    return;
                }
                
                FlightSimData.Reset();
                _simConnectProvider.StopAndReconnect();
            };
        }

        private void MapRequiredSimConnectData(List<SimDataItem> simData)
        {
            var trackIR = Convert.ToBoolean(simData.Find(d => d.PropertyName == SimDataDefinitions.PropName.TrackIREnable).Value);
            if (trackIR != FlightSimData.TrackIRStatus)
                FlightSimData.TrackIRStatus = trackIR;

            var cameraStateInt = Convert.ToInt32(simData.Find(d => d.PropertyName == SimDataDefinitions.PropName.CameraState).Value);
            var result = Enum.TryParse<CameraState>(cameraStateInt.ToString(), out var cameraState);
            if (!result)
                cameraState = CameraState.Unknown;
            if (cameraState != FlightSimData.CameraState)
                FlightSimData.CameraState = cameraState;

            var cameraViewTypeAndIndex0 = Convert.ToInt32(simData.Find(d => d.PropertyName == SimDataDefinitions.PropName.CameraViewTypeAndIndex0).Value);
            if (cameraViewTypeAndIndex0 != FlightSimData.CameraViewTypeAndIndex0)
                FlightSimData.CameraViewTypeAndIndex0 = cameraViewTypeAndIndex0;

            var cameraViewTypeAndIndex1 = Convert.ToInt32(simData.Find(d => d.PropertyName == SimDataDefinitions.PropName.CameraViewTypeAndIndex1).Value);
            if (cameraViewTypeAndIndex1 != FlightSimData.CameraViewTypeAndIndex1)
                FlightSimData.CameraViewTypeAndIndex1 = cameraViewTypeAndIndex1;

            var cameraViewTypeAndIndex1Max = Convert.ToInt32(simData.Find(d => d.PropertyName == SimDataDefinitions.PropName.CameraViewTypeAndIndex1Max).Value);
            if (cameraViewTypeAndIndex1Max != FlightSimData.CameraViewTypeAndIndex1Max)
                FlightSimData.CameraViewTypeAndIndex1Max = cameraViewTypeAndIndex1Max;

            var cameraViewTypeAndIndex2Max = Convert.ToInt32(simData.Find(d => d.PropertyName == SimDataDefinitions.PropName.CameraViewTypeAndIndex2Max).Value);
            if (cameraViewTypeAndIndex2Max != FlightSimData.CameraViewTypeAndIndex2Max)
                FlightSimData.CameraViewTypeAndIndex2Max = cameraViewTypeAndIndex2Max;

            FlightSimData.IsSimConnectDataReceived = true;
        }

        private bool CompareSimConnectData(List<SimDataItem> simData, string propName, double source, out double newValue)
        {
            var propData = simData.Find(d => d.PropertyName == propName);

            if (propData == null)
            {
                newValue = 0;
                return false;
            }

            var value = Convert.ToDouble(simData.Find(d => d.PropertyName == propName).Value);
            if (Math.Abs(value - source) > 0.000000000000001)
            {
                newValue = value;
                return true;
            }

            newValue = 0;
            return false;
        }          
    }
}
