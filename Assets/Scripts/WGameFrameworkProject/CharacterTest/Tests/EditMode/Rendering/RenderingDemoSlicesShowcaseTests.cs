using MxFramework.Demo.Rendering;
using MxFramework.Rendering;
using NUnit.Framework;
using UnityEngine;

namespace MxFramework.Tests.Rendering
{
    public sealed class RenderingDemoSlicesShowcaseTests
    {
        [Test]
        public void ContextSlice_UpdatesGlobalFrameContextSnapshot()
        {
            RendererFixture fixture;
            using (RenderingDemoSlicesShowcaseRuntime runtime = CreateRuntime(out fixture))
            {
                try
                {
                    runtime.Enqueue(RenderingDemoCommand.ToggleWind);
                    runtime.Step(1f / 30f);

                    GlobalFrameSnapshot globals = runtime.Snapshot.Globals;
                    Assert.Greater(globals.WindDirection.sqrMagnitude, 0.9f);
                    Assert.Greater(globals.WindStrength, 0.9f);
                    Assert.AreNotEqual(Vector3.zero, globals.PrimarySubjectVelocity);
                }
                finally
                {
                    fixture.Dispose();
                }
            }
        }

        [Test]
        public void SharedRtSlice_UsesProviderPassesAndRegistryDiagnostics()
        {
            RendererFixture fixture;
            using (RenderingDemoSlicesShowcaseRuntime runtime = CreateRuntime(out fixture))
            {
                try
                {
                    runtime.Step(1f / 30f);

                    RenderingDemoSlicesSnapshot snapshot = runtime.Snapshot;
                    Assert.GreaterOrEqual(snapshot.Topology.Passes.Count, 3);
                    Assert.Greater(snapshot.SharedRT.Entries.Count, 0);
                    AssertSharedRtHasReadersAndWriters(snapshot.SharedRT, new SharedRTId("mxframework.demo.rendering.synthetic-rt"));
                    AssertFogBudgetDoesNotAllocate(snapshot.SharedRT);
                }
                finally
                {
                    fixture.Dispose();
                }
            }
        }

        [Test]
        public void MaterialBindingSlice_WritesThroughHubDiagnostics()
        {
            RendererFixture fixture;
            using (RenderingDemoSlicesShowcaseRuntime runtime = CreateRuntime(out fixture))
            {
                try
                {
                    runtime.Enqueue(RenderingDemoCommand.PulseMaterial);
                    runtime.Step(1f / 30f);

                    MaterialBindingDiagnosticsSnapshot material = runtime.Snapshot.MaterialBindings;
                    Assert.AreEqual(2, material.BindingCount);
                    Assert.GreaterOrEqual(material.LastAppliedTargetCount, 1);
                    Assert.AreEqual(1, material.CountFor(MxMaterialChannel.StatusTint));
                    Assert.AreEqual(1, material.CountFor(MxMaterialChannel.DebugOverlay));
                }
                finally
                {
                    fixture.Dispose();
                }
            }
        }

        [Test]
        public void PublisherSlice_ReportsGenericLifecycleAndEventCounters()
        {
            RendererFixture fixture;
            using (RenderingDemoSlicesShowcaseRuntime runtime = CreateRuntime(out fixture))
            {
                try
                {
                    runtime.Enqueue(RenderingDemoCommand.PublishEventBurst);
                    runtime.Step(1f / 30f);

                    RenderDataPublisherSnapshot publisher = runtime.Snapshot.Publisher;
                    Assert.GreaterOrEqual(publisher.CurrentFrameEventCount, 5);
                    Assert.AreEqual(1, publisher.CurrentFrameCount(RenderDataEventKind.Impact));
                    Assert.AreEqual(1, publisher.CurrentFrameCount(RenderDataEventKind.SurfaceContact));
                    Assert.AreEqual(1, publisher.CurrentFrameCount(RenderDataEventKind.FieldImpulse));
                    Assert.GreaterOrEqual(publisher.CurrentFrameCount(RenderDataEventKind.Movement), 1);
                    Assert.AreEqual(1, publisher.CurrentFrameCount(RenderDataEventKind.Lifecycle));
                }
                finally
                {
                    fixture.Dispose();
                }
            }
        }

        [Test]
        public void VolumeBlenderSlice_ReportsDiagnosticsOnlyAppliedAndSuppressedRequests()
        {
            RendererFixture fixture;
            using (RenderingDemoSlicesShowcaseRuntime runtime = CreateRuntime(out fixture))
            {
                try
                {
                    runtime.Enqueue(RenderingDemoCommand.CycleVolumePriority);
                    runtime.Step(0.2f);

                    RenderingDemoSlicesSnapshot snapshot = runtime.Snapshot;
                    Assert.GreaterOrEqual(snapshot.VolumeDiagnostics.ActiveRequests.Count, 2);
                    Assert.AreEqual(1, snapshot.VolumeBlendState.AppliedProfiles.Count);
                    Assert.GreaterOrEqual(snapshot.VolumeBlendState.SuppressedRequests.Count, 1);
                }
                finally
                {
                    fixture.Dispose();
                }
            }
        }

        [Test]
        public void VolumetricFogSlice_ReportsQualityAndDiagnosticBudget()
        {
            RendererFixture fixture;
            using (RenderingDemoSlicesShowcaseRuntime runtime = CreateRuntime(out fixture))
            {
                try
                {
                    runtime.Step(1f / 30f);

                    RenderingDemoSlicesSnapshot snapshot = runtime.Snapshot;
                    Assert.IsTrue(snapshot.Fog.Enabled);
                    Assert.AreEqual(MxVolumetricFogQuality.Medium, snapshot.Fog.Quality);
                    Assert.AreEqual(12, snapshot.Fog.QualitySampleBudget);
                    Assert.AreEqual(new SharedRTSize(640, 360), snapshot.Fog.AccumulationSize);
                    Assert.Greater(snapshot.Fog.FocusClearRadius, 0f);
                    Assert.Greater(snapshot.Fog.EstimatedMemoryBytes, 0);
                    AssertFogBudgetDoesNotAllocate(snapshot.SharedRT);

                    runtime.Enqueue(RenderingDemoCommand.CycleFogQuality);
                    runtime.Step(1f / 30f);

                    Assert.AreEqual(MxVolumetricFogQuality.High, runtime.Snapshot.Fog.Quality);
                    Assert.AreEqual(20, runtime.Snapshot.Fog.QualitySampleBudget);
                    AssertFogBudgetDoesNotAllocate(runtime.Snapshot.SharedRT);
                }
                finally
                {
                    fixture.Dispose();
                }
            }
        }

        private static void AssertFogBudgetDoesNotAllocate(SharedRTDiagnosticsSnapshot snapshot)
        {
            foreach (var entry in snapshot.Entries)
            {
                Assert.AreNotEqual(MxVolumetricFogPass.AccumulationId, entry.Id);
                Assert.AreNotEqual(MxVolumetricFogPass.ReservedTemporalHistoryId, entry.Id);
            }
        }

        private static RenderingDemoSlicesShowcaseRuntime CreateRuntime(out RendererFixture fixture)
        {
            fixture = RendererFixture.Create();
            var runtime = new RenderingDemoSlicesShowcaseRuntime();
            runtime.Initialize(fixture.Renderer);
            return runtime;
        }

        private static void AssertSharedRtHasReadersAndWriters(SharedRTDiagnosticsSnapshot snapshot, SharedRTId id)
        {
            for (int i = 0; i < snapshot.Entries.Count; i++)
            {
                SharedRTDiagnosticsEntry entry = snapshot.Entries[i];
                if (entry.Id == id)
                {
                    Assert.GreaterOrEqual(entry.CurrentFrameReaders.Count, 1);
                    Assert.GreaterOrEqual(entry.CurrentFrameWriters.Count, 1);
                    return;
                }
            }

            Assert.Fail("SharedRT entry not found: " + id);
        }

        private sealed class RendererFixture : System.IDisposable
        {
            private RendererFixture(GameObject gameObject, Material material)
            {
                GameObject = gameObject;
                Material = material;
                Renderer = gameObject.GetComponent<Renderer>();
            }

            public GameObject GameObject { get; }
            public Material Material { get; }
            public Renderer Renderer { get; }

            public static RendererFixture Create()
            {
                GameObject gameObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
                var material = new Material(Shader.Find("WGame/PropStylizedLit"));
                material.SetFloat("_WgAlbedoStrength", 1f);
                material.SetFloat("_WgBronzeTrimAmount", 0f);
                material.SetFloat("_WgInteractableGlowAmount", 0f);
                gameObject.GetComponent<Renderer>().sharedMaterial = material;
                return new RendererFixture(gameObject, material);
            }

            public void Dispose()
            {
                Object.DestroyImmediate(GameObject);
                Object.DestroyImmediate(Material);
            }
        }
    }
}
