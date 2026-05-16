using System;
using System.Collections.Generic;
using System.Text;
using MxFramework.Animation;
using MxFramework.Resources;
using UnityEngine;

namespace MxFramework.Editor.Animation
{
    public sealed class MxAnimationClipRegistryExportResult
    {
        public MxAnimationClipRegistryExportResult(
            MxAnimationSetDefinition definition,
            ResourceCatalogValidationReport validationReport)
        {
            Definition = definition;
            ValidationReport = validationReport ?? new ResourceCatalogValidationReport();
        }

        public MxAnimationSetDefinition Definition { get; }
        public ResourceCatalogValidationReport ValidationReport { get; }
        public bool Success => !ValidationReport.HasErrors && Definition != null;
    }

    public static class MxAnimationClipRegistryExporter
    {
        public static MxAnimationClipRegistryExportResult Export(
            MxAnimationClipRegistryAsset asset,
            ResourceCatalog catalog = null)
        {
            var report = new ResourceCatalogValidationReport();
            if (asset == null)
            {
                report.AddError("ClipRegistryMissing", default, "Animation clip registry asset is missing.");
                return new MxAnimationClipRegistryExportResult(null, report);
            }

            Dictionary<string, ResourceKey> clipKeys = CreateClipKeyMap(asset, report);
            ResourceKey defaultClip = ResolveRoleClip(asset, clipKeys, true, report);
            ResourceKey fallbackClip = ResolveRoleClip(asset, clipKeys, false, report);
            MxAnimationActionBinding[] bindings = CreateBindings(asset, clipKeys, report);
            MxAnimationPresentationEvent[] events = CreateEvents(asset.Events);

            var definition = new MxAnimationSetDefinition(
                asset.AnimationSetId,
                asset.Version,
                defaultClip,
                fallbackClip,
                bindings,
                events);
            report.Merge(MxAnimationSetDefinitionValidator.Validate(definition, catalog));
            return new MxAnimationClipRegistryExportResult(definition, report);
        }

        public static string CreateReportText(MxAnimationClipRegistryExportResult result)
        {
            var builder = new StringBuilder();
            builder.Append("MxAnimation Clip Registry Export Report\n");
            builder.Append("success: ").Append(result != null && result.Success ? "true" : "false").Append('\n');
            builder.Append("setId: ").Append(result != null && result.Definition != null ? result.Definition.SetId : string.Empty).Append('\n');
            builder.Append("version: ").Append(result != null && result.Definition != null ? result.Definition.Version : 0).Append('\n');
            builder.Append("hash: ").Append(result != null && result.Definition != null ? result.Definition.DefinitionHash : string.Empty).Append('\n');
            builder.Append("errors: ").Append(result != null ? result.ValidationReport.ErrorCount : 0).Append('\n');
            builder.Append("warnings: ").Append(result != null ? result.ValidationReport.WarningCount : 0).Append('\n');
            builder.Append("issues:\n");

            if (result == null || result.ValidationReport.Issues.Count == 0)
            {
                builder.Append("- none\n");
                return builder.ToString();
            }

            for (int i = 0; i < result.ValidationReport.Issues.Count; i++)
            {
                ResourceCatalogValidationIssue issue = result.ValidationReport.Issues[i];
                builder.Append("- ")
                    .Append(issue.Severity)
                    .Append(' ')
                    .Append(issue.Code)
                    .Append(" key=")
                    .Append(issue.Key)
                    .Append(" message=")
                    .Append(issue.Message)
                    .Append('\n');
            }

            return builder.ToString();
        }

        private static Dictionary<string, ResourceKey> CreateClipKeyMap(
            MxAnimationClipRegistryAsset asset,
            ResourceCatalogValidationReport report)
        {
            var keys = new Dictionary<string, ResourceKey>(StringComparer.Ordinal);
            var seen = new HashSet<string>(StringComparer.Ordinal);

            MxAnimationClipRegistryClipEntry[] clips = asset.Clips;
            for (int i = 0; i < clips.Length; i++)
            {
                MxAnimationClipRegistryClipEntry clip = clips[i];
                ResourceKey key = clip.CreateResourceKey(asset.PackageId);

                if (string.IsNullOrWhiteSpace(clip.ClipId))
                {
                    report.AddError("ClipIdMissing", key, "Clip registry entry at index " + i + " is missing a clip id.");
                    continue;
                }

                if (!seen.Add(clip.ClipId))
                {
                    report.AddError("DuplicateClipId", key, "Duplicate clip registry id: " + clip.ClipId + ".");
                    continue;
                }

                if (clip.Clip == null)
                    report.AddError("ClipReferenceMissing", key, "Clip registry entry is missing an AnimationClip reference: " + clip.ClipId + ".");

                if (!key.IsValid)
                    report.AddError("ClipResourceKeyInvalid", key, "Clip registry entry has an invalid ResourceKey: " + clip.ClipId + ".");

                keys[clip.ClipId] = key;
            }

            return keys;
        }

        private static ResourceKey ResolveRoleClip(
            MxAnimationClipRegistryAsset asset,
            IReadOnlyDictionary<string, ResourceKey> clipKeys,
            bool resolveDefault,
            ResourceCatalogValidationReport report)
        {
            ResourceKey resolved = default;
            bool found = false;
            MxAnimationClipRegistryClipEntry[] clips = asset.Clips;
            for (int i = 0; i < clips.Length; i++)
            {
                MxAnimationClipRegistryClipEntry clip = clips[i];
                bool roleMatches = resolveDefault ? clip.IsDefault : clip.IsFallback;
                if (!roleMatches)
                    continue;

                if (found)
                {
                    report.AddError(
                        resolveDefault ? "DuplicateDefaultClip" : "DuplicateFallbackClip",
                        clip.CreateResourceKey(asset.PackageId),
                        resolveDefault ? "Multiple default clips are marked." : "Multiple fallback clips are marked.");
                    continue;
                }

                if (clipKeys.TryGetValue(clip.ClipId, out ResourceKey key))
                    resolved = key;
                found = true;
            }

            return resolved;
        }

        private static MxAnimationActionBinding[] CreateBindings(
            MxAnimationClipRegistryAsset asset,
            IReadOnlyDictionary<string, ResourceKey> clipKeys,
            ResourceCatalogValidationReport report)
        {
            MxAnimationClipRegistryBindingEntry[] source = asset.Bindings;
            var bindings = new MxAnimationActionBinding[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                MxAnimationClipRegistryBindingEntry binding = source[i];
                if (!clipKeys.TryGetValue(binding.ClipId, out ResourceKey clipKey))
                    report.AddError("BindingClipReferenceMissing", default, "Binding references an unknown clip id: " + binding.ClipId + ".");

                string actionKey = binding.ResolveActionKey();
                bindings[i] = new MxAnimationActionBinding(
                    binding.BindingId,
                    actionKey,
                    clipKey,
                    new MxAnimationLayerId(binding.LayerId),
                    binding.PlaybackSpeed,
                    binding.Loop,
                    binding.AlignmentPolicy,
                    CreateEvents(binding.Events),
                    binding.FadeDurationSeconds);
            }

            return bindings;
        }

        private static MxAnimationPresentationEvent[] CreateEvents(
            MxAnimationClipRegistryEventEntry[] source)
        {
            if (source == null || source.Length == 0)
                return Array.Empty<MxAnimationPresentationEvent>();

            var events = new MxAnimationPresentationEvent[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                MxAnimationClipRegistryEventEntry item = source[i];
                events[i] = new MxAnimationPresentationEvent(
                    item.EventId,
                    item.TimeDomain,
                    item.Time,
                    item.EventKind,
                    item.CreatePayloadKey(),
                    item.Socket,
                    item.Tag);
            }

            return events;
        }
    }
}
