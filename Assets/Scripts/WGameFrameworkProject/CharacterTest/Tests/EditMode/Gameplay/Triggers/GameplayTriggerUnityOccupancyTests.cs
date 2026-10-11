using System;
using System.Collections.Generic;
using MxFramework.Gameplay;
using MxFramework.Gameplay.Triggers;
using MxFramework.Gameplay.Triggers.Config;
using MxFramework.Gameplay.Triggers.Unity;
using MxFramework.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace MxFramework.Tests.Gameplay.Triggers
{
    public sealed class GameplayTriggerUnityOccupancyTests
    {
        private static readonly GameplayTriggerActionKindId ActionKindId =
            new GameplayTriggerActionKindId(73201);

        private readonly List<GameObject> _objects = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            for (int i = _objects.Count - 1; i >= 0; i--)
            {
                if (_objects[i] != null)
                    UnityEngine.Object.DestroyImmediate(_objects[i]);
            }
            _objects.Clear();
        }

        [Test]
        public void Catalog_CanonicalizesDescriptorsAndRejectsUnsupportedContracts()
        {
            TriggerAuthority authority = CreateAuthority(bindingCount: 2, maxSubjects: 2);
            GameplayTriggerUnityOccupancyDescriptor first = Descriptor(1, 7, 2);
            GameplayTriggerUnityOccupancyDescriptor second = Descriptor(2, 7, 2);

            Assert.That(
                GameplayTriggerUnityOccupancyCatalog.TryCreate(
                    new[] { second, first },
                    authority.Plan,
                    out GameplayTriggerUnityOccupancyCatalog reversed,
                    out GameplayTriggerUnityCatalogResult reversedResult),
                Is.True,
                Describe(reversedResult));
            Assert.That(
                GameplayTriggerUnityOccupancyCatalog.TryCreate(
                    new[] { first, second },
                    authority.Plan,
                    out GameplayTriggerUnityOccupancyCatalog forward,
                    out GameplayTriggerUnityCatalogResult forwardResult),
                Is.True,
                Describe(forwardResult));

            Assert.That(reversed.GetDescriptor(0).SourceBindingId, Is.EqualTo(SourceBinding(1)));
            Assert.That(reversed.GetDescriptor(1).SourceBindingId, Is.EqualTo(SourceBinding(2)));
            Assert.That(reversed.UnityOccupancyCatalogHash, Is.EqualTo(forward.UnityOccupancyCatalogHash));
            Assert.That(reversed.CombinedCatalogHash, Is.EqualTo(forward.CombinedCatalogHash));
            Assert.That(reversed.GameplayTriggerCatalogHash, Is.EqualTo(authority.Plan.GameplayTriggerCatalogHash));

            TriggerAuthority instigator = CreateAuthority(
                maxSubjects: 2,
                capabilities: RequiredCapabilities |
                    GameplayTriggerSourceEntityCapability.InstigatorPossible);
            Assert.That(
                GameplayTriggerUnityOccupancyCatalog.TryCreate(
                    new[] { first },
                    instigator.Plan,
                    out GameplayTriggerUnityOccupancyCatalog rejected,
                    out GameplayTriggerUnityCatalogResult rejectedResult),
                Is.False);
            Assert.That(rejected, Is.Null);
            Assert.That(
                rejectedResult.ErrorCode,
                Is.EqualTo(GameplayTriggerUnityCatalogErrorCode.EntityCapabilityMismatch));
        }

        [Test]
        public void Adapter_SubmitsFullLevelEveryFrameAndDeduplicatesMultipleColliders()
        {
            AdapterComposition composition = CreateAdapter(maxSubjects: 2, bufferCapacity: 8);
            Collider zoneCollider = CreateCollider("zone", isTrigger: true);
            Collider firstCollider = CreateCollider("subject-a", isTrigger: false);
            Collider secondCollider = CreateCollider("subject-b", isTrigger: false);
            GameplayEntityId subject = composition.ComponentWorld.CreateEntity();

            GameplayTriggerUnityZoneHandle zone = RegisterZone(
                composition.Adapter,
                SourceBinding(1),
                zoneCollider);
            BindSubject(
                composition.Adapter,
                subject,
                new[] { firstCollider, secondCollider });
            Assert.That(composition.Adapter.SetContact(zone, firstCollider, true).Mutated, Is.True);
            Assert.That(
                composition.Adapter.SetContact(zone, firstCollider, true).Status,
                Is.EqualTo(GameplayTriggerUnityContactStatus.DuplicateEnter));
            Assert.That(
                composition.Adapter.SetContact(zone, secondCollider, false).Status,
                Is.EqualTo(GameplayTriggerUnityContactStatus.UnknownExit));
            Assert.That(composition.Adapter.SetContact(zone, secondCollider, true).Mutated, Is.True);
            Assert.That(composition.Authority.Runtime.OccurrenceCount, Is.Zero);
            Assert.That(composition.Authority.ActionSink.CommitCount, Is.Zero);

            TickFlush(composition, 0);
            AssertSubmittedSubject(composition, 0, subject);
            Assert.That(composition.Authority.ActionSink.CommitCount, Is.Zero);
            TickGameplay(composition, 0);
            AssertOccurrence(composition.Authority.Runtime, GameplayTriggerOccurrenceKind.Entered, subject);
            Assert.That(composition.Authority.ActionSink.CommitCount, Is.EqualTo(1));

            TickFlush(composition, 1);
            AssertSubmittedSubject(composition, 0, subject);
            TickGameplay(composition, 1);
            AssertOccurrence(composition.Authority.Runtime, GameplayTriggerOccurrenceKind.Stayed, subject);

            Assert.That(composition.Adapter.SetContact(zone, firstCollider, false).Mutated, Is.True);
            TickFlush(composition, 2);
            AssertSubmittedSubject(composition, 0, subject);
            TickGameplay(composition, 2);
            AssertOccurrence(composition.Authority.Runtime, GameplayTriggerOccurrenceKind.Stayed, subject);

            Assert.That(composition.Adapter.SetContact(zone, secondCollider, false).Mutated, Is.True);
            TickFlush(composition, 3);
            AssertSubmittedEmpty(composition, 0);
            TickGameplay(composition, 3);
            AssertOccurrence(composition.Authority.Runtime, GameplayTriggerOccurrenceKind.Exited, subject);
        }

        [Test]
        public void Adapter_FixedCapacityFailuresDoNotMutateAcceptedRows()
        {
            AdapterComposition subjectBounded = CreateAdapter(
                maxSubjects: 1,
                maxSubjectBindings: 1,
                maxSubjectColliders: 2,
                maxActiveContactPairs: 2);
            Collider subjectFirst = CreateCollider("subject-capacity-first", false);
            Collider subjectOverflow = CreateCollider("subject-capacity-overflow", false);
            BindSubject(
                subjectBounded.Adapter,
                subjectBounded.ComponentWorld.CreateEntity(),
                new[] { subjectFirst });
            Assert.That(
                subjectBounded.Adapter.TryBindSubject(
                    subjectBounded.ComponentWorld.CreateEntity(),
                    new[] { subjectOverflow },
                    subjectOverflow.gameObject.scene.handle.GetRawData(),
                    out _,
                    out GameplayTriggerUnitySubjectBindingResult subjectFailure),
                Is.False);
            Assert.That(
                subjectFailure.Status,
                Is.EqualTo(GameplayTriggerUnitySubjectBindingStatus.SubjectCapacityExceeded));

            AdapterComposition colliderBounded = CreateAdapter(
                maxSubjects: 1,
                maxSubjectBindings: 2,
                maxSubjectColliders: 2,
                maxActiveContactPairs: 2);
            Collider colliderFirst = CreateCollider("collider-capacity-first", false);
            Collider colliderSecond = CreateCollider("collider-capacity-second", false);
            Collider colliderOverflow = CreateCollider("collider-capacity-overflow", false);
            BindSubject(
                colliderBounded.Adapter,
                colliderBounded.ComponentWorld.CreateEntity(),
                new[] { colliderFirst, colliderSecond });
            Assert.That(
                colliderBounded.Adapter.TryBindSubject(
                    colliderBounded.ComponentWorld.CreateEntity(),
                    new[] { colliderOverflow },
                    colliderOverflow.gameObject.scene.handle.GetRawData(),
                    out _,
                    out GameplayTriggerUnitySubjectBindingResult colliderFailure),
                Is.False);
            Assert.That(
                colliderFailure.Status,
                Is.EqualTo(GameplayTriggerUnitySubjectBindingStatus.ColliderCapacityExceeded));

            AdapterComposition contactBounded = CreateAdapter(
                maxSubjects: 1,
                maxSubjectBindings: 2,
                maxSubjectColliders: 2,
                maxActiveContactPairs: 1);
            Collider contactZoneCollider = CreateCollider("contact-capacity-zone", true);
            Collider contactFirst = CreateCollider("contact-capacity-first", false);
            Collider contactOverflow = CreateCollider("contact-capacity-overflow", false);
            GameplayEntityId acceptedSubject = contactBounded.ComponentWorld.CreateEntity();
            GameplayEntityId rejectedSubject = contactBounded.ComponentWorld.CreateEntity();
            GameplayTriggerUnityZoneHandle contactZone = RegisterZone(
                contactBounded.Adapter,
                SourceBinding(1),
                contactZoneCollider);
            GameplayTriggerUnitySubjectHandle acceptedHandle = BindSubject(
                contactBounded.Adapter,
                acceptedSubject,
                new[] { contactFirst });
            BindSubject(contactBounded.Adapter, rejectedSubject, new[] { contactOverflow });
            Assert.That(contactBounded.Adapter.SetContact(contactZone, contactFirst, true).Mutated, Is.True);
            Assert.That(
                contactBounded.Adapter.SetContact(contactZone, contactOverflow, true).Status,
                Is.EqualTo(GameplayTriggerUnityContactStatus.ContactCapacityExceeded));
            TickFlush(contactBounded, 0);
            AssertSubmittedSubject(contactBounded, 0, acceptedSubject);
            TickGameplay(contactBounded, 0);
            Assert.That(contactBounded.Adapter.TryUnbindSubject(acceptedHandle, out _), Is.True);
            Assert.That(contactBounded.Adapter.SetContact(contactZone, contactOverflow, true).Mutated, Is.True);
            TickFlush(contactBounded, 1);
            AssertSubmittedSubject(contactBounded, 0, rejectedSubject);

            AdapterComposition sourceBounded = CreateAdapter(
                maxSubjects: 1,
                maxSubjectBindings: 2,
                maxSubjectColliders: 2,
                maxActiveContactPairs: 2);
            Collider sourceZoneCollider = CreateCollider("source-capacity-zone", true);
            Collider sourceFirst = CreateCollider("source-capacity-first", false);
            Collider sourceOverflow = CreateCollider("source-capacity-overflow", false);
            GameplayTriggerUnityZoneHandle sourceZone = RegisterZone(
                sourceBounded.Adapter,
                SourceBinding(1),
                sourceZoneCollider);
            BindSubject(
                sourceBounded.Adapter,
                sourceBounded.ComponentWorld.CreateEntity(),
                new[] { sourceFirst });
            BindSubject(
                sourceBounded.Adapter,
                sourceBounded.ComponentWorld.CreateEntity(),
                new[] { sourceOverflow });
            sourceBounded.Adapter.SetContact(sourceZone, sourceFirst, true);
            sourceBounded.Adapter.SetContact(sourceZone, sourceOverflow, true);
            TickFlush(sourceBounded, 0);
            Assert.That(sourceBounded.CommandBuffer.PendingCount, Is.Zero);
            Assert.That(
                sourceBounded.Adapter.GetLastZoneFlushResult(0).Status,
                Is.EqualTo(GameplayTriggerUnityZoneFlushStatus.SubjectCapacityExceeded));
        }

        [Test]
        public void Adapter_CallbackPermutationsProduceIdenticalCommandsAndAuthorityHash()
        {
            AdapterComposition left = CreateAdapter(maxSubjects: 2, bufferCapacity: 4);
            AdapterComposition right = CreateAdapter(maxSubjects: 2, bufferCapacity: 4);
            Collider leftZoneCollider = CreateCollider("left-zone", true);
            Collider rightZoneCollider = CreateCollider("right-zone", true);
            Collider leftFirstCollider = CreateCollider("left-first", false);
            Collider leftSecondCollider = CreateCollider("left-second", false);
            Collider rightFirstCollider = CreateCollider("right-first", false);
            Collider rightSecondCollider = CreateCollider("right-second", false);
            GameplayTriggerUnityZoneHandle leftZone = RegisterZone(
                left.Adapter,
                SourceBinding(1),
                leftZoneCollider);
            GameplayTriggerUnityZoneHandle rightZone = RegisterZone(
                right.Adapter,
                SourceBinding(1),
                rightZoneCollider);
            GameplayEntityId leftFirst = left.ComponentWorld.CreateEntity();
            GameplayEntityId leftSecond = left.ComponentWorld.CreateEntity();
            GameplayEntityId rightFirst = right.ComponentWorld.CreateEntity();
            GameplayEntityId rightSecond = right.ComponentWorld.CreateEntity();
            Assert.That(leftFirst, Is.EqualTo(rightFirst));
            Assert.That(leftSecond, Is.EqualTo(rightSecond));
            BindSubject(left.Adapter, leftFirst, new[] { leftFirstCollider });
            BindSubject(left.Adapter, leftSecond, new[] { leftSecondCollider });
            BindSubject(right.Adapter, rightFirst, new[] { rightFirstCollider });
            BindSubject(right.Adapter, rightSecond, new[] { rightSecondCollider });

            left.Adapter.SetContact(leftZone, leftSecondCollider, true);
            left.Adapter.SetContact(leftZone, leftFirstCollider, true);
            right.Adapter.SetContact(rightZone, rightFirstCollider, true);
            right.Adapter.SetContact(rightZone, rightSecondCollider, true);
            TickFlush(left, 0);
            TickFlush(right, 0);

            RuntimeCommandBufferSnapshot leftCommands = left.CommandBuffer.CaptureSnapshot();
            RuntimeCommandBufferSnapshot rightCommands = right.CommandBuffer.CaptureSnapshot();
            Assert.That(leftCommands.PendingCount, Is.EqualTo(rightCommands.PendingCount));
            for (int i = 0; i < leftCommands.PendingCount; i++)
                Assert.That(leftCommands.PendingCommands[i], Is.EqualTo(rightCommands.PendingCommands[i]));

            TickGameplay(left, 0);
            TickGameplay(right, 0);
            Assert.That(
                ComputeAuthorityHash(left.Authority.Runtime, RuntimeFrame.Zero),
                Is.EqualTo(ComputeAuthorityHash(right.Authority.Runtime, RuntimeFrame.Zero)));
        }

        [Test]
        public void Adapter_RebindsActiveContactAndFiltersDestroyedGeneration()
        {
            AdapterComposition composition = CreateAdapter(maxSubjects: 2, bufferCapacity: 8);
            Collider zoneCollider = CreateCollider("zone", isTrigger: true);
            Collider subjectCollider = CreateCollider("subject", isTrigger: false);
            GameplayEntityId first = composition.ComponentWorld.CreateEntity();
            GameplayEntityId second = composition.ComponentWorld.CreateEntity();
            GameplayTriggerUnityZoneHandle zone = RegisterZone(
                composition.Adapter,
                SourceBinding(1),
                zoneCollider);
            GameplayTriggerUnitySubjectHandle handle = BindSubject(
                composition.Adapter,
                first,
                new[] { subjectCollider });
            composition.Adapter.SetContact(zone, subjectCollider, true);
            TickFlush(composition, 0);
            TickGameplay(composition, 0);

            Assert.That(
                composition.Adapter.TryRebindSubject(
                    handle,
                    second,
                    out GameplayTriggerUnitySubjectBindingResult rebound),
                Is.True,
                rebound.Status.ToString());
            TickFlush(composition, 1);
            AssertSubmittedSubject(composition, 0, second);
            TickGameplay(composition, 1);
            Assert.That(composition.Authority.Runtime.OccurrenceCount, Is.EqualTo(2));

            Assert.That(composition.ComponentWorld.DestroyEntity(second), Is.True);
            TickFlush(composition, 2);
            GameplayTriggerUnityZoneFlushResult stale =
                composition.Adapter.GetLastZoneFlushResult(0);
            Assert.That(stale.Status, Is.EqualTo(GameplayTriggerUnityZoneFlushStatus.Submitted));
            Assert.That(stale.SubjectCount, Is.Zero);
            Assert.That(stale.StaleSubjectCount, Is.EqualTo(1));
        }

        [Test]
        public void Adapter_EnterThenExitWithinBoundarySubmitsExplicitEmptyLevel()
        {
            AdapterComposition composition = CreateAdapter(maxSubjects: 1, bufferCapacity: 4);
            Collider zoneCollider = CreateCollider("zone", isTrigger: true);
            Collider subjectCollider = CreateCollider("subject", isTrigger: false);
            GameplayEntityId subject = composition.ComponentWorld.CreateEntity();
            GameplayTriggerUnityZoneHandle zone = RegisterZone(
                composition.Adapter,
                SourceBinding(1),
                zoneCollider);
            BindSubject(composition.Adapter, subject, new[] { subjectCollider });

            Assert.That(composition.Adapter.SetContact(zone, subjectCollider, true).Mutated, Is.True);
            Assert.That(composition.Adapter.SetContact(zone, subjectCollider, false).Mutated, Is.True);
            TickFlush(composition, 0);

            RuntimeCommand command = composition.CommandBuffer.CaptureSnapshot().PendingCommands[0];
            Assert.That(
                GameplayTriggerOccupancyCommandCodec.TryDecodeRecord(
                    command,
                    out GameplayTriggerOccupancyCommandRecord record,
                    out GameplayTriggerOccupancyCommandCodecStatus status),
                Is.True,
                status.ToString());
            Assert.That(record.IsEmptyBatch, Is.True);
            Assert.That(composition.Adapter.GetLastZoneFlushResult(0).SubjectCount, Is.Zero);

            TickGameplay(composition, 0);
            Assert.That(composition.Authority.Runtime.OccurrenceCount, Is.Zero);
        }

        [Test]
        public void Adapter_DisabledSourceRetainsContactsAndReenableResubmitsFullLevel()
        {
            AdapterComposition composition = CreateAdapter(maxSubjects: 1, bufferCapacity: 4);
            Collider zoneCollider = CreateCollider("zone", isTrigger: true);
            Collider subjectCollider = CreateCollider("subject", isTrigger: false);
            GameplayEntityId subject = composition.ComponentWorld.CreateEntity();
            GameplayTriggerUnityZoneHandle zone = RegisterZone(
                composition.Adapter,
                SourceBinding(1),
                zoneCollider);
            BindSubject(composition.Adapter, subject, new[] { subjectCollider });
            composition.Adapter.SetContact(zone, subjectCollider, true);

            Assert.That(
                composition.Authority.Runtime.TrySetEnabled(
                    composition.Authority.Runtime.GetInstanceId(0),
                    false,
                    out GameplayTriggerResult disabled),
                Is.True,
                disabled.ErrorCode.ToString());
            TickFlush(composition, 0);
            Assert.That(composition.CommandBuffer.PendingCount, Is.Zero);
            Assert.That(
                composition.Adapter.GetLastZoneFlushResult(0).Status,
                Is.EqualTo(GameplayTriggerUnityZoneFlushStatus.SourceDisabled));

            Assert.That(
                composition.Authority.Runtime.TrySetEnabled(
                    composition.Authority.Runtime.GetInstanceId(0),
                    true,
                    out GameplayTriggerResult enabled),
                Is.True,
                enabled.ErrorCode.ToString());
            TickFlush(composition, 1);
            AssertSubmittedSubject(composition, 0, subject);
        }

        [Test]
        public void Adapter_RetirementRetriesFinalEmptyUntilAtomicAdmissionSucceeds()
        {
            AdapterComposition composition = CreateAdapter(maxSubjects: 1, bufferCapacity: 1);
            Collider zoneCollider = CreateCollider("zone", isTrigger: true);
            GameplayTriggerUnityZoneHandle zone = RegisterZone(
                composition.Adapter,
                SourceBinding(1),
                zoneCollider);

            TickFlush(composition, 0);
            Assert.That(composition.CommandBuffer.PendingCount, Is.EqualTo(1));
            Assert.That(
                composition.Adapter.TryRetireZone(
                    zone,
                    out GameplayTriggerUnityLifecycleResult retirement),
                Is.True);
            Assert.That(
                retirement.Status,
                Is.EqualTo(GameplayTriggerUnityLifecycleStatus.RetirementScheduled));

            TickFlush(composition, 1);
            Assert.That(
                composition.Adapter.GetLastZoneFlushResult(0).Status,
                Is.EqualTo(GameplayTriggerUnityZoneFlushStatus.AdmissionRejected));
            Assert.That(
                composition.Adapter.GetLastZoneFlushResult(0).CommandErrorCode,
                Is.EqualTo(RuntimeCommandErrorCode.PendingCapacityExceeded));

            composition.CommandBuffer.Clear();
            TickFlush(composition, 2);
            Assert.That(
                composition.Adapter.GetLastZoneFlushResult(0).Status,
                Is.EqualTo(GameplayTriggerUnityZoneFlushStatus.Submitted));
            Assert.That(
                composition.Adapter.SetContact(zone, zoneCollider, true).Status,
                Is.EqualTo(GameplayTriggerUnityContactStatus.StaleZone));
        }

        [Test]
        public void Adapter_ReplayDisabledSuppressesLiveSubmissionAndRecyclesRetirement()
        {
            AdapterComposition composition = CreateAdapter(
                maxSubjects: 1,
                bufferCapacity: 2,
                submissionMode: GameplayTriggerUnitySubmissionMode.ReplayDisabled);
            Collider zoneCollider = CreateCollider("zone", isTrigger: true);
            GameplayTriggerUnityZoneHandle zone = RegisterZone(
                composition.Adapter,
                SourceBinding(1),
                zoneCollider);

            TickFlush(composition, 0);
            Assert.That(composition.CommandBuffer.PendingCount, Is.Zero);
            Assert.That(
                composition.Adapter.GetLastZoneFlushResult(0).Status,
                Is.EqualTo(GameplayTriggerUnityZoneFlushStatus.ReplaySuppressed));

            Assert.That(composition.Adapter.TryRetireZone(zone, out _), Is.True);
            TickFlush(composition, 1);
            Assert.That(composition.CommandBuffer.PendingCount, Is.Zero);
            Assert.That(
                composition.Adapter.GetLastZoneFlushResult(0).Status,
                Is.EqualTo(GameplayTriggerUnityZoneFlushStatus.ReplayRetiredWithoutSubmission));
        }

        [Test]
        public void Adapter_FlushesCanonicalSourceBindingOrderIndependentOfRegistrationOrder()
        {
            AdapterComposition composition = CreateAdapter(
                bindingCount: 2,
                maxSubjects: 1,
                bufferCapacity: 4);
            RegisterZone(composition.Adapter, SourceBinding(2), CreateCollider("zone-2", true));
            RegisterZone(composition.Adapter, SourceBinding(1), CreateCollider("zone-1", true));

            TickFlush(composition, 0);
            RuntimeCommandBufferSnapshot snapshot = composition.CommandBuffer.CaptureSnapshot();

            Assert.That(snapshot.PendingCount, Is.EqualTo(2));
            Assert.That(snapshot.PendingCommands[0].TargetId, Is.EqualTo(1));
            Assert.That(snapshot.PendingCommands[0].Sequence, Is.EqualTo(0));
            Assert.That(snapshot.PendingCommands[1].TargetId, Is.EqualTo(2));
            Assert.That(snapshot.PendingCommands[1].Sequence, Is.EqualTo(1));
        }

        [Test]
        public void Adapter_RegisteredSteadyStateDoesNotAllocateAcross64Cycles()
        {
            AdapterComposition composition = CreateAdapter(
                bindingCount: 2,
                maxSubjects: 1,
                bufferCapacity: 2);
            RegisterZone(composition.Adapter, SourceBinding(2), CreateCollider("zone-2", true));
            RegisterZone(composition.Adapter, SourceBinding(1), CreateCollider("zone-1", true));
            TickFlush(composition, 0);
            composition.CommandBuffer.Reset(new RuntimeFrame(1));

            _ = GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int frame = 1; frame <= 64; frame++)
            {
                TickFlush(composition, frame);
                composition.CommandBuffer.Reset(new RuntimeFrame(frame + 1L));
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.That(allocated, Is.Zero);
            Assert.That(composition.Adapter.LastFlushSummary.SubmittedZoneCount, Is.EqualTo(2));
        }

        [Test]
        public void Composition_RejectsApprovedMismatchMatrix()
        {
            AssertCompositionFailure(
                CompositionFailure.CatalogPlan,
                GameplayTriggerUnityInitializationErrorCode.CatalogPlanMismatch);
            AssertCompositionFailure(
                CompositionFailure.RuntimeCatalog,
                GameplayTriggerUnityInitializationErrorCode.RuntimeCatalogMismatch);
            AssertCompositionFailure(
                CompositionFailure.GameplaySchedule,
                GameplayTriggerUnityInitializationErrorCode.GameplayModuleScheduleMismatch);
            AssertCompositionFailure(
                CompositionFailure.UnboundedBuffer,
                GameplayTriggerUnityInitializationErrorCode.CommandBufferUnbounded);
            AssertCompositionFailure(
                CompositionFailure.CommandSchedule,
                GameplayTriggerUnityInitializationErrorCode.CommandSystemScheduleMismatch);
            AssertCompositionFailure(
                CompositionFailure.CommandDisabled,
                GameplayTriggerUnityInitializationErrorCode.CommandSystemDisabled);
            AssertCompositionFailure(
                CompositionFailure.CommandRuntime,
                GameplayTriggerUnityInitializationErrorCode.CommandSystemRuntimeMismatch);
            AssertCompositionFailure(
                CompositionFailure.CommandNotRegistered,
                GameplayTriggerUnityInitializationErrorCode.CommandSystemNotRegistered);
            AssertCompositionFailure(
                CompositionFailure.ReconstructionCapacity,
                GameplayTriggerUnityInitializationErrorCode.ReconstructionCapacityInsufficient);
            AssertCompositionFailure(
                CompositionFailure.SourceRegistrySealed,
                GameplayTriggerUnityInitializationErrorCode.SourceRegistrationFailed);
        }

        private static GameplayTriggerSourceEntityCapability RequiredCapabilities =>
            GameplayTriggerSourceEntityCapability.SubjectPossible |
            GameplayTriggerSourceEntityCapability.SubjectGuaranteed;

        private enum CompositionFailure
        {
            CatalogPlan,
            RuntimeCatalog,
            GameplaySchedule,
            UnboundedBuffer,
            CommandSchedule,
            CommandDisabled,
            CommandRuntime,
            CommandNotRegistered,
            ReconstructionCapacity,
            SourceRegistrySealed
        }

        private AdapterComposition CreateAdapter(
            int bindingCount = 1,
            int maxSubjects = 1,
            int bufferCapacity = 8,
            GameplayTriggerUnitySubmissionMode submissionMode =
                GameplayTriggerUnitySubmissionMode.LiveExternal,
            int? maxSubjectBindings = null,
            int? maxSubjectColliders = null,
            int? maxActiveContactPairs = null)
        {
            TriggerAuthority authority = CreateAuthority(bindingCount, maxSubjects);
            var descriptors = new GameplayTriggerUnityOccupancyDescriptor[bindingCount];
            for (int i = 0; i < bindingCount; i++)
                descriptors[i] = Descriptor(i + 1, 7, maxSubjects);
            Assert.That(
                GameplayTriggerUnityOccupancyCatalog.TryCreate(
                    descriptors,
                    authority.Plan,
                    out GameplayTriggerUnityOccupancyCatalog catalog,
                    out GameplayTriggerUnityCatalogResult catalogResult),
                Is.True,
                Describe(catalogResult));

            var commandBuffer = RuntimeCommandBuffer.CreateBounded(bufferCapacity);
            var componentWorld = new GameplayComponentWorld();
            var pipeline = new GameplaySystemPipeline();
            var commandSystem = new GameplayTriggerCommandSystem(
                authority.Runtime,
                GameplayTriggerCommandSystem.DefaultSystemId,
                GameplayTriggerCommandSystem.DefaultPriority,
                GameplayTriggerCommandSystem.DefaultMaxSignalCommandItems,
                maxSubjects,
                GameplayTriggerCommandSystem.DefaultMaxScalarCommandItems);
            pipeline.Add(commandSystem);
            pipeline.Add(new GameplayTriggerEvaluationSystem(authority.Runtime));
            var gameplayModule = new GameplayRuntimeModule(
                new GameplayWorld(),
                new GameplayAbilityRegistry(),
                commandBuffer,
                tickWorldAutomatically: false,
                systemPipeline: pipeline,
                componentWorld: componentWorld);
            var sourceRegistry = new RuntimeCommandSourceRegistry(1);
            var options = new GameplayTriggerUnityOccupancyOptions(
                maxSubjectBindings: maxSubjectBindings ?? bindingCount * maxSubjects,
                maxSubjectColliders: maxSubjectColliders ?? bindingCount * maxSubjects * 2,
                maxActiveContactPairs: maxActiveContactPairs ?? bindingCount * maxSubjects * 2);

            Assert.That(
                GameplayTriggerUnityOccupancyComposition.TryCreate(
                    commandSourceId: 732,
                    sourceRegistry,
                    gameplayModule,
                    commandSystem,
                    authority.Runtime,
                    authority.Plan,
                    catalog,
                    submissionMode,
                    in options,
                    out GameplayTriggerUnityOccupancyAdapter adapter,
                    out GameplayTriggerUnityOccupancyFlushModule flushModule,
                    out GameplayTriggerUnityInitializationResult result),
                Is.True,
                Describe(result));
            return new AdapterComposition(
                authority,
                componentWorld,
                commandBuffer,
                gameplayModule,
                adapter,
                flushModule);
        }

        private static void AssertCompositionFailure(
            CompositionFailure failure,
            GameplayTriggerUnityInitializationErrorCode expected)
        {
            TriggerAuthority baseline = CreateAuthority(maxSubjects: 2);
            Assert.That(
                GameplayTriggerUnityOccupancyCatalog.TryCreate(
                    new[] { Descriptor(1, 7, 2) },
                    baseline.Plan,
                    out GameplayTriggerUnityOccupancyCatalog catalog,
                    out GameplayTriggerUnityCatalogResult catalogResult),
                Is.True,
                Describe(catalogResult));
            TriggerAuthority alternate = CreateAuthority(bindingCount: 2, maxSubjects: 2);
            GameplayTriggerCompiledCatalogPlan plan = failure == CompositionFailure.CatalogPlan
                ? alternate.Plan
                : baseline.Plan;
            GameplayTriggerRuntime runtime = failure == CompositionFailure.RuntimeCatalog
                ? alternate.Runtime
                : baseline.Runtime;
            GameplayTriggerRuntime commandRuntime = failure == CompositionFailure.CommandRuntime
                ? alternate.Runtime
                : runtime;
            RuntimeCommandBuffer buffer = failure == CompositionFailure.UnboundedBuffer
                ? new RuntimeCommandBuffer(initialCapacity: 8)
                : RuntimeCommandBuffer.CreateBounded(8);
            int commandPriority = failure == CompositionFailure.CommandSchedule
                ? GameplayTriggerCommandSystem.DefaultPriority + 1
                : GameplayTriggerCommandSystem.DefaultPriority;
            int occupancyCapacity = failure == CompositionFailure.ReconstructionCapacity
                ? 1
                : 2;
            var commandSystem = new GameplayTriggerCommandSystem(
                commandRuntime,
                GameplayTriggerCommandSystem.DefaultSystemId,
                commandPriority,
                GameplayTriggerCommandSystem.DefaultMaxSignalCommandItems,
                occupancyCapacity,
                GameplayTriggerCommandSystem.DefaultMaxScalarCommandItems);
            if (failure == CompositionFailure.CommandDisabled)
                commandSystem.SetEnabled(false);
            var pipeline = new GameplaySystemPipeline();
            if (failure != CompositionFailure.CommandNotRegistered)
                pipeline.Add(commandSystem);
            var gameplayModule = new GameplayRuntimeModule(
                new GameplayWorld(),
                new GameplayAbilityRegistry(),
                buffer,
                tickWorldAutomatically: false,
                priority: failure == CompositionFailure.GameplaySchedule ? 99 : 100,
                systemPipeline: pipeline,
                componentWorld: new GameplayComponentWorld());
            var sourceRegistry = new RuntimeCommandSourceRegistry(1);
            if (failure == CompositionFailure.SourceRegistrySealed)
                sourceRegistry.Seal();
            var options = new GameplayTriggerUnityOccupancyOptions(2, 2, 2);

            bool created = GameplayTriggerUnityOccupancyComposition.TryCreate(
                732,
                sourceRegistry,
                gameplayModule,
                commandSystem,
                runtime,
                plan,
                catalog,
                GameplayTriggerUnitySubmissionMode.LiveExternal,
                in options,
                out GameplayTriggerUnityOccupancyAdapter adapter,
                out GameplayTriggerUnityOccupancyFlushModule module,
                out GameplayTriggerUnityInitializationResult result);

            Assert.That(created, Is.False, failure.ToString());
            Assert.That(adapter, Is.Null, failure.ToString());
            Assert.That(module, Is.Null, failure.ToString());
            Assert.That(result.ErrorCode, Is.EqualTo(expected), failure.ToString());
            if (failure == CompositionFailure.SourceRegistrySealed)
            {
                Assert.That(
                    result.SourceErrorCode,
                    Is.EqualTo(RuntimeCommandSourceRegistrationErrorCode.RegistrySealed));
            }
        }

        private static TriggerAuthority CreateAuthority(
            int bindingCount = 1,
            int maxSubjects = 1,
            GameplayTriggerSourceEntityCapability? capabilities = null)
        {
            GameplayTriggerSourceEntityCapability actualCapabilities =
                capabilities ?? RequiredCapabilities;
            var bindings = new GameplayTriggerBindingAuthoring[bindingCount];
            var contracts = new GameplayTriggerSourceBindingContractDescriptor[bindingCount];
            for (int i = 0; i < bindingCount; i++)
            {
                int id = i + 1;
                bindings[i] = new GameplayTriggerBindingAuthoring(id, 1, id);
                contracts[i] = new GameplayTriggerSourceBindingContractDescriptor(
                    SourceBinding(id),
                    new GameplayTriggerSourceDefinitionId(1),
                    GameplayTriggerSourceKind.Occupancy,
                    new GameplayTriggerSourceProviderId(7),
                    actualCapabilities,
                    completeBatchPerEvaluation: true,
                    maxInputItemsPerEvaluation: maxSubjects,
                    maxActiveRelations: maxSubjects,
                    signalEnvelopeSchemaVersion: 0,
                    signalOrdering: GameplayTriggerSignalOrdering.UnorderedMultiset,
                    preservesOrderedSequence: false);
            }

            var document = new GameplayTriggerAuthoringDocument(
                GameplayTriggerConfigSchemas.AuthoringId,
                GameplayTriggerConfigSchemas.AuthoringVersion,
                new[]
                {
                    new GameplayTriggerSourceAuthoring(
                        1,
                        GameplayTriggerSourceKind.Occupancy,
                        initialSamplePolicy:
                            GameplayTriggerInitialSamplePolicy.EmitEnteredIfActive,
                        departurePolicy:
                            GameplayTriggerSubjectDeparturePolicy.EmitExited)
                },
                new[]
                {
                    new GameplayTriggerConditionAuthoring(
                        1,
                        new GameplayTriggerConditionNodeAuthoring(
                            GameplayTriggerConditionOperation.PushTrue))
                },
                new[]
                {
                    new GameplayTriggerDefinitionAuthoring(
                        1,
                        1,
                        1,
                        new[]
                        {
                            new GameplayTriggerRouteAuthoring(
                                1,
                                GameplayTriggerOccurrenceKind.Entered |
                                    GameplayTriggerOccurrenceKind.Stayed |
                                    GameplayTriggerOccurrenceKind.Exited,
                                1,
                                new[]
                                {
                                    new GameplayTriggerActionAuthoring(
                                        ActionKindId.Value,
                                        GameplayTriggerTargetSelector.Subject)
                                },
                                GameplayTriggerStateScope.PerSubject,
                                maxSubjects)
                        })
                },
                bindings);
            Assert.That(
                GameplayTriggerRetiredIdRegistry.TryCreate(
                    null,
                    out GameplayTriggerRetiredIdRegistry retired,
                    out GameplayTriggerConfigRegistryResult retiredResult),
                Is.True,
                Describe(retiredResult));
            Assert.That(
                GameplayTriggerSourceBindingContractRegistry.TryCreate(
                    contracts,
                    out GameplayTriggerSourceBindingContractRegistry contractRegistry,
                    out GameplayTriggerConfigRegistryResult contractResult),
                Is.True,
                Describe(contractResult));
            Assert.That(
                GameplayTriggerActionRequirementRegistry.TryCreate(
                    new[]
                    {
                        new GameplayTriggerActionRequirementDescriptor(
                            ActionKindId,
                            GameplayTriggerDeliveryContract.SameFrameReserved)
                    },
                    out GameplayTriggerActionRequirementRegistry actionRegistry,
                    out GameplayTriggerConfigRegistryResult actionResult),
                Is.True,
                Describe(actionResult));
            Assert.That(
                GameplayTriggerCompilerEnvironment.TryCreate(
                    retired,
                    contractRegistry,
                    actionRegistry,
                    new GameplayTriggerExpectedCatalogIdentity(
                        GameplayTriggerConfigSchemas.RuntimeCatalogId,
                        GameplayTriggerConfigSchemas.RuntimeCatalogVersion),
                    out GameplayTriggerCompilerEnvironment environment,
                    out GameplayTriggerConfigRegistryResult environmentResult),
                Is.True,
                Describe(environmentResult));

            GameplayTriggerCompileResult compile = GameplayTriggerConfigCompiler.Compile(
                document,
                GameplayTriggerCompilerOptions.Default,
                environment);
            Assert.That(compile.Succeeded, Is.True, Describe(compile));
            GameplayTriggerRuntimeCapacityProfile profile = ExactProfile(
                compile.CompiledCatalog.CapacityRequirements);
            var sink = new RecordingSink(ActionKindId);
            GameplayTriggerCatalogBootstrapResult bootstrap =
                GameplayTriggerCatalogBootstrap.TryCreateCatalog(
                    compile.CompiledCatalog,
                    new[]
                    {
                        GameplayTriggerActionBootstrapBinding.FromSink(
                            sink)
                    },
                    profile);
            Assert.That(bootstrap.Succeeded, Is.True, Describe(bootstrap));
            return new TriggerAuthority(
                compile.CompiledCatalog,
                new GameplayTriggerRuntime(
                    bootstrap.Catalog,
                    bootstrap.RuntimeOptions.Value),
                sink);
        }

        private static GameplayTriggerRuntimeCapacityProfile ExactProfile(
            GameplayTriggerRuntimeCapacityRequirements required) =>
            new GameplayTriggerRuntimeCapacityProfile(
                required.RequiredInstances,
                required.MaxInputBatches,
                required.MaxInputItems,
                required.MaxRelations,
                required.MaxOccurrences,
                required.MaxCandidates,
                required.MaxRouteStates,
                required.MaxActivations,
                required.MaxIntents,
                required.MaxPreparedActions,
                required.MaxConditionStackDepth,
                required.MaxDeferredRecords,
                required.MaxReliableOutboxRecords);

        private Collider CreateCollider(string name, bool isTrigger)
        {
            var gameObject = new GameObject(name);
            _objects.Add(gameObject);
            BoxCollider collider = gameObject.AddComponent<BoxCollider>();
            collider.isTrigger = isTrigger;
            return collider;
        }

        private static GameplayTriggerUnityZoneHandle RegisterZone(
            GameplayTriggerUnityOccupancyAdapter adapter,
            GameplayTriggerSourceBindingId sourceBindingId,
            Collider collider)
        {
            Assert.That(
                adapter.TryRegisterZone(
                    sourceBindingId,
                    collider,
                    collider.gameObject.scene.handle.GetRawData(),
                    out GameplayTriggerUnityZoneHandle handle,
                    out GameplayTriggerUnityZoneRegistrationResult result),
                Is.True,
                result.Status.ToString());
            return handle;
        }

        private static GameplayTriggerUnitySubjectHandle BindSubject(
            GameplayTriggerUnityOccupancyAdapter adapter,
            GameplayEntityId entityId,
            Collider[] colliders)
        {
            Assert.That(
                adapter.TryBindSubject(
                    entityId,
                    colliders,
                    colliders[0].gameObject.scene.handle.GetRawData(),
                    out GameplayTriggerUnitySubjectHandle handle,
                    out GameplayTriggerUnitySubjectBindingResult result),
                Is.True,
                result.Status.ToString());
            return handle;
        }

        private static void TickFlush(AdapterComposition composition, long frame)
        {
            composition.FlushModule.Tick(new RuntimeTickContext(
                frame,
                0d,
                0d,
                RuntimeTickStage.PreSimulation));
        }

        private static void TickGameplay(AdapterComposition composition, long frame)
        {
            composition.GameplayModule.Tick(new RuntimeTickContext(
                frame,
                0d,
                0d,
                RuntimeTickStage.Simulation));
        }

        private static void AssertSubmittedSubject(
            AdapterComposition composition,
            int commandIndex,
            GameplayEntityId expected)
        {
            RuntimeCommandBufferSnapshot snapshot = composition.CommandBuffer.CaptureSnapshot();
            Assert.That(snapshot.PendingCount, Is.GreaterThan(commandIndex));
            Assert.That(
                GameplayTriggerOccupancyCommandCodec.TryDecodeRecord(
                    snapshot.PendingCommands[commandIndex],
                    out GameplayTriggerOccupancyCommandRecord record,
                    out GameplayTriggerOccupancyCommandCodecStatus status),
                Is.True,
                status.ToString());
            Assert.That(record.ItemCount, Is.EqualTo(1));
            Assert.That(record.Subject, Is.EqualTo(expected));
        }

        private static void AssertSubmittedEmpty(
            AdapterComposition composition,
            int commandIndex)
        {
            RuntimeCommandBufferSnapshot snapshot = composition.CommandBuffer.CaptureSnapshot();
            Assert.That(snapshot.PendingCount, Is.GreaterThan(commandIndex));
            Assert.That(
                GameplayTriggerOccupancyCommandCodec.TryDecodeRecord(
                    snapshot.PendingCommands[commandIndex],
                    out GameplayTriggerOccupancyCommandRecord record,
                    out GameplayTriggerOccupancyCommandCodecStatus status),
                Is.True,
                status.ToString());
            Assert.That(record.IsEmptyBatch, Is.True);
        }

        private static long ComputeAuthorityHash(
            GameplayTriggerRuntime runtime,
            RuntimeFrame frame)
        {
            var accumulator = new RuntimeHashAccumulator(RuntimeHashSchemaIds.GameplayTriggers, RuntimeHashSchemaIds.Version1);
            new GameplayTriggerRuntimeHashContributor(runtime).Contribute(
                new RuntimeHashContext(frame),
                accumulator);
            return accumulator.ToHash();
        }

        private static void AssertOccurrence(
            GameplayTriggerRuntime runtime,
            GameplayTriggerOccurrenceKind expectedKind,
            GameplayEntityId expectedSubject)
        {
            Assert.That(runtime.OccurrenceCount, Is.EqualTo(1));
            Assert.That(runtime.GetOccurrence(0).Kind, Is.EqualTo(expectedKind));
            Assert.That(runtime.GetOccurrence(0).Subject, Is.EqualTo(expectedSubject));
        }

        private static GameplayTriggerUnityOccupancyDescriptor Descriptor(
            int sourceBindingId,
            int providerId,
            int maxSubjects) =>
            new GameplayTriggerUnityOccupancyDescriptor(
                SourceBinding(sourceBindingId),
                new GameplayTriggerSourceProviderId(providerId),
                maxSubjects);

        private static GameplayTriggerSourceBindingId SourceBinding(int value) =>
            new GameplayTriggerSourceBindingId(value);

        private static string Describe(GameplayTriggerUnityCatalogResult result) =>
            result.ErrorCode + " @ " + result.DescriptorIndex +
            " (" + result.Expected + "," + result.Actual + ")";

        private static string Describe(GameplayTriggerUnityInitializationResult result) =>
            result.ErrorCode + " (" + result.Expected + "," + result.Actual + ")";

        private static string Describe(GameplayTriggerConfigRegistryResult result) =>
            result.Succeeded || result.DiagnosticCount == 0
                ? "success"
                : result.GetDiagnostic(0).Code + " @ " + result.GetDiagnostic(0).Path;

        private static string Describe(GameplayTriggerCompileResult result) =>
            result.Succeeded || result.DiagnosticCount == 0
                ? "success"
                : result.GetDiagnostic(0).Code + " @ " + result.GetDiagnostic(0).Path;

        private static string Describe(GameplayTriggerCatalogBootstrapResult result) =>
            result.Succeeded || result.DiagnosticCount == 0
                ? "success"
                : result.GetDiagnostic(0).Code + " @ " + result.GetDiagnostic(0).Path;

        private sealed class RecordingSink : IGameplayTriggerActionSink
        {
            public RecordingSink(GameplayTriggerActionKindId actionKindId)
            {
                ActionKindId = actionKindId;
            }

            public GameplayTriggerActionKindId ActionKindId { get; }
            public GameplayTriggerDeliveryContract DeliveryContract =>
                GameplayTriggerDeliveryContract.SameFrameReserved;
            public int CommitCount { get; private set; }

            public bool TryPrepare(
                in GameplayTriggerActionPreparation preparation,
                out int reservationToken)
            {
                reservationToken = 1;
                return true;
            }

            public void Abort(int reservationToken)
            {
            }

            public void Commit(
                int reservationToken,
                in GameplayTriggerActionIntent intent)
            {
                CommitCount++;
            }
        }

        private readonly struct TriggerAuthority
        {
            public TriggerAuthority(
                GameplayTriggerCompiledCatalogPlan plan,
                GameplayTriggerRuntime runtime,
                RecordingSink actionSink)
            {
                Plan = plan;
                Runtime = runtime;
                ActionSink = actionSink;
            }

            public GameplayTriggerCompiledCatalogPlan Plan { get; }
            public GameplayTriggerRuntime Runtime { get; }
            public RecordingSink ActionSink { get; }
        }

        private readonly struct AdapterComposition
        {
            public AdapterComposition(
                TriggerAuthority authority,
                GameplayComponentWorld componentWorld,
                RuntimeCommandBuffer commandBuffer,
                GameplayRuntimeModule gameplayModule,
                GameplayTriggerUnityOccupancyAdapter adapter,
                GameplayTriggerUnityOccupancyFlushModule flushModule)
            {
                Authority = authority;
                ComponentWorld = componentWorld;
                CommandBuffer = commandBuffer;
                GameplayModule = gameplayModule;
                Adapter = adapter;
                FlushModule = flushModule;
            }

            public TriggerAuthority Authority { get; }
            public GameplayComponentWorld ComponentWorld { get; }
            public RuntimeCommandBuffer CommandBuffer { get; }
            public GameplayRuntimeModule GameplayModule { get; }
            public GameplayTriggerUnityOccupancyAdapter Adapter { get; }
            public GameplayTriggerUnityOccupancyFlushModule FlushModule { get; }
        }
    }
}
