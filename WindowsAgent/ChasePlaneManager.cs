using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MSFSPopoutPanelManager.DomainModel.Profile;
using MSFSPopoutPanelManager.Shared;
using Newtonsoft.Json;

namespace MSFSPopoutPanelManager.WindowsAgent
{
    public class ChasePlaneManager
    {
        private static ClientWebSocket _clientWebSocket;

        public static List<ChasePlaneView> ChasePlaneViews { get; private set; }

        public static ObservableRangeCollection<ChasePlaneCameraConfig> ChasePlaneCameraConfigs { get; private set; } = new();

        public static AutoResetEvent IsChasePlaneViewsReady = new AutoResetEvent(false);
        public static event EventHandler<EventArgs> CameraViewsReady;

        public static event EventHandler<EventArgs> ApiConnecting;

        public static event EventHandler<EventArgs> ApiConnected;

        public static event EventHandler<EventArgs> ApiConnectionFailed;

        public static event EventHandler<EventArgs> ApiDisconnected;

        public static event EventHandler<EventArgs> ApiGeneralFailed;

        private static bool _isShownStatus;

        private static bool _isInitialized;

        private static bool _isViewsReady;

        private static bool _isApiConnectionStarted;

        private static Task _msgListenerTask;

        public static async Task<bool> Run(bool isShownStatus)
        {
            _isShownStatus = isShownStatus;

            if (_clientWebSocket != null && _clientWebSocket.State == WebSocketState.Open)
            {
                _isApiConnectionStarted = true;
                IsChasePlaneViewsReady.Set();
                return true;
            }

            ChasePlaneCameraConfigs.Clear();

            if (!_isApiConnectionStarted)
            {
                _isApiConnectionStarted = true;

                if(await ConnectWebSocket())
                {
                    _msgListenerTask = Task.Run(() => ListenInBackground(_clientWebSocket));
                    return true;
                }
                else
                {
                    _isApiConnectionStarted = false;

                    if (_isShownStatus)
                    {
                        StatusMessageWriter.WriteFailureStatusMessage();
                        StatusMessageWriter.IsEnabled = false;
                        ApiConnectionFailed?.Invoke(null, null);
                    }
                   
                    return false;
                }
            }

            return true;
        }

        public static async Task SetCamera(string cameraViewName, string guid)
        {
            var view = ChasePlaneViews?.FirstOrDefault(v => v.Name == cameraViewName && v.Guid.Equals(guid, StringComparison.InvariantCultureIgnoreCase));

            if (view != null)
                await SendMessage(_clientWebSocket, new ChasePlaneCamSetPositionMessage { Message = "cam_set_position", Payload = view });


            Thread.Sleep(250);
        }

        public static async Task SetDefaultCamera()
        {
            await SendMessage(_clientWebSocket, new ChasePlaneMessage { Message = "cam_load_default" });
        }

        public static async Task Disconnect()
        {
            await DisconnectWebSocket();
        }

        private static async Task<bool> ConnectWebSocket()
        {
            _clientWebSocket = new ClientWebSocket();
            _isInitialized = false;
            _isViewsReady = false;

            Debug.WriteLine("Connect to ChasePlane and initialize API");

            if (_isShownStatus)
            {
                StatusMessageWriter.IsEnabled = true;
                StatusMessageWriter.ClearMessage();
                StatusMessageWriter.WriteMessage("Connecting to ChasePlane API", StatusMessageType.Info);
                StatusMessageWriter.WriteExecutingStatusMessage();
                ApiConnecting?.Invoke(null, null);
            }

            Task timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
            Task webSocketConnectionTask = _clientWebSocket.ConnectAsync(new Uri("ws://localhost:8652"), CancellationToken.None);
            await Task.WhenAny(webSocketConnectionTask, timeoutTask);

            if (_clientWebSocket != null && _clientWebSocket.State == WebSocketState.Open)
            {
                await SendMessage(_clientWebSocket, new ChasePlaneMessage
                {
                    Message = "api_connect",
                    Payload = new ChasePlaneMessagePayload { ClientName = "POPM" }
                });

                return true;
            }
            else
            {
                _clientWebSocket = null;
                return false;
            }
        }

        private async static Task ListenInBackground(ClientWebSocket ws)
        {
            var buffer = new byte[1024 * 32];

            try
            {
                while (ws.State == WebSocketState.Open)
                {
                    var result = await _clientWebSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

                    if (result.MessageType == WebSocketMessageType.Close)
                        break;

                    var message = Encoding.UTF8.GetString(buffer, 0, result.Count);

                    if (!IsJson(message))
                        continue;

                    var chasePlaneMessage = JsonConvert.DeserializeObject<ChasePlaneMessage>(message);

                    if (chasePlaneMessage != null && !String.IsNullOrEmpty(chasePlaneMessage.Message))
                    {
                        switch (chasePlaneMessage.Message.ToLower())
                        {
                            case "api_version":
                                Debug.WriteLine(message);
                                break;
                            case "cam_mode_set":
                                Debug.WriteLine(message);
                                var camModeSetMsg = JsonConvert.DeserializeObject<ChasePlaneMessage>(message);

                                if(!_isViewsReady && camModeSetMsg.Payload.ViewsLoaded)
                                {
                                    _isViewsReady = true;
                                    GetCameraViews();
                                }    
                                break;
                            case "initialized":
                                if (_isInitialized)
                                    continue;

                                Debug.WriteLine(message);

                                _isInitialized = true;

                                if (_isShownStatus)
                                {
                                    ApiConnected?.Invoke(null, null);

                                    StatusMessageWriter.WriteOkStatusMessage();
                                    Thread.Sleep(1000);
                                    StatusMessageWriter.WriteMessage("Loading ChasePlane camera views", StatusMessageType.Info);
                                    StatusMessageWriter.WriteExecutingStatusMessage();
                                }

                                break;
                            case "api_reply":
                                if (chasePlaneMessage.Payload.Message == "get_views")
                                {
                                    Debug.WriteLine(message);
                                    var viewMessage = JsonConvert.DeserializeObject<ChasePlaneMessage>(message);

                                    var currentAircraftName = viewMessage.Payload.Payload.MetaData.Aircraft;

                                    var chasePlaneCameraConfigs = new List<ChasePlaneCameraConfig>();
                                    var chasePlaneViews = viewMessage.Payload.Payload.ChasePlaneViews.FindAll(v => (v.ProfileTheme == "ONBOARD_PIC" || v.ProfileTheme == "ONBOARD_SYSTEMS") && v.Aircraft.Equals(currentAircraftName, StringComparison.InvariantCultureIgnoreCase));
                                    
                                    if (chasePlaneViews != null && chasePlaneViews.Count > 0)
                                    {
                                        foreach (var chasePlaneView in chasePlaneViews)
                                        {
                                            var item = new ChasePlaneCameraConfig
                                            {
                                                Name = chasePlaneView.Name,
                                                Guid = chasePlaneView.Guid,
                                                AircraftName = currentAircraftName
                                            };
                                            chasePlaneCameraConfigs.Add(item);
                                        }

                                        ChasePlaneViews = chasePlaneViews;
                                        ChasePlaneCameraConfigs.AddRange(chasePlaneCameraConfigs);

                                        Thread.Sleep(1000);
                                        IsChasePlaneViewsReady.Set();
                                        IsChasePlaneViewsReady.Reset();

                                        if (_isShownStatus)
                                        {
                                            StatusMessageWriter.WriteOkStatusMessage();
                                            StatusMessageWriter.IsEnabled = false;

                                            CameraViewsReady?.Invoke(null, null);
                                        }
                                    }
                                    else
                                    {
                                        Debug.WriteLine("Retry getting views");
                                        Thread.Sleep(1000);
                                        await SendMessage(_clientWebSocket, new ChasePlaneMessage
                                        {
                                            Message = "api_request",
                                            RequestId = "get_views_" + DateTimeOffset.Now.ToUnixTimeMilliseconds(),
                                            Command = "get_views"
                                        });
                                    }
                                }
                               
                                break;
                        }
                    }
                }
            }
            catch (WebSocketException ex)
            {
                if (ex.WebSocketErrorCode != WebSocketError.InvalidState)
                    FileLogger.WriteException("ChasePlane 2024 Exception", ex);

                await DisconnectWebSocket();
            }
            catch (Exception ex)
            {
                FileLogger.WriteException("ChasePlane 2024 Exception", ex);
                await DisconnectWebSocket();
            }
        }

        private static async Task DisconnectWebSocket()
        {
            try
            {
                Debug.WriteLine("Disconnecting ChasePlane API");
                if (_clientWebSocket != null && _clientWebSocket.State == WebSocketState.Open)
                    await _clientWebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None);

                if (_isShownStatus)
                {
                    StatusMessageWriter.WriteFailureStatusMessage();
                    StatusMessageWriter.IsEnabled = false;
                    ApiGeneralFailed?.Invoke(null, null);
                }
            }
            catch
            {
            }
            finally
            {
                _isApiConnectionStarted = false;
                _isInitialized = false;
                _isViewsReady = false;
                _clientWebSocket = null;
                _msgListenerTask = null;
            }
        }

        private static async Task GetCameraViews()
        {
            if (_clientWebSocket != null && _clientWebSocket.State == WebSocketState.Open)
            {
                await SendMessage(_clientWebSocket, new ChasePlaneMessage
                {
                    Message = "api_request",
                    RequestId = "get_views_" + DateTimeOffset.Now.ToUnixTimeMilliseconds(),
                    Command = "get_views"
                });
            }
        }

        private static bool IsJson(string source)
        {
            if (string.IsNullOrEmpty(source))
            {
                return false;
            }

            try
            {
                // Attempt to parse the string into a JsonDocument
                using (JsonDocument document = JsonDocument.Parse(source))
                {
                    return true; // Successfully parsed
                }
            }
            catch
            {
                // An exception means the string is not valid JSON
                return false;
            }
        }

        private static async Task SendMessage(ClientWebSocket clientWebSocket, object chasePlaneMessage)
        {
            var message = JsonConvert.SerializeObject(chasePlaneMessage, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });

            Debug.WriteLine("Sending message: " + message);

            var bytes = Encoding.UTF8.GetBytes(message);
            var arraySegment = new ArraySegment<byte>(bytes, 0, bytes.Length);
            if (clientWebSocket != null && clientWebSocket.State == WebSocketState.Open)
            {
                await clientWebSocket.SendAsync(arraySegment, WebSocketMessageType.Text, true, CancellationToken.None);
            }
        }
    }
    
    public class CameraViewReadyEventArgs : EventArgs
    {
        public List<ChasePlaneCameraConfig> CameraConfigs { get; }

        public List<ChasePlaneView> ChasePlaneViewsPayload { get; }

        public CameraViewReadyEventArgs(List<ChasePlaneView> chasePlaneViewsPayload, List<ChasePlaneCameraConfig> cameraConfigs)
        {
            ChasePlaneViewsPayload = chasePlaneViewsPayload;
            CameraConfigs = cameraConfigs;
        }
    }


    public class ChasePlaneMessage
    {
        [JsonProperty("message")]
        public string Message;

        [JsonProperty("Status")]
        public string Status;

        [JsonProperty("request_id")]
        public string RequestId;

        [JsonProperty("command")]
        public string Command;

        [JsonProperty("payload")]
        public ChasePlaneMessagePayload Payload;
    }

    public class ChasePlaneMessagePayload
    {
        [JsonProperty("message")]
        public string Message;

        [JsonProperty("Status")]
        public string Status;

        [JsonProperty("request_id")]
        public string RequestId;

        [JsonProperty("client_name")]
        public string ClientName;

        [JsonProperty("views_loaded")]
        public bool ViewsLoaded;

        [JsonProperty("payload")]
        public ChasePlaneApiGetViewReplyPayload Payload;

        public class ChasePlaneApiGetViewReplyPayload
        {
            [JsonProperty("metadata")]
            public ChasePlaneViewMeta MetaData;

            [JsonProperty("views")]
            public List<ChasePlaneView> ChasePlaneViews;
        }
    }

    public class ChasePlaneViewMeta
    {
        [JsonProperty("aircraft_readable")]
        public string Aircraft;
    }

    public class ChasePlaneView
    {
        [JsonProperty("version")]
        public string Version;

        [JsonProperty("name")]
        public string Name;

        [JsonProperty("guid")]
        public string Guid;

        [JsonProperty("mode")]
        public int Mode;

        [JsonProperty("index")]
        public int Index;

        [JsonProperty("aircraft_readable")]
        public string Aircraft;

        [JsonProperty("profile_physics_type")]
        public string ProfilePhysicsType = "HUMAN";

        [JsonProperty("profile_theme")]
        public string ProfileTheme = "ONBOARD_PIC";

        [JsonProperty("can_transition")]
        public bool CanTransition = false;

        [JsonProperty("transition_time")]
        public int TransitionTime = 0;

        [JsonProperty("transition_easing")]
        public string TransitionEasing = "LINEAR";

        [JsonProperty("position")]
        public ChasePlaneViewPosition ChasePlaneViewPosition;
    }

    public class ChasePlaneViewPosition
    {
        [JsonProperty("x")]
        public double X;

        [JsonProperty("y")]
        public double Y;

        [JsonProperty("z")]
        public double Z;

        [JsonProperty("pitch")]
        public double Pitch;

        [JsonProperty("yaw")]
        public double Yaw;

        [JsonProperty("roll")]
        public double Roll;

        [JsonProperty("zoom")]
        public double Zoom;
    }

    public class ChasePlaneCamSetPositionMessage
    {
        [JsonProperty("message")]
        public string Message = "cam_set_position";

        [JsonProperty("payload")]
        public ChasePlaneView Payload;
    }
}
