using System;
using System.IO;
using System.Runtime.CompilerServices;
using MxFramework.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.UIElements;

namespace MxFramework.PackageTests.Editor
{
    public sealed class PackageAssetLocatorContractTests
    {
        [Test]
        public void EditorAssembly_DoesNotFriendProjectOnlyFairyGuiAssembly()
        {
            object[] attributes = typeof(MxFrameworkPackageAssetLocator).Assembly.GetCustomAttributes(
                typeof(InternalsVisibleToAttribute),
                inherit: false);
            for (int i = 0; i < attributes.Length; i++)
            {
                var friend = (InternalsVisibleToAttribute)attributes[i];
                Assert.That(
                    friend.AssemblyName,
                    Is.Not.EqualTo("MxFramework.Project.FairyGUI.Editor"));
            }
        }

        [Test]
        public void FairyGuiArtSliceEditorSurface_IsPublicConsumerContract()
        {
            Type[] contractTypes =
            {
                typeof(MxFairyGuiArtSliceCatalogEditorService),
                typeof(MxFairyGuiArtSliceCatalogEditorOperationPlanner),
                typeof(MxFairyGuiArtSliceEditorDocument),
                typeof(MxFairyGuiArtSliceEditorSliceRecord),
                typeof(MxFairyGuiArtSliceEditorUsageRecord),
                typeof(MxFairyGuiArtSliceEditorReportResult),
                typeof(MxFairyGuiArtSliceEditorOperationPlan),
                typeof(MxFairyGuiArtSliceEditorOperationDiagnostic),
                typeof(MxFairyGuiArtSliceEditorOperationKind),
                typeof(MxFairyGuiArtSliceEditorOperationSeverity)
            };

            for (int i = 0; i < contractTypes.Length; i++)
                Assert.That(contractTypes[i].IsPublic, Is.True, contractTypes[i].FullName);
        }

        [Test]
        public void EditorProjectPaths_UseRegisteredHostProvider()
        {
            var provider = new ProbeProjectPathProvider();
            MxFrameworkEditorProjectPaths.Register(provider);
            try
            {
                Assert.That(
                    MxFrameworkEditorProjectPaths.TryResolve(
                        MxFrameworkEditorProjectPathKind.AbilityAuthoringJson,
                        out string resolved),
                    Is.True);
                Assert.That(resolved, Is.EqualTo("Config/ability-authoring.json"));
            }
            finally
            {
                MxFrameworkEditorProjectPaths.Unregister(provider);
            }
        }

        [Test]
        public void VfxBootstrap_AutomaticDiscoveryDoesNotLogErrors()
        {
            Assert.That(
                MxFrameworkPackageAssetLocator.TryResolveAssetPath(
                    "Editor/VfxPublishedBindingEditorBootstrap.cs",
                    out string sourcePath),
                Is.True);
            string source = File.ReadAllText(sourcePath);
            Assert.That(source, Does.Not.Contain("Debug.LogError"));
        }

        [Test]
        public void RepresentativeStyle_ResolvesAndLoads()
        {
            Assert.That(
                MxFrameworkPackageAssetLocator.TryResolveAssetPath(
                    "Editor/Levels/MxLevelEditorWindow.uss",
                    out string assetPath),
                Is.True);
            Assert.That(assetPath.Replace('\\', '/'),
                Is.EqualTo("Packages/com.vincent.wgameframework/Editor/Levels/MxLevelEditorWindow.uss"));
            Assert.That(AssetDatabase.LoadAssetAtPath<StyleSheet>(assetPath), Is.Not.Null);
        }

        [Test]
        public void PortableStyleCallers_ResolveThroughLocator()
        {
            string[] relativePaths =
            {
                "Editor/CharacterAction/CharacterActionAuthoringWindow.uss",
                "Editor/CharacterAction/Timeline/CharacterActionTimelineView.uss",
                "Editor/MxAttachmentTransformField.uss"
            };
            for (int i = 0; i < relativePaths.Length; i++)
            {
                Assert.That(
                    MxFrameworkPackageAssetLocator.TryResolveAssetPath(
                        relativePaths[i],
                        out string assetPath),
                    Is.True,
                    relativePaths[i]);
                Assert.That(
                    AssetDatabase.LoadAssetAtPath<StyleSheet>(assetPath),
                    Is.Not.Null,
                    relativePaths[i]);
            }
        }

        [Test]
        public void UnsafeRelativePaths_AreRejected()
        {
            string[] unsafePaths =
            {
                "/Editor/Levels/MxLevelEditorWindow.uss",
                "../Editor/Levels/MxLevelEditorWindow.uss",
                "Editor/../Levels/MxLevelEditorWindow.uss",
                "Editor\\Levels\\MxLevelEditorWindow.uss",
                " C:/outside.uss"
            };
            for (int i = 0; i < unsafePaths.Length; i++)
            {
                Assert.That(
                    MxFrameworkPackageAssetLocator.TryResolveAssetPath(
                        unsafePaths[i],
                        out string assetPath),
                    Is.False,
                    unsafePaths[i]);
                Assert.That(assetPath, Is.Empty, unsafePaths[i]);
            }
        }

        [Test]
        public void PaletteGenerator_MissingSourceDoesNotScheduleAssetWrites()
        {
            string root = Path.Combine(Path.GetTempPath(), "MxPaletteAbsent-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            string catalog = Path.Combine(root, "catalog.json");
            string texture = Path.Combine(root, "atlas.png");
            string receipt = Path.Combine(root, "receipt.json");
            EditorApplication.CallbackFunction callbacks = EditorApplication.delayCall;
            try
            {
                WGameCharacterPaletteAssetGenerator.EnsureGeneratedAssetExists(catalog, texture, receipt);
                Assert.That(EditorApplication.delayCall, Is.EqualTo(callbacks));
                Assert.That(Directory.GetFiles(root), Is.Empty);
            }
            finally
            {
                EditorApplication.delayCall = callbacks;
                Directory.Delete(root, true);
            }
        }

        [Test]
        public void PaletteGenerator_ExistingSourceSchedulesOnlyMissingOutputs()
        {
            string root = Path.Combine(Path.GetTempPath(), "MxPaletteSource-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            string catalog = Path.Combine(root, "catalog.json");
            string texture = Path.Combine(root, "atlas.png");
            string receipt = Path.Combine(root, "receipt.json");
            File.WriteAllText(catalog, "fixture source presence; generation is not executed");
            EditorApplication.CallbackFunction callbacks = EditorApplication.delayCall;
            try
            {
                for (int outputs = 0; outputs < 4; outputs++)
                {
                    if ((outputs & 1) != 0) File.WriteAllText(texture, "fixture");
                    else if (File.Exists(texture)) File.Delete(texture);
                    if ((outputs & 2) != 0) File.WriteAllText(receipt, "fixture");
                    else if (File.Exists(receipt)) File.Delete(receipt);
                    EditorApplication.delayCall = callbacks;
                    WGameCharacterPaletteAssetGenerator.EnsureGeneratedAssetExists(catalog, texture, receipt);
                    EditorApplication.CallbackFunction expected = callbacks;
                    if (outputs != 3) expected += WGameCharacterPaletteAssetGenerator.Generate;
                    Assert.That(EditorApplication.delayCall, Is.EqualTo(expected), "Output mask " + outputs);
                }
            }
            finally
            {
                EditorApplication.delayCall = callbacks;
                Directory.Delete(root, true);
            }
        }

        private sealed class ProbeProjectPathProvider : IMxFrameworkEditorProjectPathProvider
        {
            public bool TryGetProjectRelativePath(
                MxFrameworkEditorProjectPathKind kind,
                out string projectRelativePath)
            {
                projectRelativePath = kind == MxFrameworkEditorProjectPathKind.AbilityAuthoringJson
                    ? "Config/ability-authoring.json"
                    : string.Empty;
                return projectRelativePath.Length > 0;
            }
        }
    }
}
