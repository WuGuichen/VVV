using System;
using System.Collections.Generic;

namespace MxFramework.Authoring
{
    /// <summary>Creates review-needed semantic entries for uncovered direct VFX resources.</summary>
    public static class VfxAuthoringCoverageImporter
    {
        /// <summary>Returns a normalized clone with deterministic inferred Effect entries.</summary>
        /// <remarks>LowFreqAlloc. Does not write the source catalog or approve artistic quality.</remarks>
        public static VfxAuthoringCatalogDocument CreateProposal(VfxAuthoringProjectScanResult scan, bool enableStrictCoverage)
        {
            if (scan == null || scan.Catalog == null)
                throw new ArgumentException("A successful VFX project scan is required.", nameof(scan));
            VfxAuthoringCatalogLoadResult clone = VfxAuthoringCatalogJson.Load(VfxAuthoringCatalogJson.Serialize(scan.Catalog));
            if (!clone.Success || clone.Catalog == null)
                throw new InvalidOperationException("VFX Catalog could not be cloned for coverage proposal.");
            VfxAuthoringCatalogDocument proposal = clone.Catalog;
            var covered = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < proposal.Effects.Count; i++)
                if (proposal.Effects[i] != null && proposal.Effects[i].ResourceKey != null)
                    covered.Add(proposal.Effects[i].ResourceKey.ToStableKey());
            var missing = new List<string>();
            foreach (string key in scan.Evidence.DirectVfxResourceKeys)
                if (!covered.Contains(key))
                    missing.Add(key);
            missing.Sort(StringComparer.Ordinal);
            for (int i = 0; i < missing.Count; i++)
                proposal.Effects.Add(CreateEffect(missing[i], scan));
            proposal.Effects.Sort((left, right) => string.Compare(left?.EffectId, right?.EffectId, StringComparison.Ordinal));
            proposal.StrictCoverage = enableStrictCoverage;
            VfxAuthoringCatalogNormalizer.NormalizeInPlace(proposal);
            return proposal;
        }

        private static VfxAuthoringEffect CreateEffect(string stableKey, VfxAuthoringProjectScanResult scan)
        {
            string[] parts = stableKey.Split('|');
            if (parts.Length != 4)
                throw new InvalidOperationException("Invalid VFX ResourceKey evidence: " + stableKey);
            string packageId = parts[0];
            string id = parts[1];
            string typeId = parts[2];
            string variant = parts[3];
            string role = InferRole(id);
            string anchor = InferAnchor(role);
            bool persistent = string.Equals(role, VfxAuthoringRoles.Flight, StringComparison.Ordinal) ||
                              string.Equals(role, VfxAuthoringRoles.Aura, StringComparison.Ordinal) ||
                              string.Equals(role, VfxAuthoringRoles.Status, StringComparison.Ordinal) ||
                              string.Equals(role, VfxAuthoringRoles.Beam, StringComparison.Ordinal) ||
                              string.Equals(role, VfxAuthoringRoles.Zone, StringComparison.Ordinal);
            scan.Evidence.DefinitionIdsByResourceKey.TryGetValue(stableKey, out int definitionId);
            scan.Evidence.DefinitionLifetimesByResourceKey.TryGetValue(stableKey, out int lifetime);
            if (!persistent && lifetime <= 0)
                lifetime = 30;
            var effect = new VfxAuthoringEffect
            {
                EffectId = id,
                DisplayName = ToDisplayName(id),
                Description = "Auto-imported semantic coverage candidate for existing VFX resource; requires human artistic review.",
                ResourceKey = new VfxAuthoringResourceKey { Id = id, TypeId = typeId, Variant = variant, PackageId = packageId },
                DefinitionId = definitionId,
                RecipeId = string.Empty,
                Backend = InferBackend(stableKey, scan),
                AnchorContract = new VfxAuthoringAnchorContract { Anchor = anchor, Space = InferSpace(anchor) },
                Lifecycle = new VfxAuthoringLifecycle
                {
                    Mode = persistent ? VfxAuthoringLifecycleModes.ManualStop : VfxAuthoringLifecycleModes.OneShot,
                    DefaultLifetimeFrames = persistent ? 0 : lifetime,
                    StopPolicy = persistent ? VfxAuthoringStopPolicies.Manual : VfxAuthoringStopPolicies.Expire
                },
                Quality = new VfxAuthoringQuality { CostTier = VfxAuthoringCostTiers.Medium, FallbackPolicy = VfxAuthoringFallbackPolicies.None },
                Budget = new VfxAuthoringBudget { PrewarmCount = 1, RecommendedMaxConcurrent = 8, Priority = InferPriority(role), CullDistanceMeters = 45f },
                Preview = new VfxAuthoringPreview { AnchorPreset = anchor, TargetDistanceMeters = 8f, DurationSeconds = persistent ? 3f : 1.5f, CameraScaleMeters = 8f },
                ReviewStatus = VfxAuthoringReviewStatuses.ReviewNeeded
            };
            effect.Roles.Add(role);
            if (id.IndexOf("status_aura", StringComparison.Ordinal) >= 0 && !string.Equals(role, VfxAuthoringRoles.Aura, StringComparison.Ordinal))
                effect.Roles.Add(VfxAuthoringRoles.Aura);
            effect.ThemeIds.Add(InferTheme(id));
            effect.Tags.Add("coverage-import");
            effect.Tags.Add(role.ToLowerInvariant());
            return effect;
        }

        private static string InferRole(string id)
        {
            if (Contains(id, "telegraph")) return VfxAuthoringRoles.Telegraph;
            if (Contains(id, ".charge")) return VfxAuthoringRoles.Charge;
            if (Contains(id, ".flight") || Contains(id, ".deploy")) return VfxAuthoringRoles.Flight;
            if (Contains(id, ".impact") || Contains(id, ".shatter") || Contains(id, ".expire") || Contains(id, ".release")) return VfxAuthoringRoles.Impact;
            if (Contains(id, ".beam") || Contains(id, ".laser") || Contains(id, ".arc")) return VfxAuthoringRoles.Beam;
            if (Contains(id, ".zone") || Contains(id, ".pool")) return VfxAuthoringRoles.Zone;
            if (Contains(id, "status_aura")) return VfxAuthoringRoles.Aura;
            if (Contains(id, ".tick") || Contains(id, ".idle") || Contains(id, ".fuse") || Contains(id, ".trigger")) return VfxAuthoringRoles.Status;
            return VfxAuthoringRoles.Environment;
        }

        private static string InferAnchor(string role)
        {
            if (string.Equals(role, VfxAuthoringRoles.Beam, StringComparison.Ordinal)) return VfxAuthoringAnchors.Path;
            if (string.Equals(role, VfxAuthoringRoles.Zone, StringComparison.Ordinal) || string.Equals(role, VfxAuthoringRoles.Telegraph, StringComparison.Ordinal)) return VfxAuthoringAnchors.Ground;
            if (string.Equals(role, VfxAuthoringRoles.Impact, StringComparison.Ordinal)) return VfxAuthoringAnchors.HitPoint;
            if (string.Equals(role, VfxAuthoringRoles.Charge, StringComparison.Ordinal)) return VfxAuthoringAnchors.Caster;
            if (string.Equals(role, VfxAuthoringRoles.Aura, StringComparison.Ordinal) || string.Equals(role, VfxAuthoringRoles.Status, StringComparison.Ordinal)) return VfxAuthoringAnchors.Target;
            return VfxAuthoringAnchors.World;
        }

        private static string InferSpace(string anchor)
        {
            return string.Equals(anchor, VfxAuthoringAnchors.Caster, StringComparison.Ordinal) || string.Equals(anchor, VfxAuthoringAnchors.Target, StringComparison.Ordinal)
                ? VfxAuthoringSpaces.Local
                : VfxAuthoringSpaces.World;
        }

        private static string InferPriority(string role)
        {
            if (string.Equals(role, VfxAuthoringRoles.Telegraph, StringComparison.Ordinal)) return VfxAuthoringPriorities.CriticalTelegraph;
            if (string.Equals(role, VfxAuthoringRoles.Impact, StringComparison.Ordinal) || string.Equals(role, VfxAuthoringRoles.Beam, StringComparison.Ordinal) || string.Equals(role, VfxAuthoringRoles.Zone, StringComparison.Ordinal)) return VfxAuthoringPriorities.GameplayImportant;
            return VfxAuthoringPriorities.Normal;
        }

        private static string InferBackend(string stableKey, VfxAuthoringProjectScanResult scan)
        {
            if (!scan.Evidence.ResourceAssetPaths.TryGetValue(stableKey, out string path))
                return VfxAuthoringBackends.ParticleSystem;
            for (int i = 0; i < scan.Report.PrefabFingerprints.Count; i++)
            {
                VfxAuthoringPrefabFingerprint fingerprint = scan.Report.PrefabFingerprints[i];
                if (!string.Equals(fingerprint.AssetPath, path, StringComparison.Ordinal))
                    continue;
                if (fingerprint.VisualEffectCount > 0) return VfxAuthoringBackends.VfxGraph;
                if (fingerprint.LineRendererCount > 0) return VfxAuthoringBackends.LineRenderer;
                if (fingerprint.ParticleSystemCount == 0 && fingerprint.CustomBehaviourCount > 0) return VfxAuthoringBackends.CustomView;
                break;
            }
            return VfxAuthoringBackends.ParticleSystem;
        }

        private static string InferTheme(string id)
        {
            if (Contains(id, "poison")) return "poison";
            if (Contains(id, "fire") || Contains(id, "flame")) return "fire";
            if (Contains(id, "lightning") || Contains(id, "arc")) return "lightning";
            if (Contains(id, "tornado")) return "wind";
            if (Contains(id, "shield")) return "physical";
            if (Contains(id, "laser") || Contains(id, "beam") || Contains(id, "rune")) return "arcane";
            return "generic";
        }

        private static string ToDisplayName(string id)
        {
            int dot = id.LastIndexOf('.');
            string value = (dot >= 0 ? id.Substring(dot + 1) : id).Replace('_', ' ');
            return value.Length > 0 ? char.ToUpperInvariant(value[0]) + value.Substring(1) : "VFX";
        }

        private static bool Contains(string value, string part) => value.IndexOf(part, StringComparison.Ordinal) >= 0;
    }
}
