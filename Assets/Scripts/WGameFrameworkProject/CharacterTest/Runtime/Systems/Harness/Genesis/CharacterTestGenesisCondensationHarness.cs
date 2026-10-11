using System;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    internal static class CharacterTestGenesisCondensationHarness
    {
        internal const int DefaultTickRate = 60;
        internal const int DefaultContactTicks = 60;
        internal const float DefaultInitialContactSpeed = 6f;
        internal const string SurfaceSampleStableId = "cool_slick_surface";
        internal const string CondensationInfluenceStableId = "cool_mist_deposit";
        internal const string LayerThreshold = "P>=40&&D<=4";
        internal const string LayerSource = "condensation";

        private const int SampleNumericId = 631101;
        private const float FrictionToDecelerationScale = 0.04f;

        internal static CharacterTestGenesisCondensationValidation RunValidationSuite(
            RuntimeFrame frame = default)
        {
            CharacterTestGenesisCondensationSample sample = RunSample(frame);
            return new CharacterTestGenesisCondensationValidation(
                CharacterTestGenesisCondensationProbeKind.ValidationSuite,
                frame,
                "harness-only",
                sample);
        }

        internal static CharacterTestGenesisCondensationSample RunSample(
            RuntimeFrame frame = default)
        {
            CharacterTestGenesisCondensationTriplet before =
                new CharacterTestGenesisCondensationTriplet(4, 18, 2);
            CharacterTestGenesisCondensationTriplet rawDelta =
                new CharacterTestGenesisCondensationTriplet(-2, 24, 0);
            CharacterTestGenesisCondensationTriplet appliedDelta = rawDelta;
            CharacterTestGenesisCondensationTriplet after = before + appliedDelta;
            bool thresholdMet = after.P >= 40 && after.D <= 4;
            CharacterTestGenesisCondensationResultKind result = thresholdMet
                ? CharacterTestGenesisCondensationResultKind.CondensedLayer
                : CharacterTestGenesisCondensationResultKind.None;

            CombatSurfaceMaterial baseMaterial = CombatSurfaceMaterial.HardSlick;
            float frictionBefore = ToFloat(baseMaterial.Friction);
            float layerFriction = thresholdMet ? after.P : 0f;
            float frictionAfter = thresholdMet ? Math.Max(frictionBefore, layerFriction) : frictionBefore;

            CharacterTestGenesisCondensationContactSample contactBefore = RunContactSample(
                frictionBefore,
                DefaultInitialContactSpeed,
                DefaultContactTicks,
                DefaultTickRate);
            CharacterTestGenesisCondensationContactSample contactAfter = RunContactSample(
                frictionAfter,
                DefaultInitialContactSpeed,
                DefaultContactTicks,
                DefaultTickRate);

            return new CharacterTestGenesisCondensationSample(
                SurfaceSampleStableId,
                SampleNumericId,
                baseMaterial.Kind,
                baseMaterial.StableId,
                CondensationInfluenceStableId,
                frame,
                "harness-only",
                before,
                rawDelta,
                appliedDelta,
                after,
                LayerThreshold,
                thresholdMet,
                result,
                thresholdMet ? LayerSource : "threshold-not-met",
                materialReplacement: false,
                sameContactInput: true,
                frictionBefore,
                layerFriction,
                frictionAfter,
                contactBefore,
                contactAfter);
        }

        private static CharacterTestGenesisCondensationContactSample RunContactSample(
            float friction,
            float initialSpeed,
            int contactTicks,
            int tickRate)
        {
            int safeContactTicks = Math.Max(1, contactTicks);
            int safeTickRate = Math.Max(1, tickRate);
            float safeInitialSpeed = initialSpeed < 0f ? 0f : initialSpeed;
            float safeFriction = friction < 0f ? 0f : friction;
            float durationSeconds = safeContactTicks / (float)safeTickRate;
            float velocityDecay = Math.Min(
                safeInitialSpeed,
                safeFriction * FrictionToDecelerationScale * durationSeconds);
            float finalSpeed = safeInitialSpeed - velocityDecay;
            float slideDistance = (safeInitialSpeed + finalSpeed) * 0.5f * durationSeconds;
            return new CharacterTestGenesisCondensationContactSample(
                safeFriction,
                safeContactTicks,
                safeTickRate,
                safeInitialSpeed,
                finalSpeed,
                velocityDecay,
                slideDistance);
        }

        private static float ToFloat(Fix64 value)
        {
            return (float)((double)value.RawValue / Fix64.Scale);
        }
    }
}
