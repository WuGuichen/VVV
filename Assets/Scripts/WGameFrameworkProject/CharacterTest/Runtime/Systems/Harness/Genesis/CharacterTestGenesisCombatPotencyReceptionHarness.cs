using System;
using System.Collections.Generic;
using System.Globalization;
using MxFramework.Combat.Animation;
using MxFramework.Combat.GameplayBridge;
using MxFramework.Core.Math;
using MxFramework.Gameplay;
using MxFramework.Runtime;
using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    internal static class CharacterTestGenesisCombatPotencyReceptionHarness
    {
        public const int DefaultTickRate = 60;
        public const int DefaultBaseCohesion = 100;
        public const int DefaultRecoveryRate = 8;
        public const int DefaultRecoveryDelayFrames = 2;
        public const float DefaultSourceMassKg = CharacterTestImpactResponseResolver.DefaultMassKg;
        public const float DefaultTargetMassKg = CharacterTestImpactResponseResolver.DefaultMassKg;
        public const float DefaultHitZoneImpulseScale = 1f;
        public const float DefaultEffectiveDeceleration = 66f;

        private const string StateModel = "harness-only";
        private const int SourceId = 626;

        public static CharacterTestGenesisCombatPotencyReceptionValidation RunValidationSuite(
            RuntimeFrame frame = default)
        {
            CharacterTestGenesisCombatPotencyReceptionSample heavyHammer =
                RunSample(CreateHeavyHammerDefinition(), frame);
            CharacterTestGenesisCombatPotencyReceptionSample lightBlade =
                RunSample(CreateLightBladeDefinition(), frame);
            CharacterTestGenesisCombatPotencyReceptionSample shieldCharge =
                RunSample(CreateShieldChargeDefinition(), frame);
            CharacterTestGenesisCombatPotencyReceptionSample flame =
                RunSample(CreateFlameDefinition(), frame);

            return new CharacterTestGenesisCombatPotencyReceptionValidation(
                frame,
                StateModel,
                heavyHammer,
                lightBlade,
                shieldCharge,
                flame);
        }

        public static CharacterTestGenesisCombatPotencyReceptionSample RunHeavyHammerSample(
            RuntimeFrame frame = default)
        {
            return RunSample(CreateHeavyHammerDefinition(), frame);
        }

        public static CharacterTestGenesisCombatPotencyReceptionSample RunLightBladeSample(
            RuntimeFrame frame = default)
        {
            return RunSample(CreateLightBladeDefinition(), frame);
        }

        public static CharacterTestGenesisCombatPotencyReceptionSample RunShieldChargeSample(
            RuntimeFrame frame = default)
        {
            return RunSample(CreateShieldChargeDefinition(), frame);
        }

        public static CharacterTestGenesisCombatPotencyReceptionSample RunFlameSample(
            RuntimeFrame frame = default)
        {
            return RunSample(CreateFlameDefinition(), frame);
        }

        private static CharacterTestGenesisCombatPotencyReceptionSample RunSample(
            CharacterTestGenesisCombatPotencyReceptionDefinition definition,
            RuntimeFrame startFrame)
        {
            var componentWorld = new GameplayComponentWorld();
            GameplayEntityId target = componentWorld.CreateEntity();
            GameplayComponentStore<GameplayScatterPressureComponent> store =
                componentWorld.GetOrCreateStore<GameplayScatterPressureComponent>();
            store.Set(target, new GameplayScatterPressureComponent(
                DefaultBaseCohesion,
                DefaultRecoveryRate,
                DefaultRecoveryDelayFrames));

            var system = new GameplayScatterPressureSystem();
            var pressureEvents = new List<ScatterPressureChangedEvent>();
            var impactEvents = new List<ScatterImpactResolvedEvent>();
            var bandEvents = new List<PressureBandChangedEvent>();
            var breakEvents = new List<CohesionBreakEvent>();
            system.PressureChangedEvents.Subscribe(evt => pressureEvents.Add(evt));
            system.ScatterImpactResolvedEvents.Subscribe(evt => impactEvents.Add(evt));
            system.BandChangedEvents.Subscribe(evt => bandEvents.Add(evt));
            system.CohesionBreakEvents.Subscribe(evt => breakEvents.Add(evt));

            int cohesionLimitBefore = DefaultBaseCohesion;
            int cohesionLimit = CombatCohesionLimitAdapter.CalculateCohesionLimit(
                DefaultBaseCohesion,
                definition.SupportProfile);
            bool cohesionSupportWindowActive = definition.SupportProfile.HasValue
                && CombatCohesionLimitAdapter.IsInCohesionSupportWindow(
                    definition.SupportProfile.Value,
                    definition.LocalFrame);
            float supportRate = definition.SupportProfile.HasValue
                ? ToFloat(definition.SupportProfile.Value.SupportRate)
                : 1f;

            long frame = startFrame.Value;
            if (definition.SupportProfile.HasValue)
            {
                system.Enqueue(new GameplayCohesionLimitRequest(
                    target,
                    cohesionLimit,
                    SourceId,
                    new GameplayScatterPressureReason(90870),
                    new RuntimeTraceIdentity(90870, SourceId, startFrame.Value, (int)definition.Kind)));
                system.Tick(CreateContext(componentWorld, frame));
                frame++;
            }

            CharacterTestImpactResponse impact = CharacterTestImpactResponseResolver.Resolve(
                definition.ImpactPower,
                definition.HitZoneImpulseScale,
                definition.SourceMassKg,
                definition.TargetMassKg,
                Vector3.forward,
                DefaultEffectiveDeceleration);

            int scatterPressureBefore = 0;
            int scatterPressureAfterFirstHit = 0;
            int scatterPressureAfterInputs = 0;
            PressureBand bandAfterFirstHit = PressureBand.Stable;
            PressureBand bandAfterInputs = PressureBand.Stable;
            int hitCountToBreak = 0;

            for (int i = 0; i < definition.HitCount; i++)
            {
                int breakCountBefore = breakEvents.Count;
                if (i == 0 && store.TryGet(target, out GameplayScatterPressureComponent beforeFirst))
                    scatterPressureBefore = beforeFirst.CurrentScatterPressure;

                system.Enqueue(new GameplayScatterPressureRequest(
                    target,
                    impact.ScatterImpact,
                    SourceId,
                    new GameplayScatterPressureReason(definition.IsContinuousInput ? 90871 : 90872),
                    new RuntimeTraceIdentity(90871, SourceId, startFrame.Value, ((long)(int)definition.Kind << 32) | (uint)i)));
                system.Tick(CreateContext(componentWorld, frame));
                frame++;

                if (store.TryGet(target, out GameplayScatterPressureComponent afterHit))
                {
                    if (i == 0)
                    {
                        scatterPressureAfterFirstHit = afterHit.CurrentScatterPressure;
                        bandAfterFirstHit = afterHit.CurrentBand;
                    }

                    scatterPressureAfterInputs = afterHit.CurrentScatterPressure;
                    bandAfterInputs = afterHit.CurrentBand;
                }

                if (hitCountToBreak == 0 && breakEvents.Count > breakCountBefore)
                    hitCountToBreak = i + 1;
            }

            int scatterPressureBeforeRecovery = scatterPressureAfterInputs;
            for (int i = 0; i < definition.RecoveryTickCount; i++)
            {
                system.Tick(CreateContext(componentWorld, frame));
                frame++;
            }

            int scatterPressureAfterRecovery = scatterPressureAfterInputs;
            PressureBand bandAfterRecovery = bandAfterInputs;
            if (store.TryGet(target, out GameplayScatterPressureComponent final))
            {
                scatterPressureAfterRecovery = final.CurrentScatterPressure;
                bandAfterRecovery = final.CurrentBand;
            }

            int recoveryEventCount = 0;
            for (int i = 0; i < pressureEvents.Count; i++)
            {
                if (pressureEvents[i].Reason == GameplayScatterPressureEvents.RecoveryReason)
                {
                    recoveryEventCount++;
                }
            }

            long firstBreakFrame = breakEvents.Count > 0 ? breakEvents[0].Frame.Value : -1L;
            return new CharacterTestGenesisCombatPotencyReceptionSample(
                definition.Kind,
                definition.SampleId,
                StateModel,
                definition.InputD,
                definition.InputS,
                definition.IsContinuousInput,
                definition.ImpactPower,
                impact.SourceMassKg,
                impact.TargetMassKg,
                impact.HitZoneImpulseScale,
                impact.MassTransfer,
                impact.EffectiveImpact,
                impact.ScatterImpact,
                definition.HitCount,
                hitCountToBreak,
                scatterPressureBefore,
                scatterPressureAfterFirstHit,
                scatterPressureAfterInputs,
                scatterPressureBeforeRecovery,
                scatterPressureAfterRecovery,
                cohesionLimitBefore,
                cohesionLimit,
                DefaultBaseCohesion,
                definition.SupportProfile.HasValue,
                cohesionSupportWindowActive,
                supportRate,
                definition.LocalFrame,
                bandAfterFirstHit,
                bandAfterInputs,
                bandAfterRecovery,
                pressureEvents.Count,
                impactEvents.Count,
                bandEvents.Count,
                breakEvents.Count,
                recoveryEventCount,
                firstBreakFrame,
                DefaultRecoveryDelayFrames,
                DefaultRecoveryRate,
                definition.RecoveryTickCount,
                impact.PreviewDistanceMeters,
                impact.KnockbackSpeed,
                definition.LaterHook);
        }

        private static GameplaySystemContext CreateContext(
            GameplayComponentWorld world,
            long frame)
        {
            return new GameplaySystemContext(
                new RuntimeFrame(frame),
                1d / DefaultTickRate,
                frame / (double)DefaultTickRate,
                new GameplayWorld(),
                Array.Empty<RuntimeCommand>(),
                world.Events,
                componentWorld: world);
        }

        private static CharacterTestGenesisCombatPotencyReceptionDefinition CreateHeavyHammerDefinition()
        {
            return new CharacterTestGenesisCombatPotencyReceptionDefinition(
                CharacterTestGenesisCombatPotencyReceptionKind.HeavyHammer,
                "genesis.combat_potency.heavy_hammer",
                inputD: 70,
                inputS: 70,
                impactPower: 70,
                hitCount: 2,
                isContinuousInput: false,
                supportProfile: null,
                localFrame: 0,
                recoveryTickCount: 5,
                laterHook: "knockback");
        }

        private static CharacterTestGenesisCombatPotencyReceptionDefinition CreateLightBladeDefinition()
        {
            return new CharacterTestGenesisCombatPotencyReceptionDefinition(
                CharacterTestGenesisCombatPotencyReceptionKind.LightBlade,
                "genesis.combat_potency.light_blade",
                inputD: 12,
                inputS: 12,
                impactPower: 12,
                hitCount: 6,
                isContinuousInput: false,
                supportProfile: null,
                localFrame: 0,
                recoveryTickCount: 5,
                laterHook: "repeated-pressure");
        }

        private static CharacterTestGenesisCombatPotencyReceptionDefinition CreateShieldChargeDefinition()
        {
            var supportProfile = new CombatActionSupportProfile(
                Fix64.FromRatio(3, 2),
                Fix64.FromRatio(3, 2),
                hasCohesionSupportWindow: true,
                cohesionSupportWindow: new CombatFrameRange(0, 12));

            return new CharacterTestGenesisCombatPotencyReceptionDefinition(
                CharacterTestGenesisCombatPotencyReceptionKind.ShieldCharge,
                "genesis.combat_potency.shield_charge",
                inputD: 70,
                inputS: 70,
                impactPower: 70,
                hitCount: 2,
                isContinuousInput: false,
                supportProfile: supportProfile,
                localFrame: 4,
                recoveryTickCount: 5,
                laterHook: "cohesion-support");
        }

        private static CharacterTestGenesisCombatPotencyReceptionDefinition CreateFlameDefinition()
        {
            return new CharacterTestGenesisCombatPotencyReceptionDefinition(
                CharacterTestGenesisCombatPotencyReceptionKind.Flame,
                "genesis.combat_potency.flame",
                inputD: 18,
                inputS: 22,
                impactPower: 18,
                hitCount: 4,
                isContinuousInput: true,
                supportProfile: null,
                localFrame: 0,
                recoveryTickCount: 5,
                laterHook: "material-transformation-burning");
        }

        private static float ToFloat(Fix64 value)
        {
            return (float)((double)value.RawValue / Fix64.Scale);
        }

        private readonly struct CharacterTestGenesisCombatPotencyReceptionDefinition
        {
            public CharacterTestGenesisCombatPotencyReceptionDefinition(
                CharacterTestGenesisCombatPotencyReceptionKind kind,
                string sampleId,
                int inputD,
                int inputS,
                int impactPower,
                int hitCount,
                bool isContinuousInput,
                CombatActionSupportProfile? supportProfile,
                int localFrame,
                int recoveryTickCount,
                string laterHook)
            {
                Kind = kind;
                SampleId = sampleId ?? string.Empty;
                InputD = inputD < 0 ? 0 : inputD;
                InputS = inputS < 0 ? 0 : inputS;
                ImpactPower = impactPower < 0 ? 0 : impactPower;
                HitCount = hitCount < 0 ? 0 : hitCount;
                IsContinuousInput = isContinuousInput;
                SupportProfile = supportProfile;
                LocalFrame = localFrame < 0 ? 0 : localFrame;
                RecoveryTickCount = recoveryTickCount < 0 ? 0 : recoveryTickCount;
                LaterHook = laterHook ?? string.Empty;
                SourceMassKg = DefaultSourceMassKg;
                TargetMassKg = DefaultTargetMassKg;
                HitZoneImpulseScale = DefaultHitZoneImpulseScale;
            }

            public CharacterTestGenesisCombatPotencyReceptionKind Kind { get; }
            public string SampleId { get; }
            public int InputD { get; }
            public int InputS { get; }
            public int ImpactPower { get; }
            public int HitCount { get; }
            public bool IsContinuousInput { get; }
            public CombatActionSupportProfile? SupportProfile { get; }
            public int LocalFrame { get; }
            public int RecoveryTickCount { get; }
            public string LaterHook { get; }
            public float SourceMassKg { get; }
            public float TargetMassKg { get; }
            public float HitZoneImpulseScale { get; }
        }
    }

}
