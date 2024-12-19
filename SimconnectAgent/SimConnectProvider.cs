using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using static MSFSPopoutPanelManager.SimConnectAgent.SimDataDefinitions;

namespace MSFSPopoutPanelManager.SimConnectAgent
{
    public class SimConnectProvider
    {
        private const int MSFS_DATA_REFRESH_TIMEOUT = 500;

        private readonly SimConnector _simConnector;

        private bool _isHandlingCriticalError;
        private List<SimDataItem> _requiredSimData;

        private System.Timers.Timer _requiredRequestDataTimer;
        private bool _isPowerOnForPopOut;
        private bool _isAvionicsOnForPopOut;
        private bool _isTrackIRManaged;

        public event EventHandler OnConnected;
        public event EventHandler OnDisconnected;
        public event EventHandler<bool> OnIsInCockpitChanged;
        public event EventHandler OnFlightStarted;
        public event EventHandler OnFlightStopped;
        public event EventHandler OnException;
        public event EventHandler<List<SimDataItem>> OnSimConnectDataRequiredRefreshed;
        public event EventHandler<int> OnSimConnectDataEventFrameRefreshed;
        public event EventHandler<string> OnActiveAircraftChanged;

        public SimConnectProvider()
        {
            _simConnector = new SimConnector();
            _simConnector.OnConnected += HandleSimConnected;
            _simConnector.OnDisconnected += HandleSimDisconnected;
            _simConnector.OnException += HandleSimException;
            _simConnector.OnReceiveSystemEvent += HandleReceiveSystemEvent;
            _simConnector.OnReceivedRequiredData += HandleRequiredDataReceived;
            _simConnector.OnReceivedEventFrameData += (_, e) => OnSimConnectDataEventFrameRefreshed?.Invoke(this, e);
            _simConnector.OnActiveAircraftChanged += (_, e) => OnActiveAircraftChanged?.Invoke(this, e);

            _isHandlingCriticalError = false;
        }

        public void Start()
        {
            _simConnector.Stop();
            Thread.Sleep(2000);     // wait for everything to stop
            _simConnector.Start();
        }

        public void Stop(bool appExit)
        {
            _simConnector.Stop();

            if (!appExit)
                OnDisconnected?.Invoke(this, EventArgs.Empty);
        }

        public void StopAndReconnect()
        {
            _simConnector.Stop();
            Thread.Sleep(2000);     // wait for everything to stop
            _simConnector.Restart();
        }

        public void TurnOnTrackIR()
        {
            if (_requiredSimData == null)
                return;

            if (!_isTrackIRManaged)
                return;

            Debug.WriteLine("Turn On TrackIR...");
            SetTrackIREnable(true);
            _isTrackIRManaged = false;
        }

        public void TurnOffTrackIR()
        {
            if (_requiredSimData == null) 
                return;

            var trackIREnable = Convert.ToBoolean(_requiredSimData.Find(d => d.PropertyName == PropName.TrackIREnable).Value);

            if (!trackIREnable) 
                return;

            Debug.WriteLine("Turn Off TrackIR...");

            SetTrackIREnable(false);
            _isTrackIRManaged = true;
            Thread.Sleep(200);
        }

        public void TurnOnActivePause()
        {
            Debug.WriteLine("Active Pause On...");

            _simConnector.TransmitActionEvent(ActionEvent.PAUSE_SET, 1);
        }

        public void TurnOffActivePause()
        {
            Debug.WriteLine("Active Pause Off...");

            _simConnector.TransmitActionEvent(ActionEvent.PAUSE_SET, 0);
        }
        
        public void SetCameraState(CameraState cameraState)
        {
            _simConnector.SetDataObject(WritableVariableName.CameraState, Convert.ToDouble(cameraState));
        }
        
        public void SetCameraRequestAction(int actionEnum)
        {
            _simConnector.SetDataObject(WritableVariableName.CameraRequestAction, Convert.ToDouble(actionEnum));
        }

        public void SetCameraViewTypeAndIndex0(int actionEnum)
        {
            _simConnector.SetDataObject(WritableVariableName.CameraViewTypeAndIndex0, Convert.ToDouble(actionEnum));
        }

        public void SetCameraViewTypeAndIndex1(int actionEnum)
        {
            _simConnector.SetDataObject(WritableVariableName.CameraViewTypeAndIndex1, Convert.ToDouble(actionEnum));
        }

        private void SetTrackIREnable(bool enable)
        {
            _simConnector.SetDataObject(WritableVariableName.TrackIREnable, enable ? Convert.ToDouble(1) : Convert.ToDouble(0));
        }

        private void HandleSimConnected(object source, EventArgs e)
        {
            // Setup required data request timer
            _requiredRequestDataTimer = new()
            {
                Interval = MSFS_DATA_REFRESH_TIMEOUT
            };
            _requiredRequestDataTimer.Start();
            _requiredRequestDataTimer.Elapsed += (_, _) =>
            {
                try
                {
                    _simConnector.RequestRequiredData(); 
                    _simConnector.ReceiveMessage();
                }
                catch
                {
                    // ignored
                }
            };

            OnConnected?.Invoke(this, EventArgs.Empty);
        }

        private void HandleSimDisconnected(object source, EventArgs e)
        {
            _requiredRequestDataTimer.Stop();
            OnDisconnected?.Invoke(this, EventArgs.Empty);
            StopAndReconnect();
        }

        private void HandleSimException(object source, string e)
        {
            OnException?.Invoke(this, EventArgs.Empty);

            _requiredRequestDataTimer.Stop();

            if (!_isHandlingCriticalError)
            {
                _isHandlingCriticalError = true;     // Prevent restarting to occur in parallel
                StopAndReconnect();
                _isHandlingCriticalError = false;
            }
        }

        private void HandleRequiredDataReceived(object sender, List<SimDataItem> e)
        {
            _requiredSimData = e;
            DetectFlightStartedOrStopped(e);
            OnSimConnectDataRequiredRefreshed?.Invoke(this, e);
        }

        private CameraState _currentCameraState = CameraState.Unknown;

        private void DetectFlightStartedOrStopped(List<SimDataItem> simData)
        {
            // Determine is flight started or ended
            var cameraStateInt = Convert.ToInt32(simData.Find(d => d.PropertyName == PropName.CameraState).Value);

            var success = Enum.TryParse<CameraState>(cameraStateInt.ToString(), out var cameraState);
            if(!success)
                cameraState = CameraState.Unknown;

            if (_currentCameraState == cameraState)
                return;

            if (cameraState == CameraState.Cockpit)
                OnIsInCockpitChanged?.Invoke(this, true);

            Debug.WriteLine($"Current State: {_currentCameraState} - Camera State: {cameraState}");

            switch (_currentCameraState)
            {
                case CameraState.ReadyToFlyScreen:
                case CameraState.PreloadScreen:
                case CameraState.HomeScreen:
                    if (cameraState == CameraState.Cockpit)
                    {
                        _currentCameraState = cameraState;
                        OnFlightStarted?.Invoke(this, EventArgs.Empty);
                    }
                    break;
                case CameraState.RestartScreen:
                    if (cameraState == CameraState.PreloadScreen || cameraState == CameraState.HomeScreen)
                    {
                        _currentCameraState = cameraState;
                        OnFlightStopped?.Invoke(this, EventArgs.Empty);
                        OnIsInCockpitChanged?.Invoke(this, false);
                    }
                    break;
                case CameraState.Cockpit:
                    if (cameraState == CameraState.HomeScreen)
                    {
                        _currentCameraState = cameraState;
                        OnFlightStopped?.Invoke(this, EventArgs.Empty);
                        OnIsInCockpitChanged?.Invoke(this, false);
                    }
                    break;
            }

            if (cameraState is CameraState.Cockpit or CameraState.HomeScreen or CameraState.RestartScreen or CameraState.ReadyToFlyScreen or CameraState.PreloadScreen)
                _currentCameraState = cameraState;
        }

        private void HandleReceiveSystemEvent(object sender, SimConnectEvent e)
        {
            // TBD
        }
    }
}
