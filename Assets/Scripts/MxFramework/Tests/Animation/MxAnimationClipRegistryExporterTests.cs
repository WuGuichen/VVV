using MxFramework.Animation;
using MxFramework.Editor.Animation;
using MxFramework.Resources;
using NUnit.Framework;
using UnityEngine;

namespace MxFramework.Tests.Animation
{
    public sealed class MxAnimationClipRegistryExporterTests
    {
        [Test]
        public void Export_CreatesCatalogReadyResourceKeyDefinition()
        {
            MxAnimationClipRegistryAsset asset = ScriptableObject.CreateInstance<MxAnimationClipRegistryAsset>();
            var idleClip = new AnimationClip { name = "Idle" };
            var fallbackClip = new AnimationClip { name = "Fallback" };
            try
            {
                asset.AnimationSetId = "demo.set";
                asset.Version = 2;
                asset.PackageId = "demo.package";
                asset.Clips = new[]
                {
                    new MxAnimationClipRegistryClipEntry
                    {
                        ClipId = "idle",
                        Clip = idleClip,
                        ResourceId = "demo.animation.idle",
                        IsDefault = true
                    },
                    new MxAnimationClipRegistryClipEntry
                    {
                        ClipId = "fallback",
                        Clip = fallbackClip,
                        ResourceId = "demo.animation.fallback",
                        IsFallback = true
                    }
                };
                asset.Bindings = new[]
                {
                    new MxAnimationClipRegistryBindingEntry
                    {
                        BindingId = "idle",
                        ActionId = 1,
                        ClipId = "idle",
                        LayerId = "base",
                        PlaybackSpeed = 1.25f,
                        Loop = true,
                        FadeDurationSeconds = 0.2f
                    }
                };
                var catalog = new ResourceCatalog(
                    "demo.catalog",
                    "demo.package",
                    new[]
                    {
                        new ResourceCatalogEntry("demo.animation.idle", ResourceTypeIds.AnimationClip, "memory", "idle"),
                        new ResourceCatalogEntry("demo.animation.fallback", ResourceTypeIds.AnimationClip, "memory", "fallback")
                    });

                MxAnimationClipRegistryExportResult result = MxAnimationClipRegistryExporter.Export(asset, catalog);

                Assert.IsTrue(result.Success, MxAnimationClipRegistryExporter.CreateReportText(result));
                Assert.AreEqual("demo.set", result.Definition.SetId);
                Assert.AreEqual(2, result.Definition.Version);
                Assert.That(result.Definition.DefinitionHash, Does.StartWith(MxAnimationSetDefinitionHasher.HashPrefix));
                Assert.AreEqual("demo.animation.idle", result.Definition.DefaultClip.Id);
                Assert.AreEqual(ResourceTypeIds.AnimationClip, result.Definition.Actions[0].Clip.TypeId);
                Assert.AreEqual("action:1", result.Definition.Actions[0].ActionKey);
                Assert.AreEqual(0.2f, result.Definition.Actions[0].FadeDurationSeconds);
                Assert.That(result.Definition.Actions[0].Clip.Id, Does.Not.Contain("Assets/"));
            }
            finally
            {
                Object.DestroyImmediate(idleClip);
                Object.DestroyImmediate(fallbackClip);
                Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void Export_ReportsDuplicateBindingWrongCatalogTypeAndMissingClipReference()
        {
            MxAnimationClipRegistryAsset asset = ScriptableObject.CreateInstance<MxAnimationClipRegistryAsset>();
            var idleClip = new AnimationClip { name = "Idle" };
            try
            {
                asset.AnimationSetId = "demo.set";
                asset.Version = 1;
                asset.PackageId = "demo.package";
                asset.Clips = new[]
                {
                    new MxAnimationClipRegistryClipEntry
                    {
                        ClipId = "idle",
                        Clip = idleClip,
                        ResourceId = "demo.animation.idle",
                        IsDefault = true,
                        IsFallback = true
                    },
                    new MxAnimationClipRegistryClipEntry
                    {
                        ClipId = "attack",
                        ResourceId = "demo.animation.attack"
                    }
                };
                asset.Bindings = new[]
                {
                    new MxAnimationClipRegistryBindingEntry
                    {
                        BindingId = "shared",
                        ActionKey = "action:1",
                        ClipId = "idle"
                    },
                    new MxAnimationClipRegistryBindingEntry
                    {
                        BindingId = "shared",
                        ActionKey = "action:1",
                        ClipId = "attack"
                    }
                };
                var catalog = new ResourceCatalog(
                    "demo.catalog",
                    "demo.package",
                    new[]
                    {
                        new ResourceCatalogEntry("demo.animation.idle", ResourceTypeIds.TextAsset, "memory", "idle"),
                        new ResourceCatalogEntry("demo.animation.attack", ResourceTypeIds.AnimationClip, "memory", "attack")
                    });

                MxAnimationClipRegistryExportResult result = MxAnimationClipRegistryExporter.Export(asset, catalog);

                Assert.IsFalse(result.Success);
                AssertIssue(result.ValidationReport, "ClipReferenceMissing");
                AssertIssue(result.ValidationReport, "ClipCatalogTypeMismatch");
                AssertIssue(result.ValidationReport, "DuplicateBindingId");
                AssertIssue(result.ValidationReport, "DuplicateActionKey");
            }
            finally
            {
                Object.DestroyImmediate(idleClip);
                Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void ExportStructureOnly_AllowsInspectorValidationWithoutCatalog()
        {
            MxAnimationClipRegistryAsset asset = ScriptableObject.CreateInstance<MxAnimationClipRegistryAsset>();
            var idleClip = new AnimationClip { name = "Idle" };
            try
            {
                asset.AnimationSetId = "demo.set";
                asset.Version = 1;
                asset.Clips = new[]
                {
                    new MxAnimationClipRegistryClipEntry
                    {
                        ClipId = "idle",
                        Clip = idleClip,
                        ResourceId = "demo.animation.idle",
                        IsDefault = true,
                        IsFallback = true
                    }
                };
                asset.Bindings = new[]
                {
                    new MxAnimationClipRegistryBindingEntry
                    {
                        BindingId = "idle",
                        ActionKey = "action:1",
                        ClipId = "idle"
                    }
                };

                MxAnimationClipRegistryExportResult result =
                    MxAnimationClipRegistryExporter.ExportStructureOnly(asset);

                Assert.IsTrue(result.Success, MxAnimationClipRegistryExporter.CreateReportText(result));
            }
            finally
            {
                Object.DestroyImmediate(idleClip);
                Object.DestroyImmediate(asset);
            }
        }

        private static void AssertIssue(ResourceCatalogValidationReport report, string code)
        {
            for (int i = 0; i < report.Issues.Count; i++)
            {
                if (report.Issues[i].Code == code)
                    return;
            }

            Assert.Fail("Expected clip registry validation issue: " + code);
        }
    }
}
