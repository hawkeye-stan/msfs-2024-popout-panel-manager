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

        public static List<ChasePlaneView> ChasePlaneViews { get; set; }

        public static bool IsConnected { get; set; } = false;

        public static ObservableRangeCollection<ChasePlaneCameraConfig> ChasePlaneCameraConfigs { get; private set; } = new();

        public static event EventHandler<EventArgs> ApiConnected;

        public static event EventHandler<EventArgs> ApiDisconnected;

        public static event EventHandler<EventArgs> CameraSet;

        public static event EventHandler<CameraViewReadyEventArgs> CameraViewsReady;


        public static async Task Connect()
        {
            if(_clientWebSocket == null || _clientWebSocket.State != WebSocketState.Open)
                _clientWebSocket = new ClientWebSocket();

            ChasePlaneCameraConfigs.Clear();

            await _clientWebSocket.ConnectAsync(new Uri("ws://localhost:8652"), CancellationToken.None);

            try
            {
                var messageReceiver = new MessageReceiver(_clientWebSocket);
                messageReceiver.ApiConnected += (_, e) =>
                {
                    Debug.WriteLine("Api Connected...");
                    IsConnected = true;
                };
                messageReceiver.CameraSet += (_, e) =>
                {
                    Debug.WriteLine("Camera Set...");
                };
                messageReceiver.CameraViewsReady += (_, e) =>
                {
                    Debug.WriteLine("Views readied...");

                    ChasePlaneViews = e.ChasePlaneViewsPayload;

                    CameraViewsReady?.Invoke(null, new CameraViewReadyEventArgs(null, e.CameraConfigs));
                };

                await MessageSender.SendMessage(_clientWebSocket, new ChasePlaneMessage
                {
                    Message = "api_connect",
                    Payload = new ChasePlaneMessagePayload { ClientName = "POPM" }
                });

                await messageReceiver.ReceivedMessages();
            }
            catch (WebSocketException ex)
            {
                if(ex.WebSocketErrorCode != WebSocketError.InvalidState)
                    FileLogger.WriteException("ChasePlane 2024 Exception", ex);
            }
            catch(Exception ex)
            {
                FileLogger.WriteException("ChasePlane 2024 Exception", ex);
            }
            finally
            {
                await Disconnect();
            }
        }

        public static async Task Disconnect()
        {
            if(_clientWebSocket != null && _clientWebSocket.State == WebSocketState.Open) 
                await _clientWebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None);

            _clientWebSocket = null;
            IsConnected = false;
        }

        public static async Task GetCameraViews()
        {
            if (_clientWebSocket != null && _clientWebSocket.State == WebSocketState.Open)
                await MessageSender.SendMessage(_clientWebSocket, new ChasePlaneMessage
                {
                    Message = "api_request",
                    RequestId = "get_views_" + DateTimeOffset.Now.ToUnixTimeMilliseconds(),
                    Command = "get_views"
                });
        }

        public static async Task SetCamera(string cameraViewName, string guid)
        {
            var view = ChasePlaneViews?.FirstOrDefault(v => v.Name == cameraViewName && v.Guid.Equals(guid, StringComparison.InvariantCultureIgnoreCase));

            if(view != null)
                await MessageSender.SendMessage(_clientWebSocket, new ChasePlaneCamSetPositionMessage { Message = "cam_set_position", Payload = view });

            Thread.Sleep(250);
        }

        public static async Task SetDefaultCamera()
        {
            await MessageSender.SendMessage(_clientWebSocket, new ChasePlaneMessage { Message = "cam_load_default" });
        }
    }

    public class MessageReceiver
    {
        public event EventHandler<EventArgs> ApiConnected;

        public event EventHandler<EventArgs> ApiDisconnected;

        public event EventHandler<EventArgs> ApiInnitialized;

        public event EventHandler<EventArgs> CameraSet;

        public event EventHandler<CameraViewReadyEventArgs> CameraViewsReady;

        public ClientWebSocket _clientWebSocket;

        public MessageReceiver(ClientWebSocket clientWebSocket)
        {
            _clientWebSocket = clientWebSocket;
        }

        protected virtual void OnCameraViewsReady(CameraViewReadyEventArgs e)
        {
            // Null check and invocation
            CameraViewsReady?.Invoke(this, e);
        }

        public async Task ReceivedMessages()
        {
            var buffer = new byte[1024 * 32];
            while (true)
            {
                var result = await _clientWebSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

                if (result.MessageType == WebSocketMessageType.Close)
                    break;

                var message = Encoding.UTF8.GetString(buffer, 0, result.Count);

                Debug.WriteLine(message);

                if (!IsJson(message))
                    continue;

                var chasePlaneMessage = JsonConvert.DeserializeObject<ChasePlaneMessage>(message);

                if (chasePlaneMessage != null && !String.IsNullOrEmpty(chasePlaneMessage.Message))
                {
                    switch (chasePlaneMessage.Message.ToLower())
                    {
                        case "cam_mode_set":
                            CameraSet?.Invoke(this, null);
                            break;
                        case "initialized":
                            ApiConnected?.Invoke(this, null);

                            // Get camera views
                            await MessageSender.SendMessage(_clientWebSocket, new ChasePlaneMessage
                            {
                                Message = "api_request",
                                RequestId = "get_views_" + DateTimeOffset.Now.ToUnixTimeMilliseconds(),
                                Command = "get_views"
                            });

                            break;
                        case "api_reply":
                            if (chasePlaneMessage.Payload.Message == "get_views")
                            {
                                var viewMessage = JsonConvert.DeserializeObject<ChasePlaneMessage>(message);

                                var chasePlaneCameraConfigs = new List<ChasePlaneCameraConfig>();
                                var chasePlaneViews = viewMessage.Payload.Payload.ChasePlaneViews.FindAll(v => v.ProfileTheme == "ONBOARD_PIC" || v.ProfileTheme == "ONBOARD_SYSTEMS");
                                var currentAircraftName = string.Empty;

                                if (chasePlaneViews != null && chasePlaneViews.Count > 0)
                                {
                                    currentAircraftName = viewMessage.Payload.Payload.MetaData.Aircraft;

                                    var cameraViews = chasePlaneViews.FindAll(v => v.ProfilePhysicsType == "HUMAN");

                                    foreach (var chasePlaneView in cameraViews)
                                    {
                                        var item = new ChasePlaneCameraConfig
                                        {
                                            Name = chasePlaneView.Name,
                                            Guid = chasePlaneView.Guid,
                                            AircraftName = currentAircraftName
                                        };
                                        chasePlaneCameraConfigs.Add(item);
                                    }
                                }

                                OnCameraViewsReady(new CameraViewReadyEventArgs(chasePlaneViews, chasePlaneCameraConfigs));
                            }

                            break;
                    }
                }
            }
        }
        private bool IsJson(string source)
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
    }

    public class MessageSender()
    {
        public static async Task SendMessage(ClientWebSocket clientWebSocket, object chasePlaneMessage)
        {
            var message = JsonConvert.SerializeObject(chasePlaneMessage, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });

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
