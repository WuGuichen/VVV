using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MxFramework.Authoring
{
    /// <summary>
    /// 唯一规范化摘要算法。对应规范 v1.4 第 1.3 节。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 该类型是 <c>sourcePackageHash</c> 的唯一权威实现。任何其它位置都不得再实现
    /// 摘要拼接逻辑，否则会重新引入"同一输入得到不同结果"的分歧。
    /// </para>
    /// <para>
    /// 规范澄清项（序列化策略）：第 1.3 节只规定"紧凑 UTF-8 字节流（无缩进、无空格）"，
    /// 未固定字符串转义策略与数字格式。两者都会改变字节流，因此本实现显式固定：
    /// </para>
    /// <list type="bullet">
    /// <item><description>转义策略：<see cref="JavaScriptEncoder.UnsafeRelaxedJsonEscaping"/>，
    /// 即非 ASCII 字符按原始 UTF-8 输出，与 JavaScript <c>JSON.stringify</c> 的语义一致，
    /// 便于与 Web 侧 CharacterStudio 互操作。</description></item>
    /// <item><description>数字格式：保留源文档中的原始字面量（不重新格式化），
    /// 避免 <c>1</c> 与 <c>1.0</c> 之间的漂移。</description></item>
    /// </list>
    /// <para>以上两点需规范确认，否则跨实现字节级一致性无保证。</para>
    /// </remarks>
    public static class CharacterSourcePackageCanonicalDigest
    {
        public const string HashAlgorithm = "sha256";

        public const string HashPrefix = "sha256:";

        public const string ManifestFileName = "manifest.json";

        /// <summary>计算摘要时强制写入的 hashStatus 字面量。</summary>
        public const string HashStatusValid = "Valid";

        private const string HashesPropertyName = "hashes";

        private const string SourcePackageHashPropertyName = "sourcePackageHash";

        private const string HashStatusPropertyName = "hashStatus";

        private static readonly JsonWriterOptions CanonicalWriterOptions = new JsonWriterOptions
        {
            Indented = false,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            SkipValidation = false
        };

        /// <summary>
        /// 路径规范化：统一转为小写正斜杠。规范第 1.3 节步骤 3e。
        /// </summary>
        public static string NormalizeRelativePath(string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath))
            {
                return string.Empty;
            }

            return relativePath.Replace('\\', '/').ToLowerInvariant();
        }

        /// <summary>计算文件的原始磁盘二进制 SHA-256，返回十六进制（无前缀）。</summary>
        public static string ComputeFileHashHex(string absoluteFilePath)
        {
            using FileStream stream = File.OpenRead(absoluteFilePath);
            using var sha256 = SHA256.Create();
            byte[] hash = sha256.ComputeHash(stream);
            return ToHex(hash);
        }

        /// <summary>计算文件的原始磁盘二进制 SHA-256，返回 <c>sha256:&lt;hex&gt;</c>。</summary>
        public static string ComputeFileHash(string absoluteFilePath)
        {
            return HashPrefix + ComputeFileHashHex(absoluteFilePath);
        }

        /// <summary>
        /// 步骤 2：Manifest 规范化清洗并计算摘要。
        /// </summary>
        /// <remarks>
        /// 清洗规则不可替换为等价写法：
        /// <list type="number">
        /// <item><description><c>/hashes/sourcePackageHash</c> 必须<b>彻底删除键值对</b>，
        /// 严禁保留为 <c>null</c>——两者字节流不同。</description></item>
        /// <item><description><c>/hashes/hashStatus</c> 必须被<b>强制覆写</b>为
        /// <c>"Valid"</c>，这是消除状态时序回环的关键。</description></item>
        /// <item><description>属性按 <see cref="StringComparer.Ordinal"/> 升序序列化。</description></item>
        /// </list>
        /// </remarks>
        public static string ComputeCanonicalManifestDigestHex(string manifestJsonText)
        {
            byte[] canonicalBytes = SerializeCanonicalManifestBytes(manifestJsonText);
            using var sha256 = SHA256.Create();
            return ToHex(sha256.ComputeHash(canonicalBytes));
        }

        /// <summary>步骤 2 的字节流产出，便于诊断与跨实现比对。</summary>
        public static byte[] SerializeCanonicalManifestBytes(string manifestJsonText)
        {
            if (string.IsNullOrWhiteSpace(manifestJsonText))
            {
                throw new CharacterDiagnosticException(
                    ManifestFileName, "manifest.json content is empty.", CharacterGateCodes.ManifestSchemaInvalid);
            }

            JsonNode root;
            try
            {
                root = JsonNode.Parse(manifestJsonText);
            }
            catch (JsonException exception)
            {
                throw new CharacterDiagnosticException(
                    ManifestFileName,
                    $"manifest.json is not valid JSON: {exception.Message}",
                    CharacterGateCodes.ManifestSchemaInvalid);
            }

            if (root is not JsonObject manifestObject)
            {
                throw new CharacterDiagnosticException(
                    ManifestFileName,
                    "manifest.json root must be a JSON object.",
                    CharacterGateCodes.ManifestSchemaInvalid);
            }

            if (manifestObject[HashesPropertyName] is not JsonObject hashesObject)
            {
                throw new CharacterDiagnosticException(
                    ManifestFileName,
                    "manifest.json must declare a 'hashes' object before a canonical digest can be computed.",
                    CharacterGateCodes.ManifestSchemaInvalid,
                    sourcePath: ManifestFileName,
                    field: HashesPropertyName);
            }

            // 步骤 2a：彻底删除 sourcePackageHash 键值对（不是置 null）。
            hashesObject.Remove(SourcePackageHashPropertyName);

            // 步骤 2b：强制覆写 hashStatus。
            hashesObject[HashStatusPropertyName] = JsonValue.Create(HashStatusValid);

            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream, CanonicalWriterOptions))
            {
                WriteCanonical(writer, manifestObject);
            }

            return stream.ToArray();
        }

        /// <summary>
        /// 步骤 3 + 步骤 4：由展开集合计算 <c>sourcePackageHash</c>。
        /// </summary>
        /// <param name="packageRootPath">角色包根目录绝对路径。</param>
        /// <param name="relativeFilePaths">
        /// 第 1.1 节算法产出的展开集合（相对路径，可含 manifest.json）。
        /// 未在集合中的物理文件绝不参与哈希。
        /// </param>
        public static string ComputeSourcePackageHash(string packageRootPath, IEnumerable<string> relativeFilePaths)
        {
            if (string.IsNullOrEmpty(packageRootPath))
            {
                throw new ArgumentException("packageRootPath is required.", nameof(packageRootPath));
            }

            if (relativeFilePaths == null)
            {
                throw new ArgumentNullException(nameof(relativeFilePaths));
            }

            var manifestText = ReadManifestText(packageRootPath);
            string manifestDigestHex = ComputeCanonicalManifestDigestHex(manifestText);

            var entries = new List<string>
            {
                // 步骤 3b：规范化 Manifest 作为首条记录，且只参与一次拼接。
                ManifestFileName + ":" + manifestDigestHex
            };

            foreach (string relativePath in relativeFilePaths)
            {
                string normalized = NormalizeRelativePath(relativePath);
                if (string.IsNullOrEmpty(normalized) || normalized == ManifestFileName)
                {
                    continue;
                }

                string absolutePath = Path.Combine(packageRootPath, normalized.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(absolutePath))
                {
                    throw new CharacterDiagnosticException(
                        normalized,
                        $"File declared in the expansion closure is physically missing: '{normalized}'.",
                        CharacterGateCodes.RequiredAssetPhysicallyMissing,
                        sourcePath: normalized);
                }

                entries.Add(normalized + ":" + ComputeFileHashHex(absolutePath));
            }

            return ComputeSourcePackageHashFromEntries(entries);
        }

        /// <summary>
        /// 步骤 3f + 步骤 4：对条目列表排序、以 <c>\n</c> 拼接（末尾带换行）后求最终摘要。
        /// </summary>
        public static string ComputeSourcePackageHashFromEntries(IEnumerable<string> entries)
        {
            if (entries == null)
            {
                throw new ArgumentNullException(nameof(entries));
            }

            List<string> ordered = entries.ToList();
            ordered.Sort(StringComparer.Ordinal);

            var builder = new StringBuilder();
            for (int i = 0; i < ordered.Count; i++)
            {
                builder.Append(ordered[i]).Append('\n');
            }

            byte[] payload = Encoding.UTF8.GetBytes(builder.ToString());
            using var sha256 = SHA256.Create();
            return HashPrefix + ToHex(sha256.ComputeHash(payload));
        }

        /// <summary>步骤 4 的拼接字节流，便于诊断。</summary>
        public static byte[] SerializeOrderedEntriesPayload(IEnumerable<string> entries)
        {
            List<string> ordered = entries.ToList();
            ordered.Sort(StringComparer.Ordinal);

            var builder = new StringBuilder();
            for (int i = 0; i < ordered.Count; i++)
            {
                builder.Append(ordered[i]).Append('\n');
            }

            return Encoding.UTF8.GetBytes(builder.ToString());
        }

        /// <summary>
        /// 校验 <c>sourcePackageHash</c> 回环：写入结果后重新计算必须得到同一值。
        /// 用于证明第 1.3 节"绝无二次漂移"的承诺。
        /// </summary>
        public static bool VerifyRoundTrip(string packageRootPath, IEnumerable<string> relativeFilePaths, string expectedHash)
        {
            string recomputed = ComputeSourcePackageHash(packageRootPath, relativeFilePaths);
            return string.Equals(recomputed, expectedHash, StringComparison.Ordinal);
        }

        private static string ReadManifestText(string packageRootPath)
        {
            string manifestPath = Path.Combine(packageRootPath, ManifestFileName);
            if (!File.Exists(manifestPath))
            {
                throw new CharacterDiagnosticException(
                    ManifestFileName,
                    $"manifest.json is missing under '{packageRootPath}'.",
                    CharacterGateCodes.ManifestSchemaInvalid,
                    sourcePath: ManifestFileName);
            }

            return File.ReadAllText(manifestPath, Encoding.UTF8);
        }

        private static void WriteCanonical(Utf8JsonWriter writer, JsonNode node)
        {
            if (node == null)
            {
                writer.WriteNullValue();
                return;
            }

            switch (node)
            {
                case JsonObject jsonObject:
                    writer.WriteStartObject();
                    foreach (KeyValuePair<string, JsonNode> property in jsonObject.OrderBy(
                        item => item.Key,
                        StringComparer.Ordinal))
                    {
                        writer.WritePropertyName(property.Key);
                        WriteCanonical(writer, property.Value);
                    }

                    writer.WriteEndObject();
                    break;

                case JsonArray jsonArray:
                    writer.WriteStartArray();
                    for (int i = 0; i < jsonArray.Count; i++)
                    {
                        WriteCanonical(writer, jsonArray[i]);
                    }

                    writer.WriteEndArray();
                    break;

                default:
                    // JsonValue：保留源文档原始字面量，不重新格式化数字。
                    node.WriteTo(writer);
                    break;
            }
        }

        private static string ToHex(byte[] bytes)
        {
            var builder = new StringBuilder(bytes.Length * 2);
            for (int i = 0; i < bytes.Length; i++)
            {
                builder.Append(bytes[i].ToString("x2", CultureInfo.InvariantCulture));
            }

            return builder.ToString();
        }
    }
}
