using MxFramework.Runtime;

namespace MxFramework.Story.Unity
{
    public readonly struct StoryUnityCommandResult
    {
        public StoryUnityCommandResult(
            bool success,
            RuntimeCommand command,
            RuntimeCommandError error,
            string message)
        {
            Success = success;
            Command = command;
            Error = error;
            Message = message ?? string.Empty;
        }

        public bool Success { get; }
        public RuntimeCommand Command { get; }
        public RuntimeCommandError Error { get; }
        public string Message { get; }

        public static StoryUnityCommandResult FromValidation(RuntimeCommandValidationResult validation)
        {
            return validation.Success
                ? new StoryUnityCommandResult(true, validation.Command, RuntimeCommandError.None, "enqueued")
                : new StoryUnityCommandResult(false, validation.Command, validation.Error, validation.Error.Message);
        }

        public static StoryUnityCommandResult NotConfigured(string adapterName)
        {
            string name = string.IsNullOrEmpty(adapterName) ? "Story Unity adapter" : adapterName;
            return new StoryUnityCommandResult(false, default, RuntimeCommandError.None, name + " is not bound to a RuntimeCommandBuffer.");
        }
    }
}
