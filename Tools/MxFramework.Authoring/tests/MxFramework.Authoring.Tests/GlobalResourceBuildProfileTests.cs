using System;
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
        EmptyPreloadGroup_IsError();
        RequiredDomainPlanMissing_IsError();
        AuthoringResourceProvider_ProjectsProfileEntriesAndDiagnostics();
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
