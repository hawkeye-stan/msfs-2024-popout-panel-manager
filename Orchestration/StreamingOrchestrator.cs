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
using System.Runtime.InteropServices;
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

        HttpListener _listener;
        Thread _listenerThread;
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
                Task.Run(() =>
                {
                    _listener?.Stop();
                    _listener = null;
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
                        cts.Cancel();

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
                _listener?.Stop();
                _listener = null;
                if (ProfileData?.ActiveProfile == null) return;
                foreach (var panel in ProfileData.ActiveProfile.PanelConfigs.ToList())
                    StopPanelStream(panel);
            }
        }

        public string GetStreamUrl(PanelConfig panel) =>
            $"http://localhost:{_port}/stream/{PanelKey(panel)}/";

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
            if (_listener != null && _listener.IsListening) return;

            try
            {
                _listener?.Stop();
                _listener = new HttpListener();
                _listener.Prefixes.Add($"http://localhost:{_port}/");
                _listener.Start();

                _listenerThread = new Thread(ListenLoop) { IsBackground = true, Name = "StreamListener" };
                _listenerThread.Start();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"StreamingOrchestrator: failed to start listener: {ex.Message}");
            }
        }

        private void ListenLoop()
        {
            while (_listener?.IsListening == true)
            {
                try
                {
                    var ctx = _listener.GetContext();
                    ThreadPool.QueueUserWorkItem(_ => HandleRequest(ctx));
                }
                catch { break; }
            }
        }

        private void HandleRequest(HttpListenerContext ctx)
        {
            var path = ctx.Request.Url?.AbsolutePath ?? "";
            var parts = path.Trim('/').Split('/');

            if (parts.Length >= 2 && parts[0] == "stream")
            {
                var panel = FindPanelById(parts[1]);
                if (panel != null) ServeMjpeg(ctx, panel);
                else Respond404(ctx);
                return;
            }

            if (parts.Length >= 2 && parts[0] == "snapshot")
            {
                var panel = FindPanelById(parts[1]);
                if (panel != null) ServeSnapshot(ctx, panel);
                else Respond404(ctx);
                return;
            }

            if (path == "/" || path == "")
            {
                ServeIndex(ctx);
                return;
            }

            Respond404(ctx);
        }

        private void ServeMjpeg(HttpListenerContext ctx, PanelConfig panel)
        {
            const string boundary = "mjpegframe";
            ctx.Response.ContentType = $"multipart/x-mixed-replace; boundary={boundary}";
            ctx.Response.StatusCode = 200;

            if (!_streams.TryGetValue(PanelKey(panel), out var cts))
            {
                ctx.Response.Close();
                return;
            }

            var (codec, ep) = JpegParams(80);
            using var stream = ctx.Response.OutputStream;
            using var writer = new StreamWriter(stream) { AutoFlush = true };

            try
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    using var bmp = Capture(panel.PanelHandle);
                    if (bmp == null) { Thread.Sleep(100); continue; }

                    using var ms = new MemoryStream();
                    bmp.Save(ms, codec, ep);
                    var jpg = ms.ToArray();

                    writer.Write($"\r\n--{boundary}\r\nContent-Type: image/jpeg\r\nContent-Length: {jpg.Length}\r\n\r\n");
                    writer.Flush();
                    stream.Write(jpg, 0, jpg.Length);
                    stream.Flush();

                    Thread.Sleep(33);
                }
            }
            catch { }
        }

        private void ServeSnapshot(HttpListenerContext ctx, PanelConfig panel)
        {
            using var bmp = Capture(panel.PanelHandle);
            if (bmp == null) { Respond404(ctx); return; }

            var (codec, ep) = JpegParams(95);
            using var ms = new MemoryStream();
            bmp.Save(ms, codec, ep);
            var jpg = ms.ToArray();

            ctx.Response.ContentType = "image/jpeg";
            ctx.Response.ContentLength64 = jpg.Length;
            ctx.Response.OutputStream.Write(jpg, 0, jpg.Length);
            ctx.Response.Close();
        }

        private void ServeIndex(HttpListenerContext ctx)
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

            var bytes = System.Text.Encoding.UTF8.GetBytes(html);
            ctx.Response.ContentType = "text/html; charset=utf-8";
            ctx.Response.ContentLength64 = bytes.Length;
            ctx.Response.OutputStream.Write(bytes, 0, bytes.Length);
            ctx.Response.Close();
        }

        private static void Respond404(HttpListenerContext ctx)
        {
            ctx.Response.StatusCode = 404;
            ctx.Response.Close();
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
                cts.Cancel();

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
