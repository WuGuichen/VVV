namespace MxFramework.Story
{
    public enum StoryDirectorResultCode
    {
        Success = 0,
        InvalidGraphId = 1001,
        InvalidBeatId = 1002,
        InvalidTriggerId = 1003,
        InvalidBeatInstanceId = 1004,
        InvalidStepId = 1005,
        InvalidChoiceId = 1006,
        InvalidDefinition = 1010,
        GraphNotLoaded = 1101,
        BeatNotFound = 1102,
        TriggerNotFound = 1103,
        BeatInstanceNotLive = 1104,
        ChoiceNotOffered = 1105,
        ChoiceNotFound = 1106,
        ChoiceDisabled = 1107,
        PresentationNotWaiting = 1108,
        PresentationStepMismatch = 1109,
        UnsupportedSchemaVersion = 1201,
        DirectorGuardExceeded = 1301
    }

    public readonly struct StoryLoadGraphResult
    {
        public StoryLoadGraphResult(bool success, StoryDirectorResultCode code, int graphId, string message)
        {
            Success = success;
            Code = code;
            GraphId = graphId;
            Message = message ?? string.Empty;
        }

        public bool Success { get; }
        public StoryDirectorResultCode Code { get; }
        public int GraphId { get; }
        public string Message { get; }

        public static StoryLoadGraphResult Succeeded(int graphId)
        {
            return new StoryLoadGraphResult(true, StoryDirectorResultCode.Success, graphId, string.Empty);
        }

        public static StoryLoadGraphResult Failed(StoryDirectorResultCode code, int graphId, string message)
        {
            return new StoryLoadGraphResult(false, code, graphId, message);
        }
    }

    public readonly struct StoryEnterBeatResult
    {
        public StoryEnterBeatResult(bool success, StoryDirectorResultCode code, int graphId, int beatId, int beatInstanceId, string message)
        {
            Success = success;
            Code = code;
            GraphId = graphId;
            BeatId = beatId;
            BeatInstanceId = beatInstanceId;
            Message = message ?? string.Empty;
        }

        public bool Success { get; }
        public StoryDirectorResultCode Code { get; }
        public int GraphId { get; }
        public int BeatId { get; }
        public int BeatInstanceId { get; }
        public string Message { get; }

        public static StoryEnterBeatResult Succeeded(int graphId, int beatId, int beatInstanceId)
        {
            return new StoryEnterBeatResult(true, StoryDirectorResultCode.Success, graphId, beatId, beatInstanceId, string.Empty);
        }

        public static StoryEnterBeatResult Failed(StoryDirectorResultCode code, int graphId, int beatId, string message)
        {
            return new StoryEnterBeatResult(false, code, graphId, beatId, 0, message);
        }
    }

    public readonly struct StoryTriggerResult
    {
        public StoryTriggerResult(bool success, StoryDirectorResultCode code, int triggerId, int graphId, int beatId, int beatInstanceId, string message)
        {
            Success = success;
            Code = code;
            TriggerId = triggerId;
            GraphId = graphId;
            BeatId = beatId;
            BeatInstanceId = beatInstanceId;
            Message = message ?? string.Empty;
        }

        public bool Success { get; }
        public StoryDirectorResultCode Code { get; }
        public int TriggerId { get; }
        public int GraphId { get; }
        public int BeatId { get; }
        public int BeatInstanceId { get; }
        public string Message { get; }

        public static StoryTriggerResult Succeeded(int triggerId, StoryEnterBeatResult enterResult)
        {
            return new StoryTriggerResult(true, StoryDirectorResultCode.Success, triggerId, enterResult.GraphId, enterResult.BeatId, enterResult.BeatInstanceId, string.Empty);
        }

        public static StoryTriggerResult Failed(StoryDirectorResultCode code, int triggerId, string message)
        {
            return new StoryTriggerResult(false, code, triggerId, 0, 0, 0, message);
        }
    }

    public readonly struct StoryTickResult
    {
        public StoryTickResult(bool success, StoryDirectorResultCode code, string message)
        {
            Success = success;
            Code = code;
            Message = message ?? string.Empty;
        }

        public bool Success { get; }
        public StoryDirectorResultCode Code { get; }
        public string Message { get; }

        public static StoryTickResult Succeeded()
        {
            return new StoryTickResult(true, StoryDirectorResultCode.Success, string.Empty);
        }
    }

    public readonly struct StoryChoiceResult
    {
        public StoryChoiceResult(bool success, StoryDirectorResultCode code, int beatInstanceId, int choiceId, string message)
        {
            Success = success;
            Code = code;
            BeatInstanceId = beatInstanceId;
            ChoiceId = choiceId;
            Message = message ?? string.Empty;
        }

        public bool Success { get; }
        public StoryDirectorResultCode Code { get; }
        public int BeatInstanceId { get; }
        public int ChoiceId { get; }
        public string Message { get; }

        public static StoryChoiceResult Succeeded(int beatInstanceId, int choiceId)
        {
            return new StoryChoiceResult(true, StoryDirectorResultCode.Success, beatInstanceId, choiceId, string.Empty);
        }

        public static StoryChoiceResult Failed(StoryDirectorResultCode code, int beatInstanceId, int choiceId, string message)
        {
            return new StoryChoiceResult(false, code, beatInstanceId, choiceId, message);
        }
    }

    public readonly struct StoryPresentationResult
    {
        public StoryPresentationResult(bool success, StoryDirectorResultCode code, int beatInstanceId, int stepId, string message)
        {
            Success = success;
            Code = code;
            BeatInstanceId = beatInstanceId;
            StepId = stepId;
            Message = message ?? string.Empty;
        }

        public bool Success { get; }
        public StoryDirectorResultCode Code { get; }
        public int BeatInstanceId { get; }
        public int StepId { get; }
        public string Message { get; }

        public static StoryPresentationResult Succeeded(int beatInstanceId, int stepId)
        {
            return new StoryPresentationResult(true, StoryDirectorResultCode.Success, beatInstanceId, stepId, string.Empty);
        }

        public static StoryPresentationResult Failed(StoryDirectorResultCode code, int beatInstanceId, int stepId, string message)
        {
            return new StoryPresentationResult(false, code, beatInstanceId, stepId, message);
        }
    }

    public readonly struct StoryAbortResult
    {
        public StoryAbortResult(bool success, StoryDirectorResultCode code, int graphId, int reason, string message)
        {
            Success = success;
            Code = code;
            GraphId = graphId;
            Reason = reason;
            Message = message ?? string.Empty;
        }

        public bool Success { get; }
        public StoryDirectorResultCode Code { get; }
        public int GraphId { get; }
        public int Reason { get; }
        public string Message { get; }

        public static StoryAbortResult Succeeded(int graphId, int reason)
        {
            return new StoryAbortResult(true, StoryDirectorResultCode.Success, graphId, reason, string.Empty);
        }

        public static StoryAbortResult Failed(StoryDirectorResultCode code, int graphId, int reason, string message)
        {
            return new StoryAbortResult(false, code, graphId, reason, message);
        }
    }
}
