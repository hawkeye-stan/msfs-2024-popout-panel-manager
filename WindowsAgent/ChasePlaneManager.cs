using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace MSFSPopoutPanelManager.WindowsAgent
{
    public class ChasePlaneManager
    {
        private static ClientWebSocket _clientWebSocket;
        public static bool IsConnected = false;

        public static List<ChasePlaneView> ChasePlaneViews { get; set; }

        public static async Task Connect()
        {
            IsConnected = false;
            _clientWebSocket = new ClientWebSocket();
            
            await _clientWebSocket.ConnectAsync(new Uri("ws://localhost:8652"), CancellationToken.None);

            SendMessage(new ChasePlaneMessage
            {
                Message = "api_connect",
                Payload = new ChasePlaneMessagePayload { ClientName = "POPM" }
            });

            try
            {
                var receiveTask = Task.Run(async () =>
                {
                    var buffer = new byte[1024 * 32];
                    while (true)
                    {
                        var result = await _clientWebSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

                        if (result.MessageType == WebSocketMessageType.Close)
                            break;

                        var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        ParseMessage(message);
                    }
                });

                await receiveTask;
            }
            finally
            {
                Disconnect();
            }
        }

        public static async Task Disconnect()
        {
            if(_clientWebSocket != null && _clientWebSocket.State == WebSocketState.Open) 
                await _clientWebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None);

            ChasePlaneViews = null;
            IsConnected = false;
        }

        public static void SetCamera(string cameraViewName, string guid)
        {
            var view = ChasePlaneViews.First<ChasePlaneView>(v => v.Name == cameraViewName && v.Guid.ToLower() == guid.ToLower());

            if(view != null)
                SendMessage(new ChasePlaneCamSetPositionMessage { Message = "cam_set_position", Payload = view });
        }

        public static void SetDefaultCamera()
        {
            SendMessage(new ChasePlaneMessage { Message = "cam_load_default" });
        }


        private static void ParseMessage(string message)
        {
            Debug.WriteLine(message);

            if (message == "CP_PING::" || message.StartsWith("CAM_MODE_SET"))
                return;

            var chasePlaneMessage = JsonConvert.DeserializeObject<ChasePlaneMessage>(message);

            if (chasePlaneMessage != null && !String.IsNullOrEmpty(chasePlaneMessage.Message))
            {
                switch (chasePlaneMessage.Message.ToLower())
                {
                    case "initialized":
                        // Get camera views
                        SendMessage(new ChasePlaneMessage
                        {
                            Message = "api_request",
                            RequestId = "get_views_" + DateTimeOffset.Now.ToUnixTimeMilliseconds(),
                            Command = "get_views"
                        });

                        break;
                    case "api_reply":
                        if(chasePlaneMessage.Payload.Message == "get_views")
                        {
                            var viewMessage = JsonConvert.DeserializeObject<ChasePlaneMessage>(message);
                            ChasePlaneViews = viewMessage.Payload.Payload.ChasePlaneViews.FindAll(v => v.ProfileTheme == "ONBOARD_PIC" || v.ProfileTheme == "ONBOARD_SYSTEMS");
                        }

                        if(ChasePlaneViews != null)
                            IsConnected = true;

                        break;
                }
            }
        }

        private static async void SendMessage(object chasePlaneMessage)
        {
            var message = JsonConvert.SerializeObject(chasePlaneMessage, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });

            var bytes = Encoding.UTF8.GetBytes(message);
            var arraySegment = new ArraySegment<byte>(bytes, 0, bytes.Length);
            if (_clientWebSocket.State == WebSocketState.Open)
            {
                await _clientWebSocket.SendAsync(arraySegment, WebSocketMessageType.Text, true, CancellationToken.None);
            }
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
            [JsonProperty("views")]
            public List<ChasePlaneView> ChasePlaneViews;
        }
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
