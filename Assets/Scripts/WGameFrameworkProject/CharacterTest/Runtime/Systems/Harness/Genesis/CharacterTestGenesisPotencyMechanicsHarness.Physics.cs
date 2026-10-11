using System;
using System.Collections.Generic;
using MxFramework.Combat.Core;
using MxFramework.Combat.Motion;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    internal static partial class CharacterTestGenesisPotencyMechanicsHarness
    {
        private static PotencyPhysicsProbe RunContactProbe(
            int sampleId,
            PotencyMaterialState material,
            CharacterTestGenesisPotencyInfluenceKind influenceKind)
        {
            CombatPhysicsWorld world = new CombatPhysicsWorld();
            var materialEntityId = new CombatEntityId(sampleId * 10 + 1);
            var materialBodyId = new CombatBodyId(sampleId * 10 + 1);
            var materialColliderId = new CombatColliderId(1);
            var influenceEntityId = new CombatEntityId(sampleId * 10 + 2);
            var influenceBodyId = new CombatBodyId(sampleId * 10 + 2);
            var influenceColliderId = new CombatColliderId(1);
            bool impact = influenceKind == CharacterTestGenesisPotencyInfluenceKind.Hammer;

            world.UpsertBody(new CombatPhysicsBody(materialEntityId, materialBodyId, FixVector3.Zero));
            world.UpsertBody(new CombatPhysicsBody(
                influenceEntityId,
                influenceBodyId,
                new FixVector3(impact ? ToFix64(0.06f) : Fix64.Zero, Fix64.Zero, Fix64.Zero)));
            world.UpsertAabbCollider(new CombatPhysicsAabbCollider(
                materialBodyId,
                materialColliderId,
                PotencyLayer,
                new FixVector3(ToFix64(-0.35f), Fix64.Zero, ToFix64(-0.35f)),
                new FixVector3(ToFix64(0.35f), ToFix64(0.5f), ToFix64(0.35f)),
                material.SurfaceMaterial));
            world.UpsertAabbCollider(new CombatPhysicsAabbCollider(
                influenceBodyId,
                influenceColliderId,
                PotencyLayer,
                new FixVector3(ToFix64(-ContactHalfExtentMeters), Fix64.Zero, ToFix64(-ContactHalfExtentMeters)),
                new FixVector3(ToFix64(ContactHalfExtentMeters), ToFix64(0.4f), ToFix64(ContactHalfExtentMeters)),
                influenceKind == CharacterTestGenesisPotencyInfluenceKind.Fire
                    ? CombatSurfaceMaterial.Elastic
                    : CombatSurfaceMaterial.Rough));

            var header = new CombatQueryHeader(
                queryId: sampleId,
                kind: CombatQueryKind.Aabb,
                sourceEntityId: influenceEntityId,
                traceId: PotencyTraceId,
                actionId: (int)influenceKind,
                sourceOrder: 0,
                layerMask: CombatPhysicsLayerMask.FromLayer(PotencyLayer));
            CombatPhysicsQuery query = CombatPhysicsQuery.From(
                new CombatAabbQuery(
                    header,
                    new FixVector3(ToFix64(-0.18f), Fix64.Zero, ToFix64(-0.18f)),
                    new FixVector3(ToFix64(0.28f), ToFix64(0.45f), ToFix64(0.18f))),
                new CombatPhysicsQueryFilter(includeSourceEntity: false));
            var hits = new List<CombatQueryResult>();
            world.Query(query, hits);
            CombatPhysicsQueryDebugReport report = world.ExplainQuery(query);
            CombatPhysicsWorldStats stats = world.CreateStats();

            return new PotencyPhysicsProbe(
                stats.BodyCount,
                stats.ColliderCount,
                hits.Count,
                report.HitCount,
                report.CandidateCount,
                report.PostFilterCandidateCount,
                impact ? 6.25f : 0f,
                impact ? 0.08f : 0f,
                hits.Count > 0);
        }

        private static CharacterTestGenesisPotencyInputKind CreateInputKind(
            CharacterTestGenesisPotencyInfluenceKind influenceKind)
        {
            return influenceKind == CharacterTestGenesisPotencyInfluenceKind.Hammer
                ? CharacterTestGenesisPotencyInputKind.Impact
                : CharacterTestGenesisPotencyInputKind.Continuous;
        }

        private static CharacterTestGenesisForceOriginKind CreateForceOrigin(
            CharacterTestGenesisPotencyInfluenceKind influenceKind)
        {
            return influenceKind == CharacterTestGenesisPotencyInfluenceKind.Hammer
                ? CharacterTestGenesisForceOriginKind.InstantImpulse
                : CharacterTestGenesisForceOriginKind.ContinuousExternalForce;
        }

        private static string CreateImmunitySummary(PotencyMaterialState material)
        {
            if (!material.ImmuneD && !material.ImmuneP && !material.ImmuneS)
                return "none";

            string value = string.Empty;
            if (material.ImmuneD)
                value = AppendImmunity(value, "D");
            if (material.ImmuneP)
                value = AppendImmunity(value, "P");
            if (material.ImmuneS)
                value = AppendImmunity(value, "S");
            return value;
        }

        private static string AppendImmunity(string current, string value)
        {
            return string.IsNullOrEmpty(current) ? value : current + "," + value;
        }

        private static string CreateSampleStableId(
            CharacterTestGenesisPotencyInfluenceKind influenceKind,
            CharacterTestGenesisPotencyMaterialKind materialKind)
        {
            return CreateInfluenceStableId(influenceKind) + "->" + CreateMaterialStableId(materialKind);
        }

        private static string CreateInfluenceStableId(CharacterTestGenesisPotencyInfluenceKind influenceKind)
        {
            switch (influenceKind)
            {
                case CharacterTestGenesisPotencyInfluenceKind.Fire:
                    return "fire";
                case CharacterTestGenesisPotencyInfluenceKind.Hammer:
                    return "hammer";
                default:
                    return "none";
            }
        }

        private static string CreateMaterialStableId(CharacterTestGenesisPotencyMaterialKind materialKind)
        {
            switch (materialKind)
            {
                case CharacterTestGenesisPotencyMaterialKind.DryGrass:
                    return "dry_grass";
                case CharacterTestGenesisPotencyMaterialKind.Stone:
                    return "stone";
                default:
                    return "none";
            }
        }

        private static Fix64 ToFix64(float value)
        {
            return Fix64.FromRaw((long)Math.Round(value * Fix64.Scale));
        }

        private readonly struct PotencySampleDefinition
        {
            public PotencySampleDefinition(
                int sampleId,
                PotencyMaterialState material,
                PotencyInfluenceDefinition influence,
                RuntimeFrame frame)
            {
                SampleId = sampleId;
                SampleStableId =
                    CharacterTestGenesisPotencyMechanicsHarness.CreateSampleStableId(influence.Kind, material.Kind);
                Material = material;
                Influence = influence;
                Frame = frame;
            }

            public int SampleId { get; }
            public string SampleStableId { get; }
            public PotencyMaterialState Material { get; }
            public PotencyInfluenceDefinition Influence { get; }
            public RuntimeFrame Frame { get; }
        }

        private readonly struct PotencyMaterialState
        {
            public PotencyMaterialState(
                CharacterTestGenesisPotencyMaterialKind kind,
                string stableId,
                CharacterTestGenesisPotencyTriplet before,
                bool immuneD,
                bool immuneP,
                bool immuneS,
                CombatSurfaceMaterial surfaceMaterial)
            {
                Kind = kind;
                StableId = stableId ?? string.Empty;
                Before = before;
                ImmuneD = immuneD;
                ImmuneP = immuneP;
                ImmuneS = immuneS;
                SurfaceMaterial = surfaceMaterial;
            }

            public CharacterTestGenesisPotencyMaterialKind Kind { get; }
            public string StableId { get; }
            public CharacterTestGenesisPotencyTriplet Before { get; }
            public bool ImmuneD { get; }
            public bool ImmuneP { get; }
            public bool ImmuneS { get; }
            public CombatSurfaceMaterial SurfaceMaterial { get; }
        }

        private readonly struct PotencyInfluenceDefinition
        {
            public PotencyInfluenceDefinition(
                CharacterTestGenesisPotencyInfluenceKind kind,
                string stableId,
                CharacterTestGenesisPotencyInputKind inputKind,
                CharacterTestGenesisForceOriginKind forceOrigin,
                CharacterTestGenesisPotencyTriplet rawDelta)
            {
                Kind = kind;
                StableId = stableId ?? string.Empty;
                InputKind = inputKind;
                ForceOrigin = forceOrigin;
                RawDelta = rawDelta;
            }

            public CharacterTestGenesisPotencyInfluenceKind Kind { get; }
            public string StableId { get; }
            public CharacterTestGenesisPotencyInputKind InputKind { get; }
            public CharacterTestGenesisForceOriginKind ForceOrigin { get; }
            public CharacterTestGenesisPotencyTriplet RawDelta { get; }
        }

        private readonly struct PotencySampleEvidence
        {
            public PotencySampleEvidence(
                CharacterTestGenesisPotencyTriplet appliedDelta,
                CharacterTestGenesisPotencyTriplet ignoredDelta,
                CharacterTestGenesisPotencyTriplet after,
                PotencyPhysicsProbe physics)
            {
                AppliedDelta = appliedDelta;
                IgnoredDelta = ignoredDelta;
                After = after;
                Physics = physics;
            }

            public CharacterTestGenesisPotencyTriplet AppliedDelta { get; }
            public CharacterTestGenesisPotencyTriplet IgnoredDelta { get; }
            public CharacterTestGenesisPotencyTriplet After { get; }
            public PotencyPhysicsProbe Physics { get; }
        }

        private readonly struct PotencyThresholdEvaluation
        {
            public PotencyThresholdEvaluation(
                bool burningThresholdMet,
                bool crushThresholdMet,
                bool crackThresholdMet,
                CharacterTestGenesisPotencyResultKind result)
            {
                BurningThresholdMet = burningThresholdMet;
                CrushThresholdMet = crushThresholdMet;
                CrackThresholdMet = crackThresholdMet;
                Result = result;
                ResultSource = result == CharacterTestGenesisPotencyResultKind.None
                    ? "threshold-not-met"
                    : "threshold";
            }

            public bool BurningThresholdMet { get; }
            public bool CrushThresholdMet { get; }
            public bool CrackThresholdMet { get; }
            public CharacterTestGenesisPotencyResultKind Result { get; }
            public string ResultSource { get; }
        }

        private readonly struct PotencyPhysicsProbe
        {
            public PotencyPhysicsProbe(
                int bodyCount,
                int colliderCount,
                int queryHitCount,
                int explainHitCount,
                int candidateCount,
                int postFilterCandidateCount,
                float contactSpeed,
                float contactDisplacement,
                bool hasContact)
            {
                BodyCount = bodyCount < 0 ? 0 : bodyCount;
                ColliderCount = colliderCount < 0 ? 0 : colliderCount;
                QueryHitCount = queryHitCount < 0 ? 0 : queryHitCount;
                ExplainHitCount = explainHitCount < 0 ? 0 : explainHitCount;
                CandidateCount = candidateCount < 0 ? 0 : candidateCount;
                PostFilterCandidateCount = postFilterCandidateCount < 0 ? 0 : postFilterCandidateCount;
                ContactSpeed = contactSpeed < 0f ? 0f : contactSpeed;
                ContactDisplacement = contactDisplacement < 0f ? 0f : contactDisplacement;
                HasContact = hasContact;
            }

            public int BodyCount { get; }
            public int ColliderCount { get; }
            public int QueryHitCount { get; }
            public int ExplainHitCount { get; }
            public int CandidateCount { get; }
            public int PostFilterCandidateCount { get; }
            public float ContactSpeed { get; }
            public float ContactDisplacement { get; }
            public bool HasContact { get; }
        }
    }
}
