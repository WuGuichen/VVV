using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace MxFramework.Authoring;

/// <summary>
/// GLB 外部依赖预检结果。
/// </summary>
public sealed class CharacterGlbDependencyReport
{
    /// <summary>是否成功解析出 GLB JSON chunk。</summary>
    public bool Inspected { get; set; }

    /// <summary>未能解析时的原因（例如文件缺失、容器格式非法）。</summary>
    public string FailureReason { get; set; } = string.Empty;

    /// <summary>合法的相对外部依赖（相对 GLB 所在目录），已确认存在。</summary>
    public List<string> ExternalDependencies { get; set; } = new List<string>();

    /// <summary>违规项：绝对路径、越界路径、非允许 scheme、或缺失的依赖文件。</summary>
    public List<string> Violations { get; set; } = new List<string>();

    public bool Succeeded
    {
        get { return Inspected && Violations.Count == 0; }
    }
}

/// <summary>
/// 预检 GLB 的外部依赖（<c>buffers[].uri</c> / <c>images[].uri</c>）。
/// </summary>
/// <remarks>
/// 不假定所有 GLB 都内嵌全部纹理与 buffer。制作预览允许从**作者源范围内**读取相对依赖，
/// 但不接受系统绝对路径、越界路径或外部 scheme；依赖丢失同样报告。
///
/// 本预检只做判定与报告，**不修改文件、不递归删除同目录贴图或 buffer**。
/// </remarks>
public static class CharacterGlbDependencyInspector
{
    private const uint GlbMagic = 0x46546C67;
    private const uint JsonChunkType = 0x4E4F534A;
    private const string DataUriPrefix = "data:";

    public static CharacterGlbDependencyReport Inspect(string glbPath, string packageRootPath)
    {
        var report = new CharacterGlbDependencyReport();
        if (string.IsNullOrWhiteSpace(glbPath) || !File.Exists(glbPath))
        {
            report.FailureReason = "GLB file was not found: " + (glbPath ?? string.Empty);
            return report;
        }

        string json;
        string containerError;
        if (!TryReadJsonChunk(glbPath, out json, out containerError))
        {
            report.FailureReason = containerError;
            return report;
        }

        report.Inspected = true;
        string glbDirectory = Path.GetDirectoryName(Path.GetFullPath(glbPath)) ?? string.Empty;
        string normalizedPackageRoot = NormalizeFullPath(packageRootPath);

        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            CollectUris(document.RootElement, "buffers", glbDirectory, normalizedPackageRoot, report);
            CollectUris(document.RootElement, "images", glbDirectory, normalizedPackageRoot, report);
        }
        catch (JsonException exception)
        {
            report.Inspected = false;
            report.FailureReason = "GLB JSON chunk is not valid JSON: " + exception.Message;
        }

        return report;
    }

    private static void CollectUris(
        JsonElement root,
        string arrayName,
        string glbDirectory,
        string normalizedPackageRoot,
        CharacterGlbDependencyReport report)
    {
        JsonElement array;
        if (!root.TryGetProperty(arrayName, out array) || array.ValueKind != JsonValueKind.Array)
            return;

        foreach (JsonElement item in array.EnumerateArray())
        {
            JsonElement uriElement;
            if (item.ValueKind != JsonValueKind.Object
                || !item.TryGetProperty("uri", out uriElement)
                || uriElement.ValueKind != JsonValueKind.String)
            {
                // 没有 uri 表示使用 GLB 内嵌的二进制 chunk，属正常情况。
                continue;
            }

            string uri = uriElement.GetString() ?? string.Empty;
            if (uri.Length == 0 || uri.StartsWith(DataUriPrefix, StringComparison.OrdinalIgnoreCase))
                continue;

            string violation = ValidateUri(uri, arrayName);
            if (violation != null)
            {
                report.Violations.Add(violation);
                continue;
            }

            string normalizedUri = uri.Replace('\\', '/');
            string resolved = NormalizeFullPath(Path.Combine(glbDirectory, normalizedUri.Replace('/', Path.DirectorySeparatorChar)));
            if (normalizedPackageRoot.Length > 0
                && !resolved.StartsWith(normalizedPackageRoot, StringComparison.OrdinalIgnoreCase))
            {
                report.Violations.Add(arrayName + " uri escapes the package root: " + uri);
                continue;
            }

            if (!File.Exists(resolved))
            {
                report.Violations.Add(arrayName + " dependency is missing: " + uri);
                continue;
            }

            if (!report.ExternalDependencies.Contains(normalizedUri))
                report.ExternalDependencies.Add(normalizedUri);
        }
    }

    /// <summary>返回违规说明；null 表示该 URI 形态合法（仍需存在性检查）。</summary>
    private static string ValidateUri(string uri, string arrayName)
    {
        if (uri.StartsWith("\\\\", StringComparison.Ordinal)
            || uri.StartsWith("//", StringComparison.Ordinal))
            return arrayName + " uri must not be a UNC path: " + uri;

        if (uri[0] == '/' || uri[0] == '\\')
            return arrayName + " uri must not be an absolute path: " + uri;

        if (uri.Length >= 2 && uri[1] == ':' && IsAsciiLetter(uri[0]))
            return arrayName + " uri must not be a drive-qualified path: " + uri;

        // 任何显式 scheme（http/https/file/...）都超出作者源允许范围。
        int colon = uri.IndexOf(':');
        int slash = uri.IndexOf('/');
        int backslash = uri.IndexOf('\\');
        int firstSeparator = slash < 0 ? backslash : (backslash < 0 ? slash : Math.Min(slash, backslash));
        if (colon >= 0 && (firstSeparator < 0 || colon < firstSeparator))
            return arrayName + " uri must not use an external scheme: " + uri;

        string[] segments = uri.Replace('\\', '/').Split('/');
        for (int i = 0; i < segments.Length; i++)
        {
            if (string.Equals(segments[i], "..", StringComparison.Ordinal))
                return arrayName + " uri must not escape its directory: " + uri;
        }

        return null;
    }

    private static bool IsAsciiLetter(char value)
    {
        return (value >= 'a' && value <= 'z') || (value >= 'A' && value <= 'Z');
    }

    private static string NormalizeFullPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return string.Empty;

        try
        {
            return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }

    private static bool TryReadJsonChunk(string glbPath, out string json, out string error)
    {
        json = string.Empty;
        error = string.Empty;

        byte[] bytes;
        try
        {
            bytes = File.ReadAllBytes(glbPath);
        }
        catch (Exception exception)
        {
            error = "GLB read failed: " + exception.Message;
            return false;
        }

        if (bytes.Length < 20 || ReadUInt32(bytes, 0) != GlbMagic)
        {
            error = "File is not a GLB container.";
            return false;
        }

        int declaredLength = (int)ReadUInt32(bytes, 8);
        int limit = declaredLength > 0 && declaredLength <= bytes.Length ? declaredLength : bytes.Length;
        int offset = 12;
        while (offset + 8 <= limit)
        {
            int chunkLength = (int)ReadUInt32(bytes, offset);
            uint chunkType = ReadUInt32(bytes, offset + 4);
            offset += 8;
            if (chunkLength < 0 || offset + chunkLength > limit)
            {
                error = "Invalid GLB chunk length.";
                return false;
            }

            if (chunkType == JsonChunkType)
            {
                json = System.Text.Encoding.UTF8.GetString(bytes, offset, chunkLength).TrimEnd('\0', ' ');
                return true;
            }

            offset += chunkLength;
        }

        error = "GLB JSON chunk was not found.";
        return false;
    }

    private static uint ReadUInt32(byte[] bytes, int offset)
    {
        return (uint)(bytes[offset]
            | (bytes[offset + 1] << 8)
            | (bytes[offset + 2] << 16)
            | (bytes[offset + 3] << 24));
    }
}
