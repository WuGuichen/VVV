using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using MxFramework.Animation;
using MxFramework.CharacterApplication;
using MxFramework.CharacterAction;
using MxFramework.CharacterRuntimeSpawn;
using MxFramework.Combat.Animation;
using MxFramework.Combat.Core;
using MxFramework.Combat.Damage;
using MxFramework.Combat.GameplayBridge;
using MxFramework.Combat.Hit;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using MxFramework.Gameplay;
using MxFramework.Resources;
using MxFramework.Runtime;
using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    internal static class CharacterTestGeneratedPackageSource
    {
        public const string DefaultCharacterPackageRoot = "Assets/MxFrameworkGenerated/CharacterPackages/000001";
        private const string GeneratedAssetsPrefix = "Assets/MxFrameworkGenerated";
        private const string GeneratedStreamingAssetsRoot = "MxFramework/Generated";
        private static readonly Dictionary<string, CharacterImportedPackage> s_packagesByRoot =
            new Dictionary<string, CharacterImportedPackage>(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> s_attemptedRoots =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static CharacterImportedPackage _defaultCharacterPackage;
        private static bool _attemptedDefaultCharacterLoad;

        public static CharacterImportedPackage LoadDefaultCharacterPackage()
        {
            return LoadDefaultPackage();
        }

        public static CharacterImportedPackage LoadDefaultPackage()
        {
            if (_attemptedDefaultCharacterLoad)
                return _defaultCharacterPackage;

            _attemptedDefaultCharacterLoad = true;
            try
            {
                _defaultCharacterPackage =
                    CharacterImportedPackageJson.LoadFromDirectory(ResolveGeneratedContentDirectory(DefaultCharacterPackageRoot));
            }
            catch
            {
                _defaultCharacterPackage = null;
            }

            return _defaultCharacterPackage;
        }

        public static CharacterImportedPackage LoadPackage(string packageRoot)
        {
            if (string.IsNullOrWhiteSpace(packageRoot))
                return null;

            if (string.Equals(packageRoot, DefaultCharacterPackageRoot, StringComparison.OrdinalIgnoreCase))
                return LoadDefaultPackage();

            if (s_attemptedRoots.Contains(packageRoot))
            {
                s_packagesByRoot.TryGetValue(packageRoot, out CharacterImportedPackage cached);
                return cached;
            }

            s_attemptedRoots.Add(packageRoot);
            try
            {
                CharacterImportedPackage package =
                    CharacterImportedPackageJson.LoadFromDirectory(ResolveGeneratedContentDirectory(packageRoot));
                s_packagesByRoot[packageRoot] = package;
                return package;
            }
            catch
            {
                return null;
            }
        }

        public static CharacterImportedPackage LoadPackageForEntity(CharacterTestCharacterEntity entity)
        {
            return entity == null
                ? null
                : LoadPackage(entity.Identity.GeneratedPackageRoot);
        }

        /// <summary>
        /// Invalidates imported-package snapshots after generated character content changes.
        /// AllocByDesign: this is a low-frequency authoring/session refresh boundary, not a runtime tick path.
        /// </summary>
        internal static void InvalidateCache()
        {
            CharacterTestTacticalPreparationCatalog.Invalidate();
            s_packagesByRoot.Clear();
            s_attemptedRoots.Clear();
            _defaultCharacterPackage = null;
            _attemptedDefaultCharacterLoad = false;
        }

        internal static string ResolveGeneratedContentDirectory(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return path;

            if (Directory.Exists(path))
                return path;

            string mirrorPath = ResolveGeneratedStreamingAssetsPath(path);
            return !string.IsNullOrEmpty(mirrorPath) && Directory.Exists(mirrorPath)
                ? mirrorPath
                : path;
        }

        private static string ResolveGeneratedStreamingAssetsPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return string.Empty;

            string normalizedPath = path.Replace('\\', '/');
            if (!normalizedPath.StartsWith(GeneratedAssetsPrefix + "/", StringComparison.Ordinal))
                return string.Empty;

            string relativePath = normalizedPath.Substring(GeneratedAssetsPrefix.Length + 1);
            string combinedRelativePath = Path.Combine(
                GeneratedStreamingAssetsRoot,
                relativePath.Replace('/', Path.DirectorySeparatorChar));
            return Path.Combine(Application.streamingAssetsPath, combinedRelativePath);
        }
    }
}
