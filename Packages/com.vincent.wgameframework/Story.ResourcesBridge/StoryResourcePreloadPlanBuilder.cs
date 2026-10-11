using System;
using System.Collections.Generic;
using MxFramework.Resources;

namespace MxFramework.Story.ResourcesBridge
{
    public readonly struct StoryResourceKeyMetadata : IEquatable<StoryResourceKeyMetadata>
    {
        public StoryResourceKeyMetadata(string id, string typeId, string variant = "", string packageId = "")
        {
            Id = id ?? string.Empty;
            TypeId = typeId ?? string.Empty;
            Variant = variant ?? string.Empty;
            PackageId = packageId ?? string.Empty;
        }

        public string Id { get; }
        public string TypeId { get; }
        public string Variant { get; }
        public string PackageId { get; }

        public ResourceKey ToResourceKey()
        {
            return new ResourceKey(Id, TypeId, Variant, PackageId);
        }

        public bool Equals(StoryResourceKeyMetadata other)
        {
            return string.Equals(Id, other.Id, StringComparison.Ordinal)
                && string.Equals(TypeId, other.TypeId, StringComparison.Ordinal)
                && string.Equals(Variant, other.Variant, StringComparison.Ordinal)
                && string.Equals(PackageId, other.PackageId, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is StoryResourceKeyMetadata other && Equals(other);
        }

        public override int GetHashCode()
        {
            return ToResourceKey().GetHashCode();
        }
    }

    public sealed class StoryResourcePreloadMetadata
    {
        private readonly List<StoryResourceKeyMetadata> _explicitKeys;
        private readonly List<string> _labels;

        public StoryResourcePreloadMetadata(
            string groupId,
            IEnumerable<StoryResourceKeyMetadata> explicitKeys = null,
            IEnumerable<string> labels = null,
            bool failFast = false,
            int maxConcurrentLoads = 1)
        {
            GroupId = groupId ?? string.Empty;
            FailFast = failFast;
            MaxConcurrentLoads = maxConcurrentLoads < 1 ? 1 : maxConcurrentLoads;
            _explicitKeys = explicitKeys != null
                ? new List<StoryResourceKeyMetadata>(explicitKeys)
                : new List<StoryResourceKeyMetadata>();
            _labels = labels != null
                ? new List<string>(labels)
                : new List<string>();
        }

        public string GroupId { get; }
        public IReadOnlyList<StoryResourceKeyMetadata> ExplicitKeys => _explicitKeys;
        public IReadOnlyList<string> Labels => _labels;
        public bool FailFast { get; }
        public int MaxConcurrentLoads { get; }
    }

    public enum StoryResourcesBridgeDiagnosticCode
    {
        None = 0,
        MissingMetadata = 1,
        InvalidResourceKey = 2,
        InvalidLabel = 3
    }

    public readonly struct StoryResourcesBridgeDiagnostic
    {
        public StoryResourcesBridgeDiagnostic(
            StoryResourcesBridgeDiagnosticCode code,
            string message,
            StoryResourceKeyMetadata key = default,
            string label = "")
        {
            Code = code;
            Message = message ?? string.Empty;
            Key = key;
            Label = label ?? string.Empty;
        }

        public StoryResourcesBridgeDiagnosticCode Code { get; }
        public string Message { get; }
        public StoryResourceKeyMetadata Key { get; }
        public string Label { get; }
        public bool IsNone => Code == StoryResourcesBridgeDiagnosticCode.None;
    }

    public readonly struct StoryResourcePreloadPlanResult
    {
        public StoryResourcePreloadPlanResult(
            ResourcePreloadPlan plan,
            IReadOnlyList<StoryResourcesBridgeDiagnostic> diagnostics)
        {
            Plan = plan;
            Diagnostics = diagnostics ?? Array.Empty<StoryResourcesBridgeDiagnostic>();
        }

        public ResourcePreloadPlan Plan { get; }
        public IReadOnlyList<StoryResourcesBridgeDiagnostic> Diagnostics { get; }
        public bool Success => Plan != null && Diagnostics.Count == 0;
    }

    public static class StoryResourcePreloadPlanBuilder
    {
        public static StoryResourcePreloadPlanResult Build(StoryResourcePreloadMetadata metadata)
        {
            if (metadata == null)
            {
                return new StoryResourcePreloadPlanResult(
                    null,
                    new[]
                    {
                        new StoryResourcesBridgeDiagnostic(
                            StoryResourcesBridgeDiagnosticCode.MissingMetadata,
                            "Story resource preload metadata is missing.")
                    });
            }

            var diagnostics = new List<StoryResourcesBridgeDiagnostic>();
            List<ResourceKey> keys = BuildExplicitKeys(metadata, diagnostics);
            List<string> labels = BuildLabels(metadata, diagnostics);

            if (diagnostics.Count > 0)
            {
                return new StoryResourcePreloadPlanResult(null, diagnostics.ToArray());
            }

            return new StoryResourcePreloadPlanResult(
                new ResourcePreloadPlan(
                    metadata.GroupId,
                    keys,
                    labels,
                    metadata.FailFast,
                    metadata.MaxConcurrentLoads),
                Array.Empty<StoryResourcesBridgeDiagnostic>());
        }

        private static List<ResourceKey> BuildExplicitKeys(
            StoryResourcePreloadMetadata metadata,
            List<StoryResourcesBridgeDiagnostic> diagnostics)
        {
            var keys = new List<ResourceKey>();
            var unique = new HashSet<ResourceKey>();

            for (int i = 0; i < metadata.ExplicitKeys.Count; i++)
            {
                StoryResourceKeyMetadata source = metadata.ExplicitKeys[i];
                ResourceKey key = source.ToResourceKey();
                if (!key.IsValid)
                {
                    diagnostics.Add(new StoryResourcesBridgeDiagnostic(
                        StoryResourcesBridgeDiagnosticCode.InvalidResourceKey,
                        "Story resource preload key is invalid.",
                        source));
                    continue;
                }

                if (unique.Add(key))
                    keys.Add(key);
            }

            keys.Sort(ResourceKeyComparer.Instance);
            return keys;
        }

        private static List<string> BuildLabels(
            StoryResourcePreloadMetadata metadata,
            List<StoryResourcesBridgeDiagnostic> diagnostics)
        {
            var labels = new List<string>();
            var unique = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < metadata.Labels.Count; i++)
            {
                string label = metadata.Labels[i] ?? string.Empty;
                if (string.IsNullOrWhiteSpace(label))
                {
                    diagnostics.Add(new StoryResourcesBridgeDiagnostic(
                        StoryResourcesBridgeDiagnosticCode.InvalidLabel,
                        "Story resource preload label is empty.",
                        label: label));
                    continue;
                }

                if (unique.Add(label))
                    labels.Add(label);
            }

            labels.Sort(StringComparer.Ordinal);
            return labels;
        }

        private sealed class ResourceKeyComparer : IComparer<ResourceKey>
        {
            public static readonly ResourceKeyComparer Instance = new ResourceKeyComparer();

            public int Compare(ResourceKey x, ResourceKey y)
            {
                int package = string.CompareOrdinal(x.PackageId, y.PackageId);
                if (package != 0)
                    return package;

                int id = string.CompareOrdinal(x.Id, y.Id);
                if (id != 0)
                    return id;

                int type = string.CompareOrdinal(x.TypeId, y.TypeId);
                if (type != 0)
                    return type;

                return string.CompareOrdinal(x.Variant, y.Variant);
            }
        }
    }
}
