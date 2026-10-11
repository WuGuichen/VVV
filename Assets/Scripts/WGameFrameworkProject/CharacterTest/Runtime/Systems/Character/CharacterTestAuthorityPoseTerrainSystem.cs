using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using MxFramework.Animation;
using MxFramework.CharacterAction;
using MxFramework.CharacterRuntimeSpawn;
using MxFramework.CharacterRuntimeSpawn.Unity;
using MxFramework.Combat.Core;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>Feature-specific fixed-point terrain IK producer for CharacterTest.</summary>
    /// <remarks>
    /// Package/catalog registration is LowFreqAlloc. Tick is fixed-capacity and NoAlloc after warmup.
    /// </remarks>
    internal sealed class CharacterTestAuthorityPoseTerrainSystem
    {
        private readonly Dictionary<int, EntityRegistration> _registrations =
            new Dictionary<int, EntityRegistration>();
        private readonly CharacterTestCharacterActionRuntime _actionRuntime;

        internal CharacterTestAuthorityPoseTerrainSystem(
            CharacterTestCharacterActionRuntime actionRuntime)
        {
            _actionRuntime = actionRuntime;
        }

        internal void Register(CharacterTestCharacterEntity entity)
        {
            if (entity == null)
                return;

            CharacterImportedPackage package =
                CharacterTestGeneratedPackageSource.LoadPackageForEntity(entity);
            CharacterAuthorityPoseRigRuntimeBinding binding = package?.Geometry?.AuthorityPoseRig;
            if (binding == null || !binding.IsAuthored || string.IsNullOrWhiteSpace(package.RootPath))
            {
                _registrations.Remove(entity.Id.Value);
                entity.SetTerrainIkState(new CharacterTestTerrainIkStateComponent(entity.Id.Value));
                return;
            }

            // Socket bake is a v3 index + payload pair; the verified binary hash becomes the
            // terrain-pose content identity.
            if (!CharacterSocketBakeCatalogLoader.TryLoadFromDirectory(
                    package.RootPath,
                    package.PackageId,
                    out CharacterSocketBakeCatalog catalog,
                    out CharacterSocketBakeCatalogEvidence evidence,
                    out string[] issues)
                || catalog == null
                || catalog.TrackCount == 0)
            {
                if (issues != null && issues.Length != 0)
                {
                    UnityEngine.Debug.LogError(
                        "MxFramework terrain pose socket bake is unavailable for package '" + package.PackageId
                        + "' at " + package.RootPath + ": " + string.Join(" | ", issues));
                }
                _registrations.Remove(entity.Id.Value);
                entity.SetTerrainIkState(new CharacterTestTerrainIkStateComponent(entity.Id.Value));
                return;
            }

            CharacterRuntimeLocomotionDefinition locomotion = default;
            CharacterRuntimeAnimationResources.TryCreateLocomotionDefinition(
                entity.Identity.AnimationBinding,
                NullRuntimeLogger.Instance,
                out locomotion);
            CharacterAuthorityPoseRigDefinition definition =
                CharacterAuthorityPoseRigDefinition.FromRuntimeBinding(
                    binding,
                    CharacterTestAuthorityPoseDefinitionSource
                        .CreateDefaultDefinition()
                        .SecondaryBindings);
            string sourceHash = evidence.BinaryHash;
            int sourceRevision = ComputePositiveRevision(sourceHash);
            Configure(entity, definition, catalog, sourceRevision, sourceHash, locomotion);
        }

        internal bool ConfigureForTests(
            CharacterTestCharacterEntity entity,
            CharacterAuthorityPoseRigDefinition definition,
            CharacterSocketBakeCatalog catalog,
            int baseSourceRevision,
            string baseSourceContentHash)
        {
            return Configure(
                entity,
                definition,
                catalog,
                baseSourceRevision,
                baseSourceContentHash,
                default);
        }

        internal void Remove(CharacterTestCharacterEntityId entityId)
        {
            _registrations.Remove(entityId.Value);
        }

        internal void Clear()
        {
            _registrations.Clear();
        }

        private MxFramework.CharacterAction.CharacterActionConfigRegistry _actionPoseRegistry;

        internal void Tick(
            IReadOnlyList<CharacterTestCharacterEntity> entities,
            CharacterTestCharacterPhysicsRuntime physicsRuntime,
            RuntimeFrame frame, MxFramework.CharacterAction.CharacterActionConfigRegistry actionPoseRegistry = null)
        {
            _actionPoseRegistry = actionPoseRegistry;
            if (entities == null)
                return;
            for (int i = 0; i < entities.Count; i++)
            {
                CharacterTestCharacterEntity entity = entities[i];
                if (entity != null
                    && _registrations.TryGetValue(entity.Id.Value, out EntityRegistration registration))
                {
                    TickRegistered(
                        entity,
                        registration,
                        physicsRuntime?.World,
                        frame,
                        forcedSource: default);
                }
            }
        }

        internal bool TickForTests(
            CharacterTestCharacterEntity entity,
            CombatPhysicsWorld physicsWorld,
            RuntimeFrame frame,
            string clipKey,
            int localFrame,
            bool loop)
        {
            if (entity == null
                || !_registrations.TryGetValue(entity.Id.Value, out EntityRegistration registration))
            {
                return false;
            }

            return TickRegistered(
                entity,
                registration,
                physicsWorld,
                frame,
                new BaseSource(registration.Catalog.PrepareClip(clipKey), localFrame, loop));
        }

        private bool Configure(
            CharacterTestCharacterEntity entity,
            CharacterAuthorityPoseRigDefinition definition,
            CharacterSocketBakeCatalog catalog,
            int baseSourceRevision,
            string baseSourceContentHash,
            CharacterRuntimeLocomotionDefinition locomotion)
        {
            if (entity == null || definition == null || catalog == null)
                return false;

            var state = new CharacterTestTerrainIkStateComponent(entity.Id.Value);
            if (!state.Configure(definition, baseSourceRevision, baseSourceContentHash))
                return false;

            var registration = new EntityRegistration(
                definition,
                catalog,
                locomotion,
                baseSourceRevision,
                baseSourceContentHash);
            _registrations[entity.Id.Value] = registration;
            entity.SetAuthorityPose(new CharacterAuthorityPoseStateComponent(definition));
            entity.SetTerrainIkState(state);
            return true;
        }

        private bool TickRegistered(
            CharacterTestCharacterEntity entity,
            EntityRegistration registration,
            CombatPhysicsWorld physicsWorld,
            RuntimeFrame frame,
            BaseSource forcedSource)
        {
            CharacterTestTerrainIkStateComponent state = entity.TerrainIkState;
            if (state == null || !state.IsConfigured)
                return false;

            bool definitionMatches = state.DefinitionRevision == registration.Definition.SourceRevision
                && string.Equals(
                    state.DefinitionContentHash,
                    registration.Definition.ContentHash,
                    StringComparison.Ordinal);
            bool sourceMatches = state.BaseSourceRevision == registration.BaseSourceRevision
                && string.Equals(
                    state.BaseSourceContentHash,
                    registration.BaseSourceContentHash,
                    StringComparison.Ordinal);
            BaseSource source = forcedSource.IsValid
                ? forcedSource
                : ResolveBaseSource(entity, registration, frame);

            state.BeginResolvedFrame(frame.Value);
            bool baseReady = source.IsValid
                && TryReadBaseSamples(registration, source);
            CharacterTestAuthorityPoseFailure globalFailure = !definitionMatches
                ? CharacterTestAuthorityPoseFailure.DefinitionStale
                : !sourceMatches || !baseReady
                    ? CharacterTestAuthorityPoseFailure.SourceStale
                    : CharacterTestAuthorityPoseFailure.Success;
            InitializeOverlay(registration, globalFailure);

            for (int limbIndex = 0; limbIndex < state.LimbCount; limbIndex++)
            {
                CharacterTestTerrainIkLimbState limbState = state.GetLimb(limbIndex);
                CharacterAuthorityGroundProbeDefinition probe =
                    registration.Definition.GroundProbes[limbIndex];
                CharacterAuthorityTwoBoneIkBinding ik = FindIkBinding(
                    registration.Definition,
                    probe.IkBindingId);
                CharacterTestAuthorityPoseFailure failure = globalFailure;
                if (failure == CharacterTestAuthorityPoseFailure.Success)
                    TryResolveEligibility(entity, probe, _actionRuntime, out failure);
                bool currentSolveReady = false;
                CharacterAuthorityTwoBoneIkSolveResult currentSolve = default;
                FixVector3 currentNormalModel = FixVector3.Zero;

                if (failure == CharacterTestAuthorityPoseFailure.Success
                    && registration.HasChannelConflict(limbIndex))
                {
                    failure = CharacterTestAuthorityPoseFailure.ChannelConflict;
                }
                else if (failure == CharacterTestAuthorityPoseFailure.Success
                    && (physicsWorld == null || ik == null))
                {
                    failure = CharacterTestAuthorityPoseFailure.SourceStale;
                }
                else if (failure == CharacterTestAuthorityPoseFailure.Success)
                {
                    failure = TryProbeAndSolve(
                        entity,
                        registration,
                        probe,
                        ik,
                        physicsWorld,
                        out CombatGroundProbeHit hit,
                        out currentSolve,
                        out currentNormalModel);
                    currentSolveReady = failure == CharacterTestAuthorityPoseFailure.Success;
                    if (currentSolveReady)
                        ApplySuccessfulProbe(ref limbState, hit, probe, frame.Value);
                }

                if (!currentSolveReady)
                {
                    ApplyFailedProbe(ref limbState, failure, probe);
                    currentSolveReady = TrySolveRetainedTarget(
                        entity,
                        registration,
                        ik,
                        limbState,
                        out currentSolve,
                        out currentNormalModel);
                }

                if (currentSolveReady
                    && limbState.CurrentWeight > Fix64.Zero
                    && !TryApplySolvedLimb(
                        registration,
                        ik,
                        currentSolve,
                        currentNormalModel,
                        limbState.CurrentWeight,
                        limbState.CurrentFailure))
                {
                    limbState.CurrentValid = false;
                    limbState.CurrentFailure = CharacterTestAuthorityPoseFailure.InvalidResolvedBasis;
                    ApplyFailureToLimbChannels(
                        registration,
                        ik,
                        CharacterTestAuthorityPoseFailure.InvalidResolvedBasis);
                }
                else if (!currentSolveReady)
                {
                    ApplyFailureToLimbChannels(registration, ik, limbState.CurrentFailure);
                }

                state.SetLimb(limbIndex, limbState);
            }

            ApplyNonConflictingSecondary(entity.AuthorityPose, registration);
            PublishResolvedFrame(state, registration);
            return baseReady && definitionMatches && sourceMatches;
        }

        private static bool TryReadBaseSamples(
            EntityRegistration registration,
            in BaseSource source)
        {
            bool complete = true;
            IReadOnlyList<CharacterAuthorityPoseChannelDefinition> channels =
                registration.Definition.Channels;
            registration.BaseCount = channels.Count;
            for (int i = 0; i < channels.Count; i++)
            {
                CharacterAuthorityPoseChannelDefinition channel = channels[i];
                if (source.Clip == null || !source.Clip.TryGetTrack(
                        channel.ChannelId,
                        out CharacterSocketBakeTrack track)
                    || track.Frames.Count == 0
                    || !track.HasBasis)
                {
                    registration.BaseValid[i] = false;
                    registration.BaseSamples[i] = new CharacterTestAuthorityPoseBaseSample(
                        channel.Ordinal,
                        channel.ChannelId,
                        FixVector3.Zero,
                        CharacterTestResolvedAuthorityPoseBasis.Identity);
                    complete = false;
                    continue;
                }

                int localFrame = source.LocalFrame;
                if (source.Loop)
                    localFrame %= track.Frames.Count;
                if (localFrame < 0
                    || !track.TryGetFrame(localFrame, out MxAnimationBakedSocketFrame frame)
                    || !track.TryGetBasis(localFrame, out CharacterSocketBakeBasisFrame basis)
                    || !TryConvertBasis(track.BasisQuantizationScale, basis, out CharacterTestResolvedAuthorityPoseBasis converted))
                {
                    registration.BaseValid[i] = false;
                    registration.BaseSamples[i] = new CharacterTestAuthorityPoseBaseSample(
                        channel.Ordinal,
                        channel.ChannelId,
                        FixVector3.Zero,
                        CharacterTestResolvedAuthorityPoseBasis.Identity);
                    complete = false;
                    continue;
                }

                registration.BaseValid[i] = true;
                registration.BaseSamples[i] = new CharacterTestAuthorityPoseBaseSample(
                    channel.Ordinal,
                    channel.ChannelId,
                    ConvertVector(frame.Position, track.QuantizationScale),
                    converted);
            }
            return complete;
        }

        private static void InitializeOverlay(
            EntityRegistration registration,
            CharacterTestAuthorityPoseFailure globalFailure)
        {
            for (int i = 0; i < registration.BaseCount; i++)
            {
                CharacterTestAuthorityPoseBaseSample sample = registration.BaseSamples[i];
                registration.ResolvedPositions[i] = sample.Position;
                registration.ResolvedBases[i] = sample.Basis;
                registration.SourceMasks[i] = registration.BaseValid[i]
                    ? CharacterTestAuthorityPoseSampleSourceMask.Base
                    : CharacterTestAuthorityPoseSampleSourceMask.None;
                registration.Failures[i] = globalFailure;
                registration.ResolvedValid[i] = registration.BaseValid[i];
            }
        }

        private static CharacterTestAuthorityPoseFailure TryProbeAndSolve(
            CharacterTestCharacterEntity entity,
            EntityRegistration registration,
            CharacterAuthorityGroundProbeDefinition probe,
            CharacterAuthorityTwoBoneIkBinding ik,
            CombatPhysicsWorld physicsWorld,
            out CombatGroundProbeHit hit,
            out CharacterAuthorityTwoBoneIkSolveResult solve,
            out FixVector3 normalModel)
        {
            hit = default;
            solve = default;
            normalModel = FixVector3.Zero;
            int tipIndex = registration.FindChannelIndex(probe.TipChannelId);
            int rootIndex = registration.FindChannelIndex(ik.RootChannelId);
            if (tipIndex < 0 || rootIndex < 0
                || !registration.BaseValid[tipIndex]
                || !registration.BaseValid[rootIndex])
            {
                return CharacterTestAuthorityPoseFailure.SourceStale;
            }

            CharacterTestAuthorityPoseBaseSample tip = registration.BaseSamples[tipIndex];
            FixVector3 axisModel = CharacterTestAuthorityPoseTerrainMath.TransformByBasis(
                tip.Basis,
                probe.TipLocalAxis);
            if (!axisModel.TryNormalize(out axisModel))
                return CharacterTestAuthorityPoseFailure.InvalidProbe;
            FixVector3 originModel = tip.Position
                + CharacterTestAuthorityPoseTerrainMath.TransformByBasis(
                    tip.Basis,
                    probe.TipLocalOrigin)
                - (axisModel * probe.MaxUpDistance);
            FixVector3 originWorld = CharacterTestAuthorityPoseTerrainMath.ModelPointToWorld(
                originModel,
                entity.Transform);
            FixVector3 directionWorld =
                CharacterTestAuthorityPoseTerrainMath.ModelDirectionToWorld(
                    axisModel,
                    entity.Transform.YawMilliDegrees);
            if (!directionWorld.TryNormalize(out directionWorld))
                return CharacterTestAuthorityPoseFailure.InvalidProbe;

            var query = new CombatGroundProbeQuery(
                probeId: probe.Ordinal + 1,
                new CombatEntityId(
                    CharacterTestCharacterPhysicsRuntime.DynamicBodyIdStart + entity.Id.Value),
                sourceOrder: probe.Ordinal,
                originWorld,
                directionWorld,
                probe.MaxUpDistance + probe.MaxDownDistance,
                probe.Radius,
                new CombatPhysicsLayerMask(probe.StaticLayerMask),
                probe.MinimumSlopeCosine);
            CombatGroundProbeResult result = physicsWorld.QueryGroundProbe(query);
            if (!result.Succeeded || !result.HasHit)
                return MapProbeFailure(result.Status);

            hit = result.Hit;
            FixVector3 targetWorld = hit.Point
                + (hit.Normal * (probe.WorldNormalTargetOffset + probe.Clearance));
            FixVector3 targetModel = CharacterTestAuthorityPoseTerrainMath.WorldPointToModel(
                targetWorld,
                entity.Transform);
            normalModel = CharacterTestAuthorityPoseTerrainMath.WorldDirectionToModel(
                hit.Normal,
                entity.Transform.YawMilliDegrees);
            if (!normalModel.TryNormalize(out normalModel))
                return CharacterTestAuthorityPoseFailure.InvalidResolvedBasis;
            if (!CharacterAuthorityTwoBoneIkSolver.TrySolve(
                    ik,
                    registration.BaseSamples[rootIndex].Position,
                    targetModel,
                    out solve))
            {
                return CharacterTestAuthorityPoseFailure.DegeneratePole;
            }
            return solve.TargetClamped
                ? CharacterTestAuthorityPoseFailure.OutOfReach
                : CharacterTestAuthorityPoseFailure.Success;
        }

        private static bool TrySolveRetainedTarget(
            CharacterTestCharacterEntity entity,
            EntityRegistration registration,
            CharacterAuthorityTwoBoneIkBinding ik,
            in CharacterTestTerrainIkLimbState state,
            out CharacterAuthorityTwoBoneIkSolveResult solve,
            out FixVector3 normalModel)
        {
            solve = default;
            normalModel = FixVector3.Zero;
            if (ik == null
                || state.CurrentWeight <= Fix64.Zero
                || !state.HasRetainedTarget)
            {
                return false;
            }

            int rootIndex = registration.FindChannelIndex(ik.RootChannelId);
            if (rootIndex < 0 || !registration.BaseValid[rootIndex])
                return false;
            FixVector3 targetModel = CharacterTestAuthorityPoseTerrainMath.WorldPointToModel(
                state.LastValidTargetWorld,
                entity.Transform);
            normalModel = CharacterTestAuthorityPoseTerrainMath.WorldDirectionToModel(
                state.LastValidNormalWorld,
                entity.Transform.YawMilliDegrees);
            return normalModel.TryNormalize(out normalModel)
                && CharacterAuthorityTwoBoneIkSolver.TrySolve(
                    ik,
                    registration.BaseSamples[rootIndex].Position,
                    targetModel,
                    out solve)
                && !solve.TargetClamped;
        }

        private static bool TryApplySolvedLimb(
            EntityRegistration registration,
            CharacterAuthorityTwoBoneIkBinding ik,
            in CharacterAuthorityTwoBoneIkSolveResult solve,
            in FixVector3 normalModel,
            Fix64 weight,
            CharacterTestAuthorityPoseFailure failure)
        {
            int rootIndex = registration.FindChannelIndex(ik.RootChannelId);
            int midIndex = registration.FindChannelIndex(ik.MidChannelId);
            int tipIndex = registration.FindChannelIndex(ik.TipChannelId);
            if (rootIndex < 0 || midIndex < 0 || tipIndex < 0
                || !registration.BaseValid[rootIndex]
                || !registration.BaseValid[midIndex]
                || !registration.BaseValid[tipIndex]
                || !TryResolveBendPlane(
                    registration.BaseSamples[rootIndex].Position,
                    solve.TipPosition,
                    ik.PoleDirection,
                    out FixVector3 planeUp)
                || !CharacterTestAuthorityPoseTerrainMath.TryCreateChainBasis(
                    registration.BaseSamples[rootIndex].Position,
                    solve.MidPosition,
                    planeUp,
                    out CharacterTestResolvedAuthorityPoseBasis rootBasis)
                || !CharacterTestAuthorityPoseTerrainMath.TryCreateChainBasis(
                    solve.MidPosition,
                    solve.TipPosition,
                    planeUp,
                    out CharacterTestResolvedAuthorityPoseBasis midBasis)
                || !TryCreateTipBasis(
                    registration.BaseSamples[tipIndex].Basis.Forward,
                    midBasis.Forward,
                    normalModel,
                    out CharacterTestResolvedAuthorityPoseBasis tipBasis))
            {
                return false;
            }

            ApplyOverlaySample(registration, rootIndex, registration.BaseSamples[rootIndex].Position, rootBasis, weight, failure);
            ApplyOverlaySample(registration, midIndex, solve.MidPosition, midBasis, weight, failure);
            ApplyOverlaySample(registration, tipIndex, solve.TipPosition, tipBasis, weight, failure);
            return true;
        }

        private static bool TryResolveBendPlane(
            in FixVector3 rootPosition,
            in FixVector3 tipPosition,
            in FixVector3 authoredPole,
            out FixVector3 planeUp)
        {
            planeUp = FixVector3.Zero;
            if (!(tipPosition - rootPosition).TryNormalize(out FixVector3 aim))
                return false;
            if (!TryProjectAndNormalize(authoredPole, aim, out FixVector3 bend)
                && !TryProjectAndNormalize(
                    new FixVector3(Fix64.One, Fix64.Zero, Fix64.Zero),
                    aim,
                    out bend)
                && !TryProjectAndNormalize(
                    new FixVector3(Fix64.Zero, Fix64.Zero, Fix64.One),
                    aim,
                    out bend))
            {
                return false;
            }
            return CharacterTestAuthorityPoseTerrainMath.Cross(aim, bend)
                .TryNormalize(out planeUp);
        }

        private static bool TryCreateTipBasis(
            in FixVector3 baseTipForward,
            in FixVector3 parentForward,
            in FixVector3 groundNormal,
            out CharacterTestResolvedAuthorityPoseBasis basis)
        {
            basis = default;
            if (!groundNormal.TryNormalize(out FixVector3 up))
                return false;
            if (!TryProjectAndNormalize(baseTipForward, up, out FixVector3 forward)
                && !TryProjectAndNormalize(parentForward, up, out forward)
                && !TryProjectAndNormalize(
                    new FixVector3(Fix64.Zero, Fix64.Zero, Fix64.One),
                    up,
                    out forward)
                && !TryProjectAndNormalize(
                    new FixVector3(Fix64.One, Fix64.Zero, Fix64.Zero),
                    up,
                    out forward))
            {
                return false;
            }
            return CharacterTestAuthorityPoseTerrainMath.TryCreateChainBasis(
                FixVector3.Zero,
                forward,
                up,
                out basis);
        }

        private static bool TryProjectAndNormalize(
            in FixVector3 candidate,
            in FixVector3 planeNormal,
            out FixVector3 projected)
        {
            return (candidate - (planeNormal * candidate.Dot(planeNormal)))
                .TryNormalize(out projected);
        }

        private static void ApplyOverlaySample(
            EntityRegistration registration,
            int index,
            in FixVector3 overlayPosition,
            in CharacterTestResolvedAuthorityPoseBasis overlayBasis,
            Fix64 weight,
            CharacterTestAuthorityPoseFailure failure)
        {
            CharacterTestAuthorityPoseBaseSample baseSample = registration.BaseSamples[index];
            registration.ResolvedPositions[index] = CharacterTestAuthorityPoseTerrainMath.Lerp(
                baseSample.Position,
                overlayPosition,
                weight);
            if (!CharacterTestAuthorityPoseTerrainMath.TryBlendBasis(
                    baseSample.Basis,
                    overlayBasis,
                    weight,
                    out registration.ResolvedBases[index]))
            {
                registration.ResolvedValid[index] = false;
                registration.Failures[index] = CharacterTestAuthorityPoseFailure.InvalidResolvedBasis;
                return;
            }
            registration.SourceMasks[index] |= CharacterTestAuthorityPoseSampleSourceMask.TerrainIk;
            registration.Failures[index] = failure;
        }

        private static void ApplyFailureToLimbChannels(
            EntityRegistration registration,
            CharacterAuthorityTwoBoneIkBinding ik,
            CharacterTestAuthorityPoseFailure failure)
        {
            if (ik == null)
                return;
            SetFailure(registration, registration.FindChannelIndex(ik.RootChannelId), failure);
            SetFailure(registration, registration.FindChannelIndex(ik.MidChannelId), failure);
            SetFailure(registration, registration.FindChannelIndex(ik.TipChannelId), failure);
        }

        private static void SetFailure(
            EntityRegistration registration,
            int index,
            CharacterTestAuthorityPoseFailure failure)
        {
            if (index >= 0)
                registration.Failures[index] = failure;
        }

        private static void ApplySuccessfulProbe(
            ref CharacterTestTerrainIkLimbState state,
            in CombatGroundProbeHit hit,
            CharacterAuthorityGroundProbeDefinition probe,
            long frame)
        {
            state.CurrentValid = true;
            state.CurrentFailure = CharacterTestAuthorityPoseFailure.Success;
            state.LastValidFrame = frame;
            state.ConsecutiveMissCount = 0;
            state.LastValidTargetWorld = hit.Point
                + (hit.Normal * (probe.WorldNormalTargetOffset + probe.Clearance));
            state.LastValidNormalWorld = hit.Normal;
            state.SurfaceEntityId = hit.SurfaceEntityId.Value;
            state.SurfaceBodyId = hit.SurfaceBodyId.Value;
            state.SurfaceColliderId = hit.SurfaceColliderId.Value;
            state.SurfaceLeafId = hit.SurfaceLeafId.Value;
            state.SurfaceShapeKind = hit.SurfaceShapeKind;
            state.SurfaceMaterial = hit.SurfaceMaterial;
            Fix64 step = Fix64.FromRatio(1, Math.Max(1, probe.EnterFrames));
            state.CurrentWeight = Fix64.Min(Fix64.One, state.CurrentWeight + step);
        }

        private static void ApplyFailedProbe(
            ref CharacterTestTerrainIkLimbState state,
            CharacterTestAuthorityPoseFailure failure,
            CharacterAuthorityGroundProbeDefinition probe)
        {
            state.CurrentValid = false;
            state.CurrentFailure = failure;
            int missCap = state.MissCountCap;
            if (state.ConsecutiveMissCount < missCap)
                state.ConsecutiveMissCount++;
            if (state.HasRetainedTarget
                && state.ConsecutiveMissCount <= Math.Max(0, probe.LostFrames))
            {
                return;
            }

            Fix64 step = Fix64.FromRatio(1, Math.Max(1, probe.ExitFrames));
            state.CurrentWeight = Fix64.Max(Fix64.Zero, state.CurrentWeight - step);
            if (state.CurrentWeight.IsZero)
            {
                state.LastValidFrame = -1L;
                state.LastValidTargetWorld = FixVector3.Zero;
                state.LastValidNormalWorld = FixVector3.Zero;
                state.SurfaceEntityId = 0;
                state.SurfaceBodyId = 0;
                state.SurfaceColliderId = 0;
                state.SurfaceLeafId = 0;
                state.SurfaceShapeKind = default;
                state.SurfaceMaterial = CombatSurfaceMaterial.Default;
            }
        }

        private static bool TryResolveEligibility(
            CharacterTestCharacterEntity entity,
            CharacterAuthorityGroundProbeDefinition probe,
            CharacterTestCharacterActionRuntime actionRuntime,
            out CharacterTestAuthorityPoseFailure failure)
        {
            if (!entity.CombatState.IsAlive)
            {
                failure = CharacterTestAuthorityPoseFailure.Dead;
                return false;
            }
            if (entity.SpecialHitState.IsLaunched
                || entity.BattleState.DominantReason == CharacterTestBattleStateDominantReason.Launch)
            {
                failure = CharacterTestAuthorityPoseFailure.KnockedBack;
                return false;
            }
            if (entity.MovementMode == CharacterMovementMode.Airborne)
            {
                failure = CharacterTestAuthorityPoseFailure.Flying;
                return false;
            }
            if (!(probe.PartStateId == 0)
                && entity.BodyPartState.GetPartStateDependencyStatus(probe.PartStateId)
                    == CharacterTestBodyPartDependencyStatus.Severed)
            {
                failure = CharacterTestAuthorityPoseFailure.Severed;
                return false;
            }
            if (!ContainsAllowed(probe.AllowedMotionIds, MovementModeId(entity.MovementMode)))
            {
                failure = CharacterTestAuthorityPoseFailure.MotionDisabled;
                return false;
            }
            if (!ContainsAllowed(probe.AllowedPostureIds, entity.ColliderPosture.ProfileId))
            {
                failure = CharacterTestAuthorityPoseFailure.PostureDisabled;
                return false;
            }
            if (HasDisabledActionTag(entity.Action.ActiveActionId, probe.DisabledActionTags, actionRuntime))
            {
                failure = CharacterTestAuthorityPoseFailure.ActionDisabled;
                return false;
            }

            failure = CharacterTestAuthorityPoseFailure.Success;
            return true;
        }

        private static bool ContainsAllowed(int[] allowed, int value)
        {
            if (allowed == null || allowed.Length == 0) return true;
            for (int i = 0; i < allowed.Length; i++) if (allowed[i] == value) return true;
            return false;
        }

        private static bool ContainsAllowed(string[] allowed, string value)
        {
            if (allowed == null || allowed.Length == 0)
                return true;
            for (int i = 0; i < allowed.Length; i++)
            {
                if (string.Equals(allowed[i], value, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        private static bool HasDisabledActionTag(
            CharacterActionId activeActionId,
            string[] disabledTags,
            CharacterTestCharacterActionRuntime actionRuntime)
        {
            if (!activeActionId.IsValid
                || disabledTags == null
                || disabledTags.Length == 0
                || actionRuntime?.Registry == null
                || !actionRuntime.Registry.TryGetAction(activeActionId, out CharacterActionConfig action))
            {
                return false;
            }

            string[] tags = action.Tags;
            for (int i = 0; i < disabledTags.Length; i++)
            {
                for (int j = 0; j < tags.Length; j++)
                {
                    if (string.Equals(disabledTags[i], tags[j], StringComparison.Ordinal))
                        return true;
                }
            }
            return false;
        }

        private static string MovementModeId(CharacterMovementMode mode)
        {
            switch (mode)
            {
                case CharacterMovementMode.Idle: return "Idle";
                case CharacterMovementMode.Walk: return "Walk";
                case CharacterMovementMode.Run: return "Run";
                case CharacterMovementMode.Strafe: return "Strafe";
                case CharacterMovementMode.TurnInPlace: return "TurnInPlace";
                case CharacterMovementMode.ApproachTarget: return "ApproachTarget";
                case CharacterMovementMode.Retreat: return "Retreat";
                case CharacterMovementMode.CircleTarget: return "CircleTarget";
                case CharacterMovementMode.RootMotionDriven: return "RootMotionDriven";
                case CharacterMovementMode.Airborne: return "Airborne";
                case CharacterMovementMode.ControlLocked: return "ControlLocked";
                default: return string.Empty;
            }
        }

        private static void ApplyNonConflictingSecondary(
            CharacterAuthorityPoseStateComponent authorityPose,
            EntityRegistration registration)
        {
            if (authorityPose == null)
                return;
            for (int i = 0; i < registration.BaseCount; i++)
            {
                if (!registration.BaseValid[i]
                    || registration.TerrainOwnedChannels[i]
                    || !authorityPose.TryGetChannelOffset(
                        registration.BaseSamples[i].ChannelId,
                        out FixVector3 offset))
                {
                    continue;
                }
                registration.ResolvedPositions[i] += offset;
                registration.SourceMasks[i] |= CharacterTestAuthorityPoseSampleSourceMask.Secondary;
            }
        }

        private static void PublishResolvedFrame(
            CharacterTestTerrainIkStateComponent state,
            EntityRegistration registration)
        {
            for (int i = 0; i < registration.BaseCount; i++)
            {
                CharacterTestAuthorityPoseBaseSample baseSample = registration.BaseSamples[i];
                state.ResolvedFrame.TryAdd(new CharacterTestResolvedAuthorityPoseSample(
                    baseSample.Ordinal,
                    baseSample.ChannelId,
                    registration.ResolvedPositions[i],
                    registration.ResolvedBases[i],
                    registration.ResolvedValid[i],
                    registration.SourceMasks[i],
                    registration.Failures[i]));
            }
        }

        private static CharacterAuthorityTwoBoneIkBinding FindIkBinding(
            CharacterAuthorityPoseRigDefinition definition,
            int bindingId)
        {
            IReadOnlyList<CharacterAuthorityTwoBoneIkBinding> bindings =
                definition.TwoBoneIkBindings;
            for (int i = 0; i < bindings.Count; i++)
            {
                if (bindings[i].BindingId == bindingId)
                    return bindings[i];
            }
            return null;
        }

        private static CharacterTestAuthorityPoseFailure MapProbeFailure(
            CombatGroundProbeStatus status)
        {
            switch (status)
            {
                case CombatGroundProbeStatus.NoHit:
                    return CharacterTestAuthorityPoseFailure.NoHit;
                case CombatGroundProbeStatus.TooSteep:
                    return CharacterTestAuthorityPoseFailure.TooSteep;
                case CombatGroundProbeStatus.OutOfRange:
                    return CharacterTestAuthorityPoseFailure.OutOfReach;
                default:
                    return CharacterTestAuthorityPoseFailure.InvalidProbe;
            }
        }

        private BaseSource ResolveBaseSource(
            CharacterTestCharacterEntity entity,
            EntityRegistration registration,
            RuntimeFrame frame)
        {
            CharacterTestCharacterAnimationRequestComponent request = entity.ComponentWorld.GetAnimationRequest(entity.Id);
            int bindingId = request.CanEmit(frame.Value, entity.Action.ActiveActionId)
                ? request.AuthorityBindingId : entity.Animation.AuthorityBindingId;
            MxFramework.CharacterAction.CharacterActionConfig action = null;
            _actionPoseRegistry?.TryGetAction(entity.Action.ActiveActionId, out action);
            if (bindingId > 0 && !CharacterTestCharacterAnimationSyncSystem.IsOrdinaryGuardRecovery(action, entity.Action.LocalFrame)
                && registration.ActionClips.TryGetValue(bindingId, out CharacterPreparedSocketClip actionClip))
                return new BaseSource(actionClip, (int)Math.Max(0,
                    CharacterTestCharacterAnimationSyncSystem.ResolveOrdinaryGuardClipFrame(action, entity.Action.LocalFrame)), loop: false);

            int localFrame = frame.Value <= 0L
                ? 0
                : (int)Math.Min(int.MaxValue, frame.Value);
            if (registration.Locomotion.BlendKind == CharacterAnimationBlendKind.Blend1D)
            {
                IReadOnlyList<MxAnimationBlend1DPoint> points = registration.Locomotion.Blend1D?.Points;
                if (points == null || points.Count == 0)
                    return default;
                int index = ResolveBlend1DSourceIndex(entity, registration.Locomotion.Blend1D);
                MxAnimationBlend1DPoint point = points[index];
                return new BaseSource(registration.Blend1DClips[index], localFrame, point.Loop);
            }
            if (registration.Locomotion.BlendKind == CharacterAnimationBlendKind.Blend2D)
            {
                IReadOnlyList<MxAnimationBlend2DPoint> points = registration.Locomotion.Blend2D?.Points;
                if (points == null || points.Count == 0)
                    return default;
                int inputX = entity.LastMoveInputXMilli;
                int inputY = entity.LastMoveInputYMilli;
                int best = 0;
                long bestDistance = long.MaxValue;
                for (int i = 0; i < points.Count; i++)
                {
                    long dx = points[i].X - inputX;
                    long dy = points[i].Y - inputY;
                    long distance = dx * dx + dy * dy;
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        best = i;
                    }
                }
                MxAnimationBlend2DPoint point = points[best];
                return new BaseSource(registration.Blend2DClips[best], localFrame, point.Loop);
            }
            return default;
        }

        internal static int ResolveBlend1DSourceIndex(
            CharacterTestCharacterEntity entity,
            MxAnimationBlend1DDefinition blend)
        {
            IReadOnlyList<MxAnimationBlend1DPoint> points = blend?.Points;
            if (entity == null || points == null || points.Count == 0)
                return 0;

            int value = (int)Math.Round(
                Math.Max(0f, Math.Min(1f, entity.Animation.LocomotionSpeed01))
                * Math.Max(1, blend.ParameterScale));
            if (value <= points[0].Threshold)
                return 0;

            int last = points.Count - 1;
            if (value >= points[last].Threshold)
                return last;

            for (int i = 0; i < last; i++)
            {
                if (value < points[i].Threshold || value > points[i + 1].Threshold)
                    continue;

                int lowerDistance = value - points[i].Threshold;
                int upperDistance = points[i + 1].Threshold - value;
                return lowerDistance <= upperDistance ? i : i + 1;
            }

            return 0;
        }

        private static FixVector3 ConvertVector(MxAnimationBakedVector3 value, int scale)
        {
            int safeScale = scale > 0 ? scale : 1;
            return new FixVector3(
                Fix64.FromRatio(value.X, safeScale),
                Fix64.FromRatio(value.Y, safeScale),
                Fix64.FromRatio(value.Z, safeScale));
        }

        private static bool TryConvertBasis(
            int scale,
            CharacterSocketBakeBasisFrame source,
            out CharacterTestResolvedAuthorityPoseBasis basis)
        {
            basis = default;
            if (source == null)
                return false;
            FixVector3 authoredRight = ConvertVector(source.Right, scale);
            FixVector3 authoredUp = ConvertVector(source.Up, scale);
            FixVector3 authoredForward = ConvertVector(source.Forward, scale);
            return authoredRight.TryNormalize(out authoredRight)
                && CharacterTestAuthorityPoseTerrainMath.TryCreateChainBasis(
                    FixVector3.Zero,
                    authoredForward,
                    authoredUp,
                    out basis)
                && basis.Right.Dot(authoredRight) > Fix64.Zero;
        }

        private static string ComputeSha256(string value)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(value ?? string.Empty));
                var builder = new StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++)
                    builder.Append(hash[i].ToString("x2"));
                return builder.ToString();
            }
        }

        private static int ComputePositiveRevision(string sha256)
        {
            if (string.IsNullOrEmpty(sha256) || sha256.Length < 8)
                return 1;
            int value = 0;
            for (int i = 0; i < 8; i++)
            {
                char c = sha256[i];
                int nibble = c >= '0' && c <= '9' ? c - '0' : c - 'a' + 10;
                value = unchecked((value << 4) | nibble);
            }
            value &= int.MaxValue;
            return value == 0 ? 1 : value;
        }

        private readonly struct BaseSource
        {
            internal BaseSource(CharacterPreparedSocketClip clip, int localFrame, bool loop)
            {
                Clip = clip;
                LocalFrame = localFrame < 0 ? 0 : localFrame;
                Loop = loop;
            }

            internal CharacterPreparedSocketClip Clip { get; }
            internal int LocalFrame { get; }
            internal bool Loop { get; }
            internal bool IsValid => Clip != null;
        }

        private sealed class EntityRegistration
        {
            internal EntityRegistration(
                CharacterAuthorityPoseRigDefinition definition,
                CharacterSocketBakeCatalog catalog,
                CharacterRuntimeLocomotionDefinition locomotion,
                int baseSourceRevision,
                string baseSourceContentHash)
            {
                Definition = definition;
                Catalog = catalog;
                Locomotion = locomotion;
                BaseSourceRevision = baseSourceRevision;
                BaseSourceContentHash = baseSourceContentHash ?? string.Empty;
                IReadOnlyList<MxAnimationActionBinding> actions = locomotion.AnimationSet?.Actions;
                for (int i = 0; actions != null && i < actions.Count; i++)
                {
                    MxAnimationActionBinding action = actions[i];
                    ActionClips.Add(action.BindingId, catalog.PrepareClip(action.Clip.Id));
                }
                IReadOnlyList<MxAnimationBlend1DPoint> blend1D = locomotion.Blend1D?.Points;
                Blend1DClips = new CharacterPreparedSocketClip[blend1D?.Count ?? 0];
                for (int i = 0; i < Blend1DClips.Length; i++) Blend1DClips[i] = catalog.PrepareClip(blend1D[i].ClipKey.Id);
                IReadOnlyList<MxAnimationBlend2DPoint> blend2D = locomotion.Blend2D?.Points;
                Blend2DClips = new CharacterPreparedSocketClip[blend2D?.Count ?? 0];
                for (int i = 0; i < Blend2DClips.Length; i++) Blend2DClips[i] = catalog.PrepareClip(blend2D[i].ClipKey.Id);
                MarkTerrainOwnershipAndConflicts();
            }

            internal CharacterAuthorityPoseRigDefinition Definition { get; }
            internal CharacterSocketBakeCatalog Catalog { get; }
            internal CharacterRuntimeLocomotionDefinition Locomotion { get; }
            internal readonly Dictionary<int, CharacterPreparedSocketClip> ActionClips = new Dictionary<int, CharacterPreparedSocketClip>();
            internal readonly CharacterPreparedSocketClip[] Blend1DClips;
            internal readonly CharacterPreparedSocketClip[] Blend2DClips;
            internal int BaseSourceRevision { get; }
            internal string BaseSourceContentHash { get; }
            internal int BaseCount;
            internal readonly CharacterTestAuthorityPoseBaseSample[] BaseSamples =
                new CharacterTestAuthorityPoseBaseSample[CharacterTestResolvedAuthorityPoseFrame.MaximumSampleCount];
            internal readonly bool[] BaseValid =
                new bool[CharacterTestResolvedAuthorityPoseFrame.MaximumSampleCount];
            internal readonly FixVector3[] ResolvedPositions =
                new FixVector3[CharacterTestResolvedAuthorityPoseFrame.MaximumSampleCount];
            internal readonly CharacterTestResolvedAuthorityPoseBasis[] ResolvedBases =
                new CharacterTestResolvedAuthorityPoseBasis[CharacterTestResolvedAuthorityPoseFrame.MaximumSampleCount];
            internal readonly bool[] ResolvedValid =
                new bool[CharacterTestResolvedAuthorityPoseFrame.MaximumSampleCount];
            internal readonly CharacterTestAuthorityPoseSampleSourceMask[] SourceMasks =
                new CharacterTestAuthorityPoseSampleSourceMask[CharacterTestResolvedAuthorityPoseFrame.MaximumSampleCount];
            internal readonly CharacterTestAuthorityPoseFailure[] Failures =
                new CharacterTestAuthorityPoseFailure[CharacterTestResolvedAuthorityPoseFrame.MaximumSampleCount];
            internal readonly bool[] TerrainOwnedChannels =
                new bool[CharacterTestResolvedAuthorityPoseFrame.MaximumSampleCount];
            private readonly bool[] _limbConflicts =
                new bool[CharacterTestTerrainIkStateComponent.MaximumLimbCount];

            internal int FindChannelIndex(int channelId)
            {
                IReadOnlyList<CharacterAuthorityPoseChannelDefinition> channels = Definition.Channels;
                int low = 0;
                int high = channels.Count - 1;
                while (low <= high)
                {
                    int middle = low + ((high - low) >> 1);
                    int compare = channels[middle].ChannelId.CompareTo(channelId);
                    if (compare == 0)
                        return middle;
                    if (compare < 0)
                        low = middle + 1;
                    else
                        high = middle - 1;
                }
                return -1;
            }

            internal bool HasChannelConflict(int limbIndex)
            {
                return (uint)limbIndex < (uint)_limbConflicts.Length
                    && _limbConflicts[limbIndex];
            }

            private void MarkTerrainOwnershipAndConflicts()
            {
                IReadOnlyList<CharacterAuthorityGroundProbeDefinition> probes = Definition.GroundProbes;
                for (int i = 0; i < probes.Count; i++)
                {
                    CharacterAuthorityTwoBoneIkBinding ik = FindIkBinding(
                        Definition,
                        probes[i].IkBindingId);
                    if (ik == null)
                    {
                        _limbConflicts[i] = true;
                        continue;
                    }
                    MarkOwned(ik.RootChannelId);
                    MarkOwned(ik.MidChannelId);
                    MarkOwned(ik.TipChannelId);
                    IReadOnlyList<CharacterAuthoritySecondaryBinding> secondary =
                        Definition.SecondaryBindings;
                    for (int j = 0; j < secondary.Count; j++)
                    {
                        int channelId = secondary[j].ChannelId;
                        if ((channelId == ik.RootChannelId)
                            || (channelId == ik.MidChannelId)
                            || (channelId == ik.TipChannelId))
                        {
                            _limbConflicts[i] = true;
                            break;
                        }
                    }
                }
            }

            private void MarkOwned(int channelId)
            {
                int index = FindChannelIndex(channelId);
                if (index >= 0)
                    TerrainOwnedChannels[index] = true;
            }
        }
    }
}
