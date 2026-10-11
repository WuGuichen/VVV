using System;

namespace MxFramework.CharacterAction
{
    /// <summary>Configures the optional actions selected when a character lands.</summary>
    public sealed class CharacterLandingProfile
    {
        public const int DefaultLightLandingMinDownwardSpeedMillimetersPerSecond = 1000;
        public const int DefaultHeavyLandingMinDownwardSpeedMillimetersPerSecond = 8000;

        /// <remarks>AllocByDesign: constructs authoring/configuration state outside runtime ticks.</remarks>
        public CharacterLandingProfile(
            string stableId,
            CharacterActionId lightLandingActionId = default,
            CharacterActionId heavyLandingActionId = default,
            int heavyLandingMinDownwardSpeedMillimetersPerSecond =
                DefaultHeavyLandingMinDownwardSpeedMillimetersPerSecond,
            int lightLandingMinDownwardSpeedMillimetersPerSecond =
                DefaultLightLandingMinDownwardSpeedMillimetersPerSecond)
        {
            if (lightLandingMinDownwardSpeedMillimetersPerSecond <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(lightLandingMinDownwardSpeedMillimetersPerSecond),
                    "Light landing speed threshold must be greater than zero.");
            }
            if (heavyLandingMinDownwardSpeedMillimetersPerSecond < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(heavyLandingMinDownwardSpeedMillimetersPerSecond),
                    "Heavy landing speed threshold cannot be negative.");
            }
            if (heavyLandingMinDownwardSpeedMillimetersPerSecond
                < lightLandingMinDownwardSpeedMillimetersPerSecond)
            {
                throw new ArgumentException(
                    "Heavy landing speed threshold cannot be lower than the light landing threshold.",
                    nameof(heavyLandingMinDownwardSpeedMillimetersPerSecond));
            }

            StableId = stableId ?? string.Empty;
            LightLandingActionId = lightLandingActionId;
            HeavyLandingActionId = heavyLandingActionId;
            LightLandingMinDownwardSpeedMillimetersPerSecond =
                lightLandingMinDownwardSpeedMillimetersPerSecond;
            HeavyLandingMinDownwardSpeedMillimetersPerSecond =
                heavyLandingMinDownwardSpeedMillimetersPerSecond;
        }

        public string StableId { get; }
        public CharacterActionId LightLandingActionId { get; }
        public CharacterActionId HeavyLandingActionId { get; }
        public int LightLandingMinDownwardSpeedMillimetersPerSecond { get; }
        public int HeavyLandingMinDownwardSpeedMillimetersPerSecond { get; }

        /// <remarks>NoAlloc.</remarks>
        public bool MeetsLightLandingThreshold(int downwardSpeedMillimetersPerSecond)
        {
            return downwardSpeedMillimetersPerSecond
                >= LightLandingMinDownwardSpeedMillimetersPerSecond;
        }

        /// <remarks>NoAlloc.</remarks>
        public bool IsHeavyLanding(int downwardSpeedMillimetersPerSecond)
        {
            return downwardSpeedMillimetersPerSecond
                >= HeavyLandingMinDownwardSpeedMillimetersPerSecond;
        }

        /// <remarks>NoAlloc.</remarks>
        public bool TryResolveAction(
            bool heavy,
            out CharacterActionId actionId,
            out bool resolvedHeavy,
            out bool usedFallback)
        {
            CharacterActionId preferred = heavy ? HeavyLandingActionId : LightLandingActionId;
            if (preferred.IsValid)
            {
                actionId = preferred;
                resolvedHeavy = heavy;
                usedFallback = false;
                return true;
            }

            CharacterActionId fallback = heavy ? LightLandingActionId : HeavyLandingActionId;
            if (fallback.IsValid)
            {
                actionId = fallback;
                resolvedHeavy = !heavy;
                usedFallback = true;
                return true;
            }

            actionId = CharacterActionId.None;
            resolvedHeavy = false;
            usedFallback = false;
            return false;
        }
    }
}
