using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using MxFramework.Authoring;
using MxFramework.Authoring.Preview;
using MxFramework.Authoring.Preview.Protocol;

namespace MxFramework.Authoring.Tests;

internal static class Program
{
    public static int Main()
    {
        try
        {
            WorkflowContext_IsScopedToStep();
            Workflow_ContextRoutesByWorkflowId();
            ProjectManifest_ContainsAuthoringIndexes();
            Validator_BlocksBaseWrites();
            Validator_RequiresVisibleBuffTypeFields();
            AuthoringValidate_RoutesByManifest();
            ManifestRequired_FromCustomManifest();
            ManifestAware_ReferenceListNormalization();
            MergePreview_FallbackUsesLayered();
            Precommit_ReportToStatus();
            AiStepContext_IncludesEnumAndAllowedActions();
            PatchMerger_ReturnsLatestRecord();
            PatchMerger_TracksOriginLayer();
            LayeredMerger_BaseOverriddenByMod();
            LayeredMerger_RemoveDeletesBase();
            LayeredMerger_PatchSitsBetween();
            ManifestAware_ReferenceHappyAndFail();
            ManifestAware_EnumHappyAndFail();
            ManifestAware_DamageByAttrSpecificValidation();
            ManifestAware_LocalizationWarning();
            ManifestAware_AssetPathHappyAndFail();
            SchemaVersion_ForwardCompat_RequiresUpgrade();
            SchemaVersion_OldVersion_IsWarningOnly();
            ExitCode_FromReport();
            AiStepContext_IncludesSlices();
            ReportBundleIndex_DescribesStableFiles();
            FieldValue_ListAndMapAreSupported();
            PreviewLocator_RoundTrip();
            PreviewLocator_StaleProcessIgnored();
            PreviewClient_RequiresHandshake().GetAwaiter().GetResult();
            PreviewClient_HandshakeAndLoad().GetAwaiter().GetResult();
            PreviewClient_TokenMismatch().GetAwaiter().GetResult();
            PreviewClient_FullFlow().GetAwaiter().GetResult();
            PreviewClient_DeserializesResultStatusFields().GetAwaiter().GetResult();
            EditorServer_PreviewUnavailableDoesNotThrow500();
            EditorServer_RunPreviewReturnsStructuredApplyFailure().GetAwaiter().GetResult();
            CharacterPackageTests.RunAll();
            ModDiagnoseCommandTests.RunAll();
            Console.WriteLine("MxFramework.Authoring.Tests passed.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    private static void WorkflowContext_IsScopedToStep()
    {
        AuthoringWorkflow workflow = BuiltInContent.CreateBuffWorkflow();
        string context = workflow.CreateStepContext("type-fields");

        Require(context.Contains("workflow=buff.create"), "Workflow id missing from context.");
        Require(context.Contains("step=type-fields"), "Step id missing from context.");
        Require(context.Contains("targetSource=BuffFactoryData"), "Target source missing from context.");
    }

    private static void Workflow_ContextRoutesByWorkflowId()
    {
        AuthoringWorkflow workflow = BuiltInContent.GetWorkflow("buff.create");
        Require(workflow != null && workflow.WorkflowId == "buff.create", "buff.create workflow should be registered.");
        Require(BuiltInContent.GetWorkflow("does-not-exist") == null, "Unknown workflow id should return null.");
        IReadOnlyList<AuthoringWorkflow> all = BuiltInContent.CreateBuiltInWorkflows();
        Require(all.Count >= 1, "Built-in workflow list should contain at least one workflow.");
    }

    private static void Validator_BlocksBaseWrites()
    {
        var manifest = new ModPackageManifest
        {
            PackageId = "test.buff",
            SchemaVersion = "1.0",
            Kind = PackageKind.Mod
        };
        var patch = new PatchDocument
        {
            SchemaVersion = "1.0",
            Source = "BuffFactoryData"
        };
        patch.Entries.Add(new PatchEntry
        {
            Operation = PatchOperation.Upsert,
            Source = "BuffFactoryData",
            Id = "100001",
            Layer = "Base"
        });

        ValidationReport report = PackageValidator.Validate(manifest, new[] { patch });

        Require(report.HasErrors, "Base write should be blocked.");
    }

    private static void Validator_RequiresVisibleBuffTypeFields()
    {
        ProjectAuthoringManifest project = BuiltInContent.CreateProjectManifest();
        var mod = new ModPackageManifest
        {
            PackageId = "test.buff",
            SchemaVersion = "1.0",
            Kind = PackageKind.Mod
        };
        var patch = new PatchDocument { SchemaVersion = "1.0", Source = "BuffFactoryData" };
        var entry = new PatchEntry
        {
            Operation = PatchOperation.Upsert,
            Source = "BuffFactoryData",
            Id = "100001",
            Layer = "Mod"
        };
        entry.Fields["Id"] = "100001";
        entry.Fields["Type"] = "DamageByAttr";
        entry.Fields["Name"] = "buff.test.name";
        entry.Fields["Target"] = "Target";
        entry.Fields["AddType"] = "RefreshAllTime";
        entry.Fields["Duration"] = "5000";
        patch.Entries.Add(entry);

        ValidationReport report = AuthoringValidate.Run(project, mod, new[] { patch });

        Require(report.HasErrors, "DamageByAttr should require type-specific fields.");
        Require(report.Issues.Exists(issue => issue.Field == "Values" && issue.Code == "entry.requiredField"), "DamageByAttr Values requirement missing.");
        Require(report.Issues.Exists(issue => issue.Field == "DmgType" && issue.Code == "entry.requiredField"), "DamageByAttr DmgType requirement missing.");
    }

    private static void AuthoringValidate_RoutesByManifest()
    {
        var mod = new ModPackageManifest { PackageId = "x", SchemaVersion = "1.0", Kind = PackageKind.Mod };
        var patch = new PatchDocument { SchemaVersion = "1.0", Source = "BuffFactoryData" };
        var entry = new PatchEntry { Operation = PatchOperation.Upsert, Source = "BuffFactoryData", Id = "1", Layer = "Mod" };
        entry.Fields["Type"] = "DamageByAttr";
        patch.Entries.Add(entry);

        // Without manifest -> Package path: Values/DmgType not enforced (no requiredField issues for those)
        ValidationReport noManifest = AuthoringValidate.Run(null, mod, new[] { patch });
        Require(!noManifest.Issues.Exists(i => i.Code == "entry.requiredField"), "PackageValidator path should not emit requiredField issues.");

        // With manifest -> ManifestAware: requires Values/DmgType.
        ProjectAuthoringManifest project = BuiltInContent.CreateProjectManifest();
        ValidationReport withManifest = AuthoringValidate.Run(project, mod, new[] { patch });
        Require(withManifest.Issues.Exists(i => i.Code == "entry.requiredField" && i.Field == "Values"), "ManifestAware path should require Values.");
    }

    private static void ManifestRequired_FromCustomManifest()
    {
        var custom = new ProjectAuthoringManifest { ProjectId = "custom" };
        var schema = new ConfigSchema { SchemaId = "TinySource" };
        schema.Fields.Add(new SchemaField { Name = "Foo", DisplayName = "Foo", Type = FieldType.String, Required = true });
        custom.Schemas.Add(schema);

        var mod = new ModPackageManifest { PackageId = "tiny", SchemaVersion = "1.0", Kind = PackageKind.Mod };
        var patch = new PatchDocument { SchemaVersion = "1.0", Source = "TinySource" };
        var entry = new PatchEntry { Operation = PatchOperation.Upsert, Source = "TinySource", Id = "1", Layer = "Mod" };
        entry.Fields["Bar"] = "x";
        patch.Entries.Add(entry);

        ValidationReport report = AuthoringValidate.Run(custom, mod, new[] { patch });
        Require(report.Issues.Exists(i => i.Code == "entry.requiredField" && i.Field == "Foo"), "Custom manifest should require Foo.");
        Require(!report.Issues.Exists(i => i.Field == "Values"), "Custom manifest must not leak BuiltIn buff fields.");
        Require(!report.Issues.Exists(i => i.Field == "DmgType"), "Custom manifest must not leak BuiltIn buff fields.");
    }

    private static void ManifestAware_ReferenceListNormalization()
    {
        var manifest = BuiltInContent.CreateProjectManifest();
        var mod = new ModPackageManifest { PackageId = "ref", SchemaVersion = "1.0", Kind = PackageKind.Mod };

        // Single-value reference: Values is String not Reference, so we use AddBuffList (IsList=true) for list semantics
        var listPatch = MakeBuffPatch("Condition", ("ConditionType", "CheckHP"));
        listPatch.Entries[0].Fields["AddBuffList"] = "100001,100001";
        ValidationReport listReport = AuthoringValidate.Run(manifest, mod, new[] { listPatch });
        Require(!listReport.Issues.Exists(i => i.Code == "entry.referenceMissing" && i.Field == "AddBuffList"), "List reference comma-string should split.");

        // List form
        var listPatch2 = MakeBuffPatch("Condition", ("ConditionType", "CheckHP"));
        listPatch2.Entries[0].Fields["AddBuffList"] = FieldValue.FromList(new[] { FieldValue.FromScalar("100001") });
        ValidationReport listReport2 = AuthoringValidate.Run(manifest, mod, new[] { listPatch2 });
        Require(!listReport2.Issues.Exists(i => i.Code == "entry.referenceMissing" && i.Field == "AddBuffList"), "List reference List form should validate.");
    }

    private static void MergePreview_FallbackUsesLayered()
    {
        var modDoc = MakeDoc("BuffFactoryData", MakeEntry("1", "Mod", PatchOperation.Upsert, ("Name", "fromMod")));
        MergePreview preview = LayeredMerger.Merge(null, null, new[] { modDoc });
        Require(preview.Records.Count == 1, "Fallback should produce one record.");
        Require(preview.Records[0].OriginLayer == "Mod", "Fallback layered merge should set OriginLayer=Mod.");
    }

    private static void Precommit_ReportToStatus()
    {
        var ready = new ValidationReport();
        Require(AuthoringPrecommit.Status(AuthoringExitCodes.From(ready)) == "ready", "ready map");

        var blocked = new ValidationReport();
        blocked.Issues.Add(new ValidationIssue { Severity = IssueSeverity.Error, Code = "x" });
        Require(AuthoringPrecommit.Status(AuthoringExitCodes.From(blocked)) == "blocked", "blocked map");

        var upgrade = new ValidationReport { RequiresUpgrade = true };
        Require(AuthoringPrecommit.Status(AuthoringExitCodes.From(upgrade)) == "upgrade", "upgrade map");

        string text = AuthoringPrecommit.BuildText("samples/buff-mod", "blocked", 2, blocked);
        Require(text.Contains("status=blocked") && text.Contains("exit=2") && text.Contains("errors=1"), "Precommit text should report status/exit/errors.");
    }

    private static void AiStepContext_IncludesEnumAndAllowedActions()
    {
        var manifest = BuiltInContent.CreateProjectManifest();
        var mod = new ModPackageManifest { PackageId = "test", SchemaVersion = "1.0", Kind = PackageKind.Mod };
        PatchDocument doc = MakeBuffPatch("DamageByAttr");
        AuthoringWorkflow workflow = BuiltInContent.GetWorkflow("buff.create");
        string text = workflow.CreateAiStepContext("type-fields", manifest, mod, new[] { doc });
        Require(text.Contains("enumSlice="), "AI context should expose enumSlice key.");
        Require(text.Contains("referenceSummary="), "AI context should expose referenceSummary key.");
        Require(text.Contains("allowedActions="), "AI context should expose allowedActions key.");
        Require(text.Contains("editField"), "allowedActions should include editField.");
    }

    private static void ProjectManifest_ContainsAuthoringIndexes()
    {
        ProjectAuthoringManifest manifest = BuiltInContent.CreateProjectManifest();

        Require(manifest.Schemas.Count == 1, "Manifest should contain one schema.");
        Require(manifest.Enums.Count >= 2, "Manifest should contain enum domains.");
        Require(manifest.References.Count == 1, "Manifest should contain reference index.");
        Require(manifest.Workflows.Count == 1, "Manifest should contain workflow.");
        Require(manifest.Localization.Count >= 2, "Manifest should contain localization entries.");
        Require(manifest.AssetWhitelistPrefixes.Count >= 1, "Manifest should expose asset whitelist prefixes.");
    }

    private static void PatchMerger_ReturnsLatestRecord()
    {
        var patch = new PatchDocument { SchemaVersion = "1.0", Source = "BuffFactoryData" };
        var first = new PatchEntry
        {
            Operation = PatchOperation.Upsert,
            Source = "BuffFactoryData",
            Id = "100001",
            Layer = "Mod"
        };
        first.Fields["Name"] = "buff.fire.name";
        var second = new PatchEntry
        {
            Operation = PatchOperation.Upsert,
            Source = "BuffFactoryData",
            Id = "100001",
            Layer = "Mod"
        };
        second.Fields["Name"] = "buff.fire.v2.name";
        patch.Entries.Add(first);
        patch.Entries.Add(second);

        MergePreview preview = PatchMerger.Merge(patch);

        Require(preview.Records.Count == 1, "Merge should contain one final record.");
        Require(preview.Records[0].ChangeKind == "Replaced", "Second upsert should replace first.");
        Require(preview.Records[0].Fields["Name"].Scalar == "buff.fire.v2.name", "Latest field value was not preserved.");
    }

    private static void PatchMerger_TracksOriginLayer()
    {
        var patch = new PatchDocument { SchemaVersion = "1.0", Source = "BuffFactoryData" };
        var entry = new PatchEntry { Operation = PatchOperation.Upsert, Source = "BuffFactoryData", Id = "1", Layer = "Mod" };
        entry.Fields["Name"] = "x";
        patch.Entries.Add(entry);

        MergePreview preview = PatchMerger.Merge(patch);
        Require(preview.Records[0].OriginLayer == "Mod", "OriginLayer should track entry layer.");
        Require(preview.Records[0].FieldOrigins["Name"] == "Mod", "Field origins should track per-field layer.");
    }

    private static PatchDocument MakeDoc(string source, params PatchEntry[] entries)
    {
        var doc = new PatchDocument { SchemaVersion = "1.0", Source = source };
        for (int i = 0; i < entries.Length; i++) doc.Entries.Add(entries[i]);
        return doc;
    }

    private static PatchEntry MakeEntry(string id, string layer, PatchOperation op, params (string k, string v)[] fields)
    {
        var e = new PatchEntry { Operation = op, Source = "BuffFactoryData", Id = id, Layer = layer };
        for (int i = 0; i < fields.Length; i++)
            e.Fields[fields[i].k] = fields[i].v;
        return e;
    }

    private static void LayeredMerger_BaseOverriddenByMod()
    {
        var baseDoc = MakeDoc("BuffFactoryData", MakeEntry("1", "Base", PatchOperation.Upsert, ("Name", "fromBase"), ("Duration", "1000")));
        var modDoc = MakeDoc("BuffFactoryData", MakeEntry("1", "Mod", PatchOperation.Upsert, ("Name", "fromMod")));

        MergePreview preview = LayeredMerger.Merge(new[] { baseDoc }, null, new[] { modDoc });
        Require(preview.Records.Count == 1, "Layered merge should produce one record.");
        Require(preview.Records[0].Fields["Name"].Scalar == "fromMod", "Mod layer should override Base name.");
        Require(preview.Records[0].FieldOrigins["Name"] == "Mod", "Mod-overridden field origin should be Mod.");
        Require(preview.Records[0].FieldOrigins["Duration"] == "Base", "Untouched field origin should remain Base.");
    }

    private static void LayeredMerger_RemoveDeletesBase()
    {
        var baseDoc = MakeDoc("BuffFactoryData", MakeEntry("1", "Base", PatchOperation.Upsert, ("Name", "fromBase")));
        var modDoc = MakeDoc("BuffFactoryData", MakeEntry("1", "Mod", PatchOperation.Remove));

        MergePreview preview = LayeredMerger.Merge(new[] { baseDoc }, null, new[] { modDoc });
        Require(preview.Records.Count == 1, "Removed entry should remain as marker.");
        Require(preview.Records[0].ChangeKind == "Removed", "ChangeKind should be Removed.");
    }

    private static void LayeredMerger_PatchSitsBetween()
    {
        var baseDoc = MakeDoc("BuffFactoryData", MakeEntry("1", "Base", PatchOperation.Upsert, ("Name", "fromBase"), ("Duration", "1000")));
        var patchDoc = MakeDoc("BuffFactoryData", MakeEntry("1", "Patch", PatchOperation.Upsert, ("Duration", "2000")));
        var modDoc = MakeDoc("BuffFactoryData", MakeEntry("1", "Mod", PatchOperation.Upsert, ("Name", "fromMod")));

        MergePreview preview = LayeredMerger.Merge(new[] { baseDoc }, new[] { patchDoc }, new[] { modDoc });
        Require(preview.Records[0].Fields["Duration"].Scalar == "2000", "Patch layer should override Base duration.");
        Require(preview.Records[0].FieldOrigins["Duration"] == "Patch", "Duration origin should be Patch.");
        Require(preview.Records[0].FieldOrigins["Name"] == "Mod", "Name origin should be Mod.");
    }

    private static (ProjectAuthoringManifest manifest, ModPackageManifest mod) MakeFixture()
    {
        var manifest = BuiltInContent.CreateProjectManifest();
        var mod = new ModPackageManifest { PackageId = "test.fixture", SchemaVersion = "1.0", Kind = PackageKind.Mod };
        return (manifest, mod);
    }

    private static PatchDocument MakeBuffPatch(string buffType, params (string k, string v)[] extras)
    {
        var entry = new PatchEntry { Operation = PatchOperation.Upsert, Source = "BuffFactoryData", Id = "100001", Layer = "Mod" };
        entry.Fields["Id"] = "100001";
        entry.Fields["Type"] = buffType;
        entry.Fields["Name"] = "buff.sample.fire.name";
        entry.Fields["Target"] = "Target";
        entry.Fields["AddType"] = "RefreshAllTime";
        entry.Fields["Duration"] = "5000";
        entry.Fields["Values"] = "caster.Attack * 0.5";
        entry.Fields["DmgType"] = "Magic";
        for (int i = 0; i < extras.Length; i++) entry.Fields[extras[i].k] = extras[i].v;
        var doc = new PatchDocument { SchemaVersion = "1.0", Source = "BuffFactoryData" };
        doc.Entries.Add(entry);
        return doc;
    }

    private static void ManifestAware_ReferenceHappyAndFail()
    {
        var (manifest, mod) = MakeFixture();
        // Happy path: reference id exists (BuiltInContent reference index has 100001)
        var happy = MakeBuffPatch("Condition", ("ConditionType", "CheckHP"), ("AddBuffList", "100001"));
        ValidationReport ok = ManifestAwareValidator.Validate(manifest, mod, new[] { happy });
        Require(!ok.Issues.Exists(i => i.Code == "entry.referenceMissing" && i.Field == "AddBuffList"), "Happy reference should not flag.");

        // Fail: missing reference id
        var fail = MakeBuffPatch("Condition", ("ConditionType", "CheckHP"), ("AddBuffList", "999999"));
        ValidationReport bad = ManifestAwareValidator.Validate(manifest, mod, new[] { fail });
        Require(bad.Issues.Exists(i => i.Code == "entry.referenceMissing" && i.Field == "AddBuffList"), "Missing reference should be flagged.");
    }

    private static void ManifestAware_EnumHappyAndFail()
    {
        var (manifest, mod) = MakeFixture();
        var happy = MakeBuffPatch("DamageByAttr");
        ValidationReport ok = ManifestAwareValidator.Validate(manifest, mod, new[] { happy });
        Require(!ok.Issues.Exists(i => i.Code == "entry.enumInvalid" && i.Field == "DmgType"), "Magic should be a valid DmgType.");

        var fail = MakeBuffPatch("DamageByAttr", ("DmgType", "Bogus"));
        ValidationReport bad = ManifestAwareValidator.Validate(manifest, mod, new[] { fail });
        Require(bad.Issues.Exists(i => i.Code == "entry.enumInvalid" && i.Field == "DmgType"), "Bogus enum should be flagged.");
    }

    private static void ManifestAware_DamageByAttrSpecificValidation()
    {
        var (manifest, mod) = MakeFixture();
        var badFormula = MakeBuffPatch("DamageByAttr", ("Values", "target.Hp / caster.Attack"));
        ValidationReport formulaReport = ManifestAwareValidator.Validate(manifest, mod, new[] { badFormula });
        Require(formulaReport.Issues.Exists(i => i.Code == "buff.damageByAttr.valuesUnsupported" && i.Field == "Values"), "Unsupported DamageByAttr formula should be flagged.");

        var badTiming = MakeBuffPatch("DamageByAttr", ("Duration", "1000"), ("HitCooldown", "2000"));
        ValidationReport timingReport = ManifestAwareValidator.Validate(manifest, mod, new[] { badTiming });
        Require(timingReport.Issues.Exists(i => i.Code == "buff.damageByAttr.hitCooldownLongerThanDuration" && i.Severity == IssueSeverity.Warning), "Long HitCooldown should warn.");
    }

    private static void ManifestAware_LocalizationWarning()
    {
        var (manifest, mod) = MakeFixture();
        var happy = MakeBuffPatch("DamageByAttr");
        ValidationReport ok = ManifestAwareValidator.Validate(manifest, mod, new[] { happy });
        Require(!ok.Issues.Exists(i => i.Code == "entry.localeMissing" && i.Field == "Name"), "Known locale key should not warn.");

        var fail = MakeBuffPatch("DamageByAttr", ("Name", "buff.unknown.locale"));
        ValidationReport bad = ManifestAwareValidator.Validate(manifest, mod, new[] { fail });
        Require(bad.Issues.Exists(i => i.Code == "entry.localeMissing" && i.Severity == IssueSeverity.Warning), "Missing locale should warn.");
    }

    private static void ManifestAware_AssetPathHappyAndFail()
    {
        var (manifest, mod) = MakeFixture();
        var happy = MakeBuffPatch("DamageByAttr", ("HitEffect", "Effects/Hit/FireSmall"));
        ValidationReport ok = ManifestAwareValidator.Validate(manifest, mod, new[] { happy });
        Require(!ok.Issues.Exists(i => i.Code == "entry.assetPathDenied" && i.Field == "HitEffect"), "Whitelisted path should not flag.");

        var fail = MakeBuffPatch("DamageByAttr", ("HitEffect", "C:/secret/forbidden.fbx"));
        ValidationReport bad = ManifestAwareValidator.Validate(manifest, mod, new[] { fail });
        Require(bad.Issues.Exists(i => i.Code == "entry.assetPathDenied" && i.Field == "HitEffect"), "Non-whitelisted path should be denied.");
    }

    private static void SchemaVersion_ForwardCompat_RequiresUpgrade()
    {
        var (manifest, mod) = MakeFixture();
        mod.SchemaVersion = "2.5";
        var happy = MakeBuffPatch("DamageByAttr");
        happy.SchemaVersion = "2.5";
        ValidationReport report = ManifestAwareValidator.Validate(manifest, mod, new[] { happy });
        Require(report.RequiresUpgrade, "Future schemaVersion should set RequiresUpgrade.");
        Require(report.Issues.Exists(i => i.Code == "patch.schemaVersion.unknown"), "Should warn about unknown schemaVersion.");
        Require(AuthoringExitCodes.From(report) == AuthoringExitCodes.SchemaIncompatible, "Exit code should be 3 for upgrades.");
    }

    private static void SchemaVersion_OldVersion_IsWarningOnly()
    {
        var (manifest, mod) = MakeFixture();
        var happy = MakeBuffPatch("DamageByAttr");
        happy.SchemaVersion = "0.9";
        ValidationReport report = ManifestAwareValidator.Validate(manifest, mod, new[] { happy });
        Require(!report.RequiresUpgrade, "Old schemaVersion should not require upgrade.");
        Require(report.Issues.Exists(i => i.Code == "patch.schemaVersion.unknown" && i.Severity == IssueSeverity.Warning), "Old version should be warning.");
        Require(!report.Issues.Exists(i => i.Code == "patch.schemaVersion.unknown" && i.Severity == IssueSeverity.Error), "Old version must not be blocking error.");
    }

    private static void ExitCode_FromReport()
    {
        var ready = new ValidationReport();
        Require(AuthoringExitCodes.From(ready) == 0, "Empty report should map to 0.");

        var blocked = new ValidationReport();
        blocked.Issues.Add(new ValidationIssue { Severity = IssueSeverity.Error, Code = "x" });
        Require(AuthoringExitCodes.From(blocked) == 2, "Errors should map to 2.");

        var upgrade = new ValidationReport { RequiresUpgrade = true };
        Require(AuthoringExitCodes.From(upgrade) == 3, "RequiresUpgrade should map to 3.");
    }

    private static void AiStepContext_IncludesSlices()
    {
        var manifest = BuiltInContent.CreateProjectManifest();
        var mod = new ModPackageManifest { PackageId = "test.context", SchemaVersion = "1.0", Kind = PackageKind.Mod };
        PatchDocument doc = MakeBuffPatch("DamageByAttr");
        AuthoringWorkflow workflow = BuiltInContent.GetWorkflow("buff.create");
        string text = workflow.CreateAiStepContext("type-fields", manifest, mod, new[] { doc });
        Require(text.Contains("schemaSlice="), "AI context should expose schemaSlice key.");
        Require(text.Contains("draftSlice="), "AI context should expose draftSlice key.");
        Require(text.Contains("validationIssues="), "AI context should expose validationIssues key.");
    }

    private static void ReportBundleIndex_DescribesStableFiles()
    {
        var bundle = new ReportBundle
        {
            Package = new ModPackageManifest { PackageId = "test.buff" },
            Validation = new ValidationReport { PackageId = "test.buff" }
        };
        bundle.MergePreviews.Add(new MergePreview());

        var index = new ReportBundleIndex
        {
            PackageId = bundle.Package.PackageId,
            Status = bundle.Validation.HasErrors ? "blocked" : "ready"
        };
        index.Files.Add("mod.json");
        index.Files.Add("validation_report.json");
        index.Files.Add("validation_report.txt");
        index.Files.Add("merge_preview.json");
        index.Files.Add("report_index.json");

        Require(index.Status == "ready", "Empty validation report should be ready.");
        Require(index.Files.Contains("validation_report.txt"), "Report bundle index should include text report.");
        Require(index.Files.Contains("merge_preview.json"), "Report bundle index should include merge preview.");
    }

    private static void FieldValue_ListAndMapAreSupported()
    {
        var entry = new PatchEntry { Operation = PatchOperation.Upsert, Source = "BuffFactoryData", Id = "1", Layer = "Mod" };
        entry.Fields["Scalar"] = "abc";
        entry.Fields["List"] = FieldValue.FromList(new[] { FieldValue.FromScalar("a"), FieldValue.FromScalar("b") });
        var inner = new Dictionary<string, FieldValue> { { "k", FieldValue.FromScalar("v") } };
        entry.Fields["Map"] = FieldValue.FromMap(inner);

        Require(entry.Fields["Scalar"].Kind == FieldValueKind.Scalar, "Scalar kind round-trip.");
        Require(entry.Fields["List"].Kind == FieldValueKind.List, "List kind round-trip.");
        Require(entry.Fields["Map"].Kind == FieldValueKind.Map, "Map kind round-trip.");
        Require(entry.Fields.GetScalar("Scalar") == "abc", "GetScalar helper should read scalar value.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static string CreateTempPreviewDir()
    {
        string dir = Path.Combine(Path.GetTempPath(), "mx-preview-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        Environment.SetEnvironmentVariable(PreviewConnectionLocator.EnvOverride, dir);
        return dir;
    }

    private static void PreviewLocator_RoundTrip()
    {
        string dir = CreateTempPreviewDir();
        try
        {
            var descriptor = new PreviewConnectionDescriptor
            {
                SchemaVersion = "1.0",
                Endpoint = "ws://127.0.0.1:54123/preview",
                Port = 54123,
                Token = "abc",
                ProcessId = Process.GetCurrentProcess().Id,
                GameVersion = "0.3.1",
                StartedAt = "2026-05-06T10:11:12Z",
                Capabilities = { "preview.handshake", "preview.loadPatch" }
            };
            string path = PreviewConnectionLocator.WriteForTests(dir, descriptor);
            Require(File.Exists(path), "Locator should write descriptor file.");
            PreviewConnectionDescriptor read = PreviewConnectionLocator.TryRead();
            Require(read != null, "Locator should round-trip the descriptor.");
            Require(read.Endpoint == descriptor.Endpoint, "Endpoint should match.");
            Require(read.Token == "abc", "Token should match.");
            Require(read.Capabilities.Count == 2, "Capabilities should round-trip.");
        }
        finally
        {
            Environment.SetEnvironmentVariable(PreviewConnectionLocator.EnvOverride, null);
            try { Directory.Delete(dir, true); } catch { }
        }
    }

    private static void PreviewLocator_StaleProcessIgnored()
    {
        string dir = CreateTempPreviewDir();
        try
        {
            var descriptor = new PreviewConnectionDescriptor
            {
                Endpoint = "ws://127.0.0.1:54124/preview",
                Token = "x",
                ProcessId = 1, // 几乎不可能存在的低位 PID（pid=1 在 Windows 是 System Idle Process，仍可读取，强制使用 0）
                Port = 54124
            };
            descriptor.ProcessId = 0;
            PreviewConnectionLocator.WriteForTests(dir, descriptor);
            PreviewConnectionDescriptor read = PreviewConnectionLocator.TryRead();
            Require(read == null, "Stale processId should result in null descriptor.");
        }
        finally
        {
            Environment.SetEnvironmentVariable(PreviewConnectionLocator.EnvOverride, null);
            try { Directory.Delete(dir, true); } catch { }
        }
    }

    private static async Task PreviewClient_RequiresHandshake()
    {
        string dir = CreateTempPreviewDir();
        using var server = new MockPreviewServer(dir);
        try
        {
            var client = new WebSocketPreviewClient(new Uri(server.Descriptor.Endpoint), server.Descriptor.Token);
            await client.ConnectAsync();
            bool threw = false;
            try
            {
                await client.ApplyBuffAsync(new ApplyBuffParams { BuffId = "100001" });
            }
            catch (PreviewNotHandshakedException)
            {
                threw = true;
            }
            await client.DisposeAsync();
            Require(threw, "Calling RPC before handshake should throw PreviewNotHandshakedException.");
        }
        finally
        {
            Environment.SetEnvironmentVariable(PreviewConnectionLocator.EnvOverride, null);
            try { Directory.Delete(dir, true); } catch { }
        }
    }

    private static async Task PreviewClient_HandshakeAndLoad()
    {
        string dir = CreateTempPreviewDir();
        using var server = new MockPreviewServer(dir);
        try
        {
            var client = new WebSocketPreviewClient(new Uri(server.Descriptor.Endpoint), server.Descriptor.Token);
            HandshakeResult handshake = await client.HandshakeAsync("MxAuthoringTest", "0.3.0");
            Require(handshake != null && handshake.ServerName.Contains("MxRuntimePreview"), "Handshake should return server info.");

            LoadPatchResult load = await client.LoadPatchAsync(new LoadPatchParams { PackageId = "test.preview" });
            Require(load.LoadedPatchIds.Contains("test.preview"), "loadPatch should echo packageId.");
            await client.DisposeAsync();
        }
        finally
        {
            Environment.SetEnvironmentVariable(PreviewConnectionLocator.EnvOverride, null);
            try { Directory.Delete(dir, true); } catch { }
        }
    }

    private static async Task PreviewClient_TokenMismatch()
    {
        string dir = CreateTempPreviewDir();
        using var server = new MockPreviewServer(dir, token: "server-token");
        try
        {
            var client = new WebSocketPreviewClient(new Uri(server.Descriptor.Endpoint), "wrong-token");
            bool threw = false;
            try
            {
                await client.HandshakeAsync("MxAuthoringTest", "0.3.0");
            }
            catch (PreviewTokenMismatchException ex)
            {
                threw = true;
                Require(ex.ErrorCode == PreviewError.TokenMismatch, "Mismatch exception should expose error code 1002.");
            }
            await client.DisposeAsync();
            Require(threw, "Token mismatch should throw PreviewTokenMismatchException.");
        }
        finally
        {
            Environment.SetEnvironmentVariable(PreviewConnectionLocator.EnvOverride, null);
            try { Directory.Delete(dir, true); } catch { }
        }
    }

    private static async Task PreviewClient_FullFlow()
    {
        string dir = CreateTempPreviewDir();
        using var server = new MockPreviewServer(dir);
        try
        {
            var client = new WebSocketPreviewClient(new Uri(server.Descriptor.Endpoint), server.Descriptor.Token);
            await client.HandshakeAsync("MxAuthoringTest", "0.3.0");
            await client.LoadPatchAsync(new LoadPatchParams { PackageId = "sample.buff.preview" });
            RuntimePreviewResult applied = await client.ApplyBuffAsync(new ApplyBuffParams { BuffId = "100001", WaitTicks = 0 });
            Require(applied.Success, "applyBuff should succeed in mock server.");
            Require(applied.PreviewMode == "dummy", "RuntimePreviewResult.previewMode key must be populated.");
            Require(applied.ConfigMetadata != null && applied.ConfigMetadata.SourceId == "mock.preview", "RuntimePreviewResult.configMetadata key must be populated.");
            Require(applied.BuffSnapshots.Count > 0, "RuntimePreviewResult.buffSnapshots key must be populated.");
            Require(applied.AttributeChanges.Count > 0, "RuntimePreviewResult.attributeChanges key must be populated.");
            Require(applied.DamageTicks.Count > 0, "RuntimePreviewResult.damageTicks key must be populated.");
            Require(applied.StatusChanges.Count > 0, "RuntimePreviewResult.statusChanges key must be populated.");
            Require(applied.Performance != null, "RuntimePreviewResult.performance key must be populated.");
            Require(applied.Errors != null, "RuntimePreviewResult.errors key must be present.");

            RuntimePreviewResult snapshot = await client.GetSnapshotAsync(new GetSnapshotParams { TargetId = "TestTarget" });
            Require(snapshot.BuffSnapshots.Count > 0, "snapshot should reuse buff snapshots structure.");

            GetLogsResult logs = await client.GetLogsAsync(new GetLogsParams { AfterSeq = 0, Max = 10 });
            Require(logs != null, "getLogs result should be non-null.");

            ResetResult reset = await client.ResetAsync(new ResetParams());
            Require(reset != null, "reset result should be non-null.");
            await client.DisposeAsync();
        }
        finally
        {
            Environment.SetEnvironmentVariable(PreviewConnectionLocator.EnvOverride, null);
            try { Directory.Delete(dir, true); } catch { }
        }
    }

    private static async Task PreviewClient_DeserializesResultStatusFields()
    {
        string dir = CreateTempPreviewDir();
        using var server = new MockPreviewServer(dir);
        try
        {
            var client = new WebSocketPreviewClient(new Uri(server.Descriptor.Endpoint), server.Descriptor.Token);
            await client.HandshakeAsync("MxAuthoringTest", "0.3.0");
            RuntimePreviewResult applied = await client.ApplyBuffAsync(new ApplyBuffParams { BuffId = "100001", WaitTicks = 0 });
            Require(applied.PreviewMode == "dummy", "previewMode should deserialize from preview result.");
            Require(applied.ConfigMetadata.SourceId == "mock.preview", "configMetadata.sourceId should deserialize from preview result.");
            Require(applied.ConfigMetadata.ChangedConfigIds.Count > 0, "configMetadata.changedConfigIds should deserialize from preview result.");
            await client.DisposeAsync();
        }
        finally
        {
            Environment.SetEnvironmentVariable(PreviewConnectionLocator.EnvOverride, null);
            try { Directory.Delete(dir, true); } catch { }
        }
    }

    private static void EditorServer_PreviewUnavailableDoesNotThrow500()
    {
        string dir = CreateTempPreviewDir();
        try
        {
            object status = MxFramework.Authoring.Cli.EditorServer.ReadPreviewStatus(MxFramework.Authoring.Cli.Program.CreateJsonOptions());
            JsonElement statusJson = ToJsonElement(status);
            Require(statusJson.GetProperty("connected").GetBoolean() == false, "Preview status should report disconnected.");
            Require(statusJson.GetProperty("status").GetString() == "unavailable", "Missing preview server should report unavailable.");

            object run = MxFramework.Authoring.Cli.EditorServer.RunPreview(FindRepoRoot(), "Tools/MxFramework.Authoring/samples/buff-preview", "100001", "TestCaster", "TestTarget", 1, 0, MxFramework.Authoring.Cli.Program.CreateJsonOptions());
            JsonElement runJson = ToJsonElement(run);
            Require(runJson.GetProperty("success").GetBoolean() == false, "Unavailable preview run should not succeed.");
            Require(runJson.GetProperty("status").GetString() == "unavailable", "Unavailable preview run should report unavailable.");
        }
        finally
        {
            Environment.SetEnvironmentVariable(PreviewConnectionLocator.EnvOverride, null);
            try { Directory.Delete(dir, true); } catch { }
        }
    }

    private static async Task EditorServer_RunPreviewReturnsStructuredApplyFailure()
    {
        string dir = CreateTempPreviewDir();
        using var server = new MockPreviewServer(dir) { FailApplyBuff = true };
        try
        {
            object run = MxFramework.Authoring.Cli.EditorServer.RunPreview(FindRepoRoot(), "Tools/MxFramework.Authoring/samples/buff-preview", "100001", "TestCaster", "MissingTarget", 1, 0, MxFramework.Authoring.Cli.Program.CreateJsonOptions());
            JsonElement runJson = ToJsonElement(run);
            Require(runJson.GetProperty("success").GetBoolean() == false, "Failed apply should not succeed.");
            Require(runJson.GetProperty("status").GetString() == "failed", "Failed apply should report failed.");
            Require(runJson.GetProperty("code").GetInt32() == PreviewError.ApplyBuffFailed, "Failed apply should expose 2003.");
            Require(runJson.GetProperty("previewMode").GetString() == "scene", "Failed apply should expose previewMode.");
            Require(runJson.GetProperty("reason").GetString() == "missing_target", "Failed apply should expose reason.");
            JsonElement errors = runJson.GetProperty("result").GetProperty("errors");
            Require(errors.GetArrayLength() == 1, "Failed apply should expose nested result errors.");
            Require(errors[0].GetProperty("reason").GetString() == "missing_target", "Nested error reason should survive API mapping.");
            await Task.CompletedTask;
        }
        finally
        {
            Environment.SetEnvironmentVariable(PreviewConnectionLocator.EnvOverride, null);
            try { Directory.Delete(dir, true); } catch { }
        }
    }

    private static JsonElement ToJsonElement(object value)
    {
        string text = JsonSerializer.Serialize(value, MxFramework.Authoring.Cli.Program.CreateJsonOptions());
        return JsonDocument.Parse(text).RootElement.Clone();
    }

    private static string FindRepoRoot()
    {
        string dir = Directory.GetCurrentDirectory();
        while (!string.IsNullOrEmpty(dir))
        {
            if (File.Exists(Path.Combine(dir, "Tools", "MxFramework.Authoring", "MxFramework.Authoring.sln")))
                return dir;
            dir = Directory.GetParent(dir)?.FullName;
        }

        return Directory.GetCurrentDirectory();
    }
}
