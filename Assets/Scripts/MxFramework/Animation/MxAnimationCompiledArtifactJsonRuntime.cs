using System;
using System.Collections.Generic;
using System.Globalization;
using MxFramework.Resources;
using Newtonsoft.Json.Linq;

namespace MxFramework.Animation
{
    public sealed class MxAnimationCompiledArtifactJsonException : Exception
    {
        public MxAnimationCompiledArtifactJsonException(string message)
            : base(message)
        {
        }
    }

    public static class MxAnimationCompiledArtifactJson
    {
        public const string AnimationSetDefinitionFormat = "mx.animationSetDefinition.v1";
        public const string AnimationClipRegistryFormat = "mx.animationClipRegistry.v1";

        public static IReadOnlyList<MxAnimationSetDefinition> LoadSetDefinitions(string json, string fallbackPackageId = "")
        {
            JObject root = ParseRoot(json, "animation set definition");
            RequireFormat(root, AnimationSetDefinitionFormat, "animation set definition");

            string packageId = FirstNonEmpty(fallbackPackageId, ReadString(root, "packageId"));
            JArray sets = root["sets"] as JArray;
            var definitions = new List<MxAnimationSetDefinition>();
            for (int setIndex = 0; sets != null && setIndex < sets.Count; setIndex++)
            {
                JObject set = sets[setIndex] as JObject;
                if (set == null)
                    continue;

                definitions.Add(ParseSetDefinition(set, packageId));
            }

            return definitions;
        }

        public static IMxAnimationMappingProvider LoadMappingProvider(string json, string fallbackPackageId = "")
        {
            return new MxAnimationStaticMappingProvider(LoadSetDefinitions(json, fallbackPackageId));
        }

        public static MxAnimationClipRegistry LoadClipRegistry(
            string json,
            ResourceCatalog catalog = null,
            string fallbackPackageId = "")
        {
            JObject root = ParseRoot(json, "animation clip registry");
            RequireFormat(root, AnimationClipRegistryFormat, "animation clip registry");

            string packageId = FirstNonEmpty(catalog != null ? catalog.PackageId : string.Empty, fallbackPackageId, ReadString(root, "packageId"));
            JArray clips = root["clips"] as JArray;
            var entries = new List<MxAnimationClipRegistryEntry>();
            for (int clipIndex = 0; clips != null && clipIndex < clips.Count; clipIndex++)
            {
                JObject clip = clips[clipIndex] as JObject;
                if (clip == null)
                    continue;

                ResourceKey key = CreateAnimationClipKey(ReadString(clip, "runtimeResourceKey"), packageId);
                if (!key.IsValid)
                    continue;

                entries.Add(new MxAnimationClipRegistryEntry(key, FindCatalogHash(catalog, key)));
            }

            return new MxAnimationClipRegistry(1, catalog != null ? catalog.CatalogId : string.Empty, string.Empty, entries);
        }

        private static MxAnimationSetDefinition ParseSetDefinition(JObject set, string packageId)
        {
            var clipsById = new Dictionary<string, ClipRuntimeInfo>(StringComparer.Ordinal);
            var timelinesById = new Dictionary<string, List<MxAnimationPresentationEvent>>(StringComparer.Ordinal);
            var layers = new List<MxAnimationLayerDefinition>();
            var blend1D = new List<MxAnimationBlend1DDefinition>();
            var blend2D = new List<MxAnimationBlend2DDefinition>();

            JArray layerDocs = set["layers"] as JArray;
            for (int i = 0; layerDocs != null && i < layerDocs.Count; i++)
            {
                JObject layer = layerDocs[i] as JObject;
                if (layer == null)
                    continue;

                layers.Add(new MxAnimationLayerDefinition(
                    new MxAnimationLayerId(ReadString(layer, "layerId")),
                    ReadString(layer, "purpose"),
                    ReadFloat(layer, "weight", 1f),
                    ReadBool(layer, "additive") ? MxAnimationLayerBlendMode.Additive : MxAnimationLayerBlendMode.Override,
                    CreateSelectionKey(layer["avatarMaskSelection"] as JObject, ResourceTypeIds.AvatarMask, packageId)));
            }

            JArray groups = set["groups"] as JArray;
            for (int groupIndex = 0; groups != null && groupIndex < groups.Count; groupIndex++)
            {
                JObject group = groups[groupIndex] as JObject;
                if (group == null)
                    continue;

                ReadClips(group["clips"] as JArray, packageId, clipsById);
            }

            for (int groupIndex = 0; groups != null && groupIndex < groups.Count; groupIndex++)
            {
                JObject group = groups[groupIndex] as JObject;
                if (group == null)
                    continue;

                ReadTimelines(group["timelines"] as JArray, packageId, clipsById, timelinesById);
                ReadBlend1D(group["blend1D"] as JArray, clipsById, blend1D);
                ReadBlend2D(group["blend2D"] as JArray, clipsById, blend2D);
            }

            var actions = new List<MxAnimationActionBinding>();
            JArray bindings = set["actionBindings"] as JArray;
            for (int bindingIndex = 0; bindings != null && bindingIndex < bindings.Count; bindingIndex++)
            {
                JObject binding = bindings[bindingIndex] as JObject;
                if (binding == null)
                    continue;

                string clipId = ReadString(binding, "clipId");
                string blendId = ReadString(binding, "blendId");
                if (string.IsNullOrWhiteSpace(clipId) && !string.IsNullOrWhiteSpace(blendId))
                    continue;

                ClipRuntimeInfo clip = ResolveClip(clipId, clipsById);
                List<MxAnimationPresentationEvent> events = null;
                string timelineId = ReadString(binding, "timelineId");
                if (!string.IsNullOrWhiteSpace(timelineId))
                    timelinesById.TryGetValue(timelineId, out events);

                actions.Add(new MxAnimationActionBinding(
                    ReadString(binding, "bindingId"),
                    ReadString(binding, "actionId"),
                    clip.Key,
                    MxAnimationLayerId.Base,
                    clip.Speed,
                    clip.Loop,
                    MxAnimationAlignmentPolicy.StartAtZero,
                    events,
                    0.15f));
            }

            ResourceKey defaultClip = ResolveClip(ReadString(set, "defaultClipId"), clipsById).Key;
            ResourceKey fallbackClip = ResolveClip(ReadString(set, "fallbackClipId"), clipsById).Key;
            return new MxAnimationSetDefinition(
                ReadString(set, "setId"),
                ParseVersion(ReadString(set, "version")),
                defaultClip,
                fallbackClip,
                actions,
                null,
                string.Empty,
                layers,
                BuildWarmup(defaultClip, fallbackClip, actions, layers),
                blend1D,
                blend2D);
        }

        private static void ReadClips(JArray clips, string packageId, Dictionary<string, ClipRuntimeInfo> clipsById)
        {
            for (int clipIndex = 0; clips != null && clipIndex < clips.Count; clipIndex++)
            {
                JObject clip = clips[clipIndex] as JObject;
                if (clip == null)
                    continue;

                string clipId = ReadString(clip, "clipId");
                if (string.IsNullOrWhiteSpace(clipId))
                    continue;

                clipsById[clipId] = new ClipRuntimeInfo(
                    CreateAnimationClipKey(ReadString(clip, "runtimeResourceKey"), packageId),
                    ReadFloat(clip, "speed", 1f),
                    ReadBool(clip, "loop"));
            }
        }

        private static void ReadBlend1D(
            JArray blends,
            Dictionary<string, ClipRuntimeInfo> clipsById,
            List<MxAnimationBlend1DDefinition> results)
        {
            for (int blendIndex = 0; blends != null && blendIndex < blends.Count; blendIndex++)
            {
                JObject blend = blends[blendIndex] as JObject;
                if (blend == null)
                    continue;

                var points = new List<MxAnimationBlend1DPoint>();
                JArray sourcePoints = blend["points"] as JArray;
                for (int pointIndex = 0; sourcePoints != null && pointIndex < sourcePoints.Count; pointIndex++)
                {
                    JObject point = sourcePoints[pointIndex] as JObject;
                    if (point == null)
                        continue;

                    ClipRuntimeInfo clip = ResolveClip(ReadString(point, "clipId"), clipsById);
                    points.Add(new MxAnimationBlend1DPoint(
                        Quantize(ReadFloat(point, "value", 0f)),
                        clip.Key,
                        clip.Speed,
                        clip.Loop));
                }

                results.Add(new MxAnimationBlend1DDefinition(
                    ReadString(blend, "blendId"),
                    ReadString(blend, "parameter"),
                    MxAnimationLayerId.Base,
                    points));
            }
        }

        private static void ReadBlend2D(
            JArray blends,
            Dictionary<string, ClipRuntimeInfo> clipsById,
            List<MxAnimationBlend2DDefinition> results)
        {
            for (int blendIndex = 0; blends != null && blendIndex < blends.Count; blendIndex++)
            {
                JObject blend = blends[blendIndex] as JObject;
                if (blend == null)
                    continue;

                var points = new List<MxAnimationBlend2DPoint>();
                JArray sourcePoints = blend["points"] as JArray;
                for (int pointIndex = 0; sourcePoints != null && pointIndex < sourcePoints.Count; pointIndex++)
                {
                    JObject point = sourcePoints[pointIndex] as JObject;
                    if (point == null)
                        continue;

                    ClipRuntimeInfo clip = ResolveClip(ReadString(point, "clipId"), clipsById);
                    points.Add(new MxAnimationBlend2DPoint(
                        Quantize(ReadFloat(point, "x", 0f)),
                        Quantize(ReadFloat(point, "y", 0f)),
                        clip.Key,
                        clip.Speed,
                        clip.Loop));
                }

                results.Add(new MxAnimationBlend2DDefinition(
                    ReadString(blend, "blendId"),
                    ReadString(blend, "xParameter"),
                    ReadString(blend, "yParameter"),
                    MxAnimationLayerId.Base,
                    points));
            }
        }

        private static void ReadTimelines(
            JArray timelines,
            string packageId,
            Dictionary<string, ClipRuntimeInfo> clipsById,
            Dictionary<string, List<MxAnimationPresentationEvent>> timelinesById)
        {
            for (int timelineIndex = 0; timelines != null && timelineIndex < timelines.Count; timelineIndex++)
            {
                JObject timeline = timelines[timelineIndex] as JObject;
                if (timeline == null)
                    continue;

                string timelineId = ReadString(timeline, "timelineId");
                if (string.IsNullOrWhiteSpace(timelineId))
                    continue;

                var events = new List<MxAnimationPresentationEvent>();
                MxAnimationEventTimeDomain timelineDomain = ParseTimeDomain(ReadString(timeline, "timeDomain"));
                string timelineClipId = ReadString(timeline, "clipId");
                JArray sourceEvents = timeline["events"] as JArray;
                for (int eventIndex = 0; sourceEvents != null && eventIndex < sourceEvents.Count; eventIndex++)
                {
                    JObject sourceEvent = sourceEvents[eventIndex] as JObject;
                    if (sourceEvent == null)
                        continue;

                    ResourceKey payloadKey = CreateSelectionKey(sourceEvent["resourceSelection"] as JObject, ResourceTypeIds.Object, packageId);
                    string clipId = FirstNonEmpty(ReadString(sourceEvent, "clipId"), timelineClipId);
                    ClipRuntimeInfo clip = ResolveClip(clipId, clipsById);
                    if (!payloadKey.IsValid)
                        payloadKey = clip.Key;

                    events.Add(new MxAnimationPresentationEvent(
                        ReadString(sourceEvent, "eventId"),
                        ParseTimeDomain(FirstNonEmpty(ReadString(sourceEvent, "timeDomain"), timelineDomain.ToString())),
                        ReadFloat(sourceEvent, "time", 0f),
                        ReadString(sourceEvent, "eventKind"),
                        payloadKey,
                        ReadMetadata(sourceEvent, "socket"),
                        ReadMetadata(sourceEvent, "tag")));
                }

                timelinesById[timelineId] = events;
            }
        }

        private static MxAnimationWarmupDefinition BuildWarmup(
            ResourceKey defaultClip,
            ResourceKey fallbackClip,
            IReadOnlyList<MxAnimationActionBinding> actions,
            IReadOnlyList<MxAnimationLayerDefinition> layers)
        {
            var keys = new List<ResourceKey>();
            AddKey(defaultClip, keys);
            AddKey(fallbackClip, keys);
            for (int i = 0; actions != null && i < actions.Count; i++)
                AddKey(actions[i].Clip, keys);
            for (int i = 0; layers != null && i < layers.Count; i++)
                AddKey(layers[i].AvatarMaskKey, keys);

            return new MxAnimationWarmupDefinition("compiled", keys);
        }

        private static void AddKey(ResourceKey key, List<ResourceKey> keys)
        {
            if (!key.IsValid || keys.Contains(key))
                return;
            keys.Add(key);
        }

        private static ClipRuntimeInfo ResolveClip(string clipId, Dictionary<string, ClipRuntimeInfo> clipsById)
        {
            if (!string.IsNullOrWhiteSpace(clipId) && clipsById.TryGetValue(clipId, out ClipRuntimeInfo clip))
                return clip;
            return default;
        }

        private static ResourceKey CreateAnimationClipKey(string resourceKey, string packageId)
        {
            return string.IsNullOrWhiteSpace(resourceKey)
                ? default
                : new ResourceKey(resourceKey, ResourceTypeIds.AnimationClip, string.Empty, packageId);
        }

        private static ResourceKey CreateSelectionKey(JObject selection, string fallbackTypeId, string packageId)
        {
            if (selection == null)
                return default;

            string key = FirstNonEmpty(
                ReadString(selection, "runtimeResourceKey"),
                ReadString(selection, "providerResourceKey"),
                ReadString(selection, "packageResourceKey"));
            if (string.IsNullOrWhiteSpace(key))
                return default;

            string expectedKind = ReadString(selection, "expectedKind");
            string typeId = string.Equals(expectedKind, "avatarMask", StringComparison.OrdinalIgnoreCase)
                ? ResourceTypeIds.AvatarMask
                : fallbackTypeId;
            return new ResourceKey(key, typeId, string.Empty, packageId);
        }

        private static string FindCatalogHash(ResourceCatalog catalog, ResourceKey key)
        {
            if (catalog == null || !key.IsValid)
                return string.Empty;

            for (int i = 0; i < catalog.Entries.Count; i++)
            {
                ResourceCatalogEntry entry = catalog.Entries[i];
                if (entry == null)
                    continue;
                if (string.Equals(entry.Id, key.Id, StringComparison.Ordinal)
                    && string.Equals(entry.TypeId, key.TypeId, StringComparison.Ordinal)
                    && (string.IsNullOrWhiteSpace(key.Variant) || string.Equals(entry.Variant, key.Variant, StringComparison.Ordinal))
                    && (string.IsNullOrWhiteSpace(key.PackageId) || string.IsNullOrWhiteSpace(entry.PackageId) || string.Equals(entry.PackageId, key.PackageId, StringComparison.Ordinal)))
                {
                    return entry.Hash;
                }
            }

            return string.Empty;
        }

        private static JObject ParseRoot(string json, string documentName)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new MxAnimationCompiledArtifactJsonException("Missing " + documentName + " JSON.");

            try
            {
                JObject root = JObject.Parse(json);
                if (root == null)
                    throw new MxAnimationCompiledArtifactJsonException("Invalid " + documentName + " JSON.");
                return root;
            }
            catch (Exception ex) when (!(ex is MxAnimationCompiledArtifactJsonException))
            {
                throw new MxAnimationCompiledArtifactJsonException("Invalid " + documentName + " JSON: " + ex.Message);
            }
        }

        private static void RequireFormat(JObject root, string expected, string documentName)
        {
            string actual = ReadString(root, "format");
            if (!string.Equals(actual, expected, StringComparison.Ordinal))
                throw new MxAnimationCompiledArtifactJsonException("Unsupported " + documentName + " format: " + actual + ".");
        }

        private static MxAnimationEventTimeDomain ParseTimeDomain(string value)
        {
            if (Enum.TryParse(value, ignoreCase: true, out MxAnimationEventTimeDomain result))
                return result;
            return MxAnimationEventTimeDomain.Seconds;
        }

        private static int ParseVersion(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return 1;

            int dot = value.IndexOf('.');
            string major = dot >= 0 ? value.Substring(0, dot) : value;
            return int.TryParse(major, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) && parsed > 0
                ? parsed
                : 1;
        }

        private static int Quantize(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                return 0;
            return (int)Math.Round(value * 1000f, MidpointRounding.AwayFromZero);
        }

        private static string ReadMetadata(JObject fields, string key)
        {
            JObject metadata = fields != null ? fields["metadata"] as JObject : null;
            return ReadString(metadata, key);
        }

        private static string ReadString(JObject fields, string name)
        {
            JToken token = fields != null ? fields[name] : null;
            return token == null || token.Type == JTokenType.Null ? string.Empty : token.ToString();
        }

        private static float ReadFloat(JObject fields, string name, float fallback)
        {
            string value = ReadString(fields, name);
            return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed)
                ? parsed
                : fallback;
        }

        private static bool ReadBool(JObject fields, string name)
        {
            string value = ReadString(fields, name);
            return bool.TryParse(value, out bool parsed) && parsed;
        }

        private static string FirstNonEmpty(params string[] values)
        {
            for (int i = 0; values != null && i < values.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(values[i]))
                    return values[i];
            }

            return string.Empty;
        }

        private readonly struct ClipRuntimeInfo
        {
            public ClipRuntimeInfo(ResourceKey key, float speed, bool loop)
            {
                Key = key;
                Speed = Math.Abs(speed) < 0.0001f ? 1f : speed;
                Loop = loop;
            }

            public ResourceKey Key { get; }
            public float Speed { get; }
            public bool Loop { get; }
        }
    }
}
