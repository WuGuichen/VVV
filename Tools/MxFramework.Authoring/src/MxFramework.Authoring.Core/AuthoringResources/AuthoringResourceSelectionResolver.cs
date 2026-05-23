using System;
using System.Collections.Generic;

namespace MxFramework.Authoring
{
    public enum AuthoringResourceSelectionOutputKind
    {
        Unknown = 0,
        ResourceSelectionRef = 1,
        RuntimeResourceKey = 2,
        UnityGuid = 3,
        UnityAssetPath = 4,
        ProviderResourceKey = 5,
        PackageResourceKey = 6,
        AudioCueId = 7,
        AudioEventDefinitionId = 8,
        ResourceStableId = 9
    }

    public static class AuthoringResourcePreloadPolicies
    {
        public const string None = "None";
        public const string SpawnCritical = "SpawnCritical";
        public const string PresentationCritical = "PresentationCritical";
        public const string EquipmentInitial = "EquipmentInitial";
        public const string AnimationWarmup = "AnimationWarmup";
        public const string VfxWarmup = "VfxWarmup";
        public const string UiDeferred = "UiDeferred";
        public const string Audio = "Audio";
        public const string AudioBank = "AudioBank";
    }

    public static class AuthoringResourceSelectionReasonCodes
    {
        public const string ItemMissing = "AUTH_PICKER_ITEM_MISSING";
        public const string ItemAmbiguous = "AUTH_PICKER_ITEM_AMBIGUOUS";
        public const string KindMismatch = "AUTH_PICKER_KIND_MISMATCH";
        public const string UsageMismatch = "AUTH_PICKER_USAGE_MISMATCH";
        public const string ProviderMismatch = "AUTH_PICKER_PROVIDER_MISMATCH";
        public const string SourceKindMismatch = "AUTH_PICKER_SOURCE_KIND_MISMATCH";
        public const string BindingMismatch = "AUTH_PICKER_BINDING_MISMATCH";
        public const string NotUnityImported = "AUTH_PICKER_NOT_UNITY_IMPORTED";
        public const string NotRuntimeLoadable = "AUTH_PICKER_NOT_RUNTIME_LOADABLE";
        public const string EditorOnlySelectedForRuntime = "AUTH_PICKER_EDITOR_ONLY_SELECTED_FOR_RUNTIME";
        public const string SkeletonMismatch = "AUTH_PICKER_SKELETON_MISMATCH";
        public const string AvatarMismatch = "AUTH_PICKER_AVATAR_MISMATCH";
        public const string SlotMismatch = "AUTH_PICKER_SLOT_MISMATCH";
        public const string BodyKindMismatch = "AUTH_PICKER_BODY_KIND_MISMATCH";
        public const string WeaponClassMismatch = "AUTH_PICKER_WEAPON_CLASS_MISMATCH";
        public const string CoordinateConventionMismatch = "AUTH_PICKER_COORDINATE_CONVENTION_MISMATCH";
        public const string BindingUnavailable = "AUTH_PICKER_BINDING_UNAVAILABLE";
    }

    public sealed class AuthoringResourceFieldSpec
    {
        public string FieldKey { get; set; } = string.Empty;
        public string EditorKind { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public List<string> AcceptedKinds { get; set; } = new List<string>();
        public List<string> AcceptedUsages { get; set; } = new List<string>();
        public List<string> AcceptedProviderIds { get; set; } = new List<string>();
        public List<AuthoringResourceSourceKind> AcceptedSourceKinds { get; set; } = new List<AuthoringResourceSourceKind>();
        public List<AuthoringResourceBindingKind> AcceptedBindingKinds { get; set; } = new List<AuthoringResourceBindingKind>();
        public bool RequireRuntimeLoadable { get; set; }
        public bool RequireUnityImported { get; set; }
        public bool AllowIncompatibleWithWarning { get; set; }
        public AuthoringResourceCompatibility CompatibilityFilter { get; set; } = new AuthoringResourceCompatibility();
        public string PreloadPolicy { get; set; } = AuthoringResourcePreloadPolicies.None;
        public AuthoringResourceSelectionOutputKind OutputKind { get; set; } = AuthoringResourceSelectionOutputKind.Unknown;
    }

    public sealed class AuthoringResourceConsumerContext
    {
        public string ConsumerKind { get; set; } = string.Empty;
        public string ConsumerStableId { get; set; } = string.Empty;
        public string ScopeId { get; set; } = string.Empty;
        public string PackageId { get; set; } = string.Empty;
        public string PackagePath { get; set; } = string.Empty;
        public string SkeletonStableId { get; set; } = string.Empty;
        public string AvatarStableId { get; set; } = string.Empty;
        public string SlotId { get; set; } = string.Empty;
        public string BodyKind { get; set; } = string.Empty;
        public string WeaponClass { get; set; } = string.Empty;
        public string UiContextId { get; set; } = string.Empty;
        public List<string> ProviderFilterIds { get; set; } = new List<string>();
        public Dictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();
    }

    public sealed class AuthoringResourceSelectionRef
    {
        public string ResourceStableId { get; set; } = string.Empty;
        public string SourceProviderId { get; set; } = string.Empty;
        public AuthoringResourceBindingKind BindingKind { get; set; } = AuthoringResourceBindingKind.None;
        public string ExpectedKind { get; set; } = string.Empty;
        public string ExpectedUsage { get; set; } = string.Empty;
        public string ExpectedHash { get; set; } = string.Empty;
        public string UnityGuid { get; set; } = string.Empty;
        public string UnityAssetPath { get; set; } = string.Empty;
        public string RuntimeResourceKey { get; set; } = string.Empty;
        public string ProviderResourceKey { get; set; } = string.Empty;
        public string PackageResourceKey { get; set; } = string.Empty;
        public string AudioCueId { get; set; } = string.Empty;
        public string AudioEventDefinitionId { get; set; } = string.Empty;
    }

    public sealed class AuthoringResourceSelectionReason
    {
        public string Code { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public CharacterAuthoringValidationSeverity Severity { get; set; }
        public bool BlocksSelection { get; set; }
        public string FieldKey { get; set; } = string.Empty;
        public string ResourceId { get; set; } = string.Empty;
        public string ResourceStableId { get; set; } = string.Empty;
        public string ProviderId { get; set; } = string.Empty;
        public string ExpectedValue { get; set; } = string.Empty;
        public string ActualValue { get; set; } = string.Empty;
        public AuthoringResourceBindingKind BindingKind { get; set; } = AuthoringResourceBindingKind.None;
        public string BindingKeyKind { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string SuggestedFix { get; set; } = string.Empty;
    }

    public sealed class AuthoringResourcePickerItem
    {
        public AuthoringResourceItem Item { get; set; }
        public bool Selectable { get; set; }
        public bool HasWarnings { get; set; }
        public List<AuthoringResourceSelectionReason> Reasons { get; set; } = new List<AuthoringResourceSelectionReason>();
    }

    public sealed class AuthoringResourcePickerQueryResult
    {
        public string FieldKey { get; set; } = string.Empty;
        public string ConsumerKind { get; set; } = string.Empty;
        public List<AuthoringResourcePickerItem> Items { get; set; } = new List<AuthoringResourcePickerItem>();
    }

    public sealed class AuthoringResourceSelectionResolutionResult
    {
        public bool Accepted { get; set; }
        public AuthoringResourceItem Item { get; set; }
        public AuthoringResourceSelectionRef Selection { get; set; } = new AuthoringResourceSelectionRef();
        public List<AuthoringResourceSelectionReason> Reasons { get; set; } = new List<AuthoringResourceSelectionReason>();
    }

    public interface IAuthoringResourceSelectionService
    {
        AuthoringResourcePickerQueryResult Query(AuthoringResourceCollection collection, AuthoringResourceFieldSpec spec, AuthoringResourceConsumerContext context);
        AuthoringResourceSelectionResolutionResult Resolve(AuthoringResourceCollection collection, AuthoringResourceFieldSpec spec, AuthoringResourceConsumerContext context, AuthoringResourceSelectionRef selection);
        List<AuthoringResourceSelectionReason> Validate(AuthoringResourceItem item, AuthoringResourceFieldSpec spec, AuthoringResourceConsumerContext context);
    }

    public sealed class AuthoringResourceSelectionService : IAuthoringResourceSelectionService
    {
        public AuthoringResourcePickerQueryResult Query(
            AuthoringResourceCollection collection,
            AuthoringResourceFieldSpec spec,
            AuthoringResourceConsumerContext context)
        {
            var result = new AuthoringResourcePickerQueryResult
            {
                FieldKey = spec != null ? spec.FieldKey ?? string.Empty : string.Empty,
                ConsumerKind = context != null ? context.ConsumerKind ?? string.Empty : string.Empty
            };

            if (collection == null || collection.Items == null)
                return result;

            for (int i = 0; i < collection.Items.Count; i++)
            {
                AuthoringResourceItem item = collection.Items[i];
                if (item == null)
                    continue;

                List<AuthoringResourceSelectionReason> reasons = Validate(item, spec, context);
                result.Items.Add(new AuthoringResourcePickerItem
                {
                    Item = item,
                    Selectable = !HasBlockingReason(reasons),
                    HasWarnings = HasWarning(reasons),
                    Reasons = reasons
                });
            }

            return result;
        }

        public AuthoringResourceSelectionResolutionResult Resolve(
            AuthoringResourceCollection collection,
            AuthoringResourceFieldSpec spec,
            AuthoringResourceConsumerContext context,
            AuthoringResourceSelectionRef selection)
        {
            var result = new AuthoringResourceSelectionResolutionResult();
            if (selection != null)
                result.Selection = selection;

            AuthoringResourceItem item = FindSelectedItem(collection, selection, result.Reasons, spec);
            result.Item = item;
            if (item == null)
            {
                if (result.Reasons.Count == 0)
                {
                    result.Reasons.Add(CreateReason(
                        CharacterAuthoringValidationSeverity.Error,
                        true,
                        AuthoringResourceSelectionReasonCodes.ItemMissing,
                        "identity",
                        spec,
                        null,
                        string.Empty,
                        string.Empty,
                        "selected resource item does not exist.",
                        "Choose an existing resource item or repair the saved ResourceSelectionRef."));
                }

                result.Accepted = false;
                return result;
            }

            result.Reasons.AddRange(Validate(item, spec, context));
            result.Accepted = !HasBlockingReason(result.Reasons);
            if (result.Accepted)
                FillSelection(result.Selection, item, spec);
            return result;
        }

        public List<AuthoringResourceSelectionReason> Validate(
            AuthoringResourceItem item,
            AuthoringResourceFieldSpec spec,
            AuthoringResourceConsumerContext context)
        {
            var reasons = new List<AuthoringResourceSelectionReason>();
            if (item == null || spec == null)
                return reasons;

            if (spec.AcceptedKinds != null && spec.AcceptedKinds.Count > 0 && !ContainsOrdinalIgnoreCase(spec.AcceptedKinds, item.Kind))
                reasons.Add(CreateReason(CharacterAuthoringValidationSeverity.Error, true, AuthoringResourceSelectionReasonCodes.KindMismatch, "filter", spec, item, Join(spec.AcceptedKinds), item.Kind, "resource kind is not accepted by this field.", "Pick a resource with an accepted kind."));

            if (spec.AcceptedUsages != null && spec.AcceptedUsages.Count > 0 && !ContainsOrdinal(spec.AcceptedUsages, item.Usage))
                reasons.Add(CreateReason(CharacterAuthoringValidationSeverity.Error, true, AuthoringResourceSelectionReasonCodes.UsageMismatch, "filter", spec, item, Join(spec.AcceptedUsages), item.Usage, "resource usage is not accepted by this field.", "Pick a resource with an accepted usage."));

            if (spec.AcceptedProviderIds != null && spec.AcceptedProviderIds.Count > 0 && !ContainsOrdinal(spec.AcceptedProviderIds, item.SourceProviderId))
                reasons.Add(CreateReason(CharacterAuthoringValidationSeverity.Error, true, AuthoringResourceSelectionReasonCodes.ProviderMismatch, "provider", spec, item, Join(spec.AcceptedProviderIds), item.SourceProviderId, "resource provider is not accepted by this field.", "Pick a resource from an accepted provider."));

            if (context != null && context.ProviderFilterIds != null && context.ProviderFilterIds.Count > 0 && !ContainsOrdinal(context.ProviderFilterIds, item.SourceProviderId))
                reasons.Add(CreateReason(CharacterAuthoringValidationSeverity.Error, true, AuthoringResourceSelectionReasonCodes.ProviderMismatch, "provider", spec, item, Join(context.ProviderFilterIds), item.SourceProviderId, "resource provider is filtered out by the current editor context.", "Clear the provider filter or pick a resource from the active provider."));

            if (spec.AcceptedSourceKinds != null && spec.AcceptedSourceKinds.Count > 0 && !spec.AcceptedSourceKinds.Contains(item.SourceKind))
                reasons.Add(CreateReason(CharacterAuthoringValidationSeverity.Error, true, AuthoringResourceSelectionReasonCodes.SourceKindMismatch, "provider", spec, item, JoinSourceKinds(spec.AcceptedSourceKinds), item.SourceKind.ToString(), "resource source kind is not accepted by this field.", "Pick a resource from an accepted source kind."));

            if (spec.AcceptedBindingKinds != null && spec.AcceptedBindingKinds.Count > 0 && !HasAcceptedBinding(item, spec.AcceptedBindingKinds))
                reasons.Add(CreateReason(CharacterAuthoringValidationSeverity.Error, true, AuthoringResourceSelectionReasonCodes.BindingMismatch, "binding", spec, item, JoinBindingKinds(spec.AcceptedBindingKinds), item.BindingKind.ToString(), "resource binding kind is not accepted by this field.", "Pick a resource with an accepted binding kind."));

            if (spec.RequireRuntimeLoadable && !IsRuntimeLoadable(item))
            {
                string code = item.BindingKind == AuthoringResourceBindingKind.UnityEditorOnlyAsset || item.BindingKind == AuthoringResourceBindingKind.UnityAsset
                    ? AuthoringResourceSelectionReasonCodes.EditorOnlySelectedForRuntime
                    : AuthoringResourceSelectionReasonCodes.NotRuntimeLoadable;
                reasons.Add(CreateReason(CharacterAuthoringValidationSeverity.Error, true, code, "runtime", spec, item, AuthoringResourceRuntimeAvailability.RuntimeReady.ToString(), item.RuntimeAvailability.ToString(), "field requires a runtime-loadable resource but this item is not runtime-ready.", "Import, compile, or choose a runtime-ready resource."));
            }

            if (spec.RequireUnityImported && !IsUnityImported(item))
                reasons.Add(CreateReason(CharacterAuthoringValidationSeverity.Error, true, AuthoringResourceSelectionReasonCodes.NotUnityImported, "import", spec, item, AuthoringResourceImportStatus.Clean.ToString(), item.ImportStatus.ToString(), "field requires an imported Unity asset but this item is not imported cleanly.", "Run Unity import or pick a clean Unity asset."));

            ValidateCompatibility(reasons, item, spec, context);
            return reasons;
        }

        private static void ValidateCompatibility(
            List<AuthoringResourceSelectionReason> reasons,
            AuthoringResourceItem item,
            AuthoringResourceFieldSpec spec,
            AuthoringResourceConsumerContext context)
        {
            if (reasons == null || item == null || item.Compatibility == null || spec == null)
                return;

            CharacterAuthoringValidationSeverity severity = spec.AllowIncompatibleWithWarning
                ? CharacterAuthoringValidationSeverity.Warning
                : CharacterAuthoringValidationSeverity.Error;
            bool blocks = severity == CharacterAuthoringValidationSeverity.Error;

            string skeleton = FirstNonEmpty(spec.CompatibilityFilter != null ? spec.CompatibilityFilter.SkeletonStableId : string.Empty, context != null ? context.SkeletonStableId : string.Empty);
            string avatar = FirstNonEmpty(spec.CompatibilityFilter != null ? spec.CompatibilityFilter.AvatarStableId : string.Empty, context != null ? context.AvatarStableId : string.Empty);
            string slot = FirstNonEmpty(spec.CompatibilityFilter != null ? spec.CompatibilityFilter.SlotId : string.Empty, context != null ? context.SlotId : string.Empty);
            string bodyKind = FirstNonEmpty(spec.CompatibilityFilter != null ? spec.CompatibilityFilter.BodyKind : string.Empty, context != null ? context.BodyKind : string.Empty);
            string weaponClass = FirstNonEmpty(spec.CompatibilityFilter != null ? spec.CompatibilityFilter.WeaponClass : string.Empty, context != null ? context.WeaponClass : string.Empty);
            string coordinate = spec.CompatibilityFilter != null ? spec.CompatibilityFilter.CoordinateConvention : string.Empty;

            if (!MatchesFilter(skeleton, item.Compatibility.SkeletonStableId))
                reasons.Add(CreateReason(severity, blocks, AuthoringResourceSelectionReasonCodes.SkeletonMismatch, "compatibility", spec, item, skeleton, item.Compatibility.SkeletonStableId, "resource skeleton is incompatible with this field.", "Choose a resource matching the requested skeleton."));

            if (!MatchesFilter(avatar, item.Compatibility.AvatarStableId))
                reasons.Add(CreateReason(severity, blocks, AuthoringResourceSelectionReasonCodes.AvatarMismatch, "compatibility", spec, item, avatar, item.Compatibility.AvatarStableId, "resource avatar is incompatible with this field.", "Choose a resource matching the requested avatar."));

            if (!MatchesFilter(slot, item.Compatibility.SlotId))
                reasons.Add(CreateReason(severity, blocks, AuthoringResourceSelectionReasonCodes.SlotMismatch, "compatibility", spec, item, slot, item.Compatibility.SlotId, "resource slot is incompatible with this field.", "Choose a resource matching the requested slot."));

            if (!MatchesFilter(bodyKind, item.Compatibility.BodyKind))
                reasons.Add(CreateReason(severity, blocks, AuthoringResourceSelectionReasonCodes.BodyKindMismatch, "compatibility", spec, item, bodyKind, item.Compatibility.BodyKind, "resource body kind is incompatible with this field.", "Choose a resource matching the requested body kind."));

            if (!MatchesFilter(weaponClass, item.Compatibility.WeaponClass))
                reasons.Add(CreateReason(severity, blocks, AuthoringResourceSelectionReasonCodes.WeaponClassMismatch, "compatibility", spec, item, weaponClass, item.Compatibility.WeaponClass, "resource weapon class is incompatible with this field.", "Choose a resource matching the requested weapon class."));

            if (!MatchesFilter(coordinate, item.Compatibility.CoordinateConvention))
                reasons.Add(CreateReason(severity, blocks, AuthoringResourceSelectionReasonCodes.CoordinateConventionMismatch, "compatibility", spec, item, coordinate, item.Compatibility.CoordinateConvention, "resource coordinate convention is incompatible with this field.", "Choose a resource using the requested coordinate convention."));
        }

        private static AuthoringResourceItem FindSelectedItem(
            AuthoringResourceCollection collection,
            AuthoringResourceSelectionRef selection,
            List<AuthoringResourceSelectionReason> reasons,
            AuthoringResourceFieldSpec spec)
        {
            if (collection == null || collection.Items == null || selection == null)
                return null;

            AuthoringResourceItem exact = null;
            if (!string.IsNullOrWhiteSpace(selection.ResourceStableId) && !string.IsNullOrWhiteSpace(selection.SourceProviderId))
            {
                for (int i = 0; i < collection.Items.Count; i++)
                {
                    AuthoringResourceItem item = collection.Items[i];
                    if (item != null &&
                        string.Equals(item.StableId, selection.ResourceStableId, StringComparison.Ordinal) &&
                        string.Equals(item.SourceProviderId, selection.SourceProviderId, StringComparison.Ordinal))
                    {
                        exact = item;
                        break;
                    }
                }
            }

            if (exact != null)
                return exact;

            var matches = new List<AuthoringResourceItem>();
            for (int i = 0; i < collection.Items.Count; i++)
            {
                AuthoringResourceItem item = collection.Items[i];
                if (item == null)
                    continue;

                if (!string.IsNullOrWhiteSpace(selection.ResourceStableId) && string.Equals(item.StableId, selection.ResourceStableId, StringComparison.Ordinal))
                    matches.Add(item);
                else if (!string.IsNullOrWhiteSpace(selection.RuntimeResourceKey) && HasBindingValue(item, selection.RuntimeResourceKey))
                    matches.Add(item);
                else if (!string.IsNullOrWhiteSpace(selection.ProviderResourceKey) && HasBindingValue(item, selection.ProviderResourceKey))
                    matches.Add(item);
                else if (!string.IsNullOrWhiteSpace(selection.UnityGuid) && HasBindingValue(item, selection.UnityGuid))
                    matches.Add(item);
            }

            if (matches.Count == 1)
                return matches[0];

            if (matches.Count > 1 && reasons != null)
            {
                reasons.Add(CreateReason(
                    CharacterAuthoringValidationSeverity.Error,
                    true,
                    AuthoringResourceSelectionReasonCodes.ItemAmbiguous,
                    "identity",
                    spec,
                    matches[0],
                    selection.ResourceStableId,
                    matches.Count.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    "selection stable id matches multiple resource providers.",
                    "Persist sourceProviderId in the ResourceSelectionRef and re-select the resource."));
            }

            return null;
        }

        private static void FillSelection(AuthoringResourceSelectionRef selection, AuthoringResourceItem item, AuthoringResourceFieldSpec spec)
        {
            if (selection == null || item == null)
                return;

            AuthoringResourceProviderBinding binding = FindBinding(item, spec);
            if (binding == null)
                binding = FindPrimaryBinding(item);

            string expectedHash = FirstNonEmpty(
                binding != null ? binding.Hash : string.Empty,
                GetFirstBindingValue(item, BindingValueKind.Hash));
            string providerResourceKey = FirstNonEmpty(
                binding != null ? binding.ProviderResourceKey : string.Empty,
                GetFirstBindingValue(item, BindingValueKind.ProviderResourceKey));
            string packageResourceKey = FirstNonEmpty(
                binding != null ? binding.PackageResourceKey : string.Empty,
                GetFirstBindingValue(item, BindingValueKind.PackageResourceKey));
            string runtimeResourceKey = FirstNonEmpty(
                binding != null ? binding.RuntimeResourceKey : string.Empty,
                GetFirstBindingValue(item, BindingValueKind.RuntimeResourceKey));
            string unityGuid = FirstNonEmpty(
                binding != null ? binding.UnityGuid : string.Empty,
                GetFirstBindingValue(item, BindingValueKind.UnityGuid));
            string unityAssetPath = FirstNonEmpty(
                binding != null ? binding.UnityAssetPath : string.Empty,
                GetFirstBindingValue(item, BindingValueKind.UnityAssetPath));
            string audioCueId = FirstNonEmpty(
                GetProviderData(binding, "audioCueId"),
                GetFirstBindingValue(item, BindingValueKind.AudioCueId));
            string audioEventDefinitionId = FirstNonEmpty(
                GetProviderData(binding, "audioEventDefinitionId"),
                GetFirstBindingValue(item, BindingValueKind.AudioEventDefinitionId));

            selection.ResourceStableId = item.StableId;
            selection.SourceProviderId = item.SourceProviderId;
            selection.BindingKind = binding != null ? binding.BindingKind : item.BindingKind;
            selection.ExpectedKind = item.Kind;
            selection.ExpectedUsage = item.Usage;
            selection.ExpectedHash = FirstNonEmpty(expectedHash, selection.ExpectedHash);
            selection.ProviderResourceKey = FirstNonEmpty(providerResourceKey, selection.ProviderResourceKey);
            selection.PackageResourceKey = FirstNonEmpty(packageResourceKey, selection.PackageResourceKey);
            selection.RuntimeResourceKey = FirstNonEmpty(runtimeResourceKey, selection.RuntimeResourceKey);
            selection.UnityGuid = FirstNonEmpty(unityGuid, selection.UnityGuid);
            selection.UnityAssetPath = FirstNonEmpty(unityAssetPath, selection.UnityAssetPath);
            selection.AudioCueId = FirstNonEmpty(audioCueId, selection.AudioCueId);
            selection.AudioEventDefinitionId = FirstNonEmpty(audioEventDefinitionId, selection.AudioEventDefinitionId);

            if (spec == null)
                return;

            if (spec.OutputKind == AuthoringResourceSelectionOutputKind.PackageResourceKey && string.IsNullOrWhiteSpace(selection.PackageResourceKey))
                selection.PackageResourceKey = selection.ProviderResourceKey;
            if (spec.OutputKind == AuthoringResourceSelectionOutputKind.ProviderResourceKey && string.IsNullOrWhiteSpace(selection.ProviderResourceKey))
                selection.ProviderResourceKey = FirstNonEmpty(selection.PackageResourceKey, selection.RuntimeResourceKey);
        }

        private static AuthoringResourceProviderBinding FindBinding(AuthoringResourceItem item, AuthoringResourceFieldSpec spec)
        {
            if (item == null || item.ProviderBindings == null || item.ProviderBindings.Count == 0)
                return null;

            AuthoringResourceSelectionOutputKind outputKind = spec != null
                ? spec.OutputKind
                : AuthoringResourceSelectionOutputKind.Unknown;
            bool prioritizeCompleteness =
                outputKind == AuthoringResourceSelectionOutputKind.Unknown ||
                outputKind == AuthoringResourceSelectionOutputKind.ResourceSelectionRef;
            AuthoringResourceProviderBinding best = null;
            int bestScore = int.MinValue;

            for (int i = 0; i < item.ProviderBindings.Count; i++)
            {
                AuthoringResourceProviderBinding binding = item.ProviderBindings[i];
                if (binding == null)
                    continue;
                if (spec != null && spec.AcceptedBindingKinds != null && spec.AcceptedBindingKinds.Count > 0 && !spec.AcceptedBindingKinds.Contains(binding.BindingKind))
                    continue;
                if (!MatchesOutputKind(binding, outputKind))
                    continue;

                if (!prioritizeCompleteness)
                    return binding;

                int score = ComputeBindingCompletenessScore(binding);
                if (best == null || score > bestScore)
                {
                    best = binding;
                    bestScore = score;
                }
            }

            if (best != null)
                return best;

            return FindPrimaryBinding(item);
        }

        private enum BindingValueKind
        {
            RuntimeResourceKey,
            ProviderResourceKey,
            PackageResourceKey,
            UnityGuid,
            UnityAssetPath,
            Hash,
            AudioCueId,
            AudioEventDefinitionId
        }

        private static int ComputeBindingCompletenessScore(AuthoringResourceProviderBinding binding)
        {
            if (binding == null)
                return int.MinValue;

            int score = 0;
            if (binding.IsPrimary)
                score += 1;
            if (!string.IsNullOrWhiteSpace(GetBindingValue(binding, BindingValueKind.RuntimeResourceKey)))
                score += 4;
            if (!string.IsNullOrWhiteSpace(GetBindingValue(binding, BindingValueKind.UnityAssetPath)))
                score += 3;
            if (!string.IsNullOrWhiteSpace(GetBindingValue(binding, BindingValueKind.ProviderResourceKey)))
                score += 2;
            if (!string.IsNullOrWhiteSpace(GetBindingValue(binding, BindingValueKind.Hash)))
                score += 1;
            return score;
        }

        private static string GetFirstBindingValue(AuthoringResourceItem item, BindingValueKind kind)
        {
            if (item == null || item.ProviderBindings == null)
                return string.Empty;

            for (int i = 0; i < item.ProviderBindings.Count; i++)
            {
                AuthoringResourceProviderBinding binding = item.ProviderBindings[i];
                string value = GetBindingValue(binding, kind);
                if (!string.IsNullOrWhiteSpace(value))
                    return value;
            }

            return string.Empty;
        }

        private static string GetBindingValue(AuthoringResourceProviderBinding binding, BindingValueKind kind)
        {
            if (binding == null)
                return string.Empty;

            switch (kind)
            {
                case BindingValueKind.RuntimeResourceKey:
                    return binding.RuntimeResourceKey ?? string.Empty;
                case BindingValueKind.ProviderResourceKey:
                    return binding.ProviderResourceKey ?? string.Empty;
                case BindingValueKind.PackageResourceKey:
                    return binding.PackageResourceKey ?? string.Empty;
                case BindingValueKind.UnityGuid:
                    return FirstNonEmpty(binding.UnityGuid, GetProviderData(binding, "unityGuid"));
                case BindingValueKind.UnityAssetPath:
                    return FirstNonEmpty(binding.UnityAssetPath, GetProviderData(binding, "unityAssetPath"));
                case BindingValueKind.Hash:
                    return FirstNonEmpty(binding.Hash, GetProviderData(binding, "hash"));
                case BindingValueKind.AudioCueId:
                    return GetProviderData(binding, "audioCueId");
                case BindingValueKind.AudioEventDefinitionId:
                    return GetProviderData(binding, "audioEventDefinitionId");
                default:
                    return string.Empty;
            }
        }

        private static AuthoringResourceProviderBinding FindPrimaryBinding(AuthoringResourceItem item)
        {
            if (item == null || item.ProviderBindings == null || item.ProviderBindings.Count == 0)
                return null;

            for (int i = 0; i < item.ProviderBindings.Count; i++)
            {
                AuthoringResourceProviderBinding binding = item.ProviderBindings[i];
                if (binding != null && binding.IsPrimary)
                    return binding;
            }

            return item.ProviderBindings[0];
        }

        private static bool MatchesOutputKind(AuthoringResourceProviderBinding binding, AuthoringResourceSelectionOutputKind outputKind)
        {
            if (binding == null || outputKind == AuthoringResourceSelectionOutputKind.Unknown || outputKind == AuthoringResourceSelectionOutputKind.ResourceSelectionRef)
                return true;

            if (outputKind == AuthoringResourceSelectionOutputKind.RuntimeResourceKey)
                return !string.IsNullOrWhiteSpace(binding.RuntimeResourceKey);
            if (outputKind == AuthoringResourceSelectionOutputKind.UnityGuid)
                return !string.IsNullOrWhiteSpace(binding.UnityGuid);
            if (outputKind == AuthoringResourceSelectionOutputKind.UnityAssetPath)
                return !string.IsNullOrWhiteSpace(binding.UnityAssetPath);
            if (outputKind == AuthoringResourceSelectionOutputKind.ProviderResourceKey)
                return !string.IsNullOrWhiteSpace(binding.ProviderResourceKey);
            if (outputKind == AuthoringResourceSelectionOutputKind.PackageResourceKey)
                return !string.IsNullOrWhiteSpace(binding.PackageResourceKey);
            if (outputKind == AuthoringResourceSelectionOutputKind.AudioCueId)
                return !string.IsNullOrWhiteSpace(GetProviderData(binding, "audioCueId"));
            if (outputKind == AuthoringResourceSelectionOutputKind.AudioEventDefinitionId)
                return !string.IsNullOrWhiteSpace(GetProviderData(binding, "audioEventDefinitionId"));

            return true;
        }

        private static bool IsRuntimeLoadable(AuthoringResourceItem item)
        {
            if (item == null)
                return false;

            if (item.BindingKind == AuthoringResourceBindingKind.ResourceManagerAsset)
                return item.RuntimeAvailability == AuthoringResourceRuntimeAvailability.RuntimeReady &&
                       HasRuntimeResourceKey(item);
            if (item.BindingKind == AuthoringResourceBindingKind.AudioCue ||
                item.BindingKind == AuthoringResourceBindingKind.AudioEventDefinition)
                return item.RuntimeAvailability == AuthoringResourceRuntimeAvailability.AudioCueOnly ||
                       item.RuntimeAvailability == AuthoringResourceRuntimeAvailability.RuntimeReady;

            return false;
        }

        private static bool IsUnityImported(AuthoringResourceItem item)
        {
            if (item == null)
                return false;

            return item.ImportStatus == AuthoringResourceImportStatus.Clean ||
                   item.ImportStatus == AuthoringResourceImportStatus.ManualOverride;
        }

        private static bool HasRuntimeResourceKey(AuthoringResourceItem item)
        {
            if (item == null || item.ProviderBindings == null)
                return false;

            for (int i = 0; i < item.ProviderBindings.Count; i++)
            {
                AuthoringResourceProviderBinding binding = item.ProviderBindings[i];
                if (binding != null && !string.IsNullOrWhiteSpace(binding.RuntimeResourceKey))
                    return true;
            }

            return false;
        }

        private static bool HasAcceptedBinding(AuthoringResourceItem item, List<AuthoringResourceBindingKind> accepted)
        {
            if (item == null || accepted == null || accepted.Count == 0)
                return true;
            if (accepted.Contains(item.BindingKind))
                return true;
            if (item.ProviderBindings == null)
                return false;

            for (int i = 0; i < item.ProviderBindings.Count; i++)
            {
                AuthoringResourceProviderBinding binding = item.ProviderBindings[i];
                if (binding != null && accepted.Contains(binding.BindingKind))
                    return true;
            }

            return false;
        }

        private static bool HasBindingValue(AuthoringResourceItem item, string value)
        {
            if (item == null || item.ProviderBindings == null || string.IsNullOrWhiteSpace(value))
                return false;

            for (int i = 0; i < item.ProviderBindings.Count; i++)
            {
                AuthoringResourceProviderBinding binding = item.ProviderBindings[i];
                if (binding == null)
                    continue;

                if (string.Equals(binding.ProviderResourceKey, value, StringComparison.Ordinal) ||
                    string.Equals(binding.RuntimeResourceKey, value, StringComparison.Ordinal) ||
                    string.Equals(binding.PackageResourceKey, value, StringComparison.Ordinal) ||
                    string.Equals(binding.UnityGuid, value, StringComparison.Ordinal) ||
                    string.Equals(binding.UnityAssetPath, value, StringComparison.Ordinal) ||
                    string.Equals(binding.FmodEventGuid, value, StringComparison.Ordinal) ||
                    string.Equals(binding.FmodEventPath, value, StringComparison.Ordinal) ||
                    string.Equals(binding.ExternalSourcePath, value, StringComparison.Ordinal) ||
                    string.Equals(binding.Address, value, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private static AuthoringResourceSelectionReason CreateReason(
            CharacterAuthoringValidationSeverity severity,
            bool blocksSelection,
            string code,
            string category,
            AuthoringResourceFieldSpec spec,
            AuthoringResourceItem item,
            string expectedValue,
            string actualValue,
            string message,
            string suggestedFix)
        {
            return new AuthoringResourceSelectionReason
            {
                Severity = severity,
                BlocksSelection = blocksSelection,
                Code = code ?? string.Empty,
                Category = category ?? string.Empty,
                FieldKey = spec != null ? spec.FieldKey ?? string.Empty : string.Empty,
                ResourceId = item != null ? item.ResourceId ?? string.Empty : string.Empty,
                ResourceStableId = item != null ? item.StableId ?? string.Empty : string.Empty,
                ProviderId = item != null ? item.SourceProviderId ?? string.Empty : string.Empty,
                ExpectedValue = expectedValue ?? string.Empty,
                ActualValue = actualValue ?? string.Empty,
                BindingKind = item != null ? item.BindingKind : AuthoringResourceBindingKind.None,
                BindingKeyKind = item != null && item.ProviderBindings != null && item.ProviderBindings.Count > 0 && item.ProviderBindings[0] != null
                    ? item.ProviderBindings[0].BindingKeyKind ?? string.Empty
                    : string.Empty,
                Message = message ?? string.Empty,
                SuggestedFix = suggestedFix ?? string.Empty
            };
        }

        private static bool HasBlockingReason(List<AuthoringResourceSelectionReason> reasons)
        {
            if (reasons == null)
                return false;

            for (int i = 0; i < reasons.Count; i++)
            {
                AuthoringResourceSelectionReason reason = reasons[i];
                if (reason != null && reason.BlocksSelection)
                    return true;
            }

            return false;
        }

        private static bool HasWarning(List<AuthoringResourceSelectionReason> reasons)
        {
            if (reasons == null)
                return false;

            for (int i = 0; i < reasons.Count; i++)
            {
                AuthoringResourceSelectionReason reason = reasons[i];
                if (reason != null && reason.Severity == CharacterAuthoringValidationSeverity.Warning)
                    return true;
            }

            return false;
        }

        private static bool ContainsOrdinal(List<string> values, string value)
        {
            if (values == null || string.IsNullOrWhiteSpace(value))
                return false;

            for (int i = 0; i < values.Count; i++)
            {
                if (string.Equals(values[i], value, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private static bool ContainsOrdinalIgnoreCase(List<string> values, string value)
        {
            if (values == null || string.IsNullOrWhiteSpace(value))
                return false;

            for (int i = 0; i < values.Count; i++)
            {
                if (string.Equals(values[i], value, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static bool MatchesFilter(string required, string actual)
        {
            return string.IsNullOrWhiteSpace(required) ||
                   string.IsNullOrWhiteSpace(actual) ||
                   string.Equals(required, actual, StringComparison.Ordinal);
        }

        private static string FirstNonEmpty(string first, string second)
        {
            return !string.IsNullOrWhiteSpace(first) ? first : second ?? string.Empty;
        }

        private static string GetProviderData(AuthoringResourceProviderBinding binding, string key)
        {
            if (binding == null || binding.ProviderData == null || string.IsNullOrWhiteSpace(key))
                return string.Empty;

            return binding.ProviderData.TryGetValue(key, out string value) ? value ?? string.Empty : string.Empty;
        }

        private static string Join(List<string> values)
        {
            return values == null ? string.Empty : string.Join(",", values);
        }

        private static string JoinBindingKinds(List<AuthoringResourceBindingKind> values)
        {
            if (values == null)
                return string.Empty;

            var text = new List<string>();
            for (int i = 0; i < values.Count; i++)
                text.Add(values[i].ToString());
            return string.Join(",", text);
        }

        private static string JoinSourceKinds(List<AuthoringResourceSourceKind> values)
        {
            if (values == null)
                return string.Empty;

            var text = new List<string>();
            for (int i = 0; i < values.Count; i++)
                text.Add(values[i].ToString());
            return string.Join(",", text);
        }
    }
}
