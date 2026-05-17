using MxFramework.Animation;
using MxFramework.Combat.Animation;
using MxFramework.Combat.Authoring;
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
        public void Export_MapsBlendDefinitionsThroughClipRegistryKeys()
        {
            MxAnimationClipRegistryAsset asset = ScriptableObject.CreateInstance<MxAnimationClipRegistryAsset>();
            var idleClip = new AnimationClip { name = "Idle" };
            var walkClip = new AnimationClip { name = "Walk" };
            var runClip = new AnimationClip { name = "Run" };
            var strafeClip = new AnimationClip { name = "Strafe" };
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
                        IsDefault = true,
                        IsFallback = true
                    },
                    new MxAnimationClipRegistryClipEntry
                    {
                        ClipId = "walk",
                        Clip = walkClip,
                        ResourceId = "demo.animation.walk"
                    },
                    new MxAnimationClipRegistryClipEntry
                    {
                        ClipId = "run",
                        Clip = runClip,
                        ResourceId = "demo.animation.run"
                    },
                    new MxAnimationClipRegistryClipEntry
                    {
                        ClipId = "strafe",
                        Clip = strafeClip,
                        ResourceId = "demo.animation.strafe"
                    }
                };
                asset.Blend1DDefinitions = new[]
                {
                    new MxAnimationClipRegistryBlend1DEntry
                    {
                        BlendId = "locomotion",
                        ParameterId = "speed",
                        LayerId = "base",
                        ParameterScale = 1000,
                        FadeDurationSeconds = 0.25f,
                        Points = new[]
                        {
                            new MxAnimationClipRegistryBlend1DPointEntry
                            {
                                Threshold = 0,
                                ClipId = "idle",
                                PlaybackSpeed = 1f,
                                Loop = true
                            },
                            new MxAnimationClipRegistryBlend1DPointEntry
                            {
                                Threshold = 500,
                                ClipId = "walk",
                                PlaybackSpeed = 1.1f,
                                Loop = true
                            },
                            new MxAnimationClipRegistryBlend1DPointEntry
                            {
                                Threshold = 1000,
                                ClipId = "run",
                                PlaybackSpeed = 1.2f,
                                Loop = true
                            }
                        }
                    }
                };
                asset.Blend2DDefinitions = new[]
                {
                    new MxAnimationClipRegistryBlend2DEntry
                    {
                        BlendId = "move2d",
                        ParameterXId = "move.x",
                        ParameterYId = "move.y",
                        LayerId = "base",
                        ParameterXScale = 1000,
                        ParameterYScale = 1000,
                        FadeDurationSeconds = 0.15f,
                        Points = new[]
                        {
                            new MxAnimationClipRegistryBlend2DPointEntry
                            {
                                X = 0,
                                Y = 0,
                                ClipId = "idle",
                                PlaybackSpeed = 1f,
                                Loop = true
                            },
                            new MxAnimationClipRegistryBlend2DPointEntry
                            {
                                X = 1000,
                                Y = 0,
                                ClipId = "strafe",
                                PlaybackSpeed = 1.05f,
                                Loop = true
                            }
                        }
                    }
                };
                var catalog = new ResourceCatalog(
                    "demo.catalog",
                    "demo.package",
                    new[]
                    {
                        new ResourceCatalogEntry("demo.animation.idle", ResourceTypeIds.AnimationClip, "memory", "idle"),
                        new ResourceCatalogEntry("demo.animation.walk", ResourceTypeIds.AnimationClip, "memory", "walk"),
                        new ResourceCatalogEntry("demo.animation.run", ResourceTypeIds.AnimationClip, "memory", "run"),
                        new ResourceCatalogEntry("demo.animation.strafe", ResourceTypeIds.AnimationClip, "memory", "strafe")
                    });

                MxAnimationClipRegistryExportResult result = MxAnimationClipRegistryExporter.Export(asset, catalog);

                Assert.IsTrue(result.Success, MxAnimationClipRegistryExporter.CreateReportText(result));
                Assert.AreEqual(1, result.Definition.Blend1DDefinitions.Count);
                Assert.AreEqual(1, result.Definition.Blend2DDefinitions.Count);
                Assert.AreEqual("locomotion", result.Definition.Blend1DDefinitions[0].BlendId);
                Assert.AreEqual("speed", result.Definition.Blend1DDefinitions[0].ParameterId);
                Assert.AreEqual("base", result.Definition.Blend1DDefinitions[0].LayerId.Value);
                Assert.AreEqual(0.25f, result.Definition.Blend1DDefinitions[0].FadeDurationSeconds);
                Assert.AreEqual("demo.animation.walk", result.Definition.Blend1DDefinitions[0].Points[1].ClipKey.Id);
                Assert.AreEqual(1.1f, result.Definition.Blend1DDefinitions[0].Points[1].PlaybackSpeed);
                Assert.AreEqual("move2d", result.Definition.Blend2DDefinitions[0].BlendId);
                Assert.AreEqual("move.x", result.Definition.Blend2DDefinitions[0].ParameterXId);
                Assert.AreEqual("move.y", result.Definition.Blend2DDefinitions[0].ParameterYId);
                Assert.AreEqual("demo.animation.strafe", result.Definition.Blend2DDefinitions[0].Points[1].ClipKey.Id);
                Assert.AreEqual(1000, result.Definition.Blend2DDefinitions[0].Points[1].X);
            }
            finally
            {
                Object.DestroyImmediate(idleClip);
                Object.DestroyImmediate(walkClip);
                Object.DestroyImmediate(runClip);
                Object.DestroyImmediate(strafeClip);
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

        [Test]
        public void TimelineScrubberPreview_AlignsActionEventsCombatTimelineAndBakeSamples()
        {
            MxAnimationClipRegistryAsset asset = CreateScrubberRegistry("event:77", eventFrame: 3);
            var clip = new AnimationClip { name = "Attack" };
            try
            {
                MxAnimationClipRegistryClipEntry[] clips = asset.Clips;
                clips[0].Clip = clip;
                asset.Clips = clips;
                MxAnimationClipRegistryExportResult result = MxAnimationClipRegistryExporter.ExportStructureOnly(asset);
                MxAnimationBakeArtifact bake = CreateBakeArtifact(asset.Clips[0].CreateResourceKey(asset.PackageId), frame: 3);
                var timeline = new CombatActionTimeline(
                    actionId: 1001,
                    totalFrames: 8,
                    startup: new CombatFrameRange(0, 1),
                    active: new CombatFrameRange(2, 4),
                    recovery: new CombatFrameRange(5, 7),
                    windows: new[] { new CombatActionWindow(CombatActionWindowKind.Cancel, new CombatFrameRange(3, 3), targetActionId: 2002) },
                    events: new[] { new CombatActionFrameEvent(3, 77) });

                MxAnimationTimelineScrubberPreview preview =
                    MxAnimationTimelineScrubberPreviewBuilder.Build(result.Definition, "action:1001", 3, bake, timeline);

                Assert.IsFalse(preview.HasErrors, MxAnimationTimelineScrubberPreviewBuilder.CreateSummary(preview));
                Assert.AreEqual("attack", preview.BindingId);
                Assert.AreEqual(0.1f, preview.Seconds, 0.0001f);
                Assert.IsTrue(ContainsScrubberRow(preview, MxAnimationTimelineScrubberRowKind.PresentationEvent, "event:77"));
                Assert.IsTrue(ContainsScrubberRow(preview, MxAnimationTimelineScrubberRowKind.CombatFrameEvent, "event:77"));
                Assert.IsTrue(ContainsScrubberRow(preview, MxAnimationTimelineScrubberRowKind.CombatWindow, "Active"));
                Assert.IsTrue(ContainsScrubberRow(preview, MxAnimationTimelineScrubberRowKind.CombatWindow, "Cancel"));
                Assert.IsTrue(ContainsScrubberRow(preview, MxAnimationTimelineScrubberRowKind.RootMotion, "root"));
                Assert.IsTrue(ContainsScrubberRow(preview, MxAnimationTimelineScrubberRowKind.Socket, "weapon"));
                Assert.IsTrue(ContainsScrubberRow(preview, MxAnimationTimelineScrubberRowKind.WeaponTrace, "trace:7"));
                Assert.IsFalse(ContainsScrubberDiagnostic(preview, "TimelineFrameMismatch"));
            }
            finally
            {
                Object.DestroyImmediate(clip);
                Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void TimelineScrubberPreview_ReportsMismatchDiagnostics()
        {
            MxAnimationClipRegistryAsset asset = CreateScrubberRegistry("event:77", eventFrame: 99);
            var clip = new AnimationClip { name = "Attack" };
            try
            {
                MxAnimationClipRegistryClipEntry[] clips = asset.Clips;
                clips[0].Clip = clip;
                asset.Clips = clips;
                MxAnimationClipRegistryExportResult result = MxAnimationClipRegistryExporter.ExportStructureOnly(asset);
                var wrongClip = new ResourceKey("demo.animation.other", ResourceTypeIds.AnimationClip, packageId: asset.PackageId);
                MxAnimationBakeArtifact bake = CreateBakeArtifact(wrongClip, frame: 0);
                var timeline = new CombatActionTimeline(
                    actionId: 1001,
                    totalFrames: 8,
                    startup: new CombatFrameRange(0, 1),
                    active: new CombatFrameRange(2, 4),
                    recovery: new CombatFrameRange(5, 7),
                    windows: null,
                    events: new[] { new CombatActionFrameEvent(3, 99) });

                MxAnimationTimelineScrubberPreview preview =
                    MxAnimationTimelineScrubberPreviewBuilder.Build(result.Definition, "attack", 3, bake, timeline);

                Assert.IsTrue(ContainsScrubberDiagnostic(preview, "EventOutOfRange"));
                Assert.IsTrue(ContainsScrubberDiagnostic(preview, "BakeSourceClipMismatch"));
                Assert.IsTrue(ContainsScrubberDiagnostic(preview, "BakeFrameMissing"));
                Assert.IsTrue(ContainsScrubberDiagnostic(preview, "TimelineFrameMismatch"));
            }
            finally
            {
                Object.DestroyImmediate(clip);
                Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void TimelineScrubberPreview_NormalizedEventRoundTripsAgainstPreviewMaxFrame()
        {
            MxAnimationClipRegistryAsset asset = CreateScrubberRegistry("event:half", eventFrame: 0);
            try
            {
                MxAnimationClipRegistryBindingEntry[] bindings = asset.Bindings;
                MxAnimationClipRegistryEventEntry[] events = bindings[0].Events;
                events[0].TimeDomain = MxAnimationEventTimeDomain.NormalizedTime;
                events[0].Time = 0.5f;
                bindings[0].Events = events;
                asset.Bindings = bindings;

                MxAnimationClipRegistryExportResult result = MxAnimationClipRegistryExporter.ExportStructureOnly(asset);
                MxAnimationBakeArtifact bake = CreateBakeArtifact(asset.Clips[0].CreateResourceKey(asset.PackageId), frame: 30);

                MxAnimationTimelineScrubberPreview preview =
                    MxAnimationTimelineScrubberPreviewBuilder.Build(result.Definition, "attack", 15, bake);

                Assert.IsTrue(ContainsScrubberRow(preview, MxAnimationTimelineScrubberRowKind.PresentationEvent, "event:half"));
                Assert.AreEqual(0.5f, preview.NormalizedTime, 0.0001f);
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void TimelineEventTimeUtility_NormalizedTimeUsesPreviewMaxFrame()
        {
            int previewMaxFrame = MxAnimationTimelineEventTimeUtility.ResolvePreviewMaxFrame(
                clipLengthSeconds: 1f,
                sampleRate: 30,
                combatTotalFrames: -1);

            float normalizedTime = MxAnimationTimelineEventTimeUtility.FrameToEventTime(
                MxAnimationEventTimeDomain.NormalizedTime,
                frame: 15,
                previewMaxFrame: previewMaxFrame,
                sampleRate: 30);

            Assert.AreEqual(30, previewMaxFrame);
            Assert.AreEqual(0.5f, normalizedTime, 0.0001f);
        }

        [Test]
        public void TimelineScrubberPreview_AutoBakeWithRegistryClipKeyDoesNotReportSourceMismatch()
        {
            MxAnimationClipRegistryAsset asset = CreateScrubberRegistry("event:77", eventFrame: 0);
            var clip = new AnimationClip { name = "Attack Preview" };
            try
            {
                MxAnimationClipRegistryClipEntry[] clips = asset.Clips;
                clips[0].Clip = clip;
                asset.Clips = clips;

                MxAnimationClipRegistryExportResult result = MxAnimationClipRegistryExporter.ExportStructureOnly(asset);
                ResourceKey registryClipKey = asset.Clips[0].CreateResourceKey(asset.PackageId);
                MxAnimationBakeArtifact bake = MxAnimationBakeEditorTool.BakeClip(clip, registryClipKey).Artifact;

                MxAnimationTimelineScrubberPreview preview =
                    MxAnimationTimelineScrubberPreviewBuilder.Build(result.Definition, "attack", 0, bake);

                Assert.AreEqual(registryClipKey, bake.Profile.SourceClipKey);
                Assert.IsFalse(ContainsScrubberDiagnostic(preview, "BakeSourceClipMismatch"));
            }
            finally
            {
                Object.DestroyImmediate(clip);
                Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void TimelineScrubberPreview_CarriesExporterMissingClipDiagnostics()
        {
            MxAnimationClipRegistryAsset asset = CreateScrubberRegistry("event:77", eventFrame: 3);
            try
            {
                MxAnimationClipRegistryExportResult result = MxAnimationClipRegistryExporter.ExportStructureOnly(asset);

                MxAnimationTimelineScrubberPreview preview =
                    MxAnimationTimelineScrubberPreviewBuilder.Build(
                        result.Definition,
                        "attack",
                        3,
                        exportValidation: result.ValidationReport,
                        selectedClipReferenceAvailable: false);

                Assert.IsTrue(preview.HasErrors);
                Assert.IsTrue(ContainsScrubberDiagnostic(preview, "MissingClip"));
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void TimelineScrubberPreview_UsesEditedRegistryBindingEvents()
        {
            MxAnimationClipRegistryAsset asset = CreateScrubberRegistry("event:77", eventFrame: 3);
            var clip = new AnimationClip { name = "Attack" };
            try
            {
                MxAnimationClipRegistryClipEntry[] clips = asset.Clips;
                clips[0].Clip = clip;
                asset.Clips = clips;

                MxAnimationClipRegistryBindingEntry[] bindings = asset.Bindings;
                bindings[0].Events = new[]
                {
                    new MxAnimationClipRegistryEventEntry
                    {
                        EventId = "event:42",
                        TimeDomain = MxAnimationEventTimeDomain.PresentationFrame,
                        Time = 4f,
                        EventKind = "SFX",
                        PayloadResourceId = "demo.sfx.impact",
                        PayloadTypeId = ResourceTypeIds.AudioClip,
                        Socket = "weapon"
                    }
                };
                asset.Bindings = bindings;

                MxAnimationClipRegistryExportResult edited = MxAnimationClipRegistryExporter.ExportStructureOnly(asset);
                MxAnimationTimelineScrubberPreview editedPreview =
                    MxAnimationTimelineScrubberPreviewBuilder.Build(edited.Definition, "attack", 4);

                Assert.IsTrue(edited.Success, MxAnimationClipRegistryExporter.CreateReportText(edited));
                Assert.AreEqual("event:42", edited.Definition.Actions[0].PresentationEvents[0].EventId);
                Assert.IsTrue(ContainsScrubberRow(editedPreview, MxAnimationTimelineScrubberRowKind.PresentationEvent, "event:42"));
                Assert.IsFalse(ContainsScrubberRow(editedPreview, MxAnimationTimelineScrubberRowKind.PresentationEvent, "event:77"));

                bindings = asset.Bindings;
                bindings[0].Events = System.Array.Empty<MxAnimationClipRegistryEventEntry>();
                asset.Bindings = bindings;

                MxAnimationClipRegistryExportResult removed = MxAnimationClipRegistryExporter.ExportStructureOnly(asset);
                MxAnimationTimelineScrubberPreview removedPreview =
                    MxAnimationTimelineScrubberPreviewBuilder.Build(removed.Definition, "attack", 4);

                Assert.IsTrue(removed.Success, MxAnimationClipRegistryExporter.CreateReportText(removed));
                Assert.AreEqual(0, removed.Definition.Actions[0].PresentationEvents.Count);
                Assert.IsFalse(ContainsScrubberRow(removedPreview, MxAnimationTimelineScrubberRowKind.PresentationEvent, "event:42"));
            }
            finally
            {
                Object.DestroyImmediate(clip);
                Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void TimelineScrubberPreview_ReadsCombatAuthoringAssetAsTimelineSource()
        {
            MxAnimationClipRegistryAsset asset = CreateScrubberRegistry("event:77", eventFrame: 3);
            CombatActionAuthoringAsset action = ScriptableObject.CreateInstance<CombatActionAuthoringAsset>();
            var clip = new AnimationClip { name = "Attack" };
            try
            {
                MxAnimationClipRegistryClipEntry[] clips = asset.Clips;
                clips[0].Clip = clip;
                asset.Clips = clips;
                action.ActionId = 1001;
                action.TotalFrames = 8;
                action.Startup = new CombatAuthoringFrameRange(0, 1);
                action.Active = new CombatAuthoringFrameRange(2, 4);
                action.Recovery = new CombatAuthoringFrameRange(5, 7);
                action.WeaponTraces = new[]
                {
                    new CombatWeaponTraceAuthoringData
                    {
                        TraceId = 7,
                        FrameRange = new CombatAuthoringFrameRange(3, 3),
                        SourceOrder = 1
                    }
                };
                MxAnimationClipRegistryExportResult result = MxAnimationClipRegistryExporter.ExportStructureOnly(asset);
                MxAnimationBakeArtifact bake = CreateBakeArtifact(asset.Clips[0].CreateResourceKey(asset.PackageId), frame: 3);

                MxAnimationTimelineScrubberPreview preview =
                    MxAnimationTimelineScrubberPreviewBuilder.Build(result.Definition, "attack", 3, bake, action);

                Assert.IsFalse(preview.HasErrors, MxAnimationTimelineScrubberPreviewBuilder.CreateSummary(preview));
                Assert.IsTrue(ContainsScrubberRow(preview, MxAnimationTimelineScrubberRowKind.CombatWindow, "Active"));
                Assert.IsTrue(ContainsScrubberRow(preview, MxAnimationTimelineScrubberRowKind.CombatWindow, "WeaponTrace:7"));
                Assert.IsTrue(ContainsScrubberDiagnostic(preview, "CombatFrameEventsUnavailable"));
            }
            finally
            {
                Object.DestroyImmediate(clip);
                Object.DestroyImmediate(action);
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

        private static MxAnimationClipRegistryAsset CreateScrubberRegistry(string eventId, int eventFrame)
        {
            MxAnimationClipRegistryAsset asset = ScriptableObject.CreateInstance<MxAnimationClipRegistryAsset>();
            asset.AnimationSetId = "combat.demo";
            asset.Version = 3;
            asset.PackageId = "demo.package";
            asset.Clips = new[]
            {
                new MxAnimationClipRegistryClipEntry
                {
                    ClipId = "attack",
                    ResourceId = "demo.animation.attack",
                    IsDefault = true,
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
                            EventId = eventId,
                            TimeDomain = MxAnimationEventTimeDomain.CombatFrame,
                            Time = eventFrame,
                            EventKind = "VFX",
                            PayloadResourceId = "demo.vfx.slash",
                            PayloadTypeId = ResourceTypeIds.GameObject,
                            Socket = "weapon"
                        }
                    }
                }
            };
            return asset;
        }

        private static MxAnimationBakeArtifact CreateBakeArtifact(ResourceKey clipKey, int frame)
        {
            var profile = new MxAnimationBakeProfile(
                "profile.scrubber",
                clipKey,
                "sha256:source",
                "skeleton.scrubber",
                "sha256:skeleton",
                sampleTickRate: 30,
                quantizationScale: 1000,
                MxAnimationBakeCoordinateSpace.Local,
                MxAnimationBakeRoundingPolicy.RoundNearest,
                "import:scrubber");

            return new MxAnimationBakeArtifact(
                profile,
                new[]
                {
                    new MxAnimationBakedWeaponTraceFrame(
                        frame,
                        7,
                        "weapon",
                        new MxAnimationBakedVector3(frame - 1, 0, 0),
                        new MxAnimationBakedVector3(frame - 1, 0, 1000),
                        new MxAnimationBakedVector3(frame, 0, 0),
                        new MxAnimationBakedVector3(frame, 0, 1000))
                },
                new[]
                {
                    new MxAnimationBakedRootMotionFrame(
                        frame,
                        new MxAnimationBakedVector3(frame, 0, 0),
                        new MxAnimationBakedVector3(1, 0, 0))
                },
                socketFrames: new[]
                {
                    new MxAnimationBakedSocketFrame(
                        frame,
                        "weapon",
                        "WeaponSocket",
                        new MxAnimationBakedVector3(frame, 0, 0),
                        new MxAnimationBakedVector3(1, 0, 0))
                });
        }

        private static bool ContainsScrubberRow(
            MxAnimationTimelineScrubberPreview preview,
            MxAnimationTimelineScrubberRowKind kind,
            string label)
        {
            for (int i = 0; i < preview.Rows.Count; i++)
            {
                if (preview.Rows[i].Kind == kind && preview.Rows[i].Label == label)
                    return true;
            }

            return false;
        }

        private static bool ContainsScrubberDiagnostic(
            MxAnimationTimelineScrubberPreview preview,
            string code)
        {
            for (int i = 0; i < preview.Diagnostics.Count; i++)
            {
                if (preview.Diagnostics[i].Code == code)
                    return true;
            }

            return false;
        }
    }
}
