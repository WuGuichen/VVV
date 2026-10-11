using System;
using System.IO;
using System.Linq;
using MxFramework.Authoring;

namespace MxFramework.Authoring.Tests;

internal static class CharacterActionAuthoringResourceProviderTests
{
    public static void RunAll()
    {
        Provider_ProjectsGeneratedCharacterActionConfigItems();
        ReferenceGraph_LinksReactionActionToAnimationResource();
    }

    private static void Provider_ProjectsGeneratedCharacterActionConfigItems()
    {
        string root = CreateTempRoot();
        try
        {
            WriteCharacterActionFixtures(root);

            AuthoringResourceCollection collection = BuildCollection(root);

            Require(collection.Providers.Exists(provider => provider.ProviderId == AuthoringResourceProviderIds.CharacterAction), "CharacterAction provider descriptor should be present.");
            Require(collection.Items.Exists(item => item.Usage == CharacterActionAuthoringResourceProvider.UsageProfile && item.StableId == "test_package.default"), "CharacterAction profile item should be projected.");
            Require(collection.Items.Exists(item => item.Usage == CharacterActionAuthoringResourceProvider.UsageActionSet && item.Metadata["reactionProfileId"] == "reaction.test_package.default"), "ActionSet item should expose reactionProfileId metadata.");
            Require(collection.Items.Exists(item => item.Usage == CharacterActionAuthoringResourceProvider.UsageReactionProfile && item.StableId == "reaction.test_package.default" && item.Metadata["ruleActionIds"] == "test_package_cohesion_break_react"), "ReactionProfile item should expose rule target metadata.");

            AuthoringResourceItem action = collection.Items.FirstOrDefault(item => item.Usage == CharacterActionAuthoringResourceProvider.UsageAction && item.StableId == "test_package_cohesion_break_react");
            Require(action != null, "Reaction action item should be projected.");
            Require(action.Metadata["sourcePath"].Replace('\\', '/').EndsWith("Assets/MxFrameworkGenerated/CharacterActions/Libraries/Common/character_actions.json", StringComparison.Ordinal), "Action item should expose source path metadata.");
            Require(action.Metadata["animationClipResourceKey"] == "art.test.reaction.cohesion_break", "Action item should expose animation clip key metadata.");
            Require(action.Metadata["libraryId"] == "Common", "Action item should expose library id metadata.");
            Require(action.Kind == CharacterPackageResourceTypeIds.Config, "CharacterAction resources should be Config items.");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static void ReferenceGraph_LinksReactionActionToAnimationResource()
    {
        string root = CreateTempRoot();
        try
        {
            WriteCharacterActionFixtures(root);

            AuthoringResourceCollection actions = BuildCollection(root);
            actions.Items.Add(CreateAnimationResource("art.test.reaction.cohesion_break"));

            AuthoringResourceReferenceGraph graph = AuthoringResourceReferenceGraphBuilder.FromCharacterPackage(
                new CharacterResourcePackage(),
                actions);

            AuthoringResourceReferenceEdge edge = graph.Edges.FirstOrDefault(item =>
                item.SourceConsumerKind == "characterAction" &&
                item.SourceStableId == "test_package_cohesion_break_react" &&
                item.TargetRuntimeResourceKey == "art.test.reaction.cohesion_break");
            Require(edge != null, "ReferenceGraph should link reaction action animation track to animation resource.");
            Require(edge.PreloadPolicy == AuthoringResourcePreloadPolicies.AnimationWarmup, "Reaction action animation edge should use AnimationWarmup preload policy.");
            Require(edge.BindingKind == AuthoringResourceBindingKind.ResourceManagerAsset, "Reaction action edge should resolve to runtime animation binding kind.");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static AuthoringResourceCollection BuildCollection(string root)
    {
        return new CharacterActionAuthoringResourceProvider().BuildResourceCollection(new AuthoringResourceProviderContext
        {
            ProjectRootPath = root,
            PackageId = "test_package",
            ScopeId = "test"
        });
    }

    private static AuthoringResourceItem CreateAnimationResource(string runtimeKey)
    {
        return new AuthoringResourceItem
        {
            ResourceId = "runtimeCatalog:" + runtimeKey,
            StableId = runtimeKey,
            DisplayName = runtimeKey,
            Kind = CharacterPackageResourceTypeIds.Animation,
            Usage = AnimationAuthoringResourceUsages.AnimationClip,
            SourceProviderId = AuthoringResourceProviderIds.RuntimeCatalog,
            SourceKind = AuthoringResourceSourceKind.GeneratedAsset,
            BindingKind = AuthoringResourceBindingKind.ResourceManagerAsset,
            ImportStatus = AuthoringResourceImportStatus.Clean,
            RuntimeAvailability = AuthoringResourceRuntimeAvailability.RuntimeReady,
            ProviderBindings =
            {
                new AuthoringResourceProviderBinding
                {
                    ProviderId = AuthoringResourceProviderIds.RuntimeCatalog,
                    BindingKind = AuthoringResourceBindingKind.ResourceManagerAsset,
                    BindingKeyKind = AuthoringResourceBindingKeyKinds.RuntimeResourceKey,
                    RuntimeResourceKey = runtimeKey,
                    ProviderResourceKey = runtimeKey,
                    IsPrimary = true
                }
            }
        };
    }

    private static void WriteCharacterActionFixtures(string root)
    {
        string actionSetRoot = Path.Combine(root, "Assets", "MxFrameworkGenerated", "CharacterActions", "ActionSets", "test_package");
        string profileRoot = Path.Combine(root, "Assets", "MxFrameworkGenerated", "CharacterActions", "Profiles", "test_package");
        string libraryRoot = Path.Combine(root, "Assets", "MxFrameworkGenerated", "CharacterActions", "Libraries", "Common");
        Directory.CreateDirectory(actionSetRoot);
        Directory.CreateDirectory(profileRoot);
        Directory.CreateDirectory(libraryRoot);

        File.WriteAllText(Path.Combine(profileRoot, "character_action_profile.json"),
            "{\n"
            + "  \"schemaVersion\": \"0.1.0\",\n"
            + "  \"profiles\": [\n"
            + "    { \"id\": 1, \"stableId\": \"test_package.default\", \"displayName\": \"Test Default\", \"actionSetIds\": [\"test_package.default\"], \"actionLibraryIds\": [\"Common\"] }\n"
            + "  ]\n"
            + "}\n");
        File.WriteAllText(Path.Combine(actionSetRoot, "character_action_sets.json"),
            "{\n"
            + "  \"schemaVersion\": \"0.1.0\",\n"
            + "  \"actionSets\": [\n"
            + "    { \"id\": 1, \"stableId\": \"test_package.default\", \"displayName\": \"Test Default\", \"characterStableId\": \"char.test\", \"movementProfileId\": \"movement.default\", \"reactionProfileId\": \"reaction.test_package.default\" }\n"
            + "  ]\n"
            + "}\n");
        File.WriteAllText(Path.Combine(actionSetRoot, "character_reaction_profiles.json"),
            "{\n"
            + "  \"schemaVersion\": \"0.1.0\",\n"
            + "  \"reactionProfiles\": [\n"
            + "    { \"stableId\": \"reaction.test_package.default\", \"displayName\": \"Test Reactions\", \"defaultActionId\": \"test_package_cohesion_break_react\", \"rules\": [ { \"actionId\": \"test_package_cohesion_break_react\", \"trigger\": \"CohesionBreak\", \"currentScatterPressureBand\": \"Broken\", \"priority\": 100 } ] }\n"
            + "  ]\n"
            + "}\n");
        File.WriteAllText(Path.Combine(libraryRoot, "character_actions.json"),
            "{\n"
            + "  \"schemaVersion\": \"0.1.0\",\n"
            + "  \"actions\": [\n"
            + "    { \"id\": 1, \"stableId\": \"test_package_cohesion_break_react\", \"displayName\": \"Break React\", \"category\": \"Reaction\", \"durationFrames\": 18, \"animationTrack\": { \"events\": [ { \"frame\": 0, \"endFrame\": 17, \"kind\": \"CrossFadeAnimation\", \"animationActionKey\": \"anim.reaction.cohesion_break\", \"animationClipResourceKey\": \"art.test.reaction.cohesion_break\" } ] } }\n"
            + "  ]\n"
            + "}\n");
    }

    private static string CreateTempRoot()
    {
        string path = Path.Combine(Path.GetTempPath(), "mx-character-action-provider-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new Exception(message);
    }
}
