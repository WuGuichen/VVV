using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using MxFramework.Editor;
using MxFramework.Editor.Levels;
using MxFramework.Editor.ProjectileAuthoring;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace MxFramework.PackageConsumerSmoke
{
    [SetUpFixture]
    public sealed class PackageConsumerMutationGuard
    {
        private const string PackageName = "com.vincent.wgameframework";

        private string packageRoot = string.Empty;
        private string packageTreeHash = string.Empty;
        private string projectAssetsHash = string.Empty;

        [OneTimeSetUp]
        public void CaptureInitialTrees()
        {
            UnityEditor.PackageManager.PackageInfo package =
                UnityEditor.PackageManager.PackageInfo.FindForPackageName(PackageName);
            Assert.That(package, Is.Not.Null, "The consumer must resolve the installed package.");

            packageRoot = package.resolvedPath;
            packageTreeHash = ComputeTreeHash(packageRoot);
            projectAssetsHash = ComputeTreeHash(Application.dataPath);
        }

        [OneTimeTearDown]
        public void AssertTreesRemainUnchanged()
        {
            Assert.That(
                ComputeTreeHash(packageRoot),
                Is.EqualTo(packageTreeHash),
                "Consumer tests must not mutate the resolved package tree.");
            Assert.That(
                ComputeTreeHash(Application.dataPath),
                Is.EqualTo(projectAssetsHash),
                "Portable Editor initialization and tests must not write implicit project assets.");
        }

        private static string ComputeTreeHash(string root)
        {
            string normalizedRoot = Path.GetFullPath(root)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            var files = new List<string>(
                Directory.GetFiles(normalizedRoot, "*", SearchOption.AllDirectories));
            files.Sort((left, right) => StringComparer.Ordinal.Compare(
                ToRelativePath(normalizedRoot, left),
                ToRelativePath(normalizedRoot, right)));

            using (SHA256 hash = SHA256.Create())
            {
                byte[] separator = { 0 };
                byte[] buffer = new byte[81920];
                for (int index = 0; index < files.Count; index++)
                {
                    byte[] relativePath = Encoding.UTF8.GetBytes(
                        ToRelativePath(normalizedRoot, files[index]));
                    Append(hash, relativePath, relativePath.Length);
                    Append(hash, separator, separator.Length);

                    using (FileStream stream = File.OpenRead(files[index]))
                    {
                        int count;
                        while ((count = stream.Read(buffer, 0, buffer.Length)) > 0)
                            Append(hash, buffer, count);
                    }

                    Append(hash, separator, separator.Length);
                }

                hash.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                return BitConverter.ToString(hash.Hash).Replace("-", string.Empty);
            }
        }

        private static string ToRelativePath(string normalizedRoot, string path)
        {
            return Path.GetFullPath(path)
                .Substring(normalizedRoot.Length)
                .Replace('\\', '/');
        }

        private static void Append(SHA256 hash, byte[] buffer, int count)
        {
            hash.TransformBlock(buffer, 0, count, buffer, 0);
        }
    }

    public sealed class EditorPackageConsumerSmokeTests
    {
        private const string PackageAssetRoot = "Packages/com.vincent.wgameframework";

        [Test]
        public void InstalledPackage_ExposesRepresentativeEditorAssets()
        {
            Assert.That(
                AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                    PackageAssetRoot + "/Editor/VfxAuthoringResourceLibraryWindow.uxml"),
                Is.Not.Null,
                "The installed package must expose the VFX authoring UXML through its package path.");
            Assert.That(
                AssetDatabase.LoadAssetAtPath<StyleSheet>(
                    PackageAssetRoot + "/Editor/VfxAuthoringResourceLibraryWindow.uss"),
                Is.Not.Null,
                "The installed package must expose the VFX authoring stylesheet through its package path.");
            Assert.That(
                AssetDatabase.LoadAssetAtPath<StyleSheet>(
                    PackageAssetRoot + "/Editor/ProjectileAuthoring/ProjectileAuthoringWindow.uss"),
                Is.Not.Null,
                "The installed package must expose the Projectile Authoring stylesheet through its package path.");
            Assert.That(
                AssetDatabase.LoadAssetAtPath<StyleSheet>(
                    PackageAssetRoot + "/Editor/Levels/MxLevelEditorWindow.uss"),
                Is.Not.Null,
                "The installed package must expose the Level Editor stylesheet through its package path.");
        }

        [Test]
        public void InstalledPackage_PreservesRepresentativeEditorAssetGuids()
        {
            Assert.That(
                AssetDatabase.AssetPathToGUID(
                    PackageAssetRoot + "/Editor/VfxAuthoringResourceLibraryWindow.uxml"),
                Is.EqualTo("33f61188f4713654992a3bde5d630881"));
            Assert.That(
                AssetDatabase.AssetPathToGUID(
                    PackageAssetRoot + "/Editor/VfxAuthoringResourceLibraryWindow.uss"),
                Is.EqualTo("6cae45110fa8bd54c8f4fc7559a503bf"));
            Assert.That(
                AssetDatabase.AssetPathToGUID(
                    PackageAssetRoot
                    + "/Editor/ProjectileAuthoring/ProjectileAuthoringWindow.uss"),
                Is.EqualTo("1356761e11b238f4bbd24eff94be321e"));
            Assert.That(
                AssetDatabase.AssetPathToGUID(
                    PackageAssetRoot + "/Editor/Levels/MxLevelEditorWindow.uss"),
                Is.EqualTo("7b4de64c2c17c9b43ba3931f75ec2061"));
        }

        [Test]
        public void LevelEditor_CreateGui_UsesInstalledPackageStyle()
        {
            MxLevelEditorWindow window = ScriptableObject.CreateInstance<MxLevelEditorWindow>();
            try
            {
                window.CreateGUI();

                AssertUsesInstalledStyleSheet(
                    window.rootVisualElement,
                    "/Editor/Levels/MxLevelEditorWindow.uss");
                Assert.That(
                    window.rootVisualElement.Q("mx-level-editor-header"),
                    Is.Not.Null);
                Assert.That(
                    window.rootVisualElement.Q("mx-level-editor-content"),
                    Is.Not.Null);
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void ProjectileAuthoring_CreateGui_UsesInstalledPackageStyle()
        {
            ClearProjectileAuthoringSession();
            ProjectileAuthoringWindow window =
                ScriptableObject.CreateInstance<ProjectileAuthoringWindow>();
            try
            {
                window.SetServiceForTests(new InMemoryProjectileAuthoringService());
                window.CreateGUI();

                AssertUsesInstalledStyleSheet(
                    window.rootVisualElement,
                    "/Editor/ProjectileAuthoring/ProjectileAuthoringWindow.uss");
                Assert.That(
                    window.rootVisualElement.Q("projectile-authoring-workspace"),
                    Is.Not.Null);
                Assert.That(
                    window.rootVisualElement.Q("definition-request-tree"),
                    Is.Not.Null);
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
                ClearProjectileAuthoringSession();
            }
        }

        [Test]
        public void ProjectileAuthoring_WithoutRepositoryTools_ReportsUnavailable()
        {
            Assert.That(
                Directory.Exists(Path.Combine(ProjectRoot, "Tools")),
                Is.False,
                "The Consumer fixture must not provide repository authoring tools.");
            ClearProjectileAuthoringSession();
            ProjectileAuthoringWindow window =
                ScriptableObject.CreateInstance<ProjectileAuthoringWindow>();
            try
            {
                window.CreateGUI();

                Assert.That(
                    window.StatusForTests,
                    Is.EqualTo("不可用 — 未配置 Authoring CLI"));
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
                ClearProjectileAuthoringSession();
            }
        }

        [Test]
        public void VfxAuthoring_CreateGuiWithoutCatalog_ReportsUnavailable()
        {
            Assert.That(
                Directory.Exists(Path.Combine(ProjectRoot, "Assets", "Config")),
                Is.False,
                "The Consumer fixture must not provide repository VFX configuration.");
            VfxAuthoringResourceLibraryWindow window =
                ScriptableObject.CreateInstance<VfxAuthoringResourceLibraryWindow>();
            try
            {
                window.CreateGUI();

                Assert.That(
                    window.StatusForTests,
                    Is.EqualTo(
                        "Unavailable: VFX authoring catalog is not present in this consumer project."));
                Assert.That(window.rootVisualElement.Q("library-workspace"), Is.Not.Null);
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void Fixture_ExcludesProjectOnlyEditorInputs()
        {
            string assets = Path.Combine(ProjectRoot, "Assets");
            foreach (string path in new[]
            {
                Path.Combine(ProjectRoot, "Tools"),
                Path.Combine(ProjectRoot, "FGUIProject"),
                Path.Combine(assets, "Config"),
                Path.Combine(assets, "Scenes"),
                Path.Combine(assets, "VFX"),
                Path.Combine(assets, "Bundles")
            })
            {
                Assert.That(Directory.Exists(path), Is.False, path);
            }
        }

        [Test]
        public void FairyGuiCatalog_ProjectOnlyWindowIsNotInstalled()
        {
            Assert.That(
                Directory.Exists(Path.Combine(ProjectRoot, "FGUIProject")),
                Is.False,
                "The Consumer fixture must not provide the repository FairyGUI project.");
            Type windowType = typeof(MxLevelEditorWindow).Assembly.GetType(
                "MxFramework.Editor.MxFairyGuiArtSliceCatalogWindow",
                throwOnError: false);
            Assert.That(windowType, Is.Null);
        }

        private static void AssertUsesInstalledStyleSheet(
            VisualElement root,
            string packageRelativePath)
        {
            StyleSheet expected = AssetDatabase.LoadAssetAtPath<StyleSheet>(
                PackageAssetRoot + packageRelativePath);
            Assert.That(expected, Is.Not.Null);

            for (int index = 0; index < root.styleSheets.count; index++)
            {
                if (ReferenceEquals(root.styleSheets[index], expected))
                    return;
            }

            Assert.Fail(
                "The Editor surface did not attach the installed package stylesheet: "
                + PackageAssetRoot
                + packageRelativePath);
        }

        private static void ClearProjectileAuthoringSession()
        {
            SessionState.EraseString("MxFramework.ProjectileAuthoring.State");
            SessionState.EraseString("MxFramework.ProjectileAuthoring.Selected");
            SessionState.EraseString("MxFramework.ProjectileAuthoring.Filter");
            SessionState.EraseString("MxFramework.ProjectileAuthoring.Draft");
            SessionState.EraseString("MxFramework.ProjectileAuthoring.Dirty");
            SessionState.EraseString("MxFramework.ProjectileAuthoring.Mutation");
        }

        private static string ProjectRoot =>
            Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

        private sealed class InMemoryProjectileAuthoringService : IProjectileAuthoringService
        {
            public bool IsBusy => false;

            public void LoadWorkspace(
                string packagePath,
                Action<ProjectileAuthoringWorkspaceView, string> completed)
            {
                completed(new ProjectileAuthoringWorkspaceView(), string.Empty);
            }

            public void Validate(
                string packagePath,
                Action<ProjectileAuthoringProblemView[], string> completed)
            {
                throw new InvalidOperationException("CreateGUI must not validate the workspace.");
            }

            public void Mutate(
                string packagePath,
                ProjectileAuthoringMutationRequestView request,
                Action<ProjectileAuthoringMutationResultView, string, string> completed)
            {
                throw new InvalidOperationException("CreateGUI must not mutate the workspace.");
            }

            public void MutateSequence(
                string packagePath,
                ProjectileAuthoringMutationRequestView[] commands,
                Action<ProjectileAuthoringMutationResultView, string, string> completed)
            {
                throw new InvalidOperationException("CreateGUI must not mutate the workspace.");
            }

            public void Apply(
                string packagePath,
                string draftPath,
                Action<ProjectileAuthoringApplyResult, string> completed)
            {
                throw new InvalidOperationException("CreateGUI must not apply the workspace.");
            }

            public void Cancel()
            {
            }

            public void Dispose()
            {
            }
        }
    }
}
