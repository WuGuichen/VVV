using System;
using System.Collections.Generic;
using MxFramework.Runtime;

namespace MxFramework.Story.Runtime
{
    public static class StoryRuntimeCommandIds
    {
        public const int MinStoryCommandId = 1003000;
        public const int MaxStoryCommandId = 1003999;
        public const int RaiseTrigger = 1003001;
        public const int SelectChoice = 1003002;
        public const int CompletePresentation = 1003003;
        public const int RequestEnterBeat = 1003004;
        public const int AbortGraph = 1003005;

        public static bool IsStoryCommandId(int commandId)
        {
            return commandId >= MinStoryCommandId && commandId <= MaxStoryCommandId;
        }
    }

    public static class StoryRuntimeCommandSources
    {
        public const int Input = 1003101;
        public const int GameplayBridge = 1003102;
        public const int UnityAdapter = 1003103;
        public const int UiAdapter = 1003104;
        public const int PresentationAdapter = 1003105;
        public const int TestDriver = 1003190;
        public const int Debug = 1003191;
        public const int System = 1003192;
    }

    public static class StoryRuntimeCommandFactory
    {
        public static RuntimeCommand RaiseTrigger(
            RuntimeFrame frame,
            int sourceId,
            int triggerId,
            int param0 = 0,
            int param1 = 0,
            int targetId = 0,
            string traceId = "")
        {
            return new RuntimeCommand(
                frame,
                sourceId,
                StoryRuntimeCommandIds.RaiseTrigger,
                targetId,
                triggerId,
                param0,
                param1,
                traceId);
        }

        public static RuntimeCommand SelectChoice(
            RuntimeFrame frame,
            int sourceId,
            int beatInstanceId,
            int choiceId,
            int graphId = 0,
            string traceId = "")
        {
            return new RuntimeCommand(
                frame,
                sourceId,
                StoryRuntimeCommandIds.SelectChoice,
                graphId,
                beatInstanceId,
                choiceId,
                0,
                traceId);
        }

        public static RuntimeCommand CompletePresentation(
            RuntimeFrame frame,
            int sourceId,
            int beatInstanceId,
            int stepId,
            int graphId = 0,
            string traceId = "")
        {
            return new RuntimeCommand(
                frame,
                sourceId,
                StoryRuntimeCommandIds.CompletePresentation,
                graphId,
                beatInstanceId,
                stepId,
                0,
                traceId);
        }

        public static RuntimeCommand RequestEnterBeat(
            RuntimeFrame frame,
            int sourceId,
            int graphId,
            int beatId,
            string traceId = "")
        {
            return new RuntimeCommand(
                frame,
                sourceId,
                StoryRuntimeCommandIds.RequestEnterBeat,
                0,
                graphId,
                beatId,
                0,
                traceId);
        }

        public static RuntimeCommand AbortGraph(
            RuntimeFrame frame,
            int sourceId,
            int graphId,
            int reason,
            string traceId = "")
        {
            return new RuntimeCommand(
                frame,
                sourceId,
                StoryRuntimeCommandIds.AbortGraph,
                0,
                graphId,
                reason,
                0,
                traceId);
        }
    }

    public static class StoryRuntimeCommandRegistry
    {
        public static RuntimeCommandRegistry CreateDefault()
        {
            var registry = new RuntimeCommandRegistry();
            registry.Register(new RuntimeCommandDefinition(
                StoryRuntimeCommandIds.RaiseTrigger,
                "Story.RaiseTrigger",
                new RuntimeCommandPayloadSchema("payload0=triggerId>0 payload1=param0 payload2=param1 targetId>=0", command => command.Payload0 > 0 && command.TargetId >= 0)));
            registry.Register(new RuntimeCommandDefinition(
                StoryRuntimeCommandIds.SelectChoice,
                "Story.SelectChoice",
                new RuntimeCommandPayloadSchema("payload0=beatInstanceId>0 payload1=choiceId>0 payload2=0 targetId>=0", command => command.Payload0 > 0 && command.Payload1 > 0 && command.Payload2 == 0 && command.TargetId >= 0)));
            registry.Register(new RuntimeCommandDefinition(
                StoryRuntimeCommandIds.CompletePresentation,
                "Story.CompletePresentation",
                new RuntimeCommandPayloadSchema("payload0=beatInstanceId>0 payload1=stepId>0 payload2=0 targetId>=0", command => command.Payload0 > 0 && command.Payload1 > 0 && command.Payload2 == 0 && command.TargetId >= 0)));
            registry.Register(new RuntimeCommandDefinition(
                StoryRuntimeCommandIds.RequestEnterBeat,
                "Story.RequestEnterBeat",
                new RuntimeCommandPayloadSchema("payload0=graphId>0 payload1=beatId>0 payload2=0 targetId=0", command => command.Payload0 > 0 && command.Payload1 > 0 && command.Payload2 == 0 && command.TargetId == 0)));
            registry.Register(new RuntimeCommandDefinition(
                StoryRuntimeCommandIds.AbortGraph,
                "Story.AbortGraph",
                new RuntimeCommandPayloadSchema("payload0=graphId>0 payload1=reason payload2=0 targetId=0", command => command.Payload0 > 0 && command.Payload2 == 0 && command.TargetId == 0)));
            return registry;
        }
    }

    public sealed class StoryRuntimeCommandValidator : IRuntimeCommandValidator
    {
        private readonly RuntimeCommandRegistryValidator _registryValidator;
        private readonly StoryDirector _director;
        private readonly HashSet<int> _debugOrSystemSourceIds;

        public StoryRuntimeCommandValidator(StoryDirector director)
            : this(director, StoryRuntimeCommandRegistry.CreateDefault(), null)
        {
        }

        public StoryRuntimeCommandValidator(
            StoryDirector director,
            RuntimeCommandRegistry registry,
            IEnumerable<int> debugOrSystemSourceIds = null)
        {
            _director = director ?? throw new ArgumentNullException(nameof(director));
            _registryValidator = new RuntimeCommandRegistryValidator(registry ?? throw new ArgumentNullException(nameof(registry)));
            _debugOrSystemSourceIds = new HashSet<int>();

            if (debugOrSystemSourceIds == null)
            {
                _debugOrSystemSourceIds.Add(StoryRuntimeCommandSources.Debug);
                _debugOrSystemSourceIds.Add(StoryRuntimeCommandSources.System);
                _debugOrSystemSourceIds.Add(StoryRuntimeCommandSources.TestDriver);
            }
            else
            {
                foreach (int sourceId in debugOrSystemSourceIds)
                {
                    _debugOrSystemSourceIds.Add(sourceId);
                }
            }
        }

        public RuntimeCommandValidationResult Validate(RuntimeCommand command)
        {
            RuntimeCommandValidationResult registry = _registryValidator.Validate(command);
            if (!registry.Success)
            {
                return registry;
            }

            switch (command.CommandId)
            {
                case StoryRuntimeCommandIds.RaiseTrigger:
                    return ValidateRaiseTrigger(command);
                case StoryRuntimeCommandIds.SelectChoice:
                    return ValidateSelectChoice(command);
                case StoryRuntimeCommandIds.CompletePresentation:
                    return ValidateCompletePresentation(command);
                case StoryRuntimeCommandIds.RequestEnterBeat:
                    return ValidateRequestEnterBeat(command);
                case StoryRuntimeCommandIds.AbortGraph:
                    return ValidateAbortGraph(command);
                default:
                    return Invalid(command, "Runtime command id is not a Story command id.");
            }
        }

        private RuntimeCommandValidationResult ValidateRaiseTrigger(RuntimeCommand command)
        {
            return command.Payload0 > 0
                ? RuntimeCommandValidationResult.Accepted(command)
                : Invalid(command, "Story.RaiseTrigger requires payload0 triggerId > 0.");
        }

        private RuntimeCommandValidationResult ValidateSelectChoice(RuntimeCommand command)
        {
            StoryChoiceResult choice = _director.CanResolveChoice(command.Payload0, command.Payload1);
            if (!choice.Success)
            {
                return Invalid(command, "Story.SelectChoice rejected by Director: " + choice.Code + ".");
            }

            RuntimeCommandValidationResult targetGraph = ValidateTargetGraph(command, "Story.SelectChoice");
            return targetGraph.Success
                ? RuntimeCommandValidationResult.Accepted(command)
                : targetGraph;
        }

        private RuntimeCommandValidationResult ValidateCompletePresentation(RuntimeCommand command)
        {
            StoryPresentationResult presentation = _director.CanCompletePresentation(command.Payload0, command.Payload1);
            if (!presentation.Success)
            {
                return Invalid(command, "Story.CompletePresentation rejected by Director: " + presentation.Code + ".");
            }

            RuntimeCommandValidationResult targetGraph = ValidateTargetGraph(command, "Story.CompletePresentation");
            return targetGraph.Success
                ? RuntimeCommandValidationResult.Accepted(command)
                : targetGraph;
        }

        private RuntimeCommandValidationResult ValidateRequestEnterBeat(RuntimeCommand command)
        {
            if (!_debugOrSystemSourceIds.Contains(command.SourceId))
            {
                return Invalid(command, "Story.RequestEnterBeat requires a whitelisted debug/system source id.");
            }

            return _director.CanEnterBeat(command.Payload0, command.Payload1)
                ? RuntimeCommandValidationResult.Accepted(command)
                : Invalid(command, "Story.RequestEnterBeat targets an unloaded graph or missing beat.");
        }

        private RuntimeCommandValidationResult ValidateAbortGraph(RuntimeCommand command)
        {
            if (!_debugOrSystemSourceIds.Contains(command.SourceId))
            {
                return Invalid(command, "Story.AbortGraph requires a whitelisted debug/system source id.");
            }

            return _director.IsGraphLoaded(command.Payload0)
                ? RuntimeCommandValidationResult.Accepted(command)
                : Invalid(command, "Story.AbortGraph targets an unloaded graph.");
        }

        private RuntimeCommandValidationResult ValidateTargetGraph(RuntimeCommand command, string commandName)
        {
            if (command.TargetId == 0)
            {
                return RuntimeCommandValidationResult.Accepted(command);
            }

            StoryDirectorSnapshot snapshot = _director.CreateSnapshot();
            for (int i = 0; i < snapshot.ActiveBeatInstances.Count; i++)
            {
                StoryBeatInstanceSnapshot beat = snapshot.ActiveBeatInstances[i];
                if (beat.BeatInstanceId == command.Payload0)
                {
                    return beat.GraphId == command.TargetId
                        ? RuntimeCommandValidationResult.Accepted(command)
                        : Invalid(command, commandName + " target graph id does not match the live beat instance graph.");
                }
            }

            return Invalid(command, commandName + " target beat instance is not live.");
        }

        private static RuntimeCommandValidationResult Invalid(RuntimeCommand command, string message)
        {
            return RuntimeCommandValidationResult.Failed(new RuntimeCommandError(
                RuntimeCommandErrorCode.InvalidPayload,
                command,
                RuntimeFrame.Zero,
                message));
        }
    }
}
