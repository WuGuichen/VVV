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
                    Assert.AreEqual(2, snapshot.Topology.Passes.Count);
                    Assert.AreEqual(1, snapshot.SharedRT.Entries.Count);
                    Assert.AreEqual(1, snapshot.SharedRT.Entries[0].CurrentFrameWriters.Count);
                    Assert.AreEqual(1, snapshot.SharedRT.Entries[0].CurrentFrameReaders.Count);
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

        private static RenderingDemoSlicesShowcaseRuntime CreateRuntime(out RendererFixture fixture)
        {
            fixture = RendererFixture.Create();
            var runtime = new RenderingDemoSlicesShowcaseRuntime();
            runtime.Initialize(fixture.Renderer);
            return runtime;
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
                var material = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
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
