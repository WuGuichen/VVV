using System.Collections.Generic;
using System.IO;
using System.Linq;
using MxFramework.Diagnostics;
using MxFramework.Rendering;
using NUnit.Framework;

namespace MxFramework.Tests.Rendering
{
    public class SharedRenderTextureRegistryTests
    {
        private static readonly SharedRTOwnerId OwnerA = new SharedRTOwnerId("owner-a");
        private static readonly SharedRTOwnerId OwnerB = new SharedRTOwnerId("owner-b");
        private static readonly SharedRTOwnerId Reader = new SharedRTOwnerId("reader");
        private static readonly SharedRTWriterSetId WriterSet = new SharedRTWriterSetId("writers");
        private static readonly SharedRTWriterSetId OwnerBWriterSet = new SharedRTWriterSetId("owner-b-writers");

        private SharedRenderTextureRegistry _registry;

        [TearDown]
        public void TearDown()
        {
            _registry?.Dispose();
            _registry = null;
        }

        [Test]
        public void SharedRTRegistry_R_RT_01_AdditiveWritersAllowed()
        {
            _registry = CreateRegistry(allowAdditiveWriters: true, out SharedRTHandle handle);

            SharedRTAccessResult first = _registry.RecordWriter(handle, OwnerA, MxRenderPhase.BeforeRenderingOpaques, 0);
            SharedRTAccessResult second = _registry.RecordWriter(handle, OwnerB, MxRenderPhase.BeforeRenderingOpaques, 1);

            Assert.IsTrue(first.Succeeded);
            Assert.IsTrue(second.Succeeded);
            Assert.AreEqual(SharedRTConflictCode.AdditiveWritersAllowed, second.ConflictCode);
            SharedRTDiagnosticsEntry entry = SingleEntry();
            Assert.AreEqual(2, entry.CurrentFrameWriters.Count);
            AssertConflict(SharedRTConflictCode.AdditiveWritersAllowed);
        }

        [Test]
        public void SharedRTRegistry_R_RT_02_WriterConflict()
        {
            _registry = CreateRegistry(allowAdditiveWriters: false, out SharedRTHandle handle);

            SharedRTAccessResult first = _registry.RecordWriter(handle, OwnerA, MxRenderPhase.BeforeRenderingOpaques, 0);
            SharedRTAccessResult second = _registry.RecordWriter(handle, OwnerB, MxRenderPhase.BeforeRenderingOpaques, 1);

            Assert.IsTrue(first.Succeeded);
            Assert.IsFalse(second.Succeeded);
            Assert.AreEqual(SharedRTConflictCode.WriterConflict, second.ConflictCode);
            SharedRTDiagnosticsEntry entry = SingleEntry();
            Assert.AreEqual(1, entry.CurrentFrameWriters.Count);
            AssertConflict(SharedRTConflictCode.WriterConflict);
        }

        [Test]
        public void SharedRTRegistry_R_RT_03_StaleReader()
        {
            _registry = CreateRegistry(allowAdditiveWriters: false, out SharedRTHandle handle);
            _registry.RecordWriter(handle, OwnerA, MxRenderPhase.BeforeRenderingOpaques, 10);

            SharedRTAccessResult stale = _registry.RecordReader(handle, Reader, MxRenderPhase.BeforeRenderingOpaques, 10);
            SharedRTAccessResult fresh = _registry.RecordReader(handle, Reader, MxRenderPhase.BeforeRenderingOpaques, 11);

            Assert.IsFalse(stale.Succeeded);
            Assert.AreEqual(SharedRTConflictCode.StaleReader, stale.ConflictCode);
            Assert.IsTrue(fresh.Succeeded);
            Assert.AreEqual(1, SingleEntry().CurrentFrameReaders.Count);
            AssertConflict(SharedRTConflictCode.StaleReader);
        }

        [Test]
        public void SharedRTRegistry_R_RT_04_UnauthorizedWriter()
        {
            _registry = CreateRegistry(allowAdditiveWriters: false, out SharedRTHandle handle, allowedWriters: new[] { OwnerA });

            SharedRTAccessResult result = _registry.RecordWriter(handle, OwnerB, MxRenderPhase.BeforeRenderingOpaques, 0);

            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual(SharedRTConflictCode.UnauthorizedWriter, result.ConflictCode);
            Assert.AreEqual(0, SingleEntry().CurrentFrameWriters.Count);
            AssertConflict(SharedRTConflictCode.UnauthorizedWriter);
        }

        [Test]
        public void SharedRTRegistry_R_RT_04_ReRegisterUnauthorizedWriterRejectedWithoutMutation()
        {
            _registry = CreateRegistry(allowAdditiveWriters: false, out SharedRTHandle originalHandle, allowedWriters: new[] { OwnerA });

            SharedRenderTextureKey unauthorizedKey = CreateKey(
                "unauthorized-re-register",
                owner: OwnerB,
                size: new SharedRTSize(32, 32));
            SharedRTHandle rejectedHandle = _registry.Register(unauthorizedKey);

            Assert.IsFalse(rejectedHandle.IsValid);
            Assert.IsTrue(_registry.TryResolve(originalHandle, out var _));
            SharedRTDiagnosticsEntry entry = SingleEntry();
            Assert.AreEqual(OwnerA, entry.Owner);
            Assert.AreEqual(new SharedRTSize(16, 16), entry.Dimensions);
            Assert.AreNotEqual("unauthorized-re-register", entry.DebugName);
            AssertConflict(SharedRTConflictCode.UnauthorizedWriter);
        }

        [Test]
        public void SharedRTRegistry_R_RT_04_ReRegisterCannotBypassExistingOwnershipWithIncomingWriterSet()
        {
            _registry = CreateRegistry(
                allowAdditiveWriters: false,
                out SharedRTHandle originalHandle,
                resizePolicy: SharedRTResizePolicy.FailOnResize,
                allowedWriters: new[] { OwnerA });
            _registry.RegisterWriterSet(OwnerBWriterSet, new[] { OwnerB });

            SharedRenderTextureKey spoofedKey = CreateKey(
                "owner-b-spoofed",
                owner: OwnerB,
                writerSet: OwnerBWriterSet,
                format: SharedRTFormat.R8,
                size: new SharedRTSize(64, 64),
                resizePolicy: SharedRTResizePolicy.KeepLargest);
            SharedRTHandle rejectedHandle = _registry.Register(spoofedKey);

            Assert.IsFalse(rejectedHandle.IsValid);
            Assert.IsTrue(_registry.TryResolve(originalHandle, out var _));
            SharedRTDiagnosticsEntry entry = SingleEntry();
            Assert.AreEqual(OwnerA, entry.Owner);
            Assert.AreEqual("test-shared-rt", entry.DebugName);
            Assert.AreEqual(SharedRTFormat.ARGB32, entry.Format);
            Assert.AreEqual(new SharedRTSize(16, 16), entry.Dimensions);
            Assert.AreEqual(SharedRTResizePolicy.FailOnResize, entry.Resize);
            AssertConflict(SharedRTConflictCode.UnauthorizedWriter);
        }

        [Test]
        public void SharedRTRegistry_R_RT_04_ReRegisterRequiresRequestedWriterSetAuthorization()
        {
            _registry = CreateRegistry(
                allowAdditiveWriters: false,
                out SharedRTHandle originalHandle,
                resizePolicy: SharedRTResizePolicy.FailOnResize,
                allowedWriters: new[] { OwnerA });
            _registry.RegisterWriterSet(OwnerBWriterSet, new[] { OwnerB });

            SharedRenderTextureKey requestedKey = CreateKey(
                "owner-a-requested-owner-b-writer-set",
                owner: OwnerA,
                writerSet: OwnerBWriterSet,
                format: SharedRTFormat.R8,
                size: new SharedRTSize(64, 64),
                resizePolicy: SharedRTResizePolicy.KeepLargest);

            SharedRTHandle rejectedHandle = _registry.Register(requestedKey);

            Assert.IsFalse(rejectedHandle.IsValid);
            Assert.IsTrue(_registry.TryResolve(originalHandle, out var _));
            SharedRTDiagnosticsEntry entry = SingleEntry();
            Assert.AreEqual(OwnerA, entry.Owner);
            Assert.AreEqual("test-shared-rt", entry.DebugName);
            Assert.AreEqual(SharedRTFormat.ARGB32, entry.Format);
            Assert.AreEqual(new SharedRTSize(16, 16), entry.Dimensions);
            Assert.AreEqual(SharedRTResizePolicy.FailOnResize, entry.Resize);

            SharedRTAccessResult ownerBWrite = _registry.RecordWriter(originalHandle, OwnerB, MxRenderPhase.BeforeRenderingOpaques, 0);
            SharedRTAccessResult ownerAWrite = _registry.RecordWriter(originalHandle, OwnerA, MxRenderPhase.BeforeRenderingOpaques, 1);

            Assert.IsFalse(ownerBWrite.Succeeded);
            Assert.AreEqual(SharedRTConflictCode.UnauthorizedWriter, ownerBWrite.ConflictCode);
            Assert.IsTrue(ownerAWrite.Succeeded);
            Assert.AreEqual(SharedRTConflictCode.None, ownerAWrite.ConflictCode);
            AssertConflict(SharedRTConflictCode.UnauthorizedWriter);
        }

        [Test]
        public void SharedRTRegistry_R_RT_05_OrphanRT()
        {
            _registry = CreateRegistry(allowAdditiveWriters: false, out _, orphanThreshold: 2);

            _registry.EndFrame();
            _registry.BeginFrame();
            _registry.EndFrame();

            SharedRTDiagnosticsEntry entry = SingleEntry();
            Assert.IsTrue(entry.IsOrphaned);
            Assert.AreEqual(0, entry.ActualMemoryBytes);
            AssertConflict(SharedRTConflictCode.OrphanRT);
        }

        [Test]
        public void SharedRTRegistry_R_RT_06_ResizeRejected()
        {
            _registry = CreateRegistry(allowAdditiveWriters: false, out SharedRTHandle handle, resizePolicy: SharedRTResizePolicy.FailOnResize);

            SharedRTAccessResult result = _registry.RequestResize(handle, new SharedRTSize(32, 32));

            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual(SharedRTConflictCode.ResizeRejected, result.ConflictCode);
            Assert.AreEqual(new SharedRTSize(16, 16), SingleEntry().Dimensions);
            AssertConflict(SharedRTConflictCode.ResizeRejected);
        }

        [Test]
        public void SharedRTRegistry_R_RT_07_ResizeBurst()
        {
            _registry = CreateRegistry(
                allowAdditiveWriters: false,
                out SharedRTHandle handle,
                resizePolicy: SharedRTResizePolicy.Reallocate,
                resizeBurstThreshold: 2);

            _registry.RequestResize(handle, new SharedRTSize(32, 32));
            _registry.AdvanceTestTime(0.1f);
            _registry.RequestResize(handle, new SharedRTSize(64, 64));
            _registry.AdvanceTestTime(0.1f);
            SharedRTAccessResult result = _registry.RequestResize(handle, new SharedRTSize(128, 128));

            Assert.IsTrue(result.Succeeded);
            Assert.AreEqual(SharedRTConflictCode.ResizeBurst, result.ConflictCode);
            AssertConflict(SharedRTConflictCode.ResizeBurst);
        }

        [Test]
        public void SharedRTRegistry_R_RT_08_DroppedAllocation()
        {
            _registry = CreateRegistry(allowAdditiveWriters: false, out SharedRTHandle handle, simulateAllocationFailure: true);

            Assert.IsTrue(handle.IsValid);
            var resolved = _registry.TryResolve(handle, out var _);
            Assert.IsTrue(resolved);
            SharedRTDiagnosticsEntry entry = SingleEntry();
            Assert.IsTrue(entry.IsFallback);
            Assert.AreEqual(0, entry.ActualMemoryBytes);
            AssertConflict(SharedRTConflictCode.DroppedAllocation);
        }

        [Test]
        public void SharedRTRegistry_R_RT_08_DroppedAllocation_ResizeKeepsFallbackResolvable()
        {
            _registry = CreateRegistry(allowAdditiveWriters: false, out SharedRTHandle handle, simulateAllocationFailure: true);

            SharedRTAccessResult result = default;
            Assert.DoesNotThrow(() => result = _registry.RequestResize(handle, new SharedRTSize(32, 32)));

            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual(SharedRTConflictCode.DroppedAllocation, result.ConflictCode);
            var resolved = _registry.TryResolve(handle, out var _);
            Assert.IsTrue(resolved);
            Assert.IsTrue(SingleEntry().IsFallback);
            AssertConflict(SharedRTConflictCode.DroppedAllocation);
        }

        [Test]
        public void SharedRenderTextureKey_DebugName_DoesNotAffectIdentityOrHash()
        {
            SharedRenderTextureKey left = CreateKey("localized-name");
            SharedRenderTextureKey right = CreateKey("debug-name-changed");

            Assert.AreEqual(left, right);
            Assert.AreEqual(left.GetHashCode(), right.GetHashCode());
        }

        [Test]
        public void SharedRTRegistryDiagnostics_ExposeMemoryReadersWritersAndDebugSource()
        {
            _registry = CreateRegistry(allowAdditiveWriters: true, out SharedRTHandle handle);
            _registry.RecordWriter(handle, OwnerA, MxRenderPhase.BeforeRenderingOpaques, 0);
            _registry.RecordReader(handle, Reader, MxRenderPhase.BeforeRenderingOpaques, 1);

            SharedRTDiagnosticsSnapshot diagnostics = _registry.CaptureDiagnostics();
            Assert.AreEqual(1, diagnostics.Entries.Count);
            Assert.Greater(diagnostics.ActualMemoryBytes, 0);
            Assert.Greater(diagnostics.MemoryBudgetBytes, 0);
            Assert.AreEqual(1, diagnostics.Entries[0].CurrentFrameReaders.Count);
            Assert.AreEqual(1, diagnostics.Entries[0].CurrentFrameWriters.Count);

            var source = new SharedRTRegistryDebugSource(_registry);
            FrameworkDebugSnapshot snapshot = source.CreateSnapshot();
            Assert.AreEqual(RenderingDebugSectionNames.SharedRTHealth, snapshot.Sections[0].Title);
            StringAssert.Contains("actualBytes:", snapshot.Sections[0].Body);
        }

        [Test]
        public void RenderingAsmdef_DoesNotGainForbiddenSharedRTDependencies()
        {
            string asmdef = File.ReadAllText("Assets/Scripts/MxFramework/Rendering/MxFramework.Rendering.asmdef");

            Assert.IsFalse(asmdef.Contains("MxFramework.Gameplay"));
            Assert.IsFalse(asmdef.Contains("MxFramework.Combat"));
            Assert.IsFalse(asmdef.Contains("MxFramework.Buffs"));
            Assert.IsFalse(asmdef.Contains("MxFramework.Resources"));
            Assert.IsFalse(asmdef.Contains("MxFramework.DebugUI"));
        }

        private static SharedRenderTextureKey CreateKey(
            string debugName,
            bool allowAdditiveWriters = false,
            SharedRTResizePolicy resizePolicy = SharedRTResizePolicy.Reallocate,
            SharedRTOwnerId owner = default,
            SharedRTSize size = default,
            SharedRTWriterSetId writerSet = default,
            SharedRTFormat format = SharedRTFormat.ARGB32)
        {
            if (!owner.IsValid)
                owner = OwnerA;
            if (!size.IsValid)
                size = new SharedRTSize(16, 16);
            if (!writerSet.IsValid)
                writerSet = WriterSet;

            return new SharedRenderTextureKey(
                new SharedRTId("mx.shared.test"),
                debugName,
                owner,
                new SharedRTAccessPolicy(allowAdditiveWriters, SharedRTOrderRule.ReadAfterWriteSameFrame, writerSet),
                SharedRTAnchor.MainCamera,
                format,
                size,
                new SharedRTClearSpec(SharedRTClearKind.ClearEveryFrame, UnityEngine.Color.clear),
                resizePolicy);
        }

        private SharedRenderTextureRegistry CreateRegistry(
            bool allowAdditiveWriters,
            out SharedRTHandle handle,
            SharedRTResizePolicy resizePolicy = SharedRTResizePolicy.Reallocate,
            IReadOnlyList<SharedRTOwnerId> allowedWriters = null,
            int orphanThreshold = 3,
            int resizeBurstThreshold = 3,
            bool simulateAllocationFailure = false)
        {
            var registry = new SharedRenderTextureRegistry(new SharedRenderTextureRegistryOptions
            {
                OrphanFrameThreshold = orphanThreshold,
                ResizeBurstThresholdPerSecond = resizeBurstThreshold,
                SimulateAllocationFailure = simulateAllocationFailure
            });

            registry.RegisterWriterSet(WriterSet, allowedWriters ?? new[] { OwnerA, OwnerB });
            handle = registry.Register(CreateKey("test-shared-rt", allowAdditiveWriters, resizePolicy));
            Assert.IsTrue(handle.IsValid);
            return registry;
        }

        private SharedRTDiagnosticsEntry SingleEntry()
        {
            SharedRTDiagnosticsSnapshot snapshot = _registry.CaptureDiagnostics();
            Assert.AreEqual(1, snapshot.Entries.Count);
            return snapshot.Entries[0];
        }

        private void AssertConflict(SharedRTConflictCode code)
        {
            SharedRTDiagnosticsSnapshot snapshot = _registry.CaptureDiagnostics();
            Assert.IsTrue(snapshot.RecentConflicts.Any(conflict => conflict.Code == code), "Expected SharedRT conflict " + code + ".");
        }
    }
}
