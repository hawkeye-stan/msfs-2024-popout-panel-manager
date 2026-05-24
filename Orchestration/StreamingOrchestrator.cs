using MSFSPopoutPanelManager.DomainModel.Profile;
using MSFSPopoutPanelManager.DomainModel.Setting;
using MSFSPopoutPanelManager.WindowsAgent;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MSFSPopoutPanelManager.Orchestration
{
    public class StreamingOrchestrator : BaseOrchestrator
    {
        [DllImport("user32.dll")] static extern bool PrintWindow(IntPtr hwnd, IntPtr hdc, uint flags);
        const uint PW_RENDERFULLCONTENT = 0x2;
        const uint PW_CLIENTONLY = 0x1;

        // Slug → cancellation token (presence means panel is available to stream)
        readonly ConcurrentDictionary<string, CancellationTokenSource> _streams = new();

        TcpListener _tcpListener;
        CancellationTokenSource _serverCts;
        int _port;

        readonly PanelPopOutOrchestrator _popOutOrchestrator;

        public StreamingOrchestrator(SharedStorage sharedStorage, PanelPopOutOrchestrator popOutOrchestrator) : base(sharedStorage)
        {
            _port = AppSettingData.ApplicationSetting.StreamingSetting.Port;
            if (AppSettingData.ApplicationSetting.StreamingSetting.IsEnabled)
                EnsureListenerRunning();
            _popOutOrchestrator = popOutOrchestrator;
            _popOutOrchestrator.OnPopOutCompleted += OnPopOutCompleted;
            AppSettingData.ApplicationSetting.StreamingSetting.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName != nameof(StreamingSetting.Port)) return;
                if (!AppSettingData.ApplicationSetting.StreamingSetting.IsEnabled) return;
                _port = AppSettingData.ApplicationSetting.StreamingSetting.Port;
                Task.Run(async () =>
                {
                    StopServer();
                    await Task.Delay(200); // allow OS to release the port
                    EnsureListenerRunning();
                });
            };
        }

        private void SubscribeToActiveProfile()
        {
            if (ProfileData?.ActiveProfile == null) return;
            ProfileData.ActiveProfile.PropertyChanged -= OnActiveProfilePropertyChanged;
            ProfileData.ActiveProfile.PropertyChanged += OnActiveProfilePropertyChanged;

            foreach (var panel in ProfileData.ActiveProfile.PanelConfigs)
            {
                panel.PropertyChanged -= OnPanelPropertyChanged;
                panel.PropertyChanged += OnPanelPropertyChanged;
            }
        }

        private void OnPanelPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (sender is not PanelConfig panel) return;
            if (!AppSettingData.ApplicationSetting.StreamingSetting.IsEnabled) return;

            if (e.PropertyName == nameof(PanelConfig.EnableStreaming))
            {
                if (panel.EnableStreaming) StartPanelStream(panel);
                else StopPanelStream(panel);
                return;
            }

            if (e.PropertyName == nameof(PanelConfig.PanelName))
            {
                // Remove any _streams entries whose key no longer matches a live panel (i.e. the old name)
                var liveKeys = LivePanels().Select(PanelKey).ToHashSet();
                foreach (var staleKey in _streams.Keys.Where(k => !liveKeys.Contains(k)).ToList())
                    if (_streams.TryRemove(staleKey, out var cts))
                    {
                        cts.Cancel();
                        cts.Dispose();
                    }

                if (panel.EnableStreaming && panel.IsPopOutSuccess == true)
                    StartPanelStream(panel);
            }
        }

        private void OnActiveProfilePropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(ProfileData.ActiveProfile.IsLocked)) return;

            if (ProfileData.ActiveProfile.IsLocked)
            {
                if (!AppSettingData.ApplicationSetting.StreamingSetting.IsEnabled) return;

                // Delay to run after POPM's StartConfiguration finishes repositioning windows
                Task.Delay(1500).ContinueWith(_ =>
                {
                    foreach (var panel in LivePanels())
                        MoveOffScreen(panel);
                });
            }
            else
            {
                foreach (var panel in LivePanels())
                    RestorePanel(panel);
            }
        }

        public void ApplyMasterToggle(bool isEnabled)
        {
            if (isEnabled)
            {
                EnsureListenerRunning();
                if (ProfileData?.ActiveProfile == null) return;
                var panels = LivePanels();
                Task.Delay(200).ContinueWith(_ =>
                {
                    foreach (var panel in panels)
                        StartPanelStream(panel);
                });
            }
            else
            {
                StopServer();
                if (ProfileData?.ActiveProfile == null) return;
                foreach (var panel in ProfileData.ActiveProfile.PanelConfigs.ToList())
                    StopPanelStream(panel);
            }
        }

        private string StreamHost
        {
            get
            {
                var ov = AppSettingData.ApplicationSetting.StreamingSetting.HostOverride?.Trim();
                if (!string.IsNullOrEmpty(ov)) return ov;

                var hostname = Dns.GetHostName();
                if (string.Equals(hostname, "localhost", StringComparison.OrdinalIgnoreCase))
                    hostname = DetectLanIp() ?? hostname;
                return hostname;
            }
        }

        private static string DetectLanIp()
        {
            foreach (var addr in Dns.GetHostAddresses(Dns.GetHostName()))
                if (addr.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(addr))
                    return addr.ToString();
            return null;
        }

        public string GetIndexUrl() => $"http://{StreamHost}:{_port}/";

        public string GetStreamUrl(PanelConfig panel) =>
            $"http://{StreamHost}:{_port}/stream/{PanelKey(panel)}/";

        private void OnPopOutCompleted(object sender, EventArgs e)
        {
            _port = AppSettingData.ApplicationSetting.StreamingSetting.Port;
            if (AppSettingData.ApplicationSetting.StreamingSetting.IsEnabled)
                EnsureListenerRunning();

            if (ProfileData?.ActiveProfile == null) return;

            SubscribeToActiveProfile();

            if (!AppSettingData.ApplicationSetting.StreamingSetting.IsEnabled) return;

            // Delay to run after POPM's StartConfiguration finishes repositioning windows
            var panels = LivePanels();
            Task.Delay(1500).ContinueWith(_ =>
            {
                foreach (var panel in panels)
                    StartPanelStream(panel);
            });
        }

        private void EnsureListenerRunning()
        {
            if (_tcpListener != null) return;
            try
            {
                _serverCts = new CancellationTokenSource();
                _tcpListener = new TcpListener(IPAddress.Any, _port);
                _tcpListener.Start();
                Task.Run(() => AcceptLoop(_serverCts.Token));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"StreamingOrchestrator: failed to start server: {ex.Message}");
            }
        }

        private void StopServer()
        {
            _serverCts?.Cancel();
            _serverCts?.Dispose();
            _serverCts = null;
            _tcpListener?.Stop();
            _tcpListener = null;
        }

        private async Task AcceptLoop(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var client = await _tcpListener.AcceptTcpClientAsync(ct);
                    _ = Task.Run(() => HandleClient(client, ct), CancellationToken.None);
                }
                catch { break; }
            }
        }

        private async Task HandleClient(TcpClient client, CancellationToken serverCt)
        {
            using (client)
            using (var stream = client.GetStream())
            using (var handshakeCts = CancellationTokenSource.CreateLinkedTokenSource(serverCt))
            {
                handshakeCts.CancelAfter(TimeSpan.FromSeconds(5));
                var ct = handshakeCts.Token;
                try
                {
                    var requestLine = await ReadLineAsync(stream, ct);
                    if (string.IsNullOrEmpty(requestLine)) return;

                    // Drain request headers
                    string headerLine;
                    while (!string.IsNullOrEmpty(headerLine = await ReadLineAsync(stream, ct))) { }

                    // Parse "GET /path HTTP/1.1" — strip query string and trim slashes
                    var tokens = requestLine.Split(' ');
                    if (tokens.Length < 2) return;
                    var rawPath = tokens[1].Split('?')[0].Trim('/');
                    var parts = rawPath.Split('/');

                    if (rawPath == "")
                        await ServeIndex(stream, serverCt);
                    else if (parts.Length >= 2 && parts[0] == "stream")
                        await ServeMjpeg(stream, parts[1], serverCt);
                    else if (parts.Length >= 2 && parts[0] == "snapshot")
                        await ServeSnapshot(stream, parts[1]);
                    else
                        await stream.WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 404 Not Found\r\n\r\n"), serverCt);
                }
                catch { }
            }
        }

        private static async Task<string> ReadLineAsync(NetworkStream stream, CancellationToken ct)
        {
            var sb = new StringBuilder();
            var buf = new byte[1];
            while (true)
            {
                int n = await stream.ReadAsync(buf, 0, 1, ct);
                if (n == 0) return null;
                char c = (char)buf[0];
                if (c == '\n') return sb.ToString().TrimEnd('\r');
                if (sb.Length < 4096) sb.Append(c); else return null;
            }
        }

        private async Task ServeMjpeg(NetworkStream stream, string key, CancellationToken serverCt)
        {
            var panel = FindPanelById(key);
            if (panel == null || !_streams.TryGetValue(key, out var cts))
            {
                await stream.WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 404 Not Found\r\n\r\n"), serverCt);
                return;
            }

            const string boundary = "mjpegframe";
            await stream.WriteAsync(Encoding.ASCII.GetBytes(
                $"HTTP/1.1 200 OK\r\nContent-Type: multipart/x-mixed-replace; boundary={boundary}\r\n\r\n"), serverCt);

            var (codec, ep) = JpegParams(80);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, serverCt);
            var token = linked.Token;

            try
            {
                while (!token.IsCancellationRequested)
                {
                    using var bmp = Capture(panel.PanelHandle);
                    if (bmp == null) { await Task.Delay(100, token); continue; }

                    using var ms = new MemoryStream();
                    bmp.Save(ms, codec, ep);
                    var jpg = ms.ToArray();

                    await stream.WriteAsync(Encoding.ASCII.GetBytes(
                        $"\r\n--{boundary}\r\nContent-Type: image/jpeg\r\nContent-Length: {jpg.Length}\r\n\r\n"), token);
                    await stream.WriteAsync(jpg, token);
                    await stream.FlushAsync(token);

                    await Task.Delay(33, token);
                }
            }
            catch (OperationCanceledException) { }
        }

        private async Task ServeSnapshot(NetworkStream stream, string key)
        {
            var panel = FindPanelById(key);
            if (panel == null) { await stream.WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 404 Not Found\r\n\r\n")); return; }

            using var bmp = Capture(panel.PanelHandle);
            if (bmp == null) { await stream.WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 404 Not Found\r\n\r\n")); return; }

            var (codec, ep) = JpegParams(95);
            using var ms = new MemoryStream();
            bmp.Save(ms, codec, ep);
            var jpg = ms.ToArray();

            await stream.WriteAsync(Encoding.ASCII.GetBytes(
                $"HTTP/1.1 200 OK\r\nContent-Type: image/jpeg\r\nContent-Length: {jpg.Length}\r\n\r\n"));
            await stream.WriteAsync(jpg);
        }

        private async Task ServeIndex(NetworkStream stream, CancellationToken ct)
        {
            var panels = ProfileData?.ActiveProfile?.PanelConfigs
                .Where(p => p.IsPopOutSuccess == true && p.EnableStreaming)
                .ToList();

            var cards = "";
            if (panels != null)
                foreach (var p in panels)
                {
                    var id = PanelKey(p);
                    cards += $@"
                    <a href='/stream/{id}/' class='card'>
                        <img id='thumb-{id}' src='/snapshot/{id}/?t=0' alt='{p.PanelName}' />
                        <div class='label'>{p.PanelName}</div>
                    </a>";
                }

            bool noStreams = panels == null || panels.Count == 0;
            var noStream = noStreams
                ? "<p style='color:#aaa'>No panels are currently streaming. Click <b>Start Pop Out</b> in MSFS Pop Out Panel Manager.</p>"
                : "";
            var metaRefresh = noStreams ? "<meta http-equiv='refresh' content='3'>" : "";

            var html = $@"<!DOCTYPE html>
<html>
<head>
<meta charset='utf-8'>
{metaRefresh}<title>MSFS Panel Streams</title>
<style>
  body {{ background:#1a1a1a; color:#eee; font-family:sans-serif; margin:0; padding:20px; }}
  h2   {{ margin-bottom:20px; }}
  .grid {{ display:flex; flex-wrap:wrap; gap:16px; }}
  .card {{ display:block; text-decoration:none; color:#eee; background:#2a2a2a;
           border-radius:8px; overflow:hidden; border:2px solid #444;
           transition:border-color .2s; }}
  .card:hover {{ border-color:#4fc3f7; }}
  .card img {{ display:block; max-width:320px; width:100%; height:auto; }}
  .label {{ padding:8px 12px; font-size:14px; }}
</style>
</head>
<body>
<h2>MSFS Panel Streams</h2>
{noStream}
<div class='grid'>{cards}</div>
<script>
  var t = 0;
  setInterval(function() {{
    t++;
    document.querySelectorAll('.card img').forEach(function(img) {{
      var base = img.src.split('?')[0];
      img.src = base + '?t=' + t;
    }});
  }}, 2000);
</script>
</body>
</html>";

            var bytes = Encoding.UTF8.GetBytes(html);
            await stream.WriteAsync(Encoding.ASCII.GetBytes(
                $"HTTP/1.1 200 OK\r\nContent-Type: text/html; charset=utf-8\r\nContent-Length: {bytes.Length}\r\n\r\n"), ct);
            await stream.WriteAsync(bytes, ct);
        }

        private void StartPanelStream(PanelConfig panel)
        {
            var key = PanelKey(panel);
            if (_streams.ContainsKey(key)) return;

            if (ProfileData?.ActiveProfile?.IsLocked == true)
                MoveOffScreen(panel);

            _streams[key] = new CancellationTokenSource();
        }

        private void StopPanelStream(PanelConfig panel)
        {
            var key = PanelKey(panel);
            if (_streams.TryRemove(key, out var cts))
            {
                cts.Cancel();
                cts.Dispose();
            }

            if (ProfileData?.ActiveProfile?.IsLocked == true)
                RestorePanel(panel);
        }

        private static void MoveOffScreen(PanelConfig panel)
        {
            var rect = WindowActionManager.GetWindowRectangle(panel.PanelHandle);
            WindowActionManager.MoveWindow(panel.PanelHandle, -rect.Width - 100, 0, rect.Width, rect.Height);
        }

        private static void RestorePanel(PanelConfig panel)
        {
            var rect = WindowActionManager.GetWindowRectangle(panel.PanelHandle);
            WindowActionManager.MoveWindow(panel.PanelHandle, panel.Left, panel.Top, rect.Width, rect.Height);
        }

        private Bitmap Capture(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero || hwnd == IntPtr.MaxValue) return null;

            var client = PInvoke.GetClientRectangle(hwnd);
            int w = Math.Max(client.Width, 1);
            int h = Math.Max(client.Height, 1);

            var bmp = new Bitmap(w, h);
            using var g = Graphics.FromImage(bmp);
            var hdc = g.GetHdc();
            PrintWindow(hwnd, hdc, PW_RENDERFULLCONTENT | PW_CLIENTONLY);
            g.ReleaseHdc(hdc);
            return bmp;
        }

        private IEnumerable<PanelConfig> LivePanels() =>
            ProfileData?.ActiveProfile?.PanelConfigs
                .Where(p => p.IsPopOutSuccess == true && p.EnableStreaming)
                .ToList() ?? Enumerable.Empty<PanelConfig>();

        private PanelConfig FindPanelById(string id) =>
            ProfileData?.ActiveProfile?.PanelConfigs
                .FirstOrDefault(p => PanelKey(p) == id && p.IsPopOutSuccess == true);

        private static string PanelKey(PanelConfig panel) =>
            Uri.EscapeDataString(panel.PanelName.Trim());

        private static (ImageCodecInfo codec, EncoderParameters ep) JpegParams(long quality)
        {
            var codec = ImageCodecInfo.GetImageEncoders().First(e => e.FormatID == ImageFormat.Jpeg.Guid);
            var ep = new EncoderParameters(1);
            ep.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, quality);
            return (codec, ep);
        }
    }
}
