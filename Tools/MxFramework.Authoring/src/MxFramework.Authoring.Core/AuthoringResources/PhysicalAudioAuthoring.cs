using System;
using System.Collections.Generic;
using System.Globalization;

namespace MxFramework.Authoring
{
    public static partial class PhysicalAudioAuthoringManifest
    {
        public const int MaxResponseParameterCount = 5;
        private const int NativeVolumeParameterId = 500251;
        private const int NativePitchParameterId = 500252;
        private const float SelectionTieEpsilon = 0.000001f;
        private static readonly HashSet<string> Lifecycles = new HashSet<string>(new[] { PhysicalAudioLifecycles.Draft, PhysicalAudioLifecycles.Active, PhysicalAudioLifecycles.Archived }, StringComparer.Ordinal);
        private static readonly HashSet<string> InteractionKinds = new HashSet<string>(new[] { "Footstep", "Landing", "ObjectImpact", "WeaponHit", "WeaponBlock", "AirDisplacement", "Scrape", "Roll", "Compression" }, StringComparer.Ordinal);
        private static readonly HashSet<string> InteractionModes = new HashSet<string>(new[] { "Any", "Step", "Landing", "Impact", "Hit", "Block", "Sweep", "Scrape", "Roll", "Compression" }, StringComparer.Ordinal);
        private static readonly HashSet<string> VariantKinds = new HashSet<string>(new[] { "Impact", "Step", "Landing", "Hit", "Block", "Sweep", "Scrape", "Roll", "Compression" }, StringComparer.Ordinal);
        private static readonly HashSet<string> InteractionPhases = new HashSet<string>(new[] { "Started", "Stayed", "Ended" }, StringComparer.Ordinal);
        private static readonly HashSet<string> Surfaces = new HashSet<string>(new[] { "Any", "Polished", "Smooth", "Normal", "Rough", "Granular" }, StringComparer.Ordinal);
        private static readonly HashSet<string> Constructions = new HashSet<string>(new[] { "Any", "Solid", "Hollow", "ThinShell", "SoftFilled", "Gas" }, StringComparer.Ordinal);
        private static readonly HashSet<string> ResponseInputs = new HashSet<string>(new[] { "EffectiveEnergy", "RelativeSpeed", "NormalSpeed", "TangentialRatio", "Pressure", "Impulse", "Intensity", "AngleDegrees", "ImpactorHardness", "ReceiverHardness", "CombinedDamping" }, StringComparer.Ordinal);

        /// <remarks>LowFreqAlloc. Authoring load/save normalization.</remarks>
        public static PhysicalAudioAuthoringManifestDocument Upgrade(PhysicalAudioAuthoringManifestDocument manifest)
        {
            manifest ??= new PhysicalAudioAuthoringManifestDocument();
            bool legacy = string.Equals(manifest.Format, PhysicalAudioAuthoringFormats.LegacyAuthoring, StringComparison.Ordinal)
                || manifest.SchemaVersion <= 1;
            manifest.Format = PhysicalAudioAuthoringFormats.Authoring;
            manifest.SchemaVersion = 2;
            manifest.IdAllocation ??= new PhysicalAudioIdAllocationState();
            NormalizeIdAllocation(manifest.IdAllocation);
            manifest.MaterialProfiles ??= new List<PhysicalAudioMaterialProfileAuthoring>();
            manifest.MaterialPairs ??= new List<PhysicalAudioMaterialPairAuthoring>();
            manifest.Families ??= new List<PhysicalAudioFamilyAuthoring>();
            manifest.BasisSounds ??= new List<PhysicalAudioBasisSoundAuthoring>();
            manifest.ResponseProfiles ??= new List<PhysicalAudioResponseProfileAuthoring>();
            manifest.ScenarioPresets ??= new List<PhysicalAudioScenarioPresetAuthoring>();
            manifest.CoverageRequirements ??= new List<PhysicalAudioCoverageRequirementAuthoring>();
            manifest.Metadata ??= new Dictionary<string, string>();
            NormalizeMaterials(manifest);
            NormalizeFamilies(manifest);
            NormalizeBasisSounds(manifest);
            MigratePairsAndVariants(manifest, legacy);
            NormalizeMaterialPairs(manifest);
            NormalizeBasisSounds(manifest);
            NormalizeResponses(manifest);
            NormalizeScenariosAndCoverage(manifest);
            return manifest;
        }

        /// <remarks>LowFreqAlloc. Save-time migration records removed runtime ids so later allocation cannot reuse them.</remarks>
        public static void RetireRemovedIds(
            PhysicalAudioAuthoringManifestDocument previous,
            PhysicalAudioAuthoringManifestDocument current)
        {
            if (current == null)
                return;
            previous = Upgrade(previous);
            current = Upgrade(current);
            RetireMissing(previous.MaterialProfiles, current.MaterialProfiles, current.IdAllocation.RetiredMaterialIds, item => item.RuntimeMaterialId);
            RetireMissing(previous.MaterialPairs, current.MaterialPairs, current.IdAllocation.RetiredMaterialPairIds, item => item.RuntimeMaterialPairId);
            RetireMissing(previous.Families, current.Families, current.IdAllocation.RetiredFamilyIds, item => item.RuntimeFamilyId);
            RetireMissing(previous.BasisSounds, current.BasisSounds, current.IdAllocation.RetiredBasisIds, item => item.RuntimeBasisId);
            RetireMissing(previous.ResponseProfiles, current.ResponseProfiles, current.IdAllocation.RetiredResponseProfileIds, item => item.RuntimeResponseProfileId);
            NormalizeIdAllocation(current.IdAllocation);
        }

        /// <remarks>LowFreqAlloc. Produces diagnostics and lookup sets.</remarks>
    }
}
