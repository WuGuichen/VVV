using System;
using System.Collections.Generic;
using System.Globalization;

namespace MxFramework.Authoring
{
    public static partial class PhysicalAudioAuthoringManifest
    {
        private static PhysicalAudioFamilyAuthoring FindFamilyForCollision(PhysicalAudioAuthoringManifestDocument manifest, AudioCollisionProfileV1 profile) { string kind = profile?.Kind; string target = string.Equals(kind, "Footstep", StringComparison.OrdinalIgnoreCase) ? "Footstep" : string.Equals(kind, "Landing", StringComparison.OrdinalIgnoreCase) ? "Landing" : string.Equals(kind, "WeaponHit", StringComparison.OrdinalIgnoreCase) ? "WeaponHit" : "ObjectImpact"; return manifest.Families.Find(x => x != null && string.Equals(x.InteractionKind, target, StringComparison.Ordinal)); }
        private static void ApplyCollisionProfile(PhysicalAudioAuthoringManifestDocument manifest, PhysicalAudioBasisSoundAuthoring basis, AudioCollisionProfileV1 profile) { if (profile == null) return; ApplyCollisionBody(basis.Impactor, profile.Impactor); ApplyCollisionBody(basis.Receiver, profile.Receiver); basis.RelativeSpeed.Anchor = MapOrdinal(profile.Interaction?.Speed, "slow", "medium", "fast", "very_fast"); basis.Intensity.Anchor = MapOrdinal(profile.Interaction?.Intensity, "very_light", "light", "medium", "strong", "very_strong"); basis.AngleDegrees.Anchor = string.Equals(profile.Interaction?.Angle, "glancing", StringComparison.OrdinalIgnoreCase) ? 25f : string.Equals(profile.Interaction?.Angle, "oblique", StringComparison.OrdinalIgnoreCase) ? 60f : 90f; }
        private static void ApplyCollisionBody(PhysicalAudioBodyAnchorAuthoring target, AudioCollisionBodyV1 source) { if (target == null || source == null) return; target.MaterialId = MapMaterialId(source.MaterialId); target.Mass.Anchor = MapOrdinal(source.MassClass, "very_light", "light", "medium", "heavy", "very_heavy"); target.ContactArea.Anchor = MapOrdinal(source.ContactArea, "point", "small", "medium", "large", "surface"); target.Surface = MapSurface(source.Surface); target.Construction = MapConstruction(source.Construction); }
        private static string MapMaterialId(string value) { string id = (value ?? "auto").Replace('-', '_').ToLowerInvariant(); return id == "auto" ? "audio.material.generic_solid" : "audio.material." + id; }
        private static string MapSurface(string value) { switch ((value ?? string.Empty).ToLowerInvariant()) { case "polished": return "Polished"; case "smooth": return "Smooth"; case "rough": return "Rough"; case "granular": return "Granular"; default: return "Normal"; } }
        private static string MapConstruction(string value) { switch ((value ?? string.Empty).Replace("_", string.Empty).ToLowerInvariant()) { case "hollow": return "Hollow"; case "thinshell": return "ThinShell"; case "softfilled": return "SoftFilled"; case "gas": return "Gas"; default: return "Solid"; } }
        private static float MapOrdinal(string value, params string[] values) { for (int i = 0; i < values.Length; i++) if (string.Equals(value, values[i], StringComparison.OrdinalIgnoreCase)) return values.Length == 1 ? 0.5f : i / (float)(values.Length - 1); return 0.5f; }
        private static string SanitizeId(string value) { if (string.IsNullOrWhiteSpace(value)) return "new"; var chars = value.Trim().ToLowerInvariant().ToCharArray(); for (int i = 0; i < chars.Length; i++) if (!char.IsLetterOrDigit(chars[i]) && chars[i] != '.' && chars[i] != '_' && chars[i] != '-') chars[i] = '-'; return new string(chars); }

        private sealed class AuthoringResolvedCandidate
        {
            public AuthoringResolvedCandidate(PhysicalAudioFamilyAuthoring family, PhysicalAudioBasisSoundAuthoring basis, float distance, bool usedFallback)
            {
                Family = family;
                Basis = basis;
                Distance = distance;
                UsedFallback = usedFallback;
            }

            public PhysicalAudioFamilyAuthoring Family { get; }
            public PhysicalAudioBasisSoundAuthoring Basis { get; }
            public float Distance { get; }
            public bool UsedFallback { get; }
        }
    }
}
