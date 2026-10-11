using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MxFramework.CharacterApplication;
using MxFramework.CharacterRuntimeSpawn;
using MxFramework.Editor.CharacterAction;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Editor
{
    internal sealed class VfxCharacterPreviewSocket
    {
        public string SocketId = string.Empty;
        public string DisplayName = string.Empty;
        public string Usage = string.Empty;
    }

    internal sealed class VfxCharacterPreviewDescriptor
    {
        public CharacterImportedPackage Package;
        public string CharacterStableId = string.Empty;
        public string PackageId = string.Empty;
        public string DisplayName = string.Empty;
        public string PrefabPath = string.Empty;
        public string EvidenceHash = string.Empty;
        public readonly List<VfxCharacterPreviewSocket> Sockets = new List<VfxCharacterPreviewSocket>();
        public CharacterActionAnimationClipChoice[] Animations = Array.Empty<CharacterActionAnimationClipChoice>();
    }

    internal readonly struct VfxCharacterPosePreset
    {
        public VfxCharacterPosePreset(string id, string displayName)
        {
            Id = id;
            DisplayName = displayName;
        }

        public string Id { get; }
        public string DisplayName { get; }
    }

    /// <summary>Discovers Ready generated character packages for VFX authoring preview.</summary>
    /// <remarks>Editor-only, AllocByDesign. The generated package directories are scanned only on explicit character-preview demand.</remarks>
    internal sealed class VfxCharacterPreviewCatalog
    {
        internal static readonly VfxCharacterPosePreset[] PosePresets =
        {
            new VfxCharacterPosePreset("Idle", "待机"),
            new VfxCharacterPosePreset("Cast", "施法"),
            new VfxCharacterPosePreset("Attack", "攻击"),
            new VfxCharacterPosePreset("Guard", "防御"),
            new VfxCharacterPosePreset("Hit", "受击"),
            new VfxCharacterPosePreset("Run", "奔跑"),
            new VfxCharacterPosePreset("Custom", "自选动画")
        };

        public IReadOnlyList<VfxCharacterPreviewDescriptor> Discover()
        {
            var result = new List<VfxCharacterPreviewDescriptor>();
            string root = CharacterActionPreviewBindingDiscovery.GeneratedPackagesRoot;
            if (!Directory.Exists(root)) return result;
            string[] directories = Directory.GetDirectories(root).OrderBy(value => value, StringComparer.Ordinal).ToArray();
            for (int i = 0; i < directories.Length; i++) DiscoverPackage(NormalizePath(directories[i]), result);
            return result;
        }

        public static bool TryResolvePose(
            VfxCharacterPreviewDescriptor character,
            string posePresetId,
            string targetEffectId,
            out CharacterActionAnimationClipChoice choice)
        {
            choice = default;
            if (character == null || character.Animations.Length == 0 || string.Equals(posePresetId, "Custom", StringComparison.Ordinal)) return false;
            string[] tokens = PoseTokens(posePresetId, targetEffectId);
            for (int tokenIndex = 0; tokenIndex < tokens.Length; tokenIndex++)
            {
                string token = tokens[tokenIndex];
                for (int clipIndex = 0; clipIndex < character.Animations.Length; clipIndex++)
                {
                    CharacterActionAnimationClipChoice candidate = character.Animations[clipIndex];
                    string searchable = candidate.ResourceKey + " " + candidate.ClipId + " " + candidate.SourceClipName;
                    if (searchable.IndexOf(token, StringComparison.OrdinalIgnoreCase) < 0) continue;
                    choice = candidate;
                    return true;
                }
            }
            return false;
        }

        private static void DiscoverPackage(string packageRoot, List<VfxCharacterPreviewDescriptor> result)
        {
            string receiptPath = packageRoot + "/package_cache/unity_editor_import_receipt.json";
            if (!File.Exists(receiptPath)) return;
            try
            {
                JObject receipt = JObject.Parse(File.ReadAllText(receiptPath));
                if (receipt.Value<bool?>("ready") != true) return;
                string prefabPath = NormalizePath(receipt.SelectToken("prefab.prefabPath")?.Value<string>());
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                Animator animator = prefab == null ? null : prefab.GetComponentInChildren<Animator>(true);
                if (prefab == null || animator == null || animator.avatar == null || !animator.avatar.isValid) return;

                CharacterImportedPackage package = CharacterImportedPackageJson.LoadFromDirectory(packageRoot);
                CharacterConfig[] characters = package.Configs?.Characters ?? Array.Empty<CharacterConfig>();
                for (int characterIndex = 0; characterIndex < characters.Length; characterIndex++)
                {
                    CharacterConfig character = characters[characterIndex];
                    if (character == null || string.IsNullOrWhiteSpace(character.StableId)) continue;
                    var descriptor = new VfxCharacterPreviewDescriptor
                    {
                        Package = package,
                        CharacterStableId = character.StableId,
                        PackageId = package.PackageId,
                        DisplayName = character.StableId + "  [" + package.PackageId + "]",
                        PrefabPath = prefabPath,
                        EvidenceHash = VfxCompositionDraftService.HashFile(receiptPath),
                        Animations = CharacterActionPreviewBindingDiscovery.DiscoverAnimationClips(character.StableId)
                    };
                    descriptor.Sockets.Add(new VfxCharacterPreviewSocket { SocketId = string.Empty, DisplayName = "角色根节点", Usage = "Root" });
                    CharacterSocketRuntimeBinding[] sockets = package.Geometry?.Sockets ?? Array.Empty<CharacterSocketRuntimeBinding>();
                    for (int socketIndex = 0; socketIndex < sockets.Length; socketIndex++)
                    {
                        CharacterSocketRuntimeBinding socket = sockets[socketIndex];
                        if (socket == null || socket.SocketId <= 0) continue;
                        string socketSourceKey = CharacterContentIdentityAuthoringCatalog.GetSourceKey(400, socket.SocketId);
                        if (FindChild(prefab.transform, "Socket_" + socketSourceKey) == null) continue;
                        descriptor.Sockets.Add(new VfxCharacterPreviewSocket
                        {
                            SocketId = socketSourceKey,
                            DisplayName = SocketDisplayName(socketSourceKey) + "  (" + socketSourceKey + ")",
                            Usage = socket.Usage
                        });
                    }
                    result.Add(descriptor);
                }
            }
            catch (Exception exception) when (exception is IOException || exception is Newtonsoft.Json.JsonException || exception is CharacterImportedPackageJsonException)
            {
                Debug.LogWarning("VFX 角色预览跳过不完整角色包 " + packageRoot + "：" + exception.Message);
            }
        }

        private static string[] PoseTokens(string posePresetId, string targetEffectId)
        {
            switch (posePresetId)
            {
                case "Idle": return new[] { "standing_idle", "idle" };
                case "Cast":
                    string effectToken = (targetEffectId ?? string.Empty).Split('.').LastOrDefault() ?? string.Empty;
                    return string.IsNullOrWhiteSpace(effectToken) ? new[] { "skill", "cast" } : new[] { effectToken, "skill", "cast" };
                case "Attack": return new[] { "basicattack", "atk1", "attack" };
                case "Guard": return new[] { "guard", "block" };
                case "Hit": return new[] { "damage_mid", "damage", "hit" };
                case "Run": return new[] { "run_forward", "run" };
                default: return Array.Empty<string>();
            }
        }

        private static Transform FindChild(Transform root, string name)
        {
            if (root == null) return null;
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++) if (string.Equals(transforms[i].name, name, StringComparison.Ordinal)) return transforms[i];
            return null;
        }

        private static string SocketDisplayName(string socketId)
        {
            switch (socketId)
            {
                case "mainHand": return "主手";
                case "offHand": return "副手";
                case "back": return "背部";
                case "foot.L": return "左脚";
                case "foot.R": return "右脚";
                case "headVfx": return "头部 VFX";
                default: return socketId;
            }
        }

        private static string NormalizePath(string value) => (value ?? string.Empty).Replace('\\', '/');
    }
}
