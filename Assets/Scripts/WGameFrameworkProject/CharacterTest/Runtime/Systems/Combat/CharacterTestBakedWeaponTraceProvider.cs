using System;
using System.Collections.Generic;
using System.Globalization;
using MxFramework.Animation;
using MxFramework.CharacterApplication;
using MxFramework.CharacterAction;
using MxFramework.CharacterRuntimeSpawn;
using MxFramework.Combat.Animation;
using MxFramework.Combat.Core;
using MxFramework.Combat.Damage;
using MxFramework.Combat.GameplayBridge;
using MxFramework.Combat.Hit;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using MxFramework.Gameplay;
using MxFramework.Resources;
using MxFramework.Runtime;
using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    internal sealed class CharacterTestBakedWeaponTraceProvider : ICombatActionTraceProvider
    {
        private readonly ICharacterTestBakedWeaponTraceSource _source;
        private readonly ICharacterTestWeaponTraceBindingResolver _bindingResolver;
        private readonly Action<int, CharacterActionId, string, string, int, string, string> _reportError;
        private readonly Dictionary<CharacterTestPreparedTraceKey, CharacterTestPreparedTrace> _preparedTraces =
            new Dictionary<CharacterTestPreparedTraceKey, CharacterTestPreparedTrace>();
        private CharacterTestCharacterRuntimeWorld _world;
        private CharacterTestCharacterPhysicsRuntime _physicsRuntime;
        private IReadOnlyList<CharacterTestActiveHitTrace> _traces = Array.Empty<CharacterTestActiveHitTrace>();
        private List<CharacterTestAttackTraceDebugFrame> _debugFrames;
        private long _debugRuntimeFrame;
        private readonly Dictionary<int, SourceMotionWitness> _sourceMotion = new Dictionary<int, SourceMotionWitness>();
        private readonly List<int> _sourceMotionRemovals = new List<int>();

        // Frame-local scratch: rebuilt from restored authority before every Simulation tick.
        // It is never a cross-frame history or a second network/snapshot authority.
        internal void CaptureSourceMotionStart(CharacterTestCharacterRuntimeWorld world, RuntimeFrame frame)
        {
            if (world == null) { ClearSourceMotion(); return; }
            for (int i = 0; i < world.Entities.Count; i++)
            {
                var entity = world.Entities[i];
                if (!_sourceMotion.TryGetValue(entity.Id.Value, out var witness))
                {
                    witness = new SourceMotionWitness();
                    _sourceMotion.Add(entity.Id.Value, witness);
                }
                witness.Capture(entity, frame.Value);
            }
            _sourceMotionRemovals.Clear();
            foreach (var pair in _sourceMotion)
                if (pair.Value.Frame != frame.Value) _sourceMotionRemovals.Add(pair.Key);
            for (int i = 0; i < _sourceMotionRemovals.Count; i++) _sourceMotion.Remove(_sourceMotionRemovals[i]);
        }

        internal void ClearSourceMotion()
        { _sourceMotion.Clear(); _sourceMotionRemovals.Clear(); }

        private sealed class SourceMotionWitness
        {
            internal CharacterTestCharacterEntity Entity;
            internal long Frame;
            internal CharacterTestDirectHitTraceSourcePose Pose;
            internal readonly Dictionary<int, FixVector3> SocketOffsets = new Dictionary<int, FixVector3>();
            internal void Capture(CharacterTestCharacterEntity entity, long frame)
            {
                Entity = entity;
                Frame = frame;
                Pose = CharacterTestDirectHitTraceSourcePose.FromTransform(0, entity.Transform);
                SocketOffsets.Clear();
                var bindings = entity.AuthorityPose?.Definition.SecondaryBindings;
                if (bindings == null) return;
                for (int i = 0; i < bindings.Count; i++)
                    if (entity.AuthorityPose.TryGetSocketOffset(bindings[i].SocketId, out var offset))
                        SocketOffsets[bindings[i].SocketId] = offset;
            }
        }

        public CharacterTestBakedWeaponTraceProvider(
            ICharacterTestBakedWeaponTraceSource source,
            ICharacterTestWeaponTraceBindingResolver bindingResolver,
            Action<int, CharacterActionId, string, string, int, string, string> reportError)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _bindingResolver = bindingResolver ?? throw new ArgumentNullException(nameof(bindingResolver));
            _reportError = reportError;
        }

        public void Configure(
            CharacterTestCharacterRuntimeWorld world,
            CharacterTestCharacterPhysicsRuntime physicsRuntime,
            IReadOnlyList<CharacterTestActiveHitTrace> traces,
            List<CharacterTestAttackTraceDebugFrame> debugFrames,
            long debugRuntimeFrame)
        {
            _preparedTraces.Clear();
            _world = world;
            _physicsRuntime = physicsRuntime;
            _traces = traces ?? Array.Empty<CharacterTestActiveHitTrace>();
            _debugFrames = debugFrames;
            _debugRuntimeFrame = debugRuntimeFrame;
        }

        public bool TryPrepare(
            CharacterTestActiveHitTrace trace,
            CharacterTestCharacterEntity entity,
            string clipKey,
            int localFrame,
            out CharacterTestWeaponTraceBinding binding)
        {
            if (!_bindingResolver.TryResolve(entity, trace.EquipmentSlotId, trace.TraceProfileId, out binding))
            {
                _reportError?.Invoke(
                    entity != null ? entity.Id.Value : 0,
                    trace.ActionId,
                    trace.DescribeSelector(),
                    clipKey,
                    localFrame,
                    "HitTraceWeaponBindingMissing",
                    "Current equipment does not expose a weapon trace binding for this hit trace selector.");
                return false;
            }

            if (!_source.TryGetFrameForEntity(entity, clipKey, binding.TraceProfileId, localFrame, out MxAnimationBakedWeaponTraceFrame frame))
            {
                _reportError?.Invoke(
                    entity != null ? entity.Id.Value : 0,
                    trace.ActionId,
                    binding.TraceProfileId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    clipKey,
                    localFrame,
                    "HitTraceBakeFrameMissing",
                    "Baked weapon trace frame is missing. No static capsule fallback is used.");
                return false;
            }

            _preparedTraces[new CharacterTestPreparedTraceKey(
                trace.HitWindowId,
                localFrame,
                trace.EquipmentSlotId,
                trace.TraceProfileId)] =
                new CharacterTestPreparedTrace(clipKey, frame, binding);
            return true;
        }

        public void GetActiveTraces(
            CombatEntityId entityId,
            int actionId,
            int actionInstanceId,
            int localFrame,
            List<WeaponTraceFrame> results)
        {
            GetActiveTraces(
                entityId,
                actionId,
                actionInstanceId,
                localFrame,
                results,
                null);
        }

        public void GetActiveTraces(
            CombatEntityId entityId,
            int actionId,
            int actionInstanceId,
            int localFrame,
            List<WeaponTraceFrame> results,
            List<int> policyEventIds,
            List<WeaponTraceFrame> incomingMotionFrames = null)
        {
            if (results == null || _world == null || _physicsRuntime == null)
                return;

            for (int i = 0; i < _traces.Count; i++)
            {
                CharacterTestActiveHitTrace trace = _traces[i];
                if (trace.HitWindowId != actionInstanceId)
                    continue;

                CharacterTestCharacterEntity entity = FindEntity(trace.EntityId);
                if (entity == null || !_physicsRuntime.GetEntityId(entity).Equals(entityId))
                    continue;

                var key = new CharacterTestPreparedTraceKey(
                    actionInstanceId,
                    localFrame,
                    trace.EquipmentSlotId,
                    trace.TraceProfileId);
                if (!_preparedTraces.TryGetValue(key, out CharacterTestPreparedTrace prepared))
                    continue;

                WeaponTraceFrame weaponTrace = BuildTraceFrame(
                    prepared.Binding.TraceProfileId,
                    prepared.Frame,
                    prepared.Binding,
                    entity,
                    localFrame,
                    CharacterTestCombatHitEventResolver.ResolveQueryLayerMask(trace.HitEvent, _physicsRuntime));
                results.Add(weaponTrace);
                if (incomingMotionFrames != null)
                {
                    // A trace without a same-frame beginning witness must not fabricate a direction.
                    TryGetIncomingMotionFrame(entity, _debugRuntimeFrame, prepared.Binding.TraceProfileId,
                        prepared.Frame, prepared.Binding, weaponTrace.TargetMask, out var motion);
                    incomingMotionFrames.Add(motion);
                }
                policyEventIds?.Add(CharacterTestCharacterCombatTraceSystem.StablePolicyEventId(
                    trace.ActionId,
                    trace.StableEventId));
                _debugFrames?.Add(CharacterTestAttackTraceDebugFrame.FromWeaponTrace(
                    _debugRuntimeFrame,
                    trace,
                    prepared.Binding.TraceProfileId,
                    localFrame,
                    weaponTrace));
            }
        }

        // NoAlloc after capture capacity is established. An id/frame match alone cannot authorize
        // a witness belonging to another world or to an entity recreated during this tick.
        internal bool TryGetIncomingMotionFrame(CharacterTestCharacterEntity entity, long runtimeFrame, int traceProfileId,
            MxAnimationBakedWeaponTraceFrame frame, CharacterTestWeaponTraceBinding binding,
            CombatPhysicsLayerMask targetMask, out WeaponTraceFrame motion)
        {
            motion = default;
            if (entity == null || !_sourceMotion.TryGetValue(entity.Id.Value, out var witness)
                || witness.Frame != runtimeFrame || !ReferenceEquals(witness.Entity, entity)) return false;
            witness.SocketOffsets.TryGetValue(frame.SocketId, out var previousOffset);
            motion = BuildIncomingMotionFrame(traceProfileId, frame, binding, witness.Pose, previousOffset,
                CharacterTestDirectHitTraceSourcePose.FromTransform(frame.LocalFrame, entity.Transform),
                ResolveAuthorityPoseSocketOffset(entity, frame.SocketId), targetMask);
            return true;
        }

        private CharacterTestCharacterEntity FindEntity(int entityId)
        {
            IReadOnlyList<CharacterTestCharacterEntity> entities = _world.Entities;
            for (int i = 0; i < entities.Count; i++)
            {
                if (entities[i].Id.Value == entityId)
                    return entities[i];
            }

            return null;
        }

        internal static WeaponTraceFrame BuildTraceFrame(
            int traceProfileId,
            MxAnimationBakedWeaponTraceFrame frame,
            CharacterTestWeaponTraceBinding binding,
            CharacterTestCharacterEntity entity,
            CombatPhysicsLayerMask targetMask)
        {
            return BuildTraceFrame(
                traceProfileId,
                frame,
                binding,
                entity,
                0,
                targetMask);
        }

        internal static WeaponTraceFrame BuildTraceFrame(
            int traceProfileId,
            MxAnimationBakedWeaponTraceFrame frame,
            CharacterTestWeaponTraceBinding binding,
            CharacterTestCharacterEntity entity,
            int localFrame,
            CombatPhysicsLayerMask targetMask)
        {
            CharacterTestDirectHitTraceSourcePose sourcePose =
                CharacterTestDirectHitTraceSourcePose.FromTransform(localFrame, entity.Transform);
            FixVector3 authorityOffset = ResolveAuthorityPoseSocketOffset(entity, frame.SocketId);
            return BuildTraceFrame(traceProfileId, frame, binding, sourcePose, authorityOffset, targetMask);
        }

        internal static WeaponTraceFrame BuildTraceFrame(
            int traceProfileId,
            MxAnimationBakedWeaponTraceFrame frame,
            CharacterTestWeaponTraceBinding binding,
            CharacterTestDirectHitTraceSourcePose sourcePose,
            FixVector3 authorityOffset,
            CombatPhysicsLayerMask targetMask)
        {
            FixVector3 rootPrevLocal = ToFixVector3(frame.RootPrev) + authorityOffset;
            FixVector3 rootNowLocal = ToFixVector3(frame.RootNow) + authorityOffset;
            FixVector3 tipPrevLocal = ToFixVector3(frame.TipPrev) + authorityOffset;
            FixVector3 tipNowLocal = ToFixVector3(frame.TipNow) + authorityOffset;
            FixVector3 rootPrev = sourcePose.TransformLocal(rootPrevLocal);
            FixVector3 rootNow = sourcePose.TransformLocal(rootNowLocal);
            FixVector3 tipPrev = sourcePose.TransformLocal(tipPrevLocal);
            FixVector3 tipNow = sourcePose.TransformLocal(tipNowLocal);

            return new WeaponTraceFrame(
                CharacterTestCharacterCombatTraceSystem.StablePositiveId(traceProfileId),
                rootPrev,
                tipPrev,
                rootNow,
                tipNow,
                binding.RadiusFix,
                targetMask,
                binding.ProximalExtensionFix,
                binding.ProximalRadiusFix);
        }

        /// <summary>Two authority poses for incoming motion only; existing collision queries are unchanged.</summary>
        internal static WeaponTraceFrame BuildIncomingMotionFrame(int traceProfileId, MxAnimationBakedWeaponTraceFrame frame,
            CharacterTestWeaponTraceBinding binding, CharacterTestDirectHitTraceSourcePose previousPose, FixVector3 previousOffset,
            CharacterTestDirectHitTraceSourcePose currentPose, FixVector3 currentOffset, CombatPhysicsLayerMask targetMask)
        {
            return new WeaponTraceFrame(CharacterTestCharacterCombatTraceSystem.StablePositiveId(traceProfileId),
                previousPose.TransformLocal(ToFixVector3(frame.RootPrev) + previousOffset),
                previousPose.TransformLocal(ToFixVector3(frame.TipPrev) + previousOffset),
                currentPose.TransformLocal(ToFixVector3(frame.RootNow) + currentOffset),
                currentPose.TransformLocal(ToFixVector3(frame.TipNow) + currentOffset),
                binding.RadiusFix, targetMask, binding.ProximalExtensionFix, binding.ProximalRadiusFix);
        }

        private static FixVector3 ResolveAuthorityPoseSocketOffset(
            CharacterTestCharacterEntity entity,
            int socketId)
        {
            return entity?.AuthorityPose != null
                && entity.AuthorityPose.TryGetSocketOffset(socketId, out FixVector3 offset)
                    ? offset
                    : FixVector3.Zero;
        }

        private static FixVector3 ToFixVector3(MxAnimationBakedVector3 value)
        {
            return new FixVector3(
                Fix64.FromRaw(value.X * Fix64.Scale / 1000000L),
                Fix64.FromRaw(value.Y * Fix64.Scale / 1000000L),
                Fix64.FromRaw(value.Z * Fix64.Scale / 1000000L));
        }
    }
}
