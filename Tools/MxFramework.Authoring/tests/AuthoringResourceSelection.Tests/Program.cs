using System.Text.Json;
using MxFramework.Authoring;

namespace MxFramework.Authoring.ResourceSelection.Tests;

internal static class Program
{
    private static readonly AuthoringResourceSelectionService Service = new();
    private static int cases;
    private static int assertions;

    private static int Main()
    {
        try
        {
            MissingSavedProviderRejectsWithoutMutation();
            ExactProviderWinsRegardlessOfItemOrder();
            ProviderConstrainsLocalKeyLookup();
            ProviderlessSelectionsRetainCompletionAndReasons();
            SourceClipRetainsSiblingBindingCompletion();
            AudioOutputsIgnoreUnrelatedBindings();
            FieldValidationRetainsTypedReasonsAndWarnings();
            Console.WriteLine($"Authoring resource selection: {cases} cases, {assertions} assertions passed.");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    private static void MissingSavedProviderRejectsWithoutMutation()
    {
        foreach (bool runtimeRemains in new[] { false, true })
        {
            AuthoringResourceItem remaining = Animation(runtimeRemains);
            var saved = Saved(remaining.StableId, runtimeRemains
                ? AuthoringResourceProviderIds.UnityAssetDatabase : AuthoringResourceProviderIds.RuntimeCatalog,
                runtimeRemains ? AuthoringResourceBindingKind.UnityAsset : AuthoringResourceBindingKind.ResourceManagerAsset);
            RejectUnchanged(Collection(remaining), AnimationAuthoringResourceFieldSpecs.CreateSourceClip(), saved,
                AuthoringResourceSelectionReasonCodes.ItemMissing);
        }

        foreach (AuthoringResourceSelectionOutputKind output in AudioOutputs())
        {
            AuthoringResourceItem remaining = Audio();
            RejectUnchanged(Collection(remaining), AnimationAuthoringResourceFieldSpecs.CreateEventAudioCue(output),
                Saved(remaining.StableId, AuthoringResourceProviderIds.CharacterPackage, AuthoringResourceBindingKind.AudioCue),
                AuthoringResourceSelectionReasonCodes.ItemMissing);
        }

        // Even matching local keys in the original provider cannot repair a missing saved stable id.
        AuthoringResourceItem replacement = Animation(true);
        RejectUnchanged(Collection(replacement), AnimationAuthoringResourceFieldSpecs.CreateSourceClip(),
            Saved("offline.animation.removed", replacement.SourceProviderId),
            AuthoringResourceSelectionReasonCodes.ItemMissing);
    }

    private static void ExactProviderWinsRegardlessOfItemOrder()
    {
        foreach (bool reverse in new[] { false, true })
        foreach (bool chooseRuntime in new[] { false, true })
        {
            AuthoringResourceItem runtime = Animation(true);
            AuthoringResourceItem unity = Animation(false);
            AuthoringResourceItem chosen = chooseRuntime ? runtime : unity;
            AuthoringResourceCollection collection = reverse ? Collection(unity, runtime) : Collection(runtime, unity);
            var selection = new AuthoringResourceSelectionRef
            {
                ResourceStableId = chosen.StableId,
                SourceProviderId = chosen.SourceProviderId,
                BindingKind = chosen.BindingKind
            };
            AuthoringResourceSelectionResolutionResult result = Resolve(collection,
                AnimationAuthoringResourceFieldSpecs.CreateSourceClip(), selection);
            Check(result.Accepted && result.Reasons.Count == 0, "Exact identity should resolve without reasons.");
            Check(ReferenceEquals(result.Item, chosen), "Exact provider must win over the other same-stable-id item.");
            Check(selection.SourceProviderId == chosen.SourceProviderId && selection.BindingKind == chosen.BindingKind,
                "Exact selection must retain its provider and intended binding.");
        }
    }

    private static void ProviderConstrainsLocalKeyLookup()
    {
        for (int keyKind = 0; keyKind < 3; keyKind++)
        {
            AuthoringResourceItem runtime = Animation(true);
            AuthoringResourceItem unity = Animation(false);
            AuthoringResourceFieldSpec spec = AnimationAuthoringResourceFieldSpecs.CreateSourceClip();
            AuthoringResourceSelectionRef missing = KeySelection(keyKind, runtime.SourceProviderId);
            RejectUnchanged(Collection(unity), spec, missing, AuthoringResourceSelectionReasonCodes.ItemMissing);

            foreach (bool reverse in new[] { false, true })
            {
                AuthoringResourceSelectionRef selection = KeySelection(keyKind, runtime.SourceProviderId);
                AuthoringResourceSelectionResolutionResult result = Resolve(
                    reverse ? Collection(unity, runtime) : Collection(runtime, unity), spec, selection);
                Check(result.Accepted && result.Reasons.Count == 0 && ReferenceEquals(result.Item, runtime),
                    "Provider plus a local key must resolve within that provider without false ambiguity.");
                Check(selection.SourceProviderId == runtime.SourceProviderId && selection.ResourceStableId == runtime.StableId,
                    "A provider-local new reference should receive its stable id.");
            }

            AuthoringResourceSelectionRef providerless = KeySelection(keyKind, string.Empty);
            AuthoringResourceSelectionResolutionResult unique = Resolve(Collection(unity), spec, providerless);
            Check(unique.Accepted && providerless.SourceProviderId == unity.SourceProviderId,
                "Providerless unique local key should still complete.");
            RejectUnchanged(Collection(runtime, unity), spec, KeySelection(keyKind, string.Empty),
                AuthoringResourceSelectionReasonCodes.ItemAmbiguous);
        }
    }

    private static void ProviderlessSelectionsRetainCompletionAndReasons()
    {
        AuthoringResourceItem runtime = Animation(true);
        AuthoringResourceFieldSpec spec = AnimationAuthoringResourceFieldSpecs.CreateSourceClip();
        foreach (string provider in new[] { null, string.Empty, "  " })
        {
            var selection = new AuthoringResourceSelectionRef { ResourceStableId = runtime.StableId, SourceProviderId = provider };
            AuthoringResourceSelectionResolutionResult result = Resolve(Collection(runtime), spec, selection);
            Check(result.Accepted && result.Reasons.Count == 0, "A unique stable id without provider should resolve.");
            Check(selection.SourceProviderId == runtime.SourceProviderId, "Missing provider should be completed.");
        }
        RejectUnchanged(Collection(runtime, Animation(false)), spec,
            new AuthoringResourceSelectionRef { ResourceStableId = runtime.StableId },
            AuthoringResourceSelectionReasonCodes.ItemAmbiguous);
        RejectUnchanged(Collection(runtime), spec,
            new AuthoringResourceSelectionRef { ResourceStableId = "offline.missing" },
            AuthoringResourceSelectionReasonCodes.ItemMissing);
        RejectUnchanged(Collection(), spec, Saved(runtime.StableId, runtime.SourceProviderId),
            AuthoringResourceSelectionReasonCodes.ItemMissing);
        AuthoringResourceSelectionResolutionResult nullResult = Resolve(null, spec, null);
        Check(!nullResult.Accepted && nullResult.Reasons.Single().Code == AuthoringResourceSelectionReasonCodes.ItemMissing,
            "Null collection/selection should retain the missing reason.");
    }

    private static void SourceClipRetainsSiblingBindingCompletion()
    {
        foreach (int[] order in BindingOrders())
        {
            AuthoringResourceItem item = Animation(true);
            var bindings = new[]
            {
                item.ProviderBindings[0],
                new AuthoringResourceProviderBinding
                {
                    ProviderId = item.SourceProviderId,
                    BindingKind = AuthoringResourceBindingKind.UnityAsset,
                    UnityAssetPath = "Assets/Offline/clip.anim"
                },
                new AuthoringResourceProviderBinding { BindingKind = AuthoringResourceBindingKind.GeneratedPreviewOnly }
            };
            item.ProviderBindings = order.Select(index => bindings[index]).ToList();
            var selection = new AuthoringResourceSelectionRef
            {
                ResourceStableId = item.StableId,
                SourceProviderId = item.SourceProviderId,
                BindingKind = AuthoringResourceBindingKind.ResourceManagerAsset
            };
            AuthoringResourceSelectionResolutionResult result = Resolve(Collection(item),
                AnimationAuthoringResourceFieldSpecs.CreateSourceClip(), selection);
            Check(result.Accepted && selection.BindingKind == AuthoringResourceBindingKind.ResourceManagerAsset,
                "SourceClip must keep the requested Runtime binding across binding orders.");
            Check(selection.RuntimeResourceKey == "offline.clip" && selection.UnityAssetPath == "Assets/Offline/clip.anim",
                "SourceClip must complete the Runtime key and allowed sibling Unity path.");
        }
    }

    private static void AudioOutputsIgnoreUnrelatedBindings()
    {
        foreach (int[] order in BindingOrders())
        foreach (AuthoringResourceSelectionOutputKind output in AudioOutputs())
        {
            AuthoringResourceItem item = Audio();
            AuthoringResourceProviderBinding[] bindings = item.ProviderBindings.ToArray();
            item.ProviderBindings = order.Select(index => bindings[index]).ToList();
            AuthoringResourceBindingKind expected = output == AuthoringResourceSelectionOutputKind.AudioEventDefinitionId
                ? AuthoringResourceBindingKind.AudioEventDefinition : AuthoringResourceBindingKind.AudioCue;
            var selection = new AuthoringResourceSelectionRef
            {
                ResourceStableId = item.StableId,
                SourceProviderId = item.SourceProviderId,
                BindingKind = expected
            };
            AuthoringResourceSelectionResolutionResult result = Resolve(Collection(item),
                AnimationAuthoringResourceFieldSpecs.CreateEventAudioCue(output), selection);
            Check(result.Accepted && result.Reasons.Count == 0 && selection.BindingKind == expected,
                "Audio output must choose the matching binding across unrelated-binding insertions.");
            Check(selection.AudioCueId == "offline.cue" && selection.AudioEventDefinitionId == "offline.event",
                "Audio output must retain cue and event identifiers.");
            Check(selection.ProviderResourceKey == (expected == AuthoringResourceBindingKind.AudioCue ? "offline.cue" : "offline.event"),
                "Audio provider key must come from the selected audio binding.");
            Check(string.IsNullOrEmpty(selection.RuntimeResourceKey) && string.IsNullOrEmpty(selection.UnityAssetPath) &&
                string.IsNullOrEmpty(selection.UnityGuid) && string.IsNullOrEmpty(selection.PackageResourceKey) &&
                string.IsNullOrEmpty(selection.ExpectedHash), "Unrelated bindings must not leak into an audio selection.");
        }
    }

    private static void FieldValidationRetainsTypedReasonsAndWarnings()
    {
        (Action<AuthoringResourceFieldSpec> Modify, string Code)[] filters =
        {
            (spec => spec.AcceptedKinds = new() { CharacterPackageResourceTypeIds.Model }, AuthoringResourceSelectionReasonCodes.KindMismatch),
            (spec => spec.AcceptedUsages = new() { "offline.other" }, AuthoringResourceSelectionReasonCodes.UsageMismatch),
            (spec => spec.AcceptedProviderIds = new() { AuthoringResourceProviderIds.Fmod }, AuthoringResourceSelectionReasonCodes.ProviderMismatch),
            (spec => spec.AcceptedBindingKinds = new() { AuthoringResourceBindingKind.AudioCue }, AuthoringResourceSelectionReasonCodes.BindingMismatch),
            (spec => spec.RequireRuntimeLoadable = true, AuthoringResourceSelectionReasonCodes.EditorOnlySelectedForRuntime)
        };
        foreach (var filter in filters)
        {
            AuthoringResourceItem item = Animation(false);
            AuthoringResourceFieldSpec spec = AnimationAuthoringResourceFieldSpecs.CreateSourceClip();
            filter.Modify(spec);
            RejectUnchanged(Collection(item), spec, Saved(item.StableId, item.SourceProviderId), filter.Code);
        }

        foreach (bool warning in new[] { false, true })
        {
            AuthoringResourceItem item = Animation(true);
            item.Compatibility.SkeletonStableId = "offline.skeleton.other";
            AuthoringResourceFieldSpec spec = AnimationAuthoringResourceFieldSpecs.CreateSourceClip();
            spec.CompatibilityFilter.SkeletonStableId = "offline.skeleton.required";
            spec.AllowIncompatibleWithWarning = warning;
            AuthoringResourceSelectionResolutionResult result = Resolve(Collection(item), spec,
                new AuthoringResourceSelectionRef { ResourceStableId = item.StableId, SourceProviderId = item.SourceProviderId });
            Check(result.Accepted == warning && result.Reasons.Single().Code == AuthoringResourceSelectionReasonCodes.SkeletonMismatch,
                "Compatibility must retain blocking-error versus accepted-warning behavior.");
            AuthoringResourcePickerQueryResult query = Service.Query(Collection(item, null), spec, new());
            Check(query.Items.Count == 1 && query.Items[0].Selectable == warning && query.Items[0].HasWarnings == warning,
                "Query must retain non-null blocked candidates and warning status.");
        }
    }

    private static AuthoringResourceSelectionResolutionResult Resolve(AuthoringResourceCollection collection,
        AuthoringResourceFieldSpec spec, AuthoringResourceSelectionRef selection)
    {
        cases++;
        return Service.Resolve(collection, spec, new AuthoringResourceConsumerContext(), selection);
    }

    private static void RejectUnchanged(AuthoringResourceCollection collection, AuthoringResourceFieldSpec spec,
        AuthoringResourceSelectionRef selection, string code)
    {
        string before = JsonSerializer.Serialize(selection);
        AuthoringResourceSelectionResolutionResult result = Resolve(collection, spec, selection);
        Check(!result.Accepted, $"{code} must reject the selection.");
        Check(result.Reasons.Any(reason => reason.Code == code && reason.BlocksSelection &&
            reason.Severity == CharacterAuthoringValidationSeverity.Error), "Rejection must contain the expected blocking typed reason.");
        Check(ReferenceEquals(result.Selection, selection) && JsonSerializer.Serialize(selection) == before,
            "Rejection must not mutate any saved selection field.");
        if (code == AuthoringResourceSelectionReasonCodes.ItemMissing)
        {
            Check(result.Item == null, "Missing identity must not expose a replacement item.");
            AuthoringResourceSelectionReason reason = result.Reasons.Single();
            Check(reason.Category == "identity" && reason.FieldKey == spec.FieldKey &&
                reason.ResourceStableId == selection.ResourceStableId && reason.ProviderId == selection.SourceProviderId &&
                reason.BindingKind == selection.BindingKind, "Missing reason must identify the unresolved saved reference.");
        }
    }

    private static AuthoringResourceCollection Collection(params AuthoringResourceItem[] items) => new() { Items = items.ToList() };

    private static AuthoringResourceSelectionRef Saved(string stableId, string provider,
        AuthoringResourceBindingKind binding = AuthoringResourceBindingKind.ResourceManagerAsset) => new()
    {
        ResourceStableId = stableId, SourceProviderId = provider, BindingKind = binding,
        ExpectedKind = "saved.kind", ExpectedUsage = "saved.usage", ExpectedHash = "saved.hash",
        RuntimeResourceKey = "offline.clip", ProviderResourceKey = "offline.clip", PackageResourceKey = "saved.package",
        UnityGuid = "offline.guid", UnityAssetPath = "saved.path", AudioCueId = "saved.cue", AudioEventDefinitionId = "saved.event"
    };

    private static AuthoringResourceSelectionRef KeySelection(int keyKind, string provider)
    {
        var selection = new AuthoringResourceSelectionRef { SourceProviderId = provider };
        if (keyKind == 0) selection.RuntimeResourceKey = "offline.clip";
        else if (keyKind == 1) selection.ProviderResourceKey = "offline.clip";
        else selection.UnityGuid = "offline.guid";
        return selection;
    }

    private static AuthoringResourceItem Animation(bool runtime)
    {
        string provider = runtime ? AuthoringResourceProviderIds.RuntimeCatalog : AuthoringResourceProviderIds.UnityAssetDatabase;
        AuthoringResourceBindingKind binding = runtime ? AuthoringResourceBindingKind.ResourceManagerAsset : AuthoringResourceBindingKind.UnityAsset;
        return new AuthoringResourceItem
        {
            ResourceId = provider + ":offline.clip", StableId = "offline.animation.same", SourceProviderId = provider,
            Kind = CharacterPackageResourceTypeIds.Animation, Usage = AnimationAuthoringResourceUsages.AnimationClip,
            SourceKind = runtime ? AuthoringResourceSourceKind.RuntimeCatalogAsset : AuthoringResourceSourceKind.UnityAsset,
            BindingKind = binding, ImportStatus = AuthoringResourceImportStatus.Clean,
            RuntimeAvailability = runtime ? AuthoringResourceRuntimeAvailability.RuntimeReady : AuthoringResourceRuntimeAvailability.EditorOnly,
            ProviderBindings = new()
            {
                new AuthoringResourceProviderBinding
                {
                    ProviderId = provider, BindingKind = binding, IsPrimary = true,
                    RuntimeResourceKey = runtime ? "offline.clip" : string.Empty,
                    ProviderResourceKey = "offline.clip", UnityGuid = "offline.guid"
                }
            }
        };
    }

    private static AuthoringResourceItem Audio() => new()
    {
        ResourceId = "fmod:offline.event", StableId = "offline.audio.same", SourceProviderId = AuthoringResourceProviderIds.Fmod,
        Kind = CharacterPackageResourceTypeIds.Audio, Usage = CharacterPackageResourceUsageIds.AudioCue,
        SourceKind = AuthoringResourceSourceKind.FmodLibrary, BindingKind = AuthoringResourceBindingKind.AudioEventDefinition,
        ImportStatus = AuthoringResourceImportStatus.Clean, RuntimeAvailability = AuthoringResourceRuntimeAvailability.AudioCueOnly,
        ProviderBindings = new()
        {
            AudioBinding(AuthoringResourceBindingKind.AudioCue, "offline.cue"),
            AudioBinding(AuthoringResourceBindingKind.AudioEventDefinition, "offline.event"),
            new AuthoringResourceProviderBinding
            {
                BindingKind = AuthoringResourceBindingKind.ResourceManagerAsset, IsPrimary = true,
                RuntimeResourceKey = "unrelated.runtime", ProviderResourceKey = "unrelated.provider",
                PackageResourceKey = "unrelated.package", UnityGuid = "unrelated.guid", UnityAssetPath = "unrelated.path", Hash = "unrelated.hash"
            }
        }
    };

    private static AuthoringResourceProviderBinding AudioBinding(AuthoringResourceBindingKind kind, string key) => new()
    {
        ProviderId = AuthoringResourceProviderIds.Fmod, BindingKind = kind, ProviderResourceKey = key,
        ProviderData = new() { { "audioCueId", "offline.cue" }, { "audioEventDefinitionId", "offline.event" } }
    };

    private static AuthoringResourceSelectionOutputKind[] AudioOutputs() => new[]
    {
        AuthoringResourceSelectionOutputKind.AudioCueId, AuthoringResourceSelectionOutputKind.AudioEventDefinitionId,
        AuthoringResourceSelectionOutputKind.ResourceSelectionRef
    };

    private static int[][] BindingOrders() => new[]
    {
        new[] { 0, 1, 2 }, new[] { 0, 2, 1 }, new[] { 1, 0, 2 },
        new[] { 1, 2, 0 }, new[] { 2, 0, 1 }, new[] { 2, 1, 0 }
    };

    private static void Check(bool condition, string message)
    {
        assertions++;
        if (!condition) throw new InvalidOperationException($"Case {cases}, assertion {assertions}: {message}");
    }
}
