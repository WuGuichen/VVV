using System.IO;
using MxFramework.Rendering;
using NUnit.Framework;

namespace MxFramework.Tests.Rendering
{
    public class RenderSubjectIdentityTests
    {
        [Test]
        public void SubjectMap_GetOrCreateAndResolve_ReturnsStableSubject()
        {
            var registry = new MxRenderSubjectRegistry();
            IRenderSubjectMap<int> map = registry.CreateMap<int>();

            MxRenderSubjectId first = map.GetOrCreate(10, MxRenderSubjectRole.Primary);
            MxRenderSubjectId second = map.GetOrCreate(10, MxRenderSubjectRole.Focus);

            Assert.AreEqual(first, second);
            Assert.IsTrue(first.IsValid);
            Assert.IsTrue(map.TryResolve(10, out MxRenderSubjectId resolved));
            Assert.AreEqual(first, resolved);
            Assert.IsTrue(registry.TryResolve(first, out MxRenderSubjectRegistration registration));
            Assert.AreEqual(MxRenderSubjectRole.Primary, registration.Role);
        }

        [Test]
        public void SubjectMap_Release_ClearsSourceMappingAndRejectsStaleSubject()
        {
            var registry = new MxRenderSubjectRegistry();
            IRenderSubjectMap<string> map = registry.CreateMap<string>();
            MxRenderSubjectId subject = map.GetOrCreate("source-a", MxRenderSubjectRole.Tracked);

            Assert.IsTrue(map.Release("source-a"));

            Assert.IsFalse(map.TryResolve("source-a", out var _));
            Assert.IsFalse(registry.TryResolve(subject, out var _));
            Assert.AreEqual(0, registry.ActiveCount);
        }

        [Test]
        public void Registry_ReleasedSlotCanReuseIndexWithNewGeneration()
        {
            var registry = new MxRenderSubjectRegistry();
            IRenderSubjectMap<int> map = registry.CreateMap<int>();
            MxRenderSubjectId first = map.GetOrCreate(1, MxRenderSubjectRole.Primary);

            Assert.IsTrue(map.Release(1));
            MxRenderSubjectId second = map.GetOrCreate(2, MxRenderSubjectRole.LocalControlled);

            Assert.AreNotEqual(first, second);
            Assert.AreEqual(first.Value & ((1 << 20) - 1), second.Value & ((1 << 20) - 1));
            Assert.IsFalse(registry.TryResolve(first, out var _));
            Assert.IsTrue(registry.TryResolve(second, out MxRenderSubjectRegistration registration));
            Assert.AreEqual(MxRenderSubjectRole.LocalControlled, registration.Role);
        }

        [Test]
        public void Registry_ReusesSlotOnlyAfterPublisherReleaseCallbackClearsEvents()
        {
            var registry = new MxRenderSubjectRegistry();
            MxRenderSubjectId first = registry.Register(MxRenderSubjectRole.Primary);
            var publisher = new RenderDataPublisher(registry, recentCapacity: 4);

            Assert.IsTrue(publisher.Publish(new RenderDataEvent(first, RenderDataEventKind.Movement, default, default)));
            Assert.IsTrue(registry.Release(first));
            MxRenderSubjectId second = registry.Register(MxRenderSubjectRole.Focus);

            Assert.AreEqual(first.Value & ((1 << 20) - 1), second.Value & ((1 << 20) - 1));
            Assert.AreNotEqual(first, second);
            Assert.IsFalse(registry.TryResolve(first, out var _));
            Assert.AreEqual(0, publisher.CaptureSnapshot().RecentEventCount);
        }

        [Test]
        public void PublicRenderingApi_DoesNotExposeForbiddenBusinessWords()
        {
            string renderingApi = string.Join(
                "\n",
                File.ReadAllText("Assets/Scripts/MxFramework/Rendering/RenderSubjectIdentity.cs"),
                File.ReadAllText("Assets/Scripts/MxFramework/Rendering/RenderDataPublisher.cs"),
                File.ReadAllText("Assets/Scripts/MxFramework/Rendering/RenderingDiagnostics.cs"));

            AssertForbidden(renderingApi, "Player");
            AssertForbidden(renderingApi, "Enemy");
            AssertForbidden(renderingApi, "Boss");
            AssertForbidden(renderingApi, "Hero");
            AssertForbidden(renderingApi, "Monster");
            AssertForbidden(renderingApi, "Skill");
            AssertForbidden(renderingApi, "Element");
            AssertForbidden(renderingApi, "LocalPlayer");
        }

        private static void AssertForbidden(string text, string forbidden)
        {
            Assert.IsFalse(text.Contains(forbidden), forbidden);
        }
    }
}
