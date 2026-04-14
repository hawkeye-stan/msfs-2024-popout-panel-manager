using MSFSPopoutPanelManager.DomainModel.Profile;
using MSFSPopoutPanelManager.Orchestration;
using MSFSPopoutPanelManager.Shared;
using MSFSPopoutPanelManager.WindowsAgent;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using Colors = System.Windows.Media.Colors;

namespace MSFSPopoutPanelManager.MainApp.ViewModel
{
    public class MessageWindowViewModel : BaseViewModel
    {
        private const int WINDOW_WIDTH = 400;
        private const int WINDOW_HEIGHT = 225;

        private bool _isVisible;

        public IntPtr Handle { get; set; }

        public event EventHandler<List<Run>> OnMessageUpdated;
        public event EventHandler OnMessageCleared;

        public bool IsVisible
        {
            get => _isVisible;
            set
            {
                if (!AppSettingData.ApplicationSetting.PopOutSetting.EnablePopOutMessages)
                    return;

                _isVisible = value;
            }
        }

        public MessageWindowViewModel(SharedStorage sharedStorage, PanelSourceOrchestrator panelSourceOrchestrator, PanelPopOutOrchestrator panelPopOutOrchestrator) : base(sharedStorage)
        {
            IsVisible = false;

            panelPopOutOrchestrator.OnPopOutStarted += (_, _) =>
            {
                CenterDialogToProcessWindow(WindowProcessManager.SimulatorProcess);
            };
            panelPopOutOrchestrator.OnPopOutCompleted += (_, _) =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    StatusMessageWriter.ClearMessage();
                    IsVisible = false;
                    OnMessageCleared?.Invoke(this, null);
                });

                Thread.Sleep(1000);
            };

            ChasePlaneManager.ApiConnecting += (_, _) =>
            {
                IsVisible = true;
                CenterDialogToProcessWindow(WindowProcessManager.AppProcess);
            };

            ChasePlaneManager.ApiConnectionFailed += (_, _) =>
            {
                Thread.Sleep(2000);

                Application.Current.Dispatcher.Invoke(() =>
                {
                    StatusMessageWriter.ClearMessage();
                    IsVisible = false;
                    OnMessageCleared?.Invoke(this, null);
                });
            };

            ChasePlaneManager.ApiGeneralFailed += (_, _) =>
            {
                Thread.Sleep(2000);

                Application.Current.Dispatcher.Invoke(() =>
                {
                    StatusMessageWriter.ClearMessage();
                    IsVisible = false;
                    OnMessageCleared?.Invoke(this, null);
                });
            };

            ChasePlaneManager.CameraViewsReady += (_, _) =>
            {
                Thread.Sleep(1000);

                Application.Current.Dispatcher.Invoke(() =>
                {
                    StatusMessageWriter.ClearMessage();
                    IsVisible = false;
                    OnMessageCleared?.Invoke(this, null);
                });
            };

            StatusMessageWriter.OnStatusMessage += (_, e) =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (AppSettingData.ApplicationSetting.PopOutSetting.EnablePopOutMessages)
                    {
                        WindowActionManager.ApplyAlwaysOnTop(Handle, PanelType.StatusMessageWindow, true);
                        var runs = FormatStatusMessages(e);
                        OnMessageUpdated?.Invoke(this, FormatStatusMessages(e));

                        Task.Run(() => {
                            Thread.Sleep(250);
                            if (runs.Count > 0)
                                IsVisible = true;
                        });

                    }
                });
            };
        }

        private void CenterDialogToProcessWindow(WindowProcess windowProcess)
        {
            if (windowProcess == null)
                return;

            var simulatorRectangle = WindowActionManager.GetWindowRectangle(windowProcess.Handle);
            var left = simulatorRectangle.Left + simulatorRectangle.Width / 2 - WINDOW_WIDTH / 2;
            var top = simulatorRectangle.Top + simulatorRectangle.Height / 2 - WINDOW_HEIGHT / 2;
            WindowActionManager.MoveWindow(Handle, left, top, WINDOW_WIDTH, WINDOW_HEIGHT);
            WindowActionManager.ApplyAlwaysOnTop(Handle, PanelType.StatusMessageWindow, true);
        }

        private List<Run> FormatStatusMessages(List<StatusMessage> messages)
        {
            var runs = new List<Run>
            {
                Capacity = 0
            };

            lock (StatusMessageWriter.Lock)
            {
                foreach (var statusMessage in messages)
                {
                    var run = new Run
                    {
                        Text = statusMessage.Message
                    };

                    switch (statusMessage.StatusMessageType)
                    {
                        case StatusMessageType.Success:
                            run.Foreground = new SolidColorBrush(Colors.LimeGreen);
                            break;
                        case StatusMessageType.Failure:
                            run.Foreground = new SolidColorBrush(Colors.IndianRed);
                            break;
                        case StatusMessageType.Executing:
                            run.Foreground = new SolidColorBrush(Colors.NavajoWhite);
                            break;
                        case StatusMessageType.Info:
                            break;
                    }

                    runs.Add(run);

                    if (statusMessage.NewLine)
                        runs.Add(new Run { Text = Environment.NewLine });
                }
            }

            return runs;
        }
    }
}
