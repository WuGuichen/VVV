using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using MxFramework.Animation;
using MxFramework.Audio;
using MxFramework.CharacterAction;
using MxFramework.CharacterRuntimeSpawn.Unity;
using MxFramework.Combat.GameplayBridge;
using MxFramework.Combat.Physics;
using MxFramework.Combat.Shields;
using MxFramework.Combat.Projectiles;
using MxFramework.Gameplay;
using MxFramework.Runtime;
using MxFramework.Vfx;
using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    internal static partial class CharacterTestRuntimeAiLabFrameSyncProbe
    {
        private const float OverlayEpsilon = 0.001f;

        public static CharacterTestRuntimeAiLabFrameSyncSample Capture(
            GameSlice slice,
            CharacterTestNetworkSnapshotParticipant snapshotParticipant,
            CharacterTestRuntimeAiPlannerDevFrameSnapshot selectedFrame,
            CharacterTestRuntimeAiPlannerDevPreviewItem preview,
            bool includeRuntimeEvents = false)
        {
            if (slice == null)
            {
                return new CharacterTestRuntimeAiLabFrameSyncSample(
                    runtimeFrame: 0,
                    stateHash: 0L,
                    selectedFrame: selectedFrame != null ? selectedFrame.Frame : 0,
                    previewFrame: preview.Frame,
                    characters: Array.Empty<CharacterTestRuntimeAiLabFrameSyncCharacterSample>(),
                    projectileRuntimeAvailable: false,
                    projectileSchemaVersion: 0,
                    projectileDefinitionHash: 0L,
                    projectileNextProjectileId: 0,
                    projectiles: Array.Empty<CharacterTestRuntimeAiLabFrameSyncProjectileSample>(),
                    abilityRuntimeAvailable: false,
                    abilitySchemaVersion: 0,
                    abilityModuleId: string.Empty,
                    abilityDefinitionHash: 0L,
                    abilityCooldowns: Array.Empty<CharacterTestRuntimeAiLabFrameSyncAbilityCooldownSample>(),
                    guardAura: default,
                    overlay: CharacterTestRuntimeAiLabFrameSyncOverlaySample.FromPreview(preview));
            }

            int runtimeFrame = slice.CurrentFrame.Value > int.MaxValue
                ? int.MaxValue
                : (int)slice.CurrentFrame.Value;
            long stateHash = snapshotParticipant != null
                ? snapshotParticipant.ComputeStateHash(slice.CurrentFrame)
                : 0L;
            IReadOnlyList<CharacterTestCharacterEntity> entities = slice.CharacterEntities;
            var samples = new CharacterTestRuntimeAiLabFrameSyncCharacterSample[
                entities != null ? entities.Count : 0];
            CharacterTestCharacterActionRuntime actionRuntime = slice.Services?.Modules?.ActionRuntime;
            GameplayComponentWorld gameplayComponentWorld = slice.CharacterRuntime?.World?.GameplayComponentWorld;
            CharacterTestCharacterUnityViewBridge viewBridge = slice.CharacterRuntime?.ViewBridge;
            CharacterTestCharacterPhysicsRuntime physicsRuntime = slice.CharacterRuntime?.PhysicsRuntime;
            for (int i = 0; i < samples.Length; i++)
            {
                samples[i] = CharacterTestRuntimeAiLabFrameSyncCharacterSample.Capture(
                    entities[i],
                    actionRuntime,
                    gameplayComponentWorld,
                    viewBridge,
                    physicsRuntime);
            }
            CombatProjectileRuntimeState projectileState =
                slice.CombatTraceSystem?.CreateProjectileStateSnapshot();
            AbilityRuntimeState abilityState = slice.Services?.Modules?.AbilityRuntime?.CreateStateSnapshot();
            CharacterTestMechanicSourceRuntimeState poisonBottlePoolState =
                slice.CombatTraceSystem?.CreatePoisonBottlePoolStateSnapshot();
            CharacterTestPhysicalShieldRuntimeState physicalShieldState =
                slice.CombatTraceSystem?.CreatePhysicalShieldStateSnapshot();
            CharacterTestHookPullRuntimeState hookPullState =
                slice.CombatTraceSystem?.CreateHookPullStateSnapshot();
            CharacterTestArcMineRuntimeState arcMineState =
                slice.CombatTraceSystem?.CreateArcMineStateSnapshot();
            CharacterTestRuneTurretRuntimeState runeTurretState =
                slice.CombatTraceSystem?.CreateRuneTurretStateSnapshot();
            CharacterTestAreaConfinementRuntimeState areaConfinementState =
                slice.CombatTraceSystem?.CreateAreaConfinementStateSnapshot();

            return new CharacterTestRuntimeAiLabFrameSyncSample(
                runtimeFrame,
                stateHash,
                selectedFrame != null ? selectedFrame.Frame : 0,
                preview.Frame,
                samples,
                projectileState != null,
                projectileState != null ? projectileState.SchemaVersion : 0,
                projectileState != null ? projectileState.DefinitionHash : 0L,
                projectileState != null ? projectileState.NextProjectileId : 0,
                CharacterTestRuntimeAiLabFrameSyncProjectileSample.Capture(projectileState),
                abilityState != null,
                abilityState != null ? abilityState.SchemaVersion : 0,
                abilityState != null ? abilityState.ModuleId : string.Empty,
                abilityState != null ? abilityState.DefinitionHash : 0L,
                CharacterTestRuntimeAiLabFrameSyncAbilityCooldownSample.Capture(abilityState),
                CharacterTestRuntimeAiLabFrameSyncOverlaySample.FromPreview(preview),
                CharacterTestRuntimeAiLabFrameSyncGuardAuraSample.Capture(
                    slice.CombatTraceSystem != null
                        ? slice.CombatTraceSystem.GuardAuraLastTickSummary
                        : default,
                    slice.CombatTraceSystem != null
                        ? slice.CombatTraceSystem.OnHitBuffLastSummary
                        : default),
                CharacterTestRuntimeAiLabFrameSyncPoisonBottlePoolSample.Capture(
                    poisonBottlePoolState,
                    slice.CombatTraceSystem != null
                        ? slice.CombatTraceSystem.PoisonBottlePoolLastTickSummary
                        : default,
                    slice.CombatTraceSystem != null
                        ? slice.CombatTraceSystem.LastPoisonTickTargetCount
                        : 0),
                CharacterTestRuntimeAiLabFrameSyncPhysicalShieldSample.Capture(
                    physicalShieldState,
                    slice.CombatTraceSystem != null
                        ? slice.CombatTraceSystem.PhysicalShieldLastTickSummary
                        : default),
                CharacterTestRuntimeAiLabFrameSyncHookPullSample.Capture(hookPullState),
                CharacterTestRuntimeAiLabFrameSyncArcMineSample.Capture(arcMineState),
                CharacterTestRuntimeAiLabFrameSyncRuneTurretSample.Capture(runeTurretState),
                CharacterTestRuntimeAiLabFrameSyncAreaConfinementSample.Capture(areaConfinementState),
                CharacterTestRuntimeAiLabFrameSyncTornadoSample.Capture(
                    slice.CombatTraceSystem != null
                        ? slice.CombatTraceSystem.LastTornadoDamageTickTargetCount
                        : 0,
                    slice.CombatTraceSystem?.InteractionHistory),
                CharacterTestRuntimeAiLabFrameSyncChainLightningSample.Capture(
                    slice.CombatTraceSystem != null
                        ? slice.CombatTraceSystem.LastChainLightningTargetCount
                        : 0,
                    slice.CombatTraceSystem?.LastFrameChainLightningVfxEvents),
                CharacterTestRuntimeAiLabFrameSyncLaserPathSample.Capture(
                    slice.CombatTraceSystem?.LastFrameLaserPathVfxEvents),
                CharacterTestRuntimeAiLabFrameSyncInteractionSample.Capture(
                    slice.CombatTraceSystem?.InteractionHistory,
                    slice.Services != null
                        ? slice.Services.InteractionVfxSummary
                        : CharacterTestInteractionVfxSummary.Empty),
                CharacterTestRuntimeAiLabFrameSyncMovableNaturalObjectSample.Capture(
                    slice.MovableNaturalObjects),
                CharacterTestRuntimeAiLabFrameSyncCommandStateSample.Capture(
                    slice.RuntimeCommands),
                CharacterTestRuntimeAiLabFrameSyncVfxRuntimeSample.Capture(
                    slice.VfxRuntime),
                CharacterTestRuntimeAiLabFrameSyncAudioSample.Capture(
                    slice.AudioService,
                    slice.Services != null
                        ? slice.Services.AudioIntentDrainSummary
                        : CharacterTestAudioIntentDrainSummary.Empty,
                    slice.Services != null
                        ? slice.Services.InteractionAudioSummary
                        : CharacterTestInteractionAudioSummary.Empty,
                    slice.Services != null
                        ? slice.Services.PhysicalAudioSummary
                        : CharacterTestPhysicalAudioSummary.Empty,
                    slice.CharacterRuntime != null
                        ? slice.CharacterRuntime.WeaponHitAudioSummary
                        : CharacterTestWeaponHitAudioSummary.Empty),
                planner: CharacterTestRuntimeAiLabFrameSyncPlannerSample.Capture(
                    selectedFrame != null ? selectedFrame.RuntimeAiPlannerSnapshot : null,
                    selectedFrame != null ? selectedFrame.Frame : 0),
                runtimeEvents: selectedFrame != null && selectedFrame.RuntimeEvents.Available
                    ? selectedFrame.RuntimeEvents
                    : includeRuntimeEvents
                        ? CharacterTestRuntimeAiLabFrameSyncRuntimeEventSample.Capture(
                            slice.RuntimeEventQueueSnapshot,
                            slice.RecentRuntimeEvents)
                        : default);
        }

        public static CharacterTestRuntimeAiLabFrameSyncSample Capture(
            RuntimeFrame frame,
            long stateHash,
            IReadOnlyList<CharacterTestCharacterEntity> entities,
            CharacterTestCharacterActionRuntime actionRuntime,
            CombatProjectileRuntimeState projectileState = null,
            AbilityRuntimeState abilityState = null,
            GameplayComponentWorld gameplayComponentWorld = null,
            CharacterTestMechanicSourceRuntimeState poisonBottlePoolState = null,
            CharacterTestPoisonBottlePoolTickSummary poisonBottlePoolLastTickSummary = default,
            int lastPoisonTickTargetCount = 0,
            CharacterTestPhysicalShieldRuntimeState physicalShieldState = null,
            CharacterTestPhysicalShieldTickSummary physicalShieldLastTickSummary = default,
            CharacterTestHookPullRuntimeState hookPullState = null,
            CharacterTestArcMineRuntimeState arcMineState = null,
            CharacterTestRuneTurretRuntimeState runeTurretState = null,
            CharacterTestAreaConfinementRuntimeState areaConfinementState = null,
            int tornadoDamageTickTargetCount = 0,
            CharacterTestInteractionHistory tornadoInteractionHistory = null,
            int chainLightningTargetCount = 0,
            IReadOnlyList<CharacterTestChainLightningVfxEvent> chainLightningVfxEvents = null,
            IReadOnlyList<CharacterTestLaserPathVfxEvent> laserPathVfxEvents = null,
            CharacterTestInteractionHistory interactionHistory = null,
            CharacterTestInteractionVfxSummary interactionVfxSummary = default,
            CombatGameplayAuraBuffTickSummary guardAuraLastTickSummary = default,
            CombatOnHitBuffApplicationSummary onHitBuffLastSummary = default,
            IReadOnlyList<CharacterTestMovableNaturalObjectState> movableNaturalObjects = null,
            CharacterTestRuntimeAiLabFrameSyncCommandStateSample commandState = default,
            CharacterTestRuntimeAiLabFrameSyncVfxRuntimeSample vfxRuntime = default,
            CharacterTestRuntimeAiLabFrameSyncAudioSample audio = default,
            CharacterTestRuntimeAiLabFrameSyncPlannerSample planner = default,
            CharacterTestRuntimeAiLabFrameSyncRuntimeEventSample runtimeEvents = default)
        {
            int runtimeFrame = frame.Value > int.MaxValue
                ? int.MaxValue
                : (int)frame.Value;
            int count = entities != null ? entities.Count : 0;
            var samples = new CharacterTestRuntimeAiLabFrameSyncCharacterSample[count];
            for (int i = 0; i < count; i++)
            {
                samples[i] = CharacterTestRuntimeAiLabFrameSyncCharacterSample.Capture(
                    entities[i],
                    actionRuntime,
                    gameplayComponentWorld);
            }

            return new CharacterTestRuntimeAiLabFrameSyncSample(
                runtimeFrame,
                stateHash,
                runtimeFrame,
                runtimeFrame,
                samples,
                projectileState != null,
                projectileState != null ? projectileState.SchemaVersion : 0,
                projectileState != null ? projectileState.DefinitionHash : 0L,
                projectileState != null ? projectileState.NextProjectileId : 0,
                CharacterTestRuntimeAiLabFrameSyncProjectileSample.Capture(projectileState),
                abilityState != null,
                abilityState != null ? abilityState.SchemaVersion : 0,
                abilityState != null ? abilityState.ModuleId : string.Empty,
                abilityState != null ? abilityState.DefinitionHash : 0L,
                CharacterTestRuntimeAiLabFrameSyncAbilityCooldownSample.Capture(abilityState),
                new CharacterTestRuntimeAiLabFrameSyncOverlaySample(
                    available: false,
                    frame: runtimeFrame,
                    agentEntityId: 0,
                    targetEntityId: 0,
                    sourceCenter: Vector3.zero,
                    targetCenter: Vector3.zero,
                    moveEnd: Vector3.zero,
                    attackRange: 0f),
                CharacterTestRuntimeAiLabFrameSyncGuardAuraSample.Capture(
                    guardAuraLastTickSummary,
                    onHitBuffLastSummary),
                CharacterTestRuntimeAiLabFrameSyncPoisonBottlePoolSample.Capture(
                    poisonBottlePoolState,
                    poisonBottlePoolLastTickSummary,
                    lastPoisonTickTargetCount),
                CharacterTestRuntimeAiLabFrameSyncPhysicalShieldSample.Capture(
                    physicalShieldState,
                    physicalShieldLastTickSummary),
                CharacterTestRuntimeAiLabFrameSyncHookPullSample.Capture(hookPullState),
                CharacterTestRuntimeAiLabFrameSyncArcMineSample.Capture(arcMineState),
                CharacterTestRuntimeAiLabFrameSyncRuneTurretSample.Capture(runeTurretState),
                CharacterTestRuntimeAiLabFrameSyncAreaConfinementSample.Capture(areaConfinementState),
                CharacterTestRuntimeAiLabFrameSyncTornadoSample.Capture(
                    tornadoDamageTickTargetCount,
                    tornadoInteractionHistory),
                CharacterTestRuntimeAiLabFrameSyncChainLightningSample.Capture(
                    chainLightningTargetCount,
                    chainLightningVfxEvents),
                CharacterTestRuntimeAiLabFrameSyncLaserPathSample.Capture(laserPathVfxEvents),
                CharacterTestRuntimeAiLabFrameSyncInteractionSample.Capture(
                    interactionHistory,
                    interactionVfxSummary),
                CharacterTestRuntimeAiLabFrameSyncMovableNaturalObjectSample.Capture(
                    movableNaturalObjects),
                commandState,
                vfxRuntime,
                audio,
                planner,
                runtimeEvents);
        }

        public static CharacterTestRuntimeAiLabFrameSyncReport Compare(
            CharacterTestRuntimeAiLabFrameSyncSample expected,
            CharacterTestRuntimeAiLabFrameSyncSample actual)
        {
            var rows = new List<CharacterTestRuntimeAiLabFrameSyncMismatch>(16);
            AddMismatch(rows, "frame", 0, "runtimeFrame", expected.RuntimeFrame, actual.RuntimeFrame);
            AddMismatch(rows, "frame", 0, "selectedFrame", expected.SelectedFrame, actual.SelectedFrame);
            AddMismatch(rows, "frame", 0, "previewFrame", expected.PreviewFrame, actual.PreviewFrame);
            AddMismatch(rows, "stateHash", 0, "value", expected.StateHash, actual.StateHash);
            CompareCommandState(
                expected.CommandState,
                actual.CommandState,
                rows,
                includeTransientFrameState: true);
            CompareRuntimeEvents(expected.RuntimeEvents, actual.RuntimeEvents, rows);
            ComparePlanner(expected.Planner, actual.Planner, rows);
            CompareCharacters(expected.Characters, actual.Characters, rows);
            CompareProjectiles(expected, actual, rows);
            CompareAudio(expected.Audio, actual.Audio, rows);
            CompareArcMine(expected.ArcMine, actual.ArcMine, rows);
            CompareRuneTurret(expected.RuneTurret, actual.RuneTurret, rows);
            CompareAreaConfinement(expected.AreaConfinement, actual.AreaConfinement, rows);
            CompareHookPull(expected.HookPull, actual.HookPull, rows);
            ComparePoisonBottlePool(
                expected.PoisonBottlePool,
                actual.PoisonBottlePool,
                rows,
                includeTransientFrameState: true);
            ComparePhysicalShield(
                expected.PhysicalShield,
                actual.PhysicalShield,
                rows,
                includeTransientFrameState: true);
            CompareTornado(expected.Tornado, actual.Tornado, rows);
            CompareChainLightning(expected.ChainLightning, actual.ChainLightning, rows);
            CompareLaserPath(expected.LaserPath, actual.LaserPath, rows);
            CompareInteraction(expected.Interaction, actual.Interaction, rows);
            CompareAbility(expected, actual, rows);
            CompareGuardAura(expected.GuardAura, actual.GuardAura, rows);
            CompareMovableNaturalObjects(expected.MovableNaturalObjects, actual.MovableNaturalObjects, rows);
            CharacterTestRuntimeAiLabFrameSyncReport presentation =
                CharacterTestRuntimeAiLabPresentationDiagnosticProbe.Compare(
                    BuildPresentationSample(expected),
                    BuildPresentationSample(actual));
            for (int i = 0; i < presentation.MismatchRows.Count; i++)
                rows.Add(presentation.MismatchRows[i]);
            return new CharacterTestRuntimeAiLabFrameSyncReport(rows);
        }

        private static CharacterTestRuntimeAiLabPresentationDiagnosticSample BuildPresentationSample(
            CharacterTestRuntimeAiLabFrameSyncSample source)
        {
            IReadOnlyList<CharacterTestRuntimeAiLabFrameSyncCharacterSample> sourceCharacters = source.Characters;
            int count = sourceCharacters != null ? sourceCharacters.Count : 0;
            var characters = new CharacterTestRuntimeAiLabPresentationCharacterSample[count];
            for (int i = 0; i < count; i++)
            {
                CharacterTestRuntimeAiLabFrameSyncCharacterSample character = sourceCharacters[i];
                characters[i] = new CharacterTestRuntimeAiLabPresentationCharacterSample(
                    character.EntityId,
                    character.HasUnityView,
                    character.UnityViewXMillimeters,
                    character.UnityViewYMillimeters,
                    character.UnityViewZMillimeters,
                    character.UnityViewYawMilliDegrees,
                    character.PresentationState,
                    character.AnimationBlendXMilli,
                    character.AnimationBlendYMilli,
                    character.AnimationSpeed01Milli,
                    character.AnimationActionKey,
                    character.AnimationGrounded,
                    character.AnimationAirborne);
            }

            return new CharacterTestRuntimeAiLabPresentationDiagnosticSample(
                characters,
                source.VfxRuntime,
                source.Overlay);
        }

        private static void AddVectorMismatch(
            List<CharacterTestRuntimeAiLabFrameSyncMismatch> rows,
            string layer,
            int entityId,
            string field,
            Vector3 expected,
            Vector3 actual)
        {
            AddFloatMismatch(rows, layer, entityId, field + ".x", expected.x, actual.x);
            AddFloatMismatch(rows, layer, entityId, field + ".y", expected.y, actual.y);
            AddFloatMismatch(rows, layer, entityId, field + ".z", expected.z, actual.z);
        }

        private static void AddFloatMismatch(
            List<CharacterTestRuntimeAiLabFrameSyncMismatch> rows,
            string layer,
            int entityId,
            string field,
            float expected,
            float actual)
        {
            if (Math.Abs(expected - actual) <= OverlayEpsilon)
                return;

            rows.Add(new CharacterTestRuntimeAiLabFrameSyncMismatch(
                layer,
                entityId,
                field,
                expected.ToString("0.###", CultureInfo.InvariantCulture),
                actual.ToString("0.###", CultureInfo.InvariantCulture)));
        }

        private static void AddMismatch(
            List<CharacterTestRuntimeAiLabFrameSyncMismatch> rows,
            string layer,
            int entityId,
            string field,
            int expected,
            int actual)
        {
            if (expected == actual)
                return;

            rows.Add(new CharacterTestRuntimeAiLabFrameSyncMismatch(
                layer,
                entityId,
                field,
                expected.ToString(CultureInfo.InvariantCulture),
                actual.ToString(CultureInfo.InvariantCulture)));
        }

        private static void AddMismatch(
            List<CharacterTestRuntimeAiLabFrameSyncMismatch> rows,
            string layer,
            int entityId,
            string field,
            long expected,
            long actual)
        {
            if (expected == actual)
                return;

            rows.Add(new CharacterTestRuntimeAiLabFrameSyncMismatch(
                layer,
                entityId,
                field,
                expected.ToString(CultureInfo.InvariantCulture),
                actual.ToString(CultureInfo.InvariantCulture)));
        }

        private static void AddMismatch(
            List<CharacterTestRuntimeAiLabFrameSyncMismatch> rows,
            string layer,
            int entityId,
            string field,
            bool expected,
            bool actual)
        {
            if (expected == actual)
                return;

            rows.Add(new CharacterTestRuntimeAiLabFrameSyncMismatch(
                layer,
                entityId,
                field,
                expected ? "true" : "false",
                actual ? "true" : "false"));
        }

        private static void AddMismatch(
            List<CharacterTestRuntimeAiLabFrameSyncMismatch> rows,
            string layer,
            int entityId,
            string field,
            CharacterActionId expected,
            CharacterActionId actual)
        {
            if (expected == actual)
                return;

            rows.Add(new CharacterTestRuntimeAiLabFrameSyncMismatch(
                layer,
                entityId,
                field,
                expected.ToString(),
                actual.ToString()));
        }

        private static void AddMismatch(
            List<CharacterTestRuntimeAiLabFrameSyncMismatch> rows,
            string layer,
            int entityId,
            string field,
            AudioTraceIdentity expected,
            AudioTraceIdentity actual)
        {
            if (expected == actual)
                return;

            rows.Add(new CharacterTestRuntimeAiLabFrameSyncMismatch(
                layer,
                entityId,
                field,
                expected.ToString(),
                actual.ToString()));
        }

        private static void AddMismatch(
            List<CharacterTestRuntimeAiLabFrameSyncMismatch> rows,
            string layer,
            int entityId,
            string field,
            string expected,
            string actual)
        {
            if (string.Equals(expected ?? string.Empty, actual ?? string.Empty, StringComparison.Ordinal))
                return;

            rows.Add(new CharacterTestRuntimeAiLabFrameSyncMismatch(
                layer,
                entityId,
                field,
                expected ?? string.Empty,
                actual ?? string.Empty));
        }
    }

    internal sealed class CharacterTestRuntimeAiLabFrameSyncSample
    {
        public CharacterTestRuntimeAiLabFrameSyncSample(
            int runtimeFrame,
            long stateHash,
            int selectedFrame,
            int previewFrame,
            CharacterTestRuntimeAiLabFrameSyncCharacterSample[] characters,
            bool projectileRuntimeAvailable,
            int projectileSchemaVersion,
            long projectileDefinitionHash,
            int projectileNextProjectileId,
            CharacterTestRuntimeAiLabFrameSyncProjectileSample[] projectiles,
            bool abilityRuntimeAvailable,
            int abilitySchemaVersion,
            string abilityModuleId,
            long abilityDefinitionHash,
            CharacterTestRuntimeAiLabFrameSyncAbilityCooldownSample[] abilityCooldowns,
            CharacterTestRuntimeAiLabFrameSyncOverlaySample overlay,
            CharacterTestRuntimeAiLabFrameSyncGuardAuraSample guardAura = default,
            CharacterTestRuntimeAiLabFrameSyncPoisonBottlePoolSample poisonBottlePool = default,
            CharacterTestRuntimeAiLabFrameSyncPhysicalShieldSample physicalShield = default,
            CharacterTestRuntimeAiLabFrameSyncHookPullSample hookPull = default,
            CharacterTestRuntimeAiLabFrameSyncArcMineSample arcMine = default,
            CharacterTestRuntimeAiLabFrameSyncRuneTurretSample runeTurret = default,
            CharacterTestRuntimeAiLabFrameSyncAreaConfinementSample areaConfinement = default,
            CharacterTestRuntimeAiLabFrameSyncTornadoSample tornado = default,
            CharacterTestRuntimeAiLabFrameSyncChainLightningSample chainLightning = default,
            CharacterTestRuntimeAiLabFrameSyncLaserPathSample laserPath = default,
            CharacterTestRuntimeAiLabFrameSyncInteractionSample interaction = default,
            CharacterTestRuntimeAiLabFrameSyncMovableNaturalObjectSample[] movableNaturalObjects = null,
            CharacterTestRuntimeAiLabFrameSyncCommandStateSample commandState = default,
            CharacterTestRuntimeAiLabFrameSyncVfxRuntimeSample vfxRuntime = default,
            CharacterTestRuntimeAiLabFrameSyncAudioSample audio = default,
            CharacterTestRuntimeAiLabFrameSyncPlannerSample planner = default,
            CharacterTestRuntimeAiLabFrameSyncRuntimeEventSample runtimeEvents = default)
        {
            RuntimeFrame = runtimeFrame;
            StateHash = stateHash;
            SelectedFrame = selectedFrame;
            PreviewFrame = previewFrame;
            CommandState = commandState;
            Characters = characters ?? Array.Empty<CharacterTestRuntimeAiLabFrameSyncCharacterSample>();
            ProjectileRuntimeAvailable = projectileRuntimeAvailable;
            ProjectileSchemaVersion = projectileSchemaVersion;
            ProjectileDefinitionHash = projectileDefinitionHash;
            ProjectileNextProjectileId = projectileNextProjectileId;
            Projectiles = projectiles ?? Array.Empty<CharacterTestRuntimeAiLabFrameSyncProjectileSample>();
            AbilityRuntimeAvailable = abilityRuntimeAvailable;
            AbilitySchemaVersion = abilitySchemaVersion;
            AbilityModuleId = abilityModuleId ?? string.Empty;
            AbilityDefinitionHash = abilityDefinitionHash;
            AbilityCooldowns = abilityCooldowns ?? Array.Empty<CharacterTestRuntimeAiLabFrameSyncAbilityCooldownSample>();
            Overlay = overlay;
            GuardAura = guardAura;
            PoisonBottlePool = poisonBottlePool;
            PhysicalShield = physicalShield;
            HookPull = hookPull;
            ArcMine = arcMine;
            RuneTurret = runeTurret;
            AreaConfinement = areaConfinement;
            Tornado = tornado;
            ChainLightning = chainLightning;
            LaserPath = laserPath;
            Interaction = interaction;
            MovableNaturalObjects = movableNaturalObjects
                ?? Array.Empty<CharacterTestRuntimeAiLabFrameSyncMovableNaturalObjectSample>();
            VfxRuntime = vfxRuntime;
            Audio = audio;
            Planner = planner;
            RuntimeEvents = runtimeEvents;
        }

        public int RuntimeFrame { get; }
        public long StateHash { get; }
        public int SelectedFrame { get; }
        public int PreviewFrame { get; }
        public CharacterTestRuntimeAiLabFrameSyncCommandStateSample CommandState { get; }
        public IReadOnlyList<CharacterTestRuntimeAiLabFrameSyncCharacterSample> Characters { get; }
        public bool ProjectileRuntimeAvailable { get; }
        public int ProjectileSchemaVersion { get; }
        public long ProjectileDefinitionHash { get; }
        public int ProjectileNextProjectileId { get; }
        public IReadOnlyList<CharacterTestRuntimeAiLabFrameSyncProjectileSample> Projectiles { get; }
        public bool AbilityRuntimeAvailable { get; }
        public int AbilitySchemaVersion { get; }
        public string AbilityModuleId { get; }
        public long AbilityDefinitionHash { get; }
        public IReadOnlyList<CharacterTestRuntimeAiLabFrameSyncAbilityCooldownSample> AbilityCooldowns { get; }
        public CharacterTestRuntimeAiLabFrameSyncGuardAuraSample GuardAura { get; }
        public CharacterTestRuntimeAiLabFrameSyncOverlaySample Overlay { get; }
        public CharacterTestRuntimeAiLabFrameSyncPoisonBottlePoolSample PoisonBottlePool { get; }
        public CharacterTestRuntimeAiLabFrameSyncPhysicalShieldSample PhysicalShield { get; }
        public CharacterTestRuntimeAiLabFrameSyncHookPullSample HookPull { get; }
        public CharacterTestRuntimeAiLabFrameSyncArcMineSample ArcMine { get; }
        public CharacterTestRuntimeAiLabFrameSyncRuneTurretSample RuneTurret { get; }
        public CharacterTestRuntimeAiLabFrameSyncAreaConfinementSample AreaConfinement { get; }
        public CharacterTestRuntimeAiLabFrameSyncTornadoSample Tornado { get; }
        public CharacterTestRuntimeAiLabFrameSyncChainLightningSample ChainLightning { get; }
        public CharacterTestRuntimeAiLabFrameSyncLaserPathSample LaserPath { get; }
        public CharacterTestRuntimeAiLabFrameSyncInteractionSample Interaction { get; }
        public IReadOnlyList<CharacterTestRuntimeAiLabFrameSyncMovableNaturalObjectSample> MovableNaturalObjects { get; }
        public CharacterTestRuntimeAiLabFrameSyncVfxRuntimeSample VfxRuntime { get; }
        public CharacterTestRuntimeAiLabFrameSyncAudioSample Audio { get; }
        public CharacterTestRuntimeAiLabFrameSyncPlannerSample Planner { get; }
        public CharacterTestRuntimeAiLabFrameSyncRuntimeEventSample RuntimeEvents { get; }
    }
}
