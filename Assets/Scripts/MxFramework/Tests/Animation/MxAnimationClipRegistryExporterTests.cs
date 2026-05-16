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
            var upperMask = new AvatarMask { name = "UpperBody" };
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
                asset.Layers = new[]
                {
                    new MxAnimationClipRegistryLayerEntry
                    {
                        LayerId = "base",
                        ProfileId = "humanoid.base",
                        DefaultWeight = 1f
                    },
                    new MxAnimationClipRegistryLayerEntry
                    {
                        LayerId = "upper_body",
                        ProfileId = "humanoid.upper",
                        DefaultWeight = 0f,
                        AvatarMask = upperMask,
                        AvatarMaskResourceId = "demo.animation.mask.upper_body"
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
                        new ResourceCatalogEntry("demo.animation.fallback", ResourceTypeIds.AnimationClip, "memory", "fallback"),
                        new ResourceCatalogEntry("demo.animation.mask.upper_body", ResourceTypeIds.AvatarMask, "memory", "mask")
                    });

                MxAnimationClipRegistryExportResult result = MxAnimationClipRegistryExporter.Export(asset, catalog);

                Assert.IsTrue(result.Success, MxAnimationClipRegistryExporter.CreateReportText(result));
                Assert.AreEqual("demo.set", result.Definition.SetId);
                Assert.AreEqual(2, result.Definition.Version);
                Assert.That(result.Definition.DefinitionHash, Does.StartWith(MxAnimationSetDefinitionHasher.HashPrefix));
                Assert.AreEqual("demo.animation.idle", result.Definition.DefaultClip.Id);
                Assert.AreEqual(ResourceTypeIds.AnimationClip, result.Definition.Actions[0].Clip.TypeId);
                Assert.AreEqual("action:1", result.Definition.Actions[0].ActionKey);
                Assert.AreEqual(2, result.Definition.Layers.Count);
                Assert.AreEqual("humanoid.upper", result.Definition.Layers[1].ProfileId);
                Assert.AreEqual(ResourceTypeIds.AvatarMask, result.Definition.Layers[1].AvatarMaskKey.TypeId);
                Assert.AreEqual(0.2f, result.Definition.Actions[0].FadeDurationSeconds);
                Assert.That(result.Definition.Actions[0].Clip.Id, Does.Not.Contain("Assets/"));
                Assert.That(result.Definition.Layers[1].AvatarMaskKey.Id, Does.Not.Contain("Assets/"));
            }
            finally
            {
                Object.DestroyImmediate(idleClip);
                Object.DestroyImmediate(fallbackClip);
                Object.DestroyImmediate(upperMask);
                Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void Export_PreservesPresentationEventsAsResourceKeyTimelineRows()
        {
            MxAnimationClipRegistryAsset asset = ScriptableObject.CreateInstance<MxAnimationClipRegistryAsset>();
            var attackClip = new AnimationClip { name = "Attack" };
            var fallbackClip = new AnimationClip { name = "Fallback" };
            try
            {
                asset.AnimationSetId = "combat.demo";
                asset.Version = 3;
                asset.PackageId = "demo.package";
                asset.Clips = new[]
                {
                    new MxAnimationClipRegistryClipEntry
                    {
                        ClipId = "attack",
                        Clip = attackClip,
                        ResourceId = "demo.animation.attack",
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
                        BindingId = "attack",
                        ActionKey = "action:1001",
                        ClipId = "attack",
                        Events = new[]
                        {
                            new MxAnimationClipRegistryEventEntry
                            {
                                EventId = "event:77",
                                TimeDomain = MxAnimationEventTimeDomain.CombatFrame,
                                Time = 6f,
                                EventKind = "VFX",
                                PayloadResourceId = "demo.vfx.slash",
                                PayloadTypeId = ResourceTypeIds.GameObject,
                                Socket = "weapon",
                                Tag = "slash"
                            }
                        }
                    }
                };
                asset.Events = new[]
                {
                    new MxAnimationClipRegistryEventEntry
                    {
                        EventId = "event:intro",
                        TimeDomain = MxAnimationEventTimeDomain.Seconds,
                        Time = 0.15f,
                        EventKind = "SFX",
                        PayloadResourceId = "demo.sfx.swing",
                        PayloadTypeId = ResourceTypeIds.AudioClip,
                        ReplayPolicy = MxAnimationPresentationEventReplayPolicy.CatchUpSafe
                    }
                };

                MxAnimationClipRegistryExportResult result =
                    MxAnimationClipRegistryExporter.ExportStructureOnly(asset);
                System.Collections.Generic.IReadOnlyList<MxAnimationEventTimelineRow> rows =
                    MxAnimationEventTimelineBuilder.BuildRows(result.Definition);

                Assert.IsTrue(result.Success, MxAnimationClipRegistryExporter.CreateReportText(result));
                Assert.AreEqual(2, rows.Count);
                Assert.IsTrue(ContainsTimelineRow(rows, "event:77", true));
                Assert.IsTrue(ContainsTimelineRow(rows, "event:intro", false));
                Assert.AreEqual(ResourceTypeIds.GameObject, result.Definition.Actions[0].PresentationEvents[0].PayloadKey.TypeId);
                Assert.That(result.Definition.Actions[0].PresentationEvents[0].PayloadKey.Id, Does.Not.Contain("Assets/"));
                Assert.AreEqual(MxAnimationPresentationEventReplayPolicy.CatchUpSafe, result.Definition.Events[0].ReplayPolicy);
            }
            finally
            {
                Object.DestroyImmediate(attackClip);
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
                asset.Layers = new[]
                {
                    new MxAnimationClipRegistryLayerEntry
                    {
                        LayerId = "upper_body",
                        AvatarMaskResourceId = "demo.animation.mask.upper_body"
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
                AssertIssue(result.ValidationReport, "AvatarMaskReferenceMissing");
                AssertIssue(result.ValidationReport, "AvatarMaskCatalogEntryMissing");
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

        private static bool ContainsTimelineRow(
            System.Collections.Generic.IReadOnlyList<MxAnimationEventTimelineRow> rows,
            string eventId,
            bool hasDeterministicCorrelation)
        {
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i].EventId == eventId
                    && rows[i].HasDeterministicCorrelation == hasDeterministicCorrelation)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
