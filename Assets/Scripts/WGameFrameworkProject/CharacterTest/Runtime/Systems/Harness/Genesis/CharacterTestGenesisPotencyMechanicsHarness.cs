using System;
using System.Collections.Generic;
using System.Globalization;
using MxFramework.CharacterAction;
using MxFramework.Combat.Core;
using MxFramework.Combat.Motion;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using MxFramework.Resources;
using MxFramework.Runtime;
using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    internal static partial class CharacterTestGenesisPotencyMechanicsHarness
    {
        public const int DefaultTickRate = 60;

        private const int PotencyLayer = 3;
        private const int PotencyTraceId = 625;
        private const float ContactHalfExtentMeters = 0.2f;
        private const int BurningCohesionDrop = 12;
        private const float NextContactPressure = 12f;

        internal static CharacterTestGenesisPotencyMechanicsValidation RunProbe(
            CharacterTestGenesisPotencyMechanicsProbeKind scenario,
            RuntimeFrame frame)
        {
            return RunValidationSuite(scenario, frame);
        }

        internal static CharacterTestGenesisPotencyMechanicsValidation RunValidationSuite(
            CharacterTestGenesisPotencyMechanicsProbeKind scenario =
                CharacterTestGenesisPotencyMechanicsProbeKind.ValidationSuite,
            RuntimeFrame frame = default)
        {
            CharacterTestGenesisPotencyMechanicsSample fireDryGrass = RunSample(
                sampleId: 625101,
                material: CreateDryGrassState(),
                influenceKind: CharacterTestGenesisPotencyInfluenceKind.Fire,
                frame);
            CharacterTestGenesisPotencyMechanicsSample fireStone = RunSample(
                sampleId: 625102,
                material: CreateStoneState(),
                influenceKind: CharacterTestGenesisPotencyInfluenceKind.Fire,
                frame);
            CharacterTestGenesisPotencyMechanicsSample hammerDryGrass = RunSample(
                sampleId: 625103,
                material: CreateDryGrassState(),
                influenceKind: CharacterTestGenesisPotencyInfluenceKind.Hammer,
                frame);
            CharacterTestGenesisPotencyMechanicsSample hammerStone = RunSample(
                sampleId: 625104,
                material: CreateStoneState(),
                influenceKind: CharacterTestGenesisPotencyInfluenceKind.Hammer,
                frame);

            return new CharacterTestGenesisPotencyMechanicsValidation(
                scenario,
                frame,
                "harness-only",
                fireDryGrass,
                fireStone,
                hammerDryGrass,
                hammerStone);
        }

        private static CharacterTestGenesisPotencyMechanicsSample RunSample(
            int sampleId,
            PotencyMaterialState material,
            CharacterTestGenesisPotencyInfluenceKind influenceKind,
            RuntimeFrame frame)
        {
            PotencyInfluenceDefinition influence = CreateInfluenceDefinition(influenceKind);
            var definition = new PotencySampleDefinition(sampleId, material, influence, frame);
            PotencySampleEvidence evidence = CreateSampleEvidence(definition);
            PotencyThresholdEvaluation evaluation = EvaluateThresholds(definition, evidence);
            return CreateSample(definition, evidence, evaluation);
        }

        private static PotencySampleEvidence CreateSampleEvidence(PotencySampleDefinition definition)
        {
            CharacterTestGenesisPotencyTriplet appliedDelta = ApplyImmunity(
                definition.Material,
                definition.Influence.RawDelta);
            CharacterTestGenesisPotencyTriplet ignoredDelta = definition.Influence.RawDelta - appliedDelta;
            CharacterTestGenesisPotencyTriplet after = definition.Material.Before + appliedDelta;
            PotencyPhysicsProbe physics = RunContactProbe(
                definition.SampleId,
                definition.Material,
                definition.Influence.Kind);

            return new PotencySampleEvidence(appliedDelta, ignoredDelta, after, physics);
        }

        private static PotencyThresholdEvaluation EvaluateThresholds(
            PotencySampleDefinition definition,
            PotencySampleEvidence evidence)
        {
            bool burningThreshold = definition.Influence.Kind == CharacterTestGenesisPotencyInfluenceKind.Fire
                && evidence.After.D >= 15
                && evidence.After.S >= 18
                && evidence.After.P <= 25;
            bool impactDeformed = definition.Influence.InputKind == CharacterTestGenesisPotencyInputKind.Impact
                && evidence.Physics.ContactSpeed >= 5f
                && evidence.After.D > evidence.After.P;
            bool crushThreshold = impactDeformed
                && definition.Material.Kind == CharacterTestGenesisPotencyMaterialKind.DryGrass
                && definition.Influence.Kind == CharacterTestGenesisPotencyInfluenceKind.Hammer;
            bool crackThreshold = impactDeformed
                && definition.Material.Kind == CharacterTestGenesisPotencyMaterialKind.Stone
                && definition.Influence.Kind == CharacterTestGenesisPotencyInfluenceKind.Hammer;

            CharacterTestGenesisPotencyResultKind result = CharacterTestGenesisPotencyResultKind.None;
            if (burningThreshold)
                result = CharacterTestGenesisPotencyResultKind.Burning;
            else if (crushThreshold)
                result = CharacterTestGenesisPotencyResultKind.CrushedOrBroken;
            else if (crackThreshold)
                result = CharacterTestGenesisPotencyResultKind.Cracked;

            return new PotencyThresholdEvaluation(burningThreshold, crushThreshold, crackThreshold, result);
        }

        private static CharacterTestGenesisPotencyMechanicsSample CreateSample(
            PotencySampleDefinition definition,
            PotencySampleEvidence evidence,
            PotencyThresholdEvaluation evaluation)
        {
            return new CharacterTestGenesisPotencyMechanicsSample(
                definition.SampleStableId,
                definition.SampleId,
                definition.Material.Kind,
                definition.Material.StableId,
                definition.Influence.Kind,
                definition.Influence.StableId,
                definition.Influence.InputKind,
                definition.Influence.ForceOrigin,
                definition.Frame,
                "harness-only",
                definition.Material.Before,
                definition.Influence.RawDelta,
                evidence.AppliedDelta,
                evidence.IgnoredDelta,
                evidence.After,
                CreateNextContactSummary(evidence.After, evaluation.Result),
                CreateImmunitySummary(definition.Material),
                evaluation.BurningThresholdMet,
                evaluation.CrushThresholdMet,
                evaluation.CrackThresholdMet,
                evaluation.Result,
                evaluation.ResultSource,
                externalTriggeredDirectly: false,
                evidence.Physics.BodyCount,
                evidence.Physics.ColliderCount,
                evidence.Physics.QueryHitCount,
                evidence.Physics.ExplainHitCount,
                evidence.Physics.CandidateCount,
                evidence.Physics.PostFilterCandidateCount,
                evidence.Physics.ContactSpeed,
                evidence.Physics.ContactDisplacement,
                evidence.Physics.HasContact);
        }

        private static CharacterTestGenesisPotencyNextContactSummary CreateNextContactSummary(
            CharacterTestGenesisPotencyTriplet after,
            CharacterTestGenesisPotencyResultKind result)
        {
            bool burning = result == CharacterTestGenesisPotencyResultKind.Burning;
            CharacterTestGenesisPotencyTriplet resultDelta = burning
                ? new CharacterTestGenesisPotencyTriplet(0, -BurningCohesionDrop, 0)
                : CharacterTestGenesisPotencyTriplet.Zero;
            CharacterTestGenesisPotencyTriplet resultDps = burning
                ? AddClamped(after, resultDelta)
                : after;

            int supportBefore = Math.Max(0, after.P);
            int supportAfter = Math.Max(0, resultDps.P);
            int structureBefore = CalculateStructureStrength(after);
            int structureAfter = CalculateStructureStrength(resultDps);
            float blockResistanceBefore = CalculateBlockResistance(supportBefore, structureBefore);
            float blockResistanceAfter = CalculateBlockResistance(supportAfter, structureAfter);
            float displacementBefore = CalculateNextContactDisplacement(blockResistanceBefore);
            float displacementAfter = CalculateNextContactDisplacement(blockResistanceAfter);

            return new CharacterTestGenesisPotencyNextContactSummary(
                burning,
                resultDelta,
                resultDps,
                supportBefore,
                supportAfter,
                structureBefore,
                structureAfter,
                blockResistanceBefore,
                blockResistanceAfter,
                displacementBefore,
                displacementAfter);
        }

        private static CharacterTestGenesisPotencyTriplet AddClamped(
            CharacterTestGenesisPotencyTriplet left,
            CharacterTestGenesisPotencyTriplet right)
        {
            return new CharacterTestGenesisPotencyTriplet(
                Math.Max(0, left.D + right.D),
                Math.Max(0, left.P + right.P),
                Math.Max(0, left.S + right.S));
        }

        private static int CalculateStructureStrength(CharacterTestGenesisPotencyTriplet state)
        {
            return Math.Max(0, state.P - (state.S / 2));
        }

        private static float CalculateBlockResistance(int support, int structure)
        {
            return (support + structure) * 0.5f;
        }

        private static float CalculateNextContactDisplacement(float blockResistance)
        {
            return NextContactPressure / (NextContactPressure + Math.Max(0f, blockResistance));
        }

        private static PotencyMaterialState CreateDryGrassState()
        {
            return new PotencyMaterialState(
                CharacterTestGenesisPotencyMaterialKind.DryGrass,
                "dry_grass",
                new CharacterTestGenesisPotencyTriplet(0, 20, 0),
                immuneD: false,
                immuneP: false,
                immuneS: false,
                CombatSurfaceMaterial.Rough);
        }

        private static PotencyMaterialState CreateStoneState()
        {
            return new PotencyMaterialState(
                CharacterTestGenesisPotencyMaterialKind.Stone,
                "stone",
                new CharacterTestGenesisPotencyTriplet(0, 90, 0),
                immuneD: false,
                immuneP: false,
                immuneS: true,
                CombatSurfaceMaterial.HardSlick);
        }

        private static PotencyInfluenceDefinition CreateInfluenceDefinition(
            CharacterTestGenesisPotencyInfluenceKind influenceKind)
        {
            return new PotencyInfluenceDefinition(
                influenceKind,
                CreateInfluenceStableId(influenceKind),
                CreateInputKind(influenceKind),
                CreateForceOrigin(influenceKind),
                CreateRawDelta(influenceKind));
        }

        private static CharacterTestGenesisPotencyTriplet CreateRawDelta(
            CharacterTestGenesisPotencyInfluenceKind influenceKind)
        {
            switch (influenceKind)
            {
                case CharacterTestGenesisPotencyInfluenceKind.Fire:
                    return new CharacterTestGenesisPotencyTriplet(18, 0, 22);
                case CharacterTestGenesisPotencyInfluenceKind.Hammer:
                    return new CharacterTestGenesisPotencyTriplet(70, -50, 6);
                default:
                    return CharacterTestGenesisPotencyTriplet.Zero;
            }
        }

        private static CharacterTestGenesisPotencyTriplet ApplyImmunity(
            PotencyMaterialState material,
            CharacterTestGenesisPotencyTriplet rawDelta)
        {
            return new CharacterTestGenesisPotencyTriplet(
                material.ImmuneD ? 0 : rawDelta.D,
                material.ImmuneP ? 0 : rawDelta.P,
                material.ImmuneS ? 0 : rawDelta.S);
        }

    }
}
