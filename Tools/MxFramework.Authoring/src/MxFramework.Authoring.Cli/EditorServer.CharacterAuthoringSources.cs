using System.Text.Json;
using MxFramework.Authoring;

namespace MxFramework.Authoring.Cli;

internal static partial class EditorServer
{
    private const string CharacterAuthoringManifestFileName = "authoring_manifest.json";

    private static void EnsureNumericCharacterAuthoringScaffold(
        string rootPath,
        string sourcePath,
        string characterId,
        string displayName,
        string derivedFromCharacterId,
        JsonSerializerOptions jsonOptions)
    {
        if (!IsNumericCharacterId(characterId))
            return;

        Directory.CreateDirectory(sourcePath);
        string sourceManifestPath = Path.Combine(sourcePath, CharacterSourceManifestFileName);
        string manifestPath = Path.Combine(sourcePath, CharacterAuthoringManifestFileName);
        CharacterAuthoringManifestDocument copiedAuthoring = File.Exists(manifestPath)
            ? JsonSerializer.Deserialize<CharacterAuthoringManifestDocument>(File.ReadAllText(manifestPath), jsonOptions)
            : null;
        var managedItems = copiedAuthoring?.Items ?? new List<CharacterAuthoringManifestItem>();
        var authority = copiedAuthoring?.Authority ?? new CharacterAuthoringAuthority();
        if (IsNumericCharacterId(derivedFromCharacterId))
        {
            foreach (CharacterAuthoringManifestItem item in managedItems)
            {
                string previousRelative = (item.Path ?? string.Empty).Replace('\\', '/');
                string nextRelative = previousRelative.Replace(derivedFromCharacterId, characterId, StringComparison.Ordinal);
                if (!string.Equals(previousRelative, nextRelative, StringComparison.Ordinal))
                {
                    string previousPath = Path.Combine(sourcePath, previousRelative.Replace('/', Path.DirectorySeparatorChar));
                    string nextPath = Path.Combine(sourcePath, nextRelative.Replace('/', Path.DirectorySeparatorChar));
                    if (File.Exists(previousPath) && !File.Exists(nextPath))
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(nextPath)!);
                        File.Move(previousPath, nextPath);
                    }
                    item.Path = nextRelative;
                    if (File.Exists(nextPath)) item.Sha256 = CharacterAuthoringManifestValidator.ComputeSha256(nextPath);
                }
            }
            authority.BlenderPath = (authority.BlenderPath ?? string.Empty).Replace(derivedFromCharacterId, characterId, StringComparison.Ordinal);
            authority.FinalFbxPath = (authority.FinalFbxPath ?? string.Empty).Replace(derivedFromCharacterId, characterId, StringComparison.Ordinal);
        }
        if (File.Exists(sourceManifestPath))
        {
            CharacterSourceManifestDocument sourceManifest = JsonSerializer.Deserialize<CharacterSourceManifestDocument>(File.ReadAllText(sourceManifestPath), jsonOptions)
                ?? new CharacterSourceManifestDocument();
            CharacterSourceManifestItem body = sourceManifest.Items?.FirstOrDefault(item => string.Equals(item.PresetId, "characterModel", StringComparison.OrdinalIgnoreCase));
            if (body?.Source != null && string.Equals(body.Source.Kind, "file", StringComparison.OrdinalIgnoreCase))
            {
                string oldRelative = (body.Source.Path ?? string.Empty).Replace('\\', '/');
                string oldPath = Path.GetFullPath(Path.Combine(sourcePath, oldRelative.Replace('/', Path.DirectorySeparatorChar)));
                string extension = Path.GetExtension(oldRelative).ToLowerInvariant();
                string finalRelative = string.IsNullOrWhiteSpace(derivedFromCharacterId) || IsNumericCharacterId(derivedFromCharacterId)
                    ? "Final_Source/" + characterId + "_LOD0" + extension
                    : oldRelative;
                string finalPath = Path.Combine(sourcePath, finalRelative.Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(oldPath) && !string.Equals(oldPath, finalPath, StringComparison.OrdinalIgnoreCase))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(finalPath)!);
                    File.Move(oldPath, finalPath, overwrite: false);
                }
                body.Source.Path = finalRelative;
                if (File.Exists(finalPath) && string.Equals(extension, ".fbx", StringComparison.OrdinalIgnoreCase))
                {
                    managedItems.RemoveAll(item => string.Equals(item.Kind, CharacterAuthoringAssetKinds.FinalFbx, StringComparison.Ordinal));
                    managedItems.Add(new CharacterAuthoringManifestItem
                    {
                        Id = "final-fbx",
                        Kind = CharacterAuthoringAssetKinds.FinalFbx,
                        Role = "characterModel",
                        Path = finalRelative,
                        Sha256 = CharacterAuthoringManifestValidator.ComputeSha256(finalPath)
                    });
                    authority.FinalFbxPath = finalRelative;
                }
            }
            sourceManifest.CharacterId = characterId;
            sourceManifest.PackageId = characterId;
            sourceManifest.SourceRoot = ".";
            WriteJsonFileAtomic(sourceManifestPath, sourceManifest, jsonOptions);
        }

        var manifest = new CharacterAuthoringManifestDocument
        {
            CharacterId = characterId,
            PackageId = characterId,
            DisplayName = displayName ?? string.Empty,
            DerivedFromCharacterId = derivedFromCharacterId ?? string.Empty,
            Authority = authority,
            Items = managedItems
        };
        WriteJsonFileAtomic(manifestPath, manifest, jsonOptions);

        string readmePath = Path.Combine(sourcePath, "README.md");
        if (!File.Exists(readmePath))
        {
            File.WriteAllText(
                readmePath,
                "# " + characterId + " · " + (displayName ?? string.Empty) + Environment.NewLine + Environment.NewLine
                + "This is the versioned ArtSource root for numeric character `" + characterId + "`." + Environment.NewLine
                + "Optional Blender, reference, milestone, audit, and provenance files are managed by `authoring_manifest.json` only when explicitly adopted." + Environment.NewLine);
        }
    }

    internal static object ReadCharacterAuthoringSourceState(
        string rootPath,
        string packageRelative,
        JsonSerializerOptions jsonOptions)
    {
        string packagePath = ResolveSafePath(rootPath, NormalizePackageRelativePath(packageRelative));
        CharacterResourcePackage package = CharacterPackageCommands.ReadPackage(packagePath, jsonOptions);
        string characterId = package.Manifest?.PackageId ?? string.Empty;
        string sourceRelative = JoinImportMaterialPath(CharacterSourceRootRelativePrefix, characterId);
        string sourcePath = ResolveSafePath(rootPath, sourceRelative);
        string manifestPath = Path.Combine(sourcePath, CharacterAuthoringManifestFileName);
        CharacterAuthoringManifestDocument manifest = File.Exists(manifestPath)
            ? JsonSerializer.Deserialize<CharacterAuthoringManifestDocument>(File.ReadAllText(manifestPath), jsonOptions)
            : null;
        CharacterDccAuthoringValidationReport report = CharacterAuthoringManifestValidator.Validate(sourcePath, manifest, characterId);
        return new
        {
            characterId,
            sourceRelative,
            manifestRelative = JoinImportMaterialPath(sourceRelative, CharacterAuthoringManifestFileName),
            manifest,
            validation = report,
            status = manifest == null ? "NotDeclared" : report.HasBlockingIssues ? "Invalid" : "Current"
        };
    }

    internal static object ApplyCharacterAuthoringSource(
        string rootPath,
        CharacterAuthoringSourceApplyRequest request,
        JsonSerializerOptions jsonOptions)
    {
        string packageRelative = NormalizePackageRelativePath(request.package);
        string packagePath = ResolveSafePath(rootPath, packageRelative);
        CharacterResourcePackage package = CharacterPackageCommands.ReadPackage(packagePath, jsonOptions);
        string characterId = package.Manifest?.PackageId ?? string.Empty;
        if (!IsNumericCharacterId(characterId))
            throw new ArgumentException("Optional authoring source management is enabled only for six-digit characters.");
        string sourceRelative = JoinImportMaterialPath(CharacterSourceRootRelativePrefix, characterId);
        string sourcePath = ResolveSafePath(rootPath, sourceRelative);
        string manifestPath = Path.Combine(sourcePath, CharacterAuthoringManifestFileName);
        CharacterAuthoringManifestDocument manifest = JsonSerializer.Deserialize<CharacterAuthoringManifestDocument>(File.ReadAllText(manifestPath), jsonOptions)
            ?? throw new InvalidDataException("authoring_manifest.json is malformed.");
        if (request.expectedRevision > 0 && request.expectedRevision != manifest.Revision)
            throw new ArgumentException("authoring_manifest revision changed; refresh before applying.");

        string action = (request.action ?? string.Empty).Trim().ToLowerInvariant();
        string relative = (request.path ?? string.Empty).Trim().Replace('\\', '/');
        if (action == "adopt")
        {
            string fullPath = ResolveCharacterAuthoringSourcePath(sourcePath, relative);
            if (!File.Exists(fullPath))
                throw new FileNotFoundException("Optional authoring source file was not found.", fullPath);
            CharacterAuthoringManifestItem item = manifest.Items.FirstOrDefault(value => string.Equals(value.Id, request.id, StringComparison.Ordinal))
                ?? new CharacterAuthoringManifestItem { Id = request.id ?? string.Empty };
            item.Kind = request.kind ?? string.Empty;
            item.Role = request.role ?? string.Empty;
            item.Path = relative;
            item.Sha256 = CharacterAuthoringManifestValidator.ComputeSha256(fullPath);
            if (!manifest.Items.Contains(item))
                manifest.Items.Add(item);
            manifest.Authority ??= new CharacterAuthoringAuthority();
            if (string.Equals(item.Kind, CharacterAuthoringAssetKinds.BlenderAuthority, StringComparison.Ordinal))
                manifest.Authority.BlenderPath = relative;
            if (string.Equals(item.Kind, CharacterAuthoringAssetKinds.FinalFbx, StringComparison.Ordinal))
                manifest.Authority.FinalFbxPath = relative;
        }
        else if (action == "remove")
        {
            CharacterAuthoringManifestItem item = manifest.Items.FirstOrDefault(value => string.Equals(value.Id, request.id, StringComparison.Ordinal));
            if (item != null)
            {
                manifest.Items.Remove(item);
                if (string.Equals(manifest.Authority?.BlenderPath, item.Path, StringComparison.Ordinal)) manifest.Authority.BlenderPath = string.Empty;
                if (string.Equals(manifest.Authority?.FinalFbxPath, item.Path, StringComparison.Ordinal)) manifest.Authority.FinalFbxPath = string.Empty;
            }
        }
        else if (action == "refresh")
        {
            foreach (CharacterAuthoringManifestItem item in manifest.Items)
            {
                string fullPath = ResolveCharacterAuthoringSourcePath(sourcePath, item.Path);
                if (File.Exists(fullPath)) item.Sha256 = CharacterAuthoringManifestValidator.ComputeSha256(fullPath);
            }
        }
        else
        {
            throw new ArgumentException("action must be adopt, remove, or refresh.");
        }

        manifest.Items = manifest.Items.OrderBy(item => item.Id, StringComparer.Ordinal).ToList();
        manifest.Revision++;
        CharacterDccAuthoringValidationReport report = CharacterAuthoringManifestValidator.Validate(sourcePath, manifest, characterId);
        if (report.HasBlockingIssues)
            return new { success = false, validation = report, manifest };
        WriteJsonFileAtomic(manifestPath, manifest, jsonOptions);
        return new { success = true, revision = manifest.Revision, validation = report, manifest };
    }

    private static string ResolveCharacterAuthoringSourcePath(string sourcePath, string relative)
    {
        if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative) || relative.Split('/').Any(segment => segment == ".." || segment.Length == 0))
            throw new ArgumentException("Optional authoring source path must stay inside the character ArtSource root.");
        string root = Path.GetFullPath(sourcePath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        string fullPath = Path.GetFullPath(Path.Combine(sourcePath, relative.Replace('/', Path.DirectorySeparatorChar)));
        if (!fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Optional authoring source path escapes the character root.");
        return fullPath;
    }

    internal sealed class CharacterAuthoringSourceApplyRequest
    {
        public string package { get; set; } = string.Empty;
        public int expectedRevision { get; set; }
        public string action { get; set; } = string.Empty;
        public string id { get; set; } = string.Empty;
        public string kind { get; set; } = string.Empty;
        public string role { get; set; } = string.Empty;
        public string path { get; set; } = string.Empty;
    }
}
