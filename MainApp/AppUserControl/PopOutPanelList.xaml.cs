using System.Windows.Controls;
using System.Windows;
using MSFSPopoutPanelManager.DomainModel.Profile;

namespace MSFSPopoutPanelManager.MainApp.AppUserControl
{
    public partial class PopOutPanelList
    {
        public PopOutPanelList()
        {
            InitializeComponent();
        }
    }
    
    public class PopOutPanelDataTemplateSelector : DataTemplateSelector
    {
        public DataTemplate PopOutPanelDataTemplate { get; set; }
        
        public DataTemplate EmptyDataTemplate { get; set; }

        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            if (container is not FrameworkElement element || item is not PanelConfig panelConfig) 
                return null;

            if (panelConfig.PanelType != PanelType.RefocusDisplay)
                return element.FindResource("PopOutPanelDataTemplate") as DataTemplate;
            
            return element.FindResource("EmptyDataTemplate") as DataTemplate;
        }
    }

    public class EditPopOutPanelSourceTemplateSelector : DataTemplateSelector
    {
        public DataTemplate EditPopOutPanelSourceTemplate { get; set; }

        public DataTemplate EmptyDataTemplate { get; set; }

        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            if (container is not FrameworkElement element || item is not PanelConfig panelConfig)
                return null;

            if (panelConfig.PanelType != PanelType.RefocusDisplay)
                return element.FindResource("EditPopOutPanelSourceTemplate") as DataTemplate;

            return element.FindResource("EmptyDataTemplate") as DataTemplate;
        }
    }
}
