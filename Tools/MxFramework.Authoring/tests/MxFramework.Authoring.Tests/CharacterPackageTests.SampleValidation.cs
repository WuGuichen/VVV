using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using MxFramework.Authoring;
using MxFramework.Authoring.Cli;

namespace MxFramework.Authoring.Tests;

internal static partial class CharacterPackageTests
{
    private static void BodyPartAuthoring_JsonRoundTrip_PreservesSkeletonBindingFields()
    {
        var geometry = new CharacterAuthoringGeometry();
        geometry.BodyParts.Add(new CharacterBodyPartAuthoring
        {
            PartId = "right_hand",
            DisplayName = "Right Hand",
            PartKind = CharacterAuthoringBodyPartKind.Bone,
            ParentPartId = "torso",
            BonePath = "Armature/Hips/Spine/RightHand",
            LocatorId = "bone.RightHand",
            DefaultHitZoneId = "hit.right_hand",
            ReactionGroupId = "reaction.humanoid.limb",
            Tags = new List<string> { "hand", "weapon" }
        });

        string json = JsonSerializer.Serialize(geometry, JsonOptions);
        CharacterAuthoringGeometry roundTrip = JsonSerializer.Deserialize<CharacterAuthoringGeometry>(json, JsonOptions);

        Require(roundTrip != null && roundTrip.BodyParts.Count == 1, "body part geometry should roundtrip.");
        CharacterBodyPartAuthoring part = roundTrip.BodyParts[0];
        Require(part.PartKind == CharacterAuthoringBodyPartKind.Bone, "body part kind should roundtrip.");
        Require(part.BonePath == "Armature/Hips/Spine/RightHand", "bonePath should roundtrip.");
        Require(part.LocatorId == "bone.RightHand", "locatorId should roundtrip.");
        Require(part.Tags.Count == 2 && part.Tags.Contains("weapon"), "body part tags should roundtrip.");
    }

    private static void AbstractBoneAuthoring_RoundTripAndValidationAreFailClosed()
    {
        var geometry = new CharacterAuthoringGeometry();
        geometry.Sockets.Add(new CharacterSocketProfile
        {
            SocketId = "headAnchor",
            AbstractBoneId = CharacterAbstractBoneIds.Head,
            BonePath = "Rig/Head"
        });
        geometry.Sockets.Add(new CharacterSocketProfile
        {
            SocketId = "clawTip",
            AbstractBoneId = "ext.codex_flow_test.claw_tip",
            BonePath = "Rig/Hand/ClawTip"
        });

        string json = JsonSerializer.Serialize(geometry, JsonOptions);
        CharacterAuthoringGeometry roundTrip = JsonSerializer.Deserialize<CharacterAuthoringGeometry>(json, JsonOptions);
        Require(roundTrip != null && roundTrip.Sockets.Count == 2, "abstract bone sockets should roundtrip.");
        Require(roundTrip.Sockets[0].AbstractBoneId == CharacterAbstractBoneIds.Head, "core abstract bone id should roundtrip.");
        Require(roundTrip.Sockets[1].AbstractBoneId == "ext.codex_flow_test.claw_tip", "extension abstract bone id should roundtrip.");

        CharacterAuthoringGeometry legacy = JsonSerializer.Deserialize<CharacterAuthoringGeometry>(
            "{\"sockets\":[{\"socketId\":\"legacy\",\"bonePath\":\"Rig/Head\"}]}", JsonOptions);
        Require(legacy != null && legacy.Sockets[0].AbstractBoneId == string.Empty,
            "legacy socket JSON without abstractBoneId should remain readable without inferred semantics.");

        CharacterResourcePackage missing = LoadSample("character-codex-flow-test");
        for (int i = 0; i < missing.Geometry.Sockets.Count; i++)
            missing.Geometry.Sockets[i].AbstractBoneId = string.Empty;
        CharacterAuthoringValidationReport missingReport = CharacterResourcePackageValidator.Validate(missing);
        Require(HasValidationIssue(missingReport, CharacterAuthoringValidationCodes.MissingRequiredAbstractBone, CharacterAuthoringValidationGate.SpawnBlocked),
            "missing logical head must block spawn.");

        CharacterResourcePackage duplicate = LoadSample("character-codex-flow-test");
        duplicate.Geometry.Sockets[0].AbstractBoneId = CharacterAbstractBoneIds.Head;
        duplicate.Geometry.Sockets[1].AbstractBoneId = CharacterAbstractBoneIds.Head;
        CharacterAuthoringValidationReport duplicateReport = CharacterResourcePackageValidator.Validate(duplicate);
        Require(HasValidationIssue(duplicateReport, CharacterAuthoringValidationCodes.DuplicateAbstractBoneId, CharacterAuthoringValidationGate.SpawnBlocked),
            "duplicate abstract bone semantics must block spawn.");

        CharacterResourcePackage invalid = LoadSample("character-codex-flow-test");
        invalid.Geometry.Sockets[0].AbstractBoneId = "ext.other.Claw";
        CharacterAuthoringValidationReport invalidReport = CharacterResourcePackageValidator.Validate(invalid);
        Require(HasValidationIssue(invalidReport, CharacterAuthoringValidationCodes.InvalidAbstractBoneId, CharacterAuthoringValidationGate.SpawnBlocked),
            "foreign or non-canonical abstract bone extension ids must block spawn.");
    }

    private static void SkeletonBindingValidation_ReportsBrokenReferences()
    {
        CharacterResourcePackage package = LoadSample("character-codex-flow-test");
        EnsureLegacyWeaponFixture(package);
        CharacterBodyPartAuthoring torso = package.Geometry.BodyParts.Find(part => part.PartId == "torso") ?? package.Geometry.BodyParts[0];
        torso.PartKind = CharacterAuthoringBodyPartKind.Bone;
        torso.BonePath = string.Empty;
        torso.LocatorId = string.Empty;

        CharacterBodyColliderProfile collider = package.Geometry.Colliders[0];
        collider.Shape = CharacterColliderShape.Sphere;
        collider.Radius = 0f;
        package.Geometry.Colliders.Add(new CharacterBodyColliderProfile
        {
            ColliderId = collider.ColliderId,
            PartId = torso.PartId,
            HitZoneId = "hit.duplicate",
            Shape = CharacterColliderShape.Sphere,
            Radius = 0.2f
        });

        package.Geometry.Sockets.Add(new CharacterSocketProfile
        {
            SocketId = "brokenMirror",
            ParentPartId = torso.PartId,
            Usage = CharacterSocketUsage.Weapon,
            MirrorPairSocketId = "missing.socket"
        });

        package.Geometry.WeaponAttachments[0].TraceStartSocketId = "missing.trace.start";
        package.Geometry.WeaponAttachments[0].TraceId = string.Empty;
        package.Geometry.WeaponAttachments[0].TraceRadius = -0.01f;
        package.Geometry.Traces.Clear();

        CharacterAuthoringValidationReport report = CharacterResourcePackageValidator.Validate(package);

        Require(HasValidationIssue(report, CharacterAuthoringValidationCodes.MissingBodyPartLocator, CharacterAuthoringValidationGate.SpawnBlocked), "missing representative bone/locator should block spawn.");
        Require(HasValidationIssue(report, CharacterAuthoringValidationCodes.DuplicateCollider, CharacterAuthoringValidationGate.ExportBlocked), "duplicate collider id should block export.");
        Require(HasValidationIssue(report, CharacterAuthoringValidationCodes.InvalidColliderDimensions, CharacterAuthoringValidationGate.SpawnBlocked), "invalid collider dimensions should block spawn.");
        Require(HasValidationIssue(report, CharacterAuthoringValidationCodes.MissingMirrorSocket, CharacterAuthoringValidationGate.SpawnBlocked), "missing mirror socket should block spawn.");
        Require(HasValidationIssue(report, CharacterAuthoringValidationCodes.MissingAttachmentTraceSocket, CharacterAuthoringValidationGate.SpawnBlocked), "missing trace socket should block spawn.");
        Require(HasValidationIssue(report, CharacterAuthoringValidationCodes.MissingWeaponTraceBinding, CharacterAuthoringValidationGate.SpawnBlocked), "attack-capable weapon missing trace binding should block spawn.");
        Require(HasValidationIssue(report, CharacterAuthoringValidationCodes.InvalidAttachmentTraceRadius, CharacterAuthoringValidationGate.SpawnBlocked), "negative trace radius should block spawn.");
    }

    private static void WeaponTraceValidation_ReportsInvalidProximalRange()
    {
        CharacterResourcePackage package = LoadSample("character-codex-flow-test");
        WeaponTraceProfile trace = FindMainHandTrace(package);
        trace.ProximalExtension = 0.35f;
        trace.ProximalRadius = -0.01f;

        CharacterAuthoringValidationReport report = CharacterResourcePackageValidator.Validate(package);

        CharacterAuthoringValidationIssue issue = FindValidationIssue(
            report,
            CharacterAuthoringValidationCodes.InvalidWeaponTraceProximalRange,
            CharacterAuthoringValidationGate.SpawnBlocked);
        Require(issue != null && issue.SourcePath == "geometry/traces.json" && issue.SourceObjectPath == "geometry/traces/trace.iron_sword.blade" && issue.Field == "proximalRadius", "invalid proximal range diagnostic should point at geometry trace proximalRadius.");
    }

    private static void WeaponTraceValidation_ReportsTraceResponsibilityDiagnostics()
    {
        CharacterResourcePackage missingTracePackage = LoadSample("character-codex-flow-test");
        WeaponAttachmentProfile missingTraceAttachment = FindMainHandAttachment(missingTracePackage);
        missingTraceAttachment.TraceId = "trace.missing.blade";
        CharacterAuthoringValidationReport missingTraceReport = CharacterResourcePackageValidator.Validate(missingTracePackage);
        CharacterAuthoringValidationIssue missingTrace = FindValidationIssue(
            missingTraceReport,
            CharacterAuthoringValidationCodes.MissingAttachmentTrace,
            CharacterAuthoringValidationGate.SpawnBlocked);
        Require(missingTrace != null && missingTrace.SourcePath == "geometry/weapon_attachments.json" && missingTrace.SourceObjectPath == "geometry/weaponAttachments/mount.sword.main", "missing trace diagnostic should point at the attachment traceId.");

        CharacterResourcePackage mismatchPackage = LoadSample("character-codex-flow-test");
        WeaponTraceProfile mismatchTraceProfile = FindMainHandTrace(mismatchPackage);
        mismatchTraceProfile.EquipSlot = "offHand";
        CharacterAuthoringValidationReport mismatchReport = CharacterResourcePackageValidator.Validate(mismatchPackage);
        CharacterAuthoringValidationIssue mismatch = FindValidationIssue(
            mismatchReport,
            CharacterAuthoringValidationCodes.AttachmentTraceMismatch,
            CharacterAuthoringValidationGate.SpawnBlocked);
        Require(mismatch != null && mismatch.SourcePath == "geometry/traces.json" && mismatch.SourceObjectPath == "geometry/traces/trace.iron_sword.blade" && mismatch.Field == "equipSlot", "mismatched trace diagnostic should point at geometry trace equipSlot.");

        CharacterResourcePackage ambiguousPackage = LoadSample("character-codex-flow-test");
        WeaponAttachmentProfile ambiguousAttachment = FindMainHandAttachment(ambiguousPackage);
        WeaponTraceProfile ambiguousSource = FindMainHandTrace(ambiguousPackage);
        ambiguousAttachment.TraceId = string.Empty;
        ambiguousPackage.Geometry.Traces.Add(CloneTrace(ambiguousSource, "trace.iron_sword.edge"));
        CharacterAuthoringValidationReport ambiguousReport = CharacterResourcePackageValidator.Validate(ambiguousPackage);
        CharacterAuthoringValidationIssue ambiguous = FindValidationIssue(
            ambiguousReport,
            CharacterAuthoringValidationCodes.AmbiguousWeaponTraceBinding,
            CharacterAuthoringValidationGate.SpawnBlocked);
        Require(ambiguous != null && ambiguous.SourceObjectPath == "geometry/weaponAttachments/mount.sword.main", "ambiguous trace diagnostic should point at the attachment requiring explicit traceId.");

        CharacterResourcePackage conflictPackage = LoadSample("character-codex-flow-test");
        WeaponAttachmentProfile conflictAttachment = FindMainHandAttachment(conflictPackage);
        WeaponTraceProfile conflictTrace = FindMainHandTrace(conflictPackage);
        conflictAttachment.TraceRadius = conflictTrace.Radius + 0.25f;
        CharacterAuthoringValidationReport conflictReport = CharacterResourcePackageValidator.Validate(conflictPackage);
        CharacterAuthoringValidationIssue conflict = FindValidationIssue(
            conflictReport,
            CharacterAuthoringValidationCodes.LegacyTraceOverrideConflict,
            CharacterAuthoringValidationGate.WarningOnly);
        Require(conflict != null && conflict.SourcePath == "geometry/weapon_attachments.json" && conflict.Field == "traceRadius", "legacy override conflict should warn with attachment source path and field.");
    }

    private static void CodexFlowTestSample_ValidatesAsReady()
    {
        CharacterResourcePackage package = LoadSample("character-codex-flow-test");
        CharacterAuthoringValidationReport report = CharacterResourcePackageValidator.Validate(package);

        Require(!report.HasBlockingIssues, "Codex Flow Test sample should not have blocking validation issues: " + report.ToText());
        Require(package.Geometry.BodyProfile.HeightMeters > 1.8f, "Codex Flow Test height should be practical data.");
        Require(package.Geometry.Colliders.Exists(c => c.ColliderId == "head_sphere" && c.Shape == CharacterColliderShape.Sphere), "head collider missing.");
        Require(package.Geometry.Sockets.Exists(s => s.SocketId == "mainHand"), "main hand socket missing.");
        Require(package.Geometry.WeaponSlots.Exists(slot =>
                slot.SlotId == "mainHand" && slot.AttachSocketId == "mainHand")
            && package.GlobalWeaponLibrary.SingleWeaponConfigurations.Exists(weapon =>
                (weapon.SingleWeaponConfigurationId == "weapon.000001.main" || weapon.SingleWeaponConfigurationId == "sword") &&
                weapon.TraceProfiles.Exists(trace => trace.TraceId == "trace.iron_sword.blade" && trace.Radius > 0f)),
            "character main-hand slot or global sword trace data missing.");
    }

    private static void BodyPartContactPoints_ValidateSourceDrafts()
    {
        CharacterResourcePackage package = LoadSample("character-codex-flow-test");
        Require(package.Geometry.BodyPartContactPoints.Count == 2, "sample should load body part contact point source data.");

        CharacterAuthoringValidationReport ready = CharacterResourcePackageValidator.Validate(package);
        Require(!ready.HasBlockingIssues, "body part contact point sample data should validate as ready.");

        CharacterResourcePackage duplicate = LoadSample("character-codex-flow-test");
        duplicate.Geometry.BodyPartContactPoints[1].ContactPointId = duplicate.Geometry.BodyPartContactPoints[0].ContactPointId;
        CharacterAuthoringValidationReport duplicateReport = CharacterResourcePackageValidator.Validate(duplicate);
        Require(
            HasValidationIssue(
                duplicateReport,
                CharacterAuthoringValidationCodes.DuplicateBodyPartContactPointId,
                CharacterAuthoringValidationGate.SpawnBlocked),
            "duplicate body part contact point ids should block spawn.");

        CharacterResourcePackage missingPart = LoadSample("character-codex-flow-test");
        missingPart.Geometry.BodyPartContactPoints[0].PartId = "missing_part";
        CharacterAuthoringValidationReport missingPartReport = CharacterResourcePackageValidator.Validate(missingPart);
        Require(
            HasValidationIssue(
                missingPartReport,
                CharacterAuthoringValidationCodes.MissingBodyPartContactPointPart,
                CharacterAuthoringValidationGate.SpawnBlocked),
            "missing body part contact point part should block spawn.");

        CharacterResourcePackage invalidRadius = LoadSample("character-codex-flow-test");
        invalidRadius.Geometry.BodyPartContactPoints[0].RadiusMeters = 0f;
        CharacterAuthoringValidationReport invalidRadiusReport = CharacterResourcePackageValidator.Validate(invalidRadius);
        Require(
            HasValidationIssue(
                invalidRadiusReport,
                CharacterAuthoringValidationCodes.InvalidBodyPartContactPointRadius,
                CharacterAuthoringValidationGate.SpawnBlocked),
            "invalid body part contact point radius should block spawn.");
    }

    private static void CodexFlowTestSample_ResourceFilesAndHashesValidate()
    {
        CharacterResourcePackage package = LoadSample("character-codex-flow-test");
        CharacterAuthoringValidationReport report = CharacterResourcePackageValidator.Validate(package, new CharacterResourcePackageValidationOptions
        {
            PackageRootPath = FindSamplePath("character-codex-flow-test"),
            ValidateResourceFiles = true,
            ValidateResourceHashes = true
        });
        CharacterPackageResourceHashReport hashReport = CharacterPackageResourcePipeline.BuildHashReport(package, FindSamplePath("character-codex-flow-test"));

        Require(!report.HasBlockingIssues, "Codex Flow Test resource files and hashes should validate: " + report.ToText());
        Require(!hashReport.HasBlockingIssues, "Codex Flow Test hash report should not block: " + hashReport.Diagnostics.ToText());
        Require(hashReport.Entries.Exists(entry => entry.ResourceKey == "char.codex_flow_test.model.body" && entry.Exists && entry.DeclaredContentHash == entry.ComputedContentHash), "body model hash report should include matching computed hash.");
    }

    private static WeaponAttachmentProfile FindMainHandAttachment(CharacterResourcePackage package)
    {
        EnsureLegacyWeaponFixture(package);
        WeaponAttachmentProfile attachment = package.Geometry.WeaponAttachments.Find(item => item.EquipSlot == "mainHand");
        Require(attachment != null, "legacy validation fixture should include a mainHand attachment.");
        return attachment;
    }

    private static WeaponTraceProfile FindMainHandTrace(CharacterResourcePackage package)
    {
        EnsureLegacyWeaponFixture(package);
        WeaponTraceProfile trace = package.Geometry.Traces.Find(item => item.TraceId == "trace.iron_sword.blade");
        Require(trace != null, "legacy validation fixture should include trace.iron_sword.blade.");
        return trace;
    }

    private static void EnsureLegacyWeaponFixture(CharacterResourcePackage package)
    {
        string weaponId = "weapon.000001.main";
        if (package.Geometry.WeaponAttachments.Count == 0)
        {
            package.Geometry.WeaponAttachments.Add(new WeaponAttachmentProfile
            {
                AttachmentId = "mount.sword.main",
                SingleWeaponConfigurationId = weaponId,
                SlotId = "mainHand",
                EquipSlot = "mainHand",
                AttachSocketId = "mainHand",
                TraceId = "trace.iron_sword.blade",
                TraceStartSocketId = "mainHand",
                TraceEndSocketId = "mainHand",
                TraceRadius = 0.055f
            });
        }
        if (package.Geometry.Traces.Count != 0)
            return;
        SingleWeaponTraceProfile source = package.GlobalWeaponLibrary.SingleWeaponConfigurations
            .Find(item => item.SingleWeaponConfigurationId == weaponId || item.SingleWeaponConfigurationId == "sword")?.TraceProfiles
            .Find(item => item.TraceId == "trace.iron_sword.blade");
        package.Geometry.Traces.Add(new WeaponTraceProfile
        {
            TraceId = "trace.iron_sword.blade",
            WeaponId = weaponId,
            EquipSlot = "mainHand",
            StartLocatorPath = source?.StartLocatorPath ?? "",
            EndLocatorPath = source?.EndLocatorPath ?? "",
            StartPose = source?.StartPose ?? new CharacterAuthoringLocalPose(),
            EndPose = source?.EndPose ?? new CharacterAuthoringLocalPose(),
            Radius = source?.Radius ?? 0.055f,
            ProximalExtension = source?.ProximalExtension ?? 0f,
            ProximalRadius = source?.ProximalRadius ?? 0f,
            SampleRule = source?.SampleRule ?? WeaponTraceSampleRule.CapsuleSweep,
            FixedSampleCount = source?.FixedSampleCount ?? 0,
            ActionKeys = source?.ActionKeys != null ? new List<string>(source.ActionKeys) : new List<string>()
        });
    }

    private static WeaponTraceProfile CloneTrace(WeaponTraceProfile source, string traceId)
    {
        return new WeaponTraceProfile
        {
            TraceId = traceId,
            WeaponId = source.WeaponId,
            EquipSlot = source.EquipSlot,
            StartLocatorPath = source.StartLocatorPath,
            EndLocatorPath = source.EndLocatorPath,
            StartPose = source.StartPose,
            EndPose = source.EndPose,
            Radius = source.Radius,
            ProximalExtension = source.ProximalExtension,
            ProximalRadius = source.ProximalRadius,
            SampleRule = source.SampleRule,
            FixedSampleCount = source.FixedSampleCount,
            ActionKeys = new List<string>(source.ActionKeys)
        };
    }

    private static void CodexFlowTestTemplate_ResolvesDefaultHumanoidTemplate()
    {
        MethodInfo method = typeof(MxFramework.Authoring.Cli.EditorServer).GetMethod("ResolveCharacterTemplateRelative", BindingFlags.NonPublic | BindingFlags.Static);
        Require(method != null, "template resolver should be discoverable for regression coverage.");

        string humanoid = method.Invoke(null, new object[] { "humanoid" }) as string;
        string defaultTemplate = method.Invoke(null, new object[] { string.Empty }) as string;
        string iron = method.Invoke(null, new object[] { "iron-vanguard" }) as string;
        string creature = method.Invoke(null, new object[] { "creature" }) as string;

        Require(humanoid == "Tools/MxFramework.Authoring/samples/character-codex-flow-test", "humanoid template should resolve to starter sample.");
        Require(defaultTemplate == "Tools/MxFramework.Authoring/samples/character-codex-flow-test", "empty template should resolve to starter humanoid sample.");
        Require(iron == "Tools/MxFramework.Authoring/samples/character-codex-flow-test", "Codex Flow Test should remain an explicit regression sample.");
        Require(creature == "Tools/MxFramework.Authoring/samples/character-slime", "creature template should still resolve to slime.");
    }

    private static void CodexFlowTestSample_UsesLineSkeletonBoneColliders()
    {
        CharacterResourcePackage package = LoadSample("character-codex-flow-test");
        CharacterAuthoringValidationReport report = CharacterResourcePackageValidator.Validate(package, new CharacterResourcePackageValidationOptions
        {
            PackageRootPath = FindSamplePath("character-codex-flow-test"),
            ValidateResourceFiles = true,
            ValidateResourceHashes = true
        });

        Require(!report.HasBlockingIssues, "Codex flow test sample should validate: " + report.ToText());
        AssertStarterBoneColliders(package, "Codex flow test");
        AssertCodexFlowTestSourceAssets(package, FindSamplePath("character-codex-flow-test"));

        CharacterResourcePlanCompileResult result = CharacterResourcePlanCompiler.Compile(new CharacterResourcePlanCompileRequest
        {
            Package = package,
            PackageRootPath = FindSamplePath("character-codex-flow-test"),
            ValidateResourceFiles = true,
            ValidateResourceHashes = true
        });

        Require(result.ResourceValidationReport.Status == "Ready", "Codex flow test resource plan should be ready.");
        Require(!result.ResourceValidationReport.HasErrors, "Codex flow test resource plan should not have errors.");
        Require(result.CharacterResourcePlan.SpawnCritical.Resources.Exists(resource => resource.ResourceKey == "char.codex_flow_test.model.body"), "Codex flow test plan should include body in SpawnCritical.");
        Require(result.CharacterResourcePlan.PresentationCritical.Resources.Exists(resource => resource.ResourceKey == "char.codex_flow_test.texture.body_albedo"), "Codex flow test plan should include albedo in PresentationCritical.");
        Require(!result.CharacterResourcePlan.AnimationWarmup.Resources.Exists(resource => resource.ResourceKey == "char.codex_flow_test.anim.locomotion"), "Codex flow test plan should not warm up legacy package-local locomotion GLB resource.");
        Require(result.RuntimeResourceCatalog.Entries.Exists(entry => entry.Id == "char.codex_flow_test.model.body"), "Codex flow test runtime catalog should include body model.");
        Require(!result.RuntimeResourceCatalog.Entries.Exists(entry => entry.Id == "char.codex_flow_test.anim.locomotion"), "Codex flow test runtime catalog should not include legacy package-local locomotion GLB.");
    }

    private static void CodexFlowTestSample_UsesStarterBoneColliderAliases()
    {
        CharacterResourcePackage package = LoadSample("character-codex-flow-test");
        CharacterAuthoringValidationReport report = CharacterResourcePackageValidator.Validate(package, new CharacterResourcePackageValidationOptions
        {
            PackageRootPath = FindSamplePath("character-codex-flow-test"),
            ValidateResourceFiles = true,
            ValidateResourceHashes = true
        });

        Require(!report.HasBlockingIssues, "Codex flow test sample should validate: " + report.ToText());
        AssertStarterBoneColliders(package, "Codex flow test");

        CharacterResourcePlanCompileResult result = CharacterResourcePlanCompiler.Compile(new CharacterResourcePlanCompileRequest
        {
            Package = package,
            PackageRootPath = FindSamplePath("character-codex-flow-test"),
            ValidateResourceFiles = true,
            ValidateResourceHashes = true
        });
        Require(result.ResourceValidationReport.Status == "Ready", "Codex flow test resource plan should be ready.");
        Require(!result.CharacterResourcePlan.AnimationWarmup.Resources.Exists(resource => resource.ResourceKey == "char.codex_flow_test.anim.locomotion"), "Codex flow test should not warm up legacy locomotion GLB resource.");
        Require(result.CharacterResourcePlan.PresentationCritical.Resources.Exists(resource => resource.ResourceKey == "char.codex_flow_test.texture.body_albedo"), "Codex flow test should keep texture as managed presentation resource.");
    }

    private static void CodexFlowTestSample_UsesPublicDefaultWeaponRefs()
    {
        string packagePath = FindSamplePath("character-codex-flow-test");
        CharacterResourcePackage package = LoadSample("character-codex-flow-test");
        AuthoringResourceCollection authoringResources = CharacterPackageCommands.BuildCharacterPackageAuthoringResources(FindRepoRoot(), packagePath, package, JsonOptions);
        CharacterWeaponSlotProfile mainHand = package.Geometry.WeaponSlots.Find(slot => slot.SlotId == "mainHand");
        CharacterWeaponSlotProfile offHand = package.Geometry.WeaponSlots.Find(slot => slot.SlotId == "offHand");
        SingleWeaponConfiguration sword = package.GlobalWeaponLibrary.SingleWeaponConfigurations.Find(
            definition => definition.SingleWeaponConfigurationId == "weapon.000001.main" || definition.SingleWeaponConfigurationId == "sword");
        SingleWeaponConfiguration shield = package.GlobalWeaponLibrary.SingleWeaponConfigurations.Find(
            definition => definition.SingleWeaponConfigurationId == "shield");

        Require(mainHand != null, "Codex flow test mainHand character slot should exist.");
        Require(offHand != null, "Codex flow test offHand character slot should exist.");
        Require(sword?.WeaponRef != null && (sword.WeaponRef.RuntimeResourceKey == "weapon.000001.model" || sword.WeaponRef.RuntimeResourceKey == "weapon.common_weapon_1.model"), "Codex flow test sword definition should reference the public weapon runtime key.");
        if (shield != null)
        {
            Require(shield.WeaponRef != null && shield.WeaponRef.RuntimeResourceKey == "weapon.common_kite_shield.model", "Codex flow test shield definition should reference the public shield runtime key.");
            Require(shield.WeaponRef.SourceProviderId == AuthoringResourceProviderIds.RuntimeCatalog, "Codex flow test shield should resolve from the production runtime catalog.");
        }
        Require(sword.TraceProfiles.Exists(trace => trace.TraceId == "trace.iron_sword.blade"),
            "Codex flow test sword should own its complete weapon-local trace profile.");
        Require(sword.WeaponRef.SourceProviderId == AuthoringResourceProviderIds.RuntimeCatalog || sword.WeaponRef.SourceProviderId == "resourceMapping", "Codex flow test sword should resolve from the production runtime catalog.");
        Require(mainHand.AttachSocketId == "mainHand" && offHand.AttachSocketId == "offHand",
            "Codex flow test character slots should retain only their matching Socket bindings.");
        Require(!package.ResourceCatalog.Entries.Exists(entry => entry.ResourceKey == "char.codex_flow_test.weapon.main_hand.model"), "Codex flow test should not own a package-local mainHand weapon resource.");
        Require(!package.ResourceCatalog.Entries.Exists(entry => entry.ResourceKey == "char.codex_flow_test.weapon.off_hand.model"), "Codex flow test should not own a package-local offHand weapon resource.");

        CharacterResourcePlanCompileResult result = CharacterResourcePlanCompiler.Compile(new CharacterResourcePlanCompileRequest
        {
            Package = package,
            PackageRootPath = packagePath,
            AuthoringResources = authoringResources,
            ValidateResourceFiles = true,
            ValidateResourceHashes = true
        });

        string expectedWeaponKey = sword?.WeaponRef?.RuntimeResourceKey ?? "weapon.000001.model";
        RuntimeResourceCatalogEntryDocument publicWeapon = result.RuntimeResourceCatalog.Entries.Find(entry => entry.Id == expectedWeaponKey || entry.Id == "weapon.common_weapon_1.model" || entry.Id == "weapon.000001.model");
        RuntimeResourceCatalogEntryDocument publicShield = result.RuntimeResourceCatalog.Entries.Find(entry => entry.Id == "weapon.common_kite_shield.model");
        Require(publicWeapon != null, "Codex flow test runtime catalog should include the public mainHand weapon.");
        Require(publicShield == null, "the retained sword/empty configurations should not preload the unused public shield.");
        Require(publicWeapon.PackageId == "common" || publicWeapon.PackageId == "weapon.000001" || publicWeapon.PackageId == "", "public mainHand weapon runtime catalog entry should keep the package identity.");
        Require(result.CharacterResourcePlan.EquipmentInitial.Resources.Exists(resource => (resource.ResourceKey == expectedWeaponKey || resource.ResourceKey == "weapon.common_weapon_1.model" || resource.ResourceKey == "weapon.000001.model")), "Codex flow test EquipmentInitial should include the public mainHand weapon.");
        Require(!result.CharacterResourcePlan.EquipmentInitial.Resources.Exists(resource => resource.ResourceKey == "weapon.common_kite_shield.model"), "Codex flow test EquipmentInitial should omit the unused public shield.");
        Require(!result.CharacterResourcePlan.EquipmentInitial.Resources.Exists(resource => resource.ResourceKey == "char.codex_flow_test.weapon.main_hand.model"), "Codex flow test EquipmentInitial should not include the legacy package-local mainHand weapon.");
        Require(!result.CharacterResourcePlan.EquipmentInitial.Resources.Exists(resource => resource.ResourceKey == "char.codex_flow_test.weapon.off_hand.model"), "Codex flow test EquipmentInitial should not include the legacy package-local offHand weapon.");
    }

    private static void CharacterActionGeneratedSamples_HaveConsistentReactionProfileHandoff()
    {
        string root = FindRepoRoot();
        string actionSetRoot = Path.Combine(root, "Assets", "MxFrameworkGenerated", "CharacterActions", "ActionSets");
        string[] files = Directory.GetFiles(actionSetRoot, "character_action_sets.json", SearchOption.AllDirectories);
        Array.Sort(files, StringComparer.Ordinal);
        for (int i = 0; i < files.Length; i++)
        {
            string packageSegment = new DirectoryInfo(Path.GetDirectoryName(files[i]) ?? string.Empty).Name;
            CharacterActionHandoffConsistencyReport report =
                MxFramework.Authoring.Cli.EditorServer.ReadCharacterActionHandoffConsistency(root, packageSegment);
            Require(!report.HasBlockingIssues, packageSegment + " CharacterAction reaction handoff should be consistent: " + FormatHandoffIssues(report));
        }
    }

    private static string FormatHandoffIssues(CharacterActionHandoffConsistencyReport report)
    {
        if (report == null || report.Issues.Count == 0)
            return string.Empty;

        return string.Join(" | ", report.Issues.ConvertAll(issue => issue.Code + " " + issue.SourcePath + " " + issue.SourceObjectPath + " " + issue.Message));
    }

    private static void ResourceDependencyGraph_MissingDependencyBlocksImport()
    {
        CharacterResourcePackage package = LoadSample("character-codex-flow-test");
        package.ResourceCatalog.Entries[0].Dependencies.Add(new CharacterPackageResourceDependency
        {
            ResourceKey = "char.codex_flow_test.missing.texture",
            Required = true,
            Relation = "uses"
        });

        CharacterAuthoringValidationReport report = CharacterResourcePackageValidator.Validate(package);

        Require(report.HasBlockingIssues, "missing resource dependency should block import.");
        Require(report.Issues.Exists(issue =>
            issue.Code == CharacterAuthoringValidationCodes.MissingResourceDependency &&
            issue.Gate == CharacterAuthoringValidationGate.ImportBlocked), "missing dependency should produce stable ImportBlocked issue.");
    }

    private static void ResourceDependencyGraph_DuplicateDependencyProducesDiagnostic()
    {
        CharacterResourcePackage package = LoadSample("character-codex-flow-test");
        CharacterPackageResourceEntry previewMesh = package.ResourceCatalog.Entries.Find(entry => entry.ResourceKey == "char.codex_flow_test.preview.body_mesh");
        Require(previewMesh != null, "preview mesh resource missing from sample.");
        previewMesh.Dependencies.Add(new CharacterPackageResourceDependency
        {
            ResourceKey = "char.codex_flow_test.model.body",
            Required = true,
            Relation = "derivedFrom"
        });
        previewMesh.Dependencies.Add(new CharacterPackageResourceDependency
        {
            ResourceKey = "char.codex_flow_test.model.body",
            Required = true,
            Relation = "derivedFrom"
        });

        CharacterAuthoringValidationReport report = CharacterResourcePackageValidator.Validate(package);
        CharacterPackageDependencyGraph graph = CharacterPackageResourcePipeline.BuildDependencyGraph(package.ResourceCatalog);

        Require(report.Issues.Exists(issue => issue.Code == CharacterAuthoringValidationCodes.DuplicateResourceDependency), "duplicate dependency should produce stable diagnostic.");
        Require(graph.Edges.Exists(edge => edge.FromResourceKey == "char.codex_flow_test.preview.body_mesh" && edge.ToResourceKey == "char.codex_flow_test.model.body"), "dependency graph should include preview mesh -> body edge.");
    }

    private static void ResourceHashMismatch_BlocksImport()
    {
        CharacterResourcePackage package = LoadSample("character-codex-flow-test");
        package.ResourceCatalog.Entries[0].Hashes.ContentHash = "sha256:0000000000000000000000000000000000000000000000000000000000000000";

        CharacterAuthoringValidationReport report = CharacterResourcePackageValidator.Validate(package, new CharacterResourcePackageValidationOptions
        {
            PackageRootPath = FindSamplePath("character-codex-flow-test"),
            ValidateResourceFiles = true,
            ValidateResourceHashes = true
        });

        Require(report.HasBlockingIssues, "hash mismatch should block import.");
        Require(report.Issues.Exists(issue =>
            issue.Code == CharacterAuthoringValidationCodes.ResourceHashMismatch &&
            issue.Gate == CharacterAuthoringValidationGate.ImportBlocked), "hash mismatch should produce stable ImportBlocked issue.");
    }

    private static void MissingResourceFile_BlocksImport()
    {
        CharacterResourcePackage package = LoadSample("character-codex-flow-test");
        package.ResourceCatalog.Entries[0].RelativePath = "resources/models/missing.glb";

        CharacterAuthoringValidationReport report = CharacterResourcePackageValidator.Validate(package, new CharacterResourcePackageValidationOptions
        {
            PackageRootPath = FindSamplePath("character-codex-flow-test"),
            ValidateResourceFiles = true
        });

        Require(report.HasBlockingIssues, "missing resource file should block import.");
        Require(report.Issues.Exists(issue =>
            issue.Code == CharacterAuthoringValidationCodes.MissingResourceFile &&
            issue.Gate == CharacterAuthoringValidationGate.ImportBlocked), "missing resource file should produce stable ImportBlocked issue.");
    }

    private static void InvalidImportTargetPath_BlocksImport()
    {
        CharacterResourcePackage package = LoadSample("character-codex-flow-test");
        package.ResourceCatalog.Entries[0].ImportHints.TargetRelativePath = "../outside.glb";

        CharacterAuthoringValidationReport report = CharacterResourcePackageValidator.Validate(package);

        Require(report.HasBlockingIssues, "invalid Unity target path should block import.");
        Require(report.Issues.Exists(issue =>
            issue.Code == CharacterAuthoringValidationCodes.InvalidImportTargetPath &&
            issue.Gate == CharacterAuthoringValidationGate.ImportBlocked), "invalid import target path should produce stable ImportBlocked issue.");
    }

    private static void UnsupportedConvexShape_ProducesExportBlockedIssue()
    {
        CharacterResourcePackage package = LoadSample("character-codex-flow-test");
        package.Geometry.Colliders[0].Shape = CharacterColliderShape.Convex;

        CharacterAuthoringValidationReport report = CharacterResourcePackageValidator.Validate(package);

        Require(report.HasBlockingIssues, "convex shape should block v1 export/importable artifact.");
        Require(report.Issues.Exists(issue =>
            issue.Code == CharacterAuthoringValidationCodes.UnsupportedColliderShape &&
            issue.Gate == CharacterAuthoringValidationGate.ExportBlocked &&
            issue.Field == "shape"), "unsupported convex shape should produce stable ExportBlocked issue.");
    }

    private static void SlimeSample_UsesSameDtoForPrimitiveBody()
    {
        CharacterResourcePackage package = LoadSample("character-slime");
        package.ApplicationConfig.SchemaVersion = "1.0";
        CharacterAuthoringValidationReport report = CharacterResourcePackageValidator.Validate(package);

        Require(report.HasBlockingIssues && report.Issues.Exists(issue => issue.Code == CharacterAuthoringValidationCodes.WeaponConfigurationUpgradeRequired),
            "Schema 1.0 slime should remain readable but require a 2.0 weapon-configuration draft before compile/import.");
        Require(package.Geometry.BodyProfile.BodyKind == CharacterAuthoringBodyKind.Primitive, "slime should be primitive body kind.");
        Require(package.Geometry.BodyParts.Exists(part => part.PartId == "core" && part.PartKind == CharacterAuthoringBodyPartKind.Primitive), "slime core part missing.");
        Require(package.Geometry.Colliders.Exists(collider => collider.Shape == CharacterColliderShape.Sphere && collider.PartId == "core"), "slime core sphere collider missing.");
        Require(package.Geometry.Sockets.Exists(socket => socket.SocketId == "frontAttack" && socket.Usage == CharacterSocketUsage.Gameplay), "slime gameplay socket missing.");
    }

}
