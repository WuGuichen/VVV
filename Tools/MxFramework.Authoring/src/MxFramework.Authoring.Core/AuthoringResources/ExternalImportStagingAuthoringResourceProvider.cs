using System;
using System.Collections.Generic;
using System.IO;

namespace MxFramework.Authoring
{
    public sealed class ExternalImportStagingAuthoringResourceProvider : IAuthoringResourceProvider
    {
        public string ProviderId
        {
            get { return AuthoringResourceProviderIds.ExternalImportStaging; }
        }

        public AuthoringResourceProviderDescriptor Describe(AuthoringResourceProviderContext context)
        {
            return new AuthoringResourceProviderDescriptor
            {
                ProviderId = ProviderId,
                DisplayName = "External Import Staging",
                SourceKind = AuthoringResourceSourceKind.ExternalFile,
                Available = true,
                Status = "Ready"
            };
        }

        public AuthoringResourceCollection BuildResourceCollection(AuthoringResourceProviderContext context)
        {
            AuthoringExternalImportStagingDocument staging = null;
            if (context != null && context.Metadata != null)
            {
                string marker;
                if (context.Metadata.TryGetValue("externalImportStaging", out marker) &&
                    string.Equals(marker, "empty", StringComparison.Ordinal))
                    staging = new AuthoringExternalImportStagingDocument();
            }

            return FromStagingDocument(staging, context);
        }

        public static AuthoringResourceCollection FromStagingDocument(
            AuthoringExternalImportStagingDocument staging,
            AuthoringResourceProviderContext context)
        {
            var provider = new ExternalImportStagingAuthoringResourceProvider();
            var collection = new AuthoringResourceCollection
            {
                ScopeId = context != null && !string.IsNullOrWhiteSpace(context.ScopeId)
                    ? context.ScopeId
                    : "externalImportStaging"
            };
            collection.Providers.Add(provider.Describe(context));
            if (context != null)
            {
                AuthoringResourceProviderUtilities.AddIfPresent(collection.Metadata, "packageId", context.PackageId);
                AuthoringResourceProviderUtilities.AddIfPresent(collection.Metadata, "packagePath", context.PackagePath);
                AuthoringResourceProviderUtilities.AddIfPresent(collection.Metadata, "sourceRootLabel", staging != null ? staging.SourceRootLabel : string.Empty);
            }

            if (staging == null || staging.Files == null || staging.Files.Count == 0)
                return collection;

            var seenHashes = new Dictionary<string, string>(StringComparer.Ordinal);
            AddExistingResourceHashes(seenHashes, context != null ? context.PackageResourceCatalog : null);
            long maxFileSizeBytes = staging.MaxFileSizeBytes <= 0 ? 512L * 1024L * 1024L : staging.MaxFileSizeBytes;
            for (int i = 0; i < staging.Files.Count; i++)
            {
                AuthoringExternalImportStagingFile file = staging.Files[i];
                if (file == null)
                    continue;

                string displayPath = GetDisplayPath(file);
                if (IsIgnoredFile(displayPath, file.FileName))
                {
                    collection.Diagnostics.Add(new AuthoringResourceDiagnostic
                    {
                        Severity = CharacterAuthoringValidationSeverity.Info,
                        Code = AuthoringResourceDiagnosticCodes.IgnoredImportFile,
                        ProviderId = provider.ProviderId,
                        SourceConfigKind = "externalImportStaging",
                        SourceField = "files/" + i.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        Message = "Ignored metadata or hidden file: " + displayPath,
                        SuggestedFix = "No action needed; metadata files are not staged as resources."
                    });
                    continue;
                }

                collection.Items.Add(CreateStagedItem(file, displayPath, maxFileSizeBytes, seenHashes, i));
            }

            return collection;
        }

        private static AuthoringResourceItem CreateStagedItem(
            AuthoringExternalImportStagingFile file,
            string displayPath,
            long maxFileSizeBytes,
            Dictionary<string, string> seenHashes,
            int index)
        {
            string extension = Path.GetExtension(file.FileName ?? displayPath).TrimStart('.').ToLowerInvariant();
            ResourceTypeCandidate candidate = DetectCandidate(extension, displayPath);
            string sourceHash = ResolveSourceHash(file);
            long size = file.SizeBytes > 0 ? file.SizeBytes : GetDecodedSize(file.BytesBase64);
            string stableId = "external." + AuthoringResourceProviderUtilities.SanitizeStableSegment(
                AuthoringResourceProviderUtilities.FirstNonEmpty(displayPath, file.FileName, "file." + index.ToString(System.Globalization.CultureInfo.InvariantCulture)));
            bool tooLarge = size > maxFileSizeBytes;
            bool duplicate = !string.IsNullOrWhiteSpace(sourceHash) && seenHashes.ContainsKey(sourceHash);
            bool selectable = candidate.Supported && !tooLarge && !duplicate;

            var item = new AuthoringResourceItem
            {
                ResourceId = AuthoringResourceProviderUtilities.BuildResourceId(AuthoringResourceProviderIds.ExternalImportStaging, stableId, displayPath),
                StableId = stableId,
                DisplayName = AuthoringResourceProviderUtilities.GetFileDisplayName(displayPath, file.FileName),
                Kind = candidate.Kind,
                Usage = candidate.Usage,
                SourceProviderId = AuthoringResourceProviderIds.ExternalImportStaging,
                SourceKind = AuthoringResourceSourceKind.ExternalFile,
                BindingKind = AuthoringResourceBindingKind.ExternalSource,
                ImportStatus = selectable ? AuthoringResourceImportStatus.New : duplicate ? AuthoringResourceImportStatus.Conflict : AuthoringResourceImportStatus.ImportFailed,
                RuntimeAvailability = AuthoringResourceRuntimeAvailability.NotRuntimeLoadable,
                Tags = new List<string> { "external-import-staging", candidate.Tag }
            };
            AddMetadata(item, file, displayPath, extension, sourceHash, size, candidate, selectable, duplicate, tooLarge);
            item.ProviderBindings.Add(new AuthoringResourceProviderBinding
            {
                ProviderId = AuthoringResourceProviderIds.ExternalImportStaging,
                BindingKind = AuthoringResourceBindingKind.ExternalSource,
                BindingKeyKind = AuthoringResourceBindingKeyKinds.ExternalSourcePath,
                DisplayValue = displayPath,
                IsPrimary = true,
                ProviderResourceKey = displayPath,
                ExternalSourcePath = displayPath,
                AssetType = candidate.Kind,
                Hash = sourceHash
            });

            if (!candidate.Supported)
                AddItemDiagnostic(item, CharacterAuthoringValidationSeverity.Warning, AuthoringResourceDiagnosticCodes.UnsupportedFormat, "Unsupported import format: ." + extension, "Use .anim, .fbx, .glb, .gltf, .png, .jpg, .jpeg, .tga, .wav, .ogg, or .json.");
            if (tooLarge)
                AddItemDiagnostic(item, CharacterAuthoringValidationSeverity.Error, AuthoringResourceDiagnosticCodes.SourceFileTooLarge, "File exceeds the staging size limit.", "Import a smaller file or increase the staging limit.");
            if (duplicate)
                AddItemDiagnostic(item, CharacterAuthoringValidationSeverity.Warning, AuthoringResourceDiagnosticCodes.SourceHashDuplicate, "Duplicate source hash already exists: " + seenHashes[sourceHash], "Skip this file or replace the existing resource source explicitly.");

            if (!string.IsNullOrWhiteSpace(sourceHash) && !seenHashes.ContainsKey(sourceHash))
                seenHashes[sourceHash] = displayPath;

            return item;
        }

        private static void AddMetadata(
            AuthoringResourceItem item,
            AuthoringExternalImportStagingFile file,
            string displayPath,
            string extension,
            string sourceHash,
            long size,
            ResourceTypeCandidate candidate,
            bool selectable,
            bool duplicate,
            bool tooLarge)
        {
            AuthoringResourceProviderUtilities.AddIfPresent(item.Metadata, "fileName", file.FileName);
            AuthoringResourceProviderUtilities.AddIfPresent(item.Metadata, "relativePath", displayPath);
            AuthoringResourceProviderUtilities.AddIfPresent(item.Metadata, "extension", extension);
            AuthoringResourceProviderUtilities.AddIfPresent(item.Metadata, "sourceHash", sourceHash);
            AuthoringResourceProviderUtilities.AddIfPresent(item.Metadata, "detectedKind", candidate.Kind);
            AuthoringResourceProviderUtilities.AddIfPresent(item.Metadata, "detectedUsage", candidate.Usage);
            AuthoringResourceProviderUtilities.AddIfPresent(item.Metadata, "promoteTarget", candidate.PromoteTarget);
            item.Metadata["sizeBytes"] = size.ToString(System.Globalization.CultureInfo.InvariantCulture);
            item.Metadata["supported"] = candidate.Supported ? "true" : "false";
            item.Metadata["selectable"] = selectable ? "true" : "false";
            item.Metadata["duplicateSourceHash"] = duplicate ? "true" : "false";
            item.Metadata["tooLarge"] = tooLarge ? "true" : "false";
        }

        private static void AddExistingResourceHashes(Dictionary<string, string> hashes, CharacterPackageResourceCatalog catalog)
        {
            if (hashes == null || catalog == null || catalog.Entries == null)
                return;

            for (int i = 0; i < catalog.Entries.Count; i++)
            {
                CharacterPackageResourceEntry entry = catalog.Entries[i];
                if (entry == null)
                    continue;

                string hash = CharacterPackageResourcePipeline.GetDeclaredContentHash(entry);
                if (!string.IsNullOrWhiteSpace(hash) && !hashes.ContainsKey(hash))
                    hashes[hash] = AuthoringResourceProviderUtilities.FirstNonEmpty(entry.ResourceKey, entry.StableId, entry.LocalId);
            }
        }

        private static void AddItemDiagnostic(AuthoringResourceItem item, CharacterAuthoringValidationSeverity severity, string code, string message, string suggestedFix)
        {
            item.Diagnostics.Add(new AuthoringResourceDiagnostic
            {
                Severity = severity,
                Code = code ?? string.Empty,
                ResourceId = item.ResourceId,
                ResourceStableId = item.StableId,
                ProviderId = AuthoringResourceProviderIds.ExternalImportStaging,
                SourceConfigKind = "externalImportStaging",
                SourceField = "source",
                Message = message ?? string.Empty,
                SuggestedFix = suggestedFix ?? string.Empty
            });
        }

        private static bool IsIgnoredFile(string relativePath, string fileName)
        {
            string path = relativePath ?? string.Empty;
            string name = string.IsNullOrWhiteSpace(fileName) ? Path.GetFileName(path) : fileName;
            string lowerName = (name ?? string.Empty).ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(lowerName))
                return true;
            if (lowerName.EndsWith(".meta", StringComparison.Ordinal) || lowerName == ".ds_store" || lowerName == "thumbs.db")
                return true;

            string[] segments = path.Replace('\\', '/').Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < segments.Length; i++)
            {
                if (segments[i].StartsWith(".", StringComparison.Ordinal) && segments[i] != ".")
                    return true;
            }

            return false;
        }

        private static string ResolveSourceHash(AuthoringExternalImportStagingFile file)
        {
            if (file == null)
                return string.Empty;
            if (!string.IsNullOrWhiteSpace(file.SourceHash))
                return file.SourceHash;
            if (string.IsNullOrWhiteSpace(file.BytesBase64))
                return string.Empty;

            try
            {
                return CharacterPackageHashUtility.ComputeSha256(Convert.FromBase64String(file.BytesBase64));
            }
            catch (FormatException)
            {
                return string.Empty;
            }
        }

        private static long GetDecodedSize(string bytesBase64)
        {
            if (string.IsNullOrWhiteSpace(bytesBase64))
                return 0;
            try
            {
                return Convert.FromBase64String(bytesBase64).LongLength;
            }
            catch (FormatException)
            {
                return 0;
            }
        }

        private static string GetDisplayPath(AuthoringExternalImportStagingFile file)
        {
            return AuthoringResourceProviderUtilities.FirstNonEmpty(file != null ? file.RelativePath : string.Empty, file != null ? file.FileName : string.Empty).Replace('\\', '/');
        }

        private static ResourceTypeCandidate DetectCandidate(string extension, string displayPath)
        {
            switch (extension)
            {
                case "fbx":
                case "glb":
                case "gltf":
                    if (LooksLikeAnimationPath(displayPath))
                        return new ResourceTypeCandidate(true, CharacterPackageResourceTypeIds.Animation, CharacterPackageResourceUsageIds.AnimationClipGroup, "unityAsset", "animation");
                    return new ResourceTypeCandidate(true, CharacterPackageResourceTypeIds.Model, CharacterPackageResourceUsageIds.PreviewMesh, "unityAsset", "model");
                case "anim":
                    return new ResourceTypeCandidate(true, CharacterPackageResourceTypeIds.Animation, CharacterPackageResourceUsageIds.AnimationClipGroup, "unityAsset", "animation");
                case "png":
                case "jpg":
                case "jpeg":
                case "tga":
                    return new ResourceTypeCandidate(true, CharacterPackageResourceTypeIds.Texture, CharacterPackageResourceUsageIds.Texture, "unityAsset", "texture");
                case "wav":
                case "ogg":
                    return new ResourceTypeCandidate(true, CharacterPackageResourceTypeIds.Audio, CharacterPackageResourceUsageIds.AudioCue, "unityAsset", "audio");
                case "json":
                    return new ResourceTypeCandidate(true, CharacterPackageResourceTypeIds.Config, CharacterPackageResourceUsageIds.CharacterConfig, "characterPackage", "config");
                default:
                    return new ResourceTypeCandidate(false, "unsupported", string.Empty, string.Empty, "unsupported");
            }
        }

        private static bool LooksLikeAnimationPath(string displayPath)
        {
            string path = (displayPath ?? string.Empty).Replace('\\', '/').ToLowerInvariant();
            return path.Contains("/animation/")
                || path.Contains("/animations/")
                || path.Contains("/animationclips/")
                || path.Contains("_animation")
                || path.Contains(".animation");
        }

        private readonly struct ResourceTypeCandidate
        {
            public ResourceTypeCandidate(bool supported, string kind, string usage, string promoteTarget, string tag)
            {
                Supported = supported;
                Kind = kind ?? string.Empty;
                Usage = usage ?? string.Empty;
                PromoteTarget = promoteTarget ?? string.Empty;
                Tag = tag ?? string.Empty;
            }

            public bool Supported { get; }
            public string Kind { get; }
            public string Usage { get; }
            public string PromoteTarget { get; }
            public string Tag { get; }
        }
    }
}
