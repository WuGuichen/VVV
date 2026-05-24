using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MxFramework.Diagnostics;
using MxFramework.Rendering;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace MxFramework.Tests.Rendering
{
    public class GlobalFrameContextTests
    {
        [Test]
        public void ShaderIds_RegisterExpectedMxGlobals()
        {
            Assert.AreEqual(Shader.PropertyToID("_MxTime"), MxRenderingShaderIds.MxTime);
            Assert.AreEqual(Shader.PropertyToID("_MxWindDirection"), MxRenderingShaderIds.MxWindDirection);
            Assert.AreEqual(Shader.PropertyToID("_MxWetness"), MxRenderingShaderIds.MxWetness);
            Assert.AreEqual(Shader.PropertyToID("_MxRain"), MxRenderingShaderIds.MxRain);
            Assert.AreEqual(Shader.PropertyToID("_MxSnowCoverage"), MxRenderingShaderIds.MxSnowCoverage);
            Assert.AreEqual(Shader.PropertyToID("_MxPrimarySubjectWorldPos"), MxRenderingShaderIds.MxPrimarySubjectWorldPos);
            Assert.AreEqual(Shader.PropertyToID("_MxPrimarySubjectVelocity"), MxRenderingShaderIds.MxPrimarySubjectVelocity);
            Assert.AreEqual(Shader.PropertyToID("_MxLocalSubjectWorldPos"), MxRenderingShaderIds.MxLocalSubjectWorldPos);
        }

        [Test]
        public void GlobalAndCameraShaderOwnership_DoNotOverlap()
        {
            var globalIds = new HashSet<int>(MxRenderingShaderIds.GlobalFramePropertyIds);

            foreach (int cameraId in MxRenderingShaderIds.CameraFramePropertyIds)
                Assert.IsFalse(globalIds.Contains(cameraId), "Camera property id must not be owned by GlobalFrameContext.");
        }

        [Test]
        public void SetTimeAndWind_UpdateSnapshotAndShaderGlobals()
        {
            var context = new GlobalFrameContext();

            context.SetTime(1.25f, 9.5f, 0.016f);
            context.SetWind(new Vector3(0.25f, 0f, 0.75f), 2f, 0.5f);

            GlobalFrameSnapshot snapshot = context.Snapshot();
            Assert.AreEqual(1.25f, snapshot.Time);
            Assert.AreEqual(9.5f, snapshot.GameTime);
            Assert.AreEqual(0.016f, snapshot.DeltaTime);
            Assert.AreEqual(new Vector3(0.25f, 0f, 0.75f), snapshot.WindDirection);
            Assert.AreEqual(2f, snapshot.WindStrength);
            Assert.AreEqual(0.5f, snapshot.WindTurbulence);
            Assert.AreEqual(new Vector4(1.25f, 9.5f, 0.016f, 0f), Shader.GetGlobalVector(MxRenderingShaderIds.MxTime));
            Assert.AreEqual(new Vector4(0.25f, 0f, 0.75f, 0f), Shader.GetGlobalVector(MxRenderingShaderIds.MxWindDirection));
        }

        [Test]
        public void SetWeather_UpdatesSnapshotAndShaderGlobals()
        {
            var context = new GlobalFrameContext();

            context.SetWeather(0.2f, 0.4f, 0.6f);

            GlobalFrameSnapshot snapshot = context.Snapshot();
            Assert.AreEqual(0.2f, snapshot.Wetness);
            Assert.AreEqual(0.4f, snapshot.Rain);
            Assert.AreEqual(0.6f, snapshot.SnowCoverage);
            Assert.AreEqual(0.2f, Shader.GetGlobalFloat(MxRenderingShaderIds.MxWetness));
            Assert.AreEqual(0.4f, Shader.GetGlobalFloat(MxRenderingShaderIds.MxRain));
            Assert.AreEqual(0.6f, Shader.GetGlobalFloat(MxRenderingShaderIds.MxSnowCoverage));
        }

        [Test]
        public void SetPrimarySubjectPose_UpdatesSnapshotAndShaderGlobals()
        {
            var context = new GlobalFrameContext();
            var worldPosition = new Vector3(1f, 2f, 3f);
            var velocity = new Vector3(0.5f, -0.25f, 4f);

            context.SetPrimarySubjectPose(worldPosition, velocity);

            GlobalFrameSnapshot snapshot = context.Snapshot();
            Assert.AreEqual(worldPosition, snapshot.PrimarySubjectWorldPos);
            Assert.AreEqual(velocity, snapshot.PrimarySubjectVelocity);
            Assert.AreEqual(new Vector4(1f, 2f, 3f, 0f), Shader.GetGlobalVector(MxRenderingShaderIds.MxPrimarySubjectWorldPos));
            Assert.AreEqual(new Vector4(0.5f, -0.25f, 4f, 0f), Shader.GetGlobalVector(MxRenderingShaderIds.MxPrimarySubjectVelocity));
        }

        [Test]
        public void SetLocalSubjectPose_UpdatesSnapshotAndWorldPositionShaderGlobal()
        {
            var context = new GlobalFrameContext();
            var worldPosition = new Vector3(-2f, 5f, 8f);
            var velocity = new Vector3(3f, 0f, -1f);

            context.SetLocalSubjectPose(worldPosition, velocity);

            GlobalFrameSnapshot snapshot = context.Snapshot();
            Assert.AreEqual(worldPosition, snapshot.LocalSubjectWorldPos);
            Assert.AreEqual(velocity, snapshot.LocalSubjectVelocity);
            Assert.AreEqual(new Vector4(-2f, 5f, 8f, 0f), Shader.GetGlobalVector(MxRenderingShaderIds.MxLocalSubjectWorldPos));
        }

        [Test]
        public void MaterialCanConsumeGlobalWindDirection()
        {
            Shader shader = Shader.Find("Hidden/MxFramework/Tests/Rendering/WindDirectionGlobalConsumer");
            Assert.IsNotNull(shader, "Test shader must be imported before running Rendering material validation.");

            var context = new GlobalFrameContext();
            context.SetWind(new Vector3(1f, 0f, 0f), 1f, 0f);

            var previousActive = RenderTexture.active;
            var material = new Material(shader);
            var target = new RenderTexture(4, 4, 0, RenderTextureFormat.ARGB32);
            var readback = new Texture2D(4, 4, TextureFormat.RGBA32, false);

            try
            {
                Graphics.Blit(Texture2D.whiteTexture, target, material);
                RenderTexture.active = target;
                readback.ReadPixels(new Rect(0, 0, 4, 4), 0, 0);
                readback.Apply();

                Color pixel = readback.GetPixel(2, 2);
                Assert.Greater(pixel.r, 0.8f);
                Assert.Less(pixel.g, 0.1f);
                Assert.Less(pixel.b, 0.1f);
            }
            finally
            {
                RenderTexture.active = previousActive;
                UnityEngine.Object.DestroyImmediate(readback);
                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void GlobalFrameDebugSource_ExposesGlobalsSection()
        {
            var context = new GlobalFrameContext();
            context.SetTime(3f, 4f, 0.02f);
            context.SetWind(Vector3.right, 1.5f, 0.25f);
            var source = new GlobalFrameContextDebugSource(context);

            FrameworkDebugSnapshot snapshot = source.CreateSnapshot();

            Assert.AreEqual("Rendering", snapshot.SourceName);
            Assert.AreEqual(FrameworkDebugMode.Runtime, snapshot.Mode);
            Assert.AreEqual(1, snapshot.Sections.Count);
            Assert.AreEqual(RenderingDebugSectionNames.Globals, snapshot.Sections[0].Title);
            StringAssert.Contains("time: 3", snapshot.Sections[0].Body);
            StringAssert.Contains("windStrength: 1.5", snapshot.Sections[0].Body);
        }

        [Test]
        public void RenderingAsmdef_HasOnlyAllowedRuntimeReferences()
        {
            string asmdef = File.ReadAllText("Assets/Scripts/MxFramework/Rendering/MxFramework.Rendering.asmdef");

            StringAssert.Contains("\"MxFramework.Core\"", asmdef);
            StringAssert.Contains("\"MxFramework.Diagnostics\"", asmdef);
            StringAssert.Contains("\"Unity.RenderPipelines.Universal.Runtime\"", asmdef);
            Assert.IsFalse(asmdef.Contains("MxFramework.DebugUI"));
            Assert.IsFalse(asmdef.Contains("MxFramework.Gameplay"));
            Assert.IsFalse(asmdef.Contains("MxFramework.Combat"));
            Assert.IsFalse(asmdef.Contains("MxFramework.Buffs"));
            Assert.IsFalse(asmdef.Contains("MxFramework.Character"));
            Assert.IsFalse(asmdef.Contains("MxFramework.Animation"));
            Assert.IsFalse(asmdef.Contains("MxFramework.Camera"));
        }

        [Test]
        public void NoEngineAsmdefs_DoNotReferenceRendering()
        {
            string[] noEngineAsmdefs =
            {
                "Assets/Scripts/MxFramework/Core/MxFramework.Core.asmdef",
                "Assets/Scripts/MxFramework/Config/MxFramework.Config.asmdef",
                "Assets/Scripts/MxFramework/Events/MxFramework.Events.asmdef",
                "Assets/Scripts/MxFramework/Attributes/MxFramework.Attributes.asmdef",
                "Assets/Scripts/MxFramework/Modifiers/MxFramework.Modifiers.asmdef",
                "Assets/Scripts/MxFramework/Buffs/MxFramework.Buffs.asmdef",
                "Assets/Scripts/MxFramework/Gameplay/MxFramework.Gameplay.asmdef",
                "Assets/Scripts/MxFramework/Combat/MxFramework.Combat.asmdef",
                "Assets/Scripts/MxFramework/Runtime/MxFramework.Runtime.asmdef",
                "Assets/Scripts/MxFramework/Resources/MxFramework.Resources.asmdef",
                "Assets/Scripts/MxFramework/AI/MxFramework.AI.asmdef"
            };

            foreach (string asmdefPath in noEngineAsmdefs)
            {
                string asmdef = File.ReadAllText(asmdefPath);
                Assert.IsFalse(asmdef.Contains("MxFramework.Rendering"), asmdefPath);
            }
        }
    }

    public class CameraRenderContextAndFeaturePipelineTests
    {
        private static readonly SharedRTOwnerId Owner = new SharedRTOwnerId("pipeline-owner");
        private static readonly SharedRTWriterSetId WriterSet = new SharedRTWriterSetId("pipeline-writers");

        [Test]
        public void CameraRenderContext_SnapshotsCameraKindFocusAndOverrides()
        {
            var context = new CameraRenderContext();
            var descriptor = new MxCameraRenderContextDescriptor(MxCameraRenderKind.SceneView, null, new Vector3(1f, 2f, 3f));
            int overrideId = Shader.PropertyToID("_MxTestCameraOverride");

            context.SetDescriptor(descriptor);
            context.SetViewFocus(new Vector3(4f, 5f, 6f));
            context.SetCameraOverride(overrideId, new Vector4(7f, 8f, 9f, 10f));

            CameraRenderSnapshot snapshot = context.Snapshot();

            Assert.AreEqual(MxCameraRenderKind.SceneView, context.CurrentCameraKind);
            Assert.AreEqual(MxCameraRenderKind.SceneView, snapshot.CameraKind);
            Assert.AreEqual(new Vector3(4f, 5f, 6f), snapshot.ViewFocusWorldPosition);
            Assert.AreEqual(1, snapshot.Overrides.Count);
            Assert.AreEqual(overrideId, snapshot.Overrides[0].PropertyId);
            Assert.AreEqual(new Vector4(7f, 8f, 9f, 10f), snapshot.Overrides[0].Value);

            var source = new CameraRenderContextDebugSource(context);
            FrameworkDebugSnapshot debugSnapshot = source.CreateSnapshot();
            Assert.AreEqual(RenderingDebugSectionNames.CameraGlobals, debugSnapshot.Sections[0].Title);
            StringAssert.Contains("cameraKind: SceneView", debugSnapshot.Sections[0].Body);
        }

        [Test]
        public void CameraRenderContext_RejectsGlobalOwnedShaderOverride()
        {
            var context = new CameraRenderContext();

            Assert.Throws<ArgumentException>(() => context.SetCameraOverride(MxRenderingShaderIds.MxTime, Vector4.one));
        }

        [Test]
        public void FeaturePipeline_SortsEnabledPassesByPhaseOrderAndDebugNameOrdinal()
        {
            var pipeline = new MxRenderPipeline();
            pipeline.RegisterPass(new DummyPass("zeta", MxRenderPhase.AfterRendering, 0));
            pipeline.RegisterPass(new DummyPass("bravo", MxRenderPhase.BeforeRenderingOpaques, 0));
            pipeline.RegisterPass(new DummyPass("alpha", MxRenderPhase.BeforeRenderingOpaques, 0));
            pipeline.RegisterPass(new DummyPass("disabled", MxRenderPhase.BeforeRendering, -100, isEnabled: false));
            pipeline.RegisterPass(new DummyPass("late", MxRenderPhase.BeforeRenderingOpaques, 10));

            IReadOnlyList<IMxRenderPass> passes = pipeline.CollectPasses(CreateDescriptor(MxCameraRenderKind.Game));

            CollectionAssert.AreEqual(new[] { "alpha", "bravo", "late", "zeta" }, passes.Select(pass => pass.DebugName).ToArray());
            CollectionAssert.AreEqual(new[] { "alpha", "bravo", "late", "zeta" }, pipeline.CaptureTopology().Passes.Select(pass => pass.DebugName).ToArray());
        }

        [Test]
        public void FeaturePipeline_ProviderFiltersByCameraKind()
        {
            var pipeline = new MxRenderPipeline();
            pipeline.RegisterProvider(new CameraKindProvider("game-provider", MxCameraRenderKind.Game, "game-pass"));
            pipeline.RegisterProvider(new CameraKindProvider("scene-provider", MxCameraRenderKind.SceneView, "scene-pass"));
            pipeline.RegisterProvider(new CameraKindProvider("preview-provider", MxCameraRenderKind.Preview, "preview-pass"));
            pipeline.RegisterProvider(new CameraKindProvider("reflection-provider", MxCameraRenderKind.Reflection, "reflection-pass"));

            AssertSinglePassForKind(pipeline, MxCameraRenderKind.Game, "game-pass");
            AssertSinglePassForKind(pipeline, MxCameraRenderKind.SceneView, "scene-pass");
            AssertSinglePassForKind(pipeline, MxCameraRenderKind.Preview, "preview-pass");
            AssertSinglePassForKind(pipeline, MxCameraRenderKind.Reflection, "reflection-pass");
        }

        [Test]
        public void FeaturePipeline_TopologyReportsDuplicateNamesSharedRTCollisionAndInvalidMetadata()
        {
            var pipeline = new MxRenderPipeline();
            SharedRenderTextureKey shared = CreateSharedRT("mx.pipeline.shared");
            pipeline.RegisterPass(new DummyPass("duplicate", MxRenderPhase.BeforeRenderingOpaques, 0));
            pipeline.RegisterPass(new DummyPass("duplicate", MxRenderPhase.BeforeRenderingOpaques, 1));
            pipeline.RegisterPass(new DummyPass("writer", MxRenderPhase.AfterRenderingOpaques, 0, writes: new[] { shared }));
            pipeline.RegisterPass(new DummyPass("reader", MxRenderPhase.AfterRenderingOpaques, 0, reads: new[] { shared }));
            pipeline.RegisterPass(new DummyPass(string.Empty, MxRenderPhase.AfterRendering, 0));

            pipeline.CollectPasses(CreateDescriptor(MxCameraRenderKind.Preview));
            MxRenderPipelineTopologySnapshot snapshot = pipeline.CaptureTopology();

            Assert.AreEqual(MxCameraRenderKind.Preview, snapshot.CameraKind);
            Assert.IsTrue(snapshot.Diagnostics.Any(diagnostic => diagnostic.Code == MxRenderPipelineDiagnosticCode.DuplicateDebugName));
            Assert.IsTrue(snapshot.Diagnostics.Any(diagnostic => diagnostic.Code == MxRenderPipelineDiagnosticCode.SharedRTPhaseOrderCollision));
            Assert.IsTrue(snapshot.Diagnostics.Any(diagnostic => diagnostic.Code == MxRenderPipelineDiagnosticCode.InvalidMetadata));
            Assert.IsFalse(snapshot.Passes.Any(pass => string.IsNullOrEmpty(pass.DebugName)));

            var source = new RenderPipelineTopologyDebugSource(pipeline);
            FrameworkDebugSnapshot debugSnapshot = source.CreateSnapshot();
            Assert.AreEqual(RenderingDebugSectionNames.PipelineTopology, debugSnapshot.Sections[0].Title);
            StringAssert.Contains("cameraKind: Preview", debugSnapshot.Sections[0].Body);
            StringAssert.Contains("SharedRTPhaseOrderCollision", debugSnapshot.Sections[0].Body);
        }

        [Test]
        public void FeaturePipeline_ProviderMetadataReportsEmptyAndDuplicateDebugNames()
        {
            var pipeline = new MxRenderPipeline();
            pipeline.RegisterProvider(new CameraKindProvider(string.Empty, MxCameraRenderKind.Game, "empty-provider-pass"));
            pipeline.RegisterProvider(new CameraKindProvider("duplicate-provider", MxCameraRenderKind.Game, "first-provider-pass"));
            pipeline.RegisterProvider(new CameraKindProvider("duplicate-provider", MxCameraRenderKind.SceneView, "second-provider-pass"));

            pipeline.CollectPasses(CreateDescriptor(MxCameraRenderKind.Game));
            MxRenderPipelineTopologySnapshot snapshot = pipeline.CaptureTopology();

            Assert.IsTrue(snapshot.Diagnostics.Any(diagnostic => diagnostic.Code == MxRenderPipelineDiagnosticCode.InvalidProviderMetadata));
            Assert.IsTrue(snapshot.Diagnostics.Any(diagnostic => diagnostic.Code == MxRenderPipelineDiagnosticCode.DuplicateProviderDebugName));
        }

        [Test]
        public void FeaturePipeline_ReportsSharedRTOrderWriterPolicyAndInvalidKeyMetadata()
        {
            var pipeline = new MxRenderPipeline();
            SharedRenderTextureKey shared = CreateSharedRT("mx.pipeline.order");
            SharedRenderTextureKey missing = CreateSharedRT("mx.pipeline.missing");
            SharedRenderTextureKey invalid = CreateInvalidSharedRT();

            pipeline.RegisterPass(new DummyPass("early-reader", MxRenderPhase.BeforeRenderingOpaques, 0, reads: new[] { shared }));
            pipeline.RegisterPass(new DummyPass("writer-a", MxRenderPhase.BeforeRenderingOpaques, 10, writes: new[] { shared }));
            pipeline.RegisterPass(new DummyPass("writer-b", MxRenderPhase.BeforeRenderingOpaques, 20, writes: new[] { shared }));
            pipeline.RegisterPass(new DummyPass("missing-reader", MxRenderPhase.BeforeRenderingTransparents, 0, reads: new[] { missing }));
            pipeline.RegisterPass(new DummyPass("invalid-key-writer", MxRenderPhase.AfterRendering, 0, writes: new[] { invalid }));

            pipeline.CollectPasses(CreateDescriptor(MxCameraRenderKind.Game));
            MxRenderPipelineTopologySnapshot snapshot = pipeline.CaptureTopology();

            Assert.IsTrue(snapshot.Diagnostics.Any(diagnostic => diagnostic.Code == MxRenderPipelineDiagnosticCode.SharedRTReadBeforeWrite));
            Assert.IsTrue(snapshot.Diagnostics.Any(diagnostic => diagnostic.Code == MxRenderPipelineDiagnosticCode.SharedRTMissingWriter));
            Assert.IsTrue(snapshot.Diagnostics.Any(diagnostic => diagnostic.Code == MxRenderPipelineDiagnosticCode.SharedRTMultipleWriters));
            Assert.IsTrue(snapshot.Diagnostics.Any(diagnostic => diagnostic.Code == MxRenderPipelineDiagnosticCode.InvalidSharedRTMetadata));
        }

        [Test]
        public void FeatureSharedRTLifecycle_IsPerCameraInvocationAndWrapsFeaturePasses()
        {
            IReadOnlyList<string> topology = MxRenderingPipelineFeature.CaptureLifecycleTopologyForTests(new[]
            {
                new DummyPass("feature-pass", MxRenderPhase.BeforeRenderingOpaques, 0)
            });

            Assert.AreEqual("PerCameraRenderInvocation", MxRenderingPipelineFeature.SharedRTLifecycleScope);
            CollectionAssert.AreEqual(
                new[] { "SharedRT.BeginFrame(sync)", "CameraGlobals", "feature-pass", "SharedRT.EndFrame" },
                topology.ToArray());
        }

        [Test]
        public void FeatureSharedRTLifecycle_BeginsFrameBeforePassConfigure()
        {
            var feature = ScriptableObject.CreateInstance<MxRenderingPipelineFeature>();

            try
            {
                SharedRenderTextureRegistry registry = feature.EnsureSharedRenderTexturesForTests();
                registry.RegisterWriterSet(WriterSet, new[] { Owner });
                SharedRTHandle handle = registry.Register(CreateSharedRT("mx.pipeline.configure-lifecycle"));
                Assert.IsTrue(handle.IsValid);

                registry.RecordWriter(handle, Owner, MxRenderPhase.BeforeRenderingOpaques, 0);
                Assert.AreEqual(1, registry.CaptureDiagnostics().Entries[0].CurrentFrameWriters.Count);

                var probe = new ConfigureProbePass("configure-probe", MxRenderPhase.BeforeRenderingOpaques, 0);
                feature.Pipeline.RegisterPass(probe);

                feature.ConfigureRegisteredPassesForTests(CreateDescriptor(MxCameraRenderKind.Game));

                Assert.AreEqual(0, probe.WritersSeenDuringConfigure);
            }
            finally
            {
                if (feature != null)
                    UnityEngine.Object.DestroyImmediate(feature);
            }
        }

        [Test]
        public void FeatureSharedRTLifecycle_ClearsFrameDiagnosticsAndDisposesRegistry()
        {
            var feature = ScriptableObject.CreateInstance<MxRenderingPipelineFeature>();
            SharedRenderTextureRegistry registry = null;

            try
            {
                registry = feature.EnsureSharedRenderTexturesForTests();
                registry.RegisterWriterSet(WriterSet, new[] { Owner });
                SharedRTHandle handle = registry.Register(CreateSharedRT("mx.pipeline.lifecycle"));
                Assert.IsTrue(handle.IsValid);

                registry.RecordWriter(handle, Owner, MxRenderPhase.BeforeRenderingOpaques, 0);
                Assert.AreEqual(1, registry.CaptureDiagnostics().Entries[0].CurrentFrameWriters.Count);

                feature.BeginSharedRTFrameForTests();
                Assert.AreEqual(0, registry.CaptureDiagnostics().Entries[0].CurrentFrameWriters.Count);

                feature.EndSharedRTFrameForTests();
                feature.DisposeFeatureResourcesForTests();

                Assert.IsNull(feature.CurrentSharedRenderTexturesForTests);
                Assert.AreEqual(0, registry.CaptureDiagnostics().Entries.Count);
                Assert.IsFalse(registry.Register(CreateSharedRT("mx.pipeline.after-dispose")).IsValid);
            }
            finally
            {
                if (feature != null)
                    UnityEngine.Object.DestroyImmediate(feature);
            }
        }

        [Test]
        public void MxRenderPhase_MapsToExpectedUrpRenderPassEvents()
        {
            Assert.AreEqual(RenderPassEvent.BeforeRendering, MxRenderPhase.BeforeRendering.ToRenderPassEvent());
            Assert.AreEqual(RenderPassEvent.BeforeRenderingOpaques, MxRenderPhase.BeforeRenderingOpaques.ToRenderPassEvent());
            Assert.AreEqual(RenderPassEvent.AfterRenderingOpaques, MxRenderPhase.AfterRenderingOpaques.ToRenderPassEvent());
            Assert.AreEqual(RenderPassEvent.AfterRendering, MxRenderPhase.AfterRendering.ToRenderPassEvent());
        }

        [Test]
        public void RenderingAsmdef_DoesNotGainForbiddenFeaturePipelineDependencies()
        {
            string asmdef = File.ReadAllText("Assets/Scripts/MxFramework/Rendering/MxFramework.Rendering.asmdef");

            Assert.IsFalse(asmdef.Contains("MxFramework.DebugUI"));
            Assert.IsFalse(asmdef.Contains("MxFramework.Gameplay"));
            Assert.IsFalse(asmdef.Contains("MxFramework.Combat"));
            Assert.IsFalse(asmdef.Contains("MxFramework.Character"));
            Assert.IsFalse(asmdef.Contains("MxFramework.Buffs"));
            Assert.IsFalse(asmdef.Contains("MxFramework.Animation"));
            Assert.IsFalse(asmdef.Contains("MxFramework.Camera"));
        }

        private static void AssertSinglePassForKind(MxRenderPipeline pipeline, MxCameraRenderKind kind, string expectedDebugName)
        {
            IReadOnlyList<IMxRenderPass> passes = pipeline.CollectPasses(CreateDescriptor(kind));

            Assert.AreEqual(1, passes.Count);
            Assert.AreEqual(expectedDebugName, passes[0].DebugName);
            Assert.AreEqual(kind, pipeline.CaptureTopology().CameraKind);
        }

        private static MxCameraRenderContextDescriptor CreateDescriptor(MxCameraRenderKind kind)
        {
            return new MxCameraRenderContextDescriptor(kind, null, Vector3.zero);
        }

        private static SharedRenderTextureKey CreateSharedRT(string id)
        {
            return new SharedRenderTextureKey(
                new SharedRTId(id),
                id,
                Owner,
                new SharedRTAccessPolicy(false, SharedRTOrderRule.ReadAfterWriteSameFrame, WriterSet),
                SharedRTAnchor.MainCamera,
                SharedRTFormat.ARGB32,
                new SharedRTSize(16, 16),
                new SharedRTClearSpec(SharedRTClearKind.ClearEveryFrame, Color.clear),
                SharedRTResizePolicy.Reallocate);
        }

        private static SharedRenderTextureKey CreateInvalidSharedRT()
        {
            return new SharedRenderTextureKey(
                default,
                string.Empty,
                default,
                new SharedRTAccessPolicy(false, (SharedRTOrderRule)999, default),
                (SharedRTAnchor)999,
                SharedRTFormat.ARGB32,
                default,
                new SharedRTClearSpec((SharedRTClearKind)999, Color.clear),
                SharedRTResizePolicy.Reallocate,
                -1);
        }

        private sealed class CameraKindProvider : IMxRenderPassProvider
        {
            private readonly MxCameraRenderKind _cameraKind;
            private readonly string _passName;

            public CameraKindProvider(string debugName, MxCameraRenderKind cameraKind, string passName)
            {
                DebugName = debugName;
                _cameraKind = cameraKind;
                _passName = passName;
            }

            public string DebugName { get; }

            public void CollectPasses(IMxRenderPassRegistry registry, in MxCameraRenderContextDescriptor cameraContext)
            {
                if (cameraContext.CameraKind == _cameraKind)
                    registry.RegisterPass(new DummyPass(_passName, MxRenderPhase.BeforeRenderingOpaques, 0));
            }
        }

        private sealed class DummyPass : IMxRenderPass
        {
            public DummyPass(
                string debugName,
                MxRenderPhase phase,
                int order,
                bool isEnabled = true,
                IReadOnlyList<SharedRenderTextureKey> reads = null,
                IReadOnlyList<SharedRenderTextureKey> writes = null)
            {
                DebugName = debugName;
                Phase = phase;
                Order = order;
                IsEnabled = isEnabled;
                Reads = reads ?? Array.Empty<SharedRenderTextureKey>();
                Writes = writes ?? Array.Empty<SharedRenderTextureKey>();
            }

            public string DebugName { get; }
            public MxRenderPhase Phase { get; }
            public int Order { get; }
            public bool IsEnabled { get; }
            public IReadOnlyList<SharedRenderTextureKey> Reads { get; }
            public IReadOnlyList<SharedRenderTextureKey> Writes { get; }

            public void Configure(in MxRenderPassConfigureContext context)
            {
            }

            public void Execute(in MxRenderPassExecuteContext context)
            {
            }
        }

        private sealed class ConfigureProbePass : IMxRenderPass
        {
            public ConfigureProbePass(string debugName, MxRenderPhase phase, int order)
            {
                DebugName = debugName;
                Phase = phase;
                Order = order;
                Reads = Array.Empty<SharedRenderTextureKey>();
                Writes = Array.Empty<SharedRenderTextureKey>();
                WritersSeenDuringConfigure = -1;
            }

            public string DebugName { get; }
            public MxRenderPhase Phase { get; }
            public int Order { get; }
            public bool IsEnabled => true;
            public IReadOnlyList<SharedRenderTextureKey> Reads { get; }
            public IReadOnlyList<SharedRenderTextureKey> Writes { get; }
            public int WritersSeenDuringConfigure { get; private set; }

            public void Configure(in MxRenderPassConfigureContext context)
            {
                WritersSeenDuringConfigure = context.SharedRenderTextures.CaptureDiagnostics().Entries[0].CurrentFrameWriters.Count;
            }

            public void Execute(in MxRenderPassExecuteContext context)
            {
            }
        }
    }
}
