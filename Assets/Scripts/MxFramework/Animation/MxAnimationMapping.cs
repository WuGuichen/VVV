using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using MxFramework.Resources;

namespace MxFramework.Animation
{
    public readonly struct MxAnimationClipRegistryEntry
    {
        public MxAnimationClipRegistryEntry(ResourceKey clipKey, string catalogEntryHash = "")
        {
            ClipKey = clipKey;
            CatalogEntryHash = catalogEntryHash ?? string.Empty;
        }

        public ResourceKey ClipKey { get; }
        public string CatalogEntryHash { get; }
    }

    public sealed class MxAnimationClipRegistry
    {
        private readonly List<MxAnimationClipRegistryEntry> _entries;

        public MxAnimationClipRegistry(
            int version,
            string catalogId,
            string catalogHash,
            IEnumerable<MxAnimationClipRegistryEntry> entries)
        {
            Version = version < 0 ? 0 : version;
            CatalogId = catalogId ?? string.Empty;
            CatalogHash = catalogHash ?? string.Empty;
            _entries = entries != null
                ? new List<MxAnimationClipRegistryEntry>(entries)
                : new List<MxAnimationClipRegistryEntry>();
        }

        public int Version { get; }
        public string CatalogId { get; }
        public string CatalogHash { get; }
        public IReadOnlyList<MxAnimationClipRegistryEntry> Entries => _entries;

        public bool Contains(ResourceKey clipKey)
        {
            return TryFind(clipKey, out _);
        }

        public bool TryFind(ResourceKey clipKey, out MxAnimationClipRegistryEntry entry)
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                MxAnimationClipRegistryEntry candidate = _entries[i];
                if (!MatchesClipKey(candidate.ClipKey, clipKey))
                    continue;

                entry = candidate;
                return true;
            }

            entry = default;
            return false;
        }

        private static bool MatchesClipKey(ResourceKey registeredKey, ResourceKey requestedKey)
        {
            if (!string.Equals(registeredKey.Id, requestedKey.Id, StringComparison.Ordinal))
                return false;
            if (!string.Equals(registeredKey.TypeId, requestedKey.TypeId, StringComparison.Ordinal))
                return false;
            if (!string.Equals(registeredKey.Variant, requestedKey.Variant, StringComparison.Ordinal))
                return false;
            if (!string.IsNullOrWhiteSpace(requestedKey.PackageId)
                && !string.Equals(registeredKey.PackageId, requestedKey.PackageId, StringComparison.Ordinal))
                return false;

            return true;
        }
    }

    public static class MxAnimationClipRegistryBuilder
    {
        public static MxAnimationClipRegistry FromCatalog(
            ResourceCatalog catalog,
            int version = 1,
            string catalogHash = "")
        {
            if (catalog == null)
                return new MxAnimationClipRegistry(version, string.Empty, catalogHash, null);

            var entries = new List<MxAnimationClipRegistryEntry>();
            for (int i = 0; i < catalog.Entries.Count; i++)
            {
                ResourceCatalogEntry entry = catalog.Entries[i];
                if (entry == null || !string.Equals(entry.TypeId, ResourceTypeIds.AnimationClip, StringComparison.Ordinal))
                    continue;

                entries.Add(new MxAnimationClipRegistryEntry(entry.CreateKey(catalog.PackageId), entry.Hash));
            }

            entries.Sort(CompareEntries);
            return new MxAnimationClipRegistry(version, catalog.CatalogId, catalogHash, entries);
        }

        private static int CompareEntries(MxAnimationClipRegistryEntry left, MxAnimationClipRegistryEntry right)
        {
            return string.CompareOrdinal(left.ClipKey.ToString(), right.ClipKey.ToString());
        }
    }

    public interface IMxAnimationMappingProvider
    {
        IReadOnlyList<MxAnimationSetDefinition> Definitions { get; }
        bool TryFindDefinition(string setId, out MxAnimationSetDefinition definition);
    }

    public sealed class MxAnimationStaticMappingProvider : IMxAnimationMappingProvider
    {
        private readonly List<MxAnimationSetDefinition> _definitions;

        public MxAnimationStaticMappingProvider(IEnumerable<MxAnimationSetDefinition> definitions)
        {
            _definitions = definitions != null
                ? new List<MxAnimationSetDefinition>(definitions)
                : new List<MxAnimationSetDefinition>();
        }

        public IReadOnlyList<MxAnimationSetDefinition> Definitions => _definitions;

        public bool TryFindDefinition(string setId, out MxAnimationSetDefinition definition)
        {
            for (int i = 0; i < _definitions.Count; i++)
            {
                MxAnimationSetDefinition candidate = _definitions[i];
                if (candidate == null || !string.Equals(candidate.SetId, setId ?? string.Empty, StringComparison.Ordinal))
                    continue;

                definition = candidate;
                return true;
            }

            definition = null;
            return false;
        }
    }

    public static class MxAnimationActionKey
    {
        public static bool IsValid(string actionKey)
        {
            if (string.IsNullOrWhiteSpace(actionKey))
                return false;

            for (int i = 0; i < actionKey.Length; i++)
            {
                char c = actionKey[i];
                if (char.IsWhiteSpace(c) || char.IsControl(c))
                    return false;
            }

            return true;
        }
    }

    public static class MxAnimationSetDefinitionHasher
    {
        public const string HashPrefix = "sha256:";

        public static string ComputeHash(MxAnimationSetDefinition definition)
        {
            if (definition == null)
                return string.Empty;

            string canonical = CreateCanonicalText(definition);
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] bytes = Encoding.UTF8.GetBytes(canonical);
                byte[] hash = sha256.ComputeHash(bytes);
                return HashPrefix + ToHex(hash);
            }
        }

        public static string CreateCanonicalText(MxAnimationSetDefinition definition)
        {
            if (definition == null)
                return string.Empty;

            var builder = new StringBuilder();
            builder.Append("mxanimation.set.v1").Append('\n');
            builder.Append("set=").Append(definition.SetId ?? string.Empty).Append('\n');
            builder.Append("version=").Append(definition.Version.ToString(CultureInfo.InvariantCulture)).Append('\n');
            AppendResourceKey(builder, "default", definition.DefaultClip);
            AppendResourceKey(builder, "fallback", definition.FallbackClip);

            var layers = new List<MxAnimationLayerDefinition>(definition.Layers);
            layers.Sort(CompareLayerDefinition);
            for (int i = 0; i < layers.Count; i++)
                AppendLayer(builder, layers[i], i);

            AppendWarmup(builder, definition.Warmup);

            var actions = new List<MxAnimationActionBinding>(definition.Actions);
            actions.Sort(CompareActionBinding);
            for (int i = 0; i < actions.Count; i++)
                AppendAction(builder, actions[i], i);

            var events = new List<MxAnimationPresentationEvent>(definition.Events);
            events.Sort(ComparePresentationEvent);
            for (int i = 0; i < events.Count; i++)
                AppendEvent(builder, "setEvent", events[i], i);

            return builder.ToString();
        }

        private static void AppendLayer(StringBuilder builder, MxAnimationLayerDefinition layer, int index)
        {
            builder.Append("layer[").Append(index.ToString(CultureInfo.InvariantCulture)).Append("]").Append('\n');
            if (layer == null)
            {
                builder.Append("null").Append('\n');
                return;
            }

            builder.Append("id=").Append(layer.LayerId.Value).Append('\n');
            builder.Append("profile=").Append(layer.ProfileId ?? string.Empty).Append('\n');
            builder.Append("weight=").Append(layer.DefaultWeight.ToString("R", CultureInfo.InvariantCulture)).Append('\n');
            builder.Append("blend=").Append(((int)layer.BlendMode).ToString(CultureInfo.InvariantCulture)).Append('\n');
            AppendResourceKey(builder, "mask", layer.AvatarMaskKey);
        }

        private static void AppendWarmup(StringBuilder builder, MxAnimationWarmupDefinition warmup)
        {
            if (warmup == null || warmup.IsDefault)
                return;

            builder.Append("warmup").Append('\n');
            builder.Append("group=").Append(warmup.GroupId ?? string.Empty).Append('\n');
            builder.Append("failFast=").Append(warmup.FailFast ? "1" : "0").Append('\n');
            builder.Append("includeDefault=").Append(warmup.IncludeDefaultClip ? "1" : "0").Append('\n');
            builder.Append("includeFallback=").Append(warmup.IncludeFallbackClip ? "1" : "0").Append('\n');
            builder.Append("includeActions=").Append(warmup.IncludeActionClips ? "1" : "0").Append('\n');
            builder.Append("includeMasks=").Append(warmup.IncludeLayerMasks ? "1" : "0").Append('\n');

            var keys = new List<ResourceKey>(warmup.RequiredKeys);
            keys.Sort(CompareResourceKey);
            for (int i = 0; i < keys.Count; i++)
                AppendResourceKey(builder, "warmup.key[" + i.ToString(CultureInfo.InvariantCulture) + "]", keys[i]);

            var labels = new List<string>(warmup.Labels);
            labels.Sort(StringComparer.Ordinal);
            for (int i = 0; i < labels.Count; i++)
                builder.Append("warmup.label[").Append(i.ToString(CultureInfo.InvariantCulture)).Append("]=").Append(labels[i] ?? string.Empty).Append('\n');
        }

        private static void AppendAction(StringBuilder builder, MxAnimationActionBinding binding, int index)
        {
            builder.Append("action[").Append(index.ToString(CultureInfo.InvariantCulture)).Append("]").Append('\n');
            if (binding == null)
            {
                builder.Append("null").Append('\n');
                return;
            }

            builder.Append("bindingId=").Append(binding.BindingId ?? string.Empty).Append('\n');
            builder.Append("actionKey=").Append(binding.ActionKey ?? string.Empty).Append('\n');
            AppendResourceKey(builder, "clip", binding.Clip);
            builder.Append("layer=").Append(binding.Layer.Value).Append('\n');
            builder.Append("speed=").Append(binding.PlaybackSpeed.ToString("R", CultureInfo.InvariantCulture)).Append('\n');
            builder.Append("loop=").Append(binding.Loop ? "1" : "0").Append('\n');
            builder.Append("alignment=").Append(((int)binding.AlignmentPolicy).ToString(CultureInfo.InvariantCulture)).Append('\n');
            builder.Append("fade=").Append(binding.FadeDurationSeconds.ToString("R", CultureInfo.InvariantCulture)).Append('\n');

            var events = new List<MxAnimationPresentationEvent>(binding.PresentationEvents);
            events.Sort(ComparePresentationEvent);
            for (int i = 0; i < events.Count; i++)
                AppendEvent(builder, "bindingEvent", events[i], i);
        }

        private static void AppendEvent(StringBuilder builder, string prefix, MxAnimationPresentationEvent animationEvent, int index)
        {
            builder.Append(prefix).Append('[').Append(index.ToString(CultureInfo.InvariantCulture)).Append("]").Append('\n');
            if (animationEvent == null)
            {
                builder.Append("null").Append('\n');
                return;
            }

            builder.Append("eventId=").Append(animationEvent.EventId ?? string.Empty).Append('\n');
            builder.Append("domain=").Append(((int)animationEvent.TimeDomain).ToString(CultureInfo.InvariantCulture)).Append('\n');
            builder.Append("time=").Append(animationEvent.Time.ToString("R", CultureInfo.InvariantCulture)).Append('\n');
            builder.Append("kind=").Append(animationEvent.EventKind ?? string.Empty).Append('\n');
            AppendResourceKey(builder, "payload", animationEvent.PayloadKey);
            builder.Append("socket=").Append(animationEvent.Socket ?? string.Empty).Append('\n');
            builder.Append("tag=").Append(animationEvent.Tag ?? string.Empty).Append('\n');
            builder.Append("replay=").Append(((int)animationEvent.ReplayPolicy).ToString(CultureInfo.InvariantCulture)).Append('\n');
        }

        private static void AppendResourceKey(StringBuilder builder, string prefix, ResourceKey key)
        {
            builder.Append(prefix).Append(".id=").Append(key.Id ?? string.Empty).Append('\n');
            builder.Append(prefix).Append(".type=").Append(key.TypeId ?? string.Empty).Append('\n');
            builder.Append(prefix).Append(".variant=").Append(key.Variant ?? string.Empty).Append('\n');
            builder.Append(prefix).Append(".package=").Append(key.PackageId ?? string.Empty).Append('\n');
        }

        private static int CompareActionBinding(MxAnimationActionBinding left, MxAnimationActionBinding right)
        {
            if (ReferenceEquals(left, right))
                return 0;
            if (left == null)
                return -1;
            if (right == null)
                return 1;

            int result = string.CompareOrdinal(left.BindingId, right.BindingId);
            if (result != 0)
                return result;

            result = string.CompareOrdinal(left.ActionKey, right.ActionKey);
            if (result != 0)
                return result;

            return string.CompareOrdinal(left.Clip.ToString(), right.Clip.ToString());
        }

        private static int CompareLayerDefinition(MxAnimationLayerDefinition left, MxAnimationLayerDefinition right)
        {
            if (ReferenceEquals(left, right))
                return 0;
            if (left == null)
                return -1;
            if (right == null)
                return 1;

            int result = string.CompareOrdinal(left.LayerId.Value, right.LayerId.Value);
            if (result != 0)
                return result;

            result = string.CompareOrdinal(left.ProfileId, right.ProfileId);
            if (result != 0)
                return result;

            return CompareResourceKey(left.AvatarMaskKey, right.AvatarMaskKey);
        }

        private static int ComparePresentationEvent(MxAnimationPresentationEvent left, MxAnimationPresentationEvent right)
        {
            if (ReferenceEquals(left, right))
                return 0;
            if (left == null)
                return -1;
            if (right == null)
                return 1;

            int result = string.CompareOrdinal(left.EventId, right.EventId);
            if (result != 0)
                return result;

            result = ((int)left.TimeDomain).CompareTo((int)right.TimeDomain);
            if (result != 0)
                return result;

            result = left.Time.CompareTo(right.Time);
            if (result != 0)
                return result;

            result = string.CompareOrdinal(left.EventKind, right.EventKind);
            if (result != 0)
                return result;

            result = CompareResourceKey(left.PayloadKey, right.PayloadKey);
            if (result != 0)
                return result;

            result = string.CompareOrdinal(left.Socket, right.Socket);
            if (result != 0)
                return result;

            result = string.CompareOrdinal(left.Tag, right.Tag);
            if (result != 0)
                return result;

            return ((int)left.ReplayPolicy).CompareTo((int)right.ReplayPolicy);
        }

        private static int CompareResourceKey(ResourceKey left, ResourceKey right)
        {
            int result = string.CompareOrdinal(left.Id, right.Id);
            if (result != 0)
                return result;

            result = string.CompareOrdinal(left.TypeId, right.TypeId);
            if (result != 0)
                return result;

            result = string.CompareOrdinal(left.Variant, right.Variant);
            if (result != 0)
                return result;

            return string.CompareOrdinal(left.PackageId, right.PackageId);
        }

        private static string ToHex(byte[] bytes)
        {
            var builder = new StringBuilder(bytes.Length * 2);
            for (int i = 0; i < bytes.Length; i++)
                builder.Append(bytes[i].ToString("x2", CultureInfo.InvariantCulture));
            return builder.ToString();
        }
    }

    public readonly struct MxAnimationEventTimelineRow
    {
        public MxAnimationEventTimelineRow(
            string setId,
            string bindingId,
            string actionKey,
            int sourceOrder,
            bool setLevel,
            MxAnimationPresentationEvent animationEvent)
        {
            SetId = setId ?? string.Empty;
            BindingId = bindingId ?? string.Empty;
            ActionKey = actionKey ?? string.Empty;
            SourceOrder = Math.Max(0, sourceOrder);
            SetLevel = setLevel;
            Event = animationEvent;
        }

        public string SetId { get; }
        public string BindingId { get; }
        public string ActionKey { get; }
        public int SourceOrder { get; }
        public bool SetLevel { get; }
        public MxAnimationPresentationEvent Event { get; }
        public string EventId => Event != null ? Event.EventId : string.Empty;
        public MxAnimationEventTimeDomain TimeDomain => Event != null ? Event.TimeDomain : MxAnimationEventTimeDomain.Seconds;
        public float Time => Event != null ? Event.Time : 0f;
        public string EventKind => Event != null ? Event.EventKind : string.Empty;
        public ResourceKey PayloadKey => Event != null ? Event.PayloadKey : default;
        public MxAnimationPresentationEventReplayPolicy ReplayPolicy =>
            Event != null ? Event.ReplayPolicy : MxAnimationPresentationEventReplayPolicy.OneShot;
        public bool HasDeterministicCorrelation =>
            TimeDomain == MxAnimationEventTimeDomain.CombatFrame
            || TimeDomain == MxAnimationEventTimeDomain.PresentationFrame;

        public string CorrelationLabel
        {
            get
            {
                if (!HasDeterministicCorrelation)
                    return string.Empty;

                return "set=" + SetId
                    + "|binding=" + BindingId
                    + "|action=" + ActionKey
                    + "|domain=" + TimeDomain
                    + "|time=" + Time.ToString("R", CultureInfo.InvariantCulture)
                    + "|event=" + EventId
                    + "|order=" + SourceOrder.ToString(CultureInfo.InvariantCulture);
            }
        }
    }

    public static class MxAnimationEventTimelineBuilder
    {
        public static IReadOnlyList<MxAnimationEventTimelineRow> BuildRows(MxAnimationSetDefinition definition)
        {
            if (definition == null)
                return Array.Empty<MxAnimationEventTimelineRow>();

            var rows = new List<MxAnimationEventTimelineRow>();
            for (int i = 0; i < definition.Events.Count; i++)
                rows.Add(new MxAnimationEventTimelineRow(definition.SetId, string.Empty, string.Empty, i, true, definition.Events[i]));

            for (int bindingIndex = 0; bindingIndex < definition.Actions.Count; bindingIndex++)
            {
                MxAnimationActionBinding binding = definition.Actions[bindingIndex];
                if (binding == null)
                    continue;

                for (int eventIndex = 0; eventIndex < binding.PresentationEvents.Count; eventIndex++)
                {
                    rows.Add(new MxAnimationEventTimelineRow(
                        definition.SetId,
                        binding.BindingId,
                        binding.ActionKey,
                        eventIndex,
                        false,
                        binding.PresentationEvents[eventIndex]));
                }
            }

            rows.Sort(CompareRows);
            return rows;
        }

        private static int CompareRows(MxAnimationEventTimelineRow left, MxAnimationEventTimelineRow right)
        {
            int result = string.CompareOrdinal(left.BindingId, right.BindingId);
            if (result != 0)
                return result;

            result = string.CompareOrdinal(left.ActionKey, right.ActionKey);
            if (result != 0)
                return result;

            result = ((int)left.TimeDomain).CompareTo((int)right.TimeDomain);
            if (result != 0)
                return result;

            result = left.Time.CompareTo(right.Time);
            if (result != 0)
                return result;

            result = string.CompareOrdinal(left.EventId, right.EventId);
            if (result != 0)
                return result;

            return left.SourceOrder.CompareTo(right.SourceOrder);
        }
    }

    public static class MxAnimationSetDefinitionValidator
    {
        public static ResourceCatalogValidationReport Validate(
            MxAnimationSetDefinition definition,
            ResourceCatalog catalog = null,
            bool requireCatalog = true)
        {
            var report = new ResourceCatalogValidationReport();
            if (definition == null)
            {
                report.AddError("AnimationSetMissing", default, "Animation set definition is null.");
                return report;
            }

            if (string.IsNullOrWhiteSpace(definition.SetId))
                report.AddError("AnimationSetIdMissing", default, "Animation set id is missing.");
            else if (!ResourceKey.IsValidId(definition.SetId))
                report.AddError("AnimationSetIdInvalid", default, "Animation set id is invalid: " + definition.SetId + ".");

            if (definition.Version <= 0)
                report.AddError("AnimationSetVersionInvalid", default, "Animation set version must be positive.");

            string expectedHash = MxAnimationSetDefinitionHasher.ComputeHash(definition);
            if (!string.Equals(definition.DefinitionHash, expectedHash, StringComparison.Ordinal))
            {
                report.AddError(
                    "AnimationSetHashMismatch",
                    default,
                    "Animation set hash does not match its canonical mapping content.");
            }

            bool catalogMissingReported = false;
            ValidateClip(definition.DefaultClip, "DefaultClipMissing", "default clip", catalog, requireCatalog, report, ref catalogMissingReported);
            ValidateClip(definition.FallbackClip, "FallbackClipMissing", "fallback clip", catalog, requireCatalog, report, ref catalogMissingReported);
            ValidateLayers(definition, catalog, requireCatalog, report, ref catalogMissingReported);
            ValidateActions(definition, catalog, requireCatalog, report, ref catalogMissingReported);
            return report;
        }

        private static void ValidateLayers(
            MxAnimationSetDefinition definition,
            ResourceCatalog catalog,
            bool requireCatalog,
            ResourceCatalogValidationReport report,
            ref bool catalogMissingReported)
        {
            var layerIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < definition.Layers.Count; i++)
            {
                MxAnimationLayerDefinition layer = definition.Layers[i];
                if (layer == null)
                {
                    report.AddError("LayerDefinitionMissing", default, "Animation layer definition at index " + i + " is null.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(layer.LayerId.Value))
                    report.AddError("LayerIdMissing", layer.AvatarMaskKey, "Animation layer id is missing.");
                else if (!layerIds.Add(layer.LayerId.Value))
                    report.AddError("DuplicateLayerId", layer.AvatarMaskKey, "Duplicate animation layer id: " + layer.LayerId.Value + ".");

                if (layer.AvatarMaskKey.IsValid)
                    ValidateAvatarMask(layer.AvatarMaskKey, catalog, requireCatalog, report, ref catalogMissingReported);
            }
        }

        private static void ValidateActions(
            MxAnimationSetDefinition definition,
            ResourceCatalog catalog,
            bool requireCatalog,
            ResourceCatalogValidationReport report,
            ref bool catalogMissingReported)
        {
            var bindingIds = new HashSet<string>(StringComparer.Ordinal);
            var actionKeys = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < definition.Actions.Count; i++)
            {
                MxAnimationActionBinding binding = definition.Actions[i];
                if (binding == null)
                {
                    report.AddError("BindingMissing", default, "Animation binding at index " + i + " is null.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(binding.BindingId) && string.IsNullOrWhiteSpace(binding.ActionKey))
                    report.AddError("BindingIdentityMissing", binding.Clip, "Animation binding must provide a binding id or action key.");

                if (!string.IsNullOrWhiteSpace(binding.BindingId) && !bindingIds.Add(binding.BindingId))
                    report.AddError("DuplicateBindingId", binding.Clip, "Duplicate animation binding id: " + binding.BindingId + ".");

                if (!string.IsNullOrWhiteSpace(binding.ActionKey))
                {
                    if (!MxAnimationActionKey.IsValid(binding.ActionKey))
                        report.AddError("ActionKeyInvalid", binding.Clip, "Animation action key is invalid: " + binding.ActionKey + ".");
                    if (!actionKeys.Add(binding.ActionKey))
                        report.AddError("DuplicateActionKey", binding.Clip, "Duplicate animation action key: " + binding.ActionKey + ".");
                }

                ValidateClip(binding.Clip, "BindingClipMissing", "binding clip", catalog, requireCatalog, report, ref catalogMissingReported);
            }
        }

        private static void ValidateClip(
            ResourceKey key,
            string missingCode,
            string role,
            ResourceCatalog catalog,
            bool requireCatalog,
            ResourceCatalogValidationReport report,
            ref bool catalogMissingReported)
        {
            if (!key.IsValid)
            {
                report.AddError(missingCode, key, "Animation " + role + " is missing or invalid.");
                return;
            }

            if (!string.Equals(key.TypeId, ResourceTypeIds.AnimationClip, StringComparison.Ordinal))
            {
                report.AddError("ClipTypeMismatch", key, "Animation " + role + " must use typeId " + ResourceTypeIds.AnimationClip + ".");
                return;
            }

            if (catalog == null)
            {
                if (requireCatalog && !catalogMissingReported)
                {
                    report.AddError("CatalogMissing", key, "Resource catalog is required to validate animation clip keys.");
                    catalogMissingReported = true;
                }

                return;
            }

            if (TryFindExactCatalogEntry(catalog, key))
                return;

            if (TryFindCatalogEntryWithDifferentType(catalog, key, out ResourceCatalogEntry wrongTypeEntry))
                report.AddError("ClipCatalogTypeMismatch", key, "Catalog entry for animation clip key has typeId " + wrongTypeEntry.TypeId + ".");
            else
                report.AddError("ClipCatalogEntryMissing", key, "Catalog entry is missing for animation clip key: " + key + ".");
        }

        private static void ValidateAvatarMask(
            ResourceKey key,
            ResourceCatalog catalog,
            bool requireCatalog,
            ResourceCatalogValidationReport report,
            ref bool catalogMissingReported)
        {
            if (!string.Equals(key.TypeId, ResourceTypeIds.AvatarMask, StringComparison.Ordinal))
            {
                report.AddError("AvatarMaskTypeMismatch", key, "Animation layer AvatarMask must use typeId " + ResourceTypeIds.AvatarMask + ".");
                return;
            }

            if (catalog == null)
            {
                if (requireCatalog && !catalogMissingReported)
                {
                    report.AddError("CatalogMissing", key, "Resource catalog is required to validate animation resource keys.");
                    catalogMissingReported = true;
                }

                return;
            }

            if (TryFindExactCatalogEntry(catalog, key))
                return;

            if (TryFindCatalogEntryWithDifferentType(catalog, key, out ResourceCatalogEntry wrongTypeEntry))
                report.AddError("AvatarMaskCatalogTypeMismatch", key, "Catalog entry for AvatarMask key has typeId " + wrongTypeEntry.TypeId + ".");
            else
                report.AddError("AvatarMaskCatalogEntryMissing", key, "Catalog entry is missing for AvatarMask key: " + key + ".");
        }

        private static bool TryFindExactCatalogEntry(ResourceCatalog catalog, ResourceKey key)
        {
            for (int i = 0; i < catalog.Entries.Count; i++)
            {
                ResourceCatalogEntry candidate = catalog.Entries[i];
                if (candidate == null)
                    continue;

                ResourceKey candidateKey = candidate.CreateKey(catalog.PackageId);
                if (MatchesCatalogKey(candidateKey, key, requireTypeMatch: true))
                    return true;
            }

            return false;
        }

        private static bool TryFindCatalogEntryWithDifferentType(
            ResourceCatalog catalog,
            ResourceKey key,
            out ResourceCatalogEntry entry)
        {
            for (int i = 0; i < catalog.Entries.Count; i++)
            {
                ResourceCatalogEntry candidate = catalog.Entries[i];
                if (candidate == null)
                    continue;

                ResourceKey candidateKey = candidate.CreateKey(catalog.PackageId);
                if (!MatchesCatalogKey(candidateKey, key, requireTypeMatch: false))
                    continue;

                entry = candidate;
                return true;
            }

            entry = null;
            return false;
        }

        private static bool MatchesCatalogKey(ResourceKey catalogKey, ResourceKey requestedKey, bool requireTypeMatch)
        {
            if (!string.Equals(catalogKey.Id, requestedKey.Id, StringComparison.Ordinal))
                return false;
            if (!string.Equals(catalogKey.Variant, requestedKey.Variant, StringComparison.Ordinal))
                return false;
            if (!string.IsNullOrWhiteSpace(requestedKey.PackageId)
                && !string.Equals(catalogKey.PackageId, requestedKey.PackageId, StringComparison.Ordinal))
                return false;

            bool typeMatches = string.Equals(catalogKey.TypeId, requestedKey.TypeId, StringComparison.Ordinal);
            return requireTypeMatch ? typeMatches : !typeMatches;
        }
    }
}
