using System.Collections.Generic;
using MxFramework.Combat.Authoring;
using NUnit.Framework;
using UnityEngine;

namespace MxFramework.Tests.Combat.Authoring
{
    public class CombatAuthoringJsonExporterTests
    {
        private readonly List<Object> _createdAssets = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _createdAssets.Count; i++)
            {
                Object.DestroyImmediate(_createdAssets[i]);
            }

            _createdAssets.Clear();
        }

        [Test]
        public void Export_ValidAuthoringPackage_CreatesManifestFilesHashesAndWarnings()
        {
            CombatActionAuthoringAsset action = CreateAction("ExportAction");
            action.Hitboxes = new[]
            {
                Shape(2, 3, 4, "root", 100000, 20),
                Shape(1, 0, 2, "root", 100000, 10, CombatAuthoringShapeKind.Capsule, 150000),
            };
            CombatSceneBindingAsset binding = CreateBinding("BindingB");
            binding.BindingProfileId = "Combat Animation Physics Test";
            binding.Markers = new[]
            {
                Marker("root", "Root", 2),
                Marker("tip", "Tip", 1),
            };

            CombatAuthoringExportResult result = CombatAuthoringJsonExporter.Export(
                action,
                binding,
                "Package A",
                "source-guid",
                "M10G-Test");

            Assert.IsTrue(result.Success);
            Assert.IsFalse(result.ValidationReport.HasErrors);
            Assert.AreEqual(1, result.ValidationReport.IssueCount);
            Assert.AreEqual("package_a", result.Context.PackageId);
            Assert.AreEqual("source-guid", result.Manifest.SourceAssetGuid);
            Assert.IsFalse(string.IsNullOrEmpty(result.Context.AuthoringHash));
            Assert.IsFalse(string.IsNullOrEmpty(result.Context.RuntimeDataHash));
            Assert.IsFalse(string.IsNullOrEmpty(result.Context.JsonPackageHash));
            Assert.AreEqual(result.Context.JsonPackageHash, result.Manifest.ContentHash);
            AssertFileExists(result, "manifest.json");
            AssertFileExists(result, "schema/combat_authoring.schema.json");
            AssertFileExists(result, "actions/action_400001.json");
            AssertFileExists(result, "scene_bindings/combat_animation_physics_test.json");
            AssertFileExists(result, "reports/validation_report.txt");
            AssertFileExists(result, "reports/validation_report.json");
            StringAssert.Contains("\"packageId\":\"package_a\"", GetFile(result, "manifest.json").Content);
            StringAssert.Contains("\"actionId\":400001", GetFile(result, "actions/action_400001.json").Content);
            StringAssert.Contains("\"shapeKind\":\"capsule\"", GetFile(result, "actions/action_400001.json").Content);
            StringAssert.Contains("Capsule height is smaller than diameter.", result.ReportText);
            AssertTrackOrder(result, "\"trackId\":1", "\"trackId\":2");
        }

        [Test]
        public void Export_WithValidationError_BlocksPackageFiles()
        {
            CombatActionAuthoringAsset action = CreateAction("InvalidExportAction");
            action.TotalFrames = 0;
            action.Hitboxes = new[]
            {
                Shape(1, 0, 1, "missing", -1, 1),
            };
            CombatSceneBindingAsset binding = CreateBinding("InvalidBinding");

            CombatAuthoringExportResult result = CombatAuthoringJsonExporter.Export(
                action,
                binding,
                "Package Invalid",
                "source-guid",
                "M10G-Test");

            Assert.IsFalse(result.Success);
            Assert.IsTrue(result.ValidationReport.HasErrors);
            Assert.AreEqual(0, result.Package.FileCount);
            StringAssert.Contains("Validation gate failed", result.ReportText);
            StringAssert.Contains("JSON package was not generated", result.ReportText);
        }

        [Test]
        public void Export_SameInput_ProducesStableHashesAndFileList()
        {
            CombatActionAuthoringAsset action = CreateAction("StableExportAction");
            action.Hitboxes = new[]
            {
                Shape(3, 3, 4, "root", 100000, 30),
                Shape(1, 0, 1, "root", 100000, 10),
                Shape(2, 2, 2, "root", 100000, 20),
            };
            CombatSceneBindingAsset binding = CreateBinding("StableBinding");
            binding.BindingProfileId = "Stable Binding";
            binding.Markers = new[]
            {
                Marker("z", "Z", 30),
                Marker("root", "Root", 10),
                Marker("a", "A", 20),
            };

            CombatAuthoringExportResult first = CombatAuthoringJsonExporter.Export(action, binding, "Stable Package", "guid", "M10G-Test");
            CombatAuthoringExportResult second = CombatAuthoringJsonExporter.Export(action, binding, "Stable Package", "guid", "M10G-Test");

            Assert.IsTrue(first.Success);
            Assert.IsTrue(second.Success);
            Assert.AreEqual(first.Context.AuthoringHash, second.Context.AuthoringHash);
            Assert.AreEqual(first.Context.RuntimeDataHash, second.Context.RuntimeDataHash);
            Assert.AreEqual(first.Context.JsonPackageHash, second.Context.JsonPackageHash);
            Assert.AreEqual(first.Manifest.ContentHash, second.Manifest.ContentHash);
            Assert.AreEqual(FileOrder(first), FileOrder(second));
            Assert.AreEqual(
                GetFile(first, "actions/action_400001.json").Content,
                GetFile(second, "actions/action_400001.json").Content);
            AssertMarkerOrder(first, "\"markerId\":\"a\"", "\"markerId\":\"root\"", "\"markerId\":\"z\"");
        }

        private static void AssertFileExists(CombatAuthoringExportResult result, string path)
        {
            GetFile(result, path);
        }

        private static CombatAuthoringExportFile GetFile(CombatAuthoringExportResult result, string path)
        {
            CombatAuthoringExportFile[] files = result.Files;
            for (int i = 0; i < files.Length; i++)
            {
                if (files[i].Path == path)
                {
                    return files[i];
                }
            }

            Assert.Fail("Expected export file was not found: {0}", path);
            return null;
        }

        private static void AssertTrackOrder(CombatAuthoringExportResult result, string first, string second)
        {
            string content = GetFile(result, "actions/action_400001.json").Content;
            Assert.Less(content.IndexOf(first, System.StringComparison.Ordinal), content.IndexOf(second, System.StringComparison.Ordinal));
        }

        private static void AssertMarkerOrder(CombatAuthoringExportResult result, string first, string second, string third)
        {
            string content = GetFile(result, "scene_bindings/stable_binding.json").Content;
            int firstIndex = content.IndexOf(first, System.StringComparison.Ordinal);
            int secondIndex = content.IndexOf(second, System.StringComparison.Ordinal);
            int thirdIndex = content.IndexOf(third, System.StringComparison.Ordinal);
            Assert.Less(firstIndex, secondIndex);
            Assert.Less(secondIndex, thirdIndex);
        }

        private static string FileOrder(CombatAuthoringExportResult result)
        {
            CombatAuthoringExportFile[] files = result.Files;
            var paths = new List<string>(files.Length);
            for (int i = 0; i < files.Length; i++)
            {
                paths.Add(files[i].Path);
            }

            return string.Join("|", paths.ToArray());
        }

        private CombatActionAuthoringAsset CreateAction(string name)
        {
            var action = ScriptableObject.CreateInstance<CombatActionAuthoringAsset>();
            action.name = name;
            action.ActionId = 400001;
            action.TotalFrames = 8;
            action.Startup = new CombatAuthoringFrameRange(0, 1);
            action.Active = new CombatAuthoringFrameRange(2, 4);
            action.Recovery = new CombatAuthoringFrameRange(5, 7);
            _createdAssets.Add(action);
            return action;
        }

        private CombatSceneBindingAsset CreateBinding(string name)
        {
            var binding = ScriptableObject.CreateInstance<CombatSceneBindingAsset>();
            binding.name = name;
            _createdAssets.Add(binding);
            return binding;
        }

        private static CombatShapeAuthoringData Shape(
            int trackId,
            int start,
            int end,
            string marker,
            int radiusRaw,
            int sourceOrder,
            CombatAuthoringShapeKind shapeKind = CombatAuthoringShapeKind.Sphere,
            int heightRaw = 0)
        {
            return new CombatShapeAuthoringData
            {
                TrackId = trackId,
                ShapeKind = shapeKind,
                FrameRange = new CombatAuthoringFrameRange(start, end),
                MarkerId = marker,
                LocalCenter = new Vector3(trackId, start, end),
                RadiusRaw = radiusRaw,
                HeightRaw = heightRaw,
                SourceOrder = sourceOrder,
            };
        }

        private static CombatMarkerBindingData Marker(string markerId, string targetPath, int sourceOrder)
        {
            return new CombatMarkerBindingData
            {
                MarkerId = markerId,
                TargetPath = targetPath,
                SourceOrder = sourceOrder,
            };
        }
    }
}
