using System;
using MxFramework.Gameplay;
using MxFramework.Runtime;

namespace MxFramework.Story.GameplayBridge
{
    public enum StoryGameplayEffectIntentKind
    {
        RuntimeCommand = 0,
        BuffGrant = 1,
        BuffRemove = 2
    }

    public readonly struct StoryGameplayEffectIntent
    {
        public StoryGameplayEffectIntent(
            int commandId,
            int sourceId,
            StoryGameplayEntityRef targetRef,
            int payload0 = 0,
            int payload1 = 0,
            int payload2 = 0,
            int delayFrames = 0,
            int targetId = 0,
            string traceId = "",
            StoryGameplayEffectIntentKind kind = StoryGameplayEffectIntentKind.RuntimeCommand)
        {
            Kind = kind;
            CommandId = commandId;
            SourceId = sourceId;
            TargetId = targetId;
            Payload0 = payload0;
            Payload1 = payload1;
            Payload2 = payload2;
            DelayFrames = delayFrames;
            TargetRef = targetRef;
            TraceId = traceId ?? string.Empty;
        }

        public StoryGameplayEffectIntentKind Kind { get; }
        public int CommandId { get; }
        public int SourceId { get; }
        public int TargetId { get; }
        public int Payload0 { get; }
        public int Payload1 { get; }
        public int Payload2 { get; }
        public int DelayFrames { get; }
        public StoryGameplayEntityRef TargetRef { get; }
        public string TraceId { get; }

        public static StoryGameplayEffectIntent SetComponentAttribute(
            StoryGameplayEntityRef targetRef,
            int sourceId,
            int attributeId,
            int value,
            int delayFrames = 0,
            string traceId = "")
        {
            return new StoryGameplayEffectIntent(
                GameplayRuntimeCommandIds.SetComponentAttribute,
                sourceId,
                targetRef,
                payload1: attributeId,
                payload2: value,
                delayFrames: delayFrames,
                traceId: traceId);
        }

        public static StoryGameplayEffectIntent AddComponentAttribute(
            StoryGameplayEntityRef targetRef,
            int sourceId,
            int attributeId,
            int delta,
            int delayFrames = 0,
            string traceId = "")
        {
            return new StoryGameplayEffectIntent(
                GameplayRuntimeCommandIds.AddComponentAttribute,
                sourceId,
                targetRef,
                payload1: attributeId,
                payload2: delta,
                delayFrames: delayFrames,
                traceId: traceId);
        }

        public static StoryGameplayEffectIntent CastComponentAbility(
            StoryGameplayEntityRef targetRef,
            int sourceId,
            int abilityId,
            int delayFrames = 0,
            string traceId = "")
        {
            return new StoryGameplayEffectIntent(
                GameplayRuntimeCommandIds.CastComponentAbility,
                sourceId,
                targetRef,
                payload1: abilityId,
                delayFrames: delayFrames,
                traceId: traceId);
        }

        public static StoryGameplayEffectIntent CastLegacyAbility(
            StoryGameplayEntityRef casterRef,
            int sourceId,
            int abilityId,
            int candidateEntityId = 0,
            int delayFrames = 0,
            string traceId = "")
        {
            return new StoryGameplayEffectIntent(
                GameplayRuntimeCommandIds.CastAbility,
                sourceId,
                casterRef,
                payload1: abilityId,
                payload2: candidateEntityId,
                delayFrames: delayFrames,
                traceId: traceId);
        }

        public static StoryGameplayEffectIntent BuffGrant(
            StoryGameplayEntityRef targetRef,
            int sourceId,
            int buffId,
            int stackCount = 1,
            int delayFrames = 0,
            string traceId = "")
        {
            return new StoryGameplayEffectIntent(
                0,
                sourceId,
                targetRef,
                payload0: buffId,
                payload1: stackCount,
                delayFrames: delayFrames,
                traceId: traceId,
                kind: StoryGameplayEffectIntentKind.BuffGrant);
        }

        public static StoryGameplayEffectIntent BuffRemove(
            StoryGameplayEntityRef targetRef,
            int sourceId,
            int buffId,
            int delayFrames = 0,
            string traceId = "")
        {
            return new StoryGameplayEffectIntent(
                0,
                sourceId,
                targetRef,
                payload0: buffId,
                delayFrames: delayFrames,
                traceId: traceId,
                kind: StoryGameplayEffectIntentKind.BuffRemove);
        }
    }

    public sealed class StoryGameplayEffectBridge
    {
        private readonly RuntimeCommandBuffer _gameplayCommandBuffer;
        private readonly GameplayComponentWorld _componentWorld;
        private readonly StoryBeatGameplayLocator _locator;
        private readonly StoryGameplayBridgeDiagnostics _diagnostics;

        public StoryGameplayEffectBridge(
            RuntimeCommandBuffer gameplayCommandBuffer,
            GameplayComponentWorld componentWorld = null,
            StoryBeatGameplayLocator locator = null,
            StoryGameplayBridgeDiagnostics diagnostics = null)
        {
            _gameplayCommandBuffer = gameplayCommandBuffer ?? throw new ArgumentNullException(nameof(gameplayCommandBuffer));
            _componentWorld = componentWorld;
            _diagnostics = diagnostics;
            _locator = locator ?? new StoryBeatGameplayLocator(diagnostics);
        }

        public StoryGameplayEffectResult EnqueueGameplayEffect(
            in StoryGameplayEffectIntent intent,
            RuntimeFrame currentStoryFrame)
        {
            if (intent.Kind == StoryGameplayEffectIntentKind.BuffGrant ||
                intent.Kind == StoryGameplayEffectIntentKind.BuffRemove)
            {
                return Rejected(new StoryGameplayBridgeDiagnostic(
                    StoryGameplayBridgeDiagnosticCode.UnsupportedBuffEffect,
                    "Direct Story buff grant/remove is deferred until Gameplay owns an explicit buff command.",
                    intent.TargetRef,
                    intent.CommandId));
            }

            if (!TryBuildTargetFrame(currentStoryFrame, intent.DelayFrames, out RuntimeFrame targetFrame, out StoryGameplayBridgeDiagnostic frameDiagnostic))
            {
                return Rejected(frameDiagnostic);
            }

            StoryGameplayEntityResolutionResult entity = _locator.ResolveRef(intent.TargetRef, _componentWorld);
            if (!entity.Success)
            {
                _diagnostics?.RecordRejectedEffect(entity.Diagnostic);
                return StoryGameplayEffectResult.Rejected(entity.Diagnostic);
            }

            if (!TryCreateCommand(intent, targetFrame, entity, out RuntimeCommand command, out StoryGameplayBridgeDiagnostic commandDiagnostic))
            {
                return Rejected(commandDiagnostic);
            }

            RuntimeCommandValidationResult enqueue = _gameplayCommandBuffer.Enqueue(command);
            if (!enqueue.Success)
            {
                return Rejected(new StoryGameplayBridgeDiagnostic(
                    StoryGameplayBridgeDiagnosticCode.CommandEnqueueFailed,
                    enqueue.Error.Message,
                    intent.TargetRef,
                    intent.CommandId), enqueue);
            }

            _diagnostics?.RecordEnqueuedCommand();
            return StoryGameplayEffectResult.Enqueued(enqueue.Command, enqueue);
        }

        private bool TryCreateCommand(
            in StoryGameplayEffectIntent intent,
            RuntimeFrame targetFrame,
            in StoryGameplayEntityResolutionResult entity,
            out RuntimeCommand command,
            out StoryGameplayBridgeDiagnostic diagnostic)
        {
            command = default;
            diagnostic = StoryGameplayBridgeDiagnostic.None;

            switch (intent.CommandId)
            {
                case GameplayRuntimeCommandIds.SetComponentAttribute:
                    if (!TryRequireComponentEntity(intent, entity, out GameplayEntityId setEntity, out diagnostic) ||
                        !TryRequirePositive(intent.Payload1, "Component attribute id must be positive.", intent, out diagnostic))
                    {
                        return false;
                    }

                    command = GameplayRuntimeCommandFactory.SetComponentAttribute(
                        targetFrame,
                        setEntity,
                        intent.Payload1,
                        intent.Payload2,
                        intent.SourceId,
                        intent.TraceId);
                    return true;

                case GameplayRuntimeCommandIds.AddComponentAttribute:
                    if (!TryRequireComponentEntity(intent, entity, out GameplayEntityId addEntity, out diagnostic) ||
                        !TryRequirePositive(intent.Payload1, "Component attribute id must be positive.", intent, out diagnostic))
                    {
                        return false;
                    }

                    command = GameplayRuntimeCommandFactory.AddComponentAttribute(
                        targetFrame,
                        addEntity,
                        intent.Payload1,
                        intent.Payload2,
                        intent.SourceId,
                        intent.TraceId);
                    return true;

                case GameplayRuntimeCommandIds.CastComponentAbility:
                    if (!TryRequireComponentEntity(intent, entity, out GameplayEntityId casterEntity, out diagnostic) ||
                        !TryRequirePositive(intent.Payload1, "Component ability id must be positive.", intent, out diagnostic))
                    {
                        return false;
                    }

                    command = GameplayRuntimeCommandFactory.CastComponentAbility(
                        targetFrame,
                        casterEntity,
                        intent.Payload1,
                        intent.SourceId,
                        intent.TraceId);
                    return true;

                case GameplayRuntimeCommandIds.CastAbility:
                    if (entity.LegacyRuntimeEntityId <= 0)
                    {
                        diagnostic = new StoryGameplayBridgeDiagnostic(
                            StoryGameplayBridgeDiagnosticCode.InvalidEffectIntent,
                            "Legacy CastAbility requires a legacy runtime entity ref.",
                            intent.TargetRef,
                            intent.CommandId);
                        return false;
                    }

                    if (!TryRequirePositive(intent.Payload1, "Legacy ability id must be positive.", intent, out diagnostic))
                    {
                        return false;
                    }

                    command = GameplayRuntimeCommandFactory.CastAbility(
                        targetFrame,
                        entity.LegacyRuntimeEntityId,
                        intent.Payload1,
                        intent.Payload2,
                        intent.SourceId,
                        intent.TraceId);
                    return true;

                default:
                    diagnostic = new StoryGameplayBridgeDiagnostic(
                        StoryGameplayBridgeDiagnosticCode.UnsupportedEffectIntent,
                        "Story gameplay effect intent does not map to a supported Gameplay-owned command.",
                        intent.TargetRef,
                        intent.CommandId);
                    return false;
            }
        }

        private static bool TryRequireComponentEntity(
            in StoryGameplayEffectIntent intent,
            in StoryGameplayEntityResolutionResult entity,
            out GameplayEntityId entityId,
            out StoryGameplayBridgeDiagnostic diagnostic)
        {
            entityId = entity.ComponentEntityId;
            if (entityId.IsValid)
            {
                diagnostic = StoryGameplayBridgeDiagnostic.None;
                return true;
            }

            diagnostic = new StoryGameplayBridgeDiagnostic(
                StoryGameplayBridgeDiagnosticCode.InvalidEffectIntent,
                "Component Gameplay command requires a component entity ref.",
                intent.TargetRef,
                intent.CommandId);
            return false;
        }

        private static bool TryRequirePositive(
            int value,
            string message,
            in StoryGameplayEffectIntent intent,
            out StoryGameplayBridgeDiagnostic diagnostic)
        {
            if (value > 0)
            {
                diagnostic = StoryGameplayBridgeDiagnostic.None;
                return true;
            }

            diagnostic = new StoryGameplayBridgeDiagnostic(
                StoryGameplayBridgeDiagnosticCode.InvalidEffectIntent,
                message,
                intent.TargetRef,
                intent.CommandId);
            return false;
        }

        private static bool TryBuildTargetFrame(
            RuntimeFrame currentStoryFrame,
            int delayFrames,
            out RuntimeFrame targetFrame,
            out StoryGameplayBridgeDiagnostic diagnostic)
        {
            long delay = delayFrames < 0 ? 0L : delayFrames;
            if (long.MaxValue - currentStoryFrame.Value < delay)
            {
                targetFrame = default;
                diagnostic = new StoryGameplayBridgeDiagnostic(
                    StoryGameplayBridgeDiagnosticCode.FrameOverflow,
                    "Story gameplay effect target frame overflowed.");
                return false;
            }

            targetFrame = new RuntimeFrame(currentStoryFrame.Value + delay);
            diagnostic = StoryGameplayBridgeDiagnostic.None;
            return true;
        }

        private StoryGameplayEffectResult Rejected(StoryGameplayBridgeDiagnostic diagnostic)
        {
            return Rejected(diagnostic, default);
        }

        private StoryGameplayEffectResult Rejected(
            StoryGameplayBridgeDiagnostic diagnostic,
            RuntimeCommandValidationResult enqueueValidation)
        {
            _diagnostics?.RecordRejectedEffect(diagnostic);
            return StoryGameplayEffectResult.Rejected(diagnostic, enqueueValidation);
        }
    }

    public readonly struct StoryGameplayEffectResult
    {
        private StoryGameplayEffectResult(
            bool success,
            RuntimeCommand command,
            StoryGameplayBridgeDiagnostic diagnostic,
            RuntimeCommandValidationResult enqueueValidation)
        {
            Success = success;
            Command = command;
            Diagnostic = diagnostic;
            EnqueueValidation = enqueueValidation;
        }

        public bool Success { get; }
        public RuntimeCommand Command { get; }
        public StoryGameplayBridgeDiagnostic Diagnostic { get; }
        public RuntimeCommandValidationResult EnqueueValidation { get; }

        public static StoryGameplayEffectResult Enqueued(
            RuntimeCommand command,
            RuntimeCommandValidationResult enqueueValidation)
        {
            return new StoryGameplayEffectResult(true, command, StoryGameplayBridgeDiagnostic.None, enqueueValidation);
        }

        public static StoryGameplayEffectResult Rejected(
            StoryGameplayBridgeDiagnostic diagnostic,
            RuntimeCommandValidationResult enqueueValidation = default)
        {
            return new StoryGameplayEffectResult(false, default, diagnostic, enqueueValidation);
        }
    }
}
