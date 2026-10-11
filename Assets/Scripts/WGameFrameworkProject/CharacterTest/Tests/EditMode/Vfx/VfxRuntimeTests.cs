using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using MxFramework.Combat.Animation;
using MxFramework.Combat.Core;
using MxFramework.Combat.Damage;
using MxFramework.Combat.Hit;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using MxFramework.Game.CharacterTest;
using MxFramework.Gameplay;
using MxFramework.Rendering;
using MxFramework.Resources;
using MxFramework.Resources.Unity;
using MxFramework.Runtime;
using MxFramework.Vfx;
using MxFramework.Vfx.CombatBridge;
using MxFramework.Vfx.GameplayBridge;
using MxFramework.Vfx.RenderingBridge;
using MxFramework.Vfx.Unity;
using MxFramework.Editor;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace MxFramework.Tests.Vfx
{
    public sealed class VfxRuntimeTests
    {
        private const int TestTraceKind = 9701;
        private static readonly ResourceKey TestPrefabKey = new ResourceKey("vfx.test.impact", ResourceTypeIds.GameObject);

        [Test]
        public void ApplicationBladeTrailCatalog_PublishedThenMemory_KeepsPublishedResource()
        {
            object published = new object();
            object fallback = new object();
            using (var provider = new MemoryResourceProvider()
                .Register("test/blade/published", published)
                .Register(CharacterTestBladeTrailVfxIds.ResourceAddress, fallback))
            {
                var manager = new ResourceManager();
                manager.RegisterProvider(provider);
                var key = new ResourceKey(CharacterTestBladeTrailVfxIds.ResourceId,
                    ResourceTypeIds.GameObject, packageId: CharacterTestBladeTrailVfxIds.ResourcePackageId);
                manager.AddCatalog(new ResourceCatalog("test.global.published", key.PackageId, new[]
                {
                    new ResourceCatalogEntry(key.Id, key.TypeId, MemoryResourceProvider.Id,
                        "test/blade/published", packageId: key.PackageId),
                }));

                CharacterTestRuntimeSessionBuilder.RegisterApplicationVfxCatalog(
                    manager, CharacterTestVfxDefinitionSource.CreateResourceCatalog());

                ResourceLoadResult<ResourceHandle<object>> loaded = manager.Load<object>(key);
                Assert.That(loaded.Success, Is.True);
                Assert.That(loaded.Value.Value, Is.SameAs(published));
                manager.Release(loaded.Value);
                Assert.That(manager.Contains(new ResourceKey(CharacterTestVfxIds.FireballResourceId,
                    ResourceTypeIds.GameObject, packageId: CharacterTestVfxIds.FireballResourcePackageId)), Is.True);
            }
        }

        [Test]
        public void ApplicationBladeTrailCatalog_WithoutPublishedKey_LoadsSceneMemoryFallback()
        {
            object fallback = new object();
            using (var provider = new MemoryResourceProvider()
                .Register(CharacterTestBladeTrailVfxIds.ResourceAddress, fallback))
            {
                var manager = new ResourceManager();
                manager.RegisterProvider(provider);
                CharacterTestRuntimeSessionBuilder.RegisterApplicationVfxCatalog(
                    manager, CharacterTestVfxDefinitionSource.CreateResourceCatalog());
                var key = new ResourceKey(CharacterTestBladeTrailVfxIds.ResourceId,
                    ResourceTypeIds.GameObject, packageId: CharacterTestBladeTrailVfxIds.ResourcePackageId);
                ResourceLoadResult<ResourceHandle<object>> loaded = manager.Load<object>(key);
                Assert.That(loaded.Success, Is.True);
                Assert.That(loaded.Value.Value, Is.SameAs(fallback));
                manager.Release(loaded.Value);
            }
        }

        [Test]
        public void ApplicationBladeTrailCatalog_UnrelatedDuplicate_RemainsAnAtomicError()
        {
            using (var provider = new MemoryResourceProvider())
            {
                var manager = new ResourceManager();
                manager.RegisterProvider(provider);
                manager.AddCatalog(new ResourceCatalog("test.global.conflict",
                    CharacterTestVfxIds.FireballResourcePackageId, new[]
                    {
                        new ResourceCatalogEntry(CharacterTestVfxIds.FireballResourceId,
                            ResourceTypeIds.GameObject, MemoryResourceProvider.Id, "test/fireball/published",
                            packageId: CharacterTestVfxIds.FireballResourcePackageId),
                    }));

                Assert.Throws<ResourceCatalogException>(() =>
                    CharacterTestRuntimeSessionBuilder.RegisterApplicationVfxCatalog(
                        manager, CharacterTestVfxDefinitionSource.CreateResourceCatalog()));
                Assert.That(manager.Contains(new ResourceKey(CharacterTestBladeTrailVfxIds.ResourceId,
                    ResourceTypeIds.GameObject, packageId: CharacterTestBladeTrailVfxIds.ResourcePackageId)), Is.False);
            }
        }

        [Test]
        public void ResetTransientState_ClearsRuntimeWithoutEmittingLifecycleEvents()
        {
            VfxRuntimeModule module = CreateModule(out RuntimeCommandBuffer commands);
            commands.Enqueue(VfxRuntimeCommandFactory.Spawn(RuntimeFrame.Zero, 10, new VfxDefinitionId(1), traceId: Trace(1)));
            module.EnqueueSpawnRequest(new VfxSpawnRequest(new RuntimeFrame(5), VfxSourceDomains.Manual, 5, 0, 0, 1, 1, 2,
                new VfxDefinitionId(2), VfxAnchor.World(VfxPose.Identity), VfxParameterBlock.Empty, 0, Trace(2)));
            module.Tick(new RuntimeTickContext(0, 0d, 0d, RuntimeTickStage.PostSimulation));

            module.ResetTransientState(new RuntimeFrame(3));

            Assert.That(module.ActiveCount, Is.Zero);
            Assert.That(module.PendingSpawnCount, Is.Zero);
            Assert.That(commands.PendingCount, Is.Zero);
            Assert.That(commands.CurrentFrame, Is.EqualTo(new RuntimeFrame(3)));
            var events = new List<VfxRuntimeEvent>();
            Assert.That(module.DrainEvents(RuntimeFrame.Zero, events), Is.Zero);
            Assert.That(events, Is.Empty);
        }

        [Test]
        public void ResetTransientState_ReplaysSameInputWithSameInstanceIdentity()
        {
            VfxRuntimeModule module = CreateModule(out RuntimeCommandBuffer commands);
            var events = new List<VfxRuntimeEvent>();
            commands.Enqueue(VfxRuntimeCommandFactory.Spawn(RuntimeFrame.Zero, 10, new VfxDefinitionId(1), traceId: Trace(3)));
            module.Tick(new RuntimeTickContext(0, 0d, 0d, RuntimeTickStage.PostSimulation));
            module.DrainEvents(RuntimeFrame.Zero, events);
            VfxRuntimeEvent first = events.Single(value => value.Type == VfxRuntimeEventType.Spawned);

            module.ResetTransientState(RuntimeFrame.Zero);
            events.Clear();
            commands.Enqueue(VfxRuntimeCommandFactory.Spawn(RuntimeFrame.Zero, 10, new VfxDefinitionId(1), traceId: Trace(3)));
            module.Tick(new RuntimeTickContext(0, 0d, 0d, RuntimeTickStage.PostSimulation));
            module.DrainEvents(RuntimeFrame.Zero, events);
            VfxRuntimeEvent replay = events.Single(value => value.Type == VfxRuntimeEventType.Spawned);

            Assert.That(replay.InstanceId, Is.EqualTo(first.InstanceId));
            Assert.That(replay.DefinitionId, Is.EqualTo(first.DefinitionId));
            Assert.That(replay.Type, Is.EqualTo(first.Type));
        }

        [Test]
        public void ResetTransientState_BackwardSeekIsExplicitAndRejectsCommandsBeforeResetFrame()
        {
            VfxRuntimeModule module = CreateModule(out RuntimeCommandBuffer commands);
            module.ResetTransientState(new RuntimeFrame(8));
            RuntimeCommandValidationResult late = commands.Enqueue(
                VfxRuntimeCommandFactory.Spawn(new RuntimeFrame(7), 1, new VfxDefinitionId(1)));
            Assert.That(late.Success, Is.False);
            Assert.That(late.Error.Code, Is.EqualTo(RuntimeCommandErrorCode.LateCommand));

            Assert.DoesNotThrow(() => module.ResetTransientState(new RuntimeFrame(2)));
            Assert.That(commands.CurrentFrame, Is.EqualTo(new RuntimeFrame(2)));
            Assert.That(commands.Enqueue(VfxRuntimeCommandFactory.Spawn(new RuntimeFrame(2), 1, new VfxDefinitionId(1))).Success, Is.True);
        }

        [Test]
        public void ResetTransientState_WarmedLoop_AllocatesZero()
        {
            VfxRuntimeModule module = CreateModule(out _);
            for (int i = 0; i < 64; i++)
                module.ResetTransientState(new RuntimeFrame(i));
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 4096; i++)
                module.ResetTransientState(new RuntimeFrame(i));
            Assert.That(GC.GetAllocatedBytesForCurrentThread() - before, Is.Zero);
        }

        [Test]
        public void RuntimeModule_SpawnsExpiresAndRejectsInvalidCommands()
        {
            VfxRuntimeModule module = CreateModule(out RuntimeCommandBuffer commands);

            commands.Enqueue(VfxRuntimeCommandFactory.Spawn(RuntimeFrame.Zero, 10, new VfxDefinitionId(1), targetId: 20, parameter0: 1500, traceId: Trace(4)));
            commands.Enqueue(VfxRuntimeCommandFactory.Spawn(RuntimeFrame.Zero, 10, new VfxDefinitionId(999), traceId: Trace(5)));

            module.Tick(new RuntimeTickContext(0, 0d, 0d, RuntimeTickStage.PostSimulation));

            Assert.AreEqual(1, module.ActiveCount);
            var events = new List<VfxRuntimeEvent>();
            Assert.AreEqual(2, module.DrainEvents(RuntimeFrame.Zero, events));
            Assert.AreEqual(VfxRuntimeEventType.Rejected, events[0].Type);
            Assert.AreEqual(VfxRuntimeEventType.Spawned, events[1].Type);
            Assert.AreEqual(new VfxInstanceId(1), events[1].InstanceId);

            commands.Enqueue(VfxRuntimeCommandFactory.Stop(new RuntimeFrame(1), new VfxInstanceId(1), Trace(6)));
            commands.Enqueue(VfxRuntimeCommandFactory.Stop(new RuntimeFrame(1), new VfxInstanceId(1), Trace(7)));
            module.Tick(new RuntimeTickContext(1, 0d, 0d, RuntimeTickStage.PostSimulation));

            events.Clear();
            Assert.AreEqual(2, module.DrainEvents(new RuntimeFrame(1), events));
            Assert.AreEqual(VfxRuntimeEventType.Stopped, events[0].Type);
            Assert.AreEqual(VfxRuntimeEventType.Rejected, events[1].Type);
            Assert.AreEqual(0, module.ActiveCount);
        }

        [Test]
        public void RuntimeModule_ExpirationUsesAuthoredStopMode()
        {
            VfxRuntimeModule module = CreateModule(out _);
            module.EnqueueSpawnRequest(new VfxSpawnRequest(
                RuntimeFrame.Zero,
                VfxSourceDomains.ActionPresentation,
                sourceFrame: 0,
                localFrame: 0,
                sourceOrder: 0,
                triggerId: 1,
                sourceId: 10,
                targetId: 0,
                new VfxDefinitionId(1),
                VfxAnchor.World(VfxPose.Identity),
                VfxParameterBlock.Empty,
                lifetimeFramesOverride: 1,
                traceId: Trace(8),
                expireStopMode: VfxStopMode.Dissipate));

            module.Tick(new RuntimeTickContext(0, 0d, 0d, RuntimeTickStage.PostSimulation));
            var events = new List<VfxRuntimeEvent>();
            module.DrainEvents(RuntimeFrame.Zero, events);
            events.Clear();

            module.Tick(new RuntimeTickContext(1, 0d, 0d, RuntimeTickStage.PostSimulation));
            Assert.AreEqual(1, module.DrainEvents(new RuntimeFrame(1), events));
            Assert.AreEqual(VfxRuntimeEventType.Expired, events[0].Type);
            Assert.AreEqual(VfxStopMode.Dissipate, events[0].StopMode);
        }

        [Test]
        public void RuntimeModule_DefaultLifetimeScalesWithPlaybackRate()
        {
            VfxRuntimeModule module = CreateModule(out _);
            module.EnqueueSpawnRequest(new VfxSpawnRequest(
                RuntimeFrame.Zero,
                VfxSourceDomains.ActionPresentation,
                sourceFrame: 0,
                localFrame: 0,
                sourceOrder: 0,
                triggerId: 1,
                sourceId: 10,
                targetId: 0,
                new VfxDefinitionId(1),
                VfxAnchor.World(VfxPose.Identity),
                VfxParameterBlock.Empty,
                lifetimeFramesOverride: 0,
                traceId: Trace(9),
                playbackRatePermille: 2000));

            module.Tick(new RuntimeTickContext(0, 0d, 0d, RuntimeTickStage.PostSimulation));
            var active = new List<VfxInstanceSnapshot>();
            module.CopyActiveInstances(active);

            Assert.That(active, Has.Count.EqualTo(1));
            Assert.That(active[0].ExpireFrame, Is.EqualTo(new RuntimeFrame(1)));

            module.Tick(new RuntimeTickContext(1, 0d, 0d, RuntimeTickStage.PostSimulation));
            Assert.That(module.ActiveCount, Is.Zero);
        }

        [Test]
        public void RuntimeModule_DefaultLifetimeAppliesSourceTrimsAndCarriesOffsets()
        {
            VfxRuntimeModule module = CreateModule(out _);
            module.EnqueueSpawnRequest(new VfxSpawnRequest(
                RuntimeFrame.Zero,
                VfxSourceDomains.ActionPresentation,
                sourceFrame: 0,
                localFrame: 0,
                sourceOrder: 0,
                triggerId: 1,
                sourceId: 10,
                targetId: 0,
                new VfxDefinitionId(2),
                VfxAnchor.World(VfxPose.Identity),
                VfxParameterBlock.Empty,
                lifetimeFramesOverride: 0,
                traceId: Trace(10),
                sourceStartOffsetFrames: 1,
                sourceEndOffsetFrames: 1));

            module.Tick(new RuntimeTickContext(0, 0d, 0d, RuntimeTickStage.PostSimulation));
            var active = new List<VfxInstanceSnapshot>();
            module.CopyActiveInstances(active);

            Assert.That(active, Has.Count.EqualTo(1));
            Assert.That(active[0].SourceStartOffsetFrames, Is.EqualTo(1));
            Assert.That(active[0].SourceEndOffsetFrames, Is.EqualTo(1));
            Assert.That(active[0].ExpireFrame, Is.EqualTo(new RuntimeFrame(1)));
        }

        [Test]
        public void RuntimeCommandFactory_StopRoundTripsLongInstanceIds()
        {
            var instanceId = new VfxInstanceId((long)int.MaxValue + 5L);
            RuntimeCommand command = VfxRuntimeCommandFactory.Stop(RuntimeFrame.Zero, instanceId, Trace(11));

            Assert.AreEqual(instanceId, VfxRuntimeCommandFactory.DecodeStopInstanceId(command));
            Assert.IsTrue(new VfxRuntimeCommandValidator(CreateDefinitions("hash-v1")).Validate(command).Success);
        }

        [Test]
        public void RuntimeCommandFactory_RoundTripsStopModeAndRebuildInstance()
        {
            var instanceId = new VfxInstanceId((long)int.MaxValue + 17L);
            RuntimeCommand stop = VfxRuntimeCommandFactory.Stop(RuntimeFrame.Zero, instanceId, VfxStopMode.Dissipate, Trace(12));
            RuntimeCommand rebuild = VfxRuntimeCommandFactory.Rebuild(RuntimeFrame.Zero, instanceId, Trace(13));

            Assert.That(VfxRuntimeCommandFactory.DecodeStopInstanceId(stop), Is.EqualTo(instanceId));
            Assert.That(VfxRuntimeCommandFactory.DecodeStopMode(stop), Is.EqualTo(VfxStopMode.Dissipate));
            Assert.That(VfxRuntimeCommandFactory.DecodeRebuildInstanceId(rebuild), Is.EqualTo(instanceId));
            VfxRuntimeCommandValidator validator = new VfxRuntimeCommandValidator(CreateDefinitions("hash-v1"));
            Assert.That(validator.Validate(stop).Success, Is.True);
            Assert.That(validator.Validate(rebuild).Success, Is.True);
        }

        [Test]
        public void RuntimeModule_RebuildsOnlyActiveInstanceAndStopWinsSameFrame()
        {
            VfxRuntimeModule module = CreateModule(out RuntimeCommandBuffer commands);
            commands.Enqueue(VfxRuntimeCommandFactory.Spawn(RuntimeFrame.Zero, 1, new VfxDefinitionId(1)));
            module.Tick(new RuntimeTickContext(0, 0d, 0d, RuntimeTickStage.PostSimulation));
            var events = new List<VfxRuntimeEvent>();
            module.DrainEvents(RuntimeFrame.Zero, events);

            commands.Enqueue(VfxRuntimeCommandFactory.Rebuild(new RuntimeFrame(1), new VfxInstanceId(1), Trace(14)));
            module.Tick(new RuntimeTickContext(1, 0d, 0d, RuntimeTickStage.PostSimulation));
            events.Clear();
            module.DrainEvents(new RuntimeFrame(1), events);
            Assert.That(events.Select(value => value.Type), Is.EqualTo(new[] { VfxRuntimeEventType.Rebuilt }));
            Assert.That(module.ActiveCount, Is.EqualTo(1));

            commands.Enqueue(VfxRuntimeCommandFactory.Stop(new RuntimeFrame(2), new VfxInstanceId(1), VfxStopMode.Dissipate, Trace(15)));
            commands.Enqueue(VfxRuntimeCommandFactory.Rebuild(new RuntimeFrame(2), new VfxInstanceId(1), Trace(16)));
            module.Tick(new RuntimeTickContext(2, 0d, 0d, RuntimeTickStage.PostSimulation));
            events.Clear();
            module.DrainEvents(new RuntimeFrame(2), events);
            Assert.That(events.Select(value => value.Type), Is.EqualTo(new[] { VfxRuntimeEventType.Stopped, VfxRuntimeEventType.Rejected }));
            Assert.That(events[0].StopMode, Is.EqualTo(VfxStopMode.Dissipate));
            Assert.That(module.ActiveCount, Is.Zero);
        }

        [Test]
        public void RuntimeHash_TracksActiveAndPendingDeterministicState()
        {
            VfxRuntimeModule first = CreateModule(out RuntimeCommandBuffer firstCommands);
            VfxRuntimeModule second = CreateModule(out RuntimeCommandBuffer secondCommands);

            firstCommands.Enqueue(VfxRuntimeCommandFactory.Spawn(RuntimeFrame.Zero, 10, new VfxDefinitionId(1), targetId: 20, parameter0: 7));
            secondCommands.Enqueue(VfxRuntimeCommandFactory.Spawn(RuntimeFrame.Zero, 10, new VfxDefinitionId(1), targetId: 20, parameter0: 7));
            first.EnqueueSpawnRequest(new VfxSpawnRequest(new RuntimeFrame(3), VfxSourceDomains.Manual, 3, 0, 0, 1, 1, 2, new VfxDefinitionId(2), VfxAnchor.World(VfxPose.Identity), VfxParameterBlock.Empty, 0, Trace(17)));
            second.EnqueueSpawnRequest(new VfxSpawnRequest(new RuntimeFrame(3), VfxSourceDomains.Manual, 3, 0, 0, 1, 1, 2, new VfxDefinitionId(2), VfxAnchor.World(VfxPose.Identity), VfxParameterBlock.Empty, 0, Trace(17)));

            first.Tick(new RuntimeTickContext(0, 0d, 0d, RuntimeTickStage.PostSimulation));
            second.Tick(new RuntimeTickContext(0, 0d, 0d, RuntimeTickStage.PostSimulation));

            long firstHash = RuntimeHashCombiner.ComputeHash(RuntimeFrame.Zero, new[] { new VfxRuntimeHashContributor(first) });
            long secondHash = RuntimeHashCombiner.ComputeHash(RuntimeFrame.Zero, new[] { new VfxRuntimeHashContributor(second) });
            Assert.AreEqual(firstHash, secondHash);

            VfxDefinitionRegistry changedDefinitions = CreateDefinitions("changed-hash");
            var changed = new VfxRuntimeModule(changedDefinitions, new RuntimeCommandBuffer());
            changed.Tick(new RuntimeTickContext(0, 0d, 0d, RuntimeTickStage.PostSimulation));
            long changedHash = RuntimeHashCombiner.ComputeHash(RuntimeFrame.Zero, new[] { new VfxRuntimeHashContributor(changed) });
            Assert.AreNotEqual(firstHash, changedHash);
        }

        [Test]
        public void RuntimeHash_ChangesWhenLiveStateMutates()
        {
            VfxRuntimeModule module = CreateModule(out RuntimeCommandBuffer commands);
            commands.Enqueue(VfxRuntimeCommandFactory.Spawn(
                RuntimeFrame.Zero,
                10,
                new VfxDefinitionId(1),
                targetId: 20,
                parameter0: 7,
                traceId: Trace(30)));
            module.Tick(new RuntimeTickContext(0, 0d, 0d, RuntimeTickStage.PostSimulation));

            var contributor = new VfxRuntimeHashContributor(module);
            var accumulator = new RuntimeHashAccumulator(contributor.HashSchemaId, contributor.HashSchemaVersion);
            long beforeMutation = ContributeHash(contributor, accumulator);

            module.EnqueueSpawnRequest(new VfxSpawnRequest(
                new RuntimeFrame(3),
                VfxSourceDomains.Manual,
                3,
                0,
                0,
                1,
                1,
                2,
                new VfxDefinitionId(2),
                VfxAnchor.World(VfxPose.Identity),
                VfxParameterBlock.Empty,
                lifetimeFramesOverride: 0,
                traceId: Trace(31)));

            long afterMutation = ContributeHash(contributor, accumulator);
            Assert.That(afterMutation, Is.Not.EqualTo(beforeMutation));
        }

        [Test]
        public void RuntimeHashContributor_StableIterationsMatchAndAllocateZeroAfterWarmup()
        {
            VfxRuntimeModule module = CreateModule(out RuntimeCommandBuffer commands);
            commands.Enqueue(VfxRuntimeCommandFactory.Spawn(
                RuntimeFrame.Zero,
                10,
                new VfxDefinitionId(1),
                targetId: 20,
                parameter0: 7,
                traceId: Trace(32)));
            module.Tick(new RuntimeTickContext(0, 0d, 0d, RuntimeTickStage.PostSimulation));

            var contributor = new VfxRuntimeHashContributor(module);
            var accumulator = new RuntimeHashAccumulator(contributor.HashSchemaId, contributor.HashSchemaVersion);
            RuntimeHashContext context = new RuntimeHashContext(RuntimeFrame.Zero);
            contributor.Contribute(context, accumulator);
            long expected = accumulator.ToHash();

            for (int i = 0; i < 8; i++)
            {
                accumulator.Reset();
                contributor.Contribute(context, accumulator);
                Assert.That(accumulator.ToHash(), Is.EqualTo(expected));
            }

            long positiveControlBefore = GC.GetAllocatedBytesForCurrentThread();
            byte[] positiveControl = new byte[32];
            positiveControl[0] = 1;
            long positiveControlAllocated = GC.GetAllocatedBytesForCurrentThread() - positiveControlBefore;
            Assert.That(positiveControlAllocated, Is.GreaterThan(0L));

            long before = GC.GetAllocatedBytesForCurrentThread();
            long observed = 0L;
            for (int i = 0; i < 128; i++)
            {
                accumulator.Reset();
                contributor.Contribute(context, accumulator);
                observed = accumulator.ToHash();
            }

            Assert.That(observed, Is.EqualTo(expected));
            Assert.That(GC.GetAllocatedBytesForCurrentThread() - before, Is.Zero);
            Assert.That(positiveControl[0], Is.EqualTo(1));
        }

        [Test]
        public void RuntimeHash_DistinguishesPrefabAddressesOneAndLeadingZeroOne()
        {
            VfxRuntimeModule leadingZeroModule = CreateAddressModule("01");
            VfxRuntimeModule oneModule = CreateAddressModule("1");

            var leadingZeroContributor = new VfxRuntimeHashContributor(leadingZeroModule);
            var oneContributor = new VfxRuntimeHashContributor(oneModule);
            var leadingZeroAccumulator = new RuntimeHashAccumulator(
                leadingZeroContributor.HashSchemaId,
                leadingZeroContributor.HashSchemaVersion);
            var oneAccumulator = new RuntimeHashAccumulator(
                oneContributor.HashSchemaId,
                oneContributor.HashSchemaVersion);

            Assert.That(
                ContributeHash(leadingZeroContributor, leadingZeroAccumulator),
                Is.Not.EqualTo(ContributeHash(oneContributor, oneAccumulator)));
        }

        [Test]
        public void RuntimeHash_CustomStopReasonNullAndEmptyFollowStateOwnerSemantics()
        {
            VfxRuntimeModule nullReasonModule = CreateModule(out _);
            VfxRuntimeModule emptyReasonModule = CreateModule(out _);
            RuntimeTraceIdentity trace = Trace(33);
            var nullReason = new VfxStopRequest(
                new RuntimeFrame(4),
                new VfxInstanceId(1),
                VfxStopMode.Dissipate,
                VfxStopReasonCode.Custom,
                0,
                null,
                trace);
            var emptyReason = new VfxStopRequest(
                new RuntimeFrame(4),
                new VfxInstanceId(1),
                VfxStopMode.Dissipate,
                VfxStopReasonCode.Custom,
                0,
                string.Empty,
                trace);

            Assert.That(nullReason.Reason, Is.EqualTo(string.Empty));
            Assert.That(nullReason, Is.EqualTo(emptyReason));
            nullReasonModule.EnqueueStopRequest(nullReason);
            emptyReasonModule.EnqueueStopRequest(emptyReason);

            var nullReasonContributor = new VfxRuntimeHashContributor(nullReasonModule);
            var emptyReasonContributor = new VfxRuntimeHashContributor(emptyReasonModule);
            var nullReasonAccumulator = new RuntimeHashAccumulator(
                nullReasonContributor.HashSchemaId,
                nullReasonContributor.HashSchemaVersion);
            var emptyReasonAccumulator = new RuntimeHashAccumulator(
                emptyReasonContributor.HashSchemaId,
                emptyReasonContributor.HashSchemaVersion);
            Assert.That(
                ContributeHash(nullReasonContributor, nullReasonAccumulator),
                Is.EqualTo(ContributeHash(emptyReasonContributor, emptyReasonAccumulator)));
        }

        [Test]
        public void SaveState_RestoresActiveInstancesAndPendingRequests()
        {
            VfxRuntimeModule module = CreateModule(out RuntimeCommandBuffer commands);
            commands.Enqueue(VfxRuntimeCommandFactory.Spawn(RuntimeFrame.Zero, 10, new VfxDefinitionId(1), traceId: Trace(18)));
            module.EnqueueSpawnRequest(new VfxSpawnRequest(
                new RuntimeFrame(5),
                VfxSourceDomains.Manual,
                5,
                0,
                0,
                1,
                1,
                2,
                new VfxDefinitionId(2),
                VfxAnchor.World(VfxPose.Identity),
                new VfxParameterBlock(9),
                0,
                Trace(19),
                playbackRatePermille: 750,
                sourceStartOffsetFrames: 2,
                sourceEndOffsetFrames: 1));
            module.Tick(new RuntimeTickContext(0, 0d, 0d, RuntimeTickStage.PostSimulation));

            var provider = new VfxRuntimeSaveStateProvider(module);
            RuntimeSaveState saveState = provider.CaptureSaveState().Value;
            Assert.That(
                saveState.ModuleStates.Single().CustomState.NumericTypeId,
                Is.EqualTo(VfxRuntimeSaveStateProvider.CustomStateNumericTypeId));

            VfxRuntimeModule restored = CreateModule(out _);
            var restorer = new VfxRuntimeSaveStateProvider(restored);
            Assert.IsTrue(restorer.RestoreSaveState(saveState).Success);

            long originalHash = RuntimeHashCombiner.ComputeHash(RuntimeFrame.Zero, new[] { new VfxRuntimeHashContributor(module) });
            long restoredHash = RuntimeHashCombiner.ComputeHash(RuntimeFrame.Zero, new[] { new VfxRuntimeHashContributor(restored) });
            Assert.AreEqual(originalHash, restoredHash);
            Assert.AreEqual(1, restored.ActiveCount);
            Assert.AreEqual(1, restored.PendingSpawnCount);
            var pending = new List<VfxSpawnRequest>();
            restored.CopyPendingSpawnRequests(pending);
            Assert.AreEqual(750, pending[0].PlaybackRatePermille);
            Assert.AreEqual(2, pending[0].SourceStartOffsetFrames);
            Assert.AreEqual(1, pending[0].SourceEndOffsetFrames);
        }

        [Test]
        public void SaveState_OldCustomStateSchemaVersionIsRejectedBeforeStateChange()
        {
            VfxRuntimeModule module = CreateModule(out RuntimeCommandBuffer commands);
            commands.Enqueue(VfxRuntimeCommandFactory.Spawn(RuntimeFrame.Zero, 10, new VfxDefinitionId(1), traceId: Trace(34)));
            module.Tick(new RuntimeTickContext(0, 0d, 0d, RuntimeTickStage.PostSimulation));

            var provider = new VfxRuntimeSaveStateProvider(module);
            RuntimeSaveState current = provider.CaptureSaveState().Value;
            RuntimeCustomState currentCustomState = current.ModuleStates.Single().CustomState;

            module.EnqueueSpawnRequest(new VfxSpawnRequest(
                new RuntimeFrame(5),
                VfxSourceDomains.Manual,
                5,
                0,
                0,
                1,
                1,
                2,
                new VfxDefinitionId(2),
                VfxAnchor.World(VfxPose.Identity),
                VfxParameterBlock.Empty,
                lifetimeFramesOverride: 0,
                traceId: Trace(35)));
            long hashBeforeRestore = ComputeHash(module);
            int pendingBeforeRestore = module.PendingSpawnCount;

            var oldCustomState = new RuntimeCustomState(
                VfxRuntimeSaveStateProvider.CustomStateNumericTypeId,
                VfxRuntimeSaveStateProvider.CustomStateTypeId,
                schemaVersion: 0,
                payloadJson: currentCustomState.PayloadJson);
            var oldSaveState = new RuntimeSaveState(
                current.SchemaVersion,
                current.CreatedAtUtc,
                current.FrameworkVersion,
                current.ConfigVersion,
                current.ResourceCatalogVersion,
                current.Frame,
                current.Entities,
                current.GlobalCounters,
                new[] { new RuntimeModuleSaveState(module.ModuleId, 0, oldCustomState) },
                current.Metadata);

            RuntimeSaveStateResult<bool> result = provider.RestoreSaveState(oldSaveState);

            Assert.That(result.Success, Is.False);
            Assert.That(result.Error.Code, Is.EqualTo(RuntimeSaveStateErrorCode.UnsupportedVersion));
            Assert.That(module.PendingSpawnCount, Is.EqualTo(pendingBeforeRestore));
            Assert.That(ComputeHash(module), Is.EqualTo(hashBeforeRestore));
        }

        [Test]
        public void SaveState_RestoresPendingRebuildAndStopMode()
        {
            VfxRuntimeModule module = CreateModule(out RuntimeCommandBuffer commands);
            commands.Enqueue(VfxRuntimeCommandFactory.Spawn(RuntimeFrame.Zero, 1, new VfxDefinitionId(1)));
            module.Tick(new RuntimeTickContext(0, 0d, 0d, RuntimeTickStage.PostSimulation));
            module.EnqueueRebuildRequest(new VfxRebuildRequest(new RuntimeFrame(5), new VfxInstanceId(1), Trace(20)));
            module.EnqueueStopRequest(new VfxStopRequest(new RuntimeFrame(6), new VfxInstanceId(1), VfxStopMode.Dissipate, "restore-stop", Trace(21)));

            RuntimeSaveState save = new VfxRuntimeSaveStateProvider(module).CaptureSaveState().Value;
            VfxRuntimeModule restored = CreateModule(out _);
            Assert.That(new VfxRuntimeSaveStateProvider(restored).RestoreSaveState(save).Success, Is.True);
            Assert.That(restored.PendingRebuildCount, Is.EqualTo(1));
            var stops = new List<VfxStopRequest>();
            restored.CopyPendingStopRequests(stops);
            Assert.That(stops.Single().StopMode, Is.EqualTo(VfxStopMode.Dissipate));
            Assert.That(
                RuntimeHashCombiner.ComputeHash(RuntimeFrame.Zero, new[] { new VfxRuntimeHashContributor(restored) }),
                Is.EqualTo(RuntimeHashCombiner.ComputeHash(RuntimeFrame.Zero, new[] { new VfxRuntimeHashContributor(module) })));
        }

        [Test]
        public void SaveState_RestoreRejectsDefinitionHashMismatch()
        {
            VfxRuntimeModule module = CreateModule(out RuntimeCommandBuffer commands);
            commands.Enqueue(VfxRuntimeCommandFactory.Spawn(RuntimeFrame.Zero, 10, new VfxDefinitionId(1), traceId: Trace(18)));
            module.Tick(new RuntimeTickContext(0, 0d, 0d, RuntimeTickStage.PostSimulation));

            RuntimeSaveState saveState = new VfxRuntimeSaveStateProvider(module).CaptureSaveState().Value;
            var restored = new VfxRuntimeModule(CreateDefinitions("hash-v2"), new RuntimeCommandBuffer());
            RuntimeSaveStateResult<bool> result = new VfxRuntimeSaveStateProvider(restored).RestoreSaveState(saveState);

            Assert.IsFalse(result.Success, result.Error.ToString());
            Assert.AreEqual(RuntimeSaveStateErrorCode.CustomStateMismatch, result.Error.Code);
        }

        [Test]
        public void GameplayFanout_AllowsRenderingAndVfxToConsumeSameDrainedEvents()
        {
            var gameplay = new GameplayRuntimeModule(new GameplayWorld(), new GameplayAbilityRegistry(), new RuntimeCommandBuffer(), componentWorld: new GameplayComponentWorld());
            VfxRuntimeModule vfx = CreateModule(out _);
            var resolver = new VfxTriggerMapResolver()
                .Add(new VfxTriggerMapping(VfxSourceDomains.Gameplay, GameplayVfxBridge.ComponentEntityCreatedTrigger, new VfxDefinitionId(1), 4));
            var vfxBridge = new GameplayVfxBridge(vfx, resolver);
            vfxBridge.Install();

            var registry = new MxRenderSubjectRegistry();
            var publisher = new RenderDataPublisher(registry);
            var renderingSink = new LifecyclePublishingGameplaySink(publisher, registry.CreateMap<GameplayEntityId>());

            var fanout = new GameplayRuntimeEventFanout(gameplay);
            fanout.AddSink(renderingSink);
            fanout.AddSink(vfxBridge);

            gameplay.ComponentWorld.EnqueueEvent(new GameplayRuntimeEvent(
                RuntimeFrame.Zero,
                GameplayRuntimeEventType.ComponentEntityCreated,
                commandId: 0,
                casterEntityId: 0,
                abilityId: 0,
                targetEntityId: 0,
                failureCode: GameplayAbilityRuntimeFailureCode.None,
                reason: string.Empty,
                traceId: Trace(23),
                componentEntityIndex: 3,
                componentEntityGeneration: 1));

            Assert.AreEqual(1, fanout.DrainFrame(RuntimeFrame.Zero));
            Assert.AreEqual(1, publisher.CaptureSnapshot().TotalEventCount);

            vfx.Tick(new RuntimeTickContext(0, 0d, 0d, RuntimeTickStage.PostSimulation));
            Assert.AreEqual(1, vfx.ActiveCount);
        }

        [Test]
        public void CombatBridge_MapsActionFrameAndHitPresentationToVfxRequests()
        {
            VfxRuntimeModule vfx = CreateModule(out _);
            int hitTrigger = CombatVfxBridge.GetHitPresentationTriggerId(CharacterTestCharacterActionConfigSource.FireballBurstPresentationProfileId);
            var resolver = new VfxTriggerMapResolver()
                .Add(new VfxTriggerMapping(VfxSourceDomains.CombatAction, 77, new VfxDefinitionId(1), 4))
                .Add(new VfxTriggerMapping(VfxSourceDomains.CombatHit, hitTrigger, new VfxDefinitionId(2), 4));
            var bridge = new CombatVfxBridge(vfx, resolver);
            bridge.Install();

            bridge.PublishActionFrameEvent(RuntimeFrame.Zero, new ActionFrameEventRaisedEvent(
                new CombatEntityId(5),
                actionId: 100,
                actionInstanceId: 3,
                worldFrame: new CombatFrame(12),
                localFrame: 4,
                frameEvent: new CombatActionFrameEvent(4, 77, sourceOrder: 2, intPayload: 9)));
            bridge.PublishHitPresentationEvent(RuntimeFrame.Zero, CreateHitPresentationEvent(CharacterTestCharacterActionConfigSource.FireballBurstPresentationProfileId));
            vfx.Tick(new RuntimeTickContext(0, 0d, 0d, RuntimeTickStage.PostSimulation));

            var active = new List<VfxInstanceSnapshot>();
            vfx.CopyActiveInstances(active);
            Assert.AreEqual(2, active.Count);
            Assert.AreEqual(VfxSourceDomains.CombatAction, active[0].SourceDomain);
            Assert.AreEqual(12, active[0].SourceFrame);
            Assert.AreEqual(4, active[0].LocalFrame);
            Assert.AreEqual(VfxSourceDomains.CombatHit, active[1].SourceDomain);
            Assert.AreEqual(17, active[1].Parameters.Value0);
            Assert.AreEqual(Fix64.One.RawValue, active[1].Anchor.Pose.Position.RawX);
            Assert.AreEqual(Fix64.FromInt(2).RawValue, active[1].Anchor.Pose.Position.RawY);
            Assert.AreEqual(Fix64.FromInt(3).RawValue, active[1].Anchor.Pose.Position.RawZ);
            Assert.AreEqual(Fix64.One.RawValue, active[1].Anchor.Pose.Forward.RawY);
        }

        [Test]
        public void CombatPhysicsAdapter_ProjectsFirstHitToDeterministicAnchor()
        {
            var world = new CombatPhysicsWorld();
            world.UpsertBody(new CombatPhysicsBody(new CombatEntityId(9), new CombatBodyId(1), FixVector3.Zero));
            world.UpsertAabbCollider(new CombatPhysicsAabbCollider(
                new CombatBodyId(1),
                new CombatColliderId(2),
                0,
                new FixVector3(Fix64.FromInt(-1), Fix64.FromInt(-1), Fix64.FromInt(-1)),
                new FixVector3(Fix64.One, Fix64.One, Fix64.One)));

            var header = new CombatQueryHeader(1, CombatQueryKind.Ray, new CombatEntityId(3), 4, 5, 6, CombatPhysicsLayerMask.All);
            var query = CombatPhysicsQuery.From(new CombatRayQuery(
                header,
                new FixVector3(Fix64.FromInt(-3), Fix64.Zero, Fix64.Zero),
                new FixVector3(Fix64.One, Fix64.Zero, Fix64.Zero),
                Fix64.FromInt(10)));
            var adapter = new CombatVfxPhysicsAdapter(world);

            Assert.IsTrue(adapter.TryProjectFirstHitAnchor(query, VfxAnchorKind.TargetEntity, socketId: 8, out VfxAnchor anchor, out CombatQueryResult hit));
            Assert.AreEqual(3, anchor.SourceId);
            Assert.AreEqual(9, anchor.TargetId);
            Assert.AreEqual(8, anchor.SocketId);
            Assert.AreEqual(hit.Point.X.RawValue, anchor.Pose.Position.RawX);
        }

        [Test]
        public void RenderingBridge_MapsVfxEventsToRenderData()
        {
            VfxRuntimeModule vfx = CreateModule(out RuntimeCommandBuffer commands);
            commands.Enqueue(VfxRuntimeCommandFactory.Spawn(RuntimeFrame.Zero, 1, new VfxDefinitionId(1), parameter0: 2500));
            vfx.Tick(new RuntimeTickContext(0, 0d, 0d, RuntimeTickStage.PostSimulation));

            var subjects = new MxRenderSubjectRegistry();
            var publisher = new RenderDataPublisher(subjects);
            var bridge = new VfxRenderingBridge(vfx, publisher, subjects.CreateMap<long>());
            bridge.Install();

            Assert.AreEqual(1, bridge.DrainFrame(RuntimeFrame.Zero));
            RenderDataPublisherSnapshot snapshot = publisher.CaptureSnapshot();
            Assert.AreEqual(1, snapshot.CurrentFrameCount(RenderDataEventKind.Impact));
            Assert.AreEqual(1, subjects.ActiveCount);
        }

        [Test]
        public void RenderingBridge_UninstallReleasesActiveSubjects()
        {
            VfxRuntimeModule vfx = CreateModule(out RuntimeCommandBuffer commands);
            commands.Enqueue(VfxRuntimeCommandFactory.Spawn(RuntimeFrame.Zero, 1, new VfxDefinitionId(1)));
            vfx.Tick(new RuntimeTickContext(0, 0d, 0d, RuntimeTickStage.PostSimulation));

            var subjects = new MxRenderSubjectRegistry();
            var publisher = new RenderDataPublisher(subjects);
            var bridge = new VfxRenderingBridge(vfx, publisher, subjects.CreateMap<long>());
            bridge.Install();
            bridge.DrainFrame(RuntimeFrame.Zero);

            Assert.AreEqual(1, subjects.ActiveCount);

            bridge.Uninstall();

            Assert.AreEqual(0, subjects.ActiveCount);
        }

        [Test]
        public void UnityPresenter_LoadsPlaysStopsAndReleasesPrefabResource()
        {
            GameObject prefab = new GameObject("vfx-prefab");
            prefab.AddComponent<ParticleSystem>();
            try
            {
                var provider = new MemoryResourceProvider().Register("prefab", prefab);
                var resources = new ResourceManager()
                    .RegisterProvider(provider)
                    .AddCatalog(new ResourceCatalog("test", string.Empty, new[]
                    {
                        new ResourceCatalogEntry(TestPrefabKey.Id, TestPrefabKey.TypeId, MemoryResourceProvider.Id, "prefab")
                    }));
                VfxRuntimeModule vfx = CreateModule(out RuntimeCommandBuffer commands);
                var presenter = new UnityVfxPresenter(vfx, resources, usePooling: false);

                commands.Enqueue(VfxRuntimeCommandFactory.Spawn(RuntimeFrame.Zero, 1, new VfxDefinitionId(1)));
                vfx.Tick(new RuntimeTickContext(0, 0d, 0d, RuntimeTickStage.PostSimulation));
                Assert.AreEqual(1, presenter.DrainFrame(RuntimeFrame.Zero));
                Assert.AreEqual(1, presenter.ActiveCount);
                Assert.AreEqual(1, provider.LoadCount);

                vfx.Tick(new RuntimeTickContext(2, 0d, 0d, RuntimeTickStage.PostSimulation));
                Assert.AreEqual(1, presenter.DrainFrame(new RuntimeFrame(2)));
                Assert.AreEqual(0, presenter.ActiveCount);
                Assert.AreEqual(0, provider.ReleaseCount);
                presenter.Dispose();
                Assert.AreEqual(1, provider.ReleaseCount);
            }
            finally
            {
                Object.DestroyImmediate(prefab);
            }
        }

        [Test]
        public void UnityPresenter_AppliesInstancePlaybackRateWithoutCompoundingPooledDefaults()
        {
            GameObject prefab = new GameObject("vfx-prefab-rate");
            ParticleSystem prefabParticle = prefab.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule prefabMain = prefabParticle.main;
            prefabMain.simulationSpeed = 1.5f;
            try
            {
                var provider = new MemoryResourceProvider().Register("prefab", prefab);
                var resources = new ResourceManager()
                    .RegisterProvider(provider)
                    .AddCatalog(new ResourceCatalog("test", string.Empty, new[]
                    {
                        new ResourceCatalogEntry(TestPrefabKey.Id, TestPrefabKey.TypeId, MemoryResourceProvider.Id, "prefab")
                    }));
                VfxRuntimeModule vfx = CreateModule(out _);
                var presenter = new UnityVfxPresenter(vfx, resources, usePooling: true);
                presenter.PrewarmDefinitions(1);

                vfx.EnqueueSpawnRequest(new VfxSpawnRequest(
                    RuntimeFrame.Zero,
                    VfxSourceDomains.ActionPresentation,
                    0,
                    0,
                    0,
                    1,
                    1,
                    0,
                    new VfxDefinitionId(1),
                    VfxAnchor.World(VfxPose.Identity),
                    VfxParameterBlock.Empty,
                    1,
                    Trace(40),
                    playbackRatePermille: 500));
                vfx.Tick(new RuntimeTickContext(0, 0d, 0d, RuntimeTickStage.PostSimulation));
                presenter.DrainFrame(RuntimeFrame.Zero);
                GameObject firstView = GameObject.Find("VFX_1_1");
                Assert.That(firstView.GetComponent<ParticleSystem>().main.simulationSpeed, Is.EqualTo(0.75f).Within(0.0001f));

                vfx.Tick(new RuntimeTickContext(1, 0d, 0d, RuntimeTickStage.PostSimulation));
                presenter.DrainFrame(new RuntimeFrame(1));
                vfx.EnqueueSpawnRequest(new VfxSpawnRequest(
                    new RuntimeFrame(2),
                    VfxSourceDomains.ActionPresentation,
                    2,
                    0,
                    0,
                    2,
                    1,
                    0,
                    new VfxDefinitionId(1),
                    VfxAnchor.World(VfxPose.Identity),
                    VfxParameterBlock.Empty,
                    1,
                    Trace(41),
                    playbackRatePermille: 2000));
                vfx.Tick(new RuntimeTickContext(2, 0d, 0d, RuntimeTickStage.PostSimulation));
                presenter.DrainFrame(new RuntimeFrame(2));
                GameObject secondView = GameObject.Find("VFX_1_2");
                Assert.That(secondView.GetComponent<ParticleSystem>().main.simulationSpeed, Is.EqualTo(3f).Within(0.0001f));
                presenter.Dispose();
            }
            finally
            {
                Object.DestroyImmediate(prefab);
            }
        }

        [Test]
        public void UnityPresenter_RestoreReconcilesLiveViewsAndReusedIds()
        {
            GameObject prefab = new GameObject("restore-vfx-prefab");
            try
            {
                var provider = new MemoryResourceProvider().Register("prefab", prefab);
                var resources = new ResourceManager().RegisterProvider(provider).AddCatalog(
                    new ResourceCatalog("restore", string.Empty, new[] {
                        new ResourceCatalogEntry(TestPrefabKey.Id, TestPrefabKey.TypeId, MemoryResourceProvider.Id, "prefab") }));
                VfxRuntimeModule vfx = CreateModule(out RuntimeCommandBuffer commands);
                using var presenter = new UnityVfxPresenter(vfx, resources, usePooling: true);
                presenter.PrewarmDefinitions(1);
                var saveProvider = new VfxRuntimeSaveStateProvider(vfx);
                RuntimeSaveState empty = saveProvider.CaptureSaveState().Value;
                commands.Enqueue(VfxRuntimeCommandFactory.Spawn(RuntimeFrame.Zero, 1, new VfxDefinitionId(1)));
                vfx.Tick(new RuntimeTickContext(0, 0d, 0d, RuntimeTickStage.PostSimulation));
                presenter.DrainFrame(RuntimeFrame.Zero);
                RuntimeSaveState active = saveProvider.CaptureSaveState().Value;
                Assert.That(presenter.ActiveCount, Is.EqualTo(1));
                for (int i = 0; i < 10; i++)
                {
                    Assert.That(saveProvider.RestoreSaveState(empty).Success, Is.True);
                    presenter.SynchronizeActiveInstances();
                    Assert.That(presenter.ActiveCount, Is.Zero);
                    Assert.That(GameObject.Find("VFX_1_1"), Is.Null);
                    Assert.That(saveProvider.RestoreSaveState(active).Success, Is.True);
                    presenter.SynchronizeActiveInstances();
                    Assert.That(presenter.ActiveCount, Is.EqualTo(1));
                    Assert.That(GameObject.Find("VFX_1_1"), Is.Not.Null);
                    presenter.SynchronizeActiveInstances();
                    Assert.That(presenter.ActiveCount, Is.EqualTo(1));
                }
            }
            finally { Object.DestroyImmediate(prefab); }
        }

        [Test]
        public void UnityPresenter_MissingPrefabResource_ReportsActionableConsoleError()
        {
            var resources = new ResourceManager();
            VfxRuntimeModule vfx = CreateModule(out RuntimeCommandBuffer commands);
            var presenter = new UnityVfxPresenter(vfx, resources, usePooling: false);

            commands.Enqueue(VfxRuntimeCommandFactory.Spawn(RuntimeFrame.Zero, 1, new VfxDefinitionId(1)));
            vfx.Tick(new RuntimeTickContext(0, 0d, 0d, RuntimeTickStage.PostSimulation));

            LogAssert.Expect(
                LogType.Error,
                new Regex(@"\[MxFramework VFX\] Prefab load failed; spawn instance=1 definition=1; resourceKey=vfx\.test\.impact:GameObject"));
            Assert.AreEqual(1, presenter.DrainFrame(RuntimeFrame.Zero));
            Assert.That(presenter.Diagnostics, Has.Some.Contains("resourceKey=vfx.test.impact:GameObject"));

            presenter.Dispose();
        }

        [Test]
        public void PublishedProductionVfx_ResolvesThroughGlobalEditorResourceRuntime()
        {
            using GlobalResourceRuntimeServices services = new GlobalEditorResourceRuntimeFactory().Create(
                new GlobalResourceRuntimeOptions { Mode = GlobalResourceRuntimeMode.Editor });
            var key = new ResourceKey(
                "vfx.authored.sample.fireball_impact",
                ResourceTypeIds.GameObject,
                packageId: "mxframework.samples");

            ResourceLoadResult<ResourceHandle<GameObject>> loaded = services.ResourceManager.Load<GameObject>(key);

            Assert.IsTrue(loaded.Success, loaded.Error.ToString());
            Assert.NotNull(loaded.Value);
            Assert.NotNull(loaded.Value.Value);
            services.ResourceManager.Release(loaded.Value);
        }

        [Test]
        public void UnityPresenter_SyncActiveAnchors_UpdatesTransformFromResolverAndFallsBackToBakedPose()
        {
            GameObject prefab = new GameObject("vfx-prefab-anchor");
            prefab.AddComponent<ParticleSystem>();
            try
            {
                var provider = new MemoryResourceProvider().Register("prefab", prefab);
                var resources = new ResourceManager()
                    .RegisterProvider(provider)
                    .AddCatalog(new ResourceCatalog("test", string.Empty, new[]
                    {
                        new ResourceCatalogEntry(TestPrefabKey.Id, TestPrefabKey.TypeId, MemoryResourceProvider.Id, "prefab")
                    }));
                VfxRuntimeModule vfx = CreateModule(out _);
                var resolver = new TestUnityVfxAnchorResolver();
                var presenter = new UnityVfxPresenter(
                    vfx,
                    resources,
                    usePooling: false,
                    anchorResolver: resolver);

                resolver.Set(new Vector3(1f, 2f, 3f), Vector3.right);
                vfx.EnqueueSpawnRequest(new VfxSpawnRequest(
                    RuntimeFrame.Zero,
                    VfxSourceDomains.ActionPresentation,
                    sourceFrame: 0,
                    localFrame: 0,
                    sourceOrder: 0,
                    triggerId: 1,
                    sourceId: 7,
                    targetId: 0,
                    definitionId: new VfxDefinitionId(1),
                    anchor: VfxAnchor.Source(7, CreatePose(9f, 8f, 7f, 0f, 0f, 1f)),
                    parameters: VfxParameterBlock.Empty,
                    lifetimeFramesOverride: 5,
                    traceId: Trace(22)));
                vfx.Tick(new RuntimeTickContext(0, 0d, 0d, RuntimeTickStage.PostSimulation));

                Assert.AreEqual(1, presenter.DrainFrame(RuntimeFrame.Zero));
                GameObject view = GameObject.Find("VFX_1_1");
                Assert.NotNull(view);
                AssertVector(new Vector3(1f, 2f, 3f), view.transform.position);
                AssertVector(Vector3.right, view.transform.forward);

                resolver.Set(new Vector3(4f, 5f, 6f), Vector3.back);
                presenter.SyncActiveAnchors();
                AssertVector(new Vector3(4f, 5f, 6f), view.transform.position);
                AssertVector(Vector3.back, view.transform.forward);

                resolver.Enabled = false;
                Assert.DoesNotThrow(() => presenter.SyncActiveAnchors());
                AssertVector(new Vector3(9f, 8f, 7f), view.transform.position);
                AssertVector(Vector3.forward, view.transform.forward);

                presenter.Dispose();
            }
            finally
            {
                Object.DestroyImmediate(prefab);
            }
        }

        [Test]
        public void UnityPresenter_PoolingRetainsResourceHandleUntilDisposed()
        {
            GameObject prefab = new GameObject("vfx-prefab-pool");
            prefab.AddComponent<ParticleSystem>();
            try
            {
                var provider = new MemoryResourceProvider().Register("prefab", prefab);
                var resources = new ResourceManager()
                    .RegisterProvider(provider)
                    .AddCatalog(new ResourceCatalog("test", string.Empty, new[]
                    {
                        new ResourceCatalogEntry(TestPrefabKey.Id, TestPrefabKey.TypeId, MemoryResourceProvider.Id, "prefab")
                    }));
                VfxRuntimeModule vfx = CreateModule(out RuntimeCommandBuffer commands);
                var presenter = new UnityVfxPresenter(vfx, resources, usePooling: true);
                presenter.PrewarmDefinitions(poolSizePerDefinition: 1);
                Assert.AreEqual(1, provider.LoadCount);

                commands.Enqueue(VfxRuntimeCommandFactory.Spawn(RuntimeFrame.Zero, 1, new VfxDefinitionId(1)));
                vfx.Tick(new RuntimeTickContext(0, 0d, 0d, RuntimeTickStage.PostSimulation));
                Assert.AreEqual(1, presenter.DrainFrame(RuntimeFrame.Zero));

                vfx.Tick(new RuntimeTickContext(2, 0d, 0d, RuntimeTickStage.PostSimulation));
                Assert.AreEqual(1, presenter.DrainFrame(new RuntimeFrame(2)));
                Assert.AreEqual(1, provider.LoadCount);
                Assert.AreEqual(0, provider.ReleaseCount);

                commands.Enqueue(VfxRuntimeCommandFactory.Spawn(new RuntimeFrame(3), 1, new VfxDefinitionId(1)));
                vfx.Tick(new RuntimeTickContext(3, 0d, 0d, RuntimeTickStage.PostSimulation));
                Assert.AreEqual(1, presenter.DrainFrame(new RuntimeFrame(3)));
                Assert.AreEqual(1, presenter.ActiveCount);
                Assert.AreEqual(1, provider.LoadCount);

                presenter.Dispose();
                Assert.AreEqual(1, provider.ReleaseCount);
            }
            finally
            {
                Object.DestroyImmediate(prefab);
            }
        }

        [Test]
        public void UnityPresenter_OptionalBackendReinitializesStopsAndSuppressesParticleFallback()
        {
            GameObject prefab = new GameObject("vfx-prefab-backend");
            prefab.AddComponent<ParticleSystem>();
            TestUnityVfxBackendController templateController = prefab.AddComponent<TestUnityVfxBackendController>();
            templateController.Available = true;
            templateController.SuppressFallback = true;
            try
            {
                var provider = new MemoryResourceProvider().Register("prefab", prefab);
                var resources = new ResourceManager()
                    .RegisterProvider(provider)
                    .AddCatalog(new ResourceCatalog("test", string.Empty, new[]
                    {
                        new ResourceCatalogEntry(TestPrefabKey.Id, TestPrefabKey.TypeId, MemoryResourceProvider.Id, "prefab")
                    }));
                VfxRuntimeModule vfx = CreateModule(out RuntimeCommandBuffer commands);
                var presenter = new UnityVfxPresenter(vfx, resources, usePooling: true);
                presenter.PrewarmDefinitions(1);

                commands.Enqueue(VfxRuntimeCommandFactory.Spawn(RuntimeFrame.Zero, 1, new VfxDefinitionId(1), parameter0: 7));
                vfx.Tick(new RuntimeTickContext(0, 0d, 0d, RuntimeTickStage.PostSimulation));
                presenter.DrainFrame(RuntimeFrame.Zero);
                GameObject view = GameObject.Find("VFX_1_1");
                TestUnityVfxBackendController controller = view.GetComponent<TestUnityVfxBackendController>();
                Assert.AreEqual(1, controller.PlayCount);
                Assert.AreEqual(7, controller.LastValue0);
                Assert.IsTrue(view.GetComponent<ParticleSystem>().isStopped);

                vfx.Tick(new RuntimeTickContext(2, 0d, 0d, RuntimeTickStage.PostSimulation));
                presenter.DrainFrame(new RuntimeFrame(2));
                Assert.AreEqual(1, controller.StopCount);
                presenter.Dispose();
            }
            finally
            {
                Object.DestroyImmediate(prefab);
            }
        }

        [Test]
        public void AsmdefDependencies_KeepVfxCoreNoEngineAndBridgesOptional()
        {
            string vfx = File.ReadAllText("Packages/com.vincent.wgameframework/Vfx/MxFramework.Vfx.asmdef");
            string gameplayBridge = File.ReadAllText("Packages/com.vincent.wgameframework/Vfx.GameplayBridge/MxFramework.Vfx.GameplayBridge.asmdef");
            string combatBridge = File.ReadAllText("Packages/com.vincent.wgameframework/Vfx.CombatBridge/MxFramework.Vfx.CombatBridge.asmdef");
            string renderingBridge = File.ReadAllText("Packages/com.vincent.wgameframework/Vfx.RenderingBridge/MxFramework.Vfx.RenderingBridge.asmdef");
            string graphAdapter = File.ReadAllText("Packages/com.vincent.wgameframework/Vfx.Unity.VfxGraph/MxFramework.Vfx.Unity.VfxGraph.asmdef");

            StringAssert.Contains("\"noEngineReferences\": true", vfx);
            StringAssert.DoesNotContain("MxFramework.Gameplay", vfx);
            StringAssert.DoesNotContain("MxFramework.Combat", vfx);
            StringAssert.DoesNotContain("MxFramework.Rendering", vfx);
            StringAssert.Contains("MxFramework.Gameplay", gameplayBridge);
            StringAssert.Contains("Unity.VisualEffectGraph.Runtime", graphAdapter);
            StringAssert.Contains("MXFRAMEWORK_VFX_GRAPH", graphAdapter);
            StringAssert.Contains("MxFramework.Combat", combatBridge);
            StringAssert.Contains("MxFramework.Rendering", renderingBridge);
        }

        [Test]
        public void VfxGraphImpactSample_KeepsGraphControllerAndParticleFallback()
        {
            const string prefabPath = "Assets/VFX/MxFramework/Samples/Fireball/Prefabs/Fireball_BurstImpact.prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

            Assert.IsNotNull(prefab);
            Assert.IsNotNull(prefab.transform.Find("VFXGraph_ImpactBurst"));
            Assert.IsTrue(ContainsComponent(prefab, "UnityEngine.VFX.VisualEffect"));
            Assert.IsTrue(ContainsComponent(prefab, "MxFramework.Vfx.Unity.VfxGraph.UnityVfxGraphController"));
            Assert.Greater(prefab.GetComponentsInChildren<ParticleSystem>(true).Length, 0);
        }

        [Test]
        public void VfxResourceLibrary_BuildsUiToolkitWorkspace()
        {
            var window = ScriptableObject.CreateInstance<VfxAuthoringResourceLibraryWindow>();
            try
            {
                window.CreateGUI();
                Assert.IsNotNull(window.rootVisualElement.Q<Button>("library-mode"));
                Assert.IsNotNull(window.rootVisualElement.Q<Button>("creative-mode"));
                Assert.IsNotNull(window.rootVisualElement.Q("library-workspace"));
                Assert.IsNotNull(window.rootVisualElement.Q("creative-workspace"));
                Assert.IsNotNull(window.rootVisualElement.Q<ListView>("effect-grid"));
                Assert.IsNotNull(window.rootVisualElement.Q("preview-host"));
                Assert.IsNotNull(window.rootVisualElement.Q("tab-content"));
                Assert.IsNotNull(window.rootVisualElement.Q<Button>("apply-change"));
                Assert.IsNotNull(window.rootVisualElement.Q<Button>("stop"));
                Assert.AreEqual("播放中", window.rootVisualElement.Q<Label>("playback-status").text);
                Assert.IsTrue(window.rootVisualElement.Q<Button>("play").ClassListContains("playback-active"));
                Assert.AreEqual(5, window.rootVisualElement.Query<Button>(className: "vfx-tab").ToList().Count);
                Assert.AreEqual(1, window.rootVisualElement.Query<Button>(className: "vfx-tab").ToList().Count(value => value.ClassListContains("active")));
                Assert.IsNotNull(window.rootVisualElement.Q<TextField>("brief-name"));
                Assert.IsNotNull(window.rootVisualElement.Q<TextField>("brief-intent"));
                Assert.IsNotNull(window.rootVisualElement.Q("candidate-host"));
                Assert.AreEqual(0, window.rootVisualElement.Query<Button>(className: "candidate-card").ToList().Count);
                Assert.AreEqual(0, window.rootVisualElement.Query<VisualElement>(className: "candidate-preview").ToList().Count);
                Assert.IsNotNull(window.rootVisualElement.Q<Button>("reference-pack-connect"));
                Assert.IsNotNull(window.rootVisualElement.Q<Label>("reference-pack-status"));
                Assert.IsNotNull(window.rootVisualElement.Q("reuse-gap-host"));
                Assert.IsNotNull(window.rootVisualElement.Q<Button>("creative-generate"));
                Assert.AreEqual("创建 Agent 任务", window.rootVisualElement.Q<Button>("creative-generate").text);
                Assert.IsNotNull(window.rootVisualElement.Q<Button>("creative-copy-agent"));
                Assert.IsNotNull(window.rootVisualElement.Q<Button>("creative-refresh-agent"));
                Assert.IsNotNull(window.rootVisualElement.Q<Button>("creative-cancel"));
                Assert.IsNotNull(window.rootVisualElement.Q<Button>("creative-quality-evidence"));
                Assert.IsNotNull(window.rootVisualElement.Q<Button>("creative-clean"));
                Assert.IsNotNull(window.rootVisualElement.Q<Button>("creative-promote"));
                Assert.IsNotNull(window.rootVisualElement.Q<Toggle>("creative-human-played"));
                Assert.IsNull(window.rootVisualElement.Q<IMGUIContainer>("creative-agent-live-preview"));
                Assert.IsTrue(window.rootVisualElement.Q<Button>("compare-play").enabledInHierarchy);
                Assert.IsTrue(window.rootVisualElement.Q<Toggle>("compare-loop").enabledInHierarchy);
                Assert.IsTrue(window.rootVisualElement.Q<Slider>("compare-speed").enabledInHierarchy);
                Assert.IsFalse(window.rootVisualElement.Q<Toggle>("require-fallback").value);

                window.CreateGUI();
                Assert.AreEqual(1, window.rootVisualElement.Query<VisualElement>(className: "vfx-window").ToList().Count);
                Assert.AreEqual(1, window.rootVisualElement.Query<IMGUIContainer>("preview-canvas").ToList().Count);
                Assert.AreEqual(0, window.rootVisualElement.Query<Button>(className: "candidate-card").ToList().Count);
                Assert.AreEqual(0, window.rootVisualElement.Query<VisualElement>(className: "candidate-preview").ToList().Count);
            }
            finally
            {
                Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void VfxResourceLibrary_ToggleFocusShortcutUsesSingleVKey()
        {
            const string shortcutId = "MxFramework/VFX Authoring/Toggle Focus";
            CollectionAssert.Contains(
                ShortcutManager.instance.GetAvailableShortcutIds(),
                shortcutId);

            MethodInfo shortcutMethod = typeof(VfxAuthoringResourceLibraryWindow).GetMethod(
                "ToggleFocusShortcut",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(shortcutMethod);

            bool foundDefaultV = false;
            IList<CustomAttributeData> attributes = shortcutMethod.GetCustomAttributesData();
            for (int i = 0; i < attributes.Count; i++)
            {
                CustomAttributeData attribute = attributes[i];
                if (attribute.AttributeType != typeof(ShortcutAttribute))
                    continue;

                IList<CustomAttributeTypedArgument> arguments = attribute.ConstructorArguments;
                for (int j = 0; j < arguments.Count; j++)
                {
                    CustomAttributeTypedArgument argument = arguments[j];
                    if (argument.ArgumentType == typeof(KeyCode)
                        && Convert.ToInt32(argument.Value) == (int)KeyCode.V)
                    {
                        foundDefaultV = true;
                    }
                }
            }

            Assert.IsTrue(foundDefaultV);
        }

        [Test]
        public void EditorWindowShortcutTray_SecondItemSitsBesideFirstItem()
        {
            var bottomRight = new Vector2(1180f, 880f);
            var collapsedSize = new Vector2(180f, 28f);
            Rect first = EditorWindowShortcutTray.BuildPosition(bottomRight, 0f, collapsedSize);
            Rect second = EditorWindowShortcutTray.BuildPosition(bottomRight, 184f, collapsedSize);

            Assert.AreEqual(first.xMin - 4f, second.xMax);
            Assert.AreEqual(first.yMin, second.yMin);
        }

        [Test]
        public void VfxResourceLibraryWorkspace_LoadsRecipePrimitiveAndGenericReuseEvidence()
        {
            using VfxLibraryWorkspace workspace = VfxLibraryWorkspace.Load(
                VfxAuthoringResourceLibraryWindow.CatalogPath,
                VfxAuthoringResourceLibraryWindow.ThumbnailRoot);

            Assert.AreEqual(4, workspace.RecipeCount);
            Assert.AreEqual(12, workspace.PrimitiveCount);
            Assert.AreEqual(39, workspace.LegacyPlaceholderCount);
            Assert.AreEqual(0, workspace.ProductionEffectCount);
            Assert.AreEqual(4, workspace.Filter(VfxLibraryItemKind.Recipe, string.Empty, "All", "All", "All", "All", false).Count);
            Assert.AreEqual(12, workspace.Filter(VfxLibraryItemKind.Primitive, string.Empty, "All", "All", "All", "All", false).Count);
            Assert.IsTrue(workspace.RecipeItems.Any(item => item.ConsumerEffectIds.Count > 0), "至少一个 Recipe 应有 Effect consumer。");
            Assert.IsTrue(workspace.PrimitiveItems.Any(item => item.ConsumerRecipeIds.Count > 0), "至少一个 Primitive 应有 Recipe consumer。");
            Assert.IsTrue(workspace.PrimitiveItems.Any(item => item.ConsumerEffectIds.Count > 0), "至少一个 Primitive 应有 Effect consumer。");
            Assert.IsNotEmpty(workspace.ReuseGaps, "应保留复用缺口队列。");
            Assert.IsNotEmpty(workspace.FilterSpecial("gaps", string.Empty, "All", "All", "All", "All"), "特殊筛选应显示复用缺口。");

            List<VfxLibraryReuseRecommendation> recommendations = workspace.FindReuseRecommendations("fire charge flight impact");
            Assert.IsNotEmpty(recommendations);
            bool foundFireballEvidence = false;
            for (int i = 0; i < recommendations.Count; i++)
            {
                if (recommendations[i].Item.StableId.Contains("fire"))
                {
                    foundFireballEvidence = true;
                    break;
                }
            }
            Assert.IsTrue(foundFireballEvidence, "通用创作简报必须能按主题与职责检索现有合同证据。");
        }

        [Test]
        public void VfxResourceLibraryWorkspace_IndexesExemplarsAndReverseConsumers()
        {
            const string catalogPath = "Temp/MxFramework/VfxAuthoring/Tests/exemplar_catalog.json";
            Directory.CreateDirectory(Path.GetDirectoryName(catalogPath));
            File.WriteAllText(catalogPath,
                "{\"schemaVersion\":\"mx.vfx.authoring-catalog.v2\",\"strictCoverage\":false," +
                "\"primitives\":[{\"primitiveId\":\"vfx.primitive.test.spark\",\"displayName\":\"Spark\",\"backend\":\"ParticleSystem\",\"reviewStatus\":\"Ready\",\"exemplar\":true,\"roles\":[\"Impact\"],\"compatibleAnchors\":[\"HitPoint\"]}]," +
                "\"recipes\":[{\"recipeId\":\"vfx.recipe.test.impact\",\"displayName\":\"Impact\",\"reviewStatus\":\"Ready\",\"exemplar\":true,\"layers\":[{\"layerId\":\"spark\",\"primitiveId\":\"vfx.primitive.test.spark\",\"order\":0}]}]," +
                "\"effects\":[{\"effectId\":\"vfx.test.impact\",\"displayName\":\"Impact\",\"backend\":\"ParticleSystem\",\"reviewStatus\":\"Ready\",\"exemplar\":true,\"recipeId\":\"vfx.recipe.test.impact\",\"roles\":[\"Impact\"],\"themeIds\":[\"fire\"]}]}" );
            try
            {
                using VfxLibraryWorkspace workspace = VfxLibraryWorkspace.Load(catalogPath, "Temp/MxFramework/VfxAuthoring/Tests/no_thumbnails");
                Assert.AreEqual(3, workspace.ExemplarCount);
                Assert.AreEqual(1, workspace.RecipeItems[0].ConsumerEffectIds.Count);
                Assert.AreEqual(1, workspace.PrimitiveItems[0].ConsumerRecipeIds.Count);
                Assert.AreEqual(1, workspace.PrimitiveItems[0].ConsumerEffectIds.Count);
                Assert.AreEqual(3, workspace.FilterSpecial("exemplars", string.Empty, "All", "All", "All", "All").Count);
            }
            finally
            {
                if (File.Exists(catalogPath)) File.Delete(catalogPath);
            }
        }

        [Test]
        public void VfxCreativePlanBinding_FiltersLifecycleAndPrimitiveExplorationAndKeepsCoreTrace()
        {
            CollectionAssert.AreEqual(new[] { "charge", "flight", "impact" }, VfxLibraryTempCreationPlanService.SelectStageIds("完整生命周期：蓄力 → 飞行 → 命中"));
            CollectionAssert.AreEqual(new[] { "impact" }, VfxLibraryTempCreationPlanService.SelectStageIds("仅命中"));

            var brief = new VfxLibraryCreationBrief
            {
                Lifecycle = "仅命中",
                ExploreNewPrimitive = true,
                StyleProfileId = "mx.vfx.style.fire-readable",
                StyleProfileVersion = "1",
                LicenseId = "project-owned"
            };
            var authoritative = new VfxLibraryAuthoritativePlanBundle
            {
                CatalogSemanticHash = "sha256:catalog",
                TemplateCatalogSemanticHash = "sha256:templates"
            };
            var core = new VfxCoreGenerationPlan
            {
                PlanId = "vfx.plan.fireball.impact.r0",
                SessionId = "vfx-brief-fireball-impact-abc123",
                PlanHash = "sha256:plan",
                QualityProfileId = "windows.d3d12.1080p60",
                QualityProfileVersion = "1"
            };
            string[] variants = { "ConservativeReuse", "HighReadability", "LowBudget", "StrongImpact", "ThemeVariant", "NewPrimitiveExploration" };
            for (int i = 0; i < variants.Length; i++)
                core.Candidates.Add(new VfxCoreGenerationCandidateSpec
                {
                    CandidateId = "candidate-" + i,
                    VariantKind = variants[i],
                    Seed = 17 + i,
                    ProviderId = "mx.vfx.provider.procedural",
                    ProviderVersion = "1.0.0",
                    TemplateId = "mx.vfx.template.particle.fireball",
                    TemplateVersion = "1",
                    OutputRoot = VfxCandidateProtocol.ResolvedSandboxRoot + "/" + core.SessionId + "/candidate-" + i
                });
            authoritative.StagePlans.Add(new VfxLibraryAuthoritativeStagePlan { StageId = "impact", GenerationPlan = core });

            List<VfxCandidatePlan> plans = VfxLibraryTempCreationPlanService.BuildBoundCandidatePlans(brief, authoritative, "test", "raw-catalog", "raw-templates", "editor-brief");
            Assert.AreEqual(6, plans.Count);
            Assert.IsTrue(plans.TrueForAll(plan => plan.PlanId.Contains(".impact.")));
            Assert.AreEqual("candidate-0", plans[0].CoreCandidateId);
            Assert.AreEqual(17u, plans[0].FixedSeed);
            Assert.AreEqual(core.SessionId, plans[0].SessionId);
            Assert.IsTrue(plans[0].CandidateOutputRoot.EndsWith("/" + core.SessionId + "/candidate-0"));
            Assert.IsTrue(plans[0].Provenance.Exists(entry => entry.Key == "corePlanHash" && entry.Value == "sha256:plan"));

            brief.ExploreNewPrimitive = false;
            plans = VfxLibraryTempCreationPlanService.BuildBoundCandidatePlans(brief, authoritative, "test-no-explore", "raw-catalog", "raw-templates", "editor-brief");
            Assert.AreEqual(5, plans.Count);
            Assert.IsFalse(plans.Exists(plan => plan.PlanId.EndsWith(".explore")));
        }

        [Test]
        public void VfxCreativeQualityContexts_KeepStageLifecycleAndPathContractsExplicit()
        {
            var flight = new VfxLibraryAuthoritativeStagePlan
            {
                StageId = "flight",
                GenerationPlan = new VfxCoreGenerationPlan()
            };
            flight.GenerationPlan.Candidates.Add(new VfxCoreGenerationCandidateSpec { CandidateId = "flight-candidate" });

            List<VfxQualityCandidateContext> contexts = VfxLibraryAuthoringStudioController.BuildQualityContexts(flight);

            Assert.AreEqual(1, contexts.Count);
            Assert.AreEqual("flight-candidate", contexts[0].CandidateId);
            Assert.AreEqual("Loop", contexts[0].LifecycleMode);
            Assert.IsTrue(contexts[0].RequiresPathMeasurement);
            Assert.IsTrue(contexts[0].IncludePathView);
            Assert.IsFalse(contexts[0].IncludeTopView);
            Assert.Greater(contexts[0].DurationSeconds, 0f);
            Assert.Greater(contexts[0].TargetScaleMeters, 0f);
        }

        [Test]
        public void VfxGenerationTemplateCatalog_LoadsStableIdsVersionsAndContentHash()
        {
            VfxLibraryGenerationTemplateCatalog catalog = VfxLibraryGenerationTemplateCatalog.Load();

            Assert.IsTrue(catalog.IsLoaded, catalog.Error);
            Assert.IsNotEmpty(catalog.ContentHash);
            Assert.AreEqual("1", catalog.CatalogVersion);
            Assert.AreEqual("mx.vfx.provider.development-agent", catalog.Providers[0].ProviderId);
            Assert.IsNotEmpty(catalog.Providers[0].ProviderVersion);
            Assert.AreEqual("mx.vfx.style.fire-readable", catalog.StyleProfiles[0].StyleProfileId);
            Assert.AreEqual("mx.vfx.template.particle.fireball", catalog.Templates[0].TemplateId);
            Assert.AreEqual("windows.d3d12.1080p60", catalog.QualityProfiles[0].QualityProfileId);
            CollectionAssert.AreEqual(new[] { 17, 73, 211 }, catalog.QualityProfiles[0].FixedSeeds);
        }

        [Test]
        public void VfxAuthoritativePlanService_FailsClosedBeforeCliWhenRawCatalogHashIsStale()
        {
            VfxLibraryGenerationTemplateCatalog catalog = VfxLibraryGenerationTemplateCatalog.Load();
            var brief = new VfxLibraryCreationBrief
            {
                Lifecycle = "仅命中",
                ProviderId = "mx.vfx.provider.procedural",
                ProviderVersion = "1.0.0",
                ProviderModelId = "mx.procedural.vfx",
                ProviderModelVersion = "1",
                TemplateId = "mx.vfx.template.particle.fireball",
                TemplateVersion = "1",
                TemplateKind = "ParticleSystemPrimitive",
                QualityProfileId = "windows.d3d12.1080p60",
                QualityProfileVersion = "1"
            };

            bool success = new VfxLibraryCliAuthoritativePlanService().TryCreatePlans(
                brief,
                "stale-content-hash",
                catalog,
                new List<VfxLibraryReuseRecommendation>(),
                out _,
                out string message);

            Assert.IsFalse(success);
            StringAssert.Contains("刷新", message);
        }

        [Test]
        public void VfxLibraryCoreReceiptExporter_ExportsCompleteStageAndRollsBackBatchOnLaterFailure()
        {
            string sessionId = "library-receipt-" + Guid.NewGuid().ToString("N").Substring(0, 12);
            Assert.IsTrue(VfxCandidateSandbox.TryGetSessionRoot(sessionId, out string sessionRoot, out string sessionError), sessionError);
            string candidateId = sessionId + "-c01";
            string candidateRoot = sessionRoot + "/" + candidateId;
            string evidenceRoot = VfxCandidateProtocol.AuthoritativeEvidenceRoot + "/" + sessionId;
            string planPath = evidenceRoot + "/generation_plan.json";
            try
            {
                VfxCandidatePlan localPlan = VfxCreativeCandidateMenu.CreateFireballPlan(sessionId);
                localPlan.CoreCandidateId = candidateId;
                localPlan.CandidateOutputRoot = candidateRoot;
                string syntheticHash = VfxCandidateSandbox.ComputeHash(new byte[] { 17, 73, 211 });
                var corePlan = new VfxCoreGenerationPlan
                {
                    PlanId = "plan_" + sessionId,
                    SessionId = sessionId,
                    BriefHash = syntheticHash,
                    ReuseDecisionHash = syntheticHash,
                    CatalogHash = VfxCandidateSandbox.ComputeAssetFileHash(VfxCandidateProtocol.AuthoringCatalogPath),
                    GenerationTemplateCatalogHash = VfxCandidateSandbox.ComputeAssetFileHash(VfxCandidateProtocol.GenerationTemplateCatalogPath),
                    SessionCapabilitiesHash = syntheticHash,
                    GenerationMode = "ControlledTemplate",
                    QualityProfileId = "windows.d3d12.1080p60",
                    QualityProfileVersion = "1",
                    TargetCostTier = "Medium",
                    SandboxRoot = sessionRoot
                };
                corePlan.FixedSeeds.Add(unchecked((int)localPlan.FixedSeed));
                corePlan.Candidates.Add(new VfxCoreGenerationCandidateSpec
                {
                    CandidateId = candidateId,
                    VariantKind = "ConservativeReuse",
                    Seed = unchecked((int)localPlan.FixedSeed),
                    ProviderId = "mx.vfx.provider.procedural",
                    ProviderVersion = "1.0.0",
                    ModelId = "mx.procedural.vfx",
                    ModelVersion = "1",
                    TemplateId = "mx.vfx.template.particle.fireball",
                    TemplateVersion = "1",
                    CostTier = "Medium",
                    OutputRoot = candidateRoot,
                    DraftOnly = true
                });
                corePlan.PlanHash = VfxCandidateSandbox.ComputeCoreDocumentHash(corePlan, "planHash", "diagnostics", "blocked");
                VfxCandidateSandbox.WriteJsonFile(planPath, corePlan);

                VfxCandidateGenerationResult generated = new VfxEditorCandidateGenerator().Generate(localPlan);
                Assert.IsTrue(generated.Success, generated.Message);
                var stage = new VfxLibraryAuthoritativeStagePlan
                {
                    StageId = "impact",
                    GenerationPlanPath = planPath,
                    GenerationPlan = corePlan
                };

                bool success = VfxLibraryCoreReceiptExporter.TryExport(
                    new[] { stage },
                    new[] { generated },
                    out List<VfxLibraryCoreStageReceiptEvidence> evidence,
                    out string message);

                Assert.IsTrue(success, message);
                Assert.AreEqual(1, evidence.Count);
                Assert.AreEqual(sessionRoot + "/candidate_manifest.json", evidence[0].Export.ManifestAssetPath);
                Assert.AreEqual(sessionRoot + "/candidate_receipt.json", evidence[0].Export.ReceiptAssetPath);
                Assert.IsTrue(File.Exists(evidence[0].Export.ManifestAssetPath));
                Assert.IsTrue(File.Exists(evidence[0].Export.ReceiptAssetPath));
                Assert.AreEqual("Draft", evidence[0].Export.Manifest.ReviewStatus);

                var missingStage = new VfxLibraryAuthoritativeStagePlan
                {
                    StageId = "flight",
                    GenerationPlanPath = evidenceRoot + "/missing_generation_plan.json",
                    GenerationPlan = corePlan
                };
                success = VfxLibraryCoreReceiptExporter.TryExport(
                    new[] { stage, missingStage },
                    new[] { generated },
                    out evidence,
                    out message);

                Assert.IsFalse(success);
                Assert.AreEqual(0, evidence.Count);
                StringAssert.Contains("不会覆盖或删除旧证据", message);
                Assert.IsTrue(File.Exists(sessionRoot + "/candidate_manifest.json"));
                Assert.IsTrue(File.Exists(sessionRoot + "/candidate_receipt.json"));

                Assert.IsTrue(VfxCandidateSandbox.TryDeleteFixedDraftEvidenceAsset(
                    sessionRoot + "/candidate_receipt.json", out bool receiptDeleted, out string receiptError), receiptError);
                Assert.IsTrue(receiptDeleted);
                Assert.IsTrue(VfxCandidateSandbox.TryDeleteFixedDraftEvidenceAsset(
                    sessionRoot + "/candidate_manifest.json", out bool manifestDeleted, out string manifestError), manifestError);
                Assert.IsTrue(manifestDeleted);

                success = VfxLibraryCoreReceiptExporter.TryExport(
                    new[] { stage, missingStage },
                    new[] { generated },
                    out evidence,
                    out message);

                Assert.IsFalse(success);
                Assert.AreEqual(0, evidence.Count);
                StringAssert.Contains("已回滚", message);
                Assert.IsFalse(File.Exists(sessionRoot + "/candidate_manifest.json"));
                Assert.IsFalse(File.Exists(sessionRoot + "/candidate_receipt.json"));
            }
            finally
            {
                if (AssetDatabase.IsValidFolder(sessionRoot)) AssetDatabase.DeleteAsset(sessionRoot);
                if (Directory.Exists(evidenceRoot)) Directory.Delete(evidenceRoot, true);
                AssetDatabase.Refresh();
            }
        }

        [Test]
        public void VfxResourceLibraryCard_ShowsMissingThumbnailStateInsteadOfBlankImage()
        {
            var effect = new VfxLibraryEffect
            {
                EffectId = "vfx.test.missing_thumbnail",
                DisplayName = "Missing thumbnail",
                Roles = new[] { "Impact" },
                Themes = new[] { "generic" },
                Backend = "ParticleSystem",
                ReviewStatus = "Draft",
                CostTier = "Low"
            };
            var card = new VfxLibraryEffectCard(_ => { }, _ => { });

            card.Bind(effect, null);

            Label placeholder = card.Q<Label>(className: "thumbnail-placeholder");
            Assert.IsNotNull(placeholder);
            Assert.AreEqual(DisplayStyle.Flex, placeholder.style.display.value);
        }

        [Test]
        public void VfxResourceLibraryDraft_IsolatesOriginalCatalogAndSelectedEffect()
        {
            const string path = "Temp/MxFramework/VfxAuthoring/Tests/draft_catalog.json";
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var catalog = JObject.Parse("{\"effects\":[{\"effectId\":\"vfx.test.a\",\"reviewStatus\":\"ReviewNeeded\",\"description\":\"A\"},{\"effectId\":\"vfx.test.b\",\"reviewStatus\":\"ReviewNeeded\",\"description\":\"B\"}]}");
            File.WriteAllText(path, catalog.ToString());
            try
            {
                JObject source = (JObject)((JArray)catalog["effects"])[0];
                var effect = new VfxLibraryEffect
                {
                    Source = source,
                    EffectId = "vfx.test.a",
                    ReviewStatus = "Ready",
                    Description = "A reviewed"
                };
                var draft = new VfxLibraryDraftSession(catalog, path);

                draft.Capture(effect);
                JObject candidate = draft.CreateCandidate();
                JArray candidateEffects = (JArray)candidate["effects"];

                Assert.IsTrue(draft.HasChanges);
                StringAssert.Contains("reviewStatus: ReviewNeeded → Ready", draft.DiffText);
                StringAssert.Contains("description: A → A reviewed", draft.DiffText);
                Assert.AreEqual("ReviewNeeded", source.Value<string>("reviewStatus"), "Draft edits must not mutate the loaded Catalog snapshot.");
                Assert.AreEqual("Ready", candidateEffects[0].Value<string>("reviewStatus"));
                Assert.AreEqual("A reviewed", candidateEffects[0].Value<string>("description"));
                Assert.AreEqual("B", candidateEffects[1].Value<string>("description"), "Only the captured Effect may enter the candidate Catalog.");
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        [Test]
        public void LaserLancePathPrefab_UsesLineRendererCompatibleMaterial()
        {
            const string materialPath = "Assets/VFX/MxFramework/Samples/LaserLance/Materials/LaserLance_Line.mat";
            const string prefabPath = "Assets/VFX/MxFramework/Samples/LaserLance/Prefabs/LaserLance_Laser.prefab";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

            Assert.IsNotNull(material);
            Assert.IsNotNull(material.shader);
            Assert.AreEqual("Sprites/Default", material.shader.name);
            Assert.IsNotNull(prefab);

            LineRenderer[] lineRenderers = prefab.GetComponentsInChildren<LineRenderer>(true);
            Assert.AreEqual(4, lineRenderers.Length);
            for (int i = 0; i < lineRenderers.Length; i++)
                Assert.AreSame(material, lineRenderers[i].sharedMaterial);

            Component[] components = prefab.GetComponentsInChildren<Component>(true);
            Component pathView = null;
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] != null && components[i].GetType().Name == "CharacterTestLaserPathVfxView")
                {
                    pathView = components[i];
                    break;
                }
            }

            Assert.IsNotNull(pathView);
            var serialized = new SerializedObject(pathView);
            SerializedProperty property = serialized.FindProperty("_lineMaterialOverride");
            Assert.IsNotNull(property);
            Assert.AreSame(material, property.objectReferenceValue);
        }

        private static bool ContainsComponent(GameObject root, string fullTypeName)
        {
            Component[] components = root.GetComponentsInChildren<Component>(true);
            for (int i = 0; i < components.Length; i++)
            {
                Component component = components[i];
                if (component != null && component.GetType().FullName == fullTypeName)
                    return true;
            }
            return false;
        }

        private static VfxRuntimeModule CreateModule(out RuntimeCommandBuffer commands)
        {
            commands = new RuntimeCommandBuffer();
            return new VfxRuntimeModule(CreateDefinitions("hash-v1"), commands);
        }

        private static VfxRuntimeModule CreateAddressModule(string address)
        {
            var definitions = new VfxDefinitionRegistry();
            Assert.IsTrue(definitions.TryRegister(new VfxDefinition(
                new VfxDefinitionId(1),
                "address-sensitive",
                new ResourceKey(address, ResourceTypeIds.GameObject),
                defaultLifetimeFrames: 2,
                definitionHash: "address-sensitive-definition"), out string failure), failure);
            return new VfxRuntimeModule(definitions, new RuntimeCommandBuffer());
        }

        private static long ComputeHash(VfxRuntimeModule module)
        {
            var contributor = new VfxRuntimeHashContributor(module);
            var accumulator = new RuntimeHashAccumulator(contributor.HashSchemaId, contributor.HashSchemaVersion);
            return ContributeHash(contributor, accumulator);
        }

        private static long ContributeHash(VfxRuntimeHashContributor contributor, RuntimeHashAccumulator accumulator)
        {
            accumulator.Reset();
            contributor.Contribute(new RuntimeHashContext(RuntimeFrame.Zero), accumulator);
            return accumulator.ToHash();
        }

        private static VfxDefinitionRegistry CreateDefinitions(string hash)
        {
            var definitions = new VfxDefinitionRegistry();
            Assert.IsTrue(definitions.TryRegister(new VfxDefinition(
                new VfxDefinitionId(1),
                "impact",
                TestPrefabKey,
                defaultLifetimeFrames: 2,
                definitionHash: hash + ".impact"), out string failure), failure);
            Assert.IsTrue(definitions.TryRegister(new VfxDefinition(
                new VfxDefinitionId(2),
                "spark",
                new ResourceKey("vfx.test.spark", ResourceTypeIds.GameObject),
                defaultLifetimeFrames: 3,
                definitionHash: hash + ".spark"), out failure), failure);
            return definitions;
        }

        private static VfxPose CreatePose(float x, float y, float z, float forwardX, float forwardY, float forwardZ)
        {
            return new VfxPose(
                CreateVfxVector(x, y, z),
                CreateVfxVector(forwardX, forwardY, forwardZ));
        }

        private static VfxVector3 CreateVfxVector(float x, float y, float z)
        {
            return VfxVector3.FromRaw(
                (long)(x * VfxVector3.Scale),
                (long)(y * VfxVector3.Scale),
                (long)(z * VfxVector3.Scale));
        }

        private static void AssertVector(Vector3 expected, Vector3 actual)
        {
            Assert.AreEqual(expected.x, actual.x, 0.0001f);
            Assert.AreEqual(expected.y, actual.y, 0.0001f);
            Assert.AreEqual(expected.z, actual.z, 0.0001f);
        }

        private static HitPresentationEvent CreateHitPresentationEvent(int presentationProfileId)
        {
            var usage = new DamageProfileUsage(
                DamageProfileSourceKind.WeaponTrace,
                actionId: CharacterTestCharacterActionConfigSource.LightAttackActionId,
                actionInstanceId: 3,
                stableEventId: CharacterTestAuthoredContentIds.D401_Id161,
                sourceEntityId: 5,
                targetEntityId: 9,
                frame: 12,
                localFrame: 4);
            var request = new DamageRequest(CharacterTestAuthoredContentIds.D408_Id1, usage);
            var profile = new DamageProfileResolveResult(
                success: true,
                reason: string.Empty,
                damageProfileId: CharacterTestAuthoredContentIds.D408_Id1,
                damageTypeId: CharacterTestAuthoredContentIds.D409_Id1,
                formulaId: DamageFormulaIds.Flat,
                incomingDamage: 20,
                staggerFrames: 0,
                impactForce: 0,
                knockback: FixVector3.Zero,
                impactPower: 0,
                guardPressure: 0,
                presentationProfileId: presentationProfileId,
                usage: usage);
            var hit = new HitResolveResult(
                new CombatEntityId(5),
                new CombatEntityId(9),
                actionId: 100,
                actionInstanceId: 3,
                traceId: 1,
                frame: new CombatFrame(12),
                kind: HitResolveKind.Damage,
                damage: 20,
                staggerFrames: 0,
                knockback: FixVector3.Zero,
                hitPointWorld: new FixVector3(Fix64.One, Fix64.FromInt(2), Fix64.FromInt(3)),
                hitNormalWorld: new FixVector3(Fix64.Zero, Fix64.One, Fix64.Zero));
            return new HitPresentationEvent(new DamageResolvedEvent(request, profile, hit, finalDamage: 17), presentationProfileId);
        }

        private static RuntimeTraceIdentity Trace(long sequence)
        {
            return new RuntimeTraceIdentity(TestTraceKind, sequence: sequence);
        }

        private sealed class LifecyclePublishingGameplaySink : IGameplayRuntimeEventSink
        {
            private readonly IRenderDataPublisher _publisher;
            private readonly IRenderSubjectMap<GameplayEntityId> _subjects;

            public LifecyclePublishingGameplaySink(
                IRenderDataPublisher publisher,
                IRenderSubjectMap<GameplayEntityId> subjects)
            {
                _publisher = publisher;
                _subjects = subjects;
            }

            public void OnGameplayRuntimeEvents(RuntimeFrame frame, IReadOnlyList<GameplayRuntimeEvent> events)
            {
                for (int i = 0; i < events.Count; i++)
                {
                    GameplayRuntimeEvent evt = events[i];
                    if (evt.Type != GameplayRuntimeEventType.ComponentEntityCreated ||
                        !evt.TryGetComponentEntityId(out GameplayEntityId entityId))
                    {
                        continue;
                    }

                    MxRenderSubjectId subject = _subjects.GetOrCreate(entityId, MxRenderSubjectRole.Tracked);
                    _publisher.PublishSubjectLifecycle(subject, MxSubjectLifecycleKind.Spawned);
                }
            }
        }

        private sealed class TestUnityVfxAnchorResolver : IUnityVfxAnchorResolver
        {
            private UnityVfxResolvedAnchor _anchor;

            public bool Enabled { get; set; } = true;

            public void Set(Vector3 position, Vector3 forward)
            {
                _anchor = new UnityVfxResolvedAnchor(position, forward);
            }

            public bool TryResolve(in VfxAnchor anchor, out UnityVfxResolvedAnchor resolvedAnchor)
            {
                resolvedAnchor = _anchor;
                return Enabled;
            }
        }

        private sealed class TestUnityVfxBackendController : MonoBehaviour, IUnityVfxBackendController
        {
            public bool Available;
            public bool SuppressFallback;
            public int PlayCount;
            public int StopCount;
            public int LastValue0;

            public bool IsAvailable => Available;
            public bool SuppressParticleSystemFallbackWhenAvailable => SuppressFallback;

            public void ReinitializeAndPlay(VfxInstanceSnapshot instance)
            {
                PlayCount++;
                LastValue0 = instance.Parameters.Value0;
            }

            public void StopAndClear()
            {
                StopCount++;
            }
        }
    }
}
