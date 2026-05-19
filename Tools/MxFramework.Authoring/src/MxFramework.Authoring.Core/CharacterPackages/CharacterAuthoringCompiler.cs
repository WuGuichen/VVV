using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace MxFramework.Authoring
{
    public static class CharacterAuthoringCompilerFormats
    {
        public const string CompileResult = "mx.characterAuthoringCompileResult.v1";
        public const string ConfigPatchBundle = "mx.characterApplicationConfigPatchBundle.v1";
        public const string GeometryBinding = "mx.characterGeometryBinding.v1";
        public const string ResourceMapping = "mx.characterResourceMapping.v1";
        public const string UnityWritePlan = "mx.characterUnityImportWritePlan.v1";
    }

    public static class CharacterAuthoringCompilerWriteKinds
    {
        public const string GeneratedConfigPatch = "generatedConfigPatch";
        public const string GeometryBinding = "geometryBinding";
        public const string ResourceMapping = "resourceMapping";
        public const string ResourceFile = "resourceFile";
    }

    public static class CharacterAuthoringCompilerOwnerKinds
    {
        public const string ExternalEditor = "ExternalEditor";
        public const string AuthoringCompiler = "AuthoringCompiler";
        public const string UnityImporter = "UnityImporter";
    }

    public static class CharacterApplicationCompilerTableNames
    {
        public const string CharacterConfig = "CharacterConfig";
        public const string CharacterAttributeProfileConfig = "CharacterAttributeProfileConfig";
        public const string CharacterBodyProfileConfig = "CharacterBodyProfileConfig";
        public const string CharacterBodyPartConfig = "CharacterBodyPartConfig";
        public const string EquipmentSchemaConfig = "EquipmentSchemaConfig";
        public const string EquipmentLoadoutConfig = "EquipmentLoadoutConfig";
        public const string EquipmentStateConfig = "EquipmentStateConfig";
        public const string WeaponConfig = "WeaponConfig";
        public const string AbilityLoadoutConfig = "AbilityLoadoutConfig";
        public const string CombatActionSetConfig = "CombatActionSetConfig";
        public const string CharacterPresentationProfileConfig = "CharacterPresentationProfileConfig";
        public const string SpawnProfileConfig = "SpawnProfileConfig";
    }

    public static class CharacterAuthoringCompilerValidationCodes
    {
        public const string CoordinateTargetMismatch = "CHARPKG_COORDINATE_TARGET_MISMATCH";
        public const string ResourceKeyConflict = "CHARPKG_RESOURCE_KEY_CONFLICT";
        public const string StrictWarningBlocked = "CHARPKG_STRICT_WARNING_BLOCKED";
    }

    public enum CharacterAuthoringCompilerStatus
    {
        Ready = 0,
        WarningOnly = 1,
        SpawnBlocked = 2,
        ImportBlocked = 3,
        ExportBlocked = 4
    }

    public sealed class CharacterAuthoringCompileOptions
    {
        public bool Strict { get; set; }
        public bool AllowWarnings { get; set; } = true;
        public bool ValidateResourceFiles { get; set; }
        public bool ValidateResourceHashes { get; set; }
        public string TargetOutputFormat { get; set; } = CharacterAuthoringCompilerFormats.ConfigPatchBundle;
        public string TargetUnityPathPolicy { get; set; } = CharacterPackageImportTargetPathPolicies.GeneratedCharacterPackage;
        public string GeneratedRootPath { get; set; } = "Assets/MxFrameworkGenerated/CharacterPackages";
        public CharacterPackageCoordinateConvention TargetCoordinateConvention { get; set; } = new CharacterPackageCoordinateConvention();
    }

    public sealed class CharacterAuthoringCompileRequest
    {
        public CharacterResourcePackage Package { get; set; }
        public string PackageRootPath { get; set; } = string.Empty;
        public ProjectAuthoringManifest ConfigSourceIndex { get; set; }
        public CharacterPackageResourceCatalog ExistingProjectResourceCatalogSummary { get; set; }
        public CharacterAuthoringCompileOptions Options { get; set; } = new CharacterAuthoringCompileOptions();
    }

    public sealed class CharacterAuthoringCompileResult
    {
        public string Format { get; set; } = CharacterAuthoringCompilerFormats.CompileResult;
        public string SchemaVersion { get; set; } = "1.0";
        public string PackageId { get; set; } = string.Empty;
        public string PackageStableId { get; set; } = string.Empty;
        public bool IsDeterministicFullCompile { get; set; } = true;
        public CharacterAuthoringCompilerStatus Status { get; set; }
        public CharacterCompilerHashSet Hashes { get; set; } = new CharacterCompilerHashSet();
        public CharacterCompilerGateReport GateReport { get; set; } = new CharacterCompilerGateReport();
        public CharacterAuthoringCompiledConfigPatch GeneratedConfigPatch { get; set; } = new CharacterAuthoringCompiledConfigPatch();
        public CharacterAuthoringGeometryBinding GeometryBinding { get; set; } = new CharacterAuthoringGeometryBinding();
        public CharacterPackageResourceMapping ResourceMapping { get; set; } = new CharacterPackageResourceMapping();
        public CharacterUnityImportWritePlan UnityImportWritePlan { get; set; } = new CharacterUnityImportWritePlan();
        public CharacterResolverVerificationPlan ResolverVerificationPlan { get; set; } = new CharacterResolverVerificationPlan();
        public CharacterPackageDependencyGraph DependencyGraph { get; set; } = new CharacterPackageDependencyGraph();
        public CharacterPackageResourceHashReport ResourceHashReport { get; set; } = new CharacterPackageResourceHashReport();
        public List<CharacterPackageSourceMapping> SourceMappings { get; set; } = new List<CharacterPackageSourceMapping>();
    }

    public sealed class CharacterCompilerHashSet
    {
        public string Algorithm { get; set; } = "sha256";
        public string SourcePackageHash { get; set; } = string.Empty;
        public string GeneratedConfigHash { get; set; } = string.Empty;
        public string GeometryBindingHash { get; set; } = string.Empty;
        public string ResourceMappingHash { get; set; } = string.Empty;
        public string WritePlanHash { get; set; } = string.Empty;
    }

    public sealed class CharacterCompilerGateReport
    {
        public string SchemaVersion { get; set; } = "1.0";
        public string PackageId { get; set; } = string.Empty;
        public CharacterAuthoringCompilerStatus Status { get; set; }
        public bool ExportBlocked { get; set; }
        public bool ImportBlocked { get; set; }
        public bool SpawnBlocked { get; set; }
        public bool WarningOnly { get; set; }
        public List<CharacterAuthoringValidationIssue> Issues { get; set; } = new List<CharacterAuthoringValidationIssue>();

        public bool HasGate(CharacterAuthoringValidationGate gate)
        {
            for (int i = 0; i < Issues.Count; i++)
            {
                if (Issues[i] != null && Issues[i].Gate == gate)
                    return true;
            }

            return false;
        }
    }

    public sealed class CharacterAuthoringCompiledConfigPatch
    {
        public string Format { get; set; } = CharacterAuthoringCompilerFormats.ConfigPatchBundle;
        public string SchemaVersion { get; set; } = "1.0";
        public string PackageId { get; set; } = string.Empty;
        public string Layer { get; set; } = "Generated";
        public PatchDocument Patch { get; set; } = new PatchDocument();
        public List<CharacterGeneratedConfigReference> GeneratedReferences { get; set; } = new List<CharacterGeneratedConfigReference>();
    }

    public sealed class CharacterGeneratedConfigReference
    {
        public string TableName { get; set; } = string.Empty;
        public int GeneratedId { get; set; }
        public string StableId { get; set; } = string.Empty;
        public string SourcePath { get; set; } = string.Empty;
        public string SourceObjectPath { get; set; } = string.Empty;
    }

    public sealed class CharacterAuthoringGeometryBinding
    {
        public string Format { get; set; } = CharacterAuthoringCompilerFormats.GeometryBinding;
        public string SchemaVersion { get; set; } = "1.0";
        public string PackageId { get; set; } = string.Empty;
        public string BodyProfileStableId { get; set; } = string.Empty;
        public CharacterCoordinateConversionPlan CoordinateConversion { get; set; } = new CharacterCoordinateConversionPlan();
        public CharacterBodyGeometryProfile BodyProfile { get; set; } = new CharacterBodyGeometryProfile();
        public List<CharacterBodyColliderBinding> BodyColliders { get; set; } = new List<CharacterBodyColliderBinding>();
        public List<CharacterHitZoneBinding> HitZoneBindings { get; set; } = new List<CharacterHitZoneBinding>();
        public List<CharacterSocketBinding> Sockets { get; set; } = new List<CharacterSocketBinding>();
        public List<CharacterWeaponAttachmentBinding> WeaponAttachments { get; set; } = new List<CharacterWeaponAttachmentBinding>();
        public List<CharacterWeaponTraceBinding> WeaponTraces { get; set; } = new List<CharacterWeaponTraceBinding>();
    }

    public sealed class CharacterCoordinateConversionPlan
    {
        public CharacterPackageCoordinateConvention SourceConvention { get; set; } = new CharacterPackageCoordinateConvention();
        public CharacterPackageCoordinateConvention TargetConvention { get; set; } = new CharacterPackageCoordinateConvention();
        public bool RequiresConversion { get; set; }
        public float PositionScale { get; set; } = 1f;
        public string AxisConversion { get; set; } = "identity";
        public string RotationConversion { get; set; } = "identity";
    }

    public sealed class CharacterBodyColliderBinding
    {
        public string ColliderId { get; set; } = string.Empty;
        public string PartId { get; set; } = string.Empty;
        public string HitZoneId { get; set; } = string.Empty;
        public CharacterColliderShape Shape { get; set; }
        public CharacterAuthoringLocalPose LocalPose { get; set; } = new CharacterAuthoringLocalPose();
        public CharacterAuthoringVector3 Size { get; set; } = new CharacterAuthoringVector3();
        public float Radius { get; set; }
        public float Height { get; set; }
        public int Priority { get; set; }
        public bool IsWeakPoint { get; set; }
        public float DamageMultiplierOverride { get; set; }
        public float PostureDamageScaleOverride { get; set; }
        public string PhysicsLayer { get; set; } = string.Empty;
        public string MaterialStableId { get; set; } = string.Empty;
        public string SourcePath { get; set; } = "geometry/body_colliders.json";
        public string SourceObjectPath { get; set; } = string.Empty;
    }

    public sealed class CharacterHitZoneBinding
    {
        public string HitZoneId { get; set; } = string.Empty;
        public string PartId { get; set; } = string.Empty;
        public int Priority { get; set; }
        public bool IsWeakPoint { get; set; }
        public float DamageMultiplierOverride { get; set; }
        public float PostureDamageScaleOverride { get; set; }
        public string SourcePath { get; set; } = "geometry/body_colliders.json";
        public string SourceObjectPath { get; set; } = string.Empty;
    }

    public sealed class CharacterSocketBinding
    {
        public string SocketId { get; set; } = string.Empty;
        public string ParentPartId { get; set; } = string.Empty;
        public string BonePath { get; set; } = string.Empty;
        public string LocatorPath { get; set; } = string.Empty;
        public CharacterAuthoringLocalPose LocalPose { get; set; } = new CharacterAuthoringLocalPose();
        public CharacterSocketUsage Usage { get; set; }
        public string SourcePath { get; set; } = "geometry/sockets.json";
        public string SourceObjectPath { get; set; } = string.Empty;
    }

    public sealed class CharacterWeaponAttachmentBinding
    {
        public string WeaponId { get; set; } = string.Empty;
        public string EquipSlot { get; set; } = string.Empty;
        public string AttachSocketId { get; set; } = string.Empty;
        public CharacterAuthoringLocalPose LocalGripPose { get; set; } = new CharacterAuthoringLocalPose();
        public string PreviewResourceKey { get; set; } = string.Empty;
        public string TraceId { get; set; } = string.Empty;
        public string SourcePath { get; set; } = "geometry/weapon_attachments.json";
        public string SourceObjectPath { get; set; } = string.Empty;
    }

    public sealed class CharacterWeaponTraceBinding
    {
        public string TraceId { get; set; } = string.Empty;
        public string WeaponId { get; set; } = string.Empty;
        public string EquipSlot { get; set; } = string.Empty;
        public string StartLocatorPath { get; set; } = string.Empty;
        public string EndLocatorPath { get; set; } = string.Empty;
        public CharacterAuthoringLocalPose StartPose { get; set; } = new CharacterAuthoringLocalPose();
        public CharacterAuthoringLocalPose EndPose { get; set; } = new CharacterAuthoringLocalPose();
        public float Radius { get; set; }
        public WeaponTraceSampleRule SampleRule { get; set; }
        public int FixedSampleCount { get; set; }
        public List<string> ActionKeys { get; set; } = new List<string>();
        public string SourcePath { get; set; } = "geometry/traces.json";
        public string SourceObjectPath { get; set; } = string.Empty;
    }

    public sealed class CharacterPackageResourceMapping
    {
        public string Format { get; set; } = CharacterAuthoringCompilerFormats.ResourceMapping;
        public string SchemaVersion { get; set; } = "1.0";
        public string PackageId { get; set; } = string.Empty;
        public List<CharacterPackageResourceMappingEntry> Entries { get; set; } = new List<CharacterPackageResourceMappingEntry>();
    }

    public sealed class CharacterPackageResourceMappingEntry
    {
        public string PackageResourceKey { get; set; } = string.Empty;
        public string ProjectResourceKey { get; set; } = string.Empty;
        public string StableId { get; set; } = string.Empty;
        public string TypeId { get; set; } = string.Empty;
        public string Usage { get; set; } = string.Empty;
        public string SourceFormat { get; set; } = string.Empty;
        public string SourceRelativePath { get; set; } = string.Empty;
        public string DeclaredContentHash { get; set; } = string.Empty;
        public string ImportHash { get; set; } = string.Empty;
        public string DependencyHash { get; set; } = string.Empty;
        public string ProviderId { get; set; } = string.Empty;
        public string TargetPathPolicy { get; set; } = string.Empty;
        public string ImportTargetPath { get; set; } = string.Empty;
        public CharacterAuthoringLocalPose ModelWrapperPose { get; set; } = new CharacterAuthoringLocalPose();
        public string ConflictAction { get; set; } = string.Empty;
    }

    public sealed class CharacterUnityImportWritePlan
    {
        public string Format { get; set; } = CharacterAuthoringCompilerFormats.UnityWritePlan;
        public string SchemaVersion { get; set; } = "1.0";
        public string PackageId { get; set; } = string.Empty;
        public string TargetRootPath { get; set; } = string.Empty;
        public string TargetPathPolicy { get; set; } = string.Empty;
        public bool CanWriteToUnityProject { get; set; }
        public bool CanSpawnAfterImport { get; set; }
        public List<CharacterUnityImportWriteEntry> Writes { get; set; } = new List<CharacterUnityImportWriteEntry>();
    }

    public sealed class CharacterUnityImportWriteEntry
    {
        public string Kind { get; set; } = string.Empty;
        public string Owner { get; set; } = string.Empty;
        public string SourcePath { get; set; } = string.Empty;
        public string TargetPath { get; set; } = string.Empty;
        public string WritePolicy { get; set; } = "Recreate";
        public string ContentHash { get; set; } = string.Empty;
    }

    public sealed class CharacterResolverVerificationPlan
    {
        public string SchemaVersion { get; set; } = "1.0";
        public string ExpectedResolverEntrypoint { get; set; } = "CharacterPackageResolver.Resolve";
        public string Status { get; set; } = "NotReady";
        public string CharacterStableId { get; set; } = string.Empty;
        public string DefaultLoadoutStableId { get; set; } = string.Empty;
        public string ExpectedActiveEquipmentStateStableId { get; set; } = string.Empty;
        public string ExpectedCombatActionSetStableId { get; set; } = string.Empty;
        public string ExpectedAnimationProfileId { get; set; } = string.Empty;
        public List<CharacterResolverTableRequirement> RequiredTables { get; set; } = new List<CharacterResolverTableRequirement>();
        public List<int> KnownAbilityIds { get; set; } = new List<int>();
        public List<string> RequiredResourceKeys { get; set; } = new List<string>();
        public List<CharacterAuthoringValidationIssue> Diagnostics { get; set; } = new List<CharacterAuthoringValidationIssue>();
    }

    public sealed class CharacterResolverTableRequirement
    {
        public string TableName { get; set; } = string.Empty;
        public int RowCount { get; set; }
        public List<int> GeneratedIds { get; set; } = new List<int>();
        public List<string> StableIds { get; set; } = new List<string>();
    }

    public sealed class CharacterPackageSourceMapping
    {
        public string SourcePath { get; set; } = string.Empty;
        public string SourceObjectPath { get; set; } = string.Empty;
        public string TargetKind { get; set; } = string.Empty;
        public string TargetPath { get; set; } = string.Empty;
        public string TargetField { get; set; } = string.Empty;
    }

    public static class CharacterAuthoringCompiler
    {
        public static CharacterAuthoringCompileResult Compile(CharacterAuthoringCompileRequest request)
        {
            if (request == null)
                request = new CharacterAuthoringCompileRequest();

            CharacterAuthoringCompileOptions options = request.Options ?? new CharacterAuthoringCompileOptions();
            CharacterResourcePackage package = request.Package;
            var result = new CharacterAuthoringCompileResult();
            result.PackageId = package != null && package.Manifest != null ? package.Manifest.PackageId : string.Empty;
            result.PackageStableId = package != null && package.Manifest != null ? package.Manifest.StableId : string.Empty;

            CharacterAuthoringValidationReport validation = CharacterResourcePackageValidator.Validate(package, new CharacterResourcePackageValidationOptions
            {
                PackageRootPath = request.PackageRootPath,
                ValidateResourceFiles = options.ValidateResourceFiles || options.ValidateResourceHashes,
                ValidateResourceHashes = options.ValidateResourceHashes
            });

            var issues = new List<CharacterAuthoringValidationIssue>();
            if (validation != null && validation.Issues != null)
                issues.AddRange(validation.Issues);

            AddCoordinateDiagnostics(package, options, issues);
            AddResourceConflictDiagnostics(package, request.ExistingProjectResourceCatalogSummary, issues);
            AddStrictDiagnostics(options, issues);

            result.DependencyGraph = CharacterPackageResourcePipeline.BuildDependencyGraph(package != null ? package.ResourceCatalog : null);
            result.ResourceHashReport = options.ValidateResourceFiles || options.ValidateResourceHashes
                ? CharacterPackageResourcePipeline.BuildHashReport(package, request.PackageRootPath)
                : new CharacterPackageResourceHashReport { PackageId = result.PackageId };
            result.GateReport = BuildGateReport(result.PackageId, issues);
            result.Status = result.GateReport.Status;
            result.GeometryBinding = BuildGeometryBinding(package, options);
            result.ResourceMapping = BuildResourceMapping(package, options);
            result.GeneratedConfigPatch = BuildConfigPatch(package, result.GeometryBinding, result.ResourceMapping);
            result.UnityImportWritePlan = BuildWritePlan(package, options, result.GateReport, result.ResourceMapping, result.GeneratedConfigPatch);
            result.ResolverVerificationPlan = BuildResolverVerificationPlan(package.ApplicationConfig, result.GeneratedConfigPatch, result.GateReport, result.ResourceMapping);
            result.SourceMappings = BuildSourceMappings(result.GeneratedConfigPatch, result.GeometryBinding, result.ResourceMapping);
            result.Hashes = BuildHashes(package, result.GeneratedConfigPatch, result.GeometryBinding, result.ResourceMapping, result.UnityImportWritePlan);
            result.UnityImportWritePlan.Writes.Insert(0, new CharacterUnityImportWriteEntry
            {
                Kind = CharacterAuthoringCompilerWriteKinds.GeneratedConfigPatch,
                Owner = CharacterAuthoringCompilerOwnerKinds.AuthoringCompiler,
                SourcePath = "compiler/generated_config_patch.json",
                TargetPath = CombineProjectPath(result.UnityImportWritePlan.TargetRootPath, "generated/character_application_config_patch.json"),
                WritePolicy = "Recreate",
                ContentHash = result.Hashes.GeneratedConfigHash
            });
            result.UnityImportWritePlan.Writes.Insert(1, new CharacterUnityImportWriteEntry
            {
                Kind = CharacterAuthoringCompilerWriteKinds.GeometryBinding,
                Owner = CharacterAuthoringCompilerOwnerKinds.AuthoringCompiler,
                SourcePath = "compiler/geometry_binding.json",
                TargetPath = CombineProjectPath(result.UnityImportWritePlan.TargetRootPath, "generated/character_geometry_binding.json"),
                WritePolicy = "Recreate",
                ContentHash = result.Hashes.GeometryBindingHash
            });
            result.UnityImportWritePlan.Writes.Insert(2, new CharacterUnityImportWriteEntry
            {
                Kind = CharacterAuthoringCompilerWriteKinds.ResourceMapping,
                Owner = CharacterAuthoringCompilerOwnerKinds.AuthoringCompiler,
                SourcePath = "compiler/resource_mapping.json",
                TargetPath = CombineProjectPath(result.UnityImportWritePlan.TargetRootPath, "generated/character_resource_mapping.json"),
                WritePolicy = "Recreate",
                ContentHash = result.Hashes.ResourceMappingHash
            });

            return result;
        }

        private static CharacterCompilerGateReport BuildGateReport(string packageId, List<CharacterAuthoringValidationIssue> issues)
        {
            var report = new CharacterCompilerGateReport { PackageId = packageId ?? string.Empty };
            if (issues != null)
                report.Issues.AddRange(issues);

            for (int i = 0; i < report.Issues.Count; i++)
            {
                CharacterAuthoringValidationIssue issue = report.Issues[i];
                if (issue == null)
                    continue;

                if (issue.Gate == CharacterAuthoringValidationGate.ExportBlocked)
                    report.ExportBlocked = true;
                else if (issue.Gate == CharacterAuthoringValidationGate.ImportBlocked)
                    report.ImportBlocked = true;
                else if (issue.Gate == CharacterAuthoringValidationGate.SpawnBlocked)
                    report.SpawnBlocked = true;
                else if (issue.Gate == CharacterAuthoringValidationGate.WarningOnly || issue.Severity == CharacterAuthoringValidationSeverity.Warning)
                    report.WarningOnly = true;
            }

            if (report.ExportBlocked)
                report.Status = CharacterAuthoringCompilerStatus.ExportBlocked;
            else if (report.ImportBlocked)
                report.Status = CharacterAuthoringCompilerStatus.ImportBlocked;
            else if (report.SpawnBlocked)
                report.Status = CharacterAuthoringCompilerStatus.SpawnBlocked;
            else if (report.WarningOnly)
                report.Status = CharacterAuthoringCompilerStatus.WarningOnly;
            else
                report.Status = CharacterAuthoringCompilerStatus.Ready;

            return report;
        }

        private static CharacterAuthoringGeometryBinding BuildGeometryBinding(CharacterResourcePackage package, CharacterAuthoringCompileOptions options)
        {
            var binding = new CharacterAuthoringGeometryBinding();
            if (package == null)
                return binding;

            binding.PackageId = package.Manifest != null ? package.Manifest.PackageId : string.Empty;
            binding.BodyProfileStableId = GetBodyProfileStableId(package);
            binding.CoordinateConversion = BuildCoordinatePlan(package.Manifest != null ? package.Manifest.CoordinateConvention : null, options.TargetCoordinateConvention);
            CharacterAuthoringGeometry geometry = package.Geometry ?? new CharacterAuthoringGeometry();
            binding.BodyProfile = geometry.BodyProfile ?? new CharacterBodyGeometryProfile();

            for (int i = 0; i < geometry.Colliders.Count; i++)
            {
                CharacterBodyColliderProfile collider = geometry.Colliders[i];
                if (collider == null)
                    continue;

                string objectPath = "geometry/colliders/" + (!string.IsNullOrWhiteSpace(collider.ColliderId) ? collider.ColliderId : i.ToString(CultureInfo.InvariantCulture));
                binding.BodyColliders.Add(new CharacterBodyColliderBinding
                {
                    ColliderId = collider.ColliderId,
                    PartId = collider.PartId,
                    HitZoneId = collider.HitZoneId,
                    Shape = collider.Shape,
                    LocalPose = collider.LocalPose ?? new CharacterAuthoringLocalPose(),
                    Size = collider.Size ?? new CharacterAuthoringVector3(),
                    Radius = collider.Radius,
                    Height = collider.Height,
                    Priority = collider.Priority,
                    IsWeakPoint = collider.IsWeakPoint,
                    DamageMultiplierOverride = collider.DamageMultiplierOverride,
                    PostureDamageScaleOverride = collider.PostureDamageScaleOverride,
                    PhysicsLayer = collider.PhysicsLayer,
                    MaterialStableId = collider.MaterialStableId,
                    SourceObjectPath = objectPath
                });
                binding.HitZoneBindings.Add(new CharacterHitZoneBinding
                {
                    HitZoneId = collider.HitZoneId,
                    PartId = collider.PartId,
                    Priority = collider.Priority,
                    IsWeakPoint = collider.IsWeakPoint,
                    DamageMultiplierOverride = collider.DamageMultiplierOverride,
                    PostureDamageScaleOverride = collider.PostureDamageScaleOverride,
                    SourceObjectPath = objectPath
                });
            }

            for (int i = 0; i < geometry.Sockets.Count; i++)
            {
                CharacterSocketProfile socket = geometry.Sockets[i];
                if (socket == null)
                    continue;

                binding.Sockets.Add(new CharacterSocketBinding
                {
                    SocketId = socket.SocketId,
                    ParentPartId = socket.ParentPartId,
                    BonePath = socket.BonePath,
                    LocatorPath = socket.LocatorPath,
                    LocalPose = socket.LocalPose ?? new CharacterAuthoringLocalPose(),
                    Usage = socket.Usage,
                    SourceObjectPath = "geometry/sockets/" + (!string.IsNullOrWhiteSpace(socket.SocketId) ? socket.SocketId : i.ToString(CultureInfo.InvariantCulture))
                });
            }

            for (int i = 0; i < geometry.WeaponAttachments.Count; i++)
            {
                WeaponAttachmentProfile attachment = geometry.WeaponAttachments[i];
                if (attachment == null)
                    continue;

                binding.WeaponAttachments.Add(new CharacterWeaponAttachmentBinding
                {
                    WeaponId = attachment.WeaponId,
                    EquipSlot = attachment.EquipSlot,
                    AttachSocketId = attachment.AttachSocketId,
                    LocalGripPose = attachment.LocalGripPose ?? new CharacterAuthoringLocalPose(),
                    PreviewResourceKey = attachment.PreviewResourceKey,
                    TraceId = attachment.TraceId,
                    SourceObjectPath = "geometry/weaponAttachments/" + (!string.IsNullOrWhiteSpace(attachment.WeaponId) ? attachment.WeaponId : i.ToString(CultureInfo.InvariantCulture))
                });
            }

            for (int i = 0; i < geometry.Traces.Count; i++)
            {
                WeaponTraceProfile trace = geometry.Traces[i];
                if (trace == null)
                    continue;

                binding.WeaponTraces.Add(new CharacterWeaponTraceBinding
                {
                    TraceId = trace.TraceId,
                    WeaponId = trace.WeaponId,
                    EquipSlot = trace.EquipSlot,
                    StartLocatorPath = trace.StartLocatorPath,
                    EndLocatorPath = trace.EndLocatorPath,
                    StartPose = trace.StartPose ?? new CharacterAuthoringLocalPose(),
                    EndPose = trace.EndPose ?? new CharacterAuthoringLocalPose(),
                    Radius = trace.Radius,
                    SampleRule = trace.SampleRule,
                    FixedSampleCount = trace.FixedSampleCount,
                    ActionKeys = trace.ActionKeys != null ? new List<string>(trace.ActionKeys) : new List<string>(),
                    SourceObjectPath = "geometry/traces/" + (!string.IsNullOrWhiteSpace(trace.TraceId) ? trace.TraceId : i.ToString(CultureInfo.InvariantCulture))
                });
            }

            return binding;
        }

        private static CharacterPackageResourceMapping BuildResourceMapping(CharacterResourcePackage package, CharacterAuthoringCompileOptions options)
        {
            var mapping = new CharacterPackageResourceMapping();
            if (package == null)
                return mapping;

            mapping.PackageId = package.Manifest != null ? package.Manifest.PackageId : string.Empty;
            CharacterPackageResourceCatalog catalog = package.ResourceCatalog ?? new CharacterPackageResourceCatalog();
            for (int i = 0; i < catalog.Entries.Count; i++)
            {
                CharacterPackageResourceEntry entry = catalog.Entries[i];
                if (entry == null)
                    continue;

                string targetRelativePath = GetImportTargetRelativePath(entry);
                mapping.Entries.Add(new CharacterPackageResourceMappingEntry
                {
                    PackageResourceKey = entry.ResourceKey,
                    ProjectResourceKey = entry.ResourceKey,
                    StableId = entry.StableId,
                    TypeId = entry.TypeId,
                    Usage = entry.Usage,
                    SourceFormat = CharacterPackageResourcePipeline.GetEffectiveSourceFormat(entry),
                    SourceRelativePath = entry.RelativePath,
                    DeclaredContentHash = CharacterPackageResourcePipeline.GetDeclaredContentHash(entry),
                    ImportHash = CharacterPackageResourcePipeline.ComputeImportHash(entry),
                    DependencyHash = CharacterPackageResourcePipeline.ComputeDependencyHash(entry, catalog),
                    ProviderId = entry.ImportHints != null ? entry.ImportHints.ProviderId : string.Empty,
                    TargetPathPolicy = GetTargetPathPolicy(entry, options),
                    ImportTargetPath = CombineProjectPath(GetTargetRootPath(package, options), targetRelativePath),
                    ModelWrapperPose = entry.ImportHints != null ? entry.ImportHints.ModelWrapperPose ?? new CharacterAuthoringLocalPose() : new CharacterAuthoringLocalPose(),
                    ConflictAction = entry.ConflictPolicy != null ? entry.ConflictPolicy.HashChangedAction : string.Empty
                });
            }

            return mapping;
        }

        private static CharacterAuthoringCompiledConfigPatch BuildConfigPatch(
            CharacterResourcePackage package,
            CharacterAuthoringGeometryBinding geometryBinding,
            CharacterPackageResourceMapping resourceMapping)
        {
            var compiled = new CharacterAuthoringCompiledConfigPatch();
            if (package == null)
                return compiled;

            compiled.PackageId = package.Manifest != null ? package.Manifest.PackageId : string.Empty;
            compiled.Patch = new PatchDocument
            {
                SchemaVersion = "1.0",
                Source = CharacterAuthoringCompilerFormats.ConfigPatchBundle
            };

            var ids = new GeneratedIds();
            string packageId = package.Manifest != null ? package.Manifest.PackageId : "character";
            string characterStableId = GetCharacterStableId(package);
            string attributeStableId = GetAttributeProfileStableId(package);
            string bodyStableId = GetBodyProfileStableId(package);
            string schemaStableId = GetEquipmentSchemaStableId(package);
            string presentationStableId = "mx.presentation." + NormalizeSegment(packageId);
            string spawnStableId = "mx.spawn." + NormalizeSegment(packageId) + ".default";
            string partSetId = NormalizeSegment(packageId) + "_parts";

            AddReference(compiled, CharacterApplicationCompilerTableNames.CharacterConfig, ids.CharacterId, characterStableId, "config/character_application.json", "characterStableId");
            AddReference(compiled, CharacterApplicationCompilerTableNames.CharacterAttributeProfileConfig, ids.AttributeProfileId, attributeStableId, "config/character_application.json", "attributeProfileStableId");
            AddReference(compiled, CharacterApplicationCompilerTableNames.CharacterBodyProfileConfig, ids.BodyProfileId, bodyStableId, "config/character_application.json", "bodyProfileStableId");
            AddReference(compiled, CharacterApplicationCompilerTableNames.EquipmentSchemaConfig, ids.EquipmentSchemaId, schemaStableId, "config/character_application.json", "equipmentSchemaStableId");
            AddReference(compiled, CharacterApplicationCompilerTableNames.CharacterPresentationProfileConfig, ids.PresentationProfileId, presentationStableId, "config/character_application.json", "presentationProfile");
            AddReference(compiled, CharacterApplicationCompilerTableNames.SpawnProfileConfig, ids.SpawnProfileId, spawnStableId, "config/character_application.json", "spawnProfile");

            CharacterAuthoringGeometry geometry = package.Geometry ?? new CharacterAuthoringGeometry();
            AddCharacterAndCoreEntries(compiled, package, ids, characterStableId, attributeStableId, bodyStableId, schemaStableId, presentationStableId, spawnStableId, partSetId, resourceMapping);
            AddBodyPartEntries(compiled, geometry, ids, partSetId);

            List<WeaponAttachmentProfile> attachments = GetSortedAttachments(geometry);
            Dictionary<string, int> weaponIds = AddWeaponEntries(compiled, geometry, attachments, resourceMapping, ids);
            LoadoutPlan loadouts = AddLoadoutsAndStates(compiled, package, attachments, weaponIds, ids);
            AddAbilityLoadouts(compiled, attachments, loadouts, ids);
            AddCombatActionSets(compiled, geometry, loadouts, ids);

            SetCharacterDefaultLoadout(compiled, ids.CharacterId, loadouts.DefaultLoadoutId, loadouts.DefaultLoadoutStableId);
            return compiled;
        }

        private static void AddCharacterAndCoreEntries(
            CharacterAuthoringCompiledConfigPatch compiled,
            CharacterResourcePackage package,
            GeneratedIds ids,
            string characterStableId,
            string attributeStableId,
            string bodyStableId,
            string schemaStableId,
            string presentationStableId,
            string spawnStableId,
            string partSetId,
            CharacterPackageResourceMapping resourceMapping)
        {
            CharacterAuthoringGeometry geometry = package.Geometry ?? new CharacterAuthoringGeometry();
            CharacterBodyGeometryProfile body = geometry.BodyProfile ?? new CharacterBodyGeometryProfile();
            string packageSegment = NormalizeSegment(package.Manifest != null ? package.Manifest.PackageId : "character");

            AddEntry(compiled, CharacterApplicationCompilerTableNames.CharacterConfig, ids.CharacterId, new Dictionary<string, FieldValue>
            {
                ["CharacterId"] = Int(ids.CharacterId),
                ["StableId"] = Str(characterStableId),
                ["NameText"] = Str("character." + packageSegment + ".name"),
                ["DescriptionText"] = Str("character." + packageSegment + ".desc"),
                ["AttributeProfileId"] = Int(ids.AttributeProfileId),
                ["BodyProfileId"] = Int(ids.BodyProfileId),
                ["EquipmentSchemaId"] = Int(ids.EquipmentSchemaId),
                ["DefaultLoadoutId"] = Int(ids.DefaultLoadoutId),
                ["BaseAbilityLoadoutId"] = Int(ids.BaseAbilityLoadoutId),
                ["PresentationProfileId"] = Int(ids.PresentationProfileId),
                ["DefaultControllerKind"] = Str("HumanInput"),
                ["DefaultControllerProfileId"] = Str("controller.human.default"),
                ["DefaultSpawnTags"] = List("sample", packageSegment)
            });

            AddEntry(compiled, CharacterApplicationCompilerTableNames.CharacterAttributeProfileConfig, ids.AttributeProfileId, new Dictionary<string, FieldValue>
            {
                ["AttributeProfileId"] = Int(ids.AttributeProfileId),
                ["StableId"] = Str(attributeStableId),
                ["Attributes"] = FieldValue.FromList(new[]
                {
                    Map(("AttributeId", Int(ids.HpAttributeId)), ("StableId", Str("attr.hp")), ("Group", Str("Vital")), ("BaseValue", Float(160f)), ("InitialValue", Float(160f)), ("MinValue", Float(0f)), ("MaxValue", Float(160f))),
                    Map(("AttributeId", Int(ids.StaminaAttributeId)), ("StableId", Str("attr.stamina")), ("Group", Str("Resource")), ("BaseValue", Float(100f)), ("InitialValue", Float(100f)), ("MinValue", Float(0f)), ("MaxValue", Float(100f))),
                    Map(("AttributeId", Int(ids.PostureAttributeId)), ("StableId", Str("attr.posture")), ("Group", Str("Pressure")), ("BaseValue", Float(80f)), ("InitialValue", Float(80f)), ("MinValue", Float(0f)), ("MaxValue", Float(80f)))
                })
            });

            AddEntry(compiled, CharacterApplicationCompilerTableNames.CharacterBodyProfileConfig, ids.BodyProfileId, new Dictionary<string, FieldValue>
            {
                ["BodyProfileId"] = Int(ids.BodyProfileId),
                ["StableId"] = Str(bodyStableId),
                ["BodyKind"] = Str(MapBodyKind(body.BodyKind)),
                ["PartSetId"] = Str(partSetId),
                ["DefaultMotionProfileId"] = Str("motion." + packageSegment + ".default"),
                ["DefaultPhysicsProfileId"] = Str(string.IsNullOrWhiteSpace(body.DefaultPhysicsProfileId) ? "physics." + packageSegment + ".default" : body.DefaultPhysicsProfileId),
                ["Sockets"] = BuildSocketField(geometry),
                ["HitZoneBindings"] = BuildHitZoneBindingField(geometry)
            });

            AddEntry(compiled, CharacterApplicationCompilerTableNames.EquipmentSchemaConfig, ids.EquipmentSchemaId, new Dictionary<string, FieldValue>
            {
                ["EquipmentSchemaId"] = Int(ids.EquipmentSchemaId),
                ["StableId"] = Str(schemaStableId),
                ["Slots"] = BuildEquipmentSlotsField(geometry),
                ["ExclusiveGroups"] = FieldValue.FromList(new[] { Map(("GroupId", Str("hands")), ("SlotIds", BuildSlotIdList(geometry)), ("MaxFilledCount", Int(Math.Max(1, CountDistinctSlots(geometry)))) ) }),
                ["AllowedStateIds"] = FieldValue.FromList(new[] { Int(ids.UnarmedStateId), Int(ids.SingleWeaponStateId), Int(ids.AllWeaponsStateId) })
            });

            AddEntry(compiled, CharacterApplicationCompilerTableNames.CharacterPresentationProfileConfig, ids.PresentationProfileId, new Dictionary<string, FieldValue>
            {
                ["PresentationProfileId"] = Int(ids.PresentationProfileId),
                ["StableId"] = Str(presentationStableId),
                ["DefaultAnimationProfileId"] = Str("anim." + packageSegment + ".default"),
                ["ResourceKeys"] = BuildPresentationResourceKeys(package.ApplicationConfig, resourceMapping),
                ["PresentationTags"] = List(MapBodyKind(body.BodyKind).ToLowerInvariant(), "package:" + packageSegment)
            });

            AddEntry(compiled, CharacterApplicationCompilerTableNames.SpawnProfileConfig, ids.SpawnProfileId, new Dictionary<string, FieldValue>
            {
                ["SpawnProfileId"] = Int(ids.SpawnProfileId),
                ["StableId"] = Str(spawnStableId),
                ["CharacterId"] = Int(ids.CharacterId),
                ["TeamId"] = Str("team.neutral"),
                ["ControllerKind"] = Str("HumanInput"),
                ["EquipmentLoadoutId"] = Int(ids.DefaultLoadoutId),
                ["SpawnPose"] = Map(("AnchorId", Str("spawn.default")), ("X", Float(0f)), ("Y", Float(0f)), ("Z", Float(0f)), ("YawDegrees", Float(0f))),
                ["RuntimeAiPlannerProfileId"] = Str(string.Empty),
                ["DebugName"] = Str(package.Manifest != null && !string.IsNullOrWhiteSpace(package.Manifest.DisplayName) ? package.Manifest.DisplayName : packageSegment)
            });
        }

        private static void AddBodyPartEntries(CharacterAuthoringCompiledConfigPatch compiled, CharacterAuthoringGeometry geometry, GeneratedIds ids, string partSetId)
        {
            if (geometry == null)
                return;

            for (int i = 0; i < geometry.BodyParts.Count; i++)
            {
                CharacterBodyPartAuthoring part = geometry.BodyParts[i];
                if (part == null)
                    continue;

                int generatedId = ids.BodyPartStartId + i;
                string stableId = "mx.body_part." + NormalizeSegment(partSetId) + "." + NormalizeSegment(part.PartId);
                AddReference(compiled, CharacterApplicationCompilerTableNames.CharacterBodyPartConfig, generatedId, stableId, "geometry/body_parts.json", "geometry/bodyParts/" + i.ToString(CultureInfo.InvariantCulture));
                AddEntry(compiled, CharacterApplicationCompilerTableNames.CharacterBodyPartConfig, generatedId, new Dictionary<string, FieldValue>
                {
                    ["BodyPartConfigId"] = Int(generatedId),
                    ["StableId"] = Str(stableId),
                    ["PartSetId"] = Str(partSetId),
                    ["PartId"] = Str(part.PartId),
                    ["ParentPartId"] = Str(part.ParentPartId),
                    ["PartKind"] = Str(MapBodyPartKind(part)),
                    ["LocatorId"] = Str(!string.IsNullOrWhiteSpace(part.LocatorId) ? part.LocatorId : "loc." + NormalizeSegment(part.PartId)),
                    ["HitZoneId"] = Str(part.DefaultHitZoneId),
                    ["ReactionGroupId"] = Str(string.IsNullOrWhiteSpace(part.ReactionGroupId) ? "react.body" : part.ReactionGroupId),
                    ["DamageMultiplier"] = Float(IsHead(part.PartId) ? 1.25f : 1f),
                    ["ImpulseScale"] = Float(1f),
                    ["StaggerScale"] = Float(1f),
                    ["PostureDamageScale"] = Float(IsHead(part.PartId) ? 1.4f : 1f),
                    ["IsCritical"] = Bool(IsHead(part.PartId) || ContainsTag(part.Tags, "critical"))
                });
            }
        }

        private static Dictionary<string, int> AddWeaponEntries(
            CharacterAuthoringCompiledConfigPatch compiled,
            CharacterAuthoringGeometry geometry,
            List<WeaponAttachmentProfile> attachments,
            CharacterPackageResourceMapping resourceMapping,
            GeneratedIds ids)
        {
            var weaponIds = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < attachments.Count; i++)
            {
                WeaponAttachmentProfile attachment = attachments[i];
                int weaponId = ids.WeaponStartId + i;
                weaponIds[attachment.WeaponId] = weaponId;
                string weaponSegment = NormalizeSegment(attachment.WeaponId);
                string stableId = "mx.weapon." + weaponSegment;
                AddReference(compiled, CharacterApplicationCompilerTableNames.WeaponConfig, weaponId, stableId, "geometry/weapon_attachments.json", "geometry/weaponAttachments/" + attachment.WeaponId);
                AddEntry(compiled, CharacterApplicationCompilerTableNames.WeaponConfig, weaponId, new Dictionary<string, FieldValue>
                {
                    ["WeaponId"] = Int(weaponId),
                    ["StableId"] = Str(stableId),
                    ["Category"] = Str(MapWeaponCategory(attachment)),
                    ["Tags"] = BuildWeaponTags(attachment),
                    ["OccupiesSlots"] = List(attachment.EquipSlot),
                    ["GrantedAbilityLoadoutId"] = Int(ids.WeaponAbilityLoadoutStartId + i),
                    ["WeaponPresentationProfileId"] = Str("presentation." + weaponSegment),
                    ["WeaponCombatProfileId"] = Str("combat." + weaponSegment),
                    ["WeaponResourceProfileId"] = Str("resource." + weaponSegment),
                    ["ResourceKeys"] = BuildWeaponResourceKeys(attachment, resourceMapping),
                    ["TraceBindings"] = BuildWeaponTraceBindings(geometry, attachment),
                    ["BaseModifiers"] = FieldValue.FromList(Array.Empty<FieldValue>())
                });
            }

            return weaponIds;
        }

        private static LoadoutPlan AddLoadoutsAndStates(
            CharacterAuthoringCompiledConfigPatch compiled,
            CharacterResourcePackage package,
            List<WeaponAttachmentProfile> attachments,
            Dictionary<string, int> weaponIds,
            GeneratedIds ids)
        {
            var plan = new LoadoutPlan();
            CharacterApplicationAuthoringSummary summary = package.ApplicationConfig ?? new CharacterApplicationAuthoringSummary();
            List<string> loadoutStableIds = summary.Loadouts != null && summary.Loadouts.Count > 0
                ? new List<string>(summary.Loadouts)
                : CreateDefaultLoadoutStableIds(package, attachments);

            for (int i = 0; i < loadoutStableIds.Count; i++)
            {
                string stableId = loadoutStableIds[i];
                int loadoutId = ids.LoadoutStartId + i;
                List<WeaponAttachmentProfile> loadoutAttachments = SelectAttachmentsForLoadout(stableId, attachments);
                if (i == loadoutStableIds.Count - 1)
                {
                    plan.DefaultLoadoutId = loadoutId;
                    plan.DefaultLoadoutStableId = stableId;
                    ids.DefaultLoadoutId = loadoutId;
                }

                AddReference(compiled, CharacterApplicationCompilerTableNames.EquipmentLoadoutConfig, loadoutId, stableId, "config/character_application.json", "loadouts/" + i.ToString(CultureInfo.InvariantCulture));
                AddEntry(compiled, CharacterApplicationCompilerTableNames.EquipmentLoadoutConfig, loadoutId, new Dictionary<string, FieldValue>
                {
                    ["LoadoutId"] = Int(loadoutId),
                    ["StableId"] = Str(stableId),
                    ["EquipmentSchemaId"] = Int(ids.EquipmentSchemaId),
                    ["Slots"] = BuildLoadoutSlots(loadoutAttachments, weaponIds)
                });

                int stateId = ids.StateStartId + i;
                int actionSetId = ids.ActionSetStartId + i;
                int stateAbilityLoadoutId = loadoutAttachments.Count > 1 ? ids.StateAbilityLoadoutStartId + i : 0;
                string stateStableId = ToStateStableId(stableId);
                plan.StateIdByLoadoutStableId[stableId] = stateId;
                plan.StateStableIdByLoadoutStableId[stableId] = stateStableId;
                plan.ActionSetIdByLoadoutStableId[stableId] = actionSetId;
                plan.ActionSetStableIdByLoadoutStableId[stableId] = ToActionSetStableId(stableId);
                plan.LoadoutAttachmentsByStableId[stableId] = loadoutAttachments;

                AddReference(compiled, CharacterApplicationCompilerTableNames.EquipmentStateConfig, stateId, stateStableId, "config/character_application.json", "loadouts/" + i.ToString(CultureInfo.InvariantCulture));
                AddEntry(compiled, CharacterApplicationCompilerTableNames.EquipmentStateConfig, stateId, new Dictionary<string, FieldValue>
                {
                    ["StateId"] = Int(stateId),
                    ["StableId"] = Str(stateStableId),
                    ["EquipmentSchemaId"] = Int(ids.EquipmentSchemaId),
                    ["Priority"] = Int(loadoutAttachments.Count * 10),
                    ["MatchRules"] = FieldValue.FromList(new[] { BuildMatchRule(loadoutAttachments, attachments) }),
                    ["GrantedAbilityLoadoutId"] = Int(stateAbilityLoadoutId),
                    ["CombatActionSetId"] = Int(actionSetId),
                    ["AnimationProfileId"] = Str(ToAnimationProfileId(stableId)),
                    ["Tags"] = BuildLoadoutTags(stableId, loadoutAttachments)
                });
            }

            if (ids.DefaultLoadoutId == 0 && loadoutStableIds.Count == 0)
            {
                ids.DefaultLoadoutId = ids.LoadoutStartId;
                plan.DefaultLoadoutId = ids.DefaultLoadoutId;
                plan.DefaultLoadoutStableId = "equip_loadout." + NormalizeSegment(package.Manifest != null ? package.Manifest.PackageId : "character") + ".unarmed";
            }

            return plan;
        }

        private static void AddAbilityLoadouts(
            CharacterAuthoringCompiledConfigPatch compiled,
            List<WeaponAttachmentProfile> attachments,
            LoadoutPlan loadouts,
            GeneratedIds ids)
        {
            AddReference(compiled, CharacterApplicationCompilerTableNames.AbilityLoadoutConfig, ids.BaseAbilityLoadoutId, "mx.ability_loadout.base", "config/character_application.json", "baseAbilityLoadout");
            AddEntry(compiled, CharacterApplicationCompilerTableNames.AbilityLoadoutConfig, ids.BaseAbilityLoadoutId, new Dictionary<string, FieldValue>
            {
                ["LoadoutId"] = Int(ids.BaseAbilityLoadoutId),
                ["StableId"] = Str("mx.ability_loadout.base"),
                ["AbilityIds"] = FieldValue.FromList(new[] { Int(ids.BaseAbilityId) }),
                ["RemoveAbilityIds"] = FieldValue.FromList(Array.Empty<FieldValue>()),
                ["SlotBindings"] = FieldValue.FromList(new[] { Map(("SlotId", Str("evade")), ("AbilityId", Int(ids.BaseAbilityId)), ("InputIntentId", Str("intent.dodge"))) })
            });

            for (int i = 0; i < attachments.Count; i++)
            {
                WeaponAttachmentProfile attachment = attachments[i];
                int loadoutId = ids.WeaponAbilityLoadoutStartId + i;
                int abilityId = ids.WeaponAbilityStartId + i;
                string stableId = "mx.ability_loadout." + NormalizeSegment(attachment.WeaponId);
                AddReference(compiled, CharacterApplicationCompilerTableNames.AbilityLoadoutConfig, loadoutId, stableId, "geometry/weapon_attachments.json", "geometry/weaponAttachments/" + attachment.WeaponId);
                AddEntry(compiled, CharacterApplicationCompilerTableNames.AbilityLoadoutConfig, loadoutId, new Dictionary<string, FieldValue>
                {
                    ["LoadoutId"] = Int(loadoutId),
                    ["StableId"] = Str(stableId),
                    ["AbilityIds"] = FieldValue.FromList(new[] { Int(abilityId) }),
                    ["RemoveAbilityIds"] = FieldValue.FromList(Array.Empty<FieldValue>()),
                    ["SlotBindings"] = FieldValue.FromList(new[] { Map(("SlotId", Str(GetAbilitySlot(attachment))), ("AbilityId", Int(abilityId)), ("InputIntentId", Str(GetInputIntent(attachment)))) })
                });
            }

            int index = 0;
            foreach (KeyValuePair<string, List<WeaponAttachmentProfile>> pair in loadouts.LoadoutAttachmentsByStableId)
            {
                if (pair.Value.Count <= 1)
                {
                    index++;
                    continue;
                }

                int loadoutId = ids.StateAbilityLoadoutStartId + index;
                int abilityId = ids.StateAbilityStartId + index;
                string stableId = "mx.ability_loadout." + NormalizeSegment(pair.Key);
                AddReference(compiled, CharacterApplicationCompilerTableNames.AbilityLoadoutConfig, loadoutId, stableId, "config/character_application.json", pair.Key);
                AddEntry(compiled, CharacterApplicationCompilerTableNames.AbilityLoadoutConfig, loadoutId, new Dictionary<string, FieldValue>
                {
                    ["LoadoutId"] = Int(loadoutId),
                    ["StableId"] = Str(stableId),
                    ["AbilityIds"] = FieldValue.FromList(new[] { Int(abilityId) }),
                    ["RemoveAbilityIds"] = FieldValue.FromList(Array.Empty<FieldValue>()),
                    ["SlotBindings"] = FieldValue.FromList(new[] { Map(("SlotId", Str("secondary")), ("AbilityId", Int(abilityId)), ("InputIntentId", Str("intent.secondary"))) })
                });
                index++;
            }
        }

        private static void AddCombatActionSets(CharacterAuthoringCompiledConfigPatch compiled, CharacterAuthoringGeometry geometry, LoadoutPlan loadouts, GeneratedIds ids)
        {
            int index = 0;
            foreach (KeyValuePair<string, List<WeaponAttachmentProfile>> pair in loadouts.LoadoutAttachmentsByStableId)
            {
                int actionSetId = ids.ActionSetStartId + index;
                string stableId = loadouts.ActionSetStableIdByLoadoutStableId[pair.Key];
                AddReference(compiled, CharacterApplicationCompilerTableNames.CombatActionSetConfig, actionSetId, stableId, "config/character_application.json", pair.Key);
                AddEntry(compiled, CharacterApplicationCompilerTableNames.CombatActionSetConfig, actionSetId, new Dictionary<string, FieldValue>
                {
                    ["ActionSetId"] = Int(actionSetId),
                    ["StableId"] = Str(stableId),
                    ["Actions"] = BuildCombatActions(geometry, pair.Value, index)
                });
                index++;
            }
        }

        private static CharacterUnityImportWritePlan BuildWritePlan(
            CharacterResourcePackage package,
            CharacterAuthoringCompileOptions options,
            CharacterCompilerGateReport gate,
            CharacterPackageResourceMapping resourceMapping,
            CharacterAuthoringCompiledConfigPatch configPatch)
        {
            var plan = new CharacterUnityImportWritePlan();
            if (package == null)
                return plan;

            plan.PackageId = package.Manifest != null ? package.Manifest.PackageId : string.Empty;
            plan.TargetRootPath = GetTargetRootPath(package, options);
            plan.TargetPathPolicy = options != null ? options.TargetUnityPathPolicy : string.Empty;
            plan.CanWriteToUnityProject = gate != null && !gate.ExportBlocked && !gate.ImportBlocked;
            plan.CanSpawnAfterImport = plan.CanWriteToUnityProject && !gate.SpawnBlocked;

            if (resourceMapping != null)
            {
                for (int i = 0; i < resourceMapping.Entries.Count; i++)
                {
                    CharacterPackageResourceMappingEntry entry = resourceMapping.Entries[i];
                    plan.Writes.Add(new CharacterUnityImportWriteEntry
                    {
                        Kind = CharacterAuthoringCompilerWriteKinds.ResourceFile,
                        Owner = CharacterAuthoringCompilerOwnerKinds.UnityImporter,
                        SourcePath = entry.SourceRelativePath,
                        TargetPath = entry.ImportTargetPath,
                        WritePolicy = "CopyIfHashChanged",
                        ContentHash = entry.DeclaredContentHash
                    });
                }
            }

            return plan;
        }

        private static CharacterResolverVerificationPlan BuildResolverVerificationPlan(
            CharacterApplicationAuthoringSummary applicationConfig,
            CharacterAuthoringCompiledConfigPatch configPatch,
            CharacterCompilerGateReport gate,
            CharacterPackageResourceMapping resourceMapping)
        {
            var plan = new CharacterResolverVerificationPlan();
            plan.Status = gate != null && !gate.ExportBlocked && !gate.ImportBlocked && !gate.SpawnBlocked ? "Ready" : "Blocked";
            if (gate != null && gate.Issues != null)
                plan.Diagnostics.AddRange(gate.Issues);

            var tableMap = new Dictionary<string, CharacterResolverTableRequirement>(StringComparer.Ordinal);
            if (configPatch != null && configPatch.GeneratedReferences != null)
            {
                for (int i = 0; i < configPatch.GeneratedReferences.Count; i++)
                {
                    CharacterGeneratedConfigReference reference = configPatch.GeneratedReferences[i];
                    CharacterResolverTableRequirement requirement;
                    if (!tableMap.TryGetValue(reference.TableName, out requirement))
                    {
                        requirement = new CharacterResolverTableRequirement { TableName = reference.TableName };
                        tableMap[reference.TableName] = requirement;
                        plan.RequiredTables.Add(requirement);
                    }

                    requirement.RowCount++;
                    requirement.GeneratedIds.Add(reference.GeneratedId);
                    requirement.StableIds.Add(reference.StableId);

                    if (reference.TableName == CharacterApplicationCompilerTableNames.CharacterConfig)
                        plan.CharacterStableId = reference.StableId;
                }
            }

            plan.RequiredTables.Sort((a, b) => string.CompareOrdinal(a.TableName, b.TableName));

            if (configPatch != null && configPatch.Patch != null)
            {
                for (int i = 0; i < configPatch.Patch.Entries.Count; i++)
                {
                    PatchEntry entry = configPatch.Patch.Entries[i];
                    if (entry == null)
                        continue;

                    if (entry.Source == CharacterApplicationCompilerTableNames.CharacterConfig)
                    {
                        plan.DefaultLoadoutStableId = FindStableId(configPatch, CharacterApplicationCompilerTableNames.EquipmentLoadoutConfig, entry.Fields.GetScalar("DefaultLoadoutId"));
                    }
                    else if (entry.Source == CharacterApplicationCompilerTableNames.AbilityLoadoutConfig)
                    {
                        FieldValue abilities;
                        if (entry.Fields.TryGetValue("AbilityIds", out abilities) && abilities != null && abilities.List != null)
                        {
                            for (int j = 0; j < abilities.List.Count; j++)
                            {
                                int abilityId;
                                if (int.TryParse(abilities.List[j].Scalar, NumberStyles.Integer, CultureInfo.InvariantCulture, out abilityId) && abilityId > 0 && !plan.KnownAbilityIds.Contains(abilityId))
                                    plan.KnownAbilityIds.Add(abilityId);
                            }
                        }
                    }
                }

                string expectedStateStableId = ToStateStableId(plan.DefaultLoadoutStableId);
                for (int i = 0; i < configPatch.Patch.Entries.Count; i++)
                {
                    PatchEntry entry = configPatch.Patch.Entries[i];
                    if (entry == null || entry.Source != CharacterApplicationCompilerTableNames.EquipmentStateConfig)
                        continue;
                    if (!string.Equals(entry.Fields.GetScalar("StableId"), expectedStateStableId, StringComparison.Ordinal))
                        continue;

                    plan.ExpectedActiveEquipmentStateStableId = expectedStateStableId;
                    plan.ExpectedCombatActionSetStableId = FindStableId(configPatch, CharacterApplicationCompilerTableNames.CombatActionSetConfig, entry.Fields.GetScalar("CombatActionSetId"));
                    plan.ExpectedAnimationProfileId = entry.Fields.GetScalar("AnimationProfileId");
                    break;
                }
            }

            if (resourceMapping != null)
            {
                List<CharacterPackageResourceMappingEntry> referencedEntries = GetReferencedResourceMappingEntries(applicationConfig, resourceMapping);
                for (int i = 0; i < referencedEntries.Count; i++)
                {
                    string key = referencedEntries[i].ProjectResourceKey;
                    if (!string.IsNullOrWhiteSpace(key))
                        plan.RequiredResourceKeys.Add(key);
                }
            }

            plan.KnownAbilityIds.Sort();
            plan.RequiredResourceKeys.Sort(StringComparer.Ordinal);
            return plan;
        }

        private static List<CharacterPackageSourceMapping> BuildSourceMappings(
            CharacterAuthoringCompiledConfigPatch configPatch,
            CharacterAuthoringGeometryBinding geometryBinding,
            CharacterPackageResourceMapping resourceMapping)
        {
            var mappings = new List<CharacterPackageSourceMapping>();
            if (configPatch != null && configPatch.GeneratedReferences != null)
            {
                for (int i = 0; i < configPatch.GeneratedReferences.Count; i++)
                {
                    CharacterGeneratedConfigReference reference = configPatch.GeneratedReferences[i];
                    mappings.Add(new CharacterPackageSourceMapping
                    {
                        SourcePath = reference.SourcePath,
                        SourceObjectPath = reference.SourceObjectPath,
                        TargetKind = "CharacterApplicationConfig",
                        TargetPath = reference.TableName + "/" + reference.GeneratedId.ToString(CultureInfo.InvariantCulture),
                        TargetField = "StableId"
                    });
                }
            }

            if (geometryBinding != null)
            {
                for (int i = 0; i < geometryBinding.BodyColliders.Count; i++)
                {
                    CharacterBodyColliderBinding collider = geometryBinding.BodyColliders[i];
                    mappings.Add(new CharacterPackageSourceMapping
                    {
                        SourcePath = collider.SourcePath,
                        SourceObjectPath = collider.SourceObjectPath,
                        TargetKind = "GeometryBinding",
                        TargetPath = "bodyColliders/" + collider.ColliderId,
                        TargetField = "collider"
                    });
                }
            }

            if (resourceMapping != null)
            {
                for (int i = 0; i < resourceMapping.Entries.Count; i++)
                {
                    CharacterPackageResourceMappingEntry entry = resourceMapping.Entries[i];
                    mappings.Add(new CharacterPackageSourceMapping
                    {
                        SourcePath = entry.SourceRelativePath,
                        SourceObjectPath = entry.PackageResourceKey,
                        TargetKind = "UnityImportTarget",
                        TargetPath = entry.ImportTargetPath,
                        TargetField = "ResourceKey"
                    });
                }
            }

            return mappings;
        }

        private static CharacterCompilerHashSet BuildHashes(
            CharacterResourcePackage package,
            CharacterAuthoringCompiledConfigPatch configPatch,
            CharacterAuthoringGeometryBinding geometryBinding,
            CharacterPackageResourceMapping resourceMapping,
            CharacterUnityImportWritePlan writePlan)
        {
            return new CharacterCompilerHashSet
            {
                SourcePackageHash = CharacterPackageHashUtility.ComputeTextSha256(BuildSourcePackageCanonicalText(package)),
                GeneratedConfigHash = CharacterPackageHashUtility.ComputeTextSha256(BuildConfigPatchCanonicalText(configPatch)),
                GeometryBindingHash = CharacterPackageHashUtility.ComputeTextSha256(BuildGeometryBindingCanonicalText(geometryBinding)),
                ResourceMappingHash = CharacterPackageHashUtility.ComputeTextSha256(BuildResourceMappingCanonicalText(resourceMapping)),
                WritePlanHash = CharacterPackageHashUtility.ComputeTextSha256(BuildWritePlanCanonicalText(writePlan))
            };
        }

        private static void AddCoordinateDiagnostics(CharacterResourcePackage package, CharacterAuthoringCompileOptions options, List<CharacterAuthoringValidationIssue> issues)
        {
            if (package == null || package.Manifest == null)
                return;

            CharacterCoordinateConversionPlan plan = BuildCoordinatePlan(package.Manifest.CoordinateConvention, options.TargetCoordinateConvention);
            if (!plan.RequiresConversion)
                return;

            issues.Add(new CharacterAuthoringValidationIssue
            {
                Severity = CharacterAuthoringValidationSeverity.Warning,
                Gate = CharacterAuthoringValidationGate.WarningOnly,
                Code = CharacterAuthoringCompilerValidationCodes.CoordinateTargetMismatch,
                SourcePath = "manifest.json",
                SourceObjectPath = "manifest.coordinateConvention",
                Field = "coordinateConvention",
                Message = "package coordinate convention differs from Unity target convention; compiler emitted a deterministic conversion plan.",
                SuggestedFix = "Review axis, handedness and unit scale before import."
            });
        }

        private static void AddResourceConflictDiagnostics(CharacterResourcePackage package, CharacterPackageResourceCatalog projectCatalog, List<CharacterAuthoringValidationIssue> issues)
        {
            if (package == null || package.ResourceCatalog == null || projectCatalog == null)
                return;

            for (int i = 0; i < package.ResourceCatalog.Entries.Count; i++)
            {
                CharacterPackageResourceEntry entry = package.ResourceCatalog.Entries[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.ResourceKey))
                    continue;

                CharacterPackageResourceEntry existing = CharacterPackageResourcePipeline.FindByKey(projectCatalog, entry.ResourceKey);
                if (existing == null)
                    continue;

                string incomingHash = CharacterPackageResourcePipeline.GetDeclaredContentHash(entry);
                string existingHash = CharacterPackageResourcePipeline.GetDeclaredContentHash(existing);
                if (string.IsNullOrWhiteSpace(incomingHash) || string.Equals(incomingHash, existingHash, StringComparison.OrdinalIgnoreCase))
                    continue;

                issues.Add(new CharacterAuthoringValidationIssue
                {
                    Severity = CharacterAuthoringValidationSeverity.Error,
                    Gate = CharacterAuthoringValidationGate.ImportBlocked,
                    Code = CharacterAuthoringCompilerValidationCodes.ResourceKeyConflict,
                    SourcePath = "resource_catalog.json",
                    SourceObjectPath = "resourceCatalog/entries/" + i.ToString(CultureInfo.InvariantCulture),
                    Field = "resourceKey",
                    Message = "package ResourceKey already exists in the target project catalog with a different content hash.",
                    SuggestedFix = "Rename the package resource key, choose a variant, or explicitly migrate the existing project resource."
                });
            }
        }

        private static void AddStrictDiagnostics(CharacterAuthoringCompileOptions options, List<CharacterAuthoringValidationIssue> issues)
        {
            if (options == null || (!options.Strict && options.AllowWarnings))
                return;

            bool hasWarning = false;
            for (int i = 0; i < issues.Count; i++)
            {
                CharacterAuthoringValidationIssue issue = issues[i];
                if (issue != null && (issue.Severity == CharacterAuthoringValidationSeverity.Warning || issue.Gate == CharacterAuthoringValidationGate.WarningOnly))
                {
                    hasWarning = true;
                    break;
                }
            }

            if (!hasWarning)
                return;

            issues.Add(new CharacterAuthoringValidationIssue
            {
                Severity = CharacterAuthoringValidationSeverity.Error,
                Gate = CharacterAuthoringValidationGate.ImportBlocked,
                Code = CharacterAuthoringCompilerValidationCodes.StrictWarningBlocked,
                SourcePath = "compiler/options",
                SourceObjectPath = "options",
                Field = options.Strict ? "strict" : "allowWarnings",
                Message = "compiler options do not allow warning-only packages to continue to Unity import.",
                SuggestedFix = "Fix warnings or compile with warnings allowed."
            });
        }

        private static CharacterCoordinateConversionPlan BuildCoordinatePlan(CharacterPackageCoordinateConvention source, CharacterPackageCoordinateConvention target)
        {
            if (source == null)
                source = new CharacterPackageCoordinateConvention();
            if (target == null)
                target = new CharacterPackageCoordinateConvention();

            bool requiresConversion =
                source.UpAxis != target.UpAxis ||
                source.ForwardAxis != target.ForwardAxis ||
                source.Handedness != target.Handedness ||
                Math.Abs(source.UnitScaleMeters - target.UnitScaleMeters) > 0.000001f;

            return new CharacterCoordinateConversionPlan
            {
                SourceConvention = source,
                TargetConvention = target,
                RequiresConversion = requiresConversion,
                PositionScale = target.UnitScaleMeters <= 0f ? 1f : source.UnitScaleMeters / target.UnitScaleMeters,
                AxisConversion = requiresConversion ? source.UpAxis + "/" + source.ForwardAxis + "/" + source.Handedness + " -> " + target.UpAxis + "/" + target.ForwardAxis + "/" + target.Handedness : "identity",
                RotationConversion = requiresConversion ? "quaternion-axis-remap" : "identity"
            };
        }

        private static void AddEntry(CharacterAuthoringCompiledConfigPatch compiled, string tableName, int id, Dictionary<string, FieldValue> fields)
        {
            compiled.Patch.Entries.Add(new PatchEntry
            {
                Operation = PatchOperation.Upsert,
                Source = tableName,
                Id = id.ToString(CultureInfo.InvariantCulture),
                Layer = "Generated",
                Fields = fields ?? new Dictionary<string, FieldValue>()
            });
        }

        private static void AddReference(CharacterAuthoringCompiledConfigPatch compiled, string tableName, int id, string stableId, string sourcePath, string sourceObjectPath)
        {
            compiled.GeneratedReferences.Add(new CharacterGeneratedConfigReference
            {
                TableName = tableName,
                GeneratedId = id,
                StableId = stableId ?? string.Empty,
                SourcePath = sourcePath ?? string.Empty,
                SourceObjectPath = sourceObjectPath ?? string.Empty
            });
        }

        private static FieldValue BuildSocketField(CharacterAuthoringGeometry geometry)
        {
            var values = new List<FieldValue>();
            if (geometry != null)
            {
                for (int i = 0; i < geometry.Sockets.Count; i++)
                {
                    CharacterSocketProfile socket = geometry.Sockets[i];
                    if (socket == null)
                        continue;

                    values.Add(Map(
                        ("SocketId", Str(socket.SocketId)),
                        ("ParentPartId", Str(socket.ParentPartId)),
                        ("LocatorId", Str(!string.IsNullOrWhiteSpace(socket.LocatorPath) ? socket.LocatorPath : socket.SocketId))));
                }
            }

            return FieldValue.FromList(values);
        }

        private static FieldValue BuildHitZoneBindingField(CharacterAuthoringGeometry geometry)
        {
            var values = new List<FieldValue>();
            if (geometry != null)
            {
                for (int i = 0; i < geometry.Colliders.Count; i++)
                {
                    CharacterBodyColliderProfile collider = geometry.Colliders[i];
                    if (collider == null || string.IsNullOrWhiteSpace(collider.HitZoneId))
                        continue;

                    values.Add(Map(
                        ("HitZoneId", Str(collider.HitZoneId)),
                        ("PartId", Str(collider.PartId)),
                        ("Priority", Int(collider.Priority)),
                        ("IsWeakPoint", Bool(collider.IsWeakPoint)),
                        ("DamageMultiplierOverride", Float(collider.DamageMultiplierOverride)),
                        ("PostureDamageScaleOverride", Float(collider.PostureDamageScaleOverride))));
                }
            }

            return FieldValue.FromList(values);
        }

        private static FieldValue BuildEquipmentSlotsField(CharacterAuthoringGeometry geometry)
        {
            var values = new List<FieldValue>();
            List<string> slots = GetDistinctSlots(geometry);
            for (int i = 0; i < slots.Count; i++)
            {
                string slot = slots[i];
                values.Add(Map(
                    ("SlotId", Str(slot)),
                    ("Kind", Str(MapSlotKind(slot))),
                    ("DisplayName", Str(ToDisplayName(slot))),
                    ("AllowedWeaponCategories", SlotAllowedCategories(slot)),
                    ("RequiredTags", FieldValue.FromList(Array.Empty<FieldValue>()))));
            }

            return FieldValue.FromList(values);
        }

        private static FieldValue BuildSlotIdList(CharacterAuthoringGeometry geometry)
        {
            List<string> slots = GetDistinctSlots(geometry);
            var values = new List<FieldValue>();
            for (int i = 0; i < slots.Count; i++)
                values.Add(Str(slots[i]));
            return FieldValue.FromList(values);
        }

        private static FieldValue BuildPresentationResourceKeys(CharacterApplicationAuthoringSummary applicationConfig, CharacterPackageResourceMapping mapping)
        {
            var values = new List<FieldValue>();
            if (mapping != null)
            {
                List<CharacterPackageResourceMappingEntry> referencedEntries = GetReferencedResourceMappingEntries(applicationConfig, mapping);
                for (int i = 0; i < referencedEntries.Count; i++)
                {
                    CharacterPackageResourceMappingEntry entry = referencedEntries[i];
                    values.Add(Map(
                        ("Id", Str(entry.ProjectResourceKey)),
                        ("TypeId", Str(MapResourceType(entry))),
                        ("UsageKind", Str(MapResourceUsageKind(entry))),
                        ("Variant", Str(string.Empty)),
                        ("PackageId", Str(mapping.PackageId)),
                        ("PreloadGroupId", Str("character." + NormalizeSegment(mapping.PackageId)))));
                }
            }

            return FieldValue.FromList(values);
        }

        private static List<CharacterPackageResourceMappingEntry> GetReferencedResourceMappingEntries(
            CharacterApplicationAuthoringSummary applicationConfig,
            CharacterPackageResourceMapping mapping)
        {
            var result = new List<CharacterPackageResourceMappingEntry>();
            if (mapping == null || mapping.Entries == null)
                return result;

            List<string> resourceKeys = applicationConfig != null ? applicationConfig.ResourceKeys : null;
            if (resourceKeys == null || resourceKeys.Count == 0)
            {
                for (int i = 0; i < mapping.Entries.Count; i++)
                {
                    if (mapping.Entries[i] != null)
                        result.Add(mapping.Entries[i]);
                }
                return result;
            }

            var byPackageKey = new Dictionary<string, CharacterPackageResourceMappingEntry>(StringComparer.Ordinal);
            for (int i = 0; i < mapping.Entries.Count; i++)
            {
                CharacterPackageResourceMappingEntry entry = mapping.Entries[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.PackageResourceKey))
                    continue;
                if (!byPackageKey.ContainsKey(entry.PackageResourceKey))
                    byPackageKey.Add(entry.PackageResourceKey, entry);
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < resourceKeys.Count; i++)
            {
                string key = resourceKeys[i];
                if (string.IsNullOrWhiteSpace(key))
                    continue;
                if (!byPackageKey.TryGetValue(key, out CharacterPackageResourceMappingEntry entry))
                    continue;

                string identity = string.IsNullOrWhiteSpace(entry.ProjectResourceKey) ? entry.PackageResourceKey : entry.ProjectResourceKey;
                if (seen.Add(identity))
                    result.Add(entry);
            }

            return result;
        }

        private static FieldValue BuildWeaponResourceKeys(WeaponAttachmentProfile attachment, CharacterPackageResourceMapping resourceMapping)
        {
            var values = new List<FieldValue>();
            string key = attachment != null ? attachment.PreviewResourceKey : string.Empty;
            if (!string.IsNullOrWhiteSpace(key))
            {
                values.Add(Map(
                    ("Id", Str(key)),
                    ("TypeId", Str("GameObject")),
                    ("UsageKind", Str("WeaponModel")),
                    ("Variant", Str(string.Empty)),
                    ("PackageId", Str(resourceMapping != null ? resourceMapping.PackageId : string.Empty)),
                    ("PreloadGroupId", Str("character." + NormalizeSegment(resourceMapping != null ? resourceMapping.PackageId : string.Empty)))));
            }

            return FieldValue.FromList(values);
        }

        private static FieldValue BuildWeaponTraceBindings(CharacterAuthoringGeometry geometry, WeaponAttachmentProfile attachment)
        {
            var values = new List<FieldValue>();
            if (geometry == null || attachment == null)
                return FieldValue.FromList(values);

            for (int i = 0; i < geometry.Traces.Count; i++)
            {
                WeaponTraceProfile trace = geometry.Traces[i];
                if (trace == null || !string.Equals(trace.WeaponId, attachment.WeaponId, StringComparison.Ordinal))
                    continue;

                values.Add(Map(
                    ("TraceProfileId", Str(trace.TraceId)),
                    ("SlotId", Str(trace.EquipSlot)),
                    ("SocketId", Str(!string.IsNullOrWhiteSpace(attachment.TraceStartSocketId) ? attachment.TraceStartSocketId : attachment.AttachSocketId)),
                    ("Length", Float(ComputeTraceLength(trace))),
                    ("Radius", Float(trace.Radius))));
            }

            return FieldValue.FromList(values);
        }

        private static FieldValue BuildLoadoutSlots(List<WeaponAttachmentProfile> attachments, Dictionary<string, int> weaponIds)
        {
            var values = new List<FieldValue>();
            for (int i = 0; i < attachments.Count; i++)
            {
                WeaponAttachmentProfile attachment = attachments[i];
                int weaponId;
                weaponIds.TryGetValue(attachment.WeaponId, out weaponId);
                values.Add(Map(
                    ("SlotId", Str(attachment.EquipSlot)),
                    ("WeaponId", Int(weaponId)),
                    ("WeaponInstanceStableId", Str("weapon.instance." + NormalizeSegment(attachment.WeaponId) + ".001"))));
            }

            return FieldValue.FromList(values);
        }

        private static FieldValue BuildMatchRule(List<WeaponAttachmentProfile> loadoutAttachments, List<WeaponAttachmentProfile> allAttachments)
        {
            var filled = new List<FieldValue>();
            var empty = new List<FieldValue>();
            var categories = new List<FieldValue>();
            var tags = new List<FieldValue>();
            var filledSlots = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < loadoutAttachments.Count; i++)
            {
                WeaponAttachmentProfile attachment = loadoutAttachments[i];
                filled.Add(Str(attachment.EquipSlot));
                filledSlots.Add(attachment.EquipSlot);
                categories.Add(Str(attachment.EquipSlot + ":" + MapWeaponCategory(attachment)));
                tags.Add(Str(attachment.EquipSlot + ":" + GetPrimaryWeaponTag(attachment)));
            }

            for (int i = 0; i < allAttachments.Count; i++)
            {
                string slot = allAttachments[i].EquipSlot;
                if (!filledSlots.Contains(slot))
                    empty.Add(Str(slot));
            }

            return Map(
                ("RequiredFilledSlots", FieldValue.FromList(filled)),
                ("RequiredEmptySlots", FieldValue.FromList(empty)),
                ("RequiredWeaponCategoriesBySlot", FieldValue.FromList(categories)),
                ("RequiredWeaponTagsBySlot", FieldValue.FromList(tags)),
                ("ForbiddenWeaponTagsBySlot", FieldValue.FromList(Array.Empty<FieldValue>())));
        }

        private static FieldValue BuildLoadoutTags(string stableId, List<WeaponAttachmentProfile> attachments)
        {
            var tags = new List<FieldValue> { Str(NormalizeSegment(stableId)) };
            for (int i = 0; i < attachments.Count; i++)
                tags.Add(Str(GetPrimaryWeaponTag(attachments[i])));
            return FieldValue.FromList(tags);
        }

        private static FieldValue BuildWeaponTags(WeaponAttachmentProfile attachment)
        {
            string tag = GetPrimaryWeaponTag(attachment);
            string handed = string.Equals(attachment != null ? attachment.EquipSlot : string.Empty, "twoHand", StringComparison.OrdinalIgnoreCase) ? "two_hand" : "one_hand";
            return List(tag, handed);
        }

        private static FieldValue BuildCombatActions(CharacterAuthoringGeometry geometry, List<WeaponAttachmentProfile> attachments, int index)
        {
            var actions = new List<FieldValue>();
            if (attachments.Count == 0)
            {
                actions.Add(Map(("ActionKey", Str("primary")), ("CombatActionId", Int(910001 + index * 10)), ("TraceProfileIdOverride", Str(string.Empty)), ("AnimationActionKey", Str("punch"))));
                return FieldValue.FromList(actions);
            }

            for (int i = 0; i < attachments.Count; i++)
            {
                WeaponAttachmentProfile attachment = attachments[i];
                bool shield = string.Equals(MapWeaponCategory(attachment), "Shield", StringComparison.Ordinal);
                string actionKey = shield ? "guard" : "primary";
                string animation = shield ? "shield_guard" : "slash";
                actions.Add(Map(
                    ("ActionKey", Str(actionKey)),
                    ("CombatActionId", Int(910001 + index * 10 + i)),
                    ("TraceProfileIdOverride", Str(attachment.TraceId)),
                    ("AnimationActionKey", Str(animation))));
            }

            return FieldValue.FromList(actions);
        }

        private static void SetCharacterDefaultLoadout(CharacterAuthoringCompiledConfigPatch compiled, int characterId, int loadoutId, string stableId)
        {
            for (int i = 0; i < compiled.Patch.Entries.Count; i++)
            {
                PatchEntry entry = compiled.Patch.Entries[i];
                if (entry.Source == CharacterApplicationCompilerTableNames.CharacterConfig && entry.Id == characterId.ToString(CultureInfo.InvariantCulture))
                    entry.Fields["DefaultLoadoutId"] = Int(loadoutId);
                else if (entry.Source == CharacterApplicationCompilerTableNames.SpawnProfileConfig)
                    entry.Fields["EquipmentLoadoutId"] = Int(loadoutId);
            }
        }

        private static string FindStableId(CharacterAuthoringCompiledConfigPatch configPatch, string tableName, string id)
        {
            if (string.IsNullOrWhiteSpace(id) || configPatch == null)
                return string.Empty;

            int parsed;
            if (!int.TryParse(id, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed))
                return string.Empty;

            for (int i = 0; i < configPatch.GeneratedReferences.Count; i++)
            {
                CharacterGeneratedConfigReference reference = configPatch.GeneratedReferences[i];
                if (reference.TableName == tableName && reference.GeneratedId == parsed)
                    return reference.StableId;
            }

            return string.Empty;
        }

        private static List<WeaponAttachmentProfile> GetSortedAttachments(CharacterAuthoringGeometry geometry)
        {
            var attachments = new List<WeaponAttachmentProfile>();
            if (geometry != null && geometry.WeaponAttachments != null)
                attachments.AddRange(geometry.WeaponAttachments);
            attachments.Sort((a, b) => string.CompareOrdinal(a != null ? a.WeaponId : string.Empty, b != null ? b.WeaponId : string.Empty));
            return attachments;
        }

        private static List<WeaponAttachmentProfile> SelectAttachmentsForLoadout(string stableId, List<WeaponAttachmentProfile> attachments)
        {
            var selected = new List<WeaponAttachmentProfile>();
            string normalized = NormalizeSegment(stableId);
            if (normalized.Contains("unarmed"))
                return selected;

            for (int i = 0; i < attachments.Count; i++)
            {
                WeaponAttachmentProfile attachment = attachments[i];
                if (attachment == null)
                    continue;
                string weapon = NormalizeSegment(attachment.WeaponId);
                string category = GetPrimaryWeaponTag(attachment);
                if (normalized.Contains(weapon) || normalized.Contains(category) || normalized.Contains(NormalizeSegment(attachment.EquipSlot)))
                    selected.Add(attachment);
            }

            if (selected.Count == 0 && normalized.Contains("sword"))
            {
                for (int i = 0; i < attachments.Count; i++)
                {
                    if (GetPrimaryWeaponTag(attachments[i]) == "blade")
                        selected.Add(attachments[i]);
                }
            }
            else if (normalized.Contains("sword"))
            {
                for (int i = 0; i < attachments.Count; i++)
                {
                    WeaponAttachmentProfile attachment = attachments[i];
                    if (GetPrimaryWeaponTag(attachment) == "blade" && !selected.Contains(attachment))
                        selected.Add(attachment);
                }
            }

            if (selected.Count == 0 && !normalized.Contains("single"))
                selected.AddRange(attachments);

            if (normalized.Contains("shield"))
            {
                for (int i = 0; i < attachments.Count; i++)
                {
                    WeaponAttachmentProfile attachment = attachments[i];
                    if (GetPrimaryWeaponTag(attachment) == "shield" && !selected.Contains(attachment))
                        selected.Add(attachment);
                }
            }

            selected.Sort((a, b) => string.CompareOrdinal(a.EquipSlot, b.EquipSlot));
            return selected;
        }

        private static List<string> CreateDefaultLoadoutStableIds(CharacterResourcePackage package, List<WeaponAttachmentProfile> attachments)
        {
            string packageId = package != null && package.Manifest != null ? NormalizeSegment(package.Manifest.PackageId) : "character";
            var stableIds = new List<string> { "equip_loadout." + packageId + ".unarmed" };
            if (attachments.Count == 1)
                stableIds.Add("equip_loadout." + packageId + "." + NormalizeSegment(attachments[0].WeaponId));
            else if (attachments.Count > 1)
                stableIds.Add("equip_loadout." + packageId + ".all_weapons");
            return stableIds;
        }

        private static List<string> GetDistinctSlots(CharacterAuthoringGeometry geometry)
        {
            var slots = new List<string>();
            if (geometry != null)
            {
                for (int i = 0; i < geometry.WeaponAttachments.Count; i++)
                {
                    WeaponAttachmentProfile attachment = geometry.WeaponAttachments[i];
                    if (attachment != null && !string.IsNullOrWhiteSpace(attachment.EquipSlot) && !slots.Contains(attachment.EquipSlot))
                        slots.Add(attachment.EquipSlot);
                }
            }

            slots.Sort(StringComparer.Ordinal);
            return slots;
        }

        private static int CountDistinctSlots(CharacterAuthoringGeometry geometry)
        {
            return GetDistinctSlots(geometry).Count;
        }

        private static string GetCharacterStableId(CharacterResourcePackage package)
        {
            if (package != null && package.ApplicationConfig != null && !string.IsNullOrWhiteSpace(package.ApplicationConfig.CharacterStableId))
                return package.ApplicationConfig.CharacterStableId;
            string packageId = package != null && package.Manifest != null ? package.Manifest.PackageId : "character";
            return "mx.character." + NormalizeSegment(packageId);
        }

        private static string GetBodyProfileStableId(CharacterResourcePackage package)
        {
            if (package != null && package.ApplicationConfig != null && !string.IsNullOrWhiteSpace(package.ApplicationConfig.BodyProfileStableId))
                return package.ApplicationConfig.BodyProfileStableId;
            string packageId = package != null && package.Manifest != null ? package.Manifest.PackageId : "character";
            return "mx.body." + NormalizeSegment(packageId);
        }

        private static string GetAttributeProfileStableId(CharacterResourcePackage package)
        {
            if (package != null && package.ApplicationConfig != null && !string.IsNullOrWhiteSpace(package.ApplicationConfig.AttributeProfileStableId))
                return package.ApplicationConfig.AttributeProfileStableId;
            string packageId = package != null && package.Manifest != null ? package.Manifest.PackageId : "character";
            return "mx.character_attr." + NormalizeSegment(packageId);
        }

        private static string GetEquipmentSchemaStableId(CharacterResourcePackage package)
        {
            if (package != null && package.ApplicationConfig != null && !string.IsNullOrWhiteSpace(package.ApplicationConfig.EquipmentSchemaStableId))
                return package.ApplicationConfig.EquipmentSchemaStableId;
            string packageId = package != null && package.Manifest != null ? package.Manifest.PackageId : "character";
            return "mx.equipment_schema." + NormalizeSegment(packageId);
        }

        private static string GetTargetRootPath(CharacterResourcePackage package, CharacterAuthoringCompileOptions options)
        {
            string root = options != null && !string.IsNullOrWhiteSpace(options.GeneratedRootPath)
                ? options.GeneratedRootPath
                : "Assets/MxFrameworkGenerated/CharacterPackages";
            string packageSegment = package != null && package.Manifest != null ? NormalizeSegment(package.Manifest.PackageId) : "character";
            return CombineProjectPath(root, packageSegment);
        }

        private static string GetImportTargetRelativePath(CharacterPackageResourceEntry entry)
        {
            if (entry != null && entry.ImportHints != null && !string.IsNullOrWhiteSpace(entry.ImportHints.TargetRelativePath))
                return entry.ImportHints.TargetRelativePath.Replace('\\', '/');
            return entry != null ? (entry.RelativePath ?? string.Empty).Replace('\\', '/') : string.Empty;
        }

        private static string GetTargetPathPolicy(CharacterPackageResourceEntry entry, CharacterAuthoringCompileOptions options)
        {
            if (entry != null && entry.ImportHints != null && !string.IsNullOrWhiteSpace(entry.ImportHints.TargetPathPolicy))
                return entry.ImportHints.TargetPathPolicy;
            return options != null ? options.TargetUnityPathPolicy : string.Empty;
        }

        private static string CombineProjectPath(string left, string right)
        {
            if (string.IsNullOrWhiteSpace(left))
                return (right ?? string.Empty).Replace('\\', '/');
            if (string.IsNullOrWhiteSpace(right))
                return left.Replace('\\', '/');
            return left.TrimEnd('/', '\\').Replace('\\', '/') + "/" + right.TrimStart('/', '\\').Replace('\\', '/');
        }

        private static string ToStateStableId(string loadoutStableId)
        {
            if (loadoutStableId != null && loadoutStableId.Contains("equip_loadout."))
                return loadoutStableId.Replace("equip_loadout.", "equip_state.");
            return "equip_state." + NormalizeSegment(loadoutStableId);
        }

        private static string ToActionSetStableId(string loadoutStableId)
        {
            if (loadoutStableId != null && loadoutStableId.Contains("equip_loadout."))
                return loadoutStableId.Replace("equip_loadout.", "action_set.");
            return "action_set." + NormalizeSegment(loadoutStableId);
        }

        private static string ToAnimationProfileId(string loadoutStableId)
        {
            string value = NormalizeSegment(loadoutStableId);
            if (value.StartsWith("equip_loadout.", StringComparison.Ordinal))
                value = value.Substring("equip_loadout.".Length);
            return "anim." + value;
        }

        private static string MapBodyKind(CharacterAuthoringBodyKind kind)
        {
            if (kind == CharacterAuthoringBodyKind.Primitive)
                return "Slime";
            if (kind == CharacterAuthoringBodyKind.Compound)
                return "SimpleShape";
            return "Humanoid";
        }

        private static string MapBodyPartKind(CharacterBodyPartAuthoring part)
        {
            string id = NormalizeSegment(part != null ? part.PartId : string.Empty);
            if (id.Contains("root"))
                return "Root";
            if (id.Contains("head"))
                return "Head";
            if (id.Contains("torso") || id.Contains("body"))
                return "Torso";
            if (id.Contains("hand"))
                return "Hand";
            if (id.Contains("leg"))
                return "Leg";
            if (id.Contains("foot"))
                return "Foot";
            if (id.Contains("tail"))
                return "Tail";
            if (id.Contains("core"))
                return "Core";
            if (part != null && part.PartKind == CharacterAuthoringBodyPartKind.Primitive)
                return "Core";
            return "Custom";
        }

        private static string MapSlotKind(string slot)
        {
            if (string.Equals(slot, "mainHand", StringComparison.OrdinalIgnoreCase))
                return "MainHand";
            if (string.Equals(slot, "offHand", StringComparison.OrdinalIgnoreCase))
                return "OffHand";
            if (string.Equals(slot, "twoHand", StringComparison.OrdinalIgnoreCase))
                return "TwoHand";
            if (string.Equals(slot, "naturalWeapon", StringComparison.OrdinalIgnoreCase))
                return "NaturalWeapon";
            return "Custom";
        }

        private static string MapWeaponCategory(WeaponAttachmentProfile attachment)
        {
            string id = NormalizeSegment(attachment != null ? attachment.WeaponId : string.Empty);
            string slot = attachment != null ? attachment.EquipSlot : string.Empty;
            if (id.Contains("shield") || string.Equals(slot, "offHand", StringComparison.OrdinalIgnoreCase))
                return "Shield";
            if (id.Contains("bow") || id.Contains("gun"))
                return "Ranged";
            if (id.Contains("staff") || id.Contains("wand"))
                return "Catalyst";
            if (id.Contains("claw") || id.Contains("bite"))
                return "Natural";
            return "OneHandMelee";
        }

        private static string GetPrimaryWeaponTag(WeaponAttachmentProfile attachment)
        {
            string category = MapWeaponCategory(attachment);
            if (category == "Shield")
                return "shield";
            if (category == "Natural")
                return "natural";
            if (category == "Ranged")
                return "ranged";
            return "blade";
        }

        private static string GetAbilitySlot(WeaponAttachmentProfile attachment)
        {
            return MapWeaponCategory(attachment) == "Shield" ? "guard" : "primary";
        }

        private static string GetInputIntent(WeaponAttachmentProfile attachment)
        {
            return MapWeaponCategory(attachment) == "Shield" ? "intent.guard" : "intent.primary";
        }

        private static FieldValue SlotAllowedCategories(string slot)
        {
            if (string.Equals(slot, "offHand", StringComparison.OrdinalIgnoreCase))
                return List("Shield");
            if (string.Equals(slot, "naturalWeapon", StringComparison.OrdinalIgnoreCase))
                return List("Natural");
            return List("OneHandMelee", "Tool", "Catalyst");
        }

        private static string MapResourceType(CharacterPackageResourceMappingEntry entry)
        {
            if (entry == null)
                return string.Empty;
            if (entry.TypeId == CharacterPackageResourceTypeIds.Model)
                return "GameObject";
            if (entry.TypeId == CharacterPackageResourceTypeIds.Animation)
                return "AnimationClipGroup";
            if (entry.TypeId == CharacterPackageResourceTypeIds.Preview || entry.TypeId == CharacterPackageResourceTypeIds.Texture)
                return "Sprite";
            return entry.TypeId;
        }

        private static string MapResourceUsageKind(CharacterPackageResourceMappingEntry entry)
        {
            if (entry == null)
                return "Custom";
            if (entry.Usage == CharacterPackageResourceUsageIds.CharacterModel)
                return "Model";
            if (entry.Usage == CharacterPackageResourceUsageIds.WeaponModel)
                return "WeaponModel";
            if (entry.Usage == CharacterPackageResourceUsageIds.AnimationClipGroup)
                return "AnimationProfile";
            if (entry.Usage == CharacterPackageResourceUsageIds.PreviewThumbnail)
                return "Ui";
            return "Custom";
        }

        private static float ComputeTraceLength(WeaponTraceProfile trace)
        {
            if (trace == null || trace.StartPose == null || trace.EndPose == null || trace.StartPose.Position == null || trace.EndPose.Position == null)
                return 0f;
            float dx = trace.EndPose.Position.X - trace.StartPose.Position.X;
            float dy = trace.EndPose.Position.Y - trace.StartPose.Position.Y;
            float dz = trace.EndPose.Position.Z - trace.StartPose.Position.Z;
            return (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        private static string ToDisplayName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;
            var builder = new StringBuilder();
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (i > 0 && char.IsUpper(c))
                    builder.Append(' ');
                builder.Append(i == 0 ? char.ToUpperInvariant(c) : c);
            }

            return builder.ToString();
        }

        private static bool IsHead(string partId)
        {
            return NormalizeSegment(partId).Contains("head");
        }

        private static bool ContainsTag(List<string> tags, string tag)
        {
            if (tags == null)
                return false;
            for (int i = 0; i < tags.Count; i++)
            {
                if (string.Equals(tags[i], tag, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static FieldValue Str(string value)
        {
            return FieldValue.FromScalar(value ?? string.Empty);
        }

        private static FieldValue Int(int value)
        {
            return FieldValue.FromScalar(value.ToString(CultureInfo.InvariantCulture));
        }

        private static FieldValue Float(float value)
        {
            return FieldValue.FromScalar(value.ToString("R", CultureInfo.InvariantCulture));
        }

        private static FieldValue Bool(bool value)
        {
            return FieldValue.FromScalar(value ? "true" : "false");
        }

        private static FieldValue List(params string[] values)
        {
            var items = new List<FieldValue>();
            if (values != null)
            {
                for (int i = 0; i < values.Length; i++)
                    items.Add(Str(values[i]));
            }

            return FieldValue.FromList(items);
        }

        private static FieldValue Map(params (string Key, FieldValue Value)[] values)
        {
            var map = new Dictionary<string, FieldValue>(StringComparer.Ordinal);
            for (int i = 0; i < values.Length; i++)
                map[values[i].Key] = values[i].Value;
            return FieldValue.FromMap(map);
        }

        private static string NormalizeSegment(string value)
        {
            return CharacterPackageResourceKeyGenerator.NormalizeSegment(value);
        }

        private static string BuildSourcePackageCanonicalText(CharacterResourcePackage package)
        {
            if (package == null)
                return string.Empty;

            var builder = new StringBuilder();
            if (package.Manifest != null)
            {
                builder.Append("manifest|").Append(package.Manifest.PackageId).Append('|').Append(package.Manifest.StableId).Append('|').Append(package.Manifest.Version).Append('\n');
                if (package.Manifest.CoordinateConvention != null)
                    builder.Append("coord|").Append(package.Manifest.CoordinateConvention.UpAxis).Append('|').Append(package.Manifest.CoordinateConvention.ForwardAxis).Append('|').Append(package.Manifest.CoordinateConvention.Handedness).Append('|').Append(package.Manifest.CoordinateConvention.UnitScaleMeters.ToString("R", CultureInfo.InvariantCulture)).Append('\n');
            }

            CharacterPackageResourceCatalog catalog = package.ResourceCatalog ?? new CharacterPackageResourceCatalog();
            var resourceLines = new List<string>();
            for (int i = 0; i < catalog.Entries.Count; i++)
            {
                CharacterPackageResourceEntry entry = catalog.Entries[i];
                if (entry != null)
                    resourceLines.Add(entry.ResourceKey + "|" + entry.StableId + "|" + entry.RelativePath + "|" + CharacterPackageResourcePipeline.GetDeclaredContentHash(entry) + "|" + CharacterPackageResourcePipeline.ComputeImportHash(entry) + "|" + CharacterPackageResourcePipeline.ComputeDependencyHash(entry, catalog));
            }

            resourceLines.Sort(StringComparer.Ordinal);
            for (int i = 0; i < resourceLines.Count; i++)
                builder.Append("resource|").Append(resourceLines[i]).Append('\n');

            CharacterAuthoringGeometry geometry = package.Geometry ?? new CharacterAuthoringGeometry();
            for (int i = 0; i < geometry.BodyParts.Count; i++)
                builder.Append("part|").Append(geometry.BodyParts[i].PartId).Append('|').Append(geometry.BodyParts[i].ParentPartId).Append('|').Append(geometry.BodyParts[i].DefaultHitZoneId).Append('\n');
            for (int i = 0; i < geometry.Colliders.Count; i++)
                builder.Append("collider|").Append(geometry.Colliders[i].ColliderId).Append('|').Append(geometry.Colliders[i].PartId).Append('|').Append(geometry.Colliders[i].HitZoneId).Append('|').Append(geometry.Colliders[i].Shape).Append('\n');
            for (int i = 0; i < geometry.Sockets.Count; i++)
                builder.Append("socket|").Append(geometry.Sockets[i].SocketId).Append('|').Append(geometry.Sockets[i].ParentPartId).Append('|').Append(geometry.Sockets[i].LocatorPath).Append('\n');
            for (int i = 0; i < geometry.WeaponAttachments.Count; i++)
                builder.Append("attachment|").Append(geometry.WeaponAttachments[i].WeaponId).Append('|').Append(geometry.WeaponAttachments[i].EquipSlot).Append('|').Append(geometry.WeaponAttachments[i].AttachSocketId).Append('|').Append(geometry.WeaponAttachments[i].TraceId).Append('\n');
            return builder.ToString();
        }

        private static string BuildConfigPatchCanonicalText(CharacterAuthoringCompiledConfigPatch configPatch)
        {
            var builder = new StringBuilder();
            if (configPatch == null || configPatch.Patch == null)
                return builder.ToString();

            for (int i = 0; i < configPatch.Patch.Entries.Count; i++)
            {
                PatchEntry entry = configPatch.Patch.Entries[i];
                builder.Append(entry.Source).Append('|').Append(entry.Id).Append('|').Append(entry.Operation).Append('\n');
                var keys = new List<string>(entry.Fields.Keys);
                keys.Sort(StringComparer.Ordinal);
                for (int j = 0; j < keys.Count; j++)
                    builder.Append(keys[j]).Append('=').Append(CanonicalFieldValue(entry.Fields[keys[j]])).Append('\n');
            }

            return builder.ToString();
        }

        private static string BuildGeometryBindingCanonicalText(CharacterAuthoringGeometryBinding binding)
        {
            var builder = new StringBuilder();
            if (binding == null)
                return builder.ToString();

            builder.Append(binding.PackageId).Append('|').Append(binding.BodyProfileStableId).Append('|').Append(binding.CoordinateConversion.RequiresConversion).Append('\n');
            for (int i = 0; i < binding.BodyColliders.Count; i++)
                builder.Append("collider|").Append(binding.BodyColliders[i].ColliderId).Append('|').Append(binding.BodyColliders[i].PartId).Append('|').Append(binding.BodyColliders[i].HitZoneId).Append('|').Append(binding.BodyColliders[i].Shape).Append('\n');
            for (int i = 0; i < binding.Sockets.Count; i++)
                builder.Append("socket|").Append(binding.Sockets[i].SocketId).Append('|').Append(binding.Sockets[i].ParentPartId).Append('|').Append(binding.Sockets[i].LocatorPath).Append('\n');
            for (int i = 0; i < binding.WeaponAttachments.Count; i++)
                builder.Append("attachment|").Append(binding.WeaponAttachments[i].WeaponId).Append('|').Append(binding.WeaponAttachments[i].EquipSlot).Append('|').Append(binding.WeaponAttachments[i].AttachSocketId).Append('|').Append(binding.WeaponAttachments[i].TraceId).Append('\n');
            return builder.ToString();
        }

        private static string BuildResourceMappingCanonicalText(CharacterPackageResourceMapping mapping)
        {
            var builder = new StringBuilder();
            if (mapping == null)
                return builder.ToString();

            for (int i = 0; i < mapping.Entries.Count; i++)
            {
                CharacterPackageResourceMappingEntry entry = mapping.Entries[i];
                builder.Append(entry.PackageResourceKey).Append('|').Append(entry.ProjectResourceKey).Append('|').Append(entry.ImportTargetPath).Append('|').Append(entry.DeclaredContentHash).Append('|').Append(entry.ImportHash).Append('|').Append(entry.DependencyHash).Append('|');
                AppendPose(builder, entry.ModelWrapperPose);
            }

            return builder.ToString();
        }

        private static string BuildWritePlanCanonicalText(CharacterUnityImportWritePlan plan)
        {
            var builder = new StringBuilder();
            if (plan == null)
                return builder.ToString();

            builder.Append(plan.TargetRootPath).Append('|').Append(plan.CanWriteToUnityProject).Append('|').Append(plan.CanSpawnAfterImport).Append('\n');
            for (int i = 0; i < plan.Writes.Count; i++)
                builder.Append(plan.Writes[i].Kind).Append('|').Append(plan.Writes[i].SourcePath).Append('|').Append(plan.Writes[i].TargetPath).Append('|').Append(plan.Writes[i].ContentHash).Append('\n');
            return builder.ToString();
        }

        private static void AppendPose(StringBuilder builder, CharacterAuthoringLocalPose pose)
        {
            if (pose == null)
            {
                builder.Append("pose|null\n");
                return;
            }

            builder.Append("pose|")
                .Append(pose.ParentKind).Append('|')
                .Append(pose.ParentPath).Append('|');
            AppendVector(builder, pose.Position);
            AppendQuaternion(builder, pose.Rotation);
            AppendVector(builder, pose.Scale);
            AppendVector(builder, pose.EulerHint);
            builder.Append('\n');
        }

        private static void AppendVector(StringBuilder builder, CharacterAuthoringVector3 value)
        {
            if (value == null)
            {
                builder.Append("null|null|null|");
                return;
            }

            builder.Append(value.X.ToString("R", CultureInfo.InvariantCulture)).Append('|')
                .Append(value.Y.ToString("R", CultureInfo.InvariantCulture)).Append('|')
                .Append(value.Z.ToString("R", CultureInfo.InvariantCulture)).Append('|');
        }

        private static void AppendQuaternion(StringBuilder builder, CharacterAuthoringQuaternion value)
        {
            if (value == null)
            {
                builder.Append("null|null|null|null|");
                return;
            }

            builder.Append(value.X.ToString("R", CultureInfo.InvariantCulture)).Append('|')
                .Append(value.Y.ToString("R", CultureInfo.InvariantCulture)).Append('|')
                .Append(value.Z.ToString("R", CultureInfo.InvariantCulture)).Append('|')
                .Append(value.W.ToString("R", CultureInfo.InvariantCulture)).Append('|');
        }

        private static string CanonicalFieldValue(FieldValue value)
        {
            if (value == null)
                return string.Empty;
            if (value.Kind == FieldValueKind.Scalar)
                return value.Scalar ?? string.Empty;
            if (value.Kind == FieldValueKind.List)
            {
                var builder = new StringBuilder("[");
                if (value.List != null)
                {
                    for (int i = 0; i < value.List.Count; i++)
                    {
                        if (i > 0)
                            builder.Append(',');
                        builder.Append(CanonicalFieldValue(value.List[i]));
                    }
                }

                builder.Append(']');
                return builder.ToString();
            }

            var mapBuilder = new StringBuilder("{");
            if (value.Map != null)
            {
                var keys = new List<string>(value.Map.Keys);
                keys.Sort(StringComparer.Ordinal);
                for (int i = 0; i < keys.Count; i++)
                {
                    if (i > 0)
                        mapBuilder.Append(',');
                    mapBuilder.Append(keys[i]).Append(':').Append(CanonicalFieldValue(value.Map[keys[i]]));
                }
            }

            mapBuilder.Append('}');
            return mapBuilder.ToString();
        }

        private sealed class GeneratedIds
        {
            public int CharacterId { get; } = 710001;
            public int AttributeProfileId { get; } = 720001;
            public int BodyProfileId { get; } = 730001;
            public int BodyPartStartId { get; } = 740001;
            public int EquipmentSchemaId { get; } = 750001;
            public int LoadoutStartId { get; } = 760001;
            public int StateStartId { get; } = 770001;
            public int WeaponStartId { get; } = 780001;
            public int BaseAbilityLoadoutId { get; } = 790001;
            public int WeaponAbilityLoadoutStartId { get; } = 790002;
            public int StateAbilityLoadoutStartId { get; } = 790100;
            public int ActionSetStartId { get; } = 800001;
            public int PresentationProfileId { get; } = 810001;
            public int SpawnProfileId { get; } = 820001;
            public int BaseAbilityId { get; } = 900001;
            public int WeaponAbilityStartId { get; } = 900002;
            public int StateAbilityStartId { get; } = 900100;
            public int HpAttributeId { get; } = 920001;
            public int StaminaAttributeId { get; } = 920002;
            public int PostureAttributeId { get; } = 920003;
            public int DefaultLoadoutId { get; set; } = 760001;
            public int UnarmedStateId { get { return StateStartId; } }
            public int SingleWeaponStateId { get { return StateStartId + 1; } }
            public int AllWeaponsStateId { get { return StateStartId + 2; } }
        }

        private sealed class LoadoutPlan
        {
            public int DefaultLoadoutId { get; set; }
            public string DefaultLoadoutStableId { get; set; } = string.Empty;
            public Dictionary<string, int> StateIdByLoadoutStableId { get; } = new Dictionary<string, int>(StringComparer.Ordinal);
            public Dictionary<string, string> StateStableIdByLoadoutStableId { get; } = new Dictionary<string, string>(StringComparer.Ordinal);
            public Dictionary<string, int> ActionSetIdByLoadoutStableId { get; } = new Dictionary<string, int>(StringComparer.Ordinal);
            public Dictionary<string, string> ActionSetStableIdByLoadoutStableId { get; } = new Dictionary<string, string>(StringComparer.Ordinal);
            public Dictionary<string, List<WeaponAttachmentProfile>> LoadoutAttachmentsByStableId { get; } = new Dictionary<string, List<WeaponAttachmentProfile>>(StringComparer.Ordinal);
        }
    }
}
