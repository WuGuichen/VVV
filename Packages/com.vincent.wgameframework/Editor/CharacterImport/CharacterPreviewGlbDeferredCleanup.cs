using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Editor.CharacterImport
{
    /// <summary>
    /// Unity 重建完成后的专用预览 GLB 收口：引用核对 + 定向删除。
    /// </summary>
    /// <remarks>
    /// 这是"仅保留在制作侧"任务的阶段 B。CLI 阶段只把候选标记为 <c>Deferred</c>，
    /// 不做任何删除；真正的删除必须等到 Unity 重建 Prefab/目录之后，并且引用核对通过。
    ///
    /// 两条导入入口（菜单/批处理与 <c>CharacterPackageImportRequestBridge</c>）都经由
    /// <c>CharacterPackageImportCommand.SynchronizeImportedAssetsWithReport</c>，
    /// 因此把收口放在该方法的尾部即可让两条路径共用同一步骤。
    ///
    /// 只删除 <c>previews/</c> 下、且引用核对确认无引用的 <c>.glb</c> 及其同名 <c>.meta</c>；
    /// 不递归删除目录，不触碰缩略图、正式模型、Prefab 与烘焙产物。
    /// </remarks>
    internal static class CharacterPreviewGlbDeferredCleanup
    {
        internal const string ResultFormat = "mx.characterPreviewGlbCleanup.v1";

        private const string PreviewFolderName = "previews";

        /// <summary>JSON may store GUID strings outside Unity's dependency graph. Native/binary assets use AssetDatabase dependencies.</summary>
        private static readonly string[] ReferenceScanExtensions =
        {
            ".json"
        };

        /// <summary>引用面扫描的总字节上限；超出即视为无法完成核对，保留文件并报告。</summary>
        private const long ReferenceScanByteBudget = 256L * 1024L * 1024L;

        internal sealed class Result
        {
            public string Format = ResultFormat;
            public string ImportedRoot = string.Empty;
            public int CandidateCount;
            public int DeletedCount;
            public int BlockedCount;
            public int UnverifiedCount;
            public List<string> Messages = new List<string>();

            public bool Succeeded
            {
                get { return BlockedCount == 0 && UnverifiedCount == 0; }
            }

            public string ToLogLine()
            {
                return Format
                    + " candidates=" + CandidateCount
                    + " deleted=" + DeletedCount
                    + " blocked=" + BlockedCount
                    + " unverified=" + UnverifiedCount;
            }
        }

        /// <summary>
        /// 在 Unity 重建完成后调用。返回 null 表示没有候选，无需任何动作。
        /// </summary>
        internal static Result Execute(string importedRoot)
        {
            if (string.IsNullOrWhiteSpace(importedRoot))
                return null;

            string normalizedRoot = importedRoot.Replace('\\', '/').TrimEnd('/');
            string previewRoot = normalizedRoot + "/" + PreviewFolderName;
            if (!Directory.Exists(previewRoot))
                return null;

            string[] candidates;
            try { candidates = ReadDeferredCandidates(normalizedRoot); }
            catch (Exception exception)
            {
                return new Result { ImportedRoot = normalizedRoot, UnverifiedCount = 1,
                    Messages = new List<string> { "Unverified migration candidates: " + exception.Message } };
            }
            if (candidates.Length == 0)
                return null;

            var result = new Result { ImportedRoot = normalizedRoot, CandidateCount = candidates.Length };
            bool deletedAny = false;

            for (int i = 0; i < candidates.Length; i++)
            {
                string assetPath = candidates[i].Replace('\\', '/');
                string metaPath = assetPath + ".meta";
                if (!File.Exists(assetPath) && !File.Exists(metaPath)) continue;
                if (!File.Exists(assetPath))
                {
                    result.UnverifiedCount++;
                    result.Messages.Add("Unverified (asset missing but meta remains): " + assetPath);
                    continue;
                }
                string guid = ReadMetaGuid(metaPath);
                if (string.IsNullOrWhiteSpace(guid))
                {
                    result.UnverifiedCount++;
                    result.Messages.Add("Unverified (no meta guid): " + assetPath);
                    continue;
                }

                string assetHash = ComputeFileHash(assetPath);
                string metaHash = ComputeFileHash(metaPath);
                string referencingAsset;
                string scanError;
                bool referenced = TryFindReference(guid, assetPath, out referencingAsset, out scanError);
                if (!string.IsNullOrEmpty(scanError))
                {
                    // 无法完成核对时保留文件，不猜测可删。
                    result.UnverifiedCount++;
                    result.Messages.Add("Unverified (" + scanError + "): " + assetPath);
                    continue;
                }

                if (referenced)
                {
                    result.BlockedCount++;
                    result.Messages.Add("Blocked (still referenced by " + referencingAsset + "): " + assetPath);
                    continue;
                }

                // A scan is not a lock: reject a file changed while references were being checked.
                if (assetHash != ComputeFileHash(assetPath) || metaHash != ComputeFileHash(metaPath))
                {
                    result.UnverifiedCount++;
                    result.Messages.Add("Unverified (asset changed during reference scan): " + assetPath);
                    continue;
                }
                string backup = Path.Combine("Temp", "CharacterPreviewGlbMigration", "unity-cleanup",
                    Path.GetFileName(normalizedRoot), Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(backup);
                File.Copy(assetPath, Path.Combine(backup, Path.GetFileName(assetPath)));
                File.Copy(metaPath, Path.Combine(backup, Path.GetFileName(metaPath)));
                File.WriteAllText(Path.Combine(backup, "manifest.json"), new JObject {
                    ["assetPath"] = assetPath, ["assetHash"] = assetHash, ["metaHash"] = metaHash, ["guid"] = guid
                }.ToString());

                if (AssetDatabase.DeleteAsset(assetPath))
                {
                    result.DeletedCount++;
                    deletedAny = true;
                    result.Messages.Add("Deleted: " + assetPath + " (backup: " + backup.Replace('\\', '/') + ")");
                }
                else
                {
                    result.UnverifiedCount++;
                    result.Messages.Add("Unverified (AssetDatabase.DeleteAsset returned false): " + assetPath);
                }
            }

            if (deletedAny)
            {
                // 让 Unity 目录与本地缓存丢弃已删除资产的旧记录。
                AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            }

            return result;
        }

        private static string[] ReadDeferredCandidates(string importedRoot)
        {
            string reportPath = Path.Combine(importedRoot, "package_cache", "import_report.json");
            if (!File.Exists(reportPath)) return Array.Empty<string>();
            JArray operations = JObject.Parse(File.ReadAllText(reportPath))["operations"] as JArray;
            if (operations == null) throw new InvalidDataException("Import report has no operations.");
            string root = Path.GetFullPath(importedRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string assets = Path.GetFullPath(Application.dataPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (!root.StartsWith(assets + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Imported package must be inside Assets.");
            string preview = Path.Combine(root, PreviewFolderName) + Path.DirectorySeparatorChar;
            var paths = new SortedSet<string>(StringComparer.Ordinal);
            foreach (JObject operation in operations)
            {
                if ((string)operation["kind"] != "exportCleanup" || (string)operation["action"] != "Deferred") continue;
                string target = (string)operation["targetPath"] ?? string.Empty;
                if (string.IsNullOrWhiteSpace(target) || Path.IsPathRooted(target))
                    throw new InvalidDataException("Deferred target must be a project-relative path.");
                string full = Path.GetFullPath(target);
                if (!full.StartsWith(preview, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(Path.GetExtension(full), ".glb", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Deferred preview target escapes this package: " + target);
                for (var dir = new DirectoryInfo(Path.GetDirectoryName(full)); dir != null; dir = dir.Parent)
                    if (dir.Exists && (dir.Attributes & FileAttributes.ReparsePoint) != 0)
                        throw new InvalidDataException("Deferred target crosses a linked directory.");
                if (File.Exists(full) && (File.GetAttributes(full) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException("Deferred target is a linked file.");
                paths.Add("Assets/" + full.Substring(assets.Length + 1).Replace('\\', '/'));
            }
            return new List<string>(paths).ToArray();
        }

        private static string ComputeFileHash(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var hash = SHA256.Create())
                return "sha256:" + BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }

        private static string ReadMetaGuid(string metaPath)
        {
            if (!File.Exists(metaPath))
                return string.Empty;

            string[] lines = File.ReadAllLines(metaPath);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                if (line.StartsWith("guid:", StringComparison.Ordinal))
                    return line.Substring(5).Trim();
            }

            return string.Empty;
        }

        /// <summary>
        /// 在生成包与全部场景中查找该 GUID 的引用。
        /// </summary>
        /// <returns>
        /// true 表示找到引用；<paramref name="error"/> 非空表示**无法完成核对**（保留文件）。
        /// 扫描完成且无引用时两者分别为 false / 空串。
        /// </returns>
        private static bool TryFindReference(
            string guid,
            string assetPath,
            out string referencingAsset,
            out string error)
        {
            referencingAsset = string.Empty;
            error = string.Empty;

            var scanRoots = new List<string>
            {
                "Assets"
            };

            long consumed = 0;
            for (int r = 0; r < scanRoots.Count; r++)
            {
                string root = scanRoots[r];
                if (!Directory.Exists(root))
                    continue;

                string[] files = Directory.GetFiles(root, "*", SearchOption.AllDirectories);
                for (int f = 0; f < files.Length; f++)
                {
                    string file = files[f].Replace('\\', '/');
                    if (file.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                        continue;
                    // 候选自己的元数据不算引用。
                    if (string.Equals(file, assetPath, StringComparison.OrdinalIgnoreCase))
                        continue;

                    try
                    {
                        foreach (string dependency in AssetDatabase.GetDependencies(file, recursive: false))
                        {
                            if (!string.Equals(dependency, assetPath, StringComparison.OrdinalIgnoreCase)) continue;
                            referencingAsset = file;
                            return true;
                        }
                    }
                    catch (Exception exception)
                    {
                        error = "dependency lookup failed for " + file + ": " + exception.Message;
                        return false;
                    }
                    if (!HasScannableExtension(file)) continue;

                    long length;
                    try
                    {
                        length = new FileInfo(file).Length;
                    }
                    catch (Exception exception)
                    {
                        error = "stat failed for " + file + ": " + exception.Message;
                        return false;
                    }

                    consumed += length;
                    if (consumed > ReferenceScanByteBudget)
                    {
                        error = "reference scan exceeded byte budget";
                        return false;
                    }

                    string text;
                    try
                    {
                        text = File.ReadAllText(file);
                    }
                    catch (Exception exception)
                    {
                        error = "read failed for " + file + ": " + exception.Message;
                        return false;
                    }

                    if (text.IndexOf(guid, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        referencingAsset = file;
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool HasScannableExtension(string path)
        {
            for (int i = 0; i < ReferenceScanExtensions.Length; i++)
            {
                if (path.EndsWith(ReferenceScanExtensions[i], StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }
    }
}
