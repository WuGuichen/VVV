using System;
using System.Linq;
using UnityEditor;
using UnityEditor.PackageManager.UI;
using UnityEngine;

namespace WGameFramework.UpmSampleSmoke
{
    public static class RuntimeHostSampleImporter
    {
        private const string PackageName = "com.vincent.wgameframework";
        private const string SampleDisplayName = "Runtime Host Minimal Lifecycle";

        public static void Import()
        {
            UnityEditor.PackageManager.PackageInfo package =
                UnityEditor.PackageManager.PackageInfo.FindForPackageName(PackageName);
            if (package == null)
                throw new InvalidOperationException("Installed MxFramework package was not found.");

            Sample[] matches = Sample.FindByPackage(package.name, package.version)
                .Where(sample => string.Equals(sample.displayName, SampleDisplayName, StringComparison.Ordinal))
                .ToArray();
            if (matches.Length != 1)
                throw new InvalidOperationException("Expected exactly one RuntimeHost sample, found " + matches.Length + ".");
            if (!matches[0].Import(Sample.ImportOptions.OverridePreviousImports))
                throw new InvalidOperationException("Package Manager failed to import the RuntimeHost sample.");

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Debug.Log("Imported package sample: " + matches[0].importPath);
        }
    }
}
