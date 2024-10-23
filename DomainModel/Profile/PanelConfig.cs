﻿using MSFSPopoutPanelManager.Shared;
using Newtonsoft.Json;
using System;

namespace MSFSPopoutPanelManager.DomainModel.Profile
{
    public class PanelConfig : ObservableObject
    {
        public PanelConfig()
        {
            if (Id == Guid.Empty)
                Id = Guid.NewGuid();

            InitializeChildPropertyChangeBinding();

            PropertyChanged += PanelConfig_PropertyChanged;
        }

        private void PanelConfig_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            var arg = e as PropertyChangedExtendedEventArgs;

            switch (arg?.PropertyName)
            {
                case nameof(FullScreen) when FullScreen:
                    AlwaysOnTop = false;
                    HideTitlebar = false;
                    break;
                case nameof(TouchEnabled) when TouchEnabled:
                    AutoGameRefocus = true;
                    break;
            }
        }

        public Guid Id { get; set; }

        public PanelType PanelType { get; set; }

        public string PanelName { get; set; } = "Default Panel Name";

        public int Top { get; set; }

        public int Left { get; set; }

        public int Width { get; set; }

        public int Height { get; set; }

        public bool AlwaysOnTop { get; set; }

        public bool HideTitlebar { get; set; }

        public bool FullScreen { get; set; }

        public bool TouchEnabled { get; set; }

        public bool AutoGameRefocus { get; set; } = true;

        public FloatingPanel FloatingPanel { get; set; } = new();

        public PanelSource PanelSource { get; set; } = new();

        public FixedCameraConfig FixedCameraConfig { get; set; } = new();

        [JsonIgnore]
        public IntPtr PanelHandle { get; set; } = IntPtr.MaxValue;

        [JsonIgnore]
        public bool IsEditingPanel { get; set; }
        
        [JsonIgnore]
        public bool HasPanelSource => PanelType == PanelType.BuiltInPopout || (PanelType == PanelType.CustomPopout && PanelSource is { X: not null });

        [JsonIgnore]
        public bool? IsPopOutSuccess
        {
            get
            {
                if (PanelHandle == IntPtr.MaxValue)
                    return null;

                return PanelHandle != IntPtr.Zero;
            }
        }

        [JsonIgnore]
        public bool IsSelectedPanelSource { get; set; }

        [JsonIgnore]
        public bool IsShownPanelSource { get; set; }

        [JsonIgnore] public bool IsFloating { get; set; } = true;

        [JsonIgnore]
        public bool IsDeletablePanel => PanelType != PanelType.RefocusDisplay 
                                        && PanelType != PanelType.NumPadWindow 
                                        && PanelType != PanelType.SwitchWindow;

        [JsonIgnore]
        public bool IsPopoutPanel => PanelType is PanelType.CustomPopout or PanelType.NumPadWindow or PanelType.SwitchWindow;

        [JsonIgnore]
        public bool IsFloatablePanel => PanelType is PanelType.CustomPopout or PanelType.BuiltInPopout;

        [JsonIgnore]
        public bool IsCustomPopOut => PanelType == PanelType.CustomPopout;
        
        [JsonIgnore] 
        public bool IsBuiltInPopOut => PanelType == PanelType.BuiltInPopout;

        [JsonIgnore]
        public MonitorInfo FullScreenMonitorInfo { get; set; }
    }
}
