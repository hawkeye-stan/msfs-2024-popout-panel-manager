using MSFSPopoutPanelManager.DomainModel.Profile;
using MSFSPopoutPanelManager.Shared;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace MSFSPopoutPanelManager.WindowsAgent
{
    public class ChasePlaneManager
    {
        private const int SupportedApiVersion = 2;
        private const string WebSocketUrl = "ws://localhost:8652";
        private const int ReceiveBufferSize = 1024 * 128;

        private static ClientWebSocket _clientWebSocket;
        private static Task _messageListenerTask;
        private static bool _isInitialized;
        private static bool _isViewsReady;
        private static bool _isApiConnectionStarted;
        private static int _getCameraViewRetryCount;

        public static List<ChasePlaneView> ChasePlaneViews { get; private set; }
        public static ObservableRangeCollection<ChasePlaneCameraConfig> ChasePlaneCameraConfigs { get; private set; } = new();
        public static bool HasCameraViews => ChasePlaneCameraConfigs.Count > 0;
        public static AutoResetEvent IsChasePlaneViewsReady = new AutoResetEvent(false);

        public static async Task<bool> Run()
        {
            IsChasePlaneViewsReady.Reset();

            if (IsWebSocketConnected())
            {
                IsChasePlaneViewsReady.Set();
                return true;
            }

            ChasePlaneCameraConfigs.Clear();

            if (_isApiConnectionStarted)
                return true;

            _isApiConnectionStarted = true;

            if (!await ConnectWebSocket())
            {
                _isApiConnectionStarted = false;
                return false;
            }

            _messageListenerTask = Task.Run(() => ListenInBackground());
            await SendMessageAsync(new ChasePlaneMessage
            {
                Message = "api_connect",
                Payload = new ChasePlaneMessagePayload { ClientName = "POPM" }
            });

            return true;
        }

        public static async Task SetCamera(string cameraViewName, string guid)
        {
            var view = ChasePlaneViews?.FirstOrDefault(v =>
                v.Name.Equals(cameraViewName, StringComparison.InvariantCultureIgnoreCase) &&
                v.Guid.Equals(guid, StringComparison.InvariantCultureIgnoreCase));

            if (view != null && IsWebSocketConnected())
            {
                await SendMessageAsync(new ChasePlaneCamSetPositionMessage { Message = "cam_set_position", Payload = view });
                Thread.Sleep(250);
            }
        }

        public static async Task SetDefaultCamera()
        {
            if (IsWebSocketConnected())
            {
                await SendMessageAsync(new ChasePlaneMessage { Message = "cam_load_default" });
                Thread.Sleep(500);
            }
        }

        public static async Task Disconnect()
        {
            await DisconnectWebSocket();
        }

        private static bool IsWebSocketConnected()
        {
            return _clientWebSocket != null && _clientWebSocket.State == WebSocketState.Open;
        }

        private static async Task<bool> ConnectWebSocket()
        {
            _clientWebSocket = new ClientWebSocket();
            _clientWebSocket.Options.SetBuffer(ReceiveBufferSize, 1024 * 10);
            _isInitialized = false;
            _isViewsReady = false;

            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
            var connectionTask = _clientWebSocket.ConnectAsync(new Uri(WebSocketUrl), CancellationToken.None);
            await Task.WhenAny(connectionTask, timeoutTask);

            if (IsWebSocketConnected())
                return true;

            _clientWebSocket = null;
            return false;
        }

        private static async Task ListenInBackground()
        {
            var buffer = ArrayPool<byte>.Shared.Rent(ReceiveBufferSize);

            try
            {
                while (_clientWebSocket.State == WebSocketState.Open)
                {
                    var result = await _clientWebSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

                    if (result.MessageType == WebSocketMessageType.Close)
                        break;

                    var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    Debug.WriteLine(message);

                    if (!IsJson(message))
                        continue;

                    var chasePlaneMessage = ChasePlaneProtocol.DeserializeMessage(message);
                    if (chasePlaneMessage == null || string.IsNullOrEmpty(chasePlaneMessage.Message))
                        continue;

                    await HandleMessage(chasePlaneMessage, message);
                }
            }
            catch (WebSocketException ex) when (ex.WebSocketErrorCode != WebSocketError.InvalidState)
            {
                FileLogger.WriteException("ChasePlane 2024 Exception", ex);
                await DisconnectWebSocket();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                FileLogger.WriteException("ChasePlane 2024 Exception", ex);
                await DisconnectWebSocket();
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        private static async Task HandleMessage(ChasePlaneMessage message, string rawMessage)
        {
            switch (message.Message.ToLower())
            {
                case "api_version":
                    HandleApiVersion(message);
                    break;

                case "cam_mode_set":
                    await HandleCamModeSet(message);
                    break;

                case "initialized":
                    if (!_isInitialized)
                    {
                        Debug.WriteLine(rawMessage);
                        _isInitialized = true;
                    }
                    break;

                case "api_reply":
                    await HandleApiReply(message, rawMessage);
                    break;
            }
        }

        private static void HandleApiVersion(ChasePlaneMessage message)
        {
            if (!ChasePlaneProtocol.TryGetApiVersion(message, out int apiVersion) || apiVersion != SupportedApiVersion)
                throw new NotSupportedException($"ChasePlane API version {apiVersion} is not supported. Version {SupportedApiVersion} is required.");
        }

        private static async Task HandleCamModeSet(ChasePlaneMessage message)
        {
            if (_isViewsReady || !ChasePlaneProtocol.TryGetViewsLoaded(message, out bool viewsLoaded) || !viewsLoaded)
                return;

            _isViewsReady = true;
            _getCameraViewRetryCount = 3;

            Thread.Sleep(1000);
            await GetCameraViews();
        }

        private static async Task HandleApiReply(ChasePlaneMessage message, string rawMessage)
        {
            if (!ChasePlaneProtocol.TryGetCameraViewsPayload(message, out var viewPayload))
                return;

            Debug.WriteLine(rawMessage);

            var aircraftName = viewPayload.MetaData.Aircraft;
            var onboardViews = viewPayload.ChasePlaneViews
                .Where(v => IsOnboardView(v) && v.Aircraft.Equals(aircraftName, StringComparison.InvariantCultureIgnoreCase))
                .ToList();

            if (onboardViews.Count > 0)
            {
                PopulateCameraConfigs(onboardViews, aircraftName);
                Thread.Sleep(1000);
                IsChasePlaneViewsReady.Set();
                IsChasePlaneViewsReady.Reset();
            }
            else if (_getCameraViewRetryCount-- > 0)
            {
                Thread.Sleep(2000);
                await GetCameraViews();
            }
            else
            {
                await Disconnect();
            }
        }

        private static bool IsOnboardView(ChasePlaneView view)
        {
            return view.ProfileTheme.Equals("ONBOARD_PIC", StringComparison.InvariantCultureIgnoreCase) ||
                   view.ProfileTheme.Equals("ONBOARD_SYSTEMS", StringComparison.InvariantCultureIgnoreCase);
        }

        private static void PopulateCameraConfigs(List<ChasePlaneView> views, string aircraftName)
        {
            var configs = views.Select(v => new ChasePlaneCameraConfig
            {
                Name = v.Name,
                Guid = v.Guid,
                AircraftName = aircraftName
            }).ToList();

            ChasePlaneViews = views;
            ChasePlaneCameraConfigs.AddRange(configs);
        }

        private static async Task DisconnectWebSocket()
        {
            try
            {
                if (IsWebSocketConnected())
                    await _clientWebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None);
            }
            catch { }
            finally
            {
                ResetState();
            }
        }

        private static void ResetState()
        {
            _isApiConnectionStarted = false;
            _isInitialized = false;
            _isViewsReady = false;
            _clientWebSocket = null;
            _messageListenerTask = null;
            ChasePlaneCameraConfigs.Clear();
            IsChasePlaneViewsReady.Reset();
        }

        private static async Task GetCameraViews()
        {
            if (!IsWebSocketConnected())
                return;

            await SendMessageAsync(new ChasePlaneMessage
            {
                Message = "api_request",
                RequestId = $"get_views_{DateTimeOffset.Now.ToUnixTimeMilliseconds()}",
                Command = "get_views"
            });
        }

        private static bool IsJson(string source)
        {
            if (string.IsNullOrEmpty(source))
                return false;

            try
            {
                using var document = JsonDocument.Parse(source);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static async Task SendMessageAsync(object chasePlaneMessage)
        {
            if (!IsWebSocketConnected())
                return;

            var json = JsonConvert.SerializeObject(chasePlaneMessage, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
            var bytes = Encoding.UTF8.GetBytes(json);
            await _clientWebSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
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
        public object Payload;
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

    internal static class ChasePlaneProtocol
    {
        public static ChasePlaneMessage DeserializeMessage(string message)
        {
            return JsonConvert.DeserializeObject<ChasePlaneMessage>(message);
        }

        public static bool TryGetApiVersion(ChasePlaneMessage message, out int apiVersion)
        {
            var payload = DeserializePayload<ChasePlaneApiVersionPayload>(message?.Payload);
            apiVersion = payload?.Version ?? 0;
            return apiVersion > 0;
        }

        public static bool TryGetViewsLoaded(ChasePlaneMessage message, out bool viewsLoaded)
        {
            var payload = DeserializePayload<ChasePlaneCamModeSetPayload>(message?.Payload);
            viewsLoaded = payload?.ViewsLoaded ?? false;
            return payload != null;
        }

        public static bool TryGetCameraViewsPayload(ChasePlaneMessage message, out ChasePlaneMessagePayload.ChasePlaneApiGetViewReplyPayload viewPayload)
        {
            viewPayload = null;
            var nestedPayload = DeserializePayload<ChasePlaneMessagePayload>(message?.Payload);

            if (nestedPayload?.Message?.Equals("get_views", StringComparison.InvariantCultureIgnoreCase) == true)
            {
                viewPayload = nestedPayload.Payload;
            }
            else if (message?.RequestId?.StartsWith("get_views_", StringComparison.InvariantCultureIgnoreCase) == true)
            {
                viewPayload = DeserializePayload<ChasePlaneMessagePayload.ChasePlaneApiGetViewReplyPayload>(message.Payload);
            }

            return viewPayload?.MetaData != null && viewPayload.ChasePlaneViews != null;
        }

        private static T DeserializePayload<T>(object payload) where T : class
        {
            if (payload is JToken token)
                return token.ToObject<T>();

            return payload == null ? null : JToken.FromObject(payload).ToObject<T>();
        }

        private class ChasePlaneApiVersionPayload
        {
            [JsonProperty("version")]
            public int Version;
        }

        private class ChasePlaneCamModeSetPayload
        {
            [JsonProperty("views_loaded")]
            public bool ViewsLoaded;
        }
    }
}
