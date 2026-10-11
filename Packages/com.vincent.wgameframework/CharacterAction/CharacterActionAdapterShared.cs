using MxFramework.Runtime;
using System;

namespace MxFramework.CharacterAction
{
    internal static class CharacterActionAdapterShared
    {
        private static readonly CharacterActionDiagnostic MotionSinkMissing = CreateMissingSink("Motion");
        private static readonly CharacterActionDiagnostic RootMotionSinkMissing = CreateMissingSink("RootMotion");
        private static readonly CharacterActionDiagnostic CombatSinkMissing = CreateMissingSink("Combat");
        private static readonly CharacterActionDiagnostic GameplaySinkMissing = CreateMissingSink("Gameplay");
        private static readonly CharacterActionDiagnostic PressureOnlyReactionSinkMissing = CreateMissingSink("PressureOnlyReaction");
        private static readonly CharacterActionDiagnostic DebugSinkMissing = CreateMissingSink("Debug");
        private static readonly CharacterActionDiagnostic AnimationSinkMissing = CreateMissingSink("Animation");
        private static readonly CharacterActionDiagnostic AudioSinkMissing = CreateMissingSink("Audio");
        private static readonly CharacterActionDiagnostic VfxSinkMissing = CreateMissingSink("VFX");
        private static readonly CharacterActionDiagnostic CameraSinkMissing = CreateMissingSink("Camera");
        private static readonly CharacterActionDiagnostic UiFeedbackSinkMissing = CreateMissingSink("UIFeedback");

        internal static bool HasErrors(CharacterActionDiagnostic[] diagnostics)
        {
            diagnostics = diagnostics ?? Array.Empty<CharacterActionDiagnostic>();
            for (int i = 0; i < diagnostics.Length; i++)
            {
                if (diagnostics[i].Severity == CharacterActionDiagnosticSeverity.Error)
                    return true;
            }

            return false;
        }

        internal static CharacterActionAdapterDispatchMetadata CreateMetadata(
            CharacterActionRunnerEvent runnerEvent,
            CharacterActionTrackDispatchEvent dispatch,
            CharacterActionAdapterContext context)
        {
            RuntimeTraceIdentity traceId = context.TraceId.IsNone ? runnerEvent.TraceId : context.TraceId;
            return new CharacterActionAdapterDispatchMetadata(
                context.Frame,
                runnerEvent.InstanceId,
                runnerEvent.PlanId,
                runnerEvent.ActionId,
                runnerEvent.LocalFrame,
                dispatch.StableEventId,
                context.SourceId,
                traceId,
                runnerEvent.TrackLifecycle.Phase,
                runnerEvent.TrackLifecycle.Boundary,
                runnerEvent.TrackLifecycle.ExitReason);
        }

        internal static CharacterActionAdapterDispatchMetadata CreateMetadata(
            CharacterActionRunnerEvent runnerEvent,
            CharacterActionTrackDispatchEvent dispatch,
            CharacterActionPresentationAdapterContext context)
        {
            RuntimeTraceIdentity traceId = context.TraceId.IsNone ? runnerEvent.TraceId : context.TraceId;
            return new CharacterActionAdapterDispatchMetadata(
                context.Frame,
                runnerEvent.InstanceId,
                runnerEvent.PlanId,
                runnerEvent.ActionId,
                runnerEvent.LocalFrame,
                dispatch.StableEventId,
                context.SourceId,
                traceId,
                runnerEvent.TrackLifecycle.Phase,
                runnerEvent.TrackLifecycle.Boundary,
                runnerEvent.TrackLifecycle.ExitReason);
        }

        internal static CharacterActionDiagnostic MissingSink(string sinkName)
        {
            switch (sinkName)
            {
                case "Motion": return MotionSinkMissing;
                case "RootMotion": return RootMotionSinkMissing;
                case "Combat": return CombatSinkMissing;
                case "Gameplay": return GameplaySinkMissing;
                case "PressureOnlyReaction": return PressureOnlyReactionSinkMissing;
                case "Debug": return DebugSinkMissing;
                case "Animation": return AnimationSinkMissing;
                case "Audio": return AudioSinkMissing;
                case "VFX": return VfxSinkMissing;
                case "Camera": return CameraSinkMissing;
                case "UIFeedback": return UiFeedbackSinkMissing;
                default: return CreateMissingSink(sinkName);
            }
        }

        internal static CharacterActionDiagnostic PayloadMissing(string message)
        {
            return CharacterActionDiagnostic.Error(
                CharacterActionDiagnosticCode.AdapterPayloadMissing,
                message);
        }

        private static CharacterActionDiagnostic CreateMissingSink(string sinkName)
        {
            return CharacterActionDiagnostic.Error(
                CharacterActionDiagnosticCode.AdapterSinkMissing,
                sinkName + " request sink is missing.");
        }
    }
}
