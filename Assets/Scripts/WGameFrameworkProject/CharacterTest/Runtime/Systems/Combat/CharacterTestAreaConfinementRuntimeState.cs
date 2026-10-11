using System;
using System.Collections.Generic;
using MxFramework.Core.Math;

namespace MxFramework.Game.CharacterTest
{
    public enum CharacterTestAreaConfinementLandingMode
    {
        None = 0,
        LockedTarget = 1,
        CasterForward = 2
    }

    internal static class CharacterTestAreaConfinementLandingModeText
    {
        public static string Format(CharacterTestAreaConfinementLandingMode value)
        {
            switch (value)
            {
                case CharacterTestAreaConfinementLandingMode.None: return string.Empty;
                case CharacterTestAreaConfinementLandingMode.LockedTarget: return "locked_target";
                case CharacterTestAreaConfinementLandingMode.CasterForward: return "caster_forward";
                default: throw new ArgumentOutOfRangeException(nameof(value));
            }
        }
    }

    internal sealed class CharacterTestAreaConfinementRuntimeState
    {
        public const int CurrentSchemaVersion = 2;
        public const int DefaultModuleId = 1101;
        public const string DefaultModuleLabel = "CharacterTest.AreaConfinement";
        public const int SourceRuntimeModuleId = 1103;

        public CharacterTestAreaConfinementRuntimeState(
            int schemaVersion,
            int moduleId,
            int nextPendingChannelId,
            IReadOnlyList<CharacterTestAreaConfinementPendingChannelSnapshot> pendingChannels,
            CharacterTestMechanicSourceRuntimeState sourceRuntime,
            bool borrow = false)
        {
            SchemaVersion = schemaVersion;
            ModuleId = moduleId;
            NextPendingChannelId = nextPendingChannelId <= 0 ? 1 : nextPendingChannelId;
            PendingChannels = borrow ? pendingChannels : Copy(pendingChannels);
            SourceRuntime = sourceRuntime ?? new CharacterTestMechanicSourceRuntimeState(
                CharacterTestMechanicSourceRuntimeState.CurrentSchemaVersion,
                SourceRuntimeModuleId,
                nextRuntimeSourceId: 1,
                activeSources: null);
        }

        public string ModuleLabel => DefaultModuleLabel;
        public int SchemaVersion { get; }
        public int ModuleId { get; }
        public int NextPendingChannelId { get; internal set; }
        public IReadOnlyList<CharacterTestAreaConfinementPendingChannelSnapshot> PendingChannels { get; }
        public CharacterTestMechanicSourceRuntimeState SourceRuntime { get; internal set; }
        public int PendingCount => PendingChannels.Count;
        public int ActiveZoneCount => SourceRuntime.ActiveCount;

        private static CharacterTestAreaConfinementPendingChannelSnapshot[] Copy(
            IReadOnlyList<CharacterTestAreaConfinementPendingChannelSnapshot> pendingChannels)
        {
            if (pendingChannels == null || pendingChannels.Count == 0)
                return Array.Empty<CharacterTestAreaConfinementPendingChannelSnapshot>();

            var copy = new CharacterTestAreaConfinementPendingChannelSnapshot[pendingChannels.Count];
            for (int i = 0; i < pendingChannels.Count; i++)
                copy[i] = pendingChannels[i];
            return copy;
        }
    }

    internal readonly struct CharacterTestAreaConfinementPendingChannelSnapshot
    {
        public CharacterTestAreaConfinementPendingChannelSnapshot(
            int pendingChannelId,
            int casterEntityId,
            int sourceGameplayEntityIndex,
            int sourceGameplayEntityGeneration,
            int sourceCombatEntityId,
            long centerXRaw,
            long centerYRaw,
            long centerZRaw,
            long forwardXRaw,
            long forwardYRaw,
            long forwardZRaw,
            CharacterTestAreaConfinementLandingMode landingMode,
            long startFrame,
            long activationFrame,
            int localFrame,
            int sourceOrder,
            int traceId)
        {
            PendingChannelId = pendingChannelId <= 0 ? 1 : pendingChannelId;
            CasterEntityId = casterEntityId < 0 ? 0 : casterEntityId;
            SourceGameplayEntityIndex = sourceGameplayEntityIndex < 0 ? 0 : sourceGameplayEntityIndex;
            SourceGameplayEntityGeneration = sourceGameplayEntityGeneration < 0 ? 0 : sourceGameplayEntityGeneration;
            SourceCombatEntityId = sourceCombatEntityId < 0 ? 0 : sourceCombatEntityId;
            CenterXRaw = centerXRaw;
            CenterYRaw = centerYRaw;
            CenterZRaw = centerZRaw;
            ForwardXRaw = forwardXRaw;
            ForwardYRaw = forwardYRaw;
            ForwardZRaw = forwardZRaw;
            LandingMode = landingMode;
            StartFrame = startFrame < 0L ? 0L : startFrame;
            ActivationFrame = activationFrame < 0L ? 0L : activationFrame;
            LocalFrame = localFrame < 0 ? 0 : localFrame;
            SourceOrder = sourceOrder < 0 ? 0 : sourceOrder;
            TraceId = traceId;
        }

        public int PendingChannelId { get; }
        public int CasterEntityId { get; }
        public int SourceGameplayEntityIndex { get; }
        public int SourceGameplayEntityGeneration { get; }
        public int SourceCombatEntityId { get; }
        public long CenterXRaw { get; }
        public long CenterYRaw { get; }
        public long CenterZRaw { get; }
        public long ForwardXRaw { get; }
        public long ForwardYRaw { get; }
        public long ForwardZRaw { get; }
        public CharacterTestAreaConfinementLandingMode LandingMode { get; }
        public long StartFrame { get; }
        public long ActivationFrame { get; }
        public int LocalFrame { get; }
        public int SourceOrder { get; }
        public int TraceId { get; }

        public FixVector3 Center => new FixVector3(
            Fix64.FromRaw(CenterXRaw),
            Fix64.FromRaw(CenterYRaw),
            Fix64.FromRaw(CenterZRaw));

        public FixVector3 Forward => new FixVector3(
            Fix64.FromRaw(ForwardXRaw),
            Fix64.FromRaw(ForwardYRaw),
            Fix64.FromRaw(ForwardZRaw));

        public static CharacterTestAreaConfinementPendingChannelSnapshot FromVectors(
            int pendingChannelId,
            int casterEntityId,
            int sourceGameplayEntityIndex,
            int sourceGameplayEntityGeneration,
            int sourceCombatEntityId,
            FixVector3 center,
            FixVector3 forward,
            CharacterTestAreaConfinementLandingMode landingMode,
            long startFrame,
            long activationFrame,
            int localFrame,
            int sourceOrder,
            int traceId)
        {
            return new CharacterTestAreaConfinementPendingChannelSnapshot(
                pendingChannelId,
                casterEntityId,
                sourceGameplayEntityIndex,
                sourceGameplayEntityGeneration,
                sourceCombatEntityId,
                center.X.RawValue,
                center.Y.RawValue,
                center.Z.RawValue,
                forward.X.RawValue,
                forward.Y.RawValue,
                forward.Z.RawValue,
                landingMode,
                startFrame,
                activationFrame,
                localFrame,
                sourceOrder,
                traceId);
        }
    }
}


