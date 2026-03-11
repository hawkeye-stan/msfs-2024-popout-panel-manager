using System;
using System.Threading.Tasks;

namespace MSFSPopoutPanelManager.Shared
{
    public static class WorkflowStepWithMessage
    {
        public static async Task<bool> Execute(string message, Func<Task<bool>> function, bool isSubTask = false)
        {
            if (isSubTask)
                message = "    - " + message;

            StatusMessageWriter.WriteMessage(message, StatusMessageType.Info);
            StatusMessageWriter.WriteExecutingStatusMessage();

            var result = function.Invoke();

            StatusMessageWriter.RemoveLastMessage();
            if (result.Result)
            {
                StatusMessageWriter.WriteOkStatusMessage();
                return true;
            }
           
            StatusMessageWriter.WriteFailureStatusMessage();
            return false;
        }

        public static bool Execute(string message, Func<bool> function, bool isSubTask = false)
        {
            if (isSubTask)
                message = "    - " + message;

            StatusMessageWriter.WriteMessage(message, StatusMessageType.Info);
            StatusMessageWriter.WriteExecutingStatusMessage();

            var result = function.Invoke();

            StatusMessageWriter.RemoveLastMessage();
            if (result)
            {
                StatusMessageWriter.WriteOkStatusMessage();
                return true;
            }

            StatusMessageWriter.WriteFailureStatusMessage();
            return false;
        }


        public static void Execute(string message, Action function, bool isSubTask = false)
        {
            if (isSubTask)
                message = "    - " + message;

            StatusMessageWriter.WriteMessage(message, StatusMessageType.Info);
            StatusMessageWriter.WriteExecutingStatusMessage();

            function.Invoke();

            StatusMessageWriter.RemoveLastMessage();
            StatusMessageWriter.WriteOkStatusMessage();
        }
    }
}
