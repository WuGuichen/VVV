using System;
using System.IO;
using UnityEditor;

namespace MxFramework.Editor
{
    internal static class MxFrameworkPackageAssetLocator
    {
        private const string PackageName = "com.vincent.wgameframework";
        private const string InstalledPackageAssetRoot = "Packages/" + PackageName;

        internal static bool TryResolveAssetPath(
            string packageRelativePath,
            out string assetPath)
        {
            assetPath = string.Empty;
            if (!IsNormalizedPackageRelativePath(packageRelativePath))
                return false;

            if (!TryResolveAssetRoot(out string assetRoot))
                return false;

            assetPath = assetRoot + "/" + packageRelativePath;
            return true;
        }

        internal static bool TryResolveAssetRoot(out string assetRoot)
        {
            assetRoot = string.Empty;
            if (UnityEditor.PackageManager.PackageInfo.FindForPackageName(PackageName) == null)
                return false;
            assetRoot = InstalledPackageAssetRoot;
            return true;
        }

        private static bool IsNormalizedPackageRelativePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)
                || !string.Equals(path, path.Trim(), StringComparison.Ordinal)
                || path.IndexOf('\\') >= 0
                || path.StartsWith("/", StringComparison.Ordinal)
                || Path.IsPathRooted(path))
            {
                return false;
            }

            string[] segments = path.Split('/');
            for (int i = 0; i < segments.Length; i++)
            {
                if (segments[i].Length == 0
                    || string.Equals(segments[i], ".", StringComparison.Ordinal)
                    || string.Equals(segments[i], "..", StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
