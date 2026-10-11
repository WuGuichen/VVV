using System;
using System.IO;
using System.Text.RegularExpressions;

namespace MxFramework.Authoring
{
    /// <summary>
    /// 路径逃逸与符号链接安全沙箱。对应规范 v1.4 第 6.2 节。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 规范参考实现使用 <c>FileInfo.LinkTarget</c> / <c>DirectoryInfo.LinkTarget</c>。
    /// 这两个 API 属于 .NET 6+，在 <c>netstandard2.1</c>（本项目的目标框架）中
    /// <b>不存在</b>，无法编译。本实现改用 <see cref="FileAttributes.ReparsePoint"/>，
    /// 它是 netstandard2.0 起可用的等价机制，且在 Windows 上会同时覆盖
    /// 符号链接与目录联接（junction）——判定结果是规范要求的超集，安全语义更严。
    /// </para>
    /// <para>
    /// 另一处修正：规范用 <c>StartsWith(root.FullName)</c> 判断祖先关系，
    /// 会把 <c>...\0000011</c> 误判为 <c>...\000001</c> 的后代。本实现改为
    /// 分隔符对齐的边界比较。
    /// </para>
    /// <para>
    /// 失败关闭（fail-closed）原则：无法判定某路径是否为链接时（例如权限不足），
    /// 抛异常而非静默放行。
    /// </para>
    /// </remarks>
    public static class SecurePathSandboxEnforcer
    {
        /// <summary>packageId 的强制格式：6 位数字。杜绝路径注入字符。</summary>
        public const string PackageIdPattern = "^[0-9]{6}$";

        private static readonly char[] DirectorySeparators =
        {
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar
        };

        /// <summary>校验 packageId 格式，非法时抛出安全诊断。</summary>
        public static void AssertValidPackageId(string packageId)
        {
            if (string.IsNullOrEmpty(packageId) || !Regex.IsMatch(packageId, PackageIdPattern))
            {
                throw new CharacterDiagnosticException(
                    "packageId",
                    $"Invalid packageId format: '{packageId}'. Must be strictly 6 digits.",
                    CharacterGateCodes.PathTraversalOrSymbolicLink,
                    field: "packageId");
            }
        }

        /// <summary>
        /// 解析授权根目录：<c>&lt;trustedWorkspaceRoot&gt;/&lt;packageId&gt;</c>。
        /// </summary>
        public static string ResolvePackageRoot(string trustedWorkspaceRoot, string packageId)
        {
            if (string.IsNullOrEmpty(trustedWorkspaceRoot))
            {
                throw new ArgumentException("trustedWorkspaceRoot is required.", nameof(trustedWorkspaceRoot));
            }

            AssertValidPackageId(packageId);
            return Path.GetFullPath(Path.Combine(trustedWorkspaceRoot, packageId));
        }

        /// <summary>
        /// 解析并校验目标路径，确保其严格位于包沙箱内且路径上无符号链接。
        /// </summary>
        /// <param name="trustedWorkspaceRoot">受信任的工作区根目录。</param>
        /// <param name="packageId">6 位数字包标识。</param>
        /// <param name="targetRelativePath">相对授权根目录的目标路径。</param>
        /// <returns>规范化后的绝对路径。</returns>
        public static string ResolveAndVerifyPath(
            string trustedWorkspaceRoot,
            string packageId,
            string targetRelativePath)
        {
            string authorizedRoot = ResolvePackageRoot(trustedWorkspaceRoot, packageId);

            string rootWithSeparator = EnsureTrailingSeparator(authorizedRoot);
            string combined = Path.GetFullPath(Path.Combine(authorizedRoot, targetRelativePath ?? string.Empty));
            string combinedWithSeparator = EnsureTrailingSeparator(combined);

            if (!combinedWithSeparator.StartsWith(rootWithSeparator, StringComparison.Ordinal))
            {
                throw new CharacterDiagnosticException(
                    targetRelativePath,
                    $"Path Traversal Blocked: Path '{targetRelativePath}' attempts to escape package sandbox '{authorizedRoot}'.",
                    CharacterGateCodes.PathTraversalOrSymbolicLink,
                    field: "targetRelativePath");
            }

            VerifyNoSymbolicLinks(authorizedRoot, combined);
            return combined;
        }

        /// <summary>
        /// 符号链接深度检查：先查目标文件自身，再逐级向上查祖先目录。
        /// </summary>
        public static void VerifyNoSymbolicLinks(string rootDir, string fullPath)
        {
            if (string.IsNullOrEmpty(fullPath))
            {
                return;
            }

            // 关键整改：末端目标文件自身必须检查。只看父目录会让"文件本身是链接"穿透。
            if (IsSymbolicLinkOrReparsePoint(fullPath))
            {
                throw new CharacterDiagnosticException(
                    fullPath,
                    $"Symbolic link (or reparse point) detected and prohibited on target file: '{fullPath}'.",
                    CharacterGateCodes.PathTraversalOrSymbolicLink,
                    sourcePath: fullPath);
            }

            string rootFull = Path.GetFullPath(rootDir ?? string.Empty);
            string current = Path.GetDirectoryName(Path.GetFullPath(fullPath));

            while (!string.IsNullOrEmpty(current) && IsWithinOrEqual(current, rootFull))
            {
                if (IsSymbolicLinkOrReparsePoint(current))
                {
                    throw new CharacterDiagnosticException(
                        current,
                        $"Symbolic link (or reparse point) detected and prohibited in path ancestor: '{current}'.",
                        CharacterGateCodes.PathTraversalOrSymbolicLink,
                        sourcePath: current);
                }

                if (string.Equals(current, rootFull, StringComparison.Ordinal))
                {
                    break;
                }

                string parent = Path.GetDirectoryName(current);
                if (string.Equals(parent, current, StringComparison.Ordinal))
                {
                    break;
                }

                current = parent;
            }
        }

        /// <summary>
        /// 判断路径是否为符号链接、目录联接或其它重解析点。
        /// 路径不存在时返回 false；无法读取属性时抛异常（失败关闭）。
        /// </summary>
        public static bool IsSymbolicLinkOrReparsePoint(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            bool exists = File.Exists(path) || Directory.Exists(path);
            if (!exists)
            {
                return false;
            }

            try
            {
                FileAttributes attributes = File.GetAttributes(path);
                return (attributes & FileAttributes.ReparsePoint) != 0;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                throw new CharacterDiagnosticException(
                    path,
                    $"Unable to determine whether '{path}' is a symbolic link: {exception.Message}. " +
                    "Sandbox verification fails closed.",
                    CharacterGateCodes.PathTraversalOrSymbolicLink,
                    sourcePath: path);
            }
        }

        private static string EnsureTrailingSeparator(string path)
        {
            string trimmed = (path ?? string.Empty).TrimEnd(DirectorySeparators);
            return trimmed + Path.DirectorySeparatorChar;
        }

        /// <summary>分隔符对齐的"位于其中或相等"判定，避免 000001 / 0000011 误判。</summary>
        private static bool IsWithinOrEqual(string candidate, string root)
        {
            if (string.Equals(candidate, root, StringComparison.Ordinal))
            {
                return true;
            }

            return candidate.StartsWith(EnsureTrailingSeparator(root), StringComparison.Ordinal);
        }
    }
}
