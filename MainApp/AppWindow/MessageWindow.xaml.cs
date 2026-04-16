using Microsoft.Extensions.DependencyInjection;
using MSFSPopoutPanelManager.MainApp.ViewModel;
using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Interop;

namespace MSFSPopoutPanelManager.MainApp.AppWindow
{
    public partial class MessageWindow
    {
        private readonly MessageWindowViewModel _viewModel;

        public MessageWindow()
        {
            InitializeComponent();
            if (DesignerProperties.GetIsInDesignMode(new DependencyObject()))
                return;

            _viewModel = App.AppHost.Services.GetRequiredService<MessageWindowViewModel>();
            Loaded += (_, _) =>
            {
                DataContext = _viewModel;

                var window = Window.GetWindow(this);
                if (window == null)
                    throw new ApplicationException("Unable to instantiate status message window");
                
                _viewModel.Handle = new WindowInteropHelper(window).Handle;

                // Set window binding, needs to be in code after window loaded
                var visibleBinding = new Binding("IsVisible")
                {
                    Source = _viewModel,
                    Converter = new BooleanToVisibilityConverter()
                };
                BindingOperations.SetBinding(this, Window.VisibilityProperty, visibleBinding);

                // Set window click through
                WindowsServices.SetWindowExTransparent(_viewModel.Handle);

                // Set Textblock binding
                _viewModel.MessageList.CollectionChanged += Messages_CollectionChanged;
            };
        }

        private void Messages_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                this.Topmost = true;

                TextBlockMessage.Text = string.Empty;

                if (_viewModel.MessageList == null || _viewModel.MessageList.Count == 0)
                    return;
                                
                foreach (var message in _viewModel.MessageList)
                    TextBlockMessage.Inlines.Add(message);

                ScrollViewerMessage.ScrollToEnd();
            });
        }
    }

    public static class WindowsServices
    {
        const int WS_EX_TRANSPARENT = 0x00000020;
        const int GWL_EXSTYLE = (-20);

        [DllImport("user32.dll")]
        static extern int GetWindowLong(IntPtr hWnd, int index);

        [DllImport("user32.dll")]
        static extern int SetWindowLong(IntPtr hWnd, int index, int newStyle);

        public static void SetWindowExTransparent(IntPtr hWnd)
        {
            var extendedStyle = GetWindowLong(hWnd, GWL_EXSTYLE);
            SetWindowLong(hWnd, GWL_EXSTYLE, extendedStyle | WS_EX_TRANSPARENT);
        }
    }
}