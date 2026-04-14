using System;
using System.Collections.Generic;

namespace MSFSPopoutPanelManager.Shared
{
    public class StatusMessageWriter
    {
        public static object Lock { get; set; } = new object();

        private static readonly List<StatusMessage> Messages = new();

        public static event EventHandler<List<StatusMessage>> OnStatusMessage;

        public static void WriteMessage(string message, StatusMessageType statusMessageType)
        {
            lock (Lock)
            {
                Messages.Add(new StatusMessage { Message = message, StatusMessageType = statusMessageType });
            }

            if (IsEnabled)
                OnStatusMessage?.Invoke(null, Messages);
        }

        public static void WriteMessageWithNewLine(string message, StatusMessageType statusMessageType)
        {
            lock (Lock)
            {
                Messages.Add(new StatusMessage { Message = message, StatusMessageType = statusMessageType, NewLine = true });
            }

            if (IsEnabled)
                OnStatusMessage?.Invoke(null, Messages);
        }

        public static void WriteExecutingStatusMessage()
        {
            lock (Lock)
            {
                Messages.Add(new StatusMessage { Message = "  (Executing)", StatusMessageType = StatusMessageType.Executing, NewLine = false });
            }

            if (IsEnabled)
                OnStatusMessage?.Invoke(null, Messages);
        }

        public static void WriteOkStatusMessage()
        {
            lock (Lock)
            {
                if (Messages.Count > 1)
                {
                    Messages.RemoveAt(Messages.Count - 1);
                    Messages.Add(new StatusMessage { Message = "  (OK)", StatusMessageType = StatusMessageType.Success, NewLine = true });
                }
            }

            if (IsEnabled)
                OnStatusMessage?.Invoke(null, Messages);
        }

        public static void WriteFailureStatusMessage()
        {
            lock (Lock)
            {
                if (Messages.Count > 1)
                {
                    Messages.RemoveAt(Messages.Count - 1);
                    Messages.Add(new StatusMessage { Message = "  (FAILED)", StatusMessageType = StatusMessageType.Failure, NewLine = true });
                }
            }

            if (IsEnabled)
                OnStatusMessage?.Invoke(null, Messages);
        }

        public static void RemoveLastMessage()
        {
            lock (Lock)
            {
                Messages.RemoveAt(Messages.Count - 1);
            }
        }

        public static void ClearMessage()
        {
            Messages.Clear();
        }

        public static bool IsEnabled { get; set; }
    }
}
