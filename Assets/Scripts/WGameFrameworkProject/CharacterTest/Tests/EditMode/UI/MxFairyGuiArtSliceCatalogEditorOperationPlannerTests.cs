using System;
using System.IO;
using MxFramework.Editor;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MxFramework.Tests.UI
{
    public sealed class MxFairyGuiArtSliceCatalogEditorOperationPlannerTests
    {
        private string _tempRoot;

        [SetUp]
        public void SetUp()
        {
            _tempRoot = Path.GetFullPath(Path.Combine("Temp", "MxFairyGuiArtSlicePlannerTests", Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(_tempRoot);
        }

        [TearDown]
        public void TearDown()
        {
            if (!string.IsNullOrWhiteSpace(_tempRoot) && Directory.Exists(_tempRoot))
                Directory.Delete(_tempRoot, true);
        }

        [Test]
        public void PlanSaveCatalogSemantics_DeclaresCatalogOnlyModification()
        {
            MxFairyGuiArtSliceEditorSliceRecord slice = CreateSlice();

            MxFairyGuiArtSliceEditorOperationPlan plan =
                MxFairyGuiArtSliceCatalogEditorOperationPlanner.PlanSaveCatalogSemantics(
                    "FGUIProject/assets/MxCharacterTestHud/ui-art-slices.json",
                    slice);

            Assert.IsFalse(plan.Blocked, plan.ToDisplayText());
            Assert.AreEqual(MxFairyGuiArtSliceEditorOperationKind.SaveCatalogSemantics, plan.Kind);
            Assert.IsTrue(plan.WouldModifyCatalog);
            Assert.IsFalse(plan.WouldModifyPng);
            Assert.IsFalse(plan.WouldModifyFairyGuiXml);
            Assert.IsFalse(plan.WouldModifyPublishedOutput);
            Assert.IsFalse(plan.RequiresFairyGuiPublish);
            AssertHasDiagnostic(plan, "UIARTEDITOR003");
        }

        [Test]
        public void PlanReplaceExistingPng_BlocksTargetOutsidePackageRootBeforeReadingPng()
        {
            string packageRoot = Path.Combine(_tempRoot, "Package");
            Directory.CreateDirectory(packageRoot);
            string escapedTarget = Path.Combine(_tempRoot, "outside.png");

            MxFairyGuiArtSliceEditorOperationPlan plan =
                MxFairyGuiArtSliceCatalogEditorOperationPlanner.PlanReplaceExistingPng(
                    packageRoot,
                    CreateSlice(),
                    escapedTarget,
                    Path.Combine(_tempRoot, "missing-new.png"));

            Assert.IsTrue(plan.Blocked);
            Assert.IsFalse(plan.WouldModifyPng);
            Assert.IsFalse(plan.RequiresFairyGuiPublish);
            AssertHasDiagnostic(plan, "UIARTEDITOR013");
        }

        [Test]
        public void PlanReplaceExistingPng_BlocksReplacementThatViolatesPixelSizeConstraint()
        {
            string packageRoot = Path.Combine(_tempRoot, "Package");
            string targetPath = Path.Combine(packageRoot, "Images", "old.png");
            string replacementPath = Path.Combine(_tempRoot, "Incoming", "new.png");
            WritePng(targetPath, 2, 2, 128);
            WritePng(replacementPath, 3, 2, 128);

            MxFairyGuiArtSliceEditorOperationPlan plan =
                MxFairyGuiArtSliceCatalogEditorOperationPlanner.PlanReplaceExistingPng(
                    packageRoot,
                    CreateSlice(requireSamePixelSize: true),
                    targetPath,
                    replacementPath);

            Assert.IsTrue(plan.Blocked, plan.ToDisplayText());
            Assert.IsFalse(plan.WouldModifyPng);
            Assert.IsFalse(plan.RequiresFairyGuiPublish);
            AssertHasDiagnostic(plan, "UIARTEDITOR015");
        }

        [Test]
        public void PlanReplaceExistingPng_AllowsValidReplacementAndRequiresPublish()
        {
            string packageRoot = Path.Combine(_tempRoot, "Package");
            string targetPath = Path.Combine(packageRoot, "Images", "old.png");
            string replacementPath = Path.Combine(_tempRoot, "Incoming", "new.png");
            WritePng(targetPath, 2, 2, 128);
            WritePng(replacementPath, 2, 2, 128);

            MxFairyGuiArtSliceEditorOperationPlan plan =
                MxFairyGuiArtSliceCatalogEditorOperationPlanner.PlanReplaceExistingPng(
                    packageRoot,
                    CreateSlice(requireAlphaChannel: true, requireNonOpaqueAlpha: true, requireSamePixelSize: true),
                    targetPath,
                    replacementPath);

            Assert.IsFalse(plan.Blocked, plan.ToDisplayText());
            Assert.IsFalse(plan.WouldModifyCatalog);
            Assert.IsTrue(plan.WouldModifyPng);
            Assert.IsFalse(plan.WouldModifyFairyGuiXml);
            Assert.IsFalse(plan.WouldModifyPublishedOutput);
            Assert.IsTrue(plan.RequiresFairyGuiPublish);
            AssertHasDiagnostic(plan, "UIARTEDITOR016");
        }

        [Test]
        public void PlanRegisterDraftSlice_DeclaresCatalogOnlyDraftAndFairyGuiAuthoringWarning()
        {
            MxFairyGuiArtSliceEditorOperationPlan plan =
                MxFairyGuiArtSliceCatalogEditorOperationPlanner.PlanRegisterDraftSlice(
                    "FGUIProject/assets/MxCharacterTestHud/ui-art-slices.json",
                    "draft.test",
                    "Images/draft.png");

            Assert.IsFalse(plan.Blocked, plan.ToDisplayText());
            Assert.AreEqual(MxFairyGuiArtSliceEditorOperationKind.RegisterDraftSlice, plan.Kind);
            Assert.IsTrue(plan.WouldModifyCatalog);
            Assert.IsFalse(plan.WouldModifyPng);
            Assert.IsFalse(plan.WouldModifyFairyGuiXml);
            Assert.IsFalse(plan.WouldModifyPublishedOutput);
            Assert.IsFalse(plan.RequiresFairyGuiPublish);
            AssertHasDiagnostic(plan, "UIARTEDITOR022");
            AssertHasDiagnostic(plan, "UIARTEDITOR023");
        }

        private static MxFairyGuiArtSliceEditorSliceRecord CreateSlice(
            bool requireAlphaChannel = false,
            bool requireNonOpaqueAlpha = false,
            bool requireSamePixelSize = false)
        {
            return new MxFairyGuiArtSliceEditorSliceRecord(
                new JObject(),
                "slice.test",
                "resource-test",
                "Images/old.png",
                "Images/old.png",
                "Icon",
                "Test Icon",
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                requireAlphaChannel,
                requireNonOpaqueAlpha,
                requireSamePixelSize,
                false,
                0,
                0,
                Array.Empty<MxFairyGuiArtSliceEditorUsageRecord>());
        }

        private static void WritePng(string path, int width, int height, byte alpha)
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            var texture = new Texture2D(width, height, TextureFormat.RGBA32, mipChain: false);
            try
            {
                var pixels = new Color32[width * height];
                for (int i = 0; i < pixels.Length; i++)
                    pixels[i] = new Color32(255, 255, 255, alpha);

                texture.SetPixels32(pixels);
                byte[] png = texture.EncodeToPNG();
                Assert.IsNotNull(png);
                File.WriteAllBytes(path, png);
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }

        private static void AssertHasDiagnostic(MxFairyGuiArtSliceEditorOperationPlan plan, string code)
        {
            for (int i = 0; i < plan.Diagnostics.Count; i++)
            {
                if (string.Equals(plan.Diagnostics[i].Code, code, StringComparison.Ordinal))
                    return;
            }

            Assert.Fail("Expected diagnostic " + code + " in plan: " + plan.ToDisplayText());
        }
    }
}
