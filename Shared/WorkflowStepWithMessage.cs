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

            StatusMessageWriter.WriteExecutingStatusMessage(message);

            var result = await function.Invoke();

            if (result)
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

            StatusMessageWriter.WriteExecutingStatusMessage(message);

            var result = function.Invoke();

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

            StatusMessageWriter.WriteExecutingStatusMessage(message);

            function.Invoke();

            StatusMessageWriter.WriteOkStatusMessage();
        }
    }
}
