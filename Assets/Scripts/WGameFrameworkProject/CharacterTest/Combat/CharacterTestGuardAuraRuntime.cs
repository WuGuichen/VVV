using System;
using System.Collections.Generic;
using MxFramework.Combat.Core;
using MxFramework.Combat.GameplayBridge;
using MxFramework.Combat.Hit;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using MxFramework.Gameplay;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    public static class CharacterTestGuardAuraIds
    {
        public const int AuraId = 501008;
        public const int BuffId = 601008;
        public const long SourceDurationFrames = 300L;
        public const long GuardAuraBuffDurationFrames = 2L;
        public const int RadiusMeters = 4;
        public const int DefenseBonus = 5;
        public const int OnHitDefenseBreakBuffId = 601009;
        public const long OnHitDefenseBreakDurationFrames = 300L;
        public const int OnHitDefensePenalty = -3;
        public const string DefaultGeneratedBuffDefinitionsPath =
            "Assets/MxFrameworkGenerated/Gameplay/Buffs/buff_definitions.json";
        public const string DefaultGeneratedBuffPresentationDefinitionsPath =
            "Assets/MxFrameworkGenerated/Gameplay/Buffs/buff_presentation_definitions.json";
        public const string DefaultGeneratedAuraDefinitionsPath =
            "Assets/MxFrameworkGenerated/Combat/Auras/aura_buff_definitions.json";
    }

    public static class CharacterTestAreaConfinementIds
    {
        public const int MechanicSourceId = 501010;
        public const int RootedBuffId = 601010;
        public const int MotionPolicyId = 701010;
        public const int AreaConfinedStatusId = 901010;
        public const long ZoneDurationFrames = 180L;
        public const long RootedBuffDurationFrames = 2L;
        public const int TelegraphFrames = 60;
        public const int TelegraphCancelFrames = 12;
        public const int VfxFadeInFrames = 12;
        public const int VfxFadeOutFrames = 18;
        public const int CharacterReleaseFrames = 18;
        public const int RadiusCentimeters = 250;
        public const int FallbackForwardRangeMeters = 6;
        public const string SourceStableId = "mechanic.character_test.area_confinement.zone";
        public const string MotionPolicyStableId = "mechanic.character_test.area_confinement.root_active_movement";
        public const string RootedBuffStableId = "buff.character_test.area_confinement.rooted";
    }

    public static class CharacterTestStasisIds
    {
        public const int StasisBuffId = 601011;
        public const int StasisStatusId = 901011;
        public const int StasisMotionPolicyId = 701011;
        public const long StasisBuffDurationFrames = 90L;
        public const string StasisBuffStableId = "buff.character_test.stasis.action_stop";
        public const string StasisStatusStableId = "status.character_test.stasis";
        public const string StasisMotionPolicyStableId = "mechanic.character_test.stasis.control_scale_zero";
        public const int StasisActionFreezeStableEventId = 134;
        public const string StasisActionFreezeStableEventLabel = "character_test.stasis.action_freeze";
        public const string StasisBuffNameKey = "buff.control.stasis.name";
        public const string StasisBuffDescriptionKey = "buff.control.stasis.description";
        public const string ActionFrozenRejectReason = "ActionFrozenByStasis";
    }

    public static class CharacterTestStunIds
    {
        public const int StunBuffId = 601013;
        public const int StunnedStatusId = 901013;
        public const int StunMotionPolicyId = 701013;
        public const long StunBuffDurationFrames = 90L;
        public const string StunBuffStableId = "buff.character_test.stun.control";
        public const string StunnedStatusStableId = "status.character_test.stunned";
        public const string StunMotionPolicyStableId = "mechanic.character_test.stun.control_scale_zero";
        public const int StunControlStableEventId = 135;
        public const string StunControlStableEventLabel = "character_test.stun.control_freeze";
        public const string StunBuffNameKey = "buff.control.stun.name";
        public const string StunBuffDescriptionKey = "buff.control.stun.description";
        public const string ActionFrozenRejectReason = "ActionFrozenByStun";
    }

    public static class CharacterTestParalysisIds
    {
        public const int ParalyzedStatusId = 901014;
        public const string ParalyzedStatusStableId = "status.character_test.paralyzed";
        public const string ActionFrozenRejectReason = "ActionFrozenByParalysis";
    }

    public static class CharacterTestPoisonIds
    {
        public const int PoisonBottlePoolSourceId = 501012;
        public const int PoisonedBuffId = 601012;
        public const int PoisonedStatusId = 901012;
        public const long PoisonBottlePoolDurationFrames = 180L;
        public const int PoisonBottlePoolTickIntervalFrames = 30;
        public const int PoisonBottlePoolRadiusCentimeters = 250;
        public const long PoisonedBuffDurationFrames = 360L;
        internal const int PoisonedDamageTickIntervalFrames = 60;
        internal const int PoisonedDamagePerTick = 2;
        public const string PoisonBottlePoolSourceStableId = "mechanic.character_test.poison_bottle.pool";
        public const string PoisonedBuffStableId = "buff.character_test.poisoned.dot";
        public const string PoisonedStatusStableId = "status.character_test.poisoned";
        public const string PoisonedBuffNameKey = "buff.damage.poisoned.name";
        public const string PoisonedBuffDescriptionKey = "buff.damage.poisoned.description";
    }

    internal sealed partial class CharacterTestGuardAuraRuntime
    {
        private static readonly RuntimeCommand[] EmptyRuntimeCommands =
            Array.Empty<RuntimeCommand>();
        private static readonly HitResolveResult[] EmptyHitResults =
            Array.Empty<HitResolveResult>();
        private static readonly CombatHitBuffApplicationConfig[] EmptyOnHitBuffs =
            Array.Empty<CombatHitBuffApplicationConfig>();

        private readonly RuntimeCommandBuffer _commandBuffer = new RuntimeCommandBuffer();
        private readonly CharacterTestGuardAuraAnchorProvider _anchorProvider =
            new CharacterTestGuardAuraAnchorProvider();
        private readonly GameplayComponentAbilityRegistry _abilityRegistry =
            new GameplayComponentAbilityRegistry();
        private readonly GameplayBuffRequestBuffer _buffRequests =
            new GameplayBuffRequestBuffer();
        private readonly GameplayBuffDefinitionRegistry _buffDefinitions =
            CreateBuffDefinitions();
        private readonly CombatGameplayAuraBuffDefinitionRegistry _auraDefinitions =
            CreateAuraDefinitions();
        private readonly GameplayWorld _onHitGameplayWorld = new GameplayWorld();
        private readonly GameplayCommandExecutionState _onHitCommandState =
            new GameplayCommandExecutionState();
        private GameplayRuntimeModule _module;
        private CharacterTestCharacterRuntimeWorld _world;
        private CharacterTestCharacterPhysicsRuntime _physicsRuntime;
        private CombatGameplayAuraBuffSystem _auraSystem;
        private CombatOnHitBuffApplicationSystem _onHitSystem;
        private GameplayBuffApplicationSystem _onHitBuffApplicationSystem;
        private GameplayBuffEffectProjectionSystem _onHitBuffProjectionSystem;
        private IReadOnlyList<HitResolveResult> _currentOnHitResults = EmptyHitResults;
        private Func<HitResolveResult, CombatHitBuffApplicationConfig[]> _currentOnHitBuffResolver;

        public CharacterTestGuardAuraRuntime()
        {
            _abilityRegistry.Register(new CombatGameplayAuraBuffAbility(
                CharacterTestAbilityIds.GuardAuraAbilityId,
                CharacterTestGuardAuraIds.AuraId,
                CharacterTestGuardAuraIds.SourceDurationFrames));
        }

        public CombatGameplayAuraBuffTickSummary LastTickSummary =>
            _auraSystem?.LastTickSummary ?? default;

        public CombatOnHitBuffApplicationSummary OnHitBuffLastSummary { get; private set; }

        internal RuntimeCommandBufferSnapshot CaptureRuntimeAiLabCommandBufferSnapshot()
        {
            return _commandBuffer.CaptureSnapshot();
        }

        internal void RestoreRuntimeAiLabCommandBufferSnapshot(RuntimeCommandBufferSnapshot snapshot)
        {
            _commandBuffer.RestoreSnapshot(snapshot);
        }

        public RuntimeCommandValidationResult EnqueueCast(
            CharacterTestCharacterRuntimeWorld world,
            CharacterTestCharacterPhysicsRuntime physicsRuntime,
            CharacterTestCharacterEntity caster,
            RuntimeFrame frame,
            int traceId)
        {
            if (!EnsureModule(world, physicsRuntime))
            {
                return RuntimeCommandValidationResult.Failed(new RuntimeCommandError(
                    RuntimeCommandErrorCode.InvalidPayload,
                    GameplayRuntimeCommandFactory.CastComponentAbility(
                        frame,
                        caster != null ? caster.GameplayEntityId : default,
                        CharacterTestAbilityIds.GuardAuraAbilityId,
                        traceId: new RuntimeTraceIdentity(CharacterTestFrameworkTraceKinds.CombatRuntimeEffectVfx, caster != null ? caster.Id.Value : 0, frame.Value, traceId)),
                    frame,
                    "CharacterTest guard aura runtime is unavailable."));
            }

            return _commandBuffer.Enqueue(GameplayRuntimeCommandFactory.CastComponentAbility(
                frame,
                caster.GameplayEntityId,
                CharacterTestAbilityIds.GuardAuraAbilityId,
                traceId: new RuntimeTraceIdentity(CharacterTestFrameworkTraceKinds.CombatRuntimeEffectVfx, caster != null ? caster.Id.Value : 0, frame.Value, traceId)));
        }

        /// <remarks>NoAlloc on the steady-state path after registration/initialization; invalid-call and exception paths may allocate diagnostics.</remarks>
        public void Tick(
            CharacterTestCharacterRuntimeWorld world,
            CharacterTestCharacterPhysicsRuntime physicsRuntime,
            RuntimeFrame frame)
        {
            if (!EnsureModule(world, physicsRuntime))
                return;

            _module.Tick(new RuntimeTickContext(
                frame.Value,
                deltaTime: 0d,
                elapsedTime: 0d,
                RuntimeTickStage.Simulation));
        }

        public CombatOnHitBuffApplicationSummary ApplyOnHitBuffs(
            CharacterTestCharacterRuntimeWorld world,
            CharacterTestCharacterPhysicsRuntime physicsRuntime,
            RuntimeFrame frame,
            IReadOnlyList<HitResolveResult> results,
            Func<HitResolveResult, CombatHitBuffApplicationConfig[]> getOnHitBuffs,
            Func<HitResolveResult, CharacterTestAccessoryHitEvidence> evidence = null,
            Func<HitResolveResult, TacticalCard.TacticalSecondBatchHit> parentReceipt = null)
        {
            if (!EnsureModule(world, physicsRuntime))
            {
                OnHitBuffLastSummary = default;
                return OnHitBuffLastSummary;
            }

            _currentOnHitResults = results ?? EmptyHitResults;
            _currentOnHitBuffResolver = getOnHitBuffs;
            _buffEvidence = evidence; _buffParentReceipt = parentReceipt;
            PrepareAccessoryBuffOrigins();
            try
            {
                GameplaySystemContext context = CreateGameplayContext(world, frame);
                _onHitSystem.Tick(context);
                OnHitBuffLastSummary = _onHitSystem.LastSummary;
                if (OnHitBuffLastSummary.RequestedBuffCount > 0)
                {
                    _accessoryBuffResults.Prepare(_buffRequests.Count);
                    _onHitBuffApplicationSystem.Tick(context);
                    PublishAccessoryBuffOrigins(world, frame);
                    _onHitBuffProjectionSystem.Tick(context);
                }
            }
            finally
            {
                _currentOnHitResults = EmptyHitResults;
                _currentOnHitBuffResolver = null;
                _buffEvidence = null; _buffParentReceipt = null;
            }

            return OnHitBuffLastSummary;
        }

        private bool EnsureModule(
            CharacterTestCharacterRuntimeWorld world,
            CharacterTestCharacterPhysicsRuntime physicsRuntime)
        {
            if (world == null || physicsRuntime?.World == null)
                return false;

            if (_module != null && ReferenceEquals(_world, world) && ReferenceEquals(_physicsRuntime, physicsRuntime))
                return true;

            _world = world;
            _physicsRuntime = physicsRuntime;
            _anchorProvider.SetPhysicsRuntime(physicsRuntime);
            _auraSystem = new CombatGameplayAuraBuffSystem(
                world.CombatGameplayMap,
                world.GameplayComponentWorld,
                physicsRuntime.World,
                _auraDefinitions,
                _anchorProvider,
                _buffRequests,
                systemId: "character_test.guard_aura.buff_refresh");
            _onHitSystem = new CombatOnHitBuffApplicationSystem(
                world.CombatGameplayMap,
                _buffRequests,
                GetCurrentOnHitResults,
                ResolveCurrentOnHitBuffs,
                systemId: "character_test.on_hit_buff.application");
            _buffCorrelation ??= RecordBuffOrigin;
            _onHitSystem.RequestCorrelation = _buffCorrelation;
            _onHitBuffApplicationSystem = new GameplayBuffApplicationSystem(
                _buffDefinitions,
                _buffRequests,
                "character_test.on_hit_buff.buff_application", resultSink: _accessoryBuffResults);
            _onHitBuffProjectionSystem = new GameplayBuffEffectProjectionSystem(
                _buffDefinitions,
                "character_test.on_hit_buff.buff_projection");
            _module = new GameplayRuntimeModule(
                new GameplayWorld(),
                new GameplayAbilityRegistry(),
                _commandBuffer,
                tickWorldAutomatically: false,
                moduleId: "CharacterTest.GuardAuraGameplay",
                configureDefaultPipeline: pipeline =>
                {
                    pipeline.Add(new GameplayComponentAbilityCommandSystem(
                        _abilityRegistry,
                        buffRequests: _buffRequests));
                    pipeline.Add(new GameplayComponentAuraSourceCleanupSystem(
                        "character_test.guard_aura.source_cleanup"));
                    pipeline.Add(new GameplayComponentBuffCleanupSystem(
                        "character_test.guard_aura.buff_cleanup"));
                    pipeline.Add(_auraSystem);
                    pipeline.Add(new GameplayBuffApplicationSystem(
                        _buffDefinitions,
                        _buffRequests,
                        "character_test.guard_aura.buff_application"));
                    pipeline.Add(new GameplayBuffEffectProjectionSystem(
                        _buffDefinitions,
                        "character_test.guard_aura.buff_projection"));
                },
                componentWorld: world.GameplayComponentWorld);
            return true;
        }

        private IReadOnlyList<HitResolveResult> GetCurrentOnHitResults()
        {
            return _currentOnHitResults ?? EmptyHitResults;
        }

        private CombatHitBuffApplicationConfig[] ResolveCurrentOnHitBuffs(HitResolveResult result)
        {
            return _currentOnHitBuffResolver != null
                ? _currentOnHitBuffResolver(result) ?? EmptyOnHitBuffs
                : EmptyOnHitBuffs;
        }

        internal static GameplayBuffDefinitionRegistry CreateBuffDefinitions()
        {
            return CreateBuffDefinitions(CharacterTestGuardAuraIds.DefaultGeneratedBuffDefinitionsPath);
        }

        internal static GameplayBuffDefinitionRegistry CreateBuffDefinitions(string generatedPath)
        {
            GameplayBuffDefinitionRegistry generated =
                CharacterTestRuntimeCatalogBinaryFallback.LoadBinaryThenJsonOrEmpty(
                    generatedPath,
                    GameplayBuffDefinitionBinaryImporter.CatalogKind,
                    GameplayBuffDefinitionBinaryImporter.ImportFile,
                    GameplayBuffDefinitionJsonImporter.ImportFile,
                    registry => registry?.Count ?? 0,
                    () => new GameplayBuffDefinitionRegistry());
            return generated.Count > 0 ? generated : CreateEmbeddedBuffDefinitions();
        }

        internal static CombatGameplayAuraBuffDefinitionRegistry CreateAuraDefinitions()
        {
            return CreateAuraDefinitions(CharacterTestGuardAuraIds.DefaultGeneratedAuraDefinitionsPath);
        }

        internal static GameplayBuffPresentationDefinitionRegistry CreateBuffPresentationDefinitions()
        {
            return CreateBuffPresentationDefinitions(
                CharacterTestGuardAuraIds.DefaultGeneratedBuffPresentationDefinitionsPath);
        }

        internal static GameplayBuffPresentationDefinitionRegistry CreateBuffPresentationDefinitions(
            string generatedPath)
        {
            GameplayBuffPresentationDefinitionRegistry generated =
                CharacterTestRuntimeCatalogBinaryFallback.LoadBinaryThenJsonOrEmpty(
                    generatedPath,
                    GameplayBuffPresentationDefinitionBinaryImporter.CatalogKind,
                    GameplayBuffPresentationDefinitionBinaryImporter.ImportFile,
                    GameplayBuffPresentationDefinitionJsonImporter.ImportFile,
                    registry => registry?.Count ?? 0,
                    () => new GameplayBuffPresentationDefinitionRegistry());
            return generated.Count > 0 ? generated : CreateEmbeddedBuffPresentationDefinitions();
        }

        internal static CombatGameplayAuraBuffDefinitionRegistry CreateAuraDefinitions(string generatedPath)
        {
            CombatGameplayAuraBuffDefinitionRegistry generated =
                CharacterTestRuntimeCatalogBinaryFallback.LoadBinaryThenJsonOrEmpty(
                    generatedPath,
                    CombatGameplayAuraBuffDefinitionBinaryImporter.CatalogKind,
                    CombatGameplayAuraBuffDefinitionBinaryImporter.ImportFile,
                    CombatGameplayAuraBuffDefinitionJsonImporter.ImportFile,
                    registry => registry?.Count ?? 0,
                    () => new CombatGameplayAuraBuffDefinitionRegistry());
            return generated.Count > 0 ? generated : CreateEmbeddedAuraDefinitions();
        }

        internal static GameplayBuffDefinitionRegistry CreateEmbeddedBuffDefinitions()
        {
            var registry = new GameplayBuffDefinitionRegistry(
                new GameplayBuffDefinition(
                    CharacterTestGuardAuraIds.BuffId,
                    "buff.character_test.guard_aura.defense",
                    CharacterTestGuardAuraIds.GuardAuraBuffDurationFrames,
                    maxStackCount: 1,
                    GameplayBuffStackPolicy.RefreshDuration,
                    new[]
                    {
                        GameplayBuffEffectDefinition.AttributeModifier(
                            1,
                            CharacterTestCharacterAttributeIds.Defense,
                            CharacterTestGuardAuraIds.DefenseBonus)
                    }),
                new GameplayBuffDefinition(
                    CharacterTestGuardAuraIds.OnHitDefenseBreakBuffId,
                    "buff.character_test.on_hit.defense_break",
                    CharacterTestGuardAuraIds.OnHitDefenseBreakDurationFrames,
                    maxStackCount: 1,
                    GameplayBuffStackPolicy.RefreshDuration,
                    new[]
                    {
                        GameplayBuffEffectDefinition.AttributeModifier(
                            1,
                            CharacterTestCharacterAttributeIds.Defense,
                            CharacterTestGuardAuraIds.OnHitDefensePenalty)
                    }),
                new GameplayBuffDefinition(
                    CharacterTestAreaConfinementIds.RootedBuffId,
                    CharacterTestAreaConfinementIds.RootedBuffStableId,
                    CharacterTestAreaConfinementIds.RootedBuffDurationFrames,
                    maxStackCount: 1,
                    GameplayBuffStackPolicy.RefreshDuration,
                    new[]
                    {
                        GameplayBuffEffectDefinition.Status(
                            1,
                            new GameplayStatusId(CharacterTestAreaConfinementIds.AreaConfinedStatusId))
                    }),
                new GameplayBuffDefinition(
                    CharacterTestStasisIds.StasisBuffId,
                    CharacterTestStasisIds.StasisBuffStableId,
                    CharacterTestStasisIds.StasisBuffDurationFrames,
                    maxStackCount: 1,
                    GameplayBuffStackPolicy.RefreshDuration,
                    new[]
                    {
                        GameplayBuffEffectDefinition.Status(
                            1,
                            new GameplayStatusId(CharacterTestStasisIds.StasisStatusId))
                    }),
                new GameplayBuffDefinition(
                    CharacterTestStunIds.StunBuffId,
                    CharacterTestStunIds.StunBuffStableId,
                    CharacterTestStunIds.StunBuffDurationFrames,
                    maxStackCount: 1,
                    GameplayBuffStackPolicy.RefreshDuration,
                    new[]
                    {
                        GameplayBuffEffectDefinition.Status(
                            1,
                            new GameplayStatusId(CharacterTestStunIds.StunnedStatusId))
                    }),
                new GameplayBuffDefinition(
                    CharacterTestPoisonIds.PoisonedBuffId,
                    CharacterTestPoisonIds.PoisonedBuffStableId,
                    CharacterTestPoisonIds.PoisonedBuffDurationFrames,
                    maxStackCount: 1,
                    GameplayBuffStackPolicy.RefreshDuration,
                    new[]
                    {
                        GameplayBuffEffectDefinition.Status(
                            1,
                            new GameplayStatusId(CharacterTestPoisonIds.PoisonedStatusId))
                    }));
            CharacterTestRuntimeCatalogLoadDiagnostics.RecordEmbedded(
                GameplayBuffDefinitionBinaryImporter.CatalogKind,
                registry.Count,
                "Generated buff catalog was empty or missing.");
            return registry;
        }

        internal static CombatGameplayAuraBuffDefinitionRegistry CreateEmbeddedAuraDefinitions()
        {
            var registry = new CombatGameplayAuraBuffDefinitionRegistry(new CombatGameplayAuraBuffDefinition(
                CharacterTestGuardAuraIds.AuraId,
                CharacterTestGuardAuraIds.BuffId,
                radius: Fix64.FromInt(CharacterTestGuardAuraIds.RadiusMeters),
                layerMask: CombatPhysicsLayerMask.FromLayer(CharacterTestCharacterPhysicsRuntime.DynamicCharacterLayer),
                relationFilter: GameplayTargetRelationFilter.SameTeam,
                includeCaster: true));
            CharacterTestRuntimeCatalogLoadDiagnostics.RecordEmbedded(
                CombatGameplayAuraBuffDefinitionBinaryImporter.CatalogKind,
                registry.Count,
                "Generated aura buff catalog was empty or missing.");
            return registry;
        }

        internal static GameplayBuffPresentationDefinitionRegistry CreateEmbeddedBuffPresentationDefinitions()
        {
            var registry = new GameplayBuffPresentationDefinitionRegistry(
                new GameplayBuffPresentationDefinition(
                    CharacterTestGuardAuraIds.BuffId,
                    "CharacterTest Guard Aura Defense",
                    "buff.character_test.guard_aura.defense.name",
                    "DEF+",
                    "防御+5，瞬时，不可叠加",
                    "Auto",
                    string.Empty,
                    GameplayBuffPresentationPolarity.Buff,
                    150,
                    showOnPlayerHud: true,
                    showOnTargetHud: true,
                    showOnHeadPad: true,
                    "Defense(3) +5"),
                new GameplayBuffPresentationDefinition(
                    CharacterTestGuardAuraIds.OnHitDefenseBreakBuffId,
                    "CharacterTest On-Hit Defense Break",
                    "buff.character_test.on_hit.defense_break.name",
                    "DEF-",
                    "防御-3，持续5秒，不可叠加",
                    "Auto",
                    string.Empty,
                    GameplayBuffPresentationPolarity.Debuff,
                    200,
                    showOnPlayerHud: false,
                    showOnTargetHud: true,
                    showOnHeadPad: true,
                    "Defense(3) -3"),
                new GameplayBuffPresentationDefinition(
                    CharacterTestAreaConfinementIds.RootedBuffId,
                    "CharacterTest Area Confinement Rooted",
                    "buff.character_test.area_confinement.rooted.name",
                    "ROOT",
                    "Active movement locked; rotation and external motion remain available.",
                    "Manual",
                    string.Empty,
                    GameplayBuffPresentationPolarity.Debuff,
                    210,
                    showOnPlayerHud: false,
                    showOnTargetHud: true,
                    showOnHeadPad: true,
                    "Status(901010) AreaConfined"),
                new GameplayBuffPresentationDefinition(
                    CharacterTestStasisIds.StasisBuffId,
                    "Stasis",
                    CharacterTestStasisIds.StasisBuffNameKey,
                    "STOP",
                    "Self-driven action, movement, and facing pause; external motion remains active.",
                    "Manual",
                    string.Empty,
                    GameplayBuffPresentationPolarity.Debuff,
                    220,
                    showOnPlayerHud: true,
                    showOnTargetHud: true,
                    showOnHeadPad: true,
                    "Status(901011) Stasis"),
                new GameplayBuffPresentationDefinition(
                    CharacterTestStunIds.StunBuffId,
                    "Stunned",
                    CharacterTestStunIds.StunBuffNameKey,
                    "STUN",
                    "Cannot move, turn, or start actions while the stun control status is active.",
                    "Manual",
                    string.Empty,
                    GameplayBuffPresentationPolarity.Debuff,
                    225,
                    showOnPlayerHud: true,
                    showOnTargetHud: true,
                    showOnHeadPad: true,
                    "Status(901013) Stunned"),
                new GameplayBuffPresentationDefinition(
                    CharacterTestPoisonIds.PoisonedBuffId,
                    "Poisoned",
                    CharacterTestPoisonIds.PoisonedBuffNameKey,
                    "POIS",
                    "Takes toxic damage over time; repeated hits refresh duration without stacking.",
                    "Manual",
                    string.Empty,
                    GameplayBuffPresentationPolarity.Debuff,
                    230,
                    showOnPlayerHud: true,
                    showOnTargetHud: true,
                    showOnHeadPad: true,
                    "Status(901012) Poisoned"));
            CharacterTestRuntimeCatalogLoadDiagnostics.RecordEmbedded(
                GameplayBuffPresentationDefinitionBinaryImporter.CatalogKind,
                registry.Count,
                "Generated buff presentation catalog was empty or missing.");
            return registry;
        }

        private GameplaySystemContext CreateGameplayContext(
            CharacterTestCharacterRuntimeWorld world,
            RuntimeFrame frame)
        {
            _onHitCommandState.Clear();
            return new GameplaySystemContext(
                frame,
                0d,
                0d,
                _onHitGameplayWorld,
                EmptyRuntimeCommands,
                world.GameplayComponentWorld.Events,
                _onHitCommandState,
                componentWorld: world.GameplayComponentWorld);
        }

        private sealed class CharacterTestGuardAuraAnchorProvider : ICombatGameplayAuraAnchorProvider
        {
            private CharacterTestCharacterPhysicsRuntime _physicsRuntime;

            public void SetPhysicsRuntime(CharacterTestCharacterPhysicsRuntime physicsRuntime)
            {
                _physicsRuntime = physicsRuntime;
            }

            public bool TryGetAuraCenter(CombatEntityId sourceCombatId, out FixVector3 center)
            {
                if (_physicsRuntime?.World == null)
                {
                    center = FixVector3.Zero;
                    return false;
                }

                if (_physicsRuntime.World.TryGetBody(new CombatBodyId(sourceCombatId.Value), out CombatPhysicsBody body))
                {
                    center = body.Position;
                    return true;
                }

                center = FixVector3.Zero;
                return false;
            }
        }
    }

    internal readonly struct CharacterTestAreaConfinementZoneCastResult
    {
        public CharacterTestAreaConfinementZoneCastResult(
            bool success,
            FixVector3 center,
            CharacterTestAreaConfinementLandingMode landingMode,
            string failureReason,
            CharacterTestMechanicSourceCreateResult sourceCreateResult)
        {
            Success = success;
            Center = center;
            LandingMode = landingMode;
            FailureReason = failureReason ?? string.Empty;
            SourceCreateResult = sourceCreateResult;
        }

        public bool Success { get; }
        public FixVector3 Center { get; }
        public CharacterTestAreaConfinementLandingMode LandingMode { get; }
        public string FailureReason { get; }
        public CharacterTestMechanicSourceCreateResult SourceCreateResult { get; }

        public static CharacterTestAreaConfinementZoneCastResult Succeeded(
            FixVector3 center,
            CharacterTestAreaConfinementLandingMode landingMode,
            CharacterTestMechanicSourceCreateResult sourceCreateResult)
        {
            return new CharacterTestAreaConfinementZoneCastResult(
                true,
                center,
                landingMode,
                string.Empty,
                sourceCreateResult);
        }

        public static CharacterTestAreaConfinementZoneCastResult Failed(string failureReason)
        {
            return new CharacterTestAreaConfinementZoneCastResult(
                false,
                FixVector3.Zero,
                CharacterTestAreaConfinementLandingMode.None,
                failureReason,
                CharacterTestMechanicSourceCreateResult.Failed(failureReason));
        }
    }

    internal readonly struct CharacterTestAreaConfinementChannelStartResult
    {
        private CharacterTestAreaConfinementChannelStartResult(
            bool success,
            int pendingChannelId,
            FixVector3 center,
            FixVector3 forward,
            CharacterTestAreaConfinementLandingMode landingMode,
            string failureReason,
            long startFrame,
            long activationFrame,
            int localFrame,
            int sourceOrder,
            int traceId)
        {
            Success = success;
            PendingChannelId = pendingChannelId;
            Center = center;
            Forward = forward;
            LandingMode = landingMode;
            FailureReason = failureReason ?? string.Empty;
            StartFrame = startFrame;
            ActivationFrame = activationFrame;
            LocalFrame = localFrame;
            SourceOrder = sourceOrder;
            TraceId = traceId;
        }

        public bool Success { get; }
        public int PendingChannelId { get; }
        public FixVector3 Center { get; }
        public FixVector3 Forward { get; }
        public CharacterTestAreaConfinementLandingMode LandingMode { get; }
        public string FailureReason { get; }
        public long StartFrame { get; }
        public long ActivationFrame { get; }
        public int LocalFrame { get; }
        public int SourceOrder { get; }
        public int TraceId { get; }

        public static CharacterTestAreaConfinementChannelStartResult Succeeded(
            int pendingChannelId,
            FixVector3 center,
            FixVector3 forward,
            CharacterTestAreaConfinementLandingMode landingMode,
            long startFrame,
            long activationFrame,
            int localFrame,
            int sourceOrder,
            int traceId)
        {
            return new CharacterTestAreaConfinementChannelStartResult(
                true,
                pendingChannelId,
                center,
                forward,
                landingMode,
                string.Empty,
                startFrame,
                activationFrame,
                localFrame,
                sourceOrder,
                traceId);
        }

        public static CharacterTestAreaConfinementChannelStartResult Failed(string failureReason)
        {
            return new CharacterTestAreaConfinementChannelStartResult(
                false,
                0,
                FixVector3.Zero,
                FixVector3.Zero,
                CharacterTestAreaConfinementLandingMode.None,
                failureReason,
                0L,
                0L,
                0,
                0,
                0);
        }
    }

    internal readonly struct CharacterTestAreaConfinementActivatedZone
    {
        public CharacterTestAreaConfinementActivatedZone(
            int casterEntityId,
            FixVector3 center,
            FixVector3 forward,
            CharacterTestAreaConfinementLandingMode landingMode,
            int traceId,
            int localFrame,
            int sourceOrder,
            CharacterTestMechanicSourceCreateResult sourceCreateResult)
        {
            CasterEntityId = casterEntityId;
            Center = center;
            Forward = forward;
            LandingMode = landingMode;
            TraceId = traceId;
            LocalFrame = localFrame;
            SourceOrder = sourceOrder;
            SourceCreateResult = sourceCreateResult;
        }

        public int CasterEntityId { get; }
        public FixVector3 Center { get; }
        public FixVector3 Forward { get; }
        public CharacterTestAreaConfinementLandingMode LandingMode { get; }
        public int TraceId { get; }
        public int LocalFrame { get; }
        public int SourceOrder { get; }
        public CharacterTestMechanicSourceCreateResult SourceCreateResult { get; }
    }

    internal readonly struct CharacterTestAreaConfinementCancelledChannel
    {
        public CharacterTestAreaConfinementCancelledChannel(
            int casterEntityId,
            FixVector3 center,
            FixVector3 forward,
            int traceId,
            int localFrame,
            int sourceOrder,
            string reason)
        {
            CasterEntityId = casterEntityId;
            Center = center;
            Forward = forward;
            TraceId = traceId;
            LocalFrame = localFrame;
            SourceOrder = sourceOrder;
            Reason = reason ?? string.Empty;
        }

        public int CasterEntityId { get; }
        public FixVector3 Center { get; }
        public FixVector3 Forward { get; }
        public int TraceId { get; }
        public int LocalFrame { get; }
        public int SourceOrder { get; }
        public string Reason { get; }
    }

    public readonly struct CharacterTestAreaConfinementTickSummary
    {
        public CharacterTestAreaConfinementTickSummary(
            int activeZoneCount,
            int pendingChannelCount,
            int createdZoneCount,
            int expiredZoneCount,
            int cancelledChannelCount,
            int queryCount,
            int queryHitCount,
            int appliedOrRefreshedCount,
            int rejectedTargetCount,
            int skippedMissingGameplayTargetCount)
        {
            ActiveZoneCount = activeZoneCount;
            PendingChannelCount = pendingChannelCount;
            CreatedZoneCount = createdZoneCount;
            ExpiredZoneCount = expiredZoneCount;
            CancelledChannelCount = cancelledChannelCount;
            QueryCount = queryCount;
            QueryHitCount = queryHitCount;
            AppliedOrRefreshedCount = appliedOrRefreshedCount;
            RejectedTargetCount = rejectedTargetCount;
            SkippedMissingGameplayTargetCount = skippedMissingGameplayTargetCount;
        }

        public int ActiveZoneCount { get; }
        public int PendingChannelCount { get; }
        public int CreatedZoneCount { get; }
        public int ExpiredZoneCount { get; }
        public int CancelledChannelCount { get; }
        public int QueryCount { get; }
        public int QueryHitCount { get; }
        public int AppliedOrRefreshedCount { get; }
        public int RejectedTargetCount { get; }
        public int SkippedMissingGameplayTargetCount { get; }
    }

    internal sealed class CharacterTestAreaConfinementRuntime
    {
        private const CharacterTestAreaConfinementLandingMode LockedTargetLandingMode = CharacterTestAreaConfinementLandingMode.LockedTarget;
        private const CharacterTestAreaConfinementLandingMode ForwardLandingMode = CharacterTestAreaConfinementLandingMode.CasterForward;

        private readonly CharacterTestAreaConfinementTuning _tuning;
        private readonly CharacterTestMechanicSourceRuntime _sourceRuntime;
        private readonly List<PendingAreaConfinementChannel> _pendingChannels =
            new List<PendingAreaConfinementChannel>();
        private readonly List<CharacterTestAreaConfinementActivatedZone> _activatedZones =
            new List<CharacterTestAreaConfinementActivatedZone>();
        private readonly List<CharacterTestAreaConfinementCancelledChannel> _cancelledChannels =
            new List<CharacterTestAreaConfinementCancelledChannel>();
        private int _nextPendingChannelId = 1;
        private int _cancelledSinceLastTick;

        public CharacterTestAreaConfinementRuntime()
            : this(CharacterTestAreaConfinementTuningSource.LoadDefault())
        {
        }

        internal CharacterTestAreaConfinementRuntime(CharacterTestAreaConfinementTuning tuning)
        {
            _tuning = tuning ?? CharacterTestAreaConfinementTuning.Default;
            _sourceRuntime = new CharacterTestMechanicSourceRuntime(
                CreateMechanicSourceDefinitions(_tuning),
                CharacterTestGuardAuraRuntime.CreateBuffDefinitions());
        }

        public CharacterTestAreaConfinementTickSummary LastTickSummary { get; private set; }

        public int ActiveZoneCount => _sourceRuntime.ActiveSourceCount;
        public int PendingChannelCount => _pendingChannels.Count;

        public CharacterTestMechanicSourceDefinitionRegistry Definitions =>
            _sourceRuntime.Definitions;

        public CharacterTestAreaConfinementTuning Tuning => _tuning;

        private CharacterTestAreaConfinementRuntimeState _hashCharacterTestAreaConfinementRuntimeState;

        internal CharacterTestAreaConfinementRuntimeState ReadStateView()
        {
            if (_hashCharacterTestAreaConfinementRuntimeState == null)
            {
                var view = new CharacterTestRuntimeStateView<CharacterTestAreaConfinementPendingChannelSnapshot>(() => _pendingChannels.Count, i => _pendingChannels[i].ToSnapshot());
            _hashCharacterTestAreaConfinementRuntimeState = new CharacterTestAreaConfinementRuntimeState(
                CharacterTestAreaConfinementRuntimeState.CurrentSchemaVersion,
                CharacterTestAreaConfinementRuntimeState.DefaultModuleId,
                _nextPendingChannelId,
                view,
                _sourceRuntime.ReadStateView(
                    CharacterTestAreaConfinementRuntimeState.SourceRuntimeModuleId), borrow: true);
            }
            _hashCharacterTestAreaConfinementRuntimeState.NextPendingChannelId = _nextPendingChannelId;
            _hashCharacterTestAreaConfinementRuntimeState.SourceRuntime = _sourceRuntime.ReadStateView(CharacterTestAreaConfinementRuntimeState.SourceRuntimeModuleId);
            return _hashCharacterTestAreaConfinementRuntimeState;
        }

        internal CharacterTestAreaConfinementRuntimeState CreateStateSnapshot()
        {
            var pendingChannels = new CharacterTestAreaConfinementPendingChannelSnapshot[
                _pendingChannels.Count];
            for (int i = 0; i < _pendingChannels.Count; i++)
                pendingChannels[i] = _pendingChannels[i].ToSnapshot();

            return new CharacterTestAreaConfinementRuntimeState(
                CharacterTestAreaConfinementRuntimeState.CurrentSchemaVersion,
                CharacterTestAreaConfinementRuntimeState.DefaultModuleId,
                _nextPendingChannelId,
                pendingChannels,
                _sourceRuntime.CreateStateSnapshot(
                    CharacterTestAreaConfinementRuntimeState.SourceRuntimeModuleId));
        }

        internal bool TryApplyStateSnapshot(
            CharacterTestAreaConfinementRuntimeState state,
            out string failure)
        {
            failure = string.Empty;
            if (state != null)
            {
                if (state.SchemaVersion != CharacterTestAreaConfinementRuntimeState.CurrentSchemaVersion
                    || state.ModuleId != CharacterTestAreaConfinementRuntimeState.DefaultModuleId)
                {
                    failure = "Unsupported Area Confinement snapshot version.";
                    return false;
                }
                if (!_sourceRuntime.ValidateStateSnapshot(state.SourceRuntime, out failure)) return false;
                var pendingIds = new HashSet<int>();
                foreach (CharacterTestAreaConfinementPendingChannelSnapshot channel in state.PendingChannels)
                {
                    if (channel.PendingChannelId <= 0 || !pendingIds.Add(channel.PendingChannelId)
                        || channel.CasterEntityId <= 0 || channel.SourceGameplayEntityIndex <= 0
                        || channel.SourceGameplayEntityGeneration <= 0 || channel.TraceId < 0
                        || channel.LandingMode < CharacterTestAreaConfinementLandingMode.None
                        || channel.LandingMode > CharacterTestAreaConfinementLandingMode.CasterForward
                        || (channel.TraceId > 0 && !CharacterTestAuthoredContentIds.IsActive(401, channel.TraceId)))
                    {
                        failure = "Area Confinement snapshot contains an invalid or unknown integer identity.";
                        return false;
                    }
                }
            }
            _pendingChannels.Clear();
            _activatedZones.Clear();
            _cancelledChannels.Clear();
            _cancelledSinceLastTick = 0;

            if (state == null)
            {
                _nextPendingChannelId = 1;
                if (!_sourceRuntime.TryApplyStateSnapshot(null, out failure))
                    return false;
                LastTickSummary = ToAreaConfinementSummary(
                    _sourceRuntime.LastTickSummary,
                    _pendingChannels.Count,
                    0);
                return true;
            }

            _nextPendingChannelId = state.NextPendingChannelId;
            if (!_sourceRuntime.TryApplyStateSnapshot(state.SourceRuntime, out failure))
                return false;

            IReadOnlyList<CharacterTestAreaConfinementPendingChannelSnapshot> pendingChannels =
                state.PendingChannels;
            for (int i = 0; i < pendingChannels.Count; i++)
            {
                CharacterTestAreaConfinementPendingChannelSnapshot channel = pendingChannels[i];
                if (channel.CasterEntityId <= 0)
                    continue;
                if (channel.SourceGameplayEntityIndex <= 0
                    || channel.SourceGameplayEntityGeneration <= 0)
                {
                    failure = "Area Confinement pending channel gameplay entity id is invalid. pendingChannelId="
                        + channel.PendingChannelId;
                    _pendingChannels.Clear();
                    return false;
                }

                _pendingChannels.Add(PendingAreaConfinementChannel.FromSnapshot(channel));
            }

            LastTickSummary = ToAreaConfinementSummary(
                _sourceRuntime.LastTickSummary,
                _pendingChannels.Count,
                0);
            return true;
        }

        public CharacterTestAreaConfinementZoneCastResult EnqueueZone(
            CharacterTestCharacterRuntimeWorld world,
            CharacterTestCharacterPhysicsRuntime physicsRuntime,
            CharacterTestCharacterEntity caster,
            RuntimeFrame frame,
            int traceId,
            Func<int> lockedTargetEntityIdProvider)
        {
            CharacterTestAreaConfinementChannelStartResult result = BeginChannel(
                world,
                physicsRuntime,
                caster,
                frame,
                traceId,
                CharacterTestAreaConfinementTuning.TelegraphStartLocalFrame,
                sourceOrder: 0,
                lockedTargetEntityIdProvider);
            if (!result.Success)
                return CharacterTestAreaConfinementZoneCastResult.Failed(result.FailureReason);

            return CharacterTestAreaConfinementZoneCastResult.Succeeded(
                result.Center,
                result.LandingMode,
                CharacterTestMechanicSourceCreateResult.Failed("Area Confinement is pending telegraph activation."));
        }

        public CharacterTestAreaConfinementChannelStartResult BeginChannel(
            CharacterTestCharacterRuntimeWorld world,
            CharacterTestCharacterPhysicsRuntime physicsRuntime,
            CharacterTestCharacterEntity caster,
            RuntimeFrame frame,
            int traceId,
            int localFrame,
            int sourceOrder,
            Func<int> lockedTargetEntityIdProvider)
        {
            if (world == null)
                return CharacterTestAreaConfinementChannelStartResult.Failed("CharacterTest world is missing.");
            if (physicsRuntime?.World == null)
                return CharacterTestAreaConfinementChannelStartResult.Failed("Combat physics world is missing.");
            if (caster == null || !caster.GameplayEntityId.IsValid)
                return CharacterTestAreaConfinementChannelStartResult.Failed("Caster is missing.");

            ResolveZoneCenter(
                world,
                physicsRuntime,
                caster,
                lockedTargetEntityIdProvider,
                out FixVector3 center,
                out CharacterTestAreaConfinementLandingMode landingMode);
            FixVector3 forward = CharacterTestFireballProjectileAdapter.ResolveDirection(caster);
            long activationFrame = checked(frame.Value + _tuning.TelegraphFrames);
            int pendingChannelId = _nextPendingChannelId++;
            if (_nextPendingChannelId <= 0)
                _nextPendingChannelId = 1;

            _pendingChannels.Add(new PendingAreaConfinementChannel(
                pendingChannelId,
                caster.Id.Value,
                caster.GameplayEntityId,
                physicsRuntime.GetEntityId(caster),
                center,
                forward,
                landingMode,
                frame.Value,
                activationFrame,
                localFrame,
                sourceOrder,
                traceId));
            LastTickSummary = ToAreaConfinementSummary(
                _sourceRuntime.LastTickSummary,
                _pendingChannels.Count,
                _cancelledSinceLastTick);
            return CharacterTestAreaConfinementChannelStartResult.Succeeded(
                pendingChannelId,
                center,
                forward,
                landingMode,
                frame.Value,
                activationFrame,
                localFrame,
                sourceOrder,
                traceId);
        }

        /// <remarks>NoAlloc on the steady-state path after registration/initialization; invalid-call and exception paths may allocate diagnostics.</remarks>
        public void Tick(
            CharacterTestCharacterRuntimeWorld world,
            CharacterTestCharacterPhysicsRuntime physicsRuntime,
            RuntimeFrame frame)
        {
            _activatedZones.Clear();
            _cancelledChannels.Clear();
            TickPendingChannels(world, frame);
            _sourceRuntime.Tick(world, physicsRuntime, frame);
            LastTickSummary = ToAreaConfinementSummary(
                _sourceRuntime.LastTickSummary,
                _pendingChannels.Count,
                _cancelledSinceLastTick);
            _cancelledSinceLastTick = 0;
        }

        public int DrainLastActivatedZones(List<CharacterTestAreaConfinementActivatedZone> output)
        {
            if (output == null)
                return 0;

            for (int i = 0; i < _activatedZones.Count; i++)
                output.Add(_activatedZones[i]);
            int count = _activatedZones.Count;
            _activatedZones.Clear();
            return count;
        }

        public int DrainLastCancelledChannels(List<CharacterTestAreaConfinementCancelledChannel> output)
        {
            if (output == null)
                return 0;

            for (int i = 0; i < _cancelledChannels.Count; i++)
                output.Add(_cancelledChannels[i]);
            int count = _cancelledChannels.Count;
            _cancelledChannels.Clear();
            return count;
        }

        public int CancelPendingForDamageEvents(
            IReadOnlyList<MxFramework.Combat.Damage.DamageResolvedEvent> damageEvents,
            RuntimeFrame frame)
        {
            if (damageEvents == null || damageEvents.Count == 0 || _pendingChannels.Count == 0)
                return 0;

            int cancelled = 0;
            for (int eventIndex = 0; eventIndex < damageEvents.Count; eventIndex++)
            {
                MxFramework.Combat.Damage.DamageResolvedEvent evt = damageEvents[eventIndex];
                if (evt.FinalDamage <= 0)
                    continue;

                int targetEntityId = CharacterTestCombatEntityLookup.ResolveCharacterEntityId(
                    evt.HitResult.TargetId);
                if (targetEntityId <= 0)
                    continue;

                cancelled += CancelPendingForCaster(targetEntityId, frame, "Damage");
            }

            if (cancelled > 0)
            {
                LastTickSummary = ToAreaConfinementSummary(
                    _sourceRuntime.LastTickSummary,
                    _pendingChannels.Count,
                    _cancelledSinceLastTick);
            }

            return cancelled;
        }

        internal static CharacterTestMechanicSourceDefinitionRegistry CreateMechanicSourceDefinitions()
        {
            return CreateMechanicSourceDefinitions(CharacterTestAreaConfinementTuningSource.LoadDefault());
        }

        internal static CharacterTestMechanicSourceDefinitionRegistry CreateMechanicSourceDefinitions(
            CharacterTestAreaConfinementTuning tuning)
        {
            CharacterTestAreaConfinementTuning safeTuning =
                tuning ?? CharacterTestAreaConfinementTuning.Default;
            return new CharacterTestMechanicSourceDefinitionRegistry(
                new CharacterTestMechanicSourceDefinition(
                    CharacterTestAreaConfinementIds.MechanicSourceId,
                    CharacterTestAreaConfinementIds.SourceStableId,
                    CharacterTestMechanicSourceShape.Sphere,
                    safeTuning.Radius,
                    CombatPhysicsLayerMask.FromLayer(
                        CharacterTestCharacterPhysicsRuntime.DynamicCharacterLayer),
                    GameplayTargetRelationFilter.Enemy,
                    safeTuning.ZoneDurationFrames,
                    CharacterTestCharacterActionConfigSource.AreaConfinementActionId,
                    CharacterTestCharacterActionConfigSource.AreaConfinementStableEventId,
                    CharacterTestCharacterActionConfigSource.AreaConfinementStableEventLabel,
                    includeSourceEntity: false,
                    tickIntervalFrames: 1,
                    CharacterTestMechanicEffectDefinition.ApplyBuffOnTick(
                        1,
                        CharacterTestAreaConfinementIds.RootedBuffId),
                    CharacterTestMechanicEffectDefinition.SpawnVfxOnCreate(
                        2,
                        safeTuning.ZoneVfxDefinitionId,
                        safeTuning.ZoneDurationFrames,
                        CharacterTestCharacterActionConfigSource.AreaConfinementZoneVfxStableEventId,
                        CharacterTestCharacterActionConfigSource.AreaConfinementZoneVfxStableEventLabel)));
        }

        private static CharacterTestAreaConfinementTickSummary ToAreaConfinementSummary(
            CharacterTestMechanicSourceTickSummary summary,
            int pendingChannelCount,
            int cancelledChannelCount)
        {
            return new CharacterTestAreaConfinementTickSummary(
                summary.ActiveSourceCount,
                pendingChannelCount,
                summary.CreatedSourceCount,
                summary.ExpiredSourceCount,
                cancelledChannelCount,
                summary.QueryCount,
                summary.QueryHitCount,
                summary.AppliedOrRefreshedCount,
                summary.RejectedTargetCount,
                summary.SkippedMissingGameplayTargetCount);
        }

        private void TickPendingChannels(
            CharacterTestCharacterRuntimeWorld world,
            RuntimeFrame frame)
        {
            for (int i = _pendingChannels.Count - 1; i >= 0; i--)
            {
                PendingAreaConfinementChannel channel = _pendingChannels[i];
                CharacterTestCharacterEntity caster = FindEntity(world, channel.CasterEntityId);
                if (caster == null || !caster.BattleState.CountsAsAlive)
                {
                    CancelAt(i, channel, frame, "CasterMissingOrDead");
                    continue;
                }

                if (_tuning.InterruptPolicy == CharacterTestAreaConfinementInterruptPolicy.CancelOnDamageOrActionExit
                    && !IsAreaConfinementActionRunning(caster))
                {
                    CancelAt(i, channel, frame, "ActionExit");
                    continue;
                }

                if (frame.Value < channel.ActivationFrame)
                    continue;

                CharacterTestMechanicSourceCreateResult createResult = _sourceRuntime.CreateSource(
                    CharacterTestAreaConfinementIds.MechanicSourceId,
                    channel.SourceGameplayEntityId,
                    channel.SourceCombatEntityId,
                    channel.Center,
                    frame,
                    channel.TraceId);
                _pendingChannels.RemoveAt(i);
                if (!createResult.Success)
                {
                    AddCancel(channel, frame, "ActivationFailed");
                    continue;
                }

                _activatedZones.Add(new CharacterTestAreaConfinementActivatedZone(
                    channel.CasterEntityId,
                    channel.Center,
                    channel.Forward,
                    channel.LandingMode,
                    channel.TraceId,
                    _tuning.ActivationLocalFrame,
                    channel.SourceOrder,
                    createResult));
            }
        }

        private int CancelPendingForCaster(
            int casterEntityId,
            RuntimeFrame frame,
            string reason)
        {
            int cancelled = 0;
            for (int i = _pendingChannels.Count - 1; i >= 0; i--)
            {
                PendingAreaConfinementChannel channel = _pendingChannels[i];
                if (channel.CasterEntityId != casterEntityId)
                    continue;

                CancelAt(i, channel, frame, reason);
                cancelled++;
            }

            return cancelled;
        }

        private void CancelAt(
            int index,
            PendingAreaConfinementChannel channel,
            RuntimeFrame frame,
            string reason)
        {
            _pendingChannels.RemoveAt(index);
            AddCancel(channel, frame, reason);
        }

        private void AddCancel(
            PendingAreaConfinementChannel channel,
            RuntimeFrame frame,
            string reason)
        {
            _cancelledSinceLastTick++;
            _cancelledChannels.Add(new CharacterTestAreaConfinementCancelledChannel(
                channel.CasterEntityId,
                channel.Center,
                channel.Forward,
                channel.TraceId,
                channel.LocalFrame,
                channel.SourceOrder,
                reason));
        }

        private static bool IsAreaConfinementActionRunning(CharacterTestCharacterEntity caster)
        {
            return caster != null
                && caster.Action.ActiveActionId == CharacterTestCharacterActionConfigSource.AreaConfinementActionId
                && (caster.Action.State == CharacterTestActionStateId.Running);
        }

        private static void ResolveZoneCenter(
            CharacterTestCharacterRuntimeWorld world,
            CharacterTestCharacterPhysicsRuntime physicsRuntime,
            CharacterTestCharacterEntity caster,
            Func<int> lockedTargetEntityIdProvider,
            out FixVector3 center,
            out CharacterTestAreaConfinementLandingMode landingMode)
        {
            if (TryResolveLockedHostileTargetCenter(
                    world,
                    physicsRuntime,
                    caster,
                    lockedTargetEntityIdProvider,
                    out center))
            {
                landingMode = LockedTargetLandingMode;
                return;
            }

            FixVector3 casterCenter = ResolveEntityCenter(physicsRuntime, caster);
            FixVector3 direction = CharacterTestFireballProjectileAdapter.ResolveDirection(caster);
            center = casterCenter + direction * Fix64.FromInt(CharacterTestAreaConfinementIds.FallbackForwardRangeMeters);
            landingMode = ForwardLandingMode;
        }

        private static bool TryResolveLockedHostileTargetCenter(
            CharacterTestCharacterRuntimeWorld world,
            CharacterTestCharacterPhysicsRuntime physicsRuntime,
            CharacterTestCharacterEntity caster,
            Func<int> lockedTargetEntityIdProvider,
            out FixVector3 center)
        {
            center = FixVector3.Zero;
            if (world == null || caster == null || lockedTargetEntityIdProvider == null)
                return false;

            int lockedTargetEntityId = lockedTargetEntityIdProvider();
            if (lockedTargetEntityId <= 0 || lockedTargetEntityId == caster.Id.Value)
                return false;

            CharacterTestCharacterEntity target = FindEntity(world, lockedTargetEntityId);
            if (target == null || target.CombatState.Health <= 0)
                return false;

            if (target.Team.TeamId == caster.Team.TeamId)
                return false;

            FixVector3 casterCenter = ResolveEntityCenter(physicsRuntime, caster);
            FixVector3 targetCenter = ResolveEntityCenter(physicsRuntime, target);
            if (HorizontalDistanceSquared(casterCenter, targetCenter)
                > Fix64.FromInt(CharacterTestAreaConfinementIds.FallbackForwardRangeMeters)
                * Fix64.FromInt(CharacterTestAreaConfinementIds.FallbackForwardRangeMeters))
            {
                return false;
            }

            center = targetCenter;
            return true;
        }

        private static FixVector3 ResolveEntityCenter(
            CharacterTestCharacterPhysicsRuntime physicsRuntime,
            CharacterTestCharacterEntity entity)
        {
            if (physicsRuntime?.World != null
                && physicsRuntime.World.TryGetBody(physicsRuntime.GetBodyId(entity), out CombatPhysicsBody body))
            {
                return body.Position;
            }

            return new FixVector3(
                CharacterTestDeterministicUnits.MillimetersToFix64Meters(entity.Transform.XMillimeters),
                CharacterTestDeterministicUnits.MillimetersToFix64Meters(entity.Transform.YMillimeters)
                    + (physicsRuntime?.GetCharacterCenterOffsetY(entity) ?? Fix64.One),
                CharacterTestDeterministicUnits.MillimetersToFix64Meters(entity.Transform.ZMillimeters));
        }

        private static Fix64 HorizontalDistanceSquared(FixVector3 a, FixVector3 b)
        {
            Fix64 x = a.X - b.X;
            Fix64 z = a.Z - b.Z;
            return x * x + z * z;
        }

        private static CharacterTestCharacterEntity FindEntity(
            CharacterTestCharacterRuntimeWorld world,
            int entityId)
        {
            IReadOnlyList<CharacterTestCharacterEntity> entities = world.Entities;
            for (int i = 0; i < entities.Count; i++)
            {
                if (entities[i].Id.Value == entityId)
                    return entities[i];
            }

            return null;
        }

        private readonly struct PendingAreaConfinementChannel
        {
            public PendingAreaConfinementChannel(
                int pendingChannelId,
                int casterEntityId,
                GameplayEntityId sourceGameplayEntityId,
                CombatEntityId sourceCombatEntityId,
                FixVector3 center,
                FixVector3 forward,
                CharacterTestAreaConfinementLandingMode landingMode,
                long startFrame,
                long activationFrame,
                int localFrame,
                int sourceOrder,
                int traceId)
            {
                PendingChannelId = pendingChannelId;
                CasterEntityId = casterEntityId;
                SourceGameplayEntityId = sourceGameplayEntityId;
                SourceCombatEntityId = sourceCombatEntityId;
                Center = center;
                Forward = forward;
                LandingMode = landingMode;
                StartFrame = startFrame;
                ActivationFrame = activationFrame;
                LocalFrame = localFrame;
                SourceOrder = sourceOrder;
                TraceId = traceId;
            }

            public int PendingChannelId { get; }
            public int CasterEntityId { get; }
            public GameplayEntityId SourceGameplayEntityId { get; }
            public CombatEntityId SourceCombatEntityId { get; }
            public FixVector3 Center { get; }
            public FixVector3 Forward { get; }
            public CharacterTestAreaConfinementLandingMode LandingMode { get; }
            public long StartFrame { get; }
            public long ActivationFrame { get; }
            public int LocalFrame { get; }
            public int SourceOrder { get; }
            public int TraceId { get; }

            public CharacterTestAreaConfinementPendingChannelSnapshot ToSnapshot()
            {
                return CharacterTestAreaConfinementPendingChannelSnapshot.FromVectors(
                    PendingChannelId,
                    CasterEntityId,
                    SourceGameplayEntityId.Index,
                    SourceGameplayEntityId.Generation,
                    SourceCombatEntityId.Value,
                    Center,
                    Forward,
                    LandingMode,
                    StartFrame,
                    ActivationFrame,
                    LocalFrame,
                    SourceOrder,
                    TraceId);
            }

            public static PendingAreaConfinementChannel FromSnapshot(
                CharacterTestAreaConfinementPendingChannelSnapshot snapshot)
            {
                return new PendingAreaConfinementChannel(
                    snapshot.PendingChannelId,
                    snapshot.CasterEntityId,
                    new GameplayEntityId(
                        snapshot.SourceGameplayEntityIndex,
                        snapshot.SourceGameplayEntityGeneration),
                    new CombatEntityId(snapshot.SourceCombatEntityId),
                    snapshot.Center,
                    snapshot.Forward,
                    snapshot.LandingMode,
                    snapshot.StartFrame,
                    snapshot.ActivationFrame,
                    snapshot.LocalFrame,
                    snapshot.SourceOrder,
                    snapshot.TraceId);
            }
        }
    }

    internal readonly struct CharacterTestPoisonBottlePoolCreated
    {
        public CharacterTestPoisonBottlePoolCreated(
            int casterEntityId,
            FixVector3 center,
            FixVector3 forward,
            int traceId,
            int localFrame,
            int sourceOrder,
            CharacterTestMechanicSourceCreateResult sourceCreateResult)
        {
            CasterEntityId = casterEntityId;
            Center = center;
            Forward = forward;
            TraceId = traceId;
            LocalFrame = localFrame;
            SourceOrder = sourceOrder;
            SourceCreateResult = sourceCreateResult;
        }

        public int CasterEntityId { get; }
        public FixVector3 Center { get; }
        public FixVector3 Forward { get; }
        public int TraceId { get; }
        public int LocalFrame { get; }
        public int SourceOrder { get; }
        public CharacterTestMechanicSourceCreateResult SourceCreateResult { get; }
    }

    public readonly struct CharacterTestPoisonBottlePoolTickSummary
    {
        public CharacterTestPoisonBottlePoolTickSummary(
            int activePoolCount,
            int createdPoolCount,
            int expiredPoolCount,
            int queryCount,
            int queryHitCount,
            int appliedOrRefreshedCount,
            int rejectedTargetCount,
            int skippedMissingGameplayTargetCount)
        {
            ActivePoolCount = activePoolCount;
            CreatedPoolCount = createdPoolCount;
            ExpiredPoolCount = expiredPoolCount;
            QueryCount = queryCount;
            QueryHitCount = queryHitCount;
            AppliedOrRefreshedCount = appliedOrRefreshedCount;
            RejectedTargetCount = rejectedTargetCount;
            SkippedMissingGameplayTargetCount = skippedMissingGameplayTargetCount;
        }

        public int ActivePoolCount { get; }
        public int CreatedPoolCount { get; }
        public int ExpiredPoolCount { get; }
        public int QueryCount { get; }
        public int QueryHitCount { get; }
        public int AppliedOrRefreshedCount { get; }
        public int RejectedTargetCount { get; }
        public int SkippedMissingGameplayTargetCount { get; }
    }

    internal sealed class CharacterTestPoisonBottlePoolRuntime
    {
        internal const int SourceRuntimeModuleId = 1102;

        private readonly CharacterTestMechanicSourceRuntime _sourceRuntime =
            new CharacterTestMechanicSourceRuntime(
                CreateMechanicSourceDefinitions(),
                CharacterTestGuardAuraRuntime.CreateBuffDefinitions(), accessoryProducerKind: 6);
        private readonly List<CharacterTestPoisonBottlePoolCreated> _createdPools =
            new List<CharacterTestPoisonBottlePoolCreated>();

        public CharacterTestPoisonBottlePoolTickSummary LastTickSummary { get; private set; }
        public int ActivePoolCount => _sourceRuntime.ActiveSourceCount;
        public CharacterTestMechanicSourceDefinitionRegistry Definitions => _sourceRuntime.Definitions;

        internal CharacterTestMechanicSourceRuntimeState ReadStateView() => _sourceRuntime.ReadStateView(SourceRuntimeModuleId);

        internal CharacterTestMechanicSourceRuntimeState CreateStateSnapshot()
        {
            return _sourceRuntime.CreateStateSnapshot(SourceRuntimeModuleId);
        }

        internal bool TryApplyStateSnapshot(
            CharacterTestMechanicSourceRuntimeState state,
            out string failure)
        {
            _createdPools.Clear();
            if (!_sourceRuntime.TryApplyStateSnapshot(state, out failure))
                return false;

            LastTickSummary = ToPoisonBottlePoolSummary(_sourceRuntime.LastTickSummary);
            return true;
        }

        public CharacterTestMechanicSourceCreateResult CreatePool(
            CharacterTestCharacterRuntimeWorld world,
            CharacterTestCharacterPhysicsRuntime physicsRuntime,
            CharacterTestCharacterEntity caster,
            FixVector3 center,
            FixVector3 forward,
            RuntimeFrame frame,
            int traceId,
            int localFrame,
            int sourceOrder)
        {
            if (world == null)
                return CharacterTestMechanicSourceCreateResult.Failed("CharacterTest world is missing.");
            if (physicsRuntime?.World == null)
                return CharacterTestMechanicSourceCreateResult.Failed("Combat physics world is missing.");
            if (caster == null || !caster.GameplayEntityId.IsValid)
                return CharacterTestMechanicSourceCreateResult.Failed("Caster is missing.");

            CharacterTestMechanicSourceCreateResult result = _sourceRuntime.CreateSource(
                CharacterTestPoisonIds.PoisonBottlePoolSourceId,
                caster.GameplayEntityId,
                physicsRuntime.GetEntityId(caster),
                center,
                frame,
                traceId);
            if (result.Success)
            {
                _createdPools.Add(new CharacterTestPoisonBottlePoolCreated(
                    caster.Id.Value,
                    center,
                    forward,
                    traceId,
                    localFrame,
                    sourceOrder,
                    result));
            }

            LastTickSummary = ToPoisonBottlePoolSummary(_sourceRuntime.LastTickSummary);
            return result;
        }

        /// <remarks>NoAlloc on the steady-state path after registration/initialization; invalid-call and exception paths may allocate diagnostics.</remarks>
        public void Tick(
            CharacterTestCharacterRuntimeWorld world,
            CharacterTestCharacterPhysicsRuntime physicsRuntime,
            RuntimeFrame frame)
        {
            _sourceRuntime.Tick(world, physicsRuntime, frame);
            LastTickSummary = ToPoisonBottlePoolSummary(_sourceRuntime.LastTickSummary);
        }

        public int DrainLastCreatedPools(List<CharacterTestPoisonBottlePoolCreated> output)
        {
            if (output == null)
                return 0;

            for (int i = 0; i < _createdPools.Count; i++)
                output.Add(_createdPools[i]);
            int count = _createdPools.Count;
            _createdPools.Clear();
            return count;
        }

        internal static CharacterTestMechanicSourceDefinitionRegistry CreateMechanicSourceDefinitions()
        {
            return new CharacterTestMechanicSourceDefinitionRegistry(
                new CharacterTestMechanicSourceDefinition(
                    CharacterTestPoisonIds.PoisonBottlePoolSourceId,
                    CharacterTestPoisonIds.PoisonBottlePoolSourceStableId,
                    CharacterTestMechanicSourceShape.Sphere,
                    Fix64.FromRatio(CharacterTestPoisonIds.PoisonBottlePoolRadiusCentimeters, 100),
                    CombatPhysicsLayerMask.FromLayer(
                        CharacterTestCharacterPhysicsRuntime.DynamicCharacterLayer),
                    GameplayTargetRelationFilter.Enemy,
                    CharacterTestPoisonIds.PoisonBottlePoolDurationFrames,
                    CharacterTestCharacterActionConfigSource.PoisonBottleActionId,
                    CharacterTestCharacterActionConfigSource.PoisonBottlePoolVfxStableEventId,
                    CharacterTestCharacterActionConfigSource.PoisonBottlePoolVfxStableEventLabel,
                    includeSourceEntity: false,
                    CharacterTestPoisonIds.PoisonBottlePoolTickIntervalFrames,
                    CharacterTestMechanicEffectDefinition.ApplyBuffOnTick(
                        1,
                        CharacterTestPoisonIds.PoisonedBuffId),
                    CharacterTestMechanicEffectDefinition.SpawnVfxOnCreate(
                        2,
                        CharacterTestVfxIds.PoisonBottlePoolDefinitionId,
                        CharacterTestPoisonIds.PoisonBottlePoolDurationFrames,
                        CharacterTestCharacterActionConfigSource.PoisonBottlePoolVfxStableEventId,
                        CharacterTestCharacterActionConfigSource.PoisonBottlePoolVfxStableEventLabel)));
        }

        private static CharacterTestPoisonBottlePoolTickSummary ToPoisonBottlePoolSummary(
            CharacterTestMechanicSourceTickSummary summary)
        {
            return new CharacterTestPoisonBottlePoolTickSummary(
                summary.ActiveSourceCount,
                summary.CreatedSourceCount,
                summary.ExpiredSourceCount,
                summary.QueryCount,
                summary.QueryHitCount,
                summary.AppliedOrRefreshedCount,
                summary.RejectedTargetCount,
                summary.SkippedMissingGameplayTargetCount);
        }
    }
}
