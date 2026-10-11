using System;
using System.Collections.Generic;
using MxFramework.CharacterAction;
using MxFramework.Animation;
using MxFramework.Resources;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MxFramework.CharacterRuntimeSpawn
{
    /// <summary>
    /// LowFreqAlloc: JSON contract for package-local weapon trace bake catalogs generated during import/editor bake.
    /// </summary>
    public static class CharacterWeaponTraceBakeCatalogJson
    {
        private static int ReadSocketIdentity(JObject value)
        {
            JToken token = value?["socketId"];
            if (token == null || token.Type != JTokenType.Integer || token.Value<long>() <= 0 || token.Value<long>() > int.MaxValue)
                throw new System.IO.InvalidDataException("Baked frame requires a positive authored numeric socket identity.");
            return token.Value<int>();
        }

        private static int ReadTraceIdentity(JObject value)
        {
            JToken token = value?["traceId"];
            if (token == null || token.Type != JTokenType.Integer || token.Value<long>() <= 0 || token.Value<long>() > int.MaxValue)
                throw new System.IO.InvalidDataException("Trace bake entry requires a positive authored numeric trace identity.");
            return token.Value<int>();
        }

        public const string Format = "mx.character.weaponTraceBakeCatalog.v2";
        public const string DefaultRelativePath = "generated/weapon_trace_bake_catalog.json";
        public const string IssueCatalogMissing = "WeaponTraceBakeGeneratedCatalogMissing";
        public const string IssueCatalogInvalid = "WeaponTraceBakeGeneratedCatalogInvalid";

        public static string Serialize(
            CharacterImportedPackage package,
            IReadOnlyList<CharacterWeaponTraceBakeRequest> requests,
            CharacterWeaponTraceBakeCatalog catalog)
        {
            var root = new JObject
            {
                ["format"] = Format,
                ["packageId"] = package?.PackageId ?? string.Empty,
                ["artifacts"] = CreateArtifactsArray(requests, catalog)
            };
            return root.ToString(Formatting.Indented) + Environment.NewLine;
        }

        public static CharacterWeaponTraceBakeCatalog Load(
            string json,
            string sourcePath,
            out CharacterWeaponTraceBakeBuildReport report)
        {
            var catalog = new CharacterWeaponTraceBakeCatalog();
            var issues = new List<CharacterWeaponTraceBakeBuildIssue>();
            if (string.IsNullOrWhiteSpace(json))
            {
                issues.Add(CreateIssue(
                    IssueCatalogMissing,
                    "Generated weapon trace bake catalog is empty or missing.",
                    sourcePath,
                    "catalog",
                    string.Empty,
                    string.Empty,
                    0,
                    -1));
                report = new CharacterWeaponTraceBakeBuildReport(0, 0, 0, issues.ToArray());
                return catalog;
            }

            JObject root;
            try
            {
                root = JObject.Parse(json);
            }
            catch (JsonException ex)
            {
                issues.Add(CreateIssue(
                    IssueCatalogInvalid,
                    "Generated weapon trace bake catalog JSON is invalid: " + ex.Message,
                    sourcePath,
                    "catalog",
                    string.Empty,
                    string.Empty,
                    0,
                    -1));
                report = new CharacterWeaponTraceBakeBuildReport(0, 0, 0, issues.ToArray());
                return catalog;
            }

            string format = ReadString(root, "format");
            if (!string.Equals(format, Format, StringComparison.Ordinal))
            {
                issues.Add(CreateIssue(
                    IssueCatalogInvalid,
                    "Generated weapon trace bake catalog format is not supported.",
                    sourcePath,
                    "format",
                    string.Empty,
                    string.Empty,
                    0,
                    -1));
                report = new CharacterWeaponTraceBakeBuildReport(0, 0, 0, issues.ToArray());
                return catalog;
            }

            int artifactCount = 0;
            int frameCount = 0;
            JArray artifacts = root["artifacts"] as JArray;
            for (int i = 0; artifacts != null && i < artifacts.Count; i++)
            {
                JObject artifactRoot = artifacts[i] as JObject;
                if (artifactRoot == null)
                    continue;

                string clipKey = ReadString(artifactRoot, "clipKey");
                int traceId = ReadTraceIdentity(artifactRoot);
                if (string.IsNullOrWhiteSpace(clipKey) || (traceId <= 0))
                {
                    issues.Add(CreateIssue(
                        IssueCatalogInvalid,
                        "Generated weapon trace bake artifact is missing clipKey or traceId.",
                        sourcePath,
                        "artifacts[" + i.ToString(System.Globalization.CultureInfo.InvariantCulture) + "]",
                        string.Empty,
                        clipKey,
                        traceId,
                        -1));
                    continue;
                }

                MxAnimationBakeProfile profile = ReadProfile(
                    artifactRoot["profile"] as JObject,
                    clipKey,
                    traceId);
                List<MxAnimationBakedWeaponTraceFrame> frames = ReadFrames(
                    artifactRoot["frames"] as JArray,
                    sourcePath,
                    i,
                    traceId,
                    issues);
                List<MxAnimationBakedSocketFrame> socketFrames = ReadSocketFrames(
                    artifactRoot["socketFrames"] as JArray,
                    sourcePath,
                    i,
                    clipKey,
                    traceId,
                    issues);
                if (frames.Count == 0)
                    continue;

                catalog.RegisterArtifact(
                    clipKey,
                    traceId,
                    new MxAnimationBakeArtifact(
                        profile,
                        frames,
                        rootMotionFrames: null,
                        eventMarkers: null,
                        artifactHash: ReadString(artifactRoot, "artifactHash"),
                        socketFrames: socketFrames));
                artifactCount++;
                frameCount += frames.Count;
            }

            report = new CharacterWeaponTraceBakeBuildReport(
                artifacts != null ? artifacts.Count : 0,
                artifactCount,
                frameCount,
                issues.ToArray());
            return catalog;
        }

        private static JArray CreateArtifactsArray(
            IReadOnlyList<CharacterWeaponTraceBakeRequest> requests,
            CharacterWeaponTraceBakeCatalog catalog)
        {
            var artifacts = new JArray();
            if (requests == null || catalog == null)
                return artifacts;

            var written = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < requests.Count; i++)
            {
                CharacterWeaponTraceBakeRequest request = requests[i];
                if (request == null
                    || string.IsNullOrWhiteSpace(request.ClipKey)
                    || (request.TraceId <= 0))
                {
                    continue;
                }

                string key = request.ClipKey + "\n" + request.TraceId;
                if (!written.Add(key)
                    || !catalog.TryGetArtifact(request.ClipKey, request.TraceId, out MxAnimationBakeArtifact artifact))
                {
                    continue;
                }

                artifacts.Add(CreateArtifactObject(request.ClipKey, request.TraceId, artifact));
            }

            return artifacts;
        }

        private static JObject CreateArtifactObject(
            string clipKey,
            int traceId,
            MxAnimationBakeArtifact artifact)
        {
            return new JObject
            {
                ["clipKey"] = clipKey ?? string.Empty,
                ["traceId"] = traceId,
                ["profile"] = CreateProfileObject(artifact.Profile),
                ["artifactHash"] = artifact.ArtifactHash ?? string.Empty,
                ["frames"] = CreateFramesArray(artifact.WeaponTraceFrames),
                ["socketFrames"] = CreateSocketFramesArray(artifact.SocketFrames)
            };
        }

        private static JObject CreateProfileObject(MxAnimationBakeProfile profile)
        {
            return new JObject
            {
                ["profileId"] = profile.ProfileId,
                ["sourceClipKey"] = profile.SourceClipKey.Id,
                ["sourceClipType"] = profile.SourceClipKey.TypeId,
                ["sourceClipHash"] = profile.SourceClipHash,
                ["skeletonProfileId"] = profile.SkeletonProfileId,
                ["skeletonProfileHash"] = profile.SkeletonProfileHash,
                ["sampleTickRate"] = profile.SampleTickRate,
                ["quantizationScale"] = profile.QuantizationScale,
                ["coordinateSpace"] = profile.CoordinateSpace.ToString(),
                ["roundingPolicy"] = profile.RoundingPolicy.ToString(),
                ["importSettingsFingerprint"] = profile.ImportSettingsFingerprint,
                ["profileHash"] = profile.ProfileHash
            };
        }

        private static JArray CreateFramesArray(IReadOnlyList<MxAnimationBakedWeaponTraceFrame> frames)
        {
            var array = new JArray();
            for (int i = 0; frames != null && i < frames.Count; i++)
            {
                MxAnimationBakedWeaponTraceFrame frame = frames[i];
                array.Add(new JObject
                {
                    ["localFrame"] = frame.LocalFrame,
                    ["numericTraceId"] = frame.TraceId,
                    ["socketId"] = frame.SocketId,
                    ["rootPrev"] = CreateVector(frame.RootPrev),
                    ["tipPrev"] = CreateVector(frame.TipPrev),
                    ["rootNow"] = CreateVector(frame.RootNow),
                    ["tipNow"] = CreateVector(frame.TipNow)
                });
            }

            return array;
        }

        private static JArray CreateSocketFramesArray(IReadOnlyList<MxAnimationBakedSocketFrame> frames)
        {
            var array = new JArray();
            for (int i = 0; frames != null && i < frames.Count; i++)
            {
                MxAnimationBakedSocketFrame frame = frames[i];
                array.Add(new JObject
                {
                    ["localFrame"] = frame.LocalFrame,
                    ["socketId"] = frame.SocketId,
                    ["socketPath"] = frame.SocketPath,
                    ["position"] = CreateVector(frame.Position),
                    ["deltaPosition"] = CreateVector(frame.DeltaPosition)
                });
            }

            return array;
        }

        private static MxAnimationBakeProfile ReadProfile(
            JObject root,
            string clipKey,
            int traceId)
        {
            int quantizationScale = ReadInt(root, "quantizationScale", CharacterWeaponTraceBakeArtifactBuilder.DefaultQuantizationScale);
            return new MxAnimationBakeProfile(
                FirstNonEmpty(
                    ReadString(root, "profileId"),
                    "character.weaponTraceBake." + StableSuffix(clipKey) + "." + traceId.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                new ResourceKey(
                    FirstNonEmpty(ReadString(root, "sourceClipKey"), clipKey),
                    FirstNonEmpty(ReadString(root, "sourceClipType"), "AnimationClip")),
                ReadString(root, "sourceClipHash"),
                ReadString(root, "skeletonProfileId"),
                ReadString(root, "skeletonProfileHash"),
                ReadInt(root, "sampleTickRate", 60),
                quantizationScale,
                ReadEnum(root, "coordinateSpace", MxAnimationBakeCoordinateSpace.Local),
                ReadEnum(root, "roundingPolicy", MxAnimationBakeRoundingPolicy.RoundNearest),
                ReadString(root, "importSettingsFingerprint"),
                ReadString(root, "profileHash"));
        }

        private static List<MxAnimationBakedWeaponTraceFrame> ReadFrames(
            JArray array,
            string sourcePath,
            int artifactIndex,
            int traceId,
            List<CharacterWeaponTraceBakeBuildIssue> issues)
        {
            var frames = new List<MxAnimationBakedWeaponTraceFrame>();
            for (int i = 0; array != null && i < array.Count; i++)
            {
                JObject frameRoot = array[i] as JObject;
                if (frameRoot == null)
                    continue;

                int localFrame = ReadInt(frameRoot, "localFrame", -1);
                if (localFrame < 0)
                {
                    issues.Add(CreateIssue(
                        IssueCatalogInvalid,
                        "Generated weapon trace frame is missing localFrame.",
                        sourcePath,
                        "artifacts[" + artifactIndex.ToString(System.Globalization.CultureInfo.InvariantCulture)
                        + "]/frames[" + i.ToString(System.Globalization.CultureInfo.InvariantCulture) + "]",
                        string.Empty,
                        string.Empty,
                        traceId,
                        -1));
                    continue;
                }

                frames.Add(new MxAnimationBakedWeaponTraceFrame(
                    localFrame,
                    ReadInt(frameRoot, "numericTraceId", 0),
                    ReadSocketIdentity(frameRoot),
                    ReadVector(frameRoot["rootPrev"] as JArray),
                    ReadVector(frameRoot["tipPrev"] as JArray),
                    ReadVector(frameRoot["rootNow"] as JArray),
                    ReadVector(frameRoot["tipNow"] as JArray)));
            }

            return frames;
        }

        private static List<MxAnimationBakedSocketFrame> ReadSocketFrames(
            JArray array,
            string sourcePath,
            int artifactIndex,
            string clipKey,
            int traceId,
            List<CharacterWeaponTraceBakeBuildIssue> issues)
        {
            var frames = new List<MxAnimationBakedSocketFrame>();
            for (int i = 0; array != null && i < array.Count; i++)
            {
                JObject frameRoot = array[i] as JObject;
                if (frameRoot == null)
                    continue;

                int localFrame = ReadInt(frameRoot, "localFrame", -1);
                int socketId = ReadSocketIdentity(frameRoot);
                if (localFrame < 0 || socketId <= 0)
                {
                    issues.Add(CreateIssue(
                        IssueCatalogInvalid,
                        "Generated socket trajectory frame is missing localFrame or socketId.",
                        sourcePath,
                        "artifacts[" + artifactIndex.ToString(System.Globalization.CultureInfo.InvariantCulture)
                        + "]/socketFrames[" + i.ToString(System.Globalization.CultureInfo.InvariantCulture) + "]",
                        string.Empty,
                        clipKey,
                        traceId,
                        localFrame));
                    continue;
                }

                frames.Add(new MxAnimationBakedSocketFrame(
                    localFrame,
                    socketId,
                    ReadString(frameRoot, "socketPath"),
                    ReadVector(frameRoot["position"] as JArray),
                    ReadVector(frameRoot["deltaPosition"] as JArray)));
            }

            return frames;
        }

        private static CharacterWeaponTraceBakeBuildIssue CreateIssue(
            string code,
            string message,
            string sourcePath,
            string sourceObjectPath,
            string sourceKey,
            string clipKey,
            int traceId,
            int frame)
        {
            return new CharacterWeaponTraceBakeBuildIssue(
                code,
                message,
                CharacterActionId.None,
                clipKey,
                traceId,
                frame,
                sourcePath,
                sourceObjectPath);
        }

        private static JArray CreateVector(MxAnimationBakedVector3 value)
        {
            return new JArray(value.X, value.Y, value.Z);
        }

        private static MxAnimationBakedVector3 ReadVector(JArray array)
        {
            return new MxAnimationBakedVector3(
                array != null && array.Count > 0 ? array[0].Value<long>() : 0L,
                array != null && array.Count > 1 ? array[1].Value<long>() : 0L,
                array != null && array.Count > 2 ? array[2].Value<long>() : 0L);
        }

        private static T ReadEnum<T>(JObject root, string field, T fallback)
            where T : struct
        {
            string value = ReadString(root, field);
            return Enum.TryParse(value, out T parsed) ? parsed : fallback;
        }

        private static int ReadInt(JObject root, string field, int fallback)
        {
            JToken token = root != null ? root[field] : null;
            return token != null && token.Type != JTokenType.Null ? token.Value<int>() : fallback;
        }

        private static string ReadString(JObject root, string field)
        {
            JToken token = root != null ? root[field] : null;
            return token != null && token.Type != JTokenType.Null ? token.Value<string>() ?? string.Empty : string.Empty;
        }

        private static string FirstNonEmpty(string first, string second)
        {
            return !string.IsNullOrWhiteSpace(first) ? first : second ?? string.Empty;
        }

        private static string StableSuffix(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "none";

            var chars = new char[value.Length];
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                chars[i] = char.IsLetterOrDigit(c) ? c : '_';
            }

            return new string(chars);
        }
    }
}
