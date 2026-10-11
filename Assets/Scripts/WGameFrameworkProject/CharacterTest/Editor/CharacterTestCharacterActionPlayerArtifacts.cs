using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using MxFramework.CharacterAction;
using MxFramework.CharacterRuntimeSpawn;
using UnityEngine;

namespace MxFramework.Game.CharacterTest.Editor
{
    /// <summary>Mirrors the existing Authoring action exports and accounts for them in R-domain receipts.</summary>
    internal static class CharacterTestCharacterActionPlayerArtifacts
    {
        internal static string SourceRoot => CharacterTestCharacterActionConfigSource.GeneratedActionsRoot;
        internal static string MirrorRoot => Path.Combine(Application.streamingAssetsPath,
            CharacterTestCharacterActionConfigSource.PlayerActionsRelativeRoot);

        internal static void ValidateSources(CharacterTestPlayerCharacterPackageSelection selection)
        {
            var catalog = CharacterTestCharacterActionConfigSource.LoadCatalog(
                new CharacterTestCharacterActionConfigSourceOptions(
                    CharacterTestCharacterActionConfigSource.CreateCandidateDirectories(SourceRoot, null, true)));
            if (!catalog.IsReady)
                throw new InvalidOperationException("Character Action export is not ready: " + catalog.ConfigSourceDescription);
            foreach (var diagnostic in catalog.ConfigSourceDiagnostics)
                if (diagnostic.Severity == CharacterActionDiagnosticSeverity.Error)
                    throw new InvalidOperationException("Character Action export is invalid: " + diagnostic);
            foreach (var package in selection.Packages)
            {
                var imported = CharacterImportedPackageJson.LoadFromDirectory(package.SourceDirectory);
                var resolved = CharacterRuntimeSpawnResolver.Resolve(imported, default);
                if (!resolved.IsSuccess)
                    throw new InvalidOperationException("Cannot resolve Character Action owner for package " + package.PackageId);
                var context = resolved.Binding.ResolvedProfile.DebugContext;
                bool found = false;
                foreach (var actionSet in catalog.Registry.ActionSets)
                    if (actionSet.CharacterStableId == context.CharacterStableId
                        && actionSet.EquipmentStateStableId == context.ActiveEquipmentStateStableId)
                    {
                        found = true;
                        break;
                    }
                if (!found)
                    throw new InvalidOperationException("Character ActionSet is missing for package " + package.PackageId
                        + ", character=" + context.CharacterStableId
                        + ", equipment=" + context.ActiveEquipmentStateStableId);
            }
        }

        internal static void Refresh()
        {
            foreach (string relative in SelectFiles(SourceRoot))
            {
                string source = Path.Combine(SourceRoot, relative);
                string mirror = Path.Combine(MirrorRoot, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(mirror));
                if (!File.Exists(mirror) || !CharacterTestRuntimeCatalogPlayerArtifactExporter.FilesMatch(source, mirror))
                    File.Copy(source, mirror, overwrite: true);
            }
            RequireMirror();
        }

        internal static void RequireMirror()
        {
            if (!string.Equals(Capture(SourceRoot), Capture(MirrorRoot), StringComparison.Ordinal))
                throw new InvalidOperationException("R Character Action exports and Player mirror differ. "
                    + "Run the full Windows Package Test; inspect any extra mirror files before removing them.");
            var catalog = CharacterTestCharacterActionConfigSource.LoadCatalog(
                new CharacterTestCharacterActionConfigSourceOptions(
                    CharacterTestCharacterActionConfigSource.CreateCandidateDirectories(
                        null, Application.streamingAssetsPath, false)));
            if (!catalog.IsReady)
                throw new InvalidOperationException("Player Character Action mirror is not resolvable: " + catalog.ConfigSourceDescription);
        }

        internal static string Capture(string root)
        {
            var builder = new StringBuilder("characterActions|formal-authoring-json\n");
            foreach (string relative in SelectFiles(root))
            {
                string path = Path.Combine(root, relative);
                using var stream = File.OpenRead(path);
                using var sha = SHA256.Create();
                builder.Append(relative).Append('|').Append(stream.Length).Append('|')
                    .AppendLine(BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty).ToLowerInvariant());
            }
            return builder.ToString();
        }

        internal static List<string> SelectFiles(string root)
        {
            if (!Directory.Exists(root))
                throw new DirectoryNotFoundException("Character Action export directory is missing: " + root);
            var files = new List<string>();
            foreach (string file in Directory.GetFiles(root, "*.json", SearchOption.AllDirectories))
            {
                string name = Path.GetFileName(file);
                if (name != CharacterTestCharacterActionConfigSource.ProfilesFileName
                    && name != CharacterTestCharacterActionConfigSource.ActionSetsFileName
                    && name != CharacterTestCharacterActionConfigSource.ActionsFileName
                    && name != CharacterTestCharacterActionConfigSource.ReactionProfilesFileName
                    && name != CharacterTestCharacterActionConfigSource.LandingProfilesFileName)
                    continue;
                string relative = Path.GetRelativePath(root, file).Replace('\\', '/');
                string[] parts = relative.Split('/');
                if (parts.Length != 3 || (parts[0] != "Profiles" && parts[0] != "ActionSets" && parts[0] != "Libraries"))
                    throw new InvalidDataException("Unexpected Character Action runtime export path: " + relative);
                files.Add(relative);
            }
            files.Sort(StringComparer.Ordinal);
            if (files.Count == 0)
                throw new InvalidDataException("Character Action runtime exports are empty: " + root);
            return files;
        }
    }
}
