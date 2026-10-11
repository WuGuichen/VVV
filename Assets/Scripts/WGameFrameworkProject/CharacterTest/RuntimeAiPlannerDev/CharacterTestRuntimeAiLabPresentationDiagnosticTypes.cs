using System;
using System.Collections.Generic;
using MxFramework.Runtime;
using MxFramework.Vfx;
using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    internal readonly struct CharacterTestRuntimeAiLabFrameSyncVfxRuntimeSample
    {
        public CharacterTestRuntimeAiLabFrameSyncVfxRuntimeSample(
            bool runtimeAvailable,
            string moduleId,
            long definitionHash,
            long nextInstanceId,
            int activeCount,
            int pendingSpawnCount,
            int pendingStopCount,
            int pendingEventCount,
            CharacterTestRuntimeAiLabFrameSyncVfxInstanceSample[] activeInstances)
        {
            RuntimeAvailable = runtimeAvailable;
            ModuleId = moduleId ?? string.Empty;
            DefinitionHash = definitionHash;
            NextInstanceId = nextInstanceId;
            ActiveCount = activeCount;
            PendingSpawnCount = pendingSpawnCount;
            PendingStopCount = pendingStopCount;
            PendingEventCount = pendingEventCount;
            ActiveInstances = activeInstances
                ?? Array.Empty<CharacterTestRuntimeAiLabFrameSyncVfxInstanceSample>();
        }

        public bool RuntimeAvailable { get; }
        public string ModuleId { get; }
        public long DefinitionHash { get; }
        public long NextInstanceId { get; }
        public int ActiveCount { get; }
        public int PendingSpawnCount { get; }
        public int PendingStopCount { get; }
        public int PendingEventCount { get; }
        public IReadOnlyList<CharacterTestRuntimeAiLabFrameSyncVfxInstanceSample> ActiveInstances { get; }

        public static CharacterTestRuntimeAiLabFrameSyncVfxRuntimeSample Capture(
            VfxRuntimeModule runtime)
        {
            if (runtime == null)
                return default;

            VfxDebugSnapshot snapshot = runtime.CreateDebugSnapshot();
            var active = new List<VfxInstanceSnapshot>(snapshot.ActiveCount);
            runtime.CopyActiveInstances(active);
            var samples = new CharacterTestRuntimeAiLabFrameSyncVfxInstanceSample[active.Count];
            for (int i = 0; i < active.Count; i++)
                samples[i] = CharacterTestRuntimeAiLabFrameSyncVfxInstanceSample.Capture(active[i]);

            return new CharacterTestRuntimeAiLabFrameSyncVfxRuntimeSample(
                runtimeAvailable: true,
                moduleId: snapshot.ModuleId,
                definitionHash: snapshot.DefinitionHash,
                nextInstanceId: snapshot.NextInstanceId,
                activeCount: snapshot.ActiveCount,
                pendingSpawnCount: snapshot.PendingSpawnCount,
                pendingStopCount: snapshot.PendingStopCount,
                pendingEventCount: snapshot.PendingEventCount,
                activeInstances: samples);
        }
    }

    internal readonly struct CharacterTestRuntimeAiLabFrameSyncVfxInstanceSample
    {
        public CharacterTestRuntimeAiLabFrameSyncVfxInstanceSample(
            long instanceId,
            int definitionId,
            string definitionHash,
            string prefabResource,
            long startFrame,
            long expireFrame,
            int anchorKind,
            int anchorSourceId,
            int anchorTargetId,
            int anchorSocketId,
            long anchorPositionXRaw,
            long anchorPositionYRaw,
            long anchorPositionZRaw,
            long anchorForwardXRaw,
            long anchorForwardYRaw,
            long anchorForwardZRaw,
            int parameter0,
            int parameter1,
            int parameter2,
            int parameter3,
            int sourceDomain,
            long sourceFrame,
            int localFrame,
            int sourceOrder,
            int triggerId,
            int sourceId,
            int targetId,
             RuntimeTraceIdentity traceId)
        {
            InstanceId = instanceId;
            DefinitionId = definitionId;
            DefinitionHash = definitionHash ?? string.Empty;
            PrefabResource = prefabResource ?? string.Empty;
            StartFrame = startFrame;
            ExpireFrame = expireFrame;
            AnchorKind = anchorKind;
            AnchorSourceId = anchorSourceId;
            AnchorTargetId = anchorTargetId;
            AnchorSocketId = anchorSocketId;
            AnchorPositionXRaw = anchorPositionXRaw;
            AnchorPositionYRaw = anchorPositionYRaw;
            AnchorPositionZRaw = anchorPositionZRaw;
            AnchorForwardXRaw = anchorForwardXRaw;
            AnchorForwardYRaw = anchorForwardYRaw;
            AnchorForwardZRaw = anchorForwardZRaw;
            Parameter0 = parameter0;
            Parameter1 = parameter1;
            Parameter2 = parameter2;
            Parameter3 = parameter3;
            SourceDomain = sourceDomain;
            SourceFrame = sourceFrame;
            LocalFrame = localFrame;
            SourceOrder = sourceOrder;
            TriggerId = triggerId;
            SourceId = sourceId;
            TargetId = targetId;
             TraceId = traceId;
        }

        public long InstanceId { get; }
        public int DefinitionId { get; }
        public string DefinitionHash { get; }
        public string PrefabResource { get; }
        public long StartFrame { get; }
        public long ExpireFrame { get; }
        public int AnchorKind { get; }
        public int AnchorSourceId { get; }
        public int AnchorTargetId { get; }
        public int AnchorSocketId { get; }
        public long AnchorPositionXRaw { get; }
        public long AnchorPositionYRaw { get; }
        public long AnchorPositionZRaw { get; }
        public long AnchorForwardXRaw { get; }
        public long AnchorForwardYRaw { get; }
        public long AnchorForwardZRaw { get; }
        public int Parameter0 { get; }
        public int Parameter1 { get; }
        public int Parameter2 { get; }
        public int Parameter3 { get; }
        public int SourceDomain { get; }
        public long SourceFrame { get; }
        public int LocalFrame { get; }
        public int SourceOrder { get; }
        public int TriggerId { get; }
        public int SourceId { get; }
        public int TargetId { get; }
        public RuntimeTraceIdentity TraceId { get; }

        public static CharacterTestRuntimeAiLabFrameSyncVfxInstanceSample Capture(
            VfxInstanceSnapshot instance)
        {
            if (instance == null)
                return default;

            VfxAnchor anchor = instance.Anchor;
            VfxParameterBlock parameters = instance.Parameters;
            return new CharacterTestRuntimeAiLabFrameSyncVfxInstanceSample(
                instanceId: instance.InstanceId.Value,
                definitionId: instance.DefinitionId.Value,
                definitionHash: instance.DefinitionHash,
                prefabResource: instance.PrefabResource.ToString(),
                startFrame: instance.StartFrame.Value,
                expireFrame: instance.ExpireFrame.Value,
                anchorKind: (int)anchor.Kind,
                anchorSourceId: anchor.SourceId,
                anchorTargetId: anchor.TargetId,
                anchorSocketId: anchor.SocketId,
                anchorPositionXRaw: anchor.Pose.Position.RawX,
                anchorPositionYRaw: anchor.Pose.Position.RawY,
                anchorPositionZRaw: anchor.Pose.Position.RawZ,
                anchorForwardXRaw: anchor.Pose.Forward.RawX,
                anchorForwardYRaw: anchor.Pose.Forward.RawY,
                anchorForwardZRaw: anchor.Pose.Forward.RawZ,
                parameter0: parameters.Value0,
                parameter1: parameters.Value1,
                parameter2: parameters.Value2,
                parameter3: parameters.Value3,
                sourceDomain: instance.SourceDomain,
                sourceFrame: instance.SourceFrame,
                localFrame: instance.LocalFrame,
                sourceOrder: instance.SourceOrder,
                triggerId: instance.TriggerId,
                sourceId: instance.SourceId,
                targetId: instance.TargetId,
                 traceId: instance.TraceId);
        }
    }

    internal readonly struct CharacterTestRuntimeAiLabFrameSyncOverlaySample
    {
        public CharacterTestRuntimeAiLabFrameSyncOverlaySample(
            bool available,
            int frame,
            int agentEntityId,
            int targetEntityId,
            Vector3 sourceCenter,
            Vector3 targetCenter,
            Vector3 moveEnd,
            float attackRange)
        {
            Available = available;
            Frame = frame;
            AgentEntityId = agentEntityId;
            TargetEntityId = targetEntityId;
            SourceCenter = sourceCenter;
            TargetCenter = targetCenter;
            MoveEnd = moveEnd;
            AttackRange = attackRange;
        }

        public bool Available { get; }
        public int Frame { get; }
        public int AgentEntityId { get; }
        public int TargetEntityId { get; }
        public Vector3 SourceCenter { get; }
        public Vector3 TargetCenter { get; }
        public Vector3 MoveEnd { get; }
        public float AttackRange { get; }

        public static CharacterTestRuntimeAiLabFrameSyncOverlaySample FromPreview(
            CharacterTestRuntimeAiPlannerDevPreviewItem preview)
        {
            return new CharacterTestRuntimeAiLabFrameSyncOverlaySample(
                preview.Available,
                preview.Frame,
                preview.AgentEntityId,
                preview.TargetEntityId,
                preview.SourceCenter,
                preview.TargetCenter,
                preview.MoveEnd,
                preview.AttackRange);
        }
    }

}
