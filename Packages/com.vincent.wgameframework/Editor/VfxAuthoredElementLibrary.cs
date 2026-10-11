using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEditor;

namespace MxFramework.Editor
{
    [Serializable]
    internal sealed class VfxAuthoredElementLibraryView
    {
        internal const string CurrentSchemaVersion = "mx.vfx.authored-elements.v1";

        [JsonProperty("schemaVersion")] public string SchemaVersion = CurrentSchemaVersion;
        [JsonProperty("libraryHash")] public string LibraryHash = string.Empty;
        [JsonProperty("elements")] public List<VfxSourceElementView> Elements = new List<VfxSourceElementView>();
    }

    [Serializable]
    internal sealed class VfxAuthoredElementManifestView
    {
        internal const string CurrentSchemaVersion = "mx.vfx.authored-element-manifest.v1";

        [JsonProperty("schemaVersion")] public string SchemaVersion = CurrentSchemaVersion;
        [JsonProperty("libraryHash")] public string LibraryHash = string.Empty;
        [JsonProperty("elements")] public List<VfxAuthoredElementManifestEntryView> Elements =
            new List<VfxAuthoredElementManifestEntryView>();
    }

    [Serializable]
    internal sealed class VfxAuthoredElementManifestEntryView
    {
        [JsonProperty("elementId")] public string ElementId = string.Empty;
        [JsonProperty("elementHash")] public string ElementHash = string.Empty;
        [JsonProperty("path")] public string Path = string.Empty;
    }

    [Serializable]
    internal sealed class VfxAuthoredElementDocumentView
    {
        internal const string CurrentSchemaVersion = "mx.vfx.authored-element.v1";

        [JsonProperty("schemaVersion")] public string SchemaVersion = CurrentSchemaVersion;
        [JsonProperty("elementHash")] public string ElementHash = string.Empty;
        [JsonProperty("element")] public VfxSourceElementView Element;
    }

    /// <summary>Persists the Editor-only library of explicitly authored source-derived VFX elements.</summary>
    /// <remarks>
    /// AllocByDesign. Mutations are explicit user actions. The manifest is the commit point and each Element is
    /// stored in an immutable content-addressed shard, so an interrupted write cannot partially replace the library.
    /// </remarks>
    internal sealed class VfxAuthoredElementLibraryService
    {
        internal const string LibraryPath = "Assets/Config/MxFramework/Vfx/vfx_authored_elements.json";
        internal const string ManifestPath = "Assets/Config/MxFramework/Vfx/AuthoredElements/manifest.json";
        internal const string ElementsDirectory = "Assets/Config/MxFramework/Vfx/AuthoredElements/Elements";

        private readonly string _legacyPath;
        private readonly string _manifestPath;
        private readonly string _storeRoot;
        private readonly string _elementsDirectory;

        internal string StoreManifestPath => _manifestPath;
        internal string StoreElementsDirectory => _elementsDirectory;

        internal VfxAuthoredElementLibraryService(string path = LibraryPath)
        {
            _legacyPath = NormalizePath(path ?? LibraryPath);
            if (string.Equals(_legacyPath, LibraryPath, StringComparison.Ordinal))
            {
                _manifestPath = ManifestPath;
                _storeRoot = NormalizePath(System.IO.Path.GetDirectoryName(ManifestPath));
                _elementsDirectory = ElementsDirectory;
                return;
            }

            string directory = NormalizePath(System.IO.Path.GetDirectoryName(_legacyPath));
            string storeName = System.IO.Path.GetFileNameWithoutExtension(_legacyPath) + ".store";
            _storeRoot = CombinePath(directory, storeName);
            _manifestPath = CombinePath(_storeRoot, "manifest.json");
            _elementsDirectory = CombinePath(_storeRoot, "Elements");
        }

        internal VfxAuthoredElementLibraryView Load(out string message)
        {
            if (File.Exists(_manifestPath))
                return LoadSharded(out message);

            if (!File.Exists(_legacyPath))
            {
                message = "全局派生素材库尚未创建。";
                return CreateEmpty();
            }

            VfxAuthoredElementLibraryView legacy = LoadLegacy(out message);
            if (legacy == null) return null;
            if (!WriteSharded(legacy, null, out message)) return null;
            message = "已将 " + legacy.Elements.Count + " 个全局素材迁移为独立文件；旧单文件保留为只读兼容备份。";
            return legacy;
        }

        internal bool SaveElement(
            VfxSourceElementView proposed,
            string expectedLibraryHash,
            bool preserveManualFields,
            out VfxSourceElementView saved,
            out bool created,
            out string message)
        {
            saved = null;
            created = false;
            if (proposed == null || string.IsNullOrWhiteSpace(proposed.ElementId))
            {
                message = "待保存的派生素材无效。";
                return false;
            }

            VfxAuthoredElementLibraryView library = Load(out message);
            if (library == null || !MatchesGuard(library, expectedLibraryHash, out message)) return false;
            VfxAuthoredElementManifestView previousManifest = LoadManifestForMutation(out message);
            if (previousManifest == null && File.Exists(_manifestPath)) return false;

            int index = library.Elements.FindIndex(value =>
                string.Equals(value.ElementId, proposed.ElementId, StringComparison.Ordinal));
            if (index < 0)
            {
                saved = Clone(proposed);
                library.Elements.Add(saved);
                created = true;
            }
            else
            {
                VfxSourceElementView existing = library.Elements[index];
                if (!(existing.MemberComponentIds ?? new List<string>()).SequenceEqual(
                        proposed.MemberComponentIds ?? new List<string>(), StringComparer.Ordinal))
                {
                    message = "稳定 ID 对应的成员集合发生变化，已阻止覆盖。";
                    return false;
                }

                saved = Clone(proposed);
                if (preserveManualFields)
                {
                    saved.DisplayName = existing.DisplayName;
                    saved.Roles = new List<string>(existing.Roles ?? new List<string>());
                    saved.Family = existing.Family;
                    saved.Theme = existing.Theme;
                    saved.Shape = existing.Shape;
                    saved.Anchor = existing.Anchor;
                    saved.Lifecycle = existing.Lifecycle;
                    saved.TimingPhase = existing.TimingPhase;
                    saved.QualityTier = existing.QualityTier;
                }
                library.Elements[index] = saved;
            }

            library.Elements.Sort((left, right) => string.CompareOrdinal(left.ElementId, right.ElementId));
            if (!WriteSharded(library, previousManifest, out message)) return false;
            message = created ? "已创建全局派生素材。" : "已更新全局派生素材证据。";
            return true;
        }

        internal bool Rename(string elementId, string displayName, string expectedLibraryHash, out string message)
        {
            string normalized = (displayName ?? string.Empty).Trim();
            if (normalized.Length == 0 || normalized.Length > 32)
            {
                message = "卡片展示名必须为 1–32 个字符。";
                return false;
            }

            VfxAuthoredElementLibraryView library = Load(out message);
            if (library == null || !MatchesGuard(library, expectedLibraryHash, out message)) return false;
            VfxAuthoredElementManifestView previousManifest = LoadManifestForMutation(out message);
            if (previousManifest == null && File.Exists(_manifestPath)) return false;
            VfxSourceElementView element = library.Elements.FirstOrDefault(value =>
                string.Equals(value.ElementId, elementId, StringComparison.Ordinal));
            if (element == null)
            {
                message = "全局派生素材不存在。";
                return false;
            }

            element.DisplayName = normalized;
            if (!WriteSharded(library, previousManifest, out message)) return false;
            message = "已重命名全局派生素材。";
            return true;
        }

        internal bool Delete(
            string elementId,
            string expectedLibraryHash,
            out List<string> referencingDrafts,
            out string message)
        {
            referencingDrafts = FindReferencingDrafts(elementId);
            if (referencingDrafts.Count > 0)
            {
                message = "该素材仍被以下草稿引用：" + string.Join("、", referencingDrafts);
                return false;
            }

            VfxAuthoredElementLibraryView library = Load(out message);
            if (library == null || !MatchesGuard(library, expectedLibraryHash, out message)) return false;
            VfxAuthoredElementManifestView previousManifest = LoadManifestForMutation(out message);
            if (previousManifest == null && File.Exists(_manifestPath)) return false;
            int removed = library.Elements.RemoveAll(value =>
                string.Equals(value.ElementId, elementId, StringComparison.Ordinal));
            if (removed == 0)
            {
                message = "全局派生素材不存在。";
                return false;
            }

            if (!WriteSharded(library, previousManifest, out message)) return false;
            message = "已删除全局派生素材。";
            return true;
        }

        internal List<string> FindReferencingDrafts(string elementId)
        {
            var references = new List<string>();
            foreach (string path in new VfxCompositionDraftService().ListDrafts())
            {
                try
                {
                    VfxCompositionDraftView draft = JsonConvert.DeserializeObject<VfxCompositionDraftView>(
                        File.ReadAllText(path), VfxCompositionDraftService.JsonSettings);
                    if (draft?.Layers?.Any(layer =>
                            string.Equals(layer.ElementId, elementId, StringComparison.Ordinal)) == true)
                        references.Add(string.IsNullOrWhiteSpace(draft.DisplayName) ? draft.DraftId : draft.DisplayName);
                }
                catch (Exception exception) when (exception is IOException || exception is JsonException)
                {
                    references.Add(System.IO.Path.GetFileNameWithoutExtension(path) + "（无法检查）");
                }
            }
            return references;
        }

        internal static string ComputeLibraryHash(VfxAuthoredElementLibraryView library)
        {
            var hashView = new VfxAuthoredElementLibraryView
            {
                SchemaVersion = library?.SchemaVersion ?? VfxAuthoredElementLibraryView.CurrentSchemaVersion,
                LibraryHash = string.Empty,
                Elements = (library?.Elements ?? new List<VfxSourceElementView>())
                    .OrderBy(value => value.ElementId, StringComparer.Ordinal)
                    .Select(Clone)
                    .ToList()
            };
            return VfxSourceElementCurationService.Hash(
                JsonConvert.SerializeObject(hashView, VfxCompositionDraftService.JsonSettings));
        }

        internal static string ComputeElementHash(VfxSourceElementView element)
        {
            return VfxSourceElementCurationService.Hash(
                JsonConvert.SerializeObject(Clone(element), Formatting.None, VfxCompositionDraftService.JsonSettings));
        }

        private VfxAuthoredElementLibraryView LoadLegacy(out string message)
        {
            try
            {
                VfxAuthoredElementLibraryView library = JsonConvert.DeserializeObject<VfxAuthoredElementLibraryView>(
                    File.ReadAllText(_legacyPath), VfxCompositionDraftService.JsonSettings) ?? CreateEmpty();
                ValidateLibrary(library);
                message = "已读取旧版全局素材库。";
                return library;
            }
            catch (Exception exception) when (
                exception is IOException || exception is JsonException || exception is InvalidDataException)
            {
                message = exception.Message;
                return null;
            }
        }

        private VfxAuthoredElementLibraryView LoadSharded(out string message)
        {
            try
            {
                VfxAuthoredElementManifestView manifest = ReadAndValidateManifest();
                var library = new VfxAuthoredElementLibraryView();
                for (int i = 0; i < manifest.Elements.Count; i++)
                {
                    VfxAuthoredElementManifestEntryView entry = manifest.Elements[i];
                    string shardPath = ResolveShardPath(entry);
                    VfxAuthoredElementDocumentView document =
                        JsonConvert.DeserializeObject<VfxAuthoredElementDocumentView>(
                            File.ReadAllText(shardPath), VfxCompositionDraftService.JsonSettings);
                    if (document == null
                        || !string.Equals(
                            document.SchemaVersion,
                            VfxAuthoredElementDocumentView.CurrentSchemaVersion,
                            StringComparison.Ordinal))
                        throw new InvalidDataException("全局素材分片 schema 无效：" + entry.ElementId);
                    if (document.Element == null
                        || !string.Equals(document.Element.ElementId, entry.ElementId, StringComparison.Ordinal)
                        || !string.Equals(document.ElementHash, entry.ElementHash, StringComparison.Ordinal)
                        || !string.Equals(ComputeElementHash(document.Element), entry.ElementHash, StringComparison.Ordinal))
                        throw new InvalidDataException("全局素材分片 hash 或身份校验失败：" + entry.ElementId);
                    library.Elements.Add(document.Element);
                }

                library.Elements.Sort((left, right) => string.CompareOrdinal(left.ElementId, right.ElementId));
                ValidateLibrary(library);
                if (!string.Equals(library.LibraryHash, manifest.LibraryHash, StringComparison.Ordinal))
                    throw new InvalidDataException("全局素材 manifest 的 libraryHash 校验失败，已阻止加载。");
                message = "已加载 " + library.Elements.Count + " 个独立存储的手工派生全局素材。";
                return library;
            }
            catch (Exception exception) when (
                exception is IOException
                || exception is UnauthorizedAccessException
                || exception is JsonException
                || exception is InvalidDataException)
            {
                message = exception.Message;
                return null;
            }
        }

        private VfxAuthoredElementManifestView LoadManifestForMutation(out string message)
        {
            if (!File.Exists(_manifestPath))
            {
                message = string.Empty;
                return new VfxAuthoredElementManifestView { LibraryHash = ComputeLibraryHash(CreateEmpty()) };
            }

            try
            {
                VfxAuthoredElementManifestView manifest = ReadAndValidateManifest();
                message = string.Empty;
                return manifest;
            }
            catch (Exception exception) when (
                exception is IOException || exception is JsonException || exception is InvalidDataException)
            {
                message = exception.Message;
                return null;
            }
        }

        private VfxAuthoredElementManifestView ReadAndValidateManifest()
        {
            VfxAuthoredElementManifestView manifest = JsonConvert.DeserializeObject<VfxAuthoredElementManifestView>(
                File.ReadAllText(_manifestPath), VfxCompositionDraftService.JsonSettings);
            if (manifest == null
                || !string.Equals(
                    manifest.SchemaVersion,
                    VfxAuthoredElementManifestView.CurrentSchemaVersion,
                    StringComparison.Ordinal))
                throw new InvalidDataException("不支持的全局素材 manifest schema。");
            manifest.Elements ??= new List<VfxAuthoredElementManifestEntryView>();
            if (manifest.Elements.Any(value =>
                    value == null
                    || string.IsNullOrWhiteSpace(value.ElementId)
                    || string.IsNullOrWhiteSpace(value.ElementHash)
                    || string.IsNullOrWhiteSpace(value.Path))
                || manifest.Elements.Select(value => value.ElementId)
                    .Distinct(StringComparer.Ordinal).Count() != manifest.Elements.Count
                || manifest.Elements.Select(value => value.Path)
                    .Distinct(StringComparer.Ordinal).Count() != manifest.Elements.Count)
                throw new InvalidDataException("全局素材 manifest 包含空值或重复项。");

            for (int i = 0; i < manifest.Elements.Count; i++)
            {
                VfxAuthoredElementManifestEntryView entry = manifest.Elements[i];
                string expected = ShardRelativePath(entry.ElementId, entry.ElementHash);
                if (!string.Equals(NormalizePath(entry.Path), expected, StringComparison.Ordinal))
                    throw new InvalidDataException("全局素材 manifest 分片路径不符合内容寻址规则：" + entry.ElementId);
                string resolved = ResolveShardPath(entry);
                if (!File.Exists(resolved))
                    throw new InvalidDataException("全局素材分片缺失：" + entry.ElementId);
            }
            return manifest;
        }

        private bool WriteSharded(
            VfxAuthoredElementLibraryView library,
            VfxAuthoredElementManifestView previousManifest,
            out string message)
        {
            try
            {
                library.LibraryHash = string.Empty;
                ValidateLibrary(library);
                library.Elements.Sort((left, right) => string.CompareOrdinal(left.ElementId, right.ElementId));
                library.LibraryHash = ComputeLibraryHash(library);
                var manifest = new VfxAuthoredElementManifestView { LibraryHash = library.LibraryHash };
                var createdShards = new List<string>();

                for (int i = 0; i < library.Elements.Count; i++)
                {
                    VfxSourceElementView element = library.Elements[i];
                    string elementHash = ComputeElementHash(element);
                    string relativePath = ShardRelativePath(element.ElementId, elementHash);
                    string shardPath = CombinePath(_storeRoot, relativePath);
                    var document = new VfxAuthoredElementDocumentView
                    {
                        ElementHash = elementHash,
                        Element = Clone(element)
                    };
                    string json = JsonConvert.SerializeObject(
                        document, Formatting.Indented, VfxCompositionDraftService.JsonSettings);
                    if (!File.Exists(shardPath))
                    {
                        WriteNewImmutableFile(shardPath, json);
                        createdShards.Add(shardPath);
                    }
                    else
                    {
                        VfxAuthoredElementDocumentView existing =
                            JsonConvert.DeserializeObject<VfxAuthoredElementDocumentView>(
                                File.ReadAllText(shardPath), VfxCompositionDraftService.JsonSettings);
                        if (existing == null
                            || !string.Equals(existing.ElementHash, elementHash, StringComparison.Ordinal)
                            || !string.Equals(ComputeElementHash(existing.Element), elementHash, StringComparison.Ordinal))
                            throw new InvalidDataException("内容寻址分片与现有文件冲突：" + element.ElementId);
                    }

                    manifest.Elements.Add(new VfxAuthoredElementManifestEntryView
                    {
                        ElementId = element.ElementId,
                        ElementHash = elementHash,
                        Path = relativePath
                    });
                }

                manifest.Elements.Sort((left, right) => string.CompareOrdinal(left.ElementId, right.ElementId));
                string manifestJson = JsonConvert.SerializeObject(
                    manifest, Formatting.Indented, VfxCompositionDraftService.JsonSettings);
                WriteAtomic(_manifestPath, manifestJson);
                DeleteUnreferencedShards(previousManifest, manifest);
                ImportChangedAssets(createdShards.Append(_manifestPath));
                message = string.Empty;
                return true;
            }
            catch (Exception exception) when (
                exception is IOException
                || exception is UnauthorizedAccessException
                || exception is JsonException
                || exception is InvalidDataException)
            {
                message = "写入全局派生素材库失败：" + exception.Message;
                return false;
            }
        }

        private string ResolveShardPath(VfxAuthoredElementManifestEntryView entry)
        {
            string resolved = CombinePath(_storeRoot, entry.Path);
            string full = System.IO.Path.GetFullPath(resolved);
            string root = System.IO.Path.GetFullPath(_elementsDirectory)
                .TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar)
                + System.IO.Path.DirectorySeparatorChar;
            if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("全局素材分片路径越出素材库目录：" + entry.ElementId);
            return NormalizePath(full);
        }

        private static string ShardRelativePath(string elementId, string elementHash)
        {
            string idHash = RemoveHashPrefix(VfxSourceElementCurationService.Hash(elementId));
            string contentHash = RemoveHashPrefix(elementHash);
            return "Elements/" + idHash.Substring(0, 2) + "/"
                + idHash.Substring(0, 32) + "." + contentHash.Substring(0, 32) + ".json";
        }

        private static string RemoveHashPrefix(string value)
        {
            return value != null && value.StartsWith("sha256:", StringComparison.Ordinal)
                ? value.Substring("sha256:".Length)
                : value ?? string.Empty;
        }

        private static void ValidateLibrary(VfxAuthoredElementLibraryView library)
        {
            if (library == null
                || !string.Equals(
                    library.SchemaVersion,
                    VfxAuthoredElementLibraryView.CurrentSchemaVersion,
                    StringComparison.Ordinal))
                throw new InvalidDataException("不支持的全局派生素材库 schema。");
            library.Elements ??= new List<VfxSourceElementView>();
            if (library.Elements.Any(value => value == null || string.IsNullOrWhiteSpace(value.ElementId))
                || library.Elements.Select(value => value.ElementId)
                    .Distinct(StringComparer.Ordinal).Count() != library.Elements.Count)
                throw new InvalidDataException("全局派生素材库包含空 ID 或重复 ID。");
            string computed = ComputeLibraryHash(library);
            if (!string.IsNullOrWhiteSpace(library.LibraryHash)
                && !string.Equals(library.LibraryHash, computed, StringComparison.Ordinal))
                throw new InvalidDataException("全局派生素材库 hash 校验失败，已阻止加载。");
            library.LibraryHash = computed;
        }

        private static void WriteNewImmutableFile(string path, string contents)
        {
            string fullPath = System.IO.Path.GetFullPath(path);
            string directory = System.IO.Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
            string temporary = fullPath + ".tmp." + Guid.NewGuid().ToString("N");
            try
            {
                File.WriteAllText(temporary, contents);
                File.Move(temporary, fullPath);
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }

        private static void WriteAtomic(string path, string contents)
        {
            string fullPath = System.IO.Path.GetFullPath(path);
            string directory = System.IO.Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
            string temporary = fullPath + ".tmp." + Guid.NewGuid().ToString("N");
            string backup = fullPath + ".bak." + Guid.NewGuid().ToString("N");
            try
            {
                File.WriteAllText(temporary, contents);
                if (File.Exists(fullPath)) File.Replace(temporary, fullPath, backup, true);
                else File.Move(temporary, fullPath);
                if (File.Exists(backup)) File.Delete(backup);
            }
            catch
            {
                if (File.Exists(backup))
                {
                    if (File.Exists(fullPath)) File.Delete(fullPath);
                    File.Move(backup, fullPath);
                }
                throw;
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
                if (File.Exists(backup)) File.Delete(backup);
            }
        }

        private void DeleteUnreferencedShards(
            VfxAuthoredElementManifestView previous,
            VfxAuthoredElementManifestView current)
        {
            if (previous?.Elements == null) return;
            var retained = new HashSet<string>(
                current.Elements.Select(value => NormalizePath(value.Path)),
                StringComparer.Ordinal);
            for (int i = 0; i < previous.Elements.Count; i++)
            {
                VfxAuthoredElementManifestEntryView oldEntry = previous.Elements[i];
                if (oldEntry == null || retained.Contains(NormalizePath(oldEntry.Path))) continue;
                try
                {
                    string oldPath = ResolveShardPath(oldEntry);
                    if (File.Exists(oldPath)) File.Delete(oldPath);
                    string metaPath = oldPath + ".meta";
                    if (File.Exists(metaPath)) File.Delete(metaPath);
                }
                catch (IOException)
                {
                    // The old shard is an unreferenced immutable orphan and is safe to clean on a later mutation.
                }
                catch (UnauthorizedAccessException)
                {
                    // The committed manifest remains valid even if optional orphan cleanup is denied.
                }
            }
        }

        private static void ImportChangedAssets(IEnumerable<string> paths)
        {
            bool hasAsset = false;
            foreach (string path in paths.Distinct(StringComparer.Ordinal))
            {
                if (!NormalizePath(path).StartsWith("Assets/", StringComparison.Ordinal)) continue;
                hasAsset = true;
                AssetDatabase.ImportAsset(NormalizePath(path), ImportAssetOptions.ForceSynchronousImport);
            }
            if (hasAsset) AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        private static bool MatchesGuard(
            VfxAuthoredElementLibraryView library,
            string expected,
            out string message)
        {
            if (expected != null && !string.Equals(expected, library.LibraryHash, StringComparison.Ordinal))
            {
                message = "全局派生素材库已被其他操作修改，请刷新后重试。";
                return false;
            }
            message = string.Empty;
            return true;
        }

        private static VfxAuthoredElementLibraryView CreateEmpty()
        {
            var library = new VfxAuthoredElementLibraryView();
            library.LibraryHash = ComputeLibraryHash(library);
            return library;
        }

        private static VfxSourceElementView Clone(VfxSourceElementView value)
        {
            if (value == null) return null;
            return JsonConvert.DeserializeObject<VfxSourceElementView>(
                JsonConvert.SerializeObject(value, VfxCompositionDraftService.JsonSettings),
                VfxCompositionDraftService.JsonSettings);
        }

        private static string NormalizePath(string path)
        {
            return (path ?? string.Empty).Replace('\\', '/').TrimEnd('/');
        }

        private static string CombinePath(string left, string right)
        {
            if (string.IsNullOrWhiteSpace(left)) return NormalizePath(right);
            return NormalizePath(System.IO.Path.Combine(left, right ?? string.Empty));
        }
    }
}
