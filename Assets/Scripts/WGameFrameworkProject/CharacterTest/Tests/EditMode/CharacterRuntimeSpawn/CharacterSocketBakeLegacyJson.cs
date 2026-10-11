using System;
using System.Collections.Generic;
using MxFramework.Animation;
using MxFramework.CharacterRuntimeSpawn;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MxFramework.Tests.CharacterRuntimeSpawn
{
    /// <summary>
    /// Test-side reader/writer for the retired v2 socket bake JSON. Production code no longer
    /// contains any full-JSON socket bake reader, so this helper exists only to let tests and the
    /// P0 baseline build and inspect legacy payloads, including "re-import required" negative cases.
    /// It reuses <see cref="CharacterSocketBakeBasisValidation"/> so the retired format cannot drift
    /// away from the v3 reader's tolerances.
    /// </summary>
    public static class CharacterSocketBakeLegacyJson
    {
        public const string Format = "mx.character.socketBakeCatalog.v2";
        public const string DefaultRelativePath = "generated/character_socket_bake_catalog.json";

        public static string Serialize(string packageId, CharacterSocketBakeCatalog catalog)
        {
            var tracks = new JArray();
            if (catalog != null)
            {
                var orderedTracks = new List<KeyValuePair<CharacterSocketBakeKey, CharacterSocketBakeTrack>>(
                    catalog.EnumerateTracks());
                orderedTracks.Sort((left, right) =>
                {
                    int clip = string.CompareOrdinal(left.Key.ClipKey, right.Key.ClipKey);
                    return clip != 0 ? clip : left.Key.SocketId.CompareTo(right.Key.SocketId);
                });
                for (int trackIndex = 0; trackIndex < orderedTracks.Count; trackIndex++)
                {
                    KeyValuePair<CharacterSocketBakeKey, CharacterSocketBakeTrack> pair = orderedTracks[trackIndex];
                    var frames = new JArray();
                    IReadOnlyList<MxAnimationBakedSocketFrame> source = pair.Value.Frames;
                    for (int i = 0; i < source.Count; i++)
                    {
                        MxAnimationBakedSocketFrame frame = source[i];
                        if (frame == null)
                            continue;
                        frames.Add(new JObject
                        {
                            ["localFrame"] = frame.LocalFrame,
                            ["socketPath"] = frame.SocketPath,
                            ["position"] = Vector(frame.Position),
                            ["deltaPosition"] = Vector(frame.DeltaPosition)
                        });
                    }
                    var track = new JObject
                    {
                        ["clipKey"] = pair.Key.ClipKey,
                        ["socketId"] = pair.Key.SocketId,
                        ["quantizationScale"] = pair.Value.QuantizationScale,
                        ["frames"] = frames
                    };
                    if (pair.Value.HasBasis)
                    {
                        var basisFrames = new JArray();
                        IReadOnlyList<CharacterSocketBakeBasisFrame> basis = pair.Value.BasisFrames;
                        for (int i = 0; i < basis.Count; i++)
                        {
                            CharacterSocketBakeBasisFrame frame = basis[i];
                            if (frame == null)
                                continue;
                            basisFrames.Add(new JObject
                            {
                                ["localFrame"] = frame.LocalFrame,
                                ["right"] = Vector(frame.Right),
                                ["up"] = Vector(frame.Up),
                                ["forward"] = Vector(frame.Forward)
                            });
                        }
                        track["basisQuantizationScale"] = pair.Value.BasisQuantizationScale;
                        track["basisFrames"] = basisFrames;
                    }
                    tracks.Add(track);
                }
            }
            return new JObject
            {
                ["format"] = Format,
                ["packageId"] = packageId ?? string.Empty,
                ["tracks"] = tracks
            }.ToString(Formatting.Indented) + Environment.NewLine;
        }

        public static CharacterSocketBakeCatalog Load(string json, out string[] issues)
        {
            var catalog = new CharacterSocketBakeCatalog();
            var diagnostics = new List<string>();
            JObject root;
            try { root = JObject.Parse(json ?? string.Empty); }
            catch (JsonException ex)
            {
                issues = new[] { "Socket bake catalog JSON is invalid: " + ex.Message };
                return catalog;
            }
            if (!string.Equals((string)root["format"], Format, StringComparison.Ordinal))
            {
                issues = new[] { "Socket bake catalog format is unsupported." };
                return catalog;
            }
            JArray tracks = root["tracks"] as JArray;
            for (int i = 0; tracks != null && i < tracks.Count; i++)
            {
                JObject item = tracks[i] as JObject;
                string clipKey = (string)item?["clipKey"] ?? string.Empty;
                if (item?["socketId"]?.Type != JTokenType.Integer || item["socketId"].Value<long>() <= 0 || item["socketId"].Value<long>() > int.MaxValue)
                    throw new System.IO.InvalidDataException("Socket bake track requires a positive authored numeric socket identity.");
                int socketId = item["socketId"].Value<int>();
                int scale = (int?)item?["quantizationScale"] ?? CharacterWeaponTraceBakeArtifactBuilder.DefaultQuantizationScale;
                JArray sourceFrames = item?["frames"] as JArray;
                var frames = new MxAnimationBakedSocketFrame[sourceFrames?.Count ?? 0];
                for (int frameIndex = 0; sourceFrames != null && frameIndex < sourceFrames.Count; frameIndex++)
                {
                    JObject frame = sourceFrames[frameIndex] as JObject;
                    int localFrame = (int?)frame?["localFrame"] ?? -1;
                    if (localFrame < 0 || localFrame >= frames.Length)
                    {
                        diagnostics.Add("Socket bake frame index is invalid for " + clipKey + "/" + socketId + ".");
                        continue;
                    }
                    frames[localFrame] = new MxAnimationBakedSocketFrame(
                        localFrame,
                        socketId,
                        (string)frame?["socketPath"] ?? string.Empty,
                        ReadVector(frame?["position"] as JObject),
                        ReadVector(frame?["deltaPosition"] as JObject));
                }
                JToken basisScaleToken = item?["basisQuantizationScale"];
                JToken basisFramesToken = item?["basisFrames"];
                bool hasBasisPayload = basisScaleToken != null || basisFramesToken != null;
                int basisScale = (int?)basisScaleToken ?? 0;
                JArray sourceBasisFrames = basisFramesToken as JArray;
                CharacterSocketBakeBasisFrame[] basisFrames = Array.Empty<CharacterSocketBakeBasisFrame>();
                bool basisValid = true;
                if (hasBasisPayload)
                {
                    basisFrames = new CharacterSocketBakeBasisFrame[frames.Length];
                    var seenBasisFrames = new bool[frames.Length];
                    if (basisScale <= 0)
                    {
                        diagnostics.Add("Socket bake basis quantization scale is invalid for " + clipKey + "/" + socketId + ".");
                        basisValid = false;
                    }
                    if (sourceBasisFrames == null)
                    {
                        diagnostics.Add("Socket bake basis frame list is missing for " + clipKey + "/" + socketId + ".");
                        basisValid = false;
                    }
                    else if (sourceBasisFrames.Count != frames.Length)
                    {
                        diagnostics.Add("Socket bake basis frame count does not match position frames for " + clipKey + "/" + socketId + ".");
                        basisValid = false;
                    }
                    for (int basisIndex = 0; sourceBasisFrames != null && basisIndex < sourceBasisFrames.Count; basisIndex++)
                    {
                        JObject basis = sourceBasisFrames[basisIndex] as JObject;
                        int localFrame = (int?)basis?["localFrame"] ?? -1;
                        if (localFrame < 0 || localFrame >= basisFrames.Length)
                        {
                            diagnostics.Add("Socket bake basis frame index is invalid for " + clipKey + "/" + socketId + ".");
                            basisValid = false;
                            continue;
                        }
                        if (seenBasisFrames[localFrame])
                        {
                            diagnostics.Add(
                                "Socket bake basis frame index is duplicated for " + clipKey + "/" + socketId
                                + " at frame " + localFrame + ".");
                            basisValid = false;
                            continue;
                        }
                        seenBasisFrames[localFrame] = true;
                        if (!TryReadBasisVector(basis?["right"] as JObject, basisScale, out MxAnimationBakedVector3 right)
                            || !TryReadBasisVector(basis?["up"] as JObject, basisScale, out MxAnimationBakedVector3 up)
                            || !TryReadBasisVector(basis?["forward"] as JObject, basisScale, out MxAnimationBakedVector3 forward))
                        {
                            diagnostics.Add(
                                "Socket bake basis component range is invalid for " + clipKey + "/" + socketId
                                + " at frame " + localFrame + ".");
                            basisValid = false;
                            continue;
                        }
                        string basisIssue = ValidateBasisFrame(right, up, forward, basisScale);
                        if (!string.IsNullOrEmpty(basisIssue))
                        {
                            diagnostics.Add(
                                "Socket bake basis " + basisIssue + " is invalid for " + clipKey + "/" + socketId
                                + " at frame " + localFrame + ".");
                            basisValid = false;
                            continue;
                        }
                        basisFrames[localFrame] = new CharacterSocketBakeBasisFrame(
                            localFrame,
                            right,
                            up,
                            forward);
                    }
                    for (int localFrame = 0; localFrame < seenBasisFrames.Length; localFrame++)
                    {
                        if (seenBasisFrames[localFrame])
                            continue;
                        diagnostics.Add(
                            "Socket bake basis frame is missing for " + clipKey + "/" + socketId
                            + " at frame " + localFrame + ".");
                        basisValid = false;
                    }
                    if (!basisValid)
                    {
                        basisScale = 0;
                        basisFrames = Array.Empty<CharacterSocketBakeBasisFrame>();
                    }
                }
                if (string.IsNullOrWhiteSpace(clipKey) || socketId <= 0)
                    diagnostics.Add("Socket bake track is missing clipKey or socketId.");
                else
                    catalog.RegisterTrack(clipKey, socketId, scale, frames, basisScale, basisFrames);
            }
            issues = diagnostics.ToArray();
            return catalog;
        }

        private static JObject Vector(MxAnimationBakedVector3 value) => new JObject { ["x"] = value.X, ["y"] = value.Y, ["z"] = value.Z };
        private static MxAnimationBakedVector3 ReadVector(JObject value) => new MxAnimationBakedVector3((long?)value?["x"] ?? 0L, (long?)value?["y"] ?? 0L, (long?)value?["z"] ?? 0L);

        private static bool TryReadBasisVector(
            JObject value,
            int quantizationScale,
            out MxAnimationBakedVector3 vector)
        {
            vector = default;
            if (quantizationScale <= 0
                || !TryReadBasisComponent(value?["x"], quantizationScale, out long x)
                || !TryReadBasisComponent(value?["y"], quantizationScale, out long y)
                || !TryReadBasisComponent(value?["z"], quantizationScale, out long z))
                return false;
            vector = new MxAnimationBakedVector3(x, y, z);
            return true;
        }

        private static bool TryReadBasisComponent(JToken token, int quantizationScale, out long component)
        {
            component = 0L;
            if (token == null
                || token.Type != JTokenType.Integer
                || !long.TryParse(
                    token.ToString(Formatting.None),
                    System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out component))
                return false;
            return CharacterSocketBakeBasisValidation.IsComponentInRange(component, quantizationScale);
        }

        private static string ValidateBasisFrame(
            MxAnimationBakedVector3 right,
            MxAnimationBakedVector3 up,
            MxAnimationBakedVector3 forward,
            int quantizationScale)
        {
            return CharacterSocketBakeBasisValidation.ValidateFrame(right, up, forward, quantizationScale);
        }
    }
}
