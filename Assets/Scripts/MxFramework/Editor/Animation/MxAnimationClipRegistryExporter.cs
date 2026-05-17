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
            return Export(asset, catalog, requireCatalog: true);
        }

        public static MxAnimationClipRegistryExportResult ExportStructureOnly(
            MxAnimationClipRegistryAsset asset)
        {
            return Export(asset, null, requireCatalog: false);
        }

        private static MxAnimationClipRegistryExportResult Export(
            MxAnimationClipRegistryAsset asset,
            ResourceCatalog catalog,
            bool requireCatalog)
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
            MxAnimationLayerDefinition[] layers = CreateLayers(asset, report);
            MxAnimationActionBinding[] bindings = CreateBindings(asset, clipKeys, report);
            MxAnimationBlend1DDefinition[] blend1DDefinitions = CreateBlend1DDefinitions(asset, clipKeys, report);
            MxAnimationBlend2DDefinition[] blend2DDefinitions = CreateBlend2DDefinitions(asset, clipKeys, report);
            MxAnimationPresentationEvent[] events = CreateEvents(asset.Events);

            var definition = new MxAnimationSetDefinition(
                asset.AnimationSetId,
                asset.Version,
                defaultClip,
                fallbackClip,
                bindings,
                events,
                layers: layers,
                blend1DDefinitions: blend1DDefinitions,
                blend2DDefinitions: blend2DDefinitions);
            report.Merge(MxAnimationSetDefinitionValidator.Validate(definition, catalog, requireCatalog));
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

        private static MxAnimationLayerDefinition[] CreateLayers(
            MxAnimationClipRegistryAsset asset,
            ResourceCatalogValidationReport report)
        {
            MxAnimationClipRegistryLayerEntry[] source = asset.Layers;
            var layers = new MxAnimationLayerDefinition[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                MxAnimationClipRegistryLayerEntry layer = source[i];
                ResourceKey avatarMaskKey = layer.CreateAvatarMaskKey(asset.PackageId);
                if (string.IsNullOrWhiteSpace(layer.LayerId))
                    report.AddError("LayerIdMissing", avatarMaskKey, "Layer registry entry at index " + i + " is missing a layer id.");
                if (avatarMaskKey.IsValid && layer.AvatarMask == null)
                    report.AddError("AvatarMaskReferenceMissing", avatarMaskKey, "Layer registry entry is missing an AvatarMask reference: " + layer.LayerId + ".");

                layers[i] = new MxAnimationLayerDefinition(
                    new MxAnimationLayerId(layer.LayerId),
                    layer.ProfileId,
                    layer.DefaultWeight,
                    layer.BlendMode,
                    avatarMaskKey);
            }

            return layers;
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

        private static MxAnimationBlend1DDefinition[] CreateBlend1DDefinitions(
            MxAnimationClipRegistryAsset asset,
            IReadOnlyDictionary<string, ResourceKey> clipKeys,
            ResourceCatalogValidationReport report)
        {
            MxAnimationClipRegistryBlend1DEntry[] source = asset.Blend1DDefinitions;
            var definitions = new MxAnimationBlend1DDefinition[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                MxAnimationClipRegistryBlend1DEntry blend = source[i];
                MxAnimationClipRegistryBlend1DPointEntry[] sourcePoints = blend.Points;
                var points = new MxAnimationBlend1DPoint[sourcePoints.Length];
                for (int pointIndex = 0; pointIndex < sourcePoints.Length; pointIndex++)
                {
                    MxAnimationClipRegistryBlend1DPointEntry point = sourcePoints[pointIndex];
                    if (!clipKeys.TryGetValue(point.ClipId, out ResourceKey clipKey))
                    {
                        report.AddError(
                            "Blend1DClipReferenceMissing",
                            default,
                            "1D blend references an unknown clip id: " + point.ClipId + ".");
                    }

                    points[pointIndex] = new MxAnimationBlend1DPoint(
                        point.Threshold,
                        clipKey,
                        point.PlaybackSpeed,
                        point.Loop);
                }

                definitions[i] = new MxAnimationBlend1DDefinition(
                    blend.BlendId,
                    blend.ParameterId,
                    new MxAnimationLayerId(blend.LayerId),
                    points,
                    blend.ParameterScale,
                    blend.FadeDurationSeconds);
            }

            return definitions;
        }

        private static MxAnimationBlend2DDefinition[] CreateBlend2DDefinitions(
            MxAnimationClipRegistryAsset asset,
            IReadOnlyDictionary<string, ResourceKey> clipKeys,
            ResourceCatalogValidationReport report)
        {
            MxAnimationClipRegistryBlend2DEntry[] source = asset.Blend2DDefinitions;
            var definitions = new MxAnimationBlend2DDefinition[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                MxAnimationClipRegistryBlend2DEntry blend = source[i];
                MxAnimationClipRegistryBlend2DPointEntry[] sourcePoints = blend.Points;
                var points = new MxAnimationBlend2DPoint[sourcePoints.Length];
                for (int pointIndex = 0; pointIndex < sourcePoints.Length; pointIndex++)
                {
                    MxAnimationClipRegistryBlend2DPointEntry point = sourcePoints[pointIndex];
                    if (!clipKeys.TryGetValue(point.ClipId, out ResourceKey clipKey))
                    {
                        report.AddError(
                            "Blend2DClipReferenceMissing",
                            default,
                            "2D blend references an unknown clip id: " + point.ClipId + ".");
                    }

                    points[pointIndex] = new MxAnimationBlend2DPoint(
                        point.X,
                        point.Y,
                        clipKey,
                        point.PlaybackSpeed,
                        point.Loop);
                }

                definitions[i] = new MxAnimationBlend2DDefinition(
                    blend.BlendId,
                    blend.ParameterXId,
                    blend.ParameterYId,
                    new MxAnimationLayerId(blend.LayerId),
                    points,
                    blend.ParameterXScale,
                    blend.ParameterYScale,
                    blend.FadeDurationSeconds);
            }

            return definitions;
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
                    item.Tag,
                    item.ReplayPolicy);
            }

            return events;
        }
    }
}
