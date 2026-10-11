using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using MxFramework.Animation;
using MxFramework.CharacterRuntimeSpawn;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Editor.CharacterImport
{
    /// <summary>
    /// Explicit, out-of-band export of the full socket bake frames for inspection. The output is a
    /// diagnostic artifact only: it is never part of the shipped package and must stay outside Assets.
    /// </summary>
    internal static class CharacterSocketBakeDiagnosticsMenu
    {
        private const string MenuPath = "MxFramework/Character/Diagnostics/Export Socket Bake JSON";
        private const string DefaultOutputDirectory = "Temp/CharacterSocketBakeDiagnostics";

        [MenuItem(MenuPath)]
        private static void ExportSelectedPackageSocketBakeJson()
        {
            string selected = Selection.activeObject != null
                ? AssetDatabase.GetAssetPath(Selection.activeObject)
                : string.Empty;
            if (string.IsNullOrEmpty(selected) || !AssetDatabase.IsValidFolder(selected))
            {
                Debug.LogWarning(
                    "Select a generated character package folder (for example "
                    + "Assets/MxFrameworkGenerated/CharacterPackages/000003) before exporting socket bake diagnostics.");
                return;
            }

            string packageRoot = selected.TrimEnd('/');
            string packageId = Path.GetFileName(packageRoot);
            if (string.IsNullOrWhiteSpace(packageId))
            {
                Debug.LogWarning("Could not resolve a package id from the selected folder: " + packageRoot);
                return;
            }

            if (!CharacterSocketBakeCatalogLoader.TryLoadFromDirectory(
                    packageRoot,
                    packageId,
                    out CharacterSocketBakeCatalog catalog,
                    out CharacterSocketBakeCatalogEvidence evidence,
                    out string[] issues))
            {
                Debug.LogError(
                    "Socket bake diagnostics export failed for package '" + packageId + "': "
                    + string.Join(" | ", issues));
                return;
            }

            string outputDirectory = ResolveOutputDirectory();
            if (outputDirectory == null)
                return;

            Directory.CreateDirectory(outputDirectory);
            string outputPath = Path.Combine(outputDirectory, "character_socket_bake_catalog." + packageId + ".json");
            File.WriteAllText(outputPath, BuildDiagnosticJson(packageId, catalog, evidence), new UTF8Encoding(false));
            Debug.Log(
                "Exported socket bake diagnostics for package '" + packageId + "' to "
                + outputPath.Replace('\\', '/') + " (tracks=" + evidence.TrackCount
                + ", frames=" + evidence.FrameCount + ", basis=" + evidence.BasisFrameCount + ").");
        }

        /// <summary>Diagnostic output must never land inside Assets, where it would ship with the project.</summary>
        private static string ResolveOutputDirectory()
        {
            string directory = Path.GetFullPath(DefaultOutputDirectory);
            string assetsRoot = Path.GetFullPath(Application.dataPath);
            if (directory.StartsWith(assetsRoot, StringComparison.OrdinalIgnoreCase))
            {
                Debug.LogError("Socket bake diagnostics output must be outside Assets: " + directory);
                return null;
            }
            return directory;
        }

        private static string BuildDiagnosticJson(
            string packageId,
            CharacterSocketBakeCatalog catalog,
            CharacterSocketBakeCatalogEvidence evidence)
        {
            var ordered = new List<KeyValuePair<CharacterSocketBakeKey, CharacterSocketBakeTrack>>(catalog.EnumerateTracks());
            ordered.Sort((left, right) =>
            {
                int clip = string.CompareOrdinal(left.Key.ClipKey, right.Key.ClipKey);
                return clip != 0 ? clip : left.Key.SocketId.CompareTo(right.Key.SocketId);
            });

            var tracks = new JArray();
            for (int trackIndex = 0; trackIndex < ordered.Count; trackIndex++)
            {
                KeyValuePair<CharacterSocketBakeKey, CharacterSocketBakeTrack> pair = ordered[trackIndex];
                CharacterSocketBakeTrack track = pair.Value;
                var frames = new JArray();
                for (int i = 0; i < track.Frames.Count; i++)
                {
                    MxAnimationBakedSocketFrame frame = track.Frames[i];
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

                var trackJson = new JObject
                {
                    ["clipKey"] = pair.Key.ClipKey,
                    ["socketId"] = pair.Key.SocketId,
                    ["quantizationScale"] = track.QuantizationScale,
                    ["frames"] = frames
                };
                if (track.HasBasis)
                {
                    var basisFrames = new JArray();
                    for (int i = 0; i < track.BasisFrames.Count; i++)
                    {
                        CharacterSocketBakeBasisFrame basis = track.BasisFrames[i];
                        if (basis == null)
                            continue;
                        basisFrames.Add(new JObject
                        {
                            ["localFrame"] = basis.LocalFrame,
                            ["right"] = Vector(basis.Right),
                            ["up"] = Vector(basis.Up),
                            ["forward"] = Vector(basis.Forward)
                        });
                    }
                    trackJson["basisQuantizationScale"] = track.BasisQuantizationScale;
                    trackJson["basisFrames"] = basisFrames;
                }
                tracks.Add(trackJson);
            }

            return new JObject
            {
                ["format"] = "mx.character.socketBakeCatalogDiagnostic.v1",
                ["sourceFormat"] = evidence.Format,
                ["packageId"] = packageId,
                ["indexHash"] = evidence.IndexHash,
                ["binaryHash"] = evidence.BinaryHash,
                ["binarySize"] = evidence.BinarySize,
                ["trackCount"] = evidence.TrackCount,
                ["frameCount"] = evidence.FrameCount,
                ["basisFrameCount"] = evidence.BasisFrameCount,
                ["tracks"] = tracks
            }.ToString(Formatting.Indented) + "\n";
        }

        private static JObject Vector(MxAnimationBakedVector3 value) =>
            new JObject { ["x"] = value.X, ["y"] = value.Y, ["z"] = value.Z };
    }
}
