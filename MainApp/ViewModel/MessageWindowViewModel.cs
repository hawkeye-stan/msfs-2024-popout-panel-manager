using MSFSPopoutPanelManager.DomainModel.Profile;
using MSFSPopoutPanelManager.Orchestration;
using MSFSPopoutPanelManager.Shared;
using MSFSPopoutPanelManager.WindowsAgent;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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

        public ObservableCollection<Run> MessageList = new();

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
                Application.Current.Dispatcher.Invoke(() =>
                {
                    MessageList.Clear();
                    CenterDialogToProcessWindow(WindowProcessManager.SimulatorProcess);

                    Task.Run(() =>
                    {
                        Thread.Sleep(500);
                        IsVisible = true;
                    });
                });
            };

            panelPopOutOrchestrator.OnPopOutCompleted += (_, _) =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Task.Run(() => 
                    {
                        Thread.Sleep(500);
                        IsVisible = false;
                        MessageList.Clear();
                    });
                });
            };


            panelSourceOrchestrator.OnChasePlaneLoadStarted += (_, _) =>
            {
                if (!ChasePlaneManager.HasCameraViews)
                {
                    MessageList.Clear();
                    CenterDialogToProcessWindow(WindowProcessManager.AppProcess);

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        Task.Run(() =>
                        {
                            Thread.Sleep(500);
                            IsVisible = true;
                        });
                    });
                }
            };

            panelSourceOrchestrator.OnChasePlaneLoadCompleted += (_, _) =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Task.Run(() =>
                    {
                        IsVisible = false;
                        MessageList.Clear();
                    });
                });
            };

            StatusMessageWriter.OnStatusMessage += (_, e) =>
            {
                if (!AppSettingData.ApplicationSetting.PopOutSetting.EnablePopOutMessages)
                    return;

                Application.Current.Dispatcher.Invoke(() =>
                {
                    WindowActionManager.ApplyAlwaysOnTop(Handle, PanelType.StatusMessageWindow, true);

                    if (MessageList.Count > 0)
                        IsVisible = true;

                    FormatStatusMessages(e);
                });
            };

            StatusMessageWriter.OnStatusMessageClear += (_, e) =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    MessageList.Clear();
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

        private void FormatStatusMessages(List<StatusMessage> statusMessages)
        {
            foreach (var statusMessage in statusMessages)
            {
                var run = new Run
                {
                    Text = statusMessage.Message
                };

                switch (statusMessage.StatusMessageType)
                {
                    case StatusMessageType.Success:
                        run.Foreground = new SolidColorBrush(Colors.LimeGreen);
                        if (MessageList.Count > 1)
                            MessageList.RemoveAt(MessageList.Count - 1);
                        break;
                    case StatusMessageType.Failure:
                        run.Foreground = new SolidColorBrush(Colors.IndianRed);
                        if (MessageList.Count > 1)
                            MessageList.RemoveAt(MessageList.Count - 1);
                        break;
                    case StatusMessageType.Executing:
                        run.Foreground = new SolidColorBrush(Colors.NavajoWhite);
                        break;
                    case StatusMessageType.Info:
                        break;
                }

                MessageList.Add(run);

                if (statusMessage.NewLine)
                    MessageList.Add(new Run { Text = Environment.NewLine });
            }
        }
    }
}
