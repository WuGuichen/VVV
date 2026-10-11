using System;
using System.Collections.Generic;
using System.Globalization;
using MxFramework.Animation;
using MxFramework.CharacterAction;
using MxFramework.Combat.Animation;
using MxFramework.Combat.Core;
using MxFramework.Combat.Damage;
using MxFramework.Combat.GameplayBridge;
using MxFramework.Combat.Hit;
using MxFramework.Combat.Physics;
using MxFramework.Combat.Projectiles;
using MxFramework.Combat.Projectiles.Unity;
using MxFramework.Combat.Shields;
using MxFramework.Core.Math;
using MxFramework.Gameplay;
using MxFramework.Runtime;
using MxFramework.Vfx;
using MxFramework.Vfx.CombatBridge;
using Unity.Profiling;

namespace MxFramework.Game.CharacterTest
{
    public sealed partial class CharacterTestCharacterCombatTraceSystem :
        ICombatProjectileTrackingTargetProvider,
        IAbilityEffectTimelineEventSink
    {
        private static readonly Comparison<KeyValuePair<CharacterTestBodyHitVolumeCacheKey, FixVector3>> CompareBodyHitVolumePreviousCentersCached = CompareBodyHitVolumePreviousCenters;

        private static readonly ProfilerMarker FrameResetMarker =
            new ProfilerMarker("CharacterTest.CombatTrace.FrameReset");
        private static readonly ProfilerMarker ConfigureMarker =
            new ProfilerMarker("CharacterTest.CombatTrace.Configure");
        private static readonly ProfilerMarker AuraAndAreaMarker =
            new ProfilerMarker("CharacterTest.CombatTrace.AuraAndArea");
        private static readonly ProfilerMarker ActionTraceMarker =
            new ProfilerMarker("CharacterTest.CombatTrace.ActionTrace");
        private static readonly ProfilerMarker DamageResolveMarker =
            new ProfilerMarker("CharacterTest.CombatTrace.DamageResolve");
        private static readonly ProfilerMarker GameplayRequestsMarker =
            new ProfilerMarker("CharacterTest.CombatTrace.GameplayRequests");
        private static readonly ProfilerMarker HitResolveMarker =
            new ProfilerMarker("CharacterTest.CombatTrace.HitResolve");
        private static readonly ProfilerMarker ProjectilesMarker =
            new ProfilerMarker("CharacterTest.CombatTrace.Projectiles");
        private static readonly ProfilerMarker HookChainPullMarker =
            new ProfilerMarker("CharacterTest.CombatTrace.HookChainPull");
        private static readonly ProfilerMarker TornadoDamageTickMarker =
            new ProfilerMarker("CharacterTest.CombatTrace.TornadoDamageTick");
        private static readonly ProfilerMarker TornadoDamageIndexRebuildMarker =
            new ProfilerMarker("CharacterTest.CombatTrace.TornadoDamageIndexRebuild");
        private static readonly ProfilerMarker TornadoDamageQueryMarker =
            new ProfilerMarker("CharacterTest.CombatTrace.TornadoDamageQuery");
        private static readonly ProfilerMarker TornadoDamageCandidateResolveMarker =
            new ProfilerMarker("CharacterTest.CombatTrace.TornadoDamageCandidateResolve");
        private static readonly ProfilerMarker AbilityTimelineMarker =
            new ProfilerMarker("CharacterTest.CombatTrace.AbilityTimeline");
        private static readonly ProfilerMarker PoisonTickMarker =
            new ProfilerMarker("CharacterTest.CombatTrace.PoisonTick");
        private static readonly ProfilerMarker ApplyDamageMarker =
            new ProfilerMarker("CharacterTest.CombatTrace.ApplyDamage");
        private static readonly ProfilerMarker OnHitBuffsMarker =
            new ProfilerMarker("CharacterTest.CombatTrace.OnHitBuffs");
        private static readonly ProfilerMarker TracePublishMarker =
            new ProfilerMarker("CharacterTest.CombatTrace.TracePublish");
        private static readonly ProfilerMarker StopRequestsMarker =
            new ProfilerMarker("CharacterTest.CombatTrace.StopRequests");

        private const string LogCategory = "CharacterTest.HitTrace";
        public const int AttackTraceSweepMaxSubsteps = 8;
        public const float AttackTraceSweepMaxTipDistancePerSubstep = 0.2f;
        public const int AttackTraceDebugHistoryFrames = 30;
        private const long ChannelBeamAimCacheMaxAgeFrames = 240L;
        private const long MeteorBarrageCastContextMaxAgeFrames = 160L;
        private const int MeteorBarrageTelegraphLeadFrames = 18;
        private const int MeteorBarrageTelegraphFallbackLifetimeFrames =
            CharacterTestFireballProjectileAdapter.MeteorBarrageLifetimeFrames
            + MeteorBarrageTelegraphLeadFrames
            + CharacterTestVfxIds.MeteorBarrageImpactLifetimeFrames;
        private const string MeteorBarrageTelegraphStopReason = "MeteorBarrageImpact";
        private const int ChainLightningMaxTargets = 4;
        private const int ChainLightningImpactPower = 2;
        private const int ChainLightningSourceOrderBase = 4000;
        private const int HookChainPullDurationFrames = 24;
        private const int HookChainPullStopDistanceMillimeters = 1200;
        private const int HookChainPullAnchorForwardOffsetMillimeters = 1200;
        private const int HookChainPullAccelerationMillimetersPerSecondSquared = 52000;
        private const int HookChainPullTerminalSpeedMillimetersPerSecond = 9000;
        private const int HookChainDirectionScale = CharacterTestDeterministicMath.TrigScale;
        private const int PoisonTickIntervalFrames =
            CharacterTestPoisonIds.PoisonedDamageTickIntervalFrames;
        private const int ChainLightningOriginSocketId = CharacterTestAuthoredContentIds.D400_Id12;
        private const string PoisonTickSourceKey = "poisoned_dot";
        private static readonly Fix64 AttackTraceSweepMaxTipDistancePerSubstepFix =
            Fix64.FromRatio(20, 100);
        private static readonly Fix64 ChainLightningFirstRange = Fix64.FromInt(18);
        private static readonly Fix64 ChainLightningJumpRange = Fix64.FromInt(8);
        private static readonly Fix64 ChainLightningForwardDotThreshold = Fix64.FromRatio(1, 2);
        private static readonly FixVector3 ChainLightningOriginFallbackRootOffset = new FixVector3(
            Fix64.Zero,
            Fix64.FromRatio(115, 100),
            Fix64.FromRatio(65, 100));
        private static readonly int[] ChainLightningSegmentBaseDamages = { 6, 5, 4, 3 };
        private static readonly int[] ChainLightningDamageTags =
        {
            CharacterTestAuthoredContentIds.D410_Id5,
            CharacterTestAuthoredContentIds.D410_Id14,
            CharacterTestAuthoredContentIds.D410_Id20,
            CharacterTestAuthoredContentIds.D410_Id19,
            CharacterTestAuthoredContentIds.D410_Id6
        };

        private readonly CharacterTestHitTraceWindowState _windowState =
            new CharacterTestHitTraceWindowState();
        private readonly CharacterTestCombatEntityLookup _entityLookup =
            new CharacterTestCombatEntityLookup();
        private readonly CharacterTestCombatDamageApplier _damageApplier =
            new CharacterTestCombatDamageApplier();
        private readonly CharacterTestCombatHitResultPublisher _hitResultPublisher =
            new CharacterTestCombatHitResultPublisher();
        private readonly CharacterTestBakedWeaponTraceProvider _traceProvider;
        private readonly CharacterTestTeamRelationProvider _teamRelationProvider =
            new CharacterTestTeamRelationProvider();
        private readonly HitResolveSystem _hitResolve = new HitResolveSystem();
        private readonly CharacterTestDirectHitResolutionService _directHitResolution;
        private readonly CombatProjectileDefinitionRegistry _projectileDefinitions =
            CharacterTestFireballProjectileAdapter.CreateRegistry();
        private readonly CombatProjectileSpawnProfileRegistry _projectileSpawnProfiles =
            CharacterTestFireballProjectileAdapter.CreateSpawnProfileRegistry();
        private readonly CombatProjectileSpawnRequestBuffer _projectileSpawnRequests =
            new CombatProjectileSpawnRequestBuffer();
        private readonly Dictionary<ChannelBeamAimKey, ChannelBeamAimState> _channelBeamAimCache =
            new Dictionary<ChannelBeamAimKey, ChannelBeamAimState>();
        private readonly List<ChannelBeamAimKey> _channelBeamAimCacheRemovals =
            new List<ChannelBeamAimKey>();
        private readonly Dictionary<MeteorBarrageCastKey, MeteorBarrageCastState> _meteorBarrageCastContexts =
            new Dictionary<MeteorBarrageCastKey, MeteorBarrageCastState>();
        private readonly List<MeteorBarrageCastKey> _meteorBarrageCastContextRemovals =
            new List<MeteorBarrageCastKey>();
        private readonly ICharacterTestBakedWeaponTraceSource _bakedTraceSource;
        private readonly ICharacterTestWeaponTraceBindingResolver _bindingResolver;
        private readonly CharacterTestCombatDamageResolver _damageResolver;
        private readonly CharacterTestFireballProjectileImpactResolver _projectileImpactResolver;
        private readonly CharacterTestGuardAuraRuntime _guardAuraRuntime = new CharacterTestGuardAuraRuntime();
        private readonly CharacterTestAreaConfinementRuntime _areaConfinementRuntime =
            new CharacterTestAreaConfinementRuntime();
        private readonly CharacterTestPoisonBottlePoolRuntime _poisonBottlePoolRuntime =
            new CharacterTestPoisonBottlePoolRuntime();
        private readonly CharacterTestPhysicalShieldRuntime _physicalShieldRuntime =
            new CharacterTestPhysicalShieldRuntime();
        private readonly IRuntimeLogger _logger;
        private readonly Func<HitResolveResult, DamageProfileResolveResult> _resolveDamagePayload;
        private readonly Func<HitResolveResult, CombatHitBuffApplicationConfig[]> _resolveOnHitBuffs;
        private readonly HashSet<string> _reportedErrors = new HashSet<string>(StringComparer.Ordinal);
        private readonly List<CombatActionState> _runningActions = new List<CombatActionState>();
        private readonly List<HitCandidate> _candidates = new List<HitCandidate>();
        private readonly List<HitCandidate> _profiledCandidates = new List<HitCandidate>();
        private readonly List<HitCandidate> _gameplayProfiledCandidates = new List<HitCandidate>();
        private readonly List<HitResolveResult> _results = new List<HitResolveResult>();
        private readonly List<CombatQueryResult> _gameplayQueryResults = new List<CombatQueryResult>();
        private readonly List<CombatQueryResult> _timelineQueryResults = new List<CombatQueryResult>();
        private readonly List<CombatQueryResult> _directHitTraceQueryResults = new List<CombatQueryResult>();
        private readonly List<WeaponTraceFrame> _directHitTraceFrames = new List<WeaponTraceFrame>();
        private readonly List<WeaponTraceFrame> _directHitIncomingMotionFrames = new List<WeaponTraceFrame>();
        private readonly List<int> _directHitTracePolicyEventIds = new List<int>();
        private readonly List<CombatCapsuleQuery> _directHitTraceQueryBuffer =
            new List<CombatCapsuleQuery>();
        private readonly Dictionary<CombatQueryHeader, int> _directHitTracePolicyEventIdsByQuery =
            new Dictionary<CombatQueryHeader, int>();
        private readonly CharacterTestDirectHitTracePlanBuffer _directHitTracePlanBuffer =
            new CharacterTestDirectHitTracePlanBuffer();
        private readonly CharacterTestCombatWorldTargetClassificationSnapshot _worldTargetClassifications =
            new CharacterTestCombatWorldTargetClassificationSnapshot();
        private List<CharacterTestDirectHitTracePlanSample> _directHitTracePlanSamples;
        private readonly Dictionary<CharacterTestBodyHitVolumeCacheKey, FixVector3> _bodyHitVolumePreviousCenters =
            new Dictionary<CharacterTestBodyHitVolumeCacheKey, FixVector3>();
        private readonly List<KeyValuePair<CharacterTestBodyHitVolumeCacheKey, FixVector3>>
            _bodyHitVolumePreviousCenterSnapshotScratch =
            new List<KeyValuePair<CharacterTestBodyHitVolumeCacheKey, FixVector3>>(64);
        private readonly List<CharacterTestBodyHitVolumeCacheKey> _bodyHitVolumeCacheRemovals =
            new List<CharacterTestBodyHitVolumeCacheKey>();
        private readonly List<CombatQueryResult> _tornadoDamageQueryResults = new List<CombatQueryResult>();
        private readonly List<CombatQueryResult> _chainLightningQueryResults = new List<CombatQueryResult>();
        private readonly CharacterTestDynamicFieldTargetIndex _tornadoDamageTargetIndex =
            new CharacterTestDynamicFieldTargetIndex();
        private readonly CombatProjectileFrameEventBuffer _projectileFrameEvents =
            new CombatProjectileFrameEventBuffer();
        private readonly List<CombatProjectileActiveView> _activeProjectileViews =
            new List<CombatProjectileActiveView>();
        private readonly List<CharacterTestLaserPathVfxEvent> _laserPathVfxEvents =
            new List<CharacterTestLaserPathVfxEvent>();
        private readonly HashSet<int> _laserPathHitProjectileIds = new HashSet<int>();
        private readonly List<CharacterTestChainLightningVfxEvent> _chainLightningVfxEvents =
            new List<CharacterTestChainLightningVfxEvent>();
        private readonly HashSet<CombatEntityId> _chainLightningVisitedTargets =
            new HashSet<CombatEntityId>();
        private readonly List<ActiveHookPullState> _activeHookPulls = new List<ActiveHookPullState>();
        private readonly List<HitCandidate> _timelineProfiledCandidates = new List<HitCandidate>();
        private readonly List<HitCandidate> _tornadoDamagePayloadCandidates = new List<HitCandidate>();
        private readonly List<CharacterTestAreaConfinementActivatedZone> _areaConfinementActivatedZones =
            new List<CharacterTestAreaConfinementActivatedZone>();
        private readonly List<CharacterTestAreaConfinementCancelledChannel> _areaConfinementCancelledChannels =
            new List<CharacterTestAreaConfinementCancelledChannel>();
        private readonly List<CharacterTestPoisonBottlePoolCreated> _poisonBottleCreatedPools =
            new List<CharacterTestPoisonBottlePoolCreated>();
        private readonly HashSet<WeaponHitOnceKey> _gameplayHitOnceKeys = new HashSet<WeaponHitOnceKey>();
        private readonly HashSet<WeaponHitOnceKey> _timelineHitOnceKeys = new HashSet<WeaponHitOnceKey>();
        private readonly HashSet<WeaponHitOnceKey> _tornadoDamageHitOnceKeys = new HashSet<WeaponHitOnceKey>();
        private readonly HashSet<TimelineProjectileStartKey> _timelineProjectileStartKeys =
            new HashSet<TimelineProjectileStartKey>();
        private readonly HashSet<int> _suppressFrameZeroCombatTimelineInstances = new HashSet<int>();
        private readonly List<int> _timelineSuppressionsToRemove = new List<int>();
        private readonly List<CharacterTestAttackTraceDebugFrame> _attackTraceDebugFrames =
            new List<CharacterTestAttackTraceDebugFrame>();
        private readonly List<CharacterTestHitPointDebugFrame> _hitPointDebugFrames =
            new List<CharacterTestHitPointDebugFrame>();
        private readonly CharacterTestInteractionHistory _interactionHistory =
            new CharacterTestInteractionHistory(AttackTraceDebugHistoryFrames);
        private readonly HashSet<CharacterTestProjectileInteractionKey> _projectileInteractionKeys =
            new HashSet<CharacterTestProjectileInteractionKey>();
        private readonly Dictionary<MeteorBarrageTelegraphKey, VfxInstanceId> _meteorBarrageTelegraphInstances =
            new Dictionary<MeteorBarrageTelegraphKey, VfxInstanceId>();
        private readonly List<VfxInstanceSnapshot> _meteorBarrageActiveVfxInstances =
            new List<VfxInstanceSnapshot>();
        private long _meteorBarrageTelegraphIndexFrame = -1L;
        private readonly Dictionary<CharacterTestTracePayloadKey, CharacterTestTracePayload> _payloadsByTrace =
            new Dictionary<CharacterTestTracePayloadKey, CharacterTestTracePayload>();
        private readonly Dictionary<CharacterTestResolvedHitKey, DamageProfileResolveResult> _damagePayloadsByHit =
            new Dictionary<CharacterTestResolvedHitKey, DamageProfileResolveResult>();
        private readonly Dictionary<CharacterTestResolvedHitKey, CharacterTestHitZoneResolution> _hitZonesByHit =
            new Dictionary<CharacterTestResolvedHitKey, CharacterTestHitZoneResolution>();
        private readonly Dictionary<CharacterTestResolvedHitKey, CombatHitBuffApplicationConfig[]> _onHitBuffsByHit =
            new Dictionary<CharacterTestResolvedHitKey, CombatHitBuffApplicationConfig[]>();
        private readonly Dictionary<CharacterTestResolvedHitKey, CharacterTestWeaponHitAudioMetadata> _weaponHitAudioByHit =
            new Dictionary<CharacterTestResolvedHitKey, CharacterTestWeaponHitAudioMetadata>();
        private readonly List<CharacterTestWeaponHitAudioEvent> _weaponHitAudioEvents =
            new List<CharacterTestWeaponHitAudioEvent>();
        private CharacterTestCharacterPhysicsRuntime _physicsRuntime;
        private CombatProjectileRuntime _projectileRuntime;
        private int _directHitTraceSampleOrder;
        private CharacterTestTraceRecorder _traceRecorder;
        private AbilityDefinitionRegistry _abilityDefinitions = CharacterTestAbilityCatalogSource.CreateRegistry();
        private AbilityEffectTimelineRuntimeModule _effectTimelineRuntime;
        private VfxRuntimeModule _vfxRuntime;
        private CombatProjectileVfxBridge _projectileVfxBridge;
        private Func<int> _lockedTargetEntityIdProvider;
        private Func<CharacterTestCombatTargetHandle> _lockedCombatTargetProvider;
        private CharacterTestPerceptionFrameCache _perceptionFrameCache;
        private RuntimeFrame _frame;
        private CharacterTestCharacterRuntimeWorld _tornadoDamageIndexedWorld;
        private CharacterTestCharacterPhysicsRuntime _tornadoDamageIndexedPhysicsRuntime;
        private long _tornadoDamageIndexedFrame = -1L;
        private bool _hasTornadoDamageTargetIndex;

        public CharacterTestCharacterCombatTraceSystem(IRuntimeLogger logger = null)
            : this(
                logger,
                CharacterTestDefaultBakedWeaponTraceSource.Instance,
                CharacterTestDefaultWeaponTraceBindingResolver.Instance)
        {
        }

        internal CharacterTestCharacterCombatTraceSystem(
            IRuntimeLogger logger,
            ICharacterTestBakedWeaponTraceSource bakedTraceSource,
            ICharacterTestWeaponTraceBindingResolver bindingResolver)
        {
            _logger = logger ?? NullRuntimeLogger.Instance;
            _directHitResolution = new CharacterTestDirectHitResolutionService(
                ReportMissingDirectHitPolicy);
            _damageResolver = new CharacterTestCombatDamageResolver(
                ReportHitTraceError,
                _damageApplier.BodyPartDamageBridge);
            _projectileImpactResolver = new CharacterTestFireballProjectileImpactResolver(
                _damageResolver,
                _entityLookup,
                _projectileDefinitions);
            _bakedTraceSource = bakedTraceSource ?? CharacterTestDefaultBakedWeaponTraceSource.Instance;
            _bindingResolver = bindingResolver ?? CharacterTestDefaultWeaponTraceBindingResolver.Instance;
            _traceProvider = new CharacterTestBakedWeaponTraceProvider(
                _bakedTraceSource,
                _bindingResolver,
                ReportHitTraceError);
            _resolveDamagePayload = ResolveDamagePayload;
            _resolveAccessoryEvidence = ResolveAccessoryEvidence;
            _resolveOnHitBuffs = ResolveOnHitBuffs;
            SetAbilityEffectTimelineRegistry(CharacterTestAbilityEffectTimelineSource.CreateRegistry());
        }

        public int ActiveTraceCount => _windowState.ActiveTraceCount;
        internal CharacterTestHitResolutionPolicyState HitResolutionPolicyState =>
            _windowState.HitResolutionPolicyState;
        public int LastCandidateCount => _candidates.Count;
        public int LastResolvedHitCount => _results.Count;
        public IReadOnlyList<CharacterTestAttackTraceDebugFrame> AttackTraceDebugFrames => _attackTraceDebugFrames;
        public IReadOnlyList<CharacterTestHitPointDebugFrame> HitPointDebugFrames => _hitPointDebugFrames;
        internal CharacterTestDirectHitTracePlanCollectionView DirectHitTracePlans =>
            _directHitTracePlanBuffer.CreateView();
        internal CharacterTestCombatWorldTargetClassificationSnapshot WorldTargetClassifications =>
            _worldTargetClassifications;

        internal void ResealWorldTargetClassificationsAfterAuthorityRestore(RuntimeFrame frame)
        {
            int worldRevision = _physicsRuntime?.World != null
                ? _physicsRuntime.World.Revision
                : -1;
            _worldTargetClassifications.Reset(frame, worldRevision);
            if (worldRevision >= 0)
                _worldTargetClassifications.Seal(frame, worldRevision);
        }
        internal ICharacterTestBakedWeaponTraceSource BakedTraceSource => _bakedTraceSource;
        internal ICharacterTestWeaponTraceBindingResolver WeaponTraceBindingResolver => _bindingResolver;
        public IReadOnlyList<DamageTelemetryRow> DamageTelemetryRows => _damageApplier.DamageTelemetryRows;
        public IReadOnlyList<DamageResolvedEvent> DamageResolvedEvents => _damageApplier.DamageResolvedEvents;
        public IReadOnlyList<HitPresentationEvent> HitPresentationEvents => _damageApplier.HitPresentationEvents;
        internal CharacterTestBodyPartDamageAuthorityBridge BodyPartDamageBridge =>
            _damageApplier.BodyPartDamageBridge;

        internal void ResetBodyPartAuthorityForRestore()
        {
            _damageApplier.ResetBodyPartAuthorityForRestore();
        }
        internal IReadOnlyList<CharacterTestWeaponHitAudioEvent> WeaponHitAudioEvents => _weaponHitAudioEvents;

        internal CharacterTestCombatTraceReplayState CaptureRuntimeAiLabReplayState()
        {
            return new CharacterTestCombatTraceReplayState(
                _windowState.CaptureReplayState(),
                _bodyHitVolumePreviousCenters,
                _guardAuraRuntime.CaptureRuntimeAiLabCommandBufferSnapshot());
        }

        internal CharacterTestHitResolutionPolicyNetworkSnapshot CaptureNetworkHitResolutionPolicySnapshot()
        {
            CharacterTestHitResolutionPolicyNetworkSnapshot snapshot = _windowState.CaptureNetworkSnapshot();
            _bodyHitVolumePreviousCenterSnapshotScratch.Clear();
            foreach (KeyValuePair<CharacterTestBodyHitVolumeCacheKey, FixVector3> pair
                in _bodyHitVolumePreviousCenters)
            {
                _bodyHitVolumePreviousCenterSnapshotScratch.Add(pair);
            }
            _bodyHitVolumePreviousCenterSnapshotScratch.Sort(CompareBodyHitVolumePreviousCentersCached);
            var centers = new CharacterTestBodyHitVolumePreviousCenterNetworkSnapshot[
                _bodyHitVolumePreviousCenterSnapshotScratch.Count];
            for (int i = 0; i < centers.Length; i++)
            {
                KeyValuePair<CharacterTestBodyHitVolumeCacheKey, FixVector3> pair =
                    _bodyHitVolumePreviousCenterSnapshotScratch[i];
                centers[i] = new CharacterTestBodyHitVolumePreviousCenterNetworkSnapshot
                {
                    EntityId = pair.Key.EntityId,
                    HitWindowId = pair.Key.HitWindowId,
                    CenterXRaw = pair.Value.X.RawValue,
                    CenterYRaw = pair.Value.Y.RawValue,
                    CenterZRaw = pair.Value.Z.RawValue
                };
            }
            snapshot.BodyHitVolumePreviousCenters = centers;
            return snapshot;
        }

        internal bool TryApplyNetworkHitResolutionPolicySnapshot(
            CharacterTestHitResolutionPolicyNetworkSnapshot snapshot,
            out string failure)
        {
            if (!TryValidateBodyHitVolumePreviousCenters(snapshot, out failure))
                return false;

            if (!_windowState.TryRestoreNetworkSnapshot(
                snapshot,
                _entityLookup,
                out failure))
            {
                _bodyHitVolumePreviousCenters.Clear();
                return false;
            }

            _bodyHitVolumePreviousCenters.Clear();
            CharacterTestBodyHitVolumePreviousCenterNetworkSnapshot[] centers =
                snapshot.BodyHitVolumePreviousCenters;
            for (int i = 0; i < centers.Length; i++)
            {
                CharacterTestBodyHitVolumePreviousCenterNetworkSnapshot center = centers[i];
                _bodyHitVolumePreviousCenters.Add(
                    new CharacterTestBodyHitVolumeCacheKey(center.EntityId, center.HitWindowId),
                    new FixVector3(
                        Fix64.FromRaw(center.CenterXRaw),
                        Fix64.FromRaw(center.CenterYRaw),
                        Fix64.FromRaw(center.CenterZRaw)));
            }
            return true;
        }

        internal void AddNetworkHitResolutionPolicyHash(
            RuntimeHashAccumulator accumulator,
            int attackerEntityIdFilter = 0,
            bool excludeAttacker = false)
        {
            _windowState.AddNetworkStateHash(
                accumulator,
                attackerEntityIdFilter,
                excludeAttacker);
            AddBodyHitVolumePreviousCentersHash(
                accumulator,
                attackerEntityIdFilter,
                excludeAttacker);
        }

        private bool TryValidateBodyHitVolumePreviousCenters(
            CharacterTestHitResolutionPolicyNetworkSnapshot snapshot,
            out string failure)
        {
            failure = string.Empty;
            if (snapshot == null)
            {
                failure = "CharacterTest body hit-volume center snapshot is null.";
                return false;
            }
            if (snapshot.BodyHitVolumePreviousCenters == null)
            {
                failure = "CharacterTest body hit-volume center snapshot array is missing.";
                return false;
            }

            int previousEntityId = 0;
            int previousHitWindowId = 0;
            CharacterTestHitTraceNetworkSnapshot[] traces =
                snapshot.ActiveTraces ?? Array.Empty<CharacterTestHitTraceNetworkSnapshot>();
            for (int i = 0; i < snapshot.BodyHitVolumePreviousCenters.Length; i++)
            {
                CharacterTestBodyHitVolumePreviousCenterNetworkSnapshot entry =
                    snapshot.BodyHitVolumePreviousCenters[i];
                if (entry == null
                    || entry.EntityId <= 0
                    || entry.HitWindowId <= 0
                    || entry.CenterXRaw == long.MinValue
                    || entry.CenterYRaw == long.MinValue
                    || entry.CenterZRaw == long.MinValue)
                {
                    failure = "CharacterTest body hit-volume center snapshot contains an invalid entry.";
                    return false;
                }
                if (i > 0
                    && (entry.EntityId < previousEntityId
                        || (entry.EntityId == previousEntityId
                            && entry.HitWindowId <= previousHitWindowId)))
                {
                    failure = "CharacterTest body hit-volume centers are not in stable order or contain duplicates.";
                    return false;
                }
                previousEntityId = entry.EntityId;
                previousHitWindowId = entry.HitWindowId;

                if (_entityLookup.FindEntity(entry.EntityId) == null)
                {
                    failure = "CharacterTest body hit-volume center references an unknown source entity: "
                        + entry.EntityId.ToString(CultureInfo.InvariantCulture);
                    return false;
                }

                bool foundCasterBodyTrace = false;
                for (int traceIndex = 0; traceIndex < traces.Length; traceIndex++)
                {
                    CharacterTestHitTraceNetworkSnapshot trace = traces[traceIndex];
                    if (trace != null
                        && trace.EntityId == entry.EntityId
                        && trace.HitWindowId == entry.HitWindowId
                        && trace.HitVolume != null
                        && trace.HitVolume.SourceKind == (int)CharacterActionHitVolumeSourceKind.CasterBody)
                    {
                        foundCasterBodyTrace = true;
                        break;
                    }
                }
                if (!foundCasterBodyTrace)
                {
                    failure = "CharacterTest body hit-volume center references a non-active CasterBody trace.";
                    return false;
                }
            }

            return true;
        }

        private void AddBodyHitVolumePreviousCentersHash(
            RuntimeHashAccumulator accumulator,
            int sourceEntityIdFilter,
            bool excludeSourceEntity)
        {
            accumulator.BeginObject(CharacterTestCharacterCombatTraceSystemHashFields.F7, CharacterTestCharacterCombatTraceSystemHashFields.SchemaId, CharacterTestCharacterCombatTraceSystemHashFields.SchemaVersion, true);
            bool filtered = sourceEntityIdFilter > 0;
            _bodyHitVolumePreviousCenterSnapshotScratch.Clear();
            foreach (KeyValuePair<CharacterTestBodyHitVolumeCacheKey, FixVector3> pair
                in _bodyHitVolumePreviousCenters)
            {
                bool sourceMatches = pair.Key.EntityId == sourceEntityIdFilter;
                if (!filtered || (excludeSourceEntity ? !sourceMatches : sourceMatches))
                    _bodyHitVolumePreviousCenterSnapshotScratch.Add(pair);
            }
            _bodyHitVolumePreviousCenterSnapshotScratch.Sort(CompareBodyHitVolumePreviousCentersCached);
            accumulator.AddInt(
                CharacterTestCharacterCombatTraceSystemHashFields.F1,
                _bodyHitVolumePreviousCenterSnapshotScratch.Count);
            for (int i = 0; i < _bodyHitVolumePreviousCenterSnapshotScratch.Count; i++)
            {
                KeyValuePair<CharacterTestBodyHitVolumeCacheKey, FixVector3> pair =
                    _bodyHitVolumePreviousCenterSnapshotScratch[i];

                accumulator.AddInt(CharacterTestCharacterCombatTraceSystemHashFields.F2, pair.Key.EntityId);
                accumulator.AddInt(CharacterTestCharacterCombatTraceSystemHashFields.F3, pair.Key.HitWindowId);
                accumulator.AddLong(CharacterTestCharacterCombatTraceSystemHashFields.F4, pair.Value.X.RawValue);
                accumulator.AddLong(CharacterTestCharacterCombatTraceSystemHashFields.F5, pair.Value.Y.RawValue);
                accumulator.AddLong(CharacterTestCharacterCombatTraceSystemHashFields.F6, pair.Value.Z.RawValue);
            }
            accumulator.EndObject();
        }

        private static int CompareBodyHitVolumePreviousCenters(
            KeyValuePair<CharacterTestBodyHitVolumeCacheKey, FixVector3> left,
            KeyValuePair<CharacterTestBodyHitVolumeCacheKey, FixVector3> right)
        {
            int compare = left.Key.EntityId.CompareTo(right.Key.EntityId);
            if (compare != 0)
                return compare;
            compare = left.Key.HitWindowId.CompareTo(right.Key.HitWindowId);
            if (compare != 0)
                return compare;
            compare = left.Value.X.RawValue.CompareTo(right.Value.X.RawValue);
            if (compare != 0)
                return compare;
            compare = left.Value.Y.RawValue.CompareTo(right.Value.Y.RawValue);
            return compare != 0 ? compare : left.Value.Z.RawValue.CompareTo(right.Value.Z.RawValue);
        }

        internal void RestoreRuntimeAiLabReplayState(CharacterTestCombatTraceReplayState state)
        {
            _windowState.RestoreReplayState(state?.WindowState);
            _bodyHitVolumePreviousCenters.Clear();
            _bodyHitVolumeCacheRemovals.Clear();
            state?.CopyBodyHitVolumeCentersTo(_bodyHitVolumePreviousCenters);
            if (state != null)
                _guardAuraRuntime.RestoreRuntimeAiLabCommandBufferSnapshot(state.GuardAuraCommandBuffer);
        }

        public IReadOnlyList<ScatterPressureChangedEvent> ScatterPressureChangedEvents => _damageApplier.ScatterPressureChangedEvents;
        public IReadOnlyList<ScatterImpactResolvedEvent> ScatterImpactResolvedEvents => _damageApplier.ScatterImpactResolvedEvents;
        public IReadOnlyList<PressureBandChangedEvent> ScatterPressureBandChangedEvents => _damageApplier.ScatterPressureBandChangedEvents;
        public IReadOnlyList<CohesionBreakEvent> CohesionBreakEvents => _damageApplier.CohesionBreakEvents;
        internal IReadOnlyList<CharacterTestDeathEvent> DeathEvents => _damageApplier.DeathEvents;
        public CombatProjectileDebugSnapshot ProjectileDebugSnapshot => _projectileRuntime?.CreateDebugSnapshot();
        public CharacterTestInteractionHistory InteractionHistory => _interactionHistory;
        public CharacterTestInteractionDebugSnapshot InteractionDebugSnapshot =>
            _interactionHistory.CreateDebugSnapshot();
        public IReadOnlyList<CharacterTestInteractionEvent> LastFrameInteractionEvents =>
            _interactionHistory.LastFrameEvents;
        public IReadOnlyList<CharacterTestLaserPathVfxEvent> LastFrameLaserPathVfxEvents => _laserPathVfxEvents;
        public IReadOnlyList<CharacterTestChainLightningVfxEvent> LastFrameChainLightningVfxEvents =>
            _chainLightningVfxEvents;
        public int AbilityEffectTimelineActiveInstanceCount => _effectTimelineRuntime?.ActiveInstanceCount ?? 0;
        public int LastAbilityEffectTimelineStartCount { get; private set; }
        public int LastAbilityEffectTimelineVfxSpawnCount { get; private set; }
        public int LastAbilityEffectTimelineCombatEventCount { get; private set; }
        public int LastAbilityEffectTimelineSuppressedCombatEventCount { get; private set; }
        public long ProjectileSpawnProfileHash => _projectileSpawnProfiles.ComputeStableHash();
        public CombatGameplayAuraBuffTickSummary GuardAuraLastTickSummary => _guardAuraRuntime.LastTickSummary;
        public CombatOnHitBuffApplicationSummary OnHitBuffLastSummary => _guardAuraRuntime.OnHitBuffLastSummary;
        public CharacterTestAreaConfinementTickSummary AreaConfinementLastTickSummary =>
            _areaConfinementRuntime.LastTickSummary;
        public CharacterTestPoisonBottlePoolTickSummary PoisonBottlePoolLastTickSummary =>
            _poisonBottlePoolRuntime.LastTickSummary;
        public CharacterTestPhysicalShieldTickSummary PhysicalShieldLastTickSummary =>
            _physicalShieldRuntime.LastTickSummary;
        public int ActiveAreaConfinementZoneCount => _areaConfinementRuntime.ActiveZoneCount;
        public int ActivePoisonBottlePoolCount => _poisonBottlePoolRuntime.ActivePoolCount;
        public int ActivePhysicalShieldCount => _physicalShieldRuntime.ActiveCount;
        internal CombatShieldRuntime PhysicalShieldRuntime => _physicalShieldRuntime.Runtime;
        public int LastTornadoAttractionTargetCount { get; private set; }
        public int LastTornadoDamageTickTargetCount { get; private set; }
        public int LastChainLightningTargetCount { get; private set; }
        public int LastPoisonTickTargetCount { get; private set; }
        public int ActiveProjectileCount => _projectileRuntime?.ActiveCount ?? 0;
        public int ActiveHookPullCount => _activeHookPulls.Count;
        public int ActiveMeteorBarrageCastContextCount => _meteorBarrageCastContexts.Count;
        internal IReadOnlyList<CharacterTestScatterPressureTrace> ScatterPressureTraces => _damageApplier.ScatterPressureTraces;

        public CombatProjectileRuntimeState ReadProjectileStateView()
        {
            return _projectileRuntime?.ReadStateView();
        }

        public CombatProjectileRuntimeState CreateProjectileStateSnapshot()
        {
            return _projectileRuntime?.CreateStateSnapshot();
        }

        public int CopyActiveProjectileViewsTo(List<CombatProjectileActiveView> output)
        {
            if (output == null)
                throw new ArgumentNullException(nameof(output));

            return _projectileRuntime?.CopyActiveViewsTo(output) ?? 0;
        }

        public bool TryApplyProjectileStateSnapshot(
            CombatProjectileRuntimeState state,
            out string failure)
        {
            failure = string.Empty;
            if (_projectileRuntime == null)
            {
                if (state == null || state.ActiveCount == 0)
                    return true;

                failure = "CharacterTest projectile runtime is missing.";
                return false;
            }

            CombatProjectileRuntimeState restoreState = state
                ?? new CombatProjectileRuntimeState(
                    CombatProjectileRuntimeState.CurrentSchemaVersion,
                    CombatProjectileRuntimeModule.DefaultModuleId,
                    _projectileRuntime.DefinitionHash,
                    nextProjectileId: 1,
                    activeProjectiles: null);
            return _projectileRuntime.TryRestoreStateSnapshot(restoreState, out failure);
        }

        private CharacterTestHookPullRuntimeState _hashCharacterTestHookPullRuntimeState;

        internal CharacterTestHookPullRuntimeState ReadHookPullStateView()
        {
            if (_hashCharacterTestHookPullRuntimeState == null)
            {
                var view = new CharacterTestRuntimeStateView<CharacterTestHookPullSnapshot>(() => _activeHookPulls.Count, i => _activeHookPulls[i].ToSnapshot());
            _hashCharacterTestHookPullRuntimeState = new CharacterTestHookPullRuntimeState(
                CharacterTestHookPullRuntimeState.CurrentSchemaVersion,
                CharacterTestHookPullRuntimeState.DefaultModuleId,
                view, borrow: true);
            }
            return _hashCharacterTestHookPullRuntimeState;
        }

        internal CharacterTestHookPullRuntimeState CreateHookPullStateSnapshot()
        {
            var snapshots = new CharacterTestHookPullSnapshot[_activeHookPulls.Count];
            for (int i = 0; i < _activeHookPulls.Count; i++)
                snapshots[i] = _activeHookPulls[i].ToSnapshot();

            return new CharacterTestHookPullRuntimeState(
                CharacterTestHookPullRuntimeState.CurrentSchemaVersion,
                CharacterTestHookPullRuntimeState.DefaultModuleId,
                snapshots);
        }

        internal bool TryApplyHookPullStateSnapshot(
            CharacterTestHookPullRuntimeState state,
            out string failure)
        {
            failure = string.Empty;
            if (state != null)
            {
                if (state.SchemaVersion != CharacterTestHookPullRuntimeState.CurrentSchemaVersion || state.ModuleId != CharacterTestHookPullRuntimeState.DefaultModuleId)
                { failure = "Hook-pull snapshot schema/module mismatch."; return false; }
                var identities = new HashSet<(int, int, int, int, int)>();
                for (int i = 0; i < state.ActiveCount; i++)
                {
                    CharacterTestHookPullSnapshot item = state.ActivePulls[i];
                    if (item.CasterEntityId <= 0 || item.TargetEntityId <= 0 || item.ActionInstanceId <= 0 || item.ProjectileId <= 0
                        || item.StableEventId <= 0 || !CharacterTestAuthoredContentIds.IsActive(401, item.StableEventId)
                        || !identities.Add((item.CasterEntityId, item.TargetEntityId, item.ActionInstanceId, item.ProjectileId, item.StableEventId)))
                    { failure = "Hook-pull snapshot contains an invalid or duplicate source event/instance identity."; return false; }
                }
            }
            _activeHookPulls.Clear();
            if (state == null || state.ActiveCount == 0)
                return true;

            IReadOnlyList<CharacterTestHookPullSnapshot> pulls = state.ActivePulls;
            for (int i = 0; i < pulls.Count; i++)
            {
                CharacterTestHookPullSnapshot pull = pulls[i];
                _activeHookPulls.Add(ActiveHookPullState.FromSnapshot(pull));
            }

            return true;
        }

        internal CharacterTestAreaConfinementRuntimeState ReadAreaConfinementStateView()
        {
            return _areaConfinementRuntime.ReadStateView();
        }

        internal CharacterTestAreaConfinementRuntimeState CreateAreaConfinementStateSnapshot()
        {
            return _areaConfinementRuntime.CreateStateSnapshot();
        }

        internal bool TryApplyAreaConfinementStateSnapshot(
            CharacterTestAreaConfinementRuntimeState state,
            out string failure)
        {
            return _areaConfinementRuntime.TryApplyStateSnapshot(state, out failure);
        }

        internal CharacterTestMechanicSourceRuntimeState ReadPoisonBottlePoolStateView()
        {
            return _poisonBottlePoolRuntime.ReadStateView();
        }

        internal CharacterTestMechanicSourceRuntimeState CreatePoisonBottlePoolStateSnapshot()
        {
            return _poisonBottlePoolRuntime.CreateStateSnapshot();
        }

        internal bool TryApplyPoisonBottlePoolStateSnapshot(
            CharacterTestMechanicSourceRuntimeState state,
            out string failure)
        {
            return _poisonBottlePoolRuntime.TryApplyStateSnapshot(state, out failure);
        }

        internal CharacterTestPhysicalShieldRuntimeState ReadPhysicalShieldStateView()
        {
            return _physicalShieldRuntime.ReadStateView();
        }

        internal CharacterTestPhysicalShieldRuntimeState CreatePhysicalShieldStateSnapshot()
        {
            return _physicalShieldRuntime.CreateStateSnapshot();
        }

        internal bool TryApplyPhysicalShieldStateSnapshot(
            CharacterTestPhysicalShieldRuntimeState state,
            out string failure)
        {
            return _physicalShieldRuntime.TryApplyStateSnapshot(state, _physicsRuntime, out failure);
        }

        internal void CaptureIncomingSourceMotionStart(CharacterTestCharacterRuntimeWorld world, RuntimeFrame frame)
            => _traceProvider.CaptureSourceMotionStart(world, frame);

        internal void ClearIncomingSourceMotion() => _traceProvider.ClearSourceMotion();

        public void SetPhysicsRuntime(CharacterTestCharacterPhysicsRuntime physicsRuntime)
        {
            _physicsRuntime = physicsRuntime;
            _projectileSpawnRequests.Clear();
            _directHitTraceQueryResults.Clear();
            _directHitTraceFrames.Clear();
            _directHitIncomingMotionFrames.Clear();
            _traceProvider.ClearSourceMotion();
            _directHitTracePolicyEventIds.Clear();
            _directHitTraceQueryBuffer.Clear();
            _directHitTracePolicyEventIdsByQuery.Clear();
            _directHitTracePlanBuffer.Reset(_frame, physicsRuntime?.World?.Revision ?? -1);
            _worldTargetClassifications.Reset(
                _frame,
                physicsRuntime?.World?.Revision ?? -1);
            _directHitTracePlanSamples = null;
            _directHitTraceSampleOrder = 0;
            _physicalShieldRuntime.Configure(physicsRuntime?.World);
            _channelBeamAimCache.Clear();
            _channelBeamAimCacheRemovals.Clear();
            _meteorBarrageCastContexts.Clear();
            _meteorBarrageCastContextRemovals.Clear();
            _chainLightningQueryResults.Clear();
            _chainLightningVisitedTargets.Clear();
            _chainLightningVfxEvents.Clear();
            _activeHookPulls.Clear();
            _arcMineRuntime.Clear();
            _bodyHitVolumePreviousCenters.Clear();
            _bodyHitVolumeCacheRemovals.Clear();
            _projectileRuntime = physicsRuntime?.World == null
                ? null
                : new CombatProjectileRuntime(
                    _projectileDefinitions,
                    _projectileSpawnRequests,
                    physicsRuntime.World,
                    _projectileImpactResolver,
                    _teamRelationProvider,
                    _entityLookup,
                    allowFriendlyFire: false,
                    trackingTargetProvider: this,
                    directSweepProvider: new CharacterTestSwordWaveDirectSweepProvider(physicsRuntime));
        }

        public void SetTraceRecorder(CharacterTestTraceRecorder traceRecorder)
        {
            _traceRecorder = traceRecorder;
        }

        private CharacterTestTraceRecorder GetVerboseCombatTraceRecorder()
        {
            return _traceRecorder != null && _traceRecorder.WritesFile ? _traceRecorder : null;
        }

        private HitTargetStateFlags ResolveTargetState(CombatEntityId targetEntityId)
        {
            return _entityLookup.ResolveTargetState(targetEntityId);
        }

        public void SetAbilityDefinitions(AbilityDefinitionRegistry abilityDefinitions)
        {
            _abilityDefinitions = abilityDefinitions ?? CharacterTestAbilityCatalogSource.CreateRegistry();
        }

        public void SetAbilityEffectTimelineRegistry(AbilityEffectTimelineRegistry timelines)
        {
            _effectTimelineRuntime = new AbilityEffectTimelineRuntimeModule(
                timelines ?? new AbilityEffectTimelineRegistry(),
                this,
                moduleId: "CharacterTest.AbilityEffectTimeline");
            _suppressFrameZeroCombatTimelineInstances.Clear();
            _timelineProjectileStartKeys.Clear();
        }

        public void SetVfxRuntime(VfxRuntimeModule vfxRuntime)
        {
            _vfxRuntime = vfxRuntime;
        }

        /// <summary>Attaches the standard v2 projectile source-presentation bridge.</summary>
        /// <remarks>LowFreqAlloc during composition only; frame routing is NoAlloc after warmup.</remarks>
        public void SetProjectileVfxBridge(CombatProjectileVfxBridge bridge)
        {
            _projectileVfxBridge = bridge;
        }

        public void SetLockedTargetEntityIdProvider(Func<int> provider)
        {
            _lockedTargetEntityIdProvider = provider;
        }

        internal void SetLockedCombatTargetProvider(
            Func<CharacterTestCombatTargetHandle> provider)
        {
            _lockedCombatTargetProvider = provider;
        }

        internal void SetPerceptionFrameCache(CharacterTestPerceptionFrameCache perceptionFrameCache)
        {
            _perceptionFrameCache = perceptionFrameCache;
        }

        public int ActiveChannelBeamAimCount => _channelBeamAimCache.Count;

        public bool TryResolveMeteorBarrageLandingPoint(
            int entityId,
            long actionInstanceId,
            int meteorIndex,
            out FixVector3 landingPoint)
        {
            if (_meteorBarrageCastContexts.TryGetValue(
                    new MeteorBarrageCastKey(entityId, actionInstanceId),
                    out MeteorBarrageCastState state))
            {
                landingPoint = state.ResolveLandingPoint(meteorIndex);
                return true;
            }

            landingPoint = FixVector3.Zero;
            return false;
        }

        public bool TryResolveChannelBeamAimDirection(
            int entityId,
            long actionInstanceId,
            out FixVector3 direction)
        {
            if (_channelBeamAimCache.TryGetValue(
                    new ChannelBeamAimKey(entityId, actionInstanceId),
                    out ChannelBeamAimState state))
            {
                direction = state.Direction;
                return true;
            }

            direction = FixVector3.Zero;
            return false;
        }

        /// <remarks>NoAlloc on the steady-state path after registration/initialization; invalid-call and exception paths may allocate diagnostics.</remarks>
        public void Tick(
            CharacterTestCharacterRuntimeWorld world,
            CharacterTestCharacterActionRuntime actionRuntime,
            RuntimeFrame frame,
            ICharacterTestStatusControlProvider statusControl = null)
        {
            _frame = frame;
            using (FrameResetMarker.Auto())
            {
                PruneAttackTraceDebugFrames(frame.Value);
                PruneHitPointDebugFrames(frame.Value);
                PruneChannelBeamAimCache(world, frame.Value);
                PruneMeteorBarrageCastContexts(world, frame.Value);
                _interactionHistory.BeginFrame(frame);
                _projectileInteractionKeys.Clear();
                _runningActions.Clear();
                _candidates.Clear();
                _profiledCandidates.Clear();
                _gameplayProfiledCandidates.Clear();
                _timelineProfiledCandidates.Clear();
                _tornadoDamagePayloadCandidates.Clear();
                _results.Clear();
                _gameplayQueryResults.Clear();
                _timelineQueryResults.Clear();
                _directHitTraceQueryResults.Clear();
                _directHitTraceFrames.Clear();
                _directHitTracePolicyEventIds.Clear();
                _directHitTraceQueryBuffer.Clear();
                _directHitTracePolicyEventIdsByQuery.Clear();
                _directHitTracePlanBuffer.Reset(
                    frame,
                    _physicsRuntime?.World != null ? _physicsRuntime.World.Revision : -1);
                _worldTargetClassifications.Reset(
                    frame,
                    _physicsRuntime?.World != null ? _physicsRuntime.World.Revision : -1);
                _directHitTracePlanSamples = null;
                _directHitTraceSampleOrder = 0;
                _tornadoDamageQueryResults.Clear();
                _chainLightningQueryResults.Clear();
                _projectileFrameEvents.Clear();
                _activeProjectileViews.Clear();
                _laserPathVfxEvents.Clear();
                _laserPathHitProjectileIds.Clear();
                _chainLightningVfxEvents.Clear();
                _chainLightningVisitedTargets.Clear();
                _gameplayHitOnceKeys.Clear();
                _timelineHitOnceKeys.Clear();
                _tornadoDamageHitOnceKeys.Clear();
                _timelineProjectileStartKeys.Clear();
                LastTornadoDamageTickTargetCount = 0;
                LastChainLightningTargetCount = 0;
                LastPoisonTickTargetCount = 0;
                LastArcMineBlastTargetCount = 0;
                LastRuneTurretShotCount = 0;
                LastAbilityEffectTimelineStartCount = 0;
                LastAbilityEffectTimelineVfxSpawnCount = 0;
                LastAbilityEffectTimelineCombatEventCount = 0;
                LastAbilityEffectTimelineSuppressedCombatEventCount = 0;
                _damageApplier.ClearFrameOutputs();
                _payloadsByTrace.Clear();
                _damagePayloadsByHit.Clear();
                _accessoryEvidenceByHit.Clear();
                _hitZonesByHit.Clear();
                _onHitBuffsByHit.Clear();
                _weaponHitAudioByHit.Clear();
                _weaponHitAudioEvents.Clear();
            }

            if (world == null)
                return;

            using (ConfigureMarker.Auto())
            {
                _entityLookup.Configure(world, _windowState.TargetStateContributions);
                CharacterTestCombatEntityLookup.TickCombatStates(world);
            }

            if (_physicsRuntime == null || _physicsRuntime.World == null)
                return;

            using (ConfigureMarker.Auto())
            {
                _windowState.ProcessStartCombatRequests(world, frame, GetVerboseCombatTraceRecorder());
                _windowState.RemoveEndedTraces(_entityLookup);
                _windowState.PruneIneligiblePartDependentTraces(
                    _entityLookup,
                    actionRuntime,
                    frame,
                    GetVerboseCombatTraceRecorder());
                _traceProvider.Configure(
                    world,
                    _physicsRuntime,
                    _windowState.ActiveTraces,
                    _attackTraceDebugFrames,
                    frame.Value);
                _teamRelationProvider.Configure(world, _physicsRuntime);
            }

            using (AuraAndAreaMarker.Auto())
            {
                ProcessGuardAuraRequests(world, frame);
                ProcessAreaConfinementRequests(world, frame);
                ProcessArcMineRequests(world, actionRuntime, frame);
                ProcessRuneTurretRequests(world, actionRuntime, frame);
                ProcessPhysicalShieldRequests(world, frame);
                _guardAuraRuntime.Tick(world, _physicsRuntime, frame);
                _areaConfinementRuntime.Tick(world, _physicsRuntime, frame);
                _arcMineRuntime.Tick(world, _physicsRuntime, frame);
                ProcessArcMineRuntimeEvents(world, frame);
                _runeTurretRuntime.Tick(world, _physicsRuntime, frame);
                ProcessRuneTurretRuntimeEvents(frame);
                _physicalShieldRuntime.Tick(world, _physicsRuntime, frame);
                SpawnAreaConfinementRuntimeVfx(frame);
            }

            // Current production CombatPhysics does not author Wall/Ground
            // subtype metadata. Seal one empty authority after other combat
            // systems have finished their frame-start physics writes; every
            // encountered world collider is an Unknown fail-closed blocker.
            _worldTargetClassifications.Reset(frame, _physicsRuntime.World.Revision);
            _worldTargetClassifications.Seal(frame, _physicsRuntime.World.Revision);

            using (ActionTraceMarker.Auto())
            {
                if (actionRuntime != null)
                {
                    BuildRunningActions(actionRuntime, statusControl);
                    if (_runningActions.Count > 0)
                        EvaluateWeaponTracePlan(
                            new CombatFrame((int)Math.Min(int.MaxValue, Math.Max(0, frame.Value))));

                    ProcessActiveHitVolumes(world, actionRuntime, statusControl, frame);
                }
            }

            using (DamageResolveMarker.Auto())
            {
                _damageResolver.ResolveCandidates(
                    _candidates,
                    _payloadsByTrace,
                    _entityLookup,
                    _profiledCandidates,
                    _damagePayloadsByHit,
                    _hitZonesByHit,
                    _onHitBuffsByHit,
                    _weaponHitAudioByHit,
                    _physicsRuntime.World,
                    frame,
                    _worldTargetClassifications,
                    _directHitTracePolicyEventIdsByQuery,
                    _accessoryEvidenceByHit);
            }

            using (GameplayRequestsMarker.Auto())
            {
                ProcessChainLightningRequests(world, actionRuntime, frame);
                ProcessGameplayDamageRequests(world, actionRuntime, frame);
            }

            using (HitResolveMarker.Auto())
            {
                _directHitResolution.Resolve(
                    _profiledCandidates,
                    _payloadsByTrace,
                    _directHitTracePolicyEventIdsByQuery,
                    _windowState.HitResolutionPolicyState,
                    _results,
                    _teamRelationProvider,
                    _entityLookup);
                if (_gameplayProfiledCandidates.Count > 0)
                {
                    _gameplayHitOnceKeys.Clear();
                    _hitResolve.Resolve(
                        _gameplayProfiledCandidates,
                        _gameplayHitOnceKeys,
                        _results,
                        _teamRelationProvider,
                        _entityLookup);
                    _gameplayHitOnceKeys.Clear();
                }
            }

            using (ProjectilesMarker.Auto())
            {
                ProcessProjectiles(world, frame);
                _poisonBottlePoolRuntime.Tick(world, _physicsRuntime, frame);
                SpawnPoisonBottlePoolRuntimeVfx(frame);
            }

            using (HookChainPullMarker.Auto())
            {
                ProcessHookChainPulls(world, frame);
            }

            using (AbilityTimelineMarker.Auto())
            {
                ProcessAbilityEffectTimelines(world, frame);
            }

            using (PoisonTickMarker.Auto())
            {
                ProcessPoisonTicks(world, frame);
            }

            using (ApplyDamageMarker.Auto())
            {
                RecordHitPointDebugFrames(frame.Value);
                _damageApplier.Apply(
                    world,
                    actionRuntime,
                    frame,
                    _results,
                    _resolveDamagePayload,
                    _hitZonesByHit,
                    _traceRecorder,
                    _resolveAccessoryEvidence);
                BuildWeaponHitAudioEvents(frame);
                if (_areaConfinementRuntime.CancelPendingForDamageEvents(_damageApplier.DamageResolvedEvents, frame) > 0)
                    SpawnAreaConfinementCancelledChannelVfx(frame);
            }

            using (OnHitBuffsMarker.Auto())
            {
                _guardAuraRuntime.ApplyOnHitBuffs(world, _physicsRuntime, frame, _results, _resolveOnHitBuffs);
            }

            using (TracePublishMarker.Auto())
            {
                TraceScatterPressure(world, frame);
                _hitResultPublisher.Publish(
                    world,
                    _results,
                    _damageApplier.DamageTelemetryRows,
                    _damagePayloadsByHit,
                    frame,
                    GetVerboseCombatTraceRecorder(),
                    _entityLookup,
                    _windowState.ActiveTraceCount,
                    _candidates.Count
                    + _gameplayQueryResults.Count
                    + _arcMineRuntime.LastTickSummary.QueryHitCount
                    + _chainLightningQueryResults.Count
                    + _tornadoDamageQueryResults.Count
                    + LastPoisonTickTargetCount
                    + CountProjectileImpactQueries(),
                    _traceRecorder);
            }

            using (StopRequestsMarker.Auto())
            {
                _windowState.ProcessStopCombatRequestsAndClear(world, frame, GetVerboseCombatTraceRecorder());
            }

            _directHitTracePlanBuffer.Seal(frame, _physicsRuntime.World.Revision);
        }

        internal sealed class CharacterTestCombatTraceReplayState
        {
            private readonly KeyValuePair<CharacterTestBodyHitVolumeCacheKey, FixVector3>[] _bodyCenters;

            internal CharacterTestCombatTraceReplayState(
                CharacterTestHitTraceWindowReplayState windowState,
                Dictionary<CharacterTestBodyHitVolumeCacheKey, FixVector3> bodyCenters,
                RuntimeCommandBufferSnapshot guardAuraCommandBuffer)
            {
                WindowState = windowState;
                GuardAuraCommandBuffer = guardAuraCommandBuffer;
                _bodyCenters = new KeyValuePair<CharacterTestBodyHitVolumeCacheKey, FixVector3>[bodyCenters.Count];
                int index = 0;
                foreach (KeyValuePair<CharacterTestBodyHitVolumeCacheKey, FixVector3> pair in bodyCenters)
                    _bodyCenters[index++] = pair;
                Array.Sort(_bodyCenters, CompareBodyCenters);

            }

            public CharacterTestHitTraceWindowReplayState WindowState { get; }
            internal RuntimeCommandBufferSnapshot GuardAuraCommandBuffer { get; }
            internal void CopyBodyHitVolumeCentersTo(
                Dictionary<CharacterTestBodyHitVolumeCacheKey, FixVector3> output)
            {
                for (int i = 0; i < _bodyCenters.Length; i++)
                    output.Add(_bodyCenters[i].Key, _bodyCenters[i].Value);
            }

            private static int CompareBodyCenters(
                KeyValuePair<CharacterTestBodyHitVolumeCacheKey, FixVector3> left,
                KeyValuePair<CharacterTestBodyHitVolumeCacheKey, FixVector3> right)
            {
                int compare = left.Key.EntityId.CompareTo(right.Key.EntityId);
                if (compare != 0)
                    return compare;
                compare = left.Key.HitWindowId.CompareTo(right.Key.HitWindowId);
                if (compare != 0)
                    return compare;
                compare = left.Value.X.RawValue.CompareTo(right.Value.X.RawValue);
                if (compare != 0)
                    return compare;
                compare = left.Value.Y.RawValue.CompareTo(right.Value.Y.RawValue);
                return compare != 0 ? compare : left.Value.Z.RawValue.CompareTo(right.Value.Z.RawValue);
            }
        }
    }
}

namespace MxFramework.Game.CharacterTest
{
    // Frozen v3 protocol numbers. Retired entries remain reserved.
    internal static class CharacterTestCharacterCombatTraceSystemHashFields
    {
        internal const int SchemaId = RuntimeHashSchemaIds.CombatTrace;
        internal const int SchemaVersion = 1;
        internal const int F1 = 1; // characterTest.hitTrace.bodyPreviousCenter.count
        internal const int F2 = 2; // .entityId
        internal const int F3 = 3; // .hitWindowId
        internal const int F4 = 4; // .x.raw
        internal const int F5 = 5; // .y.raw
        internal const int F6 = 6; // .z.raw
        internal const int F7 = 7; // $scope.AddBodyHitVolumePreviousCentersHash
    }
}
