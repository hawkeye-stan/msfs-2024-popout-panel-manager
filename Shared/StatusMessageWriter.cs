using System;
using System.Collections.Generic;

namespace MSFSPopoutPanelManager.Shared
{
    public class StatusMessageWriter
    {
        public static event EventHandler<List<StatusMessage>> OnStatusMessage;

        public static event EventHandler OnStatusMessageClear;

        public static void WriteMessageWithNewLine(string message, StatusMessageType statusMessageType)
        {
            var messages = new List<StatusMessage>
            {
                new() { Message = message, StatusMessageType = statusMessageType, NewLine = true }
            };
            
            OnStatusMessage?.Invoke(null, messages);
        }

        public static void WriteExecutingStatusMessage(string message)
        {
            var messages = new List<StatusMessage>
            {
                new() { Message = message, StatusMessageType = StatusMessageType.Info },
                new() { Message = "  (Executing)", StatusMessageType = StatusMessageType.Executing }
            };
            
            OnStatusMessage?.Invoke(null, messages);
        }

        public static void WriteOkStatusMessage()
        {
            var messages = new List<StatusMessage> 
            {
                new() { Message = "  (OK)", StatusMessageType = StatusMessageType.Success, NewLine = true }
            };
            
            OnStatusMessage?.Invoke(null, messages);
        }

        public static void WriteFailureStatusMessage()
        {
            var messages = new List<StatusMessage>
            {
                new() { Message = "  (FAILED)", StatusMessageType = StatusMessageType.Failure, NewLine = true }
            };

            OnStatusMessage?.Invoke(null, messages);
        }

        public static void ClearMessage()
        {
            OnStatusMessageClear?.Invoke(null, EventArgs.Empty);
        }
    }
}
