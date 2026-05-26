using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using MxFramework.Authoring;

namespace MxFramework.Authoring.Tests;

internal static class GlobalResourceBuildProfileTests
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    public static void RunAll()
    {
        ValidProfile_PassesAndRoundTripsCamelCaseJson();
        DuplicateKey_IsError();
        InvalidKey_IsError();
        MissingUnityGuid_IsError();
        UnknownCompression_IsError();
        UnknownPlanningIntent_IsError();
        ForceBundleWithoutValue_IsError();
        EmptyPreloadGroup_IsError();
        RequiredDomainPlanMissing_IsError();
        AuthoringResourceProvider_ProjectsProfileEntriesAndDiagnostics();
        BundlePlanner_UsesBundleRulesAndStableDependencyOutput();
        BundlePlanner_OverridePrecedenceSeparatesOutputs();
        BundlePlanner_ExternalProviderInternalSelectionIsDiagnostic();
        BundlePlanner_LegacyEditorOnlyAndRuntimeFlagsDoNotEnterInternalBundles();
        BundlePlanner_ForceBundleOverridesDeliveryModeExternal();
        BundlePlanner_IncludeDependenciesFalseSuppressesDependencyBundles();
        EditorServer_GlobalBuildProfileApi_ReadsPlanAndValidatesSaveGate();
    }

    private static void ValidProfile_PassesAndRoundTripsCamelCaseJson()
    {
        GlobalResourceBuildProfile profile = CreateValidProfile();

        GlobalResourceBuildProfileValidationReport report = GlobalResourceBuildProfileValidator.Validate(profile);
        Require(!report.HasErrors, "Valid global resource build profile should pass.");

        string json = JsonSerializer.Serialize(profile, JsonOptions);
        Require(json.Contains("\"profileId\""), "GlobalResourceBuildProfile JSON should use camelCase profileId.");
        Require(json.Contains("\"resourceKey\""), "GlobalResourceBuildProfile JSON should use camelCase resourceKey.");

        GlobalResourceBuildProfile roundTrip = JsonSerializer.Deserialize<GlobalResourceBuildProfile>(json, JsonOptions);
        Require(roundTrip != null, "GlobalResourceBuildProfile should deserialize.");
        Require(roundTrip.ProfileId == profile.ProfileId, "profileId should roundtrip.");
        Require(roundTrip.Entries.Count == 1, "entries should roundtrip.");
        Require(roundTrip.Entries[0].ResourceKey.Type == "Texture2D", "resourceKey.type should roundtrip.");
        Require(roundTrip.Entries[0].DeliveryMode == GlobalResourceBuildProfileDeliveryModes.Internal, "deliveryMode should default and roundtrip.");
        Require(roundTrip.Entries[0].BundleOverrideMode == GlobalResourceBuildProfileBundleOverrideModes.None, "bundleOverrideMode should default and roundtrip.");
    }

    private static void DuplicateKey_IsError()
    {
        GlobalResourceBuildProfile profile = CreateValidProfile();
        profile.Entries.Add(new GlobalResourceBuildProfileEntry
        {
            ResourceKey = new GlobalResourceBuildProfileResourceKey { Id = "ui.startup.button.normal", Type = "Texture2D" },
            Source = new GlobalResourceBuildProfileEntrySource
            {
                ProviderId = AuthoringResourceProviderIds.UnityAssetDatabase,
                UnityAssetPath = "Assets/UI/Game/Startup/button_normal_duplicate.png",
                UnityGuid = "duplicatedguid"
            },
            Labels = { "domain.ui", "bundle.ui.startup" },
            BundleRule = "ui.startup",
            PreloadGroups = { "boot.base" }
        });

        GlobalResourceBuildProfileValidationReport report = GlobalResourceBuildProfileValidator.Validate(profile);
        Require(HasCode(report, GlobalResourceBuildProfileValidationCodes.DuplicateResourceKey), "Duplicate ResourceKey should be reported.");
    }

    private static void InvalidKey_IsError()
    {
        GlobalResourceBuildProfile profile = CreateValidProfile();
        profile.Entries[0].ResourceKey.Id = "ui startup button";

        GlobalResourceBuildProfileValidationReport report = GlobalResourceBuildProfileValidator.Validate(profile);
        Require(HasCode(report, GlobalResourceBuildProfileValidationCodes.ResourceKeyInvalidCharacters), "Invalid ResourceKey id should be reported.");
    }

    private static void MissingUnityGuid_IsError()
    {
        GlobalResourceBuildProfile profile = CreateValidProfile();
        profile.Entries[0].Source.UnityGuid = string.Empty;

        GlobalResourceBuildProfileValidationReport report = GlobalResourceBuildProfileValidator.Validate(profile);
        Require(HasCode(report, GlobalResourceBuildProfileValidationCodes.RuntimeUnityGuidRequired), "Runtime-loadable Unity AssetDatabase entry without GUID should be reported.");
    }

    private static void UnknownCompression_IsError()
    {
        GlobalResourceBuildProfile profile = CreateValidProfile();
        profile.BundleRules[0].Compression = "brotli";

        GlobalResourceBuildProfileValidationReport report = GlobalResourceBuildProfileValidator.Validate(profile);
        Require(HasCode(report, GlobalResourceBuildProfileValidationCodes.UnknownCompression), "Unknown bundle compression should be reported.");
    }

    private static void UnknownPlanningIntent_IsError()
    {
        GlobalResourceBuildProfile profile = CreateValidProfile();
        profile.Entries[0].DeliveryMode = "downloadMaybe";
        profile.Entries[0].BundleOverrideMode = "mergeSomehow";

        GlobalResourceBuildProfileValidationReport report = GlobalResourceBuildProfileValidator.Validate(profile);

        Require(HasCode(report, GlobalResourceBuildProfileValidationCodes.UnknownDeliveryMode), "Unknown deliveryMode should be reported.");
        Require(HasCode(report, GlobalResourceBuildProfileValidationCodes.UnknownBundleOverrideMode), "Unknown bundleOverrideMode should be reported.");
    }

    private static void ForceBundleWithoutValue_IsError()
    {
        GlobalResourceBuildProfile profile = CreateValidProfile();
        profile.Entries[0].BundleOverrideMode = GlobalResourceBuildProfileBundleOverrideModes.ForceBundle;
        profile.Entries[0].BundleOverrideValue = string.Empty;

        GlobalResourceBuildProfileValidationReport report = GlobalResourceBuildProfileValidator.Validate(profile);

        Require(HasCode(report, GlobalResourceBuildProfileValidationCodes.BundleOverrideValueRequired), "forceBundle without bundleOverrideValue should be reported.");
    }

    private static void EmptyPreloadGroup_IsError()
    {
        GlobalResourceBuildProfile profile = CreateValidProfile();
        profile.PreloadGroups.Add(new GlobalResourceBuildProfilePreloadGroup
        {
            Id = "empty.group"
        });

        GlobalResourceBuildProfileValidationReport report = GlobalResourceBuildProfileValidator.Validate(profile);
        Require(HasCode(report, GlobalResourceBuildProfileValidationCodes.PreloadGroupEmpty), "Preload group with no labels or keys should be reported.");
    }

    private static void RequiredDomainPlanMissing_IsError()
    {
        GlobalResourceBuildProfile profile = CreateValidProfile();
        profile.RequiredDomainPlanKeys.Add(new GlobalResourceBuildProfileResourceKey
        {
            Id = "ui.missing.icon",
            TypeId = "Texture2D"
        });

        GlobalResourceBuildProfileValidationReport report = GlobalResourceBuildProfileValidator.Validate(profile);
        Require(HasCode(report, GlobalResourceBuildProfileValidationCodes.RequiredDomainPlanKeyMissing), "Missing required domain plan key should be reported.");
    }

    private static void AuthoringResourceProvider_ProjectsProfileEntriesAndDiagnostics()
    {
        GlobalResourceBuildProfile profile = CreateValidProfile();
        var context = new AuthoringResourceProviderContext
        {
            ScopeId = "global",
            ProjectRootPath = FindRepoRoot(),
            GlobalResourceBuildProfile = profile,
            GlobalResourceBuildProfilePath = "Assets/Config/MxFramework/ResourceProfiles/global_resource_build_profile.json",
            GlobalRuntimeCatalogPath = "Assets/StreamingAssets/MxFramework/Resources/global_runtime_catalog.json",
            GlobalPreloadGroupsPath = "Assets/StreamingAssets/MxFramework/Resources/global_preload_groups.json",
            GlobalBundleDependenciesPath = "Assets/StreamingAssets/MxFramework/Resources/global_bundle_dependencies.json",
            GlobalResourceBuildReportPath = "Assets/Config/MxFramework/ResourceBuildReports/global_resource_build_report.json"
        };

        AuthoringResourceCollection collection = new GlobalResourceBuildProfileAuthoringResourceProvider().BuildResourceCollection(context);

        Require(collection.Providers.Count == 1, "Global build profile provider descriptor should be present.");
        Require(collection.Items.Count == 1, "Global build profile provider should project profile entries.");
        AuthoringResourceItem item = collection.Items[0];
        Require(item.SourceProviderId == AuthoringResourceProviderIds.GlobalResourceBuildProfile, "profile item provider id should be globalResourceBuildProfile.");
        Require(item.Metadata["bundleRule"] == "ui.startup", "profile item should expose bundle rule metadata.");
        Require(item.Metadata["preloadGroups"].Contains("boot.base"), "profile item should expose preload group metadata.");
        Require(item.ProviderBindings.Exists(binding => binding.ProviderId == AuthoringResourceProviderIds.UnityAssetDatabase), "profile item should expose Unity source binding.");
        Require(collection.Diagnostics.Exists(diagnostic => diagnostic.Code == "GLOBAL_RESOURCE_RUNTIME_CATALOG_MISSING"), "missing generated catalog should surface as provider diagnostic.");
    }

    private static void BundlePlanner_UsesBundleRulesAndStableDependencyOutput()
    {
        GlobalResourceBuildProfile profile = CreateValidProfile();
        profile.Entries.Add(new GlobalResourceBuildProfileEntry
        {
            ResourceKey = new GlobalResourceBuildProfileResourceKey { Id = "ui.startup.panel", Type = "Prefab" },
            Source = new GlobalResourceBuildProfileEntrySource
            {
                ProviderId = AuthoringResourceProviderIds.UnityAssetDatabase,
                UnityAssetPath = "Assets/UI/Game/Startup/panel.prefab",
                UnityGuid = "11111111111111111111111111111111"
            },
            Labels = { "domain.ui", "bundle.ui.startup" },
            BundleRule = "ui.startup",
            Dependencies =
            {
                new GlobalResourceBuildProfileResourceKey { Id = "shared.atlas", Type = "Texture2D" }
            },
            ProviderData = { ["sizeBytes"] = "25" }
        });
        profile.Entries.Add(new GlobalResourceBuildProfileEntry
        {
            ResourceKey = new GlobalResourceBuildProfileResourceKey { Id = "shared.atlas", Type = "Texture2D" },
            Source = new GlobalResourceBuildProfileEntrySource
            {
                ProviderId = AuthoringResourceProviderIds.UnityAssetDatabase,
                UnityAssetPath = "Assets/UI/Game/Shared/atlas.png",
                UnityGuid = "22222222222222222222222222222222"
            },
            Labels = { "domain.shared" },
            BundleGroupHint = "shared.core",
            ProviderData = { ["sizeBytes"] = "50" }
        });

        GlobalResourceBundlePlan plan = new GlobalResourceBundlePlanner().Plan(profile);

        Require(plan.Bundles.Count == 2, "Planner should produce two deterministic bundles.");
        Require(plan.Bundles[0].BundleName == "global.shared.core", "Bundles should be sorted by bundle name.");
        Require(plan.Bundles[1].BundleName == "global.ui.startup", "Bundle rule name should be used.");
        Require(plan.Bundles[1].IncludedResourceKeys[0] == ":Prefab:ui.startup.panel:", "Bundle entries should be sorted by resource key.");
        Require(plan.Bundles[1].DependencyBundleNames.Count == 1 && plan.Bundles[1].DependencyBundleNames[0] == "global.shared.core", "Cross-bundle dependency should be emitted.");
        Require(plan.Bundles[0].EstimatedSizeBytes == 50, "Estimated size should be collected from metadata.");
    }

    private static void BundlePlanner_OverridePrecedenceSeparatesOutputs()
    {
        GlobalResourceBuildProfile profile = CreateValidProfile();
        profile.RequiredDomainPlanKeys.Clear();
        profile.Entries.Clear();
        profile.BundleRules.Clear();
        profile.BundleRules.Add(new GlobalResourceBuildProfileBundleRule
        {
            Id = "ui.default",
            BundleName = "global.ui.default",
            MatchLabels = { "bundle.ui.default" }
        });
        profile.Entries.Add(CreatePlanningEntry("ui.force.bundle", "Texture2D", GlobalResourceBuildProfileBundleOverrideModes.ForceBundle, "global.manual.ui", "ui.default"));
        profile.Entries.Add(CreatePlanningEntry("ui.force.standalone", "Texture2D", GlobalResourceBuildProfileBundleOverrideModes.ForceStandalone, string.Empty, "ui.default"));
        profile.Entries.Add(CreatePlanningEntry("ui.force.external", "Texture2D", GlobalResourceBuildProfileBundleOverrideModes.ForceExternal, string.Empty, "ui.default"));
        profile.Entries.Add(CreatePlanningEntry("ui.force.exclude", "Texture2D", GlobalResourceBuildProfileBundleOverrideModes.Exclude, string.Empty, "ui.default"));

        GlobalResourceBundlePlan plan = new GlobalResourceBundlePlanner().Plan(profile);

        Require(plan.Bundles.Exists(bundle => bundle.BundleName == "global.manual.ui"), "forceBundle should use bundleOverrideValue.");
        Require(plan.Bundles.Exists(bundle => bundle.BundleName == "global.standalone.ui.force.standalone"), "forceStandalone should derive a resource-specific bundle.");
        Require(plan.ExternalEntries.Count == 1 && plan.ExternalEntries[0].ResourceId == "ui.force.external", "forceExternal should not enter internal bundle output.");
        Require(plan.ExcludedEntries.Count == 1 && plan.ExcludedEntries[0].ResourceId == "ui.force.exclude", "exclude should not enter internal or external output.");
    }

    private static void BundlePlanner_ExternalProviderInternalSelectionIsDiagnostic()
    {
        GlobalResourceBuildProfile profile = CreateValidProfile();
        profile.RequiredDomainPlanKeys.Clear();
        profile.Entries[0].Source.ProviderId = AuthoringResourceProviderIds.Fmod;

        GlobalResourceBundlePlan plan = new GlobalResourceBundlePlanner().Plan(profile);

        Require(plan.Bundles.Count == 0, "External provider should not produce an internal bundle by default.");
        Require(plan.ExternalEntries.Count == 1, "External provider selected for internal output should be routed to external entries.");
        Require(plan.Diagnostics.Exists(diagnostic => diagnostic.Code == GlobalResourceBundlePlannerDiagnosticCodes.ExternalProviderSelectedForInternalBundle), "External provider internal selection should be diagnostic.");
    }

    private static void BundlePlanner_LegacyEditorOnlyAndRuntimeFlagsDoNotEnterInternalBundles()
    {
        GlobalResourceBuildProfile profile = CreateValidProfile();
        profile.RequiredDomainPlanKeys.Clear();
        profile.Entries.Clear();
        profile.Entries.Add(CreatePlanningEntry("ui.editor.only", "Texture2D", GlobalResourceBuildProfileBundleOverrideModes.None, string.Empty, "ui.startup"));
        profile.Entries[0].EditorOnly = true;
        profile.Entries.Add(CreatePlanningEntry("ui.not.runtime", "Texture2D", GlobalResourceBuildProfileBundleOverrideModes.None, string.Empty, "ui.startup"));
        profile.Entries[1].RuntimeLoadable = false;

        GlobalResourceBundlePlan plan = new GlobalResourceBundlePlanner().Plan(profile);

        Require(plan.Bundles.Count == 0, "Legacy editor-only and non-runtime flags should not enter internal bundle output.");
        Require(plan.ExcludedEntries.Count == 2, "Legacy editor-only and non-runtime flags should become excluded entries.");
    }

    private static void BundlePlanner_ForceBundleOverridesDeliveryModeExternal()
    {
        GlobalResourceBuildProfile profile = CreateValidProfile();
        profile.RequiredDomainPlanKeys.Clear();
        profile.Entries[0].DeliveryMode = GlobalResourceBuildProfileDeliveryModes.External;
        profile.Entries[0].BundleOverrideMode = GlobalResourceBuildProfileBundleOverrideModes.ForceBundle;
        profile.Entries[0].BundleOverrideValue = "global.manual.internal";

        GlobalResourceBundlePlan plan = new GlobalResourceBundlePlanner().Plan(profile);

        Require(plan.Bundles.Count == 1 && plan.Bundles[0].BundleName == "global.manual.internal", "forceBundle should override deliveryMode external.");
        Require(plan.ExternalEntries.Count == 0, "forceBundle-overridden external delivery should not remain external.");
    }

    private static void BundlePlanner_IncludeDependenciesFalseSuppressesDependencyBundles()
    {
        GlobalResourceBuildProfile profile = CreateValidProfile();
        profile.BundleRules[0].IncludeDependencies = false;
        profile.Entries.Add(new GlobalResourceBuildProfileEntry
        {
            ResourceKey = new GlobalResourceBuildProfileResourceKey { Id = "shared.atlas", Type = "Texture2D" },
            Source = new GlobalResourceBuildProfileEntrySource
            {
                ProviderId = AuthoringResourceProviderIds.UnityAssetDatabase,
                UnityAssetPath = "Assets/UI/Game/Shared/atlas.png",
                UnityGuid = "22222222222222222222222222222222"
            },
            Labels = { "domain.shared" },
            BundleGroupHint = "shared.core"
        });
        profile.Entries[0].Dependencies.Add(new GlobalResourceBuildProfileResourceKey { Id = "shared.atlas", Type = "Texture2D" });

        GlobalResourceBundlePlan plan = new GlobalResourceBundlePlanner().Plan(profile);
        GlobalResourceBundlePlanBundle startup = plan.Bundles.Single(bundle => bundle.BundleName == "global.ui.startup");

        Require(startup.DependencyBundleNames.Count == 0, "Bundle rule IncludeDependencies=false should suppress dependency bundle output.");
    }

    private static void EditorServer_GlobalBuildProfileApi_ReadsPlanAndValidatesSaveGate()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), "mx-global-profile-api-" + Guid.NewGuid().ToString("N"));
        string packageRelative = "Tools/MxFramework.Authoring/samples/resource-api-test";
        string packagePath = Path.Combine(tempRoot, packageRelative);
        string profilePath = Path.Combine(tempRoot, "Assets", "Config", "MxFramework", "ResourceProfiles", "global_resource_build_profile.json");
        Directory.CreateDirectory(packagePath);
        Directory.CreateDirectory(Path.GetDirectoryName(profilePath)!);
        File.WriteAllText(Path.Combine(packagePath, "manifest.json"), JsonSerializer.Serialize(new CharacterPackageManifest
        {
            PackageId = "resource_api_test",
            StableId = "char.resource_api_test",
            DisplayName = "Resource API Test"
        }, JsonOptions));
        File.WriteAllText(Path.Combine(packagePath, "resource_catalog.json"), JsonSerializer.Serialize(new CharacterPackageResourceCatalog(), JsonOptions));
        File.WriteAllText(profilePath, JsonSerializer.Serialize(CreateValidProfile(), JsonOptions));

        try
        {
            object planResult = MxFramework.Authoring.Cli.EditorServer.ReadGlobalResourceBundlePlan(tempRoot, packageRelative, JsonOptions, "StandaloneOSX");
            JsonElement planJson = ToJsonElement(planResult);
            Require(planJson.GetProperty("profileId").GetString() == "global.default", "bundle plan endpoint should expose profile id.");
            Require(planJson.GetProperty("plan").GetProperty("bundles").GetArrayLength() == 1, "bundle plan endpoint should expose planner bundles.");

            string beforeInvalid = File.ReadAllText(profilePath);
            GlobalResourceBuildProfile invalid = CreateValidProfile();
            invalid.Entries[0].ResourceKey.Id = "bad id";
            object invalidSave = MxFramework.Authoring.Cli.EditorServer.SaveGlobalResourceBuildProfile(tempRoot, invalid, JsonOptions);
            JsonElement invalidJson = ToJsonElement(invalidSave);
            Require(invalidJson.GetProperty("success").GetBoolean() == false, "invalid profile save should fail.");
            Require(File.ReadAllText(profilePath) == beforeInvalid, "invalid profile save should not write to disk.");

            GlobalResourceBuildProfile valid = CreateValidProfile();
            valid.ProfileId = "global.saved";
            object validSave = MxFramework.Authoring.Cli.EditorServer.SaveGlobalResourceBuildProfile(tempRoot, valid, JsonOptions);
            JsonElement validJson = ToJsonElement(validSave);
            Require(validJson.GetProperty("success").GetBoolean(), "valid profile save should succeed.");
            GlobalResourceBuildProfile saved = JsonSerializer.Deserialize<GlobalResourceBuildProfile>(File.ReadAllText(profilePath), JsonOptions);
            Require(saved != null && saved.ProfileId == "global.saved", "valid profile save should write the profile atomically.");
        }
        finally
        {
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, recursive: true);
        }
    }

    private static GlobalResourceBuildProfile CreateValidProfile()
    {
        return new GlobalResourceBuildProfile
        {
            ProfileId = "global.default",
            CatalogId = "global.runtime",
            Entries =
            {
                new GlobalResourceBuildProfileEntry
                {
                    ResourceKey = new GlobalResourceBuildProfileResourceKey
                    {
                        Id = "ui.startup.button.normal",
                        Type = "Texture2D"
                    },
                    Source = new GlobalResourceBuildProfileEntrySource
                    {
                        ProviderId = AuthoringResourceProviderIds.UnityAssetDatabase,
                        UnityAssetPath = "Assets/UI/Game/Startup/button_normal.png",
                        UnityGuid = "abcdef0123456789abcdef0123456789"
                    },
                    Labels = { "domain.ui", "preload.boot.base", "bundle.ui.startup" },
                    BundleRule = "ui.startup",
                    PreloadGroups = { "boot.base" }
                }
            },
            BundleRules =
            {
                new GlobalResourceBuildProfileBundleRule
                {
                    Id = "ui.startup",
                    BundleName = "global.ui.startup",
                    MatchLabels = { "bundle.ui.startup" },
                    Compression = "lz4",
                    BuildTarget = "ActiveBuildTarget",
                    IncludeDependencies = true
                }
            },
            PreloadGroups =
            {
                new GlobalResourceBuildProfilePreloadGroup
                {
                    Id = "boot.base",
                    Labels = { "preload.boot.base" },
                    FailFast = true,
                    MaxConcurrentLoads = 4
                }
            },
            RequiredDomainPlanKeys =
            {
                new GlobalResourceBuildProfileResourceKey
                {
                    Id = "ui.startup.button.normal",
                    TypeId = "Texture2D"
                }
            }
        };
    }

    private static bool HasCode(GlobalResourceBuildProfileValidationReport report, string code)
    {
        return report.Issues.Any(issue => issue.Severity == IssueSeverity.Error && issue.Code == code);
    }

    private static GlobalResourceBuildProfileEntry CreatePlanningEntry(string id, string type, string overrideMode, string overrideValue, string bundleRule)
    {
        return new GlobalResourceBuildProfileEntry
        {
            ResourceKey = new GlobalResourceBuildProfileResourceKey
            {
                Id = id,
                Type = type
            },
            Source = new GlobalResourceBuildProfileEntrySource
            {
                ProviderId = AuthoringResourceProviderIds.UnityAssetDatabase,
                UnityAssetPath = "Assets/Test/" + id + ".asset",
                UnityGuid = id.Replace(".", string.Empty).PadRight(32, '0').Substring(0, 32)
            },
            Labels = { "domain.ui", "bundle.ui.default" },
            BundleRule = bundleRule,
            BundleOverrideMode = overrideMode,
            BundleOverrideValue = overrideValue
        };
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private static JsonElement ToJsonElement(object value)
    {
        return JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(value, JsonOptions), JsonOptions);
    }

    private static string FindRepoRoot()
    {
        string directory = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(directory))
        {
            if (System.IO.File.Exists(System.IO.Path.Combine(directory, "WGameFramework.sln")) ||
                System.IO.Directory.Exists(System.IO.Path.Combine(directory, ".git")))
                return directory;

            directory = System.IO.Directory.GetParent(directory)?.FullName;
        }

        return AppContext.BaseDirectory;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
