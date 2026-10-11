using System;
using System.Collections.Generic;
using MxFramework.CharacterAction;
using MxFramework.Combat.Core;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using MxFramework.Runtime;
using MxFramework.Gameplay;
using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>Isolated static-reference session that uses the formal completed-frame Attack Opportunity chain.</summary>
    /// <remarks>LowFreqAlloc. No scene, view, live session or authored asset is mutated.</remarks>
    internal sealed class CharacterActionReferenceProbeSession : IDisposable
    {
        private readonly CharacterTestRuntimeAiPlannerAttackOpportunityAdapter _adapter;
        private readonly List<CharacterTestDirectHitTraceSourcePoseOffset> _nominalTrajectory = new List<CharacterTestDirectHitTraceSourcePoseOffset>();
        private string _trajectoryHash;
        private int _nextFrame;
        private bool _disposed;

        internal CharacterActionReferenceProbeSession(CharacterActionId actionId, CharacterActionReferenceDummy dummy,
            int sourceCharacterId = CharacterTestCharacterDefinitionRegistry.DefaultPlayableCharacterId,
            CharacterTestCharacterActionRuntime actionRuntime = null, AbilityRuntimeModule abilityRuntime = null)
            : this(actionId, dummy, sourceCharacterId, false, null, actionRuntime, abilityRuntime) { }

        /// <summary>Creates an isolated probe after resolving this character's real functional opener.</summary>
        internal static CharacterActionReferenceProbeSession ForCurrentOpener(int sourceCharacterId,
            CharacterActionReferenceDummy dummy, CharacterTestCharacterEquipmentComponent? equipment = null,
            CharacterTestCharacterActionRuntime actionRuntime = null, AbilityRuntimeModule abilityRuntime = null)
            => new CharacterActionReferenceProbeSession(default, dummy, sourceCharacterId, true, equipment, actionRuntime, abilityRuntime);

        internal static CharacterActionReferenceProbeSession ForTestAction(int sourceCharacterId,
            CharacterActionReferenceDummy dummy, int actionId, CharacterTestCharacterEquipmentComponent? equipment = null)
            => new CharacterActionReferenceProbeSession(new CharacterActionId(actionId), dummy, sourceCharacterId, actionId <= 0, equipment);

        private CharacterActionReferenceProbeSession(CharacterActionId actionId, CharacterActionReferenceDummy dummy,
            int sourceCharacterId, bool resolveOpener, CharacterTestCharacterEquipmentComponent? equipment,
            CharacterTestCharacterActionRuntime actionRuntime = null, AbilityRuntimeModule abilityRuntime = null)
        {
            if (dummy == null) throw new ArgumentNullException(nameof(dummy));
            ReferenceDummy = dummy;
            var discovered = new CharacterTestCharacterDefinitionRegistry();
            if (!discovered.TryGet(sourceCharacterId, out CharacterTestCharacterDefinition original))
                throw new ArgumentException("Source character definition is unavailable.", nameof(sourceCharacterId));
            var p = original.PhysicsProfile;
            var sourceProfile = new CharacterTestCharacterPhysicsComponent(p.CapsuleRadiusMeters, p.CapsuleHeightMeters,
                p.CapsuleCenterY, p.MassKg, p.MassScale, p.RadiusScale, blocksCharacters: false, canBePushed: false);
            var sourceDefinition = new CharacterTestCharacterDefinition(original.CharacterId, original.DisplayName,
                original.PrefabKey, 2, original.AnimationBinding, sourceProfile, original.GeneratedPackageRoot);
            var targetProfile = new CharacterTestCharacterPhysicsComponent(dummy.RadiusMillimeters / 1000f,
                dummy.HeightMillimeters / 1000f, dummy.CenterYMillimeters / 1000f, 80, blocksCharacters: false, canBePushed: false);
            var targetDefinition = new CharacterTestCharacterDefinition(int.MaxValue, "Reference Dummy", default, 1, default, targetProfile);
            var definitions = new CharacterTestCharacterDefinitionRegistry(new[] { sourceDefinition, targetDefinition });
            var world = new CombatPhysicsWorld();
            var ground = new CombatBodyId(CharacterTestCharacterPhysicsRuntime.StaticBodyIdStart);
            world.UpsertBody(new CombatPhysicsBody(new CombatEntityId(ground.Value), ground, FixVector3.Zero));
            world.UpsertAabbCollider(new CombatPhysicsAabbCollider(ground, new CombatColliderId(1), 1,
                new FixVector3(Fix64.FromInt(-100), Fix64.FromInt(-1), Fix64.FromInt(-100)),
                new FixVector3(Fix64.FromInt(100), Fix64.Zero, Fix64.FromInt(100))));
            var physics = new CharacterTestCharacterPhysicsRuntime(world, CombatPhysicsLayerMask.FromLayer(1),
                new FixVector3(Fix64.Half, Fix64.One, Fix64.Half), Fix64.Zero, Fix64.Zero,
                staticColliderCount: 1, sourceDescription: "nominal-reference-capsule");
            Runtime = new CharacterTestCharacterRuntimeModule(null, NullRuntimeLogger.Instance, definitions: definitions,
                physicsRuntime: physics, createViews: false, simulationTickRate: 60, actionRuntime: actionRuntime, abilityRuntimeModule: abilityRuntime);
            try
            {
                Source = Runtime.World.Spawn(new CharacterTestCharacterSpawnRequest(sourceDefinition.CharacterId, 2, 0, 0, 0,
                    CharacterTestCharacterControlOwner.None), sourceDefinition);
                // An empty default marker means retain the spawn pipeline's resolved default
                // equipment; replacing it with an empty stable key loses its actual ActionSet.
                if (equipment.HasValue && (!equipment.Value.UseDefaultLoadout
                    || !string.IsNullOrEmpty(equipment.Value.EquipmentStateStableId))) Source.SetEquipment(equipment.Value);
                CharacterActionConfig action;
                if (resolveOpener)
                {
                    if (!Runtime.ActionRuntime.TryGetEntityActionSet(Source.Id, out var actionSet))
                        throw new ArgumentException("Current character ActionSet is unavailable.");
                    if (!CharacterTestTacticalCardInputs.TryResolveOpener(actionSet, Runtime.ActionRuntime.Registry.Actions, out action, out string issue))
                        throw new ArgumentException(issue);
                }
                else if (!Runtime.ActionRuntime.Registry.TryGetAction(actionId, out action))
                    throw new ArgumentException("Action is absent from the formal runtime catalog.", nameof(actionId));
                Action = action;
                Target = Runtime.World.Spawn(new CharacterTestCharacterSpawnRequest(targetDefinition.CharacterId, 1, 0, 0, 0,
                    CharacterTestCharacterControlOwner.None), targetDefinition);
                Runtime.ConfigureReferenceDummyForMeasurement(Target.Id.Value, dummy);
                _adapter = new CharacterTestRuntimeAiPlannerAttackOpportunityAdapter(Runtime, nominalSourcePoseOffsets: _nominalTrajectory);
            }
            catch { Runtime.Dispose(); throw; }
        }

        internal CharacterTestCharacterRuntimeModule Runtime { get; }
        internal CharacterTestCharacterEntity Source { get; }
        internal CharacterTestCharacterEntity Target { get; }
        internal CharacterActionConfig Action { get; }
        internal CharacterActionReferenceDummy ReferenceDummy { get; }

        internal bool TryComputeActionHash(out string hash, out string issue)
        {
            if (_disposed) { hash = string.Empty; issue = "SessionDisposed"; return false; }
            return CharacterActionContentHashUtility.TryCompute(Action, Source, Runtime.PhysicsRuntime,
                Runtime.CombatTraceSystem.BakedTraceSource, Runtime.CombatTraceSystem.WeaponTraceBindingResolver, out hash, out issue);
        }

        internal bool TryEvaluate(int surfaceDistanceMillimeters, out CharacterTestAttackOpportunityResult result, out string issue)
        {
            result = default; issue = string.Empty;
            if (_disposed || surfaceDistanceMillimeters < 0) { issue = "InvalidProbeRequest"; return false; }
            if (!EnsureNominalTrajectory(out issue)) return false;
            // Search on the forward side of the unique capsule. Its surface clearance is monotone
            // along this ray; zero selects the contact boundary, not an arbitrary penetrating pose.
            int low = Math.Max(0, -ReferenceDummy.CenterZMillimeters);
            if (!TryPrepareProbePose(low, out _, out long initialGap, out issue)) return false;
            if (initialGap > surfaceDistanceMillimeters)
            { issue = "ReferenceSurfaceDistanceUnreachable"; return false; }
            int high = checked(low + surfaceDistanceMillimeters + ReferenceDummy.HeightMillimeters
                + ReferenceDummy.RadiusMillimeters + 10000);
            if (!TryPrepareProbePose(high, out _, out long highGap, out issue)) return false;
            if (highGap <= surfaceDistanceMillimeters)
            { issue = "ReferenceSurfaceDistanceNotBracketed"; return false; }
            while (high - low > 1)
            {
                int middle = low + (high - low) / 2;
                if (!TryPrepareProbePose(middle, out _, out long gap, out issue)) return false;
                if (gap <= surfaceDistanceMillimeters) low = middle; else high = middle;
            }
            if (!TryPrepareProbePose(low, out int frame, out long measuredGap, out issue)) return false;
            if (Math.Abs(measuredGap - surfaceDistanceMillimeters) > 1)
            { issue = "ReferenceSurfaceDistanceResolutionMismatch"; return false; }
            bool evaluated = _adapter.TryPreviewPrimaryAttack(Source, Target, Action.ActionId, new RuntimeFrame(frame + 1), out result);
            if (!evaluated || result.Status != CharacterTestAttackOpportunityStatus.Valid)
            { issue = result.Status + ":" + result.FailureReason; return false; }
            return true;
        }

        private bool TryPrepareProbePose(int rootDistanceMillimeters, out int frame, out long surfaceGap, out string issue)
        {
            frame = -1; surfaceGap = 0; issue = string.Empty;
            Source.SetTransform(CharacterTestCharacterTransformComponent.FromDeterministic(0, 0, 0, 0));
            Target.SetTransform(CharacterTestCharacterTransformComponent.FromDeterministic(0, 0, rootDistanceMillimeters, 0));
            Source.SetMotionState(0, Vector2.zero, isGrounded: true);
            Target.SetMotionState(0, Vector2.zero, isGrounded: true);
            frame = _nextFrame++;
            Runtime.Tick(new RuntimeTickContext(frame, 1d / 60d, frame / 60d, RuntimeTickStage.Simulation));
            if (Source.Transform.XMillimeters != 0 || Source.Transform.YMillimeters != 0 || Source.Transform.ZMillimeters != 0
                || Source.Transform.YawMilliDegrees != 0 || Target.Transform.XMillimeters != 0
                || Target.Transform.YMillimeters != 0 || Target.Transform.ZMillimeters != rootDistanceMillimeters
                || Target.Transform.YawMilliDegrees != 0 || Source.AuthorityPose?.HasActiveWork == true)
            { issue = "ReferencePoseChangedDuringSimulation"; return false; }
            if (!ValidateReferenceHurtbox(frame, out issue)) return false;
            if (!Runtime.PerceptionFrameCache.TryAcquireNearestHurtbox(new RuntimeFrame(frame + 1), Source, Target,
                    out _, out var distance))
            { issue = "ReferenceSurfaceDistanceUnavailable"; return false; }
            surfaceGap = distance.SurfaceGapMillimeters;
            return true;
        }

        private bool ValidateReferenceHurtbox(int frame, out string issue)
        {
            issue = string.Empty;
            if (!Runtime.TryGetDamageGeometrySnapshot(Target.Id.Value, out var geometry)
                || geometry == null || !geometry.IsValid || geometry.Frame.Value != frame
                || geometry.ColliderCount != 1)
            { issue = "ReferenceDummyRequiresExactlyOneCurrentHurtbox"; return false; }
            var collider = geometry.GetCollider(0);
            var dummy = ReferenceDummy;
            var center = new FixVector3(Fix64.FromRatio(dummy.CenterXMillimeters, 1000),
                Fix64.FromRatio(dummy.CenterYMillimeters, 1000), Fix64.FromRatio(dummy.CenterZMillimeters, 1000));
            var radius = Fix64.FromRatio(dummy.RadiusMillimeters, 1000);
            var half = Fix64.FromRatio(dummy.HeightMillimeters, 1000) / Fix64.FromInt(2) - radius;
            var vertical = new FixVector3(Fix64.Zero, half, Fix64.Zero);
            var expected = CombatPhysicsShape.Capsule(center - vertical, center + vertical, radius);
            if (collider.LogicalColliderId != CharacterTestAuthoredContentIds.D400_Id36 || !collider.LocalShape.Equals(expected)
                || collider.Layer != CharacterTestCharacterPhysicsRuntime.CharacterHurtboxLayer)
            { issue = "ReferenceDummyHurtboxDoesNotMatchMeasurementSpecification"; return false; }
            return true;
        }

        private bool EnsureNominalTrajectory(out string issue)
        {
            string result = null;
            foreach (var step in PrepareTrajectorySteps(error => result = error)) { }
            issue = result ?? string.Empty;
            return result == null;
        }

        // Each step advances at most one nominal action tick; no live scene is involved.
        internal IEnumerable<object> PrepareTrajectorySteps(Action<string> complete, Func<bool> cancelled = null)
        {
            if (cancelled?.Invoke() == true) { complete("MeasurementCancelled"); yield break; }
            string issue;
            if (!TryComputeActionHash(out string currentHash, out issue)) { complete(issue); yield break; }
            if (currentHash == _trajectoryHash) { complete(null); yield break; }
            _trajectoryHash = null;
            _nominalTrajectory.Clear();
            if (!Action.DurationFrames.HasValue || Action.DurationFrames.Value <= 0 || Action.DurationFrames.Value > 4096)
            { issue = "ActionDurationUnsupported"; complete(issue); yield break; }
            Source.SetTransform(CharacterTestCharacterTransformComponent.FromDeterministic(0, 0, 0, 0));
            Target.SetTransform(CharacterTestCharacterTransformComponent.FromDeterministic(0, 0, 50000, 0));
            Source.SetMotionState(0, Vector2.zero, isGrounded: true);
            Target.SetMotionState(0, Vector2.zero, isGrounded: true);
            Tick();
            if (!Runtime.ActionRuntime.TryStartActionSlot(Source, (int)CharacterCancelInputMask.AttackPrimary, Action.ActionId,
                new RuntimeFrame(_nextFrame)))
            { issue = "ReferenceGatewayRejected:" + Source.Action.LastRejectReason; complete(issue); yield break; }
            var poses = new SortedDictionary<int, CharacterTestDirectHitTraceSourcePoseOffset>();
            poses[0] = new CharacterTestDirectHitTraceSourcePoseOffset(0, FixVector3.Zero, 0);
            for (int i = 0; i <= Action.DurationFrames.Value + 1; i++)
            {
                if (cancelled?.Invoke() == true) { complete("MeasurementCancelled"); yield break; }
                Tick();
                yield return null;
                if (Source.LastCombatHitCount != 0) { issue = "ReferenceTrajectoryHitDistantTarget"; complete(issue); yield break; }
                if (Source.Action.ActiveActionId == Action.ActionId && Source.Action.State == MxFramework.Game.CharacterTest.CharacterTestActionStateId.Running)
                {
                    int localFrame = Source.Action.LocalFrame;
                    poses[localFrame] = new CharacterTestDirectHitTraceSourcePoseOffset(localFrame,
                        new FixVector3(Fix64.FromRatio(Source.Transform.XMillimeters, 1000),
                            Fix64.FromRatio(Source.Transform.YMillimeters, 1000), Fix64.FromRatio(Source.Transform.ZMillimeters, 1000)),
                        Source.Transform.YawMilliDegrees);
                }
            }
            if (Source.Action.State == MxFramework.Game.CharacterTest.CharacterTestActionStateId.Running) { issue = "ReferenceActionDidNotComplete"; complete(issue); yield break; }
            if (!TryComputeActionHash(out string afterHash, out issue) || currentHash != afterHash)
            { issue = "ReferenceSourceConditionsChanged:" + issue; complete(issue); yield break; }
            foreach (var pair in poses) _nominalTrajectory.Add(pair.Value);
            _trajectoryHash = currentHash;
            complete(null); yield break;
        }

        private void Tick()
        {
            int frame = _nextFrame++;
            Runtime.Tick(new RuntimeTickContext(frame, 1d / 60d, frame / 60d, RuntimeTickStage.Simulation));
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Runtime.Dispose();
        }
    }
}
