using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using MxFramework.Authoring;

namespace MxFramework.Authoring.Tests;

internal static partial class CharacterPackageTests
{
    private static void PhysicalAudioAuthoring_CompilesSeedAndMatchesRuntimeSelectionSemantics()
    {
        string root = FindRepoRoot();
        PhysicalAudioAuthoringManifestDocument seed = JsonSerializer.Deserialize<PhysicalAudioAuthoringManifestDocument>(
            File.ReadAllText(Path.Combine(root, "Assets", "Config", "MxFramework", "Audio", "audio_physical_manifest.json")),
            JsonOptions);
        AudioSemanticManifestDocument semantic = JsonSerializer.Deserialize<AudioSemanticManifestDocument>(
            File.ReadAllText(Path.Combine(root, "Assets", "Config", "MxFramework", "Audio", "audio_semantic_manifest.json")),
            JsonOptions);
        seed = PhysicalAudioAuthoringManifest.Upgrade(seed);
        PhysicalAudioCoverageReport coverage = PhysicalAudioAuthoringManifest.AnalyzeCoverage(seed);
        Require(seed.Format == "mx.audioPhysicalAuthoringManifest.v2"
                && seed.MaterialProfiles.Count == 14 && seed.MaterialPairs.Count == 90
                && seed.Families.Count == 6 && seed.BasisSounds.Count == 104
                && seed.BasisSounds.FindAll(item => item.Lifecycle == PhysicalAudioLifecycles.Draft).Count == 86,
            "checked-in physical audio manifest should include the complete 104-Basis production inventory while unapproved generated Basis remain Draft.");
        Require(seed.MaterialPairs.TrueForAll(item =>
                    seed.MaterialProfiles.Find(material => material.Id == item.MaterialAId).RuntimeMaterialId
                    <= seed.MaterialProfiles.Find(material => material.Id == item.MaterialBId).RuntimeMaterialId)
                && seed.BasisSounds.TrueForAll(item => !string.IsNullOrWhiteSpace(item.MaterialPairId)
                    && !string.IsNullOrWhiteSpace(item.VariantKind)),
            "v1 migration must create canonical unordered Pair identities and Pair Variant Basis references.");
        Require(seed.Families.Find(item => item.Id == "audio.family.footstep")?.VariantKind == "Impact"
                && seed.Families.Find(item => item.Id == "audio.family.landing")?.VariantKind == "Landing"
                && seed.Families.Find(item => item.Id == "audio.family.object_impact")?.VariantKind == "Impact"
                && seed.Families.Find(item => item.Id == "audio.family.weapon_hit")?.VariantKind == "Hit"
                && seed.Families.Find(item => item.Id == "audio.family.weapon_block")?.VariantKind == "Block"
                && seed.Families.Find(item => item.Id == "audio.family.air_displacement")?.VariantKind == "Sweep"
                && seed.CoverageRequirements.TrueForAll(requirement =>
                    requirement.VariantKinds.Count == 1
                    && requirement.VariantKinds[0] == seed.Families.Find(family => family.Id == requirement.FamilyId)?.VariantKind),
            "v1 migration must derive each public Pair variant from its legacy family interaction mode.");
        PhysicalAudioAuthoringManifestDocument legacy = ClonePhysicalManifest(seed);
        legacy.Format = PhysicalAudioAuthoringFormats.LegacyAuthoring;
        legacy.SchemaVersion = 1;
        legacy.MaterialPairs.Clear();
        legacy.BasisSounds.RemoveAll(item => item.Lifecycle != PhysicalAudioLifecycles.Active);
        legacy.IdAllocation.NextMaterialPairId = 550001;
        foreach (PhysicalAudioFamilyAuthoring family in legacy.Families)
            family.VariantKind = "Impact"; // Matches the DTO value seen when v1 omits the property.
        foreach (PhysicalAudioBasisSoundAuthoring basis in legacy.BasisSounds)
        {
            basis.MaterialPairId = string.Empty;
            basis.VariantKind = string.Empty;
            basis.SideA = null;
            basis.SideB = null;
            if (basis.Id.StartsWith("audio.basis.footstep.", StringComparison.Ordinal))
                basis.FamilyId = "audio.family.footstep";
        }
        foreach (PhysicalAudioCoverageRequirementAuthoring requirement in legacy.CoverageRequirements)
        {
            requirement.MaterialPairIds.Clear();
            requirement.VariantKinds.Clear();
        }
        legacy = PhysicalAudioAuthoringManifest.Upgrade(legacy);
        PhysicalAudioCoverageReport migratedCoverage = PhysicalAudioAuthoringManifest.AnalyzeCoverage(legacy);
        Require(legacy.MaterialPairs.Count == 14
                && legacy.Families.Find(item => item.Id == "audio.family.footstep")?.VariantKind == "Step"
                && legacy.Families.Find(item => item.Id == "audio.family.landing")?.VariantKind == "Landing"
                && legacy.Families.Find(item => item.Id == "audio.family.weapon_hit")?.VariantKind == "Hit"
                && legacy.Families.Find(item => item.Id == "audio.family.weapon_block")?.VariantKind == "Block"
                && legacy.Families.Find(item => item.Id == "audio.family.air_displacement")?.VariantKind == "Sweep"
                && migratedCoverage.RequiredCellCount == 18
                && migratedCoverage.CoveredCellCount == 18,
            "v1 migration must reconstruct all canonical Pair identities and 18 Pair Variant requirements without losing coverage.");
        Require(coverage.RequiredCellCount == 18 && coverage.CoveredCellCount == 18
            && coverage.MissingCellCount == 0 && coverage.AmbiguousCellCount == 0,
            "checked-in physical audio coverage requirements should have one unambiguous Basis per required material pair.");

        PhysicalAudioRuntimeCatalogDocument compiled = PhysicalAudioAuthoringManifest.CompileRuntimeCatalog(
            seed, semantic, "sha256:physical", "sha256:audio");
        Require(compiled.Format == "mx.audioPhysicalRuntimeCatalog.v2" && compiled.SchemaVersion == 2,
            "physical runtime catalog format must match the strict startup loader contract.");
        Require(compiled.Materials.Count == 14 && compiled.MaterialPairs.Count == 90
            && compiled.Families.Count == 6 && compiled.BasisSounds.Count == 18
            && compiled.ResponseProfiles.Count == 2,
            "physical runtime catalog should compile stable ids, all existing Basis Sounds, and response curves.");
        Require(compiled.ResponseProfiles[0].Curves.Count == 2
                && compiled.ResponseProfiles[0].Curves[0].ParameterId == 500251
                && compiled.ResponseProfiles[0].Curves[0].Input == 6
                && compiled.ResponseProfiles[0].Curves[1].ParameterId == 500252
                && compiled.ResponseProfiles[0].Curves[1].Input == 1,
            "the seed catalog should emit only backend-native volume and pitch responses until FMOD automation evidence is available.");
        PhysicalAudioRuntimeFamily objectImpactFamily = compiled.Families.Find(item => item.RuntimeFamilyId == 520003);
        PhysicalAudioRuntimeBasisSound objectImpactBasis = compiled.BasisSounds.Find(item => item.VariantKind == 1 && item.FamilyRuntimeId == 520003);
        Require(objectImpactFamily != null
                && objectImpactFamily.InteractionKind == 3
                && objectImpactFamily.InteractionMode == 3
                && objectImpactBasis != null
                && objectImpactBasis.Impactor.Surface >= 0
                && objectImpactBasis.Impactor.Construction >= 0,
            "physical runtime catalog must serialize interaction, surface, construction, and response enums as stable integers.");
        string compiledJson = JsonSerializer.Serialize(compiled, JsonOptions);
        Require(!compiledJson.Contains("ObjectImpact", StringComparison.Ordinal)
                && !compiledJson.Contains("\"Normal\"", StringComparison.Ordinal)
                && !compiledJson.Contains("\"Solid\"", StringComparison.Ordinal)
                && !compiledJson.Contains("\"Intensity\"", StringComparison.Ordinal),
            "physical runtime JSON must not carry Authoring enum names into the numeric startup contract.");
        Require(compiled.BasisSounds.Exists(item => item.RuntimeBasisId == 530101 && item.RuntimeCueId == 500304),
            "AirDisplacement should compile its independent stable Cue instead of reusing the Light Attack hit identity.");
        AudioCueAuthoringDefinition airCue = AudioAuthoringManifestV2.FindCue(semantic, "sfx.air_displacement.light_attack");
        AudioCueAuthoringDefinition hitCue = AudioAuthoringManifestV2.FindCue(semantic, "sfx.light_attack");
        AudioEventAuthoringDefinition airEvent = AudioAuthoringManifestV2.FindEventDefinition(semantic, airCue?.EventDefinitionId);
        Require(airCue?.RuntimeCueId == 500304
                && airEvent?.RuntimeEventId == 500314
                && airCue.EventDefinitionId != hitCue?.EventDefinitionId
                && airEvent.FmodEventPath == "event:/MxFramework/Generated/Physical/audio.family.air_displacement/audio.basis.air_displacement.light_attack",
            "AirDisplacement Basis, Cue, and managed Event must remain independent from LightAttackHit.");

        AuthoringFmodAudioLibrarySnapshotDocument snapshot = JsonSerializer.Deserialize<AuthoringFmodAudioLibrarySnapshotDocument>(
            File.ReadAllText(Path.Combine(root, "Assets", "MxFrameworkGenerated", "Audio", "fmod_audio_library.json")),
            JsonOptions);
        PhysicalAudioValidationReport snapshotValidation = PhysicalAudioAuthoringManifest.Validate(seed, semantic);
        PhysicalAudioAuthoringManifest.ValidateSnapshotParameterEvidence(seed, semantic, snapshot, snapshotValidation);
        Require(!snapshotValidation.Diagnostics.Exists(item => item != null
                && item.Code == "AUTH_AUDIO_PHYSICAL_REQUIRED_PARAMETER_MISSING"),
            "backend-native volume and pitch responses must not require semantic Event-local parameter declarations.");
        Require(!snapshotValidation.Diagnostics.Exists(item => item != null
                && item.Code == "AUTH_AUDIO_PHYSICAL_FMOD_PARAMETER_EVIDENCE_MISSING"),
            "backend-native volume and pitch responses must not require FMOD Event-local parameter evidence.");

        seed.Families.Find(item => item.Id == "audio.family.object_impact")!.RequiredParameterIds.Add(500253);
        snapshotValidation = PhysicalAudioAuthoringManifest.Validate(seed, semantic);
        PhysicalAudioAuthoringManifest.ValidateSnapshotParameterEvidence(seed, semantic, snapshot, snapshotValidation);
        Require(snapshotValidation.Diagnostics.Exists(item => item != null
                && item.Code == "AUTH_AUDIO_PHYSICAL_FMOD_PARAMETER_EVIDENCE_MISSING"),
            "a non-native modulation parameter declared by a family must remain blocked without FMOD Event-local evidence.");
        AddPhysicalParameterEvidence(seed, semantic, snapshot);
        snapshotValidation = PhysicalAudioAuthoringManifest.Validate(seed, semantic);
        PhysicalAudioAuthoringManifest.ValidateSnapshotParameterEvidence(seed, semantic, snapshot, snapshotValidation);
        Require(!snapshotValidation.Diagnostics.Exists(item => item != null
                && item.Code == "AUTH_AUDIO_PHYSICAL_FMOD_PARAMETER_EVIDENCE_MISSING"),
            "the physical parameter gate should accept exact Event-local parameter names exported by the FMOD snapshot.");

        var auditionValidation = new PhysicalAudioValidationReport();
        AudioCueAuthoringDefinition firstCue = AudioAuthoringManifestV2.FindCue(semantic, seed.BasisSounds[0].AudioCueId);
        string previousBasisId = firstCue?.AuditionEvidence?.AudioBasisId;
        if (firstCue?.AuditionEvidence != null)
            firstCue.AuditionEvidence.AudioBasisId = string.Empty;
        MxFramework.Authoring.Cli.EditorServer.AddPhysicalAudioAuditionDiagnostics(
            seed, semantic, auditionValidation);
        Require(auditionValidation.Diagnostics.Exists(item => item != null
                && item.Code == "AUTH_AUDIO_PHYSICAL_AUDITION_EVIDENCE_STALE"),
            "physical publication should reject legacy approval that is not bound to the current Basis, runtime id, and adoption set.");
        if (firstCue?.AuditionEvidence != null)
            firstCue.AuditionEvidence.AudioBasisId = previousBasisId;
        BindPhysicalAuditionEvidence(seed, semantic, "sha256:physical");
        for (int i = 0; i < semantic.Cues.Count; i++)
            if (semantic.Cues[i]?.AuditionEvidence != null)
                semantic.Cues[i].AuditionEvidence.PhysicalManifestHash = "sha256:unrelated-physical-change";
        auditionValidation = new PhysicalAudioValidationReport();
        MxFramework.Authoring.Cli.EditorServer.AddPhysicalAudioAuditionDiagnostics(
            seed, semantic, auditionValidation);
        Require(!auditionValidation.HasErrors,
            "an unrelated Physical Manifest change must not invalidate approval bound to the current Basis, runtime id, and adoption set.");
        PhysicalAudioBasisSoundAuthoring checkedBasis = seed.BasisSounds[0];
        AudioCueAuthoringDefinition checkedCue = AudioAuthoringManifestV2.FindCue(semantic, checkedBasis.AudioCueId);
        checkedCue.AuditionEvidence.AudioBasisId = "audio.basis.wrong";
        auditionValidation = new PhysicalAudioValidationReport();
        MxFramework.Authoring.Cli.EditorServer.AddPhysicalAudioAuditionDiagnostics(seed, semantic, auditionValidation);
        Require(auditionValidation.Diagnostics.Exists(item => item != null
                && item.Code == "AUTH_AUDIO_PHYSICAL_AUDITION_EVIDENCE_STALE"
                && item.ResourceId == checkedBasis.Id),
            "approval bound to a different Basis must be rejected.");
        checkedCue.AuditionEvidence.AudioBasisId = checkedBasis.Id;
        checkedCue.AuditionEvidence.AppliedSetId = "set.wrong";
        auditionValidation = new PhysicalAudioValidationReport();
        MxFramework.Authoring.Cli.EditorServer.AddPhysicalAudioAuditionDiagnostics(seed, semantic, auditionValidation);
        Require(auditionValidation.Diagnostics.Exists(item => item != null
                && item.Code == "AUTH_AUDIO_PHYSICAL_AUDITION_EVIDENCE_STALE"
                && item.ResourceId == checkedBasis.Id),
            "approval bound to a different adoption set must be rejected.");
        checkedCue.AuditionEvidence.AppliedSetId = checkedBasis.AdoptionSetId;
        AudioCueAuditionEvidence evidenceRoundTrip = JsonSerializer.Deserialize<AudioCueAuditionEvidence>(
            JsonSerializer.Serialize(semantic.Cues[0].AuditionEvidence, JsonOptions), JsonOptions);
        Require(evidenceRoundTrip.AudioBasisId == semantic.Cues[0].AuditionEvidence.AudioBasisId
                && evidenceRoundTrip.RuntimeBasisId == semantic.Cues[0].AuditionEvidence.RuntimeBasisId
                && evidenceRoundTrip.PhysicalManifestHash == "sha256:unrelated-physical-change",
            "Basis-bound audition evidence fields must survive Authoring manifest JSON persistence.");

        PhysicalAudioAuthoringManifestDocument fixture = BuildPhysicalResolverFixture();
        var fact = new PhysicalAudioInteractionFactAuthoring
        {
            InteractionKind = "ObjectImpact",
            InteractionMode = "Impact",
            Phase = "Started",
            Impactor = new PhysicalAudioBodyFactAuthoring { MaterialId = "material.a", Mass = 0.5f, ContactArea = 0.5f, Surface = "Normal", Construction = "Solid" },
            Receiver = new PhysicalAudioBodyFactAuthoring { MaterialId = "material.b", Mass = 0.5f, ContactArea = 0.5f, Surface = "Normal", Construction = "Solid" },
            RelativeSpeed = 6f,
            NormalSpeed = 4f,
            TangentialSpeed = 2f,
            Intensity = 0.6f,
            AngleDegrees = 90f
        };
        PhysicalAudioResolvedPlaybackAuthoring resolved = PhysicalAudioAuthoringManifest.Resolve(fixture, fact);
        Require(resolved.Success && resolved.BasisId == "basis.exact" && Math.Abs(resolved.Score) < 0.00001f,
            "Authoring resolver should reject mismatched material anchors and prefer the exact pair over wildcard anchors.");
        Require(resolved.Parameters.Count == 2
            && Math.Abs(resolved.Parameters[0].Value - 0.6f) < 0.0001f
            && Math.Abs(resolved.Parameters[1].Value - 1.1f) < 0.0001f,
            "Authoring response evaluation should match the runtime fixture for intensity and relative speed.");

        PhysicalAudioBasisSoundAuthoring tieBasis = JsonSerializer.Deserialize<PhysicalAudioBasisSoundAuthoring>(
            JsonSerializer.Serialize(fixture.BasisSounds.Find(item => item.Id == "basis.exact"), JsonOptions), JsonOptions);
        tieBasis.Id = "basis.exact.tie";
        tieBasis.RuntimeBasisId = 530000;
        tieBasis.AudioCueId = "cue.tie";
        fixture.BasisSounds.Add(tieBasis);
        resolved = PhysicalAudioAuthoringManifest.Resolve(fixture, fact);
        Require(resolved.BasisId == "basis.exact.tie",
            "equal priority and distance should deterministically choose the smaller runtime Basis id.");

        PhysicalAudioAuthoringManifestDocument retired = BuildPhysicalResolverFixture();
        PhysicalAudioAuthoringManifestDocument afterDelete = BuildPhysicalResolverFixture();
        afterDelete.BasisSounds.RemoveAt(0);
        PhysicalAudioAuthoringManifest.RetireRemovedIds(retired, afterDelete);
        Require(afterDelete.IdAllocation.RetiredBasisIds.Contains(530001)
                && afterDelete.IdAllocation.NextBasisId > 530003,
            "save-time migration should retire removed typed ids and keep future Basis allocation monotonic.");
    }

    private static void PhysicalAudioAuthoring_RejectsInvalidActiveGraphAndRuntimeDto()
    {
        string root = FindRepoRoot();
        PhysicalAudioAuthoringManifestDocument seed = JsonSerializer.Deserialize<PhysicalAudioAuthoringManifestDocument>(
            File.ReadAllText(Path.Combine(root, "Assets", "Config", "MxFramework", "Audio", "audio_physical_manifest.json")),
            JsonOptions);
        AudioSemanticManifestDocument semantic = JsonSerializer.Deserialize<AudioSemanticManifestDocument>(
            File.ReadAllText(Path.Combine(root, "Assets", "Config", "MxFramework", "Audio", "audio_semantic_manifest.json")),
            JsonOptions);
        seed = PhysicalAudioAuthoringManifest.Upgrade(seed);

        PhysicalAudioRuntimeCatalogDocument compiled = PhysicalAudioAuthoringManifest.CompileRuntimeCatalog(
            seed, semantic, "sha256:physical", "sha256:audio");
        PhysicalAudioRuntimeCatalogDocument roundTrip = JsonSerializer.Deserialize<PhysicalAudioRuntimeCatalogDocument>(
            JsonSerializer.Serialize(compiled, JsonOptions), JsonOptions);
        PhysicalAudioValidationReport runtimeReport = PhysicalAudioAuthoringManifest.ValidateRuntimeCatalog(roundTrip);
        Require(!runtimeReport.HasErrors,
            "compiled physical runtime DTO must survive JSON round-trip and satisfy the startup loader's strict structural invariants.");
        roundTrip.Families[0].FallbackFamilyRuntimeId = 529999;
        runtimeReport = PhysicalAudioAuthoringManifest.ValidateRuntimeCatalog(roundTrip);
        Require(runtimeReport.Diagnostics.Exists(item => item?.Code == "AUTH_AUDIO_PHYSICAL_RUNTIME_FALLBACK_FAMILY_MISSING"),
            "compiled runtime validation must reject a fallback reference that the strict startup loader cannot resolve.");

        PhysicalAudioAuthoringManifestDocument missingCoverage = ClonePhysicalManifest(seed);
        missingCoverage.CoverageRequirements.Clear();
        PhysicalAudioValidationReport missingCoverageReport = PhysicalAudioAuthoringManifest.Validate(missingCoverage, semantic);
        Require(!missingCoverageReport.Diagnostics.Exists(item => item?.Code == "AUTH_AUDIO_PHYSICAL_REQUIRED_COVERAGE_MISSING"),
            "an empty project Required set is allowed; optional material combinations resolve through explicit fallback.");
        PhysicalAudioAuthoringManifestDocument optionalCoverage = ClonePhysicalManifest(seed);
        optionalCoverage.CoverageRequirements.ForEach(item => item.Required = false);
        PhysicalAudioValidationReport optionalCoverageReport = PhysicalAudioAuthoringManifest.Validate(optionalCoverage, semantic);
        Require(!optionalCoverageReport.Diagnostics.Exists(item => item?.Code == "AUTH_AUDIO_PHYSICAL_REQUIRED_COVERAGE_MISSING"),
            "optional Pair Variants must not be promoted into blocking coverage implicitly.");
        PhysicalAudioAuthoringManifestDocument emptyCoverage = ClonePhysicalManifest(seed);
        PhysicalAudioCoverageRequirementAuthoring emptyRequirement = emptyCoverage.CoverageRequirements[0];
        emptyRequirement.MaterialPairIds.Clear();
        emptyRequirement.VariantKinds.Clear();
        emptyRequirement.ImpactorMaterialIds.Clear();
        emptyRequirement.ReceiverMaterialIds.Clear();
        emptyRequirement.Facts.Clear();
        emptyRequirement.ScenarioPresetIds.Clear();
        PhysicalAudioValidationReport emptyCoverageReport = PhysicalAudioAuthoringManifest.Validate(emptyCoverage, semantic);
        Require(emptyCoverageReport.Diagnostics.Exists(item => item?.Code == "AUTH_AUDIO_PHYSICAL_REQUIRED_COVERAGE_EMPTY"),
            "an empty Required coverage contract must fail validation instead of becoming a vacuous publication gate.");

        PhysicalAudioAuthoringManifestDocument invalid = ClonePhysicalManifest(seed);
        string activeBasisMaterialId = invalid.BasisSounds[0].Impactor.MaterialId;
        invalid.MaterialProfiles.Find(item => item.Id == activeBasisMaterialId).Lifecycle = PhysicalAudioLifecycles.Archived;
        invalid.ResponseProfiles.Add(new PhysicalAudioResponseProfileAuthoring
        {
            Id = "audio.response.archived",
            RuntimeResponseProfileId = 540002,
            Lifecycle = PhysicalAudioLifecycles.Archived,
            Curves = new List<PhysicalAudioResponseCurveAuthoring>()
        });
        invalid.Families[0].CooldownFrames = -1;
        invalid.Families[0].ResponseProfileId = "audio.response.archived";
        invalid.Families[0].Weights = new PhysicalAudioSelectionWeightsAuthoring
        {
            Material = 0f, Mass = 0f, ContactArea = 0f, Surface = 0f,
            Construction = 0f, Speed = 0f, Intensity = 0f, Angle = 0f
        };
        invalid.Families[1].Weights.Speed = float.NaN;
        PhysicalAudioFamilyAuthoring weaponHit = invalid.Families.Find(item => item.Id == "audio.family.weapon_hit");
        weaponHit.Lifecycle = PhysicalAudioLifecycles.Archived;
        PhysicalAudioFamilyAuthoring footstep = invalid.Families.Find(item => item.Id == "audio.family.footstep");
        PhysicalAudioBasisSoundAuthoring footstepFallback = invalid.BasisSounds.Find(item => item.Id == footstep.FallbackBasisId);
        footstepFallback.Lifecycle = PhysicalAudioLifecycles.Archived;
        while (invalid.ResponseProfiles[0].Curves.Count <= PhysicalAudioAuthoringManifest.MaxResponseParameterCount)
        {
            int index = invalid.ResponseProfiles[0].Curves.Count;
            invalid.ResponseProfiles[0].Curves.Add(new PhysicalAudioResponseCurveAuthoring
            {
                ParameterId = 500300 + index,
                Input = "Intensity",
                OutputMin = 0f,
                OutputMax = 1f,
                Points = new List<PhysicalAudioResponsePointAuthoring>
                {
                    new PhysicalAudioResponsePointAuthoring { Input = 0f, Output = 0f },
                    new PhysicalAudioResponsePointAuthoring { Input = 1f, Output = 1f }
                }
            });
        }

        PhysicalAudioValidationReport report = PhysicalAudioAuthoringManifest.Validate(invalid, semantic);
        Require(report.Diagnostics.Exists(item => item?.Code == "AUTH_AUDIO_PHYSICAL_COOLDOWN_INVALID")
                && report.Diagnostics.Exists(item => item?.Code == "AUTH_AUDIO_PHYSICAL_WEIGHTS_TOTAL_INVALID")
                && report.Diagnostics.Exists(item => item?.Code == "AUTH_AUDIO_PHYSICAL_WEIGHTS_INVALID")
                && report.Diagnostics.Exists(item => item?.Code == "AUTH_AUDIO_PHYSICAL_RESPONSE_INACTIVE")
                && report.Diagnostics.Exists(item => item?.Code == "AUTH_AUDIO_PHYSICAL_FAMILY_INACTIVE")
                && report.Diagnostics.Exists(item => item?.Code == "AUTH_AUDIO_PHYSICAL_MATERIAL_INACTIVE")
                && report.Diagnostics.Exists(item => item?.Code == "AUTH_AUDIO_PHYSICAL_FALLBACK_INACTIVE")
                && report.Diagnostics.Exists(item => item?.Code == "AUTH_AUDIO_PHYSICAL_FALLBACK_BASIS_INACTIVE")
                && report.Diagnostics.Exists(item => item?.Code == "AUTH_AUDIO_PHYSICAL_RESPONSE_PARAMETER_LIMIT"),
            "Active physical graph validation must reject inactive references, invalid cooldown/weights, and response buffers larger than CharacterTest AudioIntent capacity. Codes: "
                + string.Join(",", report.Diagnostics.ConvertAll(item => item?.Code ?? string.Empty)));
    }

    private static void PhysicalAudioCoverage_ResolvesFullFactsAndDetectsTrueAmbiguity()
    {
        PhysicalAudioAuthoringManifestDocument fixture = BuildPhysicalResolverFixture();
        var fact = new PhysicalAudioInteractionFactAuthoring
        {
            InteractionKind = "ObjectImpact",
            InteractionMode = "Impact",
            Phase = "Started",
            Impactor = new PhysicalAudioBodyFactAuthoring { MaterialId = "material.a", Mass = 0.5f, ContactArea = 0.5f, Surface = "Normal", Construction = "Solid" },
            Receiver = new PhysicalAudioBodyFactAuthoring { MaterialId = "material.b", Mass = 0.5f, ContactArea = 0.5f, Surface = "Normal", Construction = "Solid" },
            RelativeSpeed = 6f,
            NormalSpeed = 4f,
            TangentialSpeed = 2f,
            Intensity = 0.6f,
            AngleDegrees = 90f
        };
        fixture.CoverageRequirements.Add(new PhysicalAudioCoverageRequirementAuthoring
        {
            Id = "coverage.fixture",
            MaterialPairIds = new List<string> { fixture.BasisSounds.Find(item => item.Id == "basis.exact").MaterialPairId },
            VariantKinds = new List<string> { "Impact" },
            Facts = new List<PhysicalAudioInteractionFactAuthoring> { ClonePhysicalFact(fact) }
        });

        PhysicalAudioCoverageReport coverage = PhysicalAudioAuthoringManifest.AnalyzeCoverage(fixture);
        Require(coverage.CoveredCellCount == 1 && coverage.Cells[0].ScenarioCount == 1
                && coverage.Cells[0].SelectedBasisIds.Contains("basis.exact"),
            "coverage must resolve the complete authored fact instead of counting material-pair declarations.");

        PhysicalAudioBasisSoundAuthoring lowerPriorityOverlap = CloneFixtureBasis(fixture, "basis.lower-priority", 530004, "cue.lower-priority");
        lowerPriorityOverlap.Priority = -1;
        fixture.BasisSounds.Add(lowerPriorityOverlap);
        coverage = PhysicalAudioAuthoringManifest.AnalyzeCoverage(fixture);
        Require(coverage.CoveredCellCount == 1 && coverage.AmbiguousCellCount == 0,
            "overlapping ranges with a lower Basis priority are deterministically covered, not a true ambiguity.");

        fixture.BasisSounds.Add(CloneFixtureBasis(fixture, "basis.true-tie", 530005, "cue.true-tie"));
        coverage = PhysicalAudioAuthoringManifest.AnalyzeCoverage(fixture);
        Require(coverage.AmbiguousCellCount == 1 && coverage.Cells[0].AmbiguousScenarioCount == 1
                && coverage.Cells[0].MatchCount == 2,
            "coverage must report a true equal-priority/equal-distance ambiguity even though runtime uses stable Basis ID as its final tie-break.");

        fixture.BasisSounds.RemoveAll(item => item.Id == "basis.true-tie");
        PhysicalAudioInteractionFactAuthoring outsideRange = ClonePhysicalFact(fact);
        outsideRange.Impactor.Mass = 2f;
        fixture.CoverageRequirements[0].Facts.Add(outsideRange);
        coverage = PhysicalAudioAuthoringManifest.AnalyzeCoverage(fixture);
        Require(coverage.MissingCellCount == 1 && coverage.Cells[0].ScenarioCount == 2
                && coverage.Cells[0].ResolvedScenarioCount == 1,
            "coverage must detect a mass/range hole that material-only counting would incorrectly mark covered.");
    }

    private static void PhysicalAudioAuthoring_MatchesRuntimeResponseFixture()
    {
        PhysicalAudioAuthoringManifestDocument fixture = BuildPhysicalResolverFixture();
        fixture.MaterialProfiles.Find(item => item.Id == "material.a").Damping = 0.2f;
        fixture.MaterialProfiles.Find(item => item.Id == "material.b").Damping = 0.6f;
        fixture.ResponseProfiles[0].Curves = new List<PhysicalAudioResponseCurveAuthoring>
        {
            Curve(500251, "EffectiveEnergy", 0f, 100f, 0f, 0f, 10f, 10f),
            Curve(500252, "TangentialRatio", 0f, 1f, 0f, 0f, 1f, 1f),
            Curve(500253, "Intensity", 0f, 1f, 0f, 0f, 1f, 1f),
            Curve(500254, "AngleDegrees", 0f, 180f, 0f, 0f, 180f, 180f),
            Curve(500255, "CombinedDamping", 0f, 1f, 0f, 0f, 1f, 1f)
        };
        var fact = new PhysicalAudioInteractionFactAuthoring
        {
            InteractionKind = "ObjectImpact",
            InteractionMode = "Impact",
            Phase = "Started",
            Impactor = new PhysicalAudioBodyFactAuthoring { MaterialId = "material.a", Mass = 0.5f, ContactArea = 0.5f, Surface = "Normal", Construction = "Solid" },
            Receiver = new PhysicalAudioBodyFactAuthoring { MaterialId = "material.b", Mass = 0.5f, ContactArea = 0.5f, Surface = "Normal", Construction = "Solid" },
            RelativeSpeed = 6f,
            NormalSpeed = 4f,
            TangentialSpeed = 0f,
            TangentialRatio = 0.25f,
            Intensity = 2f,
            AngleDegrees = 200f
        };

        PhysicalAudioResolvedPlaybackAuthoring resolved = PhysicalAudioAuthoringManifest.Resolve(fixture, fact);
        float[] runtimeFixture = { 4.5f, 1.5f / 5.5f, 1f, 180f, 0.4f };
        Require(resolved.Success && resolved.Parameters.Count == runtimeFixture.Length,
            "Authoring fixture must emit the same complete response parameter count as runtime.");
        for (int i = 0; i < runtimeFixture.Length; i++)
            Require(Math.Abs(resolved.Parameters[i].Value - runtimeFixture[i]) < 0.0001f,
                "Authoring response parameter " + i + " must exactly match the runtime fixture after fact clamping and legacy tangential conversion.");

        fact.RelativeSpeed = float.NaN;
        fact.TangentialRatio = 0.5f;
        fact.Intensity = -1f;
        fact.AngleDegrees = float.NaN;
        resolved = PhysicalAudioAuthoringManifest.Resolve(fixture, fact);
        Require(resolved.Success
                && Math.Abs(resolved.Parameters[0].Value) < 0.0001f
                && Math.Abs(resolved.Parameters[1].Value) < 0.0001f
                && Math.Abs(resolved.Parameters[2].Value) < 0.0001f
                && Math.Abs(resolved.Parameters[3].Value) < 0.0001f,
            "Authoring must apply the same NaN/non-negative/clamp rules as PhysicalAudioInteractionFact before selection and response evaluation.");

        fact.RelativeSpeed = 6f;
        fact.Intensity = 0.6f;
        fact.AngleDegrees = 90f;
        fixture.MaterialProfiles.Find(item => item.Id == "material.c").FallbackMaterialId = "material.a";
        PhysicalAudioInteractionFactAuthoring fallbackFact = ClonePhysicalFact(fact);
        fallbackFact.Impactor.MaterialId = "material.c";
        resolved = PhysicalAudioAuthoringManifest.Resolve(fixture, fallbackFact);
        Require(resolved.Success && resolved.BasisId == "basis.exact" && resolved.Trace.UsedFallback,
            "Authoring material selection must use the explicit per-material fallback chain instead of wildcard Basis ownership.");
    }

    private static void PhysicalAudioWorkbench_UsesTripleHashAndAtomicSave()
    {
        string repoRoot = FindRepoRoot();
        string root = Path.Combine(Path.GetTempPath(), "mx-physical-audio-save-" + Guid.NewGuid().ToString("N"));
        string audioConfig = Path.Combine(root, "Assets", "Config", "MxFramework", "Audio");
        try
        {
            Directory.CreateDirectory(audioConfig);
            CopyAudioConfig(repoRoot, root, "audio_semantic_manifest.json");
            CopyAudioConfig(repoRoot, root, "audio_generation_manifest.json");
            CopyAudioConfig(repoRoot, root, "audio_physical_manifest.json");
            string semanticPath = Path.Combine(audioConfig, "audio_semantic_manifest.json");
            string generationPath = Path.Combine(audioConfig, "audio_generation_manifest.json");
            string physicalPath = Path.Combine(audioConfig, "audio_physical_manifest.json");
            AudioSemanticManifestDocument semantic = JsonSerializer.Deserialize<AudioSemanticManifestDocument>(File.ReadAllText(semanticPath), JsonOptions);
            MxFramework.Authoring.Cli.EditorServer.AudioGenerationManifestDocument generation = JsonSerializer.Deserialize<MxFramework.Authoring.Cli.EditorServer.AudioGenerationManifestDocument>(File.ReadAllText(generationPath), JsonOptions);
            PhysicalAudioAuthoringManifestDocument physical = JsonSerializer.Deserialize<PhysicalAudioAuthoringManifestDocument>(File.ReadAllText(physicalPath), JsonOptions);

            var saved = MxFramework.Authoring.Cli.EditorServer.SaveAudioWorkbenchDraft(
                root,
                string.Empty,
                new MxFramework.Authoring.Cli.EditorServer.AudioWorkbenchManifestRequest
                {
                    expectedHash = CharacterPackageHashUtility.ComputeFileSha256(semanticPath),
                    manifest = semantic,
                    expectedGenerationHash = CharacterPackageHashUtility.ComputeFileSha256(generationPath),
                    generationManifest = generation,
                    expectedPhysicalHash = CharacterPackageHashUtility.ComputeFileSha256(physicalPath),
                    physicalManifest = physical
                },
                JsonOptions);
            Require(saved.success && !string.IsNullOrWhiteSpace(saved.currentHash)
                && !string.IsNullOrWhiteSpace(saved.generationManifestHash)
                && !string.IsNullOrWhiteSpace(saved.physicalManifestHash),
                "Audio Workbench save should atomically persist Semantic, Generation, and Physical manifests behind three hashes.");

            var stale = MxFramework.Authoring.Cli.EditorServer.SaveAudioWorkbenchDraft(
                root,
                string.Empty,
                new MxFramework.Authoring.Cli.EditorServer.AudioWorkbenchManifestRequest
                {
                    expectedHash = saved.currentHash,
                    manifest = semantic,
                    expectedPhysicalHash = "sha256:stale",
                    physicalManifest = physical
                },
                JsonOptions);
            Require(!stale.success && stale.httpStatus == 409 && stale.code == "AUTH_AUDIO_PHYSICAL_MANIFEST_CONFLICT",
                "Audio Workbench save should reject a stale physical manifest independently of the Semantic hash.");

            var generationPublishConflict = MxFramework.Authoring.Cli.EditorServer.PublishAudioWorkbench(
                root,
                string.Empty,
                new MxFramework.Authoring.Cli.EditorServer.AudioWorkbenchPublishRequest
                {
                    expectedHash = saved.currentHash,
                    expectedGenerationHash = "sha256:stale",
                    expectedPhysicalHash = saved.physicalManifestHash
                },
                JsonOptions);
            Require(!generationPublishConflict.success && generationPublishConflict.httpStatus == 409
                    && generationPublishConflict.code == "AUTH_AUDIO_GENERATION_PUBLISH_CONFLICT",
                "runtime publication should independently reject a stale generation manifest hash before compiling catalogs.");

            var physicalPublishConflict = MxFramework.Authoring.Cli.EditorServer.PublishAudioWorkbench(
                root,
                string.Empty,
                new MxFramework.Authoring.Cli.EditorServer.AudioWorkbenchPublishRequest
                {
                    expectedHash = saved.currentHash,
                    expectedGenerationHash = saved.generationManifestHash,
                    expectedPhysicalHash = "sha256:stale"
                },
                JsonOptions);
            Require(!physicalPublishConflict.success && physicalPublishConflict.httpStatus == 409
                    && physicalPublishConflict.code == "AUTH_AUDIO_PHYSICAL_PUBLISH_CONFLICT",
                "runtime publication should independently reject a stale Physical manifest hash before compiling catalogs.");

            string savedGenerationJson = File.ReadAllText(generationPath);
            File.Delete(generationPath);
            var generationMissing = MxFramework.Authoring.Cli.EditorServer.PublishAudioWorkbench(
                root,
                string.Empty,
                new MxFramework.Authoring.Cli.EditorServer.AudioWorkbenchPublishRequest
                {
                    expectedHash = saved.currentHash,
                    expectedPhysicalHash = saved.physicalManifestHash
                },
                JsonOptions);
            Require(!generationMissing.success && generationMissing.httpStatus == 400
                    && generationMissing.code == "AUTH_AUDIO_GENERATION_MANIFEST_MISSING",
                "runtime publication must require the project-level Generation manifest instead of synthesizing a default document.");
            File.WriteAllText(generationPath, savedGenerationJson);

            string savedPhysicalJson = File.ReadAllText(physicalPath);
            string staleGeneratedPhysicalPath = Path.Combine(root, "Assets", "MxFrameworkGenerated", "Audio", "audio_physical_runtime_catalog.json");
            string staleMirrorPhysicalPath = Path.Combine(root, "Assets", "StreamingAssets", "MxFramework", "Audio", "audio_physical_runtime_catalog.json");
            Directory.CreateDirectory(Path.GetDirectoryName(staleGeneratedPhysicalPath));
            Directory.CreateDirectory(Path.GetDirectoryName(staleMirrorPhysicalPath));
            File.WriteAllText(staleGeneratedPhysicalPath, "stale-generated-physical");
            File.WriteAllText(staleMirrorPhysicalPath, "stale-mirror-physical");
            File.Delete(physicalPath);
            var physicalMissing = MxFramework.Authoring.Cli.EditorServer.PublishAudioWorkbench(
                root,
                string.Empty,
                new MxFramework.Authoring.Cli.EditorServer.AudioWorkbenchPublishRequest
                {
                    expectedHash = saved.currentHash,
                    expectedGenerationHash = CharacterPackageHashUtility.ComputeFileSha256(generationPath)
                },
                JsonOptions);
            Require(!physicalMissing.success && physicalMissing.httpStatus == 400
                    && physicalMissing.code == "AUTH_AUDIO_PHYSICAL_MANIFEST_MISSING",
                "runtime publication must reject a missing Physical manifest even when stale generated catalogs still exist.");
            Require(File.ReadAllText(staleGeneratedPhysicalPath) == "stale-generated-physical"
                    && File.ReadAllText(staleMirrorPhysicalPath) == "stale-mirror-physical",
                "a rejected publication must not rewrite or bless stale Physical catalog artifacts.");
            File.WriteAllText(physicalPath, savedPhysicalJson);

            string semanticBeforeFailure = File.ReadAllText(semanticPath);
            string generationBeforeFailure = File.ReadAllText(generationPath);
            File.Delete(physicalPath);
            Directory.CreateDirectory(physicalPath);
            var failed = MxFramework.Authoring.Cli.EditorServer.SaveAudioWorkbenchDraft(
                root,
                string.Empty,
                new MxFramework.Authoring.Cli.EditorServer.AudioWorkbenchManifestRequest
                {
                    expectedHash = CharacterPackageHashUtility.ComputeFileSha256(semanticPath),
                    manifest = semantic,
                    expectedGenerationHash = CharacterPackageHashUtility.ComputeFileSha256(generationPath),
                    generationManifest = generation,
                    physicalManifest = physical
                },
                JsonOptions);
            Require(!failed.success && failed.code == "AUTH_AUDIO_SAVE_WRITE_FAILED",
                "a failed third manifest write should return a structured Authoring error.");
            Require(File.ReadAllText(semanticPath) == semanticBeforeFailure && File.ReadAllText(generationPath) == generationBeforeFailure,
                "a failed Physical write must roll back already committed Semantic and Generation writes.");
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    private static void PhysicalAudioWorkbench_PromotesAppliedSetWithHash()
    {
        string repoRoot = FindRepoRoot();
        string root = Path.Combine(Path.GetTempPath(), "mx-physical-audio-promote-" + Guid.NewGuid().ToString("N"));
        try
        {
            string audioConfig = Path.Combine(root, "Assets", "Config", "MxFramework", "Audio");
            Directory.CreateDirectory(audioConfig);
            CopyAudioConfig(repoRoot, root, "audio_semantic_manifest.json");
            CopyAudioConfig(repoRoot, root, "audio_generation_manifest.json");
            CopyAudioConfig(repoRoot, root, "audio_physical_manifest.json");
            string semanticPath = Path.Combine(audioConfig, "audio_semantic_manifest.json");
            string generationPath = Path.Combine(audioConfig, "audio_generation_manifest.json");
            string physicalPath = Path.Combine(audioConfig, "audio_physical_manifest.json");
            string semanticHash = CharacterPackageHashUtility.ComputeFileSha256(semanticPath);
            MxFramework.Authoring.Cli.EditorServer.AudioGenerationManifestDocument generation = JsonSerializer.Deserialize<MxFramework.Authoring.Cli.EditorServer.AudioGenerationManifestDocument>(
                File.ReadAllText(generationPath), JsonOptions);
            MxFramework.Authoring.AudioAdoptionSetV3 targetSet = generation.AdoptionSets.Find(item => item.SetId == "20260714071857-57ed119f");
            MxFramework.Authoring.AudioAdoptionEventStateV3 targetState = generation.EventStates.Find(item => item.EventDefinitionId == targetSet.EventDefinitionId);
            targetSet.Status = "Saved";
            File.WriteAllText(generationPath, JsonSerializer.Serialize(generation, JsonOptions));
            string generationHash = CharacterPackageHashUtility.ComputeFileSha256(generationPath);
            string physicalHash = CharacterPackageHashUtility.ComputeFileSha256(physicalPath);
            var notApplied = MxFramework.Authoring.Cli.EditorServer.PromotePhysicalAudioBasis(
                root,
                string.Empty,
                new MxFramework.Authoring.Cli.EditorServer.PhysicalAudioPromoteApiRequest
                {
                    expectedSemanticHash = semanticHash,
                    expectedGenerationHash = generationHash,
                    expectedPhysicalHash = physicalHash,
                    audioCueId = "sfx.object_collision.default",
                    adoptionSetId = "20260714071857-57ed119f",
                    familyId = "audio.family.object_impact"
                },
                JsonOptions);
            Require(!notApplied.success && notApplied.httpStatus == 400
                    && notApplied.code == "AUTH_AUDIO_PHYSICAL_ADOPTION_SET_NOT_APPLIED",
                "promotion must reject a Saved/Superseded/failed adoption version even when all three manifest hashes are current.");

            targetSet.Status = "Applied";
            targetState.ActiveSetId = "set.not-current";
            File.WriteAllText(generationPath, JsonSerializer.Serialize(generation, JsonOptions));
            generationHash = CharacterPackageHashUtility.ComputeFileSha256(generationPath);
            var notActive = MxFramework.Authoring.Cli.EditorServer.PromotePhysicalAudioBasis(
                root,
                string.Empty,
                new MxFramework.Authoring.Cli.EditorServer.PhysicalAudioPromoteApiRequest
                {
                    expectedSemanticHash = semanticHash,
                    expectedGenerationHash = generationHash,
                    expectedPhysicalHash = physicalHash,
                    audioCueId = "sfx.object_collision.default",
                    adoptionSetId = "20260714071857-57ed119f",
                    familyId = "audio.family.object_impact"
                },
                JsonOptions);
            Require(!notActive.success && notActive.httpStatus == 400
                    && notActive.code == "AUTH_AUDIO_PHYSICAL_ADOPTION_SET_NOT_ACTIVE",
                "promotion must reject an Applied version that is not the Event's current activeSetId.");

            targetState.ActiveSetId = targetSet.SetId;
            File.WriteAllText(generationPath, JsonSerializer.Serialize(generation, JsonOptions));
            generationHash = CharacterPackageHashUtility.ComputeFileSha256(generationPath);
            var promoted = MxFramework.Authoring.Cli.EditorServer.PromotePhysicalAudioBasis(
                root,
                string.Empty,
                new MxFramework.Authoring.Cli.EditorServer.PhysicalAudioPromoteApiRequest
                {
                    expectedSemanticHash = semanticHash,
                    expectedGenerationHash = generationHash,
                    expectedPhysicalHash = physicalHash,
                    audioCueId = "sfx.object_collision.default",
                    adoptionSetId = "20260714071857-57ed119f",
                    familyId = "audio.family.object_impact"
                },
                JsonOptions);
            Require(promoted.success && promoted.basis.RuntimeBasisId == 530211
                && promoted.basis.AdoptionSetId == "20260714071857-57ed119f",
                "promoting an already managed Cue should preserve the stable Basis id while updating its immutable adoption version reference.");
            Require(promoted.basis.Receiver.MaterialId == "audio.material.generic_solid"
                && promoted.basis.Receiver.Construction == "Solid",
                "promotion without a new collision profile must preserve manually authored runtime anchors.");
            Require(!string.IsNullOrWhiteSpace(promoted.basis.MaterialPairId)
                && promoted.basis.VariantKind == "Impact"
                && promoted.basis.SideA != null
                && promoted.basis.SideB != null,
                "promotion must preserve the v2 public Pair/Variant identity and canonical Side A/B anchors.");

            string currentPhysicalHash = CharacterPackageHashUtility.ComputeFileSha256(physicalPath);
            var staleSemantic = MxFramework.Authoring.Cli.EditorServer.PromotePhysicalAudioBasis(
                root,
                string.Empty,
                new MxFramework.Authoring.Cli.EditorServer.PhysicalAudioPromoteApiRequest
                {
                    expectedSemanticHash = "sha256:stale",
                    expectedGenerationHash = generationHash,
                    expectedPhysicalHash = currentPhysicalHash,
                    audioCueId = "sfx.object_collision.default",
                    adoptionSetId = targetSet.SetId,
                    familyId = "audio.family.object_impact"
                },
                JsonOptions);
            Require(!staleSemantic.success && staleSemantic.httpStatus == 409
                    && staleSemantic.code == "AUTH_AUDIO_SEMANTIC_MANIFEST_CONFLICT",
                "promotion must independently reject a stale Semantic manifest hash.");

            var staleGeneration = MxFramework.Authoring.Cli.EditorServer.PromotePhysicalAudioBasis(
                root,
                string.Empty,
                new MxFramework.Authoring.Cli.EditorServer.PhysicalAudioPromoteApiRequest
                {
                    expectedSemanticHash = semanticHash,
                    expectedGenerationHash = "sha256:stale",
                    expectedPhysicalHash = currentPhysicalHash,
                    audioCueId = "sfx.object_collision.default",
                    adoptionSetId = targetSet.SetId,
                    familyId = "audio.family.object_impact"
                },
                JsonOptions);
            Require(!staleGeneration.success && staleGeneration.httpStatus == 409
                    && staleGeneration.code == "AUTH_AUDIO_GENERATION_MANIFEST_CONFLICT",
                "promotion must independently reject a stale Generation manifest hash.");

            var stale = MxFramework.Authoring.Cli.EditorServer.PromotePhysicalAudioBasis(
                root,
                string.Empty,
                new MxFramework.Authoring.Cli.EditorServer.PhysicalAudioPromoteApiRequest
                {
                    expectedSemanticHash = semanticHash,
                    expectedGenerationHash = generationHash,
                    expectedPhysicalHash = physicalHash,
                    audioCueId = "sfx.object_collision.default",
                    adoptionSetId = "20260714071857-57ed119f",
                    familyId = "audio.family.object_impact"
                },
                JsonOptions);
            Require(!stale.success && stale.httpStatus == 409
                    && stale.code == "AUTH_AUDIO_PHYSICAL_MANIFEST_CONFLICT",
                "promotion should reject a stale expectedPhysicalHash after the first write.");
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    private static void PhysicalAudioWorkbench_WritesUnityRuntimePreviewRequest()
    {
        string repoRoot = FindRepoRoot();
        string root = Path.Combine(Path.GetTempPath(), "mx-physical-audio-preview-" + Guid.NewGuid().ToString("N"));
        try
        {
            string audioConfig = Path.Combine(root, "Assets", "Config", "MxFramework", "Audio");
            Directory.CreateDirectory(audioConfig);
            CopyAudioConfig(repoRoot, root, "audio_semantic_manifest.json");
            CopyAudioConfig(repoRoot, root, "audio_physical_manifest.json");
            AudioSemanticManifestDocument semantic = JsonSerializer.Deserialize<AudioSemanticManifestDocument>(
                File.ReadAllText(Path.Combine(audioConfig, "audio_semantic_manifest.json")),
                JsonOptions);
            PhysicalAudioAuthoringManifestDocument physical = JsonSerializer.Deserialize<PhysicalAudioAuthoringManifestDocument>(
                File.ReadAllText(Path.Combine(audioConfig, "audio_physical_manifest.json")),
                JsonOptions);
            PhysicalAudioInteractionFactAuthoring fact = physical.ScenarioPresets[0].Fact;
            fact.ContactNormal = new PhysicalAudioDirectionAuthoring { X = 1f, Y = 2f, Z = -3f };

            var response = MxFramework.Authoring.Cli.EditorServer.WritePhysicalAudioPreviewRequest(
                root,
                string.Empty,
                new MxFramework.Authoring.Cli.EditorServer.PhysicalAudioPreviewApiRequest
                {
                    semanticManifest = semantic,
                    physicalManifest = physical,
                    fact = fact,
                    position = new MxFramework.Authoring.Cli.EditorServer.AudioPreviewPosition { x = 1f, y = 2f, z = 3f }
                },
                JsonOptions);
            Require(response.success && response.httpStatus == 202
                    && !string.IsNullOrWhiteSpace(response.expectedBasisId)
                    && response.expectedRuntimeBasisId > 0
                    && response.expectedRuntimeCueId > 0,
                "physical preview should compile one expected Basis/Cue identity before handing work to Unity.");

            string requestPath = Path.Combine(root, "Temp", "MxFrameworkAuthoring", "Audio", "preview_request.json");
            var request = JsonSerializer.Deserialize<MxFramework.Authoring.Cli.EditorServer.AudioPreviewRequestFile>(
                File.ReadAllText(requestPath),
                JsonOptions);
            Require(request.previewKind == "Physical"
                    && request.physical != null
                    && request.physical.expectedRuntimeBasisId == response.expectedRuntimeBasisId
                    && request.physical.expectedRuntimeCueId == response.expectedRuntimeCueId,
                "Unity bridge request should carry the compiled runtime expectation rather than browser-computed playback truth.");
            Require(request.physical.runtimeCatalogPath.StartsWith(
                        "Temp/MxFrameworkAuthoring/Audio/physical_preview_catalogs/",
                        StringComparison.Ordinal)
                    && File.Exists(Path.Combine(root, request.physical.runtimeCatalogPath))
                    && !string.IsNullOrWhiteSpace(request.physical.runtimeCatalogHash),
                "physical preview catalog must be immutable per request and remain inside the safe Temp shelf.");
            Require(request.physical.fact.impactor.runtimeMaterialId > 0
                    && request.physical.fact.receiver.runtimeMaterialId > 0
                    && request.physical.fact.position.x == 1f
                    && Math.Abs(request.physical.fact.contactNormal.x - 0.2672612f) < 0.0001f
                    && Math.Abs(request.physical.fact.contactNormal.y - 0.5345225f) < 0.0001f
                    && Math.Abs(request.physical.fact.contactNormal.z + 0.8017837f) < 0.0001f,
                "Authoring material identities, preview position, and non-+Z contact normal should compile into runtime-only request values.");

            var capture = MxFramework.Authoring.Cli.EditorServer.WritePhysicalAudioCaptureRequest(root, JsonOptions);
            Require(capture.success && capture.httpStatus == 202
                    && File.Exists(Path.Combine(root, "Temp", "MxFrameworkAuthoring", "Audio", "physical_capture_request.json")),
                "physical fact capture should expose a low-frequency request shelf without inventing captured data.");

            AudioCueAuthoringDefinition invalidCue = semantic.Cues.Find(item => item.RuntimeCueId == response.expectedRuntimeCueId);
            invalidCue.Lifecycle = AudioCueLifecycles.Active;
            invalidCue.DefaultParameters.Add(new AudioCueDefaultParameter { Name = "MissingPreviewParameter", Value = 0.75f });
            Dictionary<string, string> before = SnapshotCueDefaultParameterFiles(root);
            var blockedPreview = MxFramework.Authoring.Cli.EditorServer.WritePhysicalAudioPreviewRequest(
                root, string.Empty,
                new MxFramework.Authoring.Cli.EditorServer.PhysicalAudioPreviewApiRequest
                {
                    semanticManifest = semantic, physicalManifest = physical, fact = fact
                }, JsonOptions);
            Require(!blockedPreview.success && blockedPreview.httpStatus == 400
                && blockedPreview.code == CueDefaultParameterMissingCode,
                "physical preview must return the parameter diagnostic before writing a catalog or Unity request.");
            RequireCueDefaultParameterFilesUnchanged(root, before);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    private static PhysicalAudioAuthoringManifestDocument BuildPhysicalResolverFixture()
    {
        var manifest = new PhysicalAudioAuthoringManifestDocument
        {
            Format = "mx.audioPhysicalAuthoringManifest.v1",
            SchemaVersion = 1
        };
        manifest.MaterialProfiles.AddRange(new[]
        {
            new PhysicalAudioMaterialProfileAuthoring { Id = "material.a", RuntimeMaterialId = 510001 },
            new PhysicalAudioMaterialProfileAuthoring { Id = "material.b", RuntimeMaterialId = 510002 },
            new PhysicalAudioMaterialProfileAuthoring { Id = "material.c", RuntimeMaterialId = 510003 }
        });
        manifest.Families.Add(new PhysicalAudioFamilyAuthoring
        {
            Id = "family.impact", RuntimeFamilyId = 520001, InteractionKind = "ObjectImpact", InteractionMode = "Impact",
            ResponseProfileId = "response.impact", Priority = 10,
            Weights = new PhysicalAudioSelectionWeightsAuthoring { Material = 1f, Mass = 1f, ContactArea = 1f, Surface = 1f, Construction = 1f, Speed = 1f, Intensity = 1f, Angle = 1f }
        });
        manifest.ResponseProfiles.Add(new PhysicalAudioResponseProfileAuthoring
        {
            Id = "response.impact", RuntimeResponseProfileId = 540001,
            Curves = new List<PhysicalAudioResponseCurveAuthoring>
            {
                new PhysicalAudioResponseCurveAuthoring { ParameterId = 500251, Input = "Intensity", OutputMin = 0f, OutputMax = 1f, Points = new List<PhysicalAudioResponsePointAuthoring> { new PhysicalAudioResponsePointAuthoring { Input = 0f, Output = 0f }, new PhysicalAudioResponsePointAuthoring { Input = 1f, Output = 1f } } },
                new PhysicalAudioResponseCurveAuthoring { ParameterId = 500252, Input = "RelativeSpeed", OutputMin = 0.5f, OutputMax = 1.6f, Points = new List<PhysicalAudioResponsePointAuthoring> { new PhysicalAudioResponsePointAuthoring { Input = 0f, Output = 0.5f }, new PhysicalAudioResponsePointAuthoring { Input = 10f, Output = 1.5f } } }
            }
        });
        manifest.BasisSounds.Add(CloneExactFixtureBasis("basis.exact", 530001, "cue.exact"));
        PhysicalAudioBasisSoundAuthoring wildcard = CloneExactFixtureBasis("basis.wildcard", 530002, "cue.wildcard");
        wildcard.Impactor.MaterialId = string.Empty;
        wildcard.Receiver.MaterialId = string.Empty;
        wildcard.Impactor.Surface = wildcard.Receiver.Surface = "Any";
        wildcard.Impactor.Construction = wildcard.Receiver.Construction = "Any";
        manifest.BasisSounds.Add(wildcard);
        PhysicalAudioBasisSoundAuthoring mismatch = CloneExactFixtureBasis("basis.mismatch", 530003, "cue.mismatch");
        mismatch.Receiver.MaterialId = "material.c";
        manifest.BasisSounds.Add(mismatch);
        return PhysicalAudioAuthoringManifest.Upgrade(manifest);
    }

    private static PhysicalAudioAuthoringManifestDocument ClonePhysicalManifest(PhysicalAudioAuthoringManifestDocument source)
    {
        return JsonSerializer.Deserialize<PhysicalAudioAuthoringManifestDocument>(
            JsonSerializer.Serialize(source, JsonOptions), JsonOptions);
    }

    private static PhysicalAudioInteractionFactAuthoring ClonePhysicalFact(PhysicalAudioInteractionFactAuthoring source)
    {
        return JsonSerializer.Deserialize<PhysicalAudioInteractionFactAuthoring>(
            JsonSerializer.Serialize(source, JsonOptions), JsonOptions);
    }

    private static PhysicalAudioResponseCurveAuthoring Curve(
        int parameterId,
        string input,
        float outputMin,
        float outputMax,
        float input0,
        float output0,
        float input1,
        float output1)
    {
        return new PhysicalAudioResponseCurveAuthoring
        {
            ParameterId = parameterId,
            Input = input,
            OutputMin = outputMin,
            OutputMax = outputMax,
            Points = new List<PhysicalAudioResponsePointAuthoring>
            {
                new PhysicalAudioResponsePointAuthoring { Input = input0, Output = output0 },
                new PhysicalAudioResponsePointAuthoring { Input = input1, Output = output1 }
            }
        };
    }

    private static PhysicalAudioBasisSoundAuthoring CloneExactFixtureBasis(string id, int runtimeId, string cue)
    {
        return new PhysicalAudioBasisSoundAuthoring
        {
            Id = id, RuntimeBasisId = runtimeId, FamilyId = "family.impact", AudioCueId = cue, Lifecycle = PhysicalAudioLifecycles.Active,
            Impactor = new PhysicalAudioBodyAnchorAuthoring { MaterialId = "material.a", Mass = new PhysicalAudioRangeAnchor { Min = 0f, Max = 1f, Anchor = 0.5f }, ContactArea = new PhysicalAudioRangeAnchor { Min = 0f, Max = 1f, Anchor = 0.5f }, Surface = "Normal", Construction = "Solid" },
            Receiver = new PhysicalAudioBodyAnchorAuthoring { MaterialId = "material.b", Mass = new PhysicalAudioRangeAnchor { Min = 0f, Max = 1f, Anchor = 0.5f }, ContactArea = new PhysicalAudioRangeAnchor { Min = 0f, Max = 1f, Anchor = 0.5f }, Surface = "Normal", Construction = "Solid" },
            RelativeSpeed = new PhysicalAudioRangeAnchor { Min = 0f, Max = 12f, Anchor = 6f },
            Intensity = new PhysicalAudioRangeAnchor { Min = 0f, Max = 1f, Anchor = 0.6f },
            AngleDegrees = new PhysicalAudioRangeAnchor { Min = 0f, Max = 180f, Anchor = 90f }
        };
    }

    private static PhysicalAudioBasisSoundAuthoring CloneFixtureBasis(
        PhysicalAudioAuthoringManifestDocument manifest,
        string id,
        int runtimeId,
        string cue)
    {
        PhysicalAudioBasisSoundAuthoring source = manifest.BasisSounds.Find(item => item.Id == "basis.exact");
        PhysicalAudioBasisSoundAuthoring clone = JsonSerializer.Deserialize<PhysicalAudioBasisSoundAuthoring>(
            JsonSerializer.Serialize(source, JsonOptions), JsonOptions);
        clone.Id = id;
        clone.RuntimeBasisId = runtimeId;
        clone.AudioCueId = cue;
        return clone;
    }

    private static void AddPhysicalParameterEvidence(
        PhysicalAudioAuthoringManifestDocument physical,
        AudioSemanticManifestDocument semantic,
        AuthoringFmodAudioLibrarySnapshotDocument snapshot)
    {
        for (int i = 0; i < physical.BasisSounds.Count; i++)
        {
            PhysicalAudioBasisSoundAuthoring basis = physical.BasisSounds[i];
            AudioCueAuthoringDefinition cue = AudioAuthoringManifestV2.FindCue(semantic, basis.AudioCueId);
            AudioEventAuthoringDefinition audioEvent = cue == null
                ? null
                : AudioAuthoringManifestV2.FindEventDefinition(semantic, cue.EventDefinitionId);
            AuthoringFmodAudioLibraryEvent snapshotEvent = audioEvent == null
                ? null
                : snapshot.Events.Find(item => item != null && string.Equals(item.Path, audioEvent.FmodEventPath, StringComparison.Ordinal));
            if (snapshotEvent == null)
                continue;
            snapshotEvent.Parameters.Clear();
            for (int p = 0; p < audioEvent.Parameters.Count; p++)
                snapshotEvent.Parameters.Add(new AuthoringFmodAudioLibraryParameter { Name = audioEvent.Parameters[p].Name });
        }
    }

    private static void BindPhysicalAuditionEvidence(
        PhysicalAudioAuthoringManifestDocument physical,
        AudioSemanticManifestDocument semantic,
        string physicalManifestHash)
    {
        for (int i = 0; i < physical.BasisSounds.Count; i++)
        {
            PhysicalAudioBasisSoundAuthoring basis = physical.BasisSounds[i];
            AudioCueAuthoringDefinition cue = AudioAuthoringManifestV2.FindCue(semantic, basis.AudioCueId);
            if (cue == null)
                continue;
            cue.AuditionEvidence ??= new AudioCueAuditionEvidence();
            cue.AuditionEvidence.Status = AudioSemanticAuditionStatuses.Approved;
            cue.AuditionEvidence.AudioBasisId = basis.Id;
            cue.AuditionEvidence.RuntimeBasisId = basis.RuntimeBasisId;
            cue.AuditionEvidence.AppliedSetId = basis.AdoptionSetId;
            cue.AuditionEvidence.PhysicalManifestHash = physicalManifestHash;
        }
    }

    private static void CopyAudioConfig(string repoRoot, string targetRoot, string fileName)
    {
        File.Copy(
            Path.Combine(repoRoot, "Assets", "Config", "MxFramework", "Audio", fileName),
            Path.Combine(targetRoot, "Assets", "Config", "MxFramework", "Audio", fileName));
    }
}
