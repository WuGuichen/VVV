using System;
using MxFramework.CharacterRuntimeSpawn;
using MxFramework.Game.CharacterTest;
using MxFramework.Resources;
using NUnit.Framework;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterTestBodyPartStateRuntimeTests
    {
        private const int OwnerEntityId = 7;

        [Test]
        public void Reinitialize_UsesBoundedOrdinalDefinitions_AndFailsClosedToZeroState()
        {
            var component = new CharacterTestBodyPartStateComponent(OwnerEntityId);
            Assert.AreEqual(
                CharacterTestBodyPartStateInitializationStatus.MissingDefinitions,
                component.Reinitialize(null));
            Assert.AreEqual(0, component.DefinitionCount);

            CharacterImportedGeometryBinding valid = CreateBinding(
                new[]
                {
                    CreateDefinition(0, 101, 201, "part.a", "head", 10, 10),
                    CreateDefinition(1, 102, 202, "part.b", "tail", 20, 20)
                },
                new[]
                {
                    new CharacterBodyPartDamageLookupRuntimeBinding(201, 0, "head"),
                    new CharacterBodyPartDamageLookupRuntimeBinding(202, 1, "tail")
                });
            Assert.AreEqual(
                CharacterTestBodyPartStateInitializationStatus.Ready,
                component.Reinitialize(valid));
            Assert.AreEqual(2, component.DefinitionCount);
            Assert.IsTrue(component.TryGetDefinition(0, out CharacterTestBodyPartStateDefinition first));
            Assert.AreEqual(101, first.PartStateId);
            Assert.AreEqual("part.a", valid.BodyPartDamageDefinitions[0].PartStateLabel);
            Assert.IsFalse(first.Severable, "M2 definitions without the optional extension stay non-severable.");
            Assert.IsTrue(component.TryResolveDefinitionIndexByPartStateId(102, out int partStateIndex));
            Assert.AreEqual(1, partStateIndex);
            Assert.AreEqual(
                CharacterTestBodyPartFilterStatus.Active,
                component.GetBodyPartFilterStatus(202));
            Assert.AreEqual(
                CharacterTestBodyPartDependencyStatus.Satisfied,
                component.GetPartStateDependencyStatus(102));
            Assert.IsTrue(component.TryResolveDefinitionIndex(
                202,
                out int definitionIndex,
                out CharacterTestBodyPartEvidenceReason reason));
            Assert.AreEqual(1, definitionIndex);
            Assert.AreEqual(CharacterTestBodyPartEvidenceReason.ExplicitBinding, reason);

            Assert.AreEqual(
                CharacterTestBodyPartStateInitializationStatus.InvalidBinding,
                component.Reinitialize(CreateBinding(
                    Array.Empty<CharacterBodyPartDamageRuntimeBinding>(),
                    Array.Empty<CharacterBodyPartDamageLookupRuntimeBinding>())));
            Assert.AreEqual(0, component.DefinitionCount);

            CharacterImportedGeometryBinding unordered = CreateBinding(
                new[]
                {
                    CreateDefinition(0, 102, 201, "part.b", "head", 10, 10),
                    CreateDefinition(1, 101, 202, "part.a", "tail", 20, 20)
                },
                new[]
                {
                    new CharacterBodyPartDamageLookupRuntimeBinding(201, 0, "head"),
                    new CharacterBodyPartDamageLookupRuntimeBinding(202, 1, "tail")
                });
            Assert.AreEqual(
                CharacterTestBodyPartStateInitializationStatus.InvalidBinding,
                component.Reinitialize(unordered));
            Assert.AreEqual(0, component.DefinitionCount);

            CharacterBodyPartDamageRuntimeBinding[] tooMany =
                new CharacterBodyPartDamageRuntimeBinding[9];
            CharacterBodyPartDamageLookupRuntimeBinding[] tooManyLookup =
                new CharacterBodyPartDamageLookupRuntimeBinding[9];
            for (int i = 0; i < tooMany.Length; i++)
            {
                string suffix = i.ToString("D2");
                tooMany[i] = CreateDefinition(i, 300 + i, 400 + i, "part." + suffix, "member." + suffix, 10, 10);
                tooManyLookup[i] = new CharacterBodyPartDamageLookupRuntimeBinding(
                    400 + i,
                    i);
            }
            Assert.AreEqual(
                CharacterTestBodyPartStateInitializationStatus.InvalidBinding,
                component.Reinitialize(CreateBinding(tooMany, tooManyLookup)));
            Assert.AreEqual(0, component.DefinitionCount);

            Assert.AreEqual(
                CharacterTestBodyPartStateInitializationStatus.Ready,
                component.Reinitialize(valid));
            Assert.IsTrue(component.TryGetCurrentEntry(0, out CharacterTestBodyPartStateEntry reset));
            Assert.AreEqual(CharacterTestBodyPartWoundState.Closed, reset.WoundState);
            Assert.AreEqual(0, reset.WoundValue);
            Assert.AreEqual(10, reset.CurrentIntegrity);
            Assert.AreEqual(0L, reset.Revision);
        }

        [Test]
        public void Sever_RequiresFrameStartBroken_CommitsOnceAndExposesStableReadSeams()
        {
            CharacterTestBodyPartStateComponent component = CreateReadyComponent(
                woundCapacity: 0,
                maxIntegrity: 10,
                severable: true,
                severRequiredTag: "action.test.tail.sever",
                presentationEventId: "event.test.tail.severed");

            Assert.IsTrue(component.TryBeginFrame(0, out _));
            Assert.AreEqual(
                CharacterTestBodyPartStateStageResult.Staged,
                component.StageAcceptedDamage(Damage(frame: 0, key: 1, damage: 10)));
            Assert.AreEqual(
                CharacterTestBodyPartStateStageResult.Staged,
                component.StageSever(Sever(frame: 0, correlation: 10)));
            CharacterTestBodyPartStateCommitResult breakFrame = component.CommitFrame();
            Assert.AreEqual(0, breakFrame.SeverFactCount,
                "A sever fact cannot consume Broken produced in the same authority frame.");
            Assert.IsTrue(component.TryGetCurrentEntry(0, out CharacterTestBodyPartStateEntry state));
            Assert.AreEqual(CharacterTestBodyPartStructureState.Broken, state.StructureState);

            Assert.IsTrue(component.TryBeginFrame(1, out _));
            CharacterTestBodyPartSeverFact sever = Sever(frame: 1, correlation: 11);
            Assert.AreEqual(CharacterTestBodyPartStateStageResult.Staged, component.StageSever(sever));
            Assert.AreEqual(CharacterTestBodyPartStateStageResult.Staged, component.StageSever(sever));
            CharacterTestBodyPartStateCommitResult severFrame = component.CommitFrame();
            Assert.AreEqual(CharacterTestBodyPartStateCommitStatus.Committed, severFrame.Status);
            Assert.AreEqual(1, severFrame.SeverFactCount);
            Assert.AreEqual(1, severFrame.TransitionCount);
            Assert.AreEqual(CharacterTestBodyPartTransitionReason.Severed,
                component.GetTransitionAt(0).Reason);
            Assert.IsTrue(component.TryGetCurrentEntry(0, out state));
            Assert.AreEqual(CharacterTestBodyPartStructureState.Severed, state.StructureState);
            Assert.AreEqual(
                CharacterTestBodyPartFilterStatus.Severed,
                component.GetBodyPartFilterStatus(301));
            Assert.AreEqual(
                CharacterTestBodyPartDependencyStatus.Severed,
                component.GetPartStateDependencyStatus(201));

            Assert.IsTrue(component.TryBeginFrame(2, out _));
            Assert.AreEqual(
                CharacterTestBodyPartStateStageResult.Staged,
                component.StageSever(Sever(frame: 2, correlation: 12)));
            CharacterTestBodyPartStateCommitResult repeated = component.CommitFrame();
            Assert.AreEqual(CharacterTestBodyPartStateCommitStatus.NoChanges, repeated.Status);
            Assert.AreEqual(0, repeated.SeverFactCount);
            Assert.AreEqual(0, repeated.TransitionCount);
        }

        [Test]
        public void SeverBridge_DeduplicatesProjectsOneEventAndRestoreDoesNotReplay()
        {
            CharacterTestBodyPartStateComponent intact = CreateReadyComponent(
                woundCapacity: 0,
                maxIntegrity: 10,
                severable: true,
                severRequiredTag: "action.test.tail.sever",
                presentationEventId: "event.test.tail.severed");
            var earlyBridge = new CharacterTestBodyPartDamageAuthorityBridge();
            Assert.IsTrue(earlyBridge.BeginAuthorityFrame(0));
            Assert.AreEqual(
                CharacterTestBodyPartSeverIngressResult.StateNotBrokenAtFrameStart,
                earlyBridge.StageAcceptedSever(intact, SeverIngress(frame: 0, correlation: 20)));
            Assert.IsTrue(earlyBridge.CompleteAuthorityFrame(0));
            Assert.AreEqual(0, earlyBridge.TransitionProjectionCount);

            CharacterTestBodyPartStateComponent broken = CreateReadyComponent(
                woundCapacity: 0,
                maxIntegrity: 10,
                severable: true,
                severRequiredTag: "action.test.tail.sever",
                presentationEventId: "event.test.tail.severed");
            CommitDamage(broken, frame: 0, key: 1, damage: 10);
            var bridge = new CharacterTestBodyPartDamageAuthorityBridge();
            Assert.IsTrue(bridge.BeginAuthorityFrame(1));
            CharacterTestBodyPartSeverIngress ingress = SeverIngress(frame: 1, correlation: 21);
            Assert.AreEqual(
                CharacterTestBodyPartSeverIngressResult.Staged,
                bridge.StageAcceptedSever(broken, ingress));
            Assert.AreEqual(
                CharacterTestBodyPartSeverIngressResult.Duplicate,
                bridge.StageAcceptedSever(broken, ingress));
            Assert.IsTrue(bridge.CompleteAuthorityFrame(1));
            Assert.AreEqual(1, bridge.TransitionProjectionCount);
            CharacterTestBodyPartTransitionProjection projection = bridge.GetTransitionProjectionAt(0);
            Assert.IsTrue(projection.HasSeveredPresentationEvent);
            Assert.AreEqual("event.test.tail.severed", projection.PresentationEventId);
            Assert.AreEqual(OwnerEntityId, projection.Transition.TargetEntityId);
            Assert.AreEqual(201, projection.Transition.PartStateId);
            Assert.AreEqual(projection.Transition.After.Revision, projection.AuthorityEventRevision);

            bridge.ResetForAuthorityRestore();
            Assert.AreEqual(0, bridge.TransitionProjectionCount,
                "Restore clears transient projections and must not replay the sever event.");
        }

        [Test]
        public void Reducer_ThresholdBeforeEqualAndOver_SaturatesAndAssignsAxisRevisions()
        {
            CharacterTestBodyPartStateComponent component = CreateReadyComponent(
                woundCapacity: 10,
                maxIntegrity: 10);

            CharacterTestBodyPartStateCommitResult before = CommitDamage(
                component,
                frame: 0,
                key: 1,
                damage: 9);
            Assert.AreEqual(CharacterTestBodyPartStateCommitStatus.Committed, before.Status);
            Assert.AreEqual(0, before.TransitionCount);
            Assert.IsTrue(component.TryGetCurrentEntry(0, out CharacterTestBodyPartStateEntry entry));
            Assert.AreEqual(9, entry.WoundValue);
            Assert.AreEqual(CharacterTestBodyPartWoundState.Closed, entry.WoundState);
            Assert.AreEqual(1, entry.CurrentIntegrity);
            Assert.AreEqual(CharacterTestBodyPartStructureState.Intact, entry.StructureState);
            Assert.AreEqual(1L, entry.Revision, "A value-only commit increments once.");

            Assert.IsTrue(component.TryBeginFrame(1, out _));
            CharacterTestBodyPartAcceptedDamageFact exact = Damage(frame: 1, key: 2, damage: 1);
            Assert.AreEqual(
                CharacterTestBodyPartStateStageResult.Staged,
                component.StageAcceptedDamage(exact));
            Assert.AreEqual(
                CharacterTestBodyPartStateStageResult.Staged,
                component.StageAcceptedDamage(exact),
                "An exact duplicate is accepted into staging and deduped by the reducer.");
            CharacterTestBodyPartStateCommitResult exactResult = component.CommitFrame();
            Assert.AreEqual(1, exactResult.AcceptedDamageFactCount);
            Assert.AreEqual(2, exactResult.TransitionCount);
            Assert.IsTrue(component.TryGetCurrentEntry(0, out entry));
            Assert.AreEqual(10, entry.WoundValue);
            Assert.AreEqual(CharacterTestBodyPartWoundState.Open, entry.WoundState);
            Assert.AreEqual(0, entry.CurrentIntegrity);
            Assert.AreEqual(CharacterTestBodyPartStructureState.Broken, entry.StructureState);
            Assert.AreEqual(3L, entry.Revision, "Open and Broken each advance revision once.");

            CharacterTestBodyPartStateTransition opened = component.GetTransitionAt(0);
            CharacterTestBodyPartStateTransition broken = component.GetTransitionAt(1);
            Assert.AreEqual(CharacterTestBodyPartTransitionReason.Opened, opened.Reason);
            Assert.AreEqual(2L, opened.After.Revision);
            Assert.AreEqual(CharacterTestBodyPartTransitionReason.Broken, broken.Reason);
            Assert.AreEqual(3L, broken.After.Revision);

            CharacterTestBodyPartStateComponent over = CreateReadyComponent(10, 10);
            CharacterTestBodyPartStateCommitResult overResult = CommitDamage(
                over,
                frame: 0,
                key: 1,
                damage: int.MaxValue);
            Assert.AreEqual(2, overResult.TransitionCount);
            Assert.IsTrue(over.TryGetCurrentEntry(0, out entry));
            Assert.AreEqual(10, entry.WoundValue);
            Assert.AreEqual(0, entry.CurrentIntegrity);
            Assert.AreEqual(2L, entry.Revision);
        }

        [Test]
        public void Reducer_FrameStartViewIsOld_DamageFirstAndConsumedRemainsTerminal()
        {
            CharacterTestBodyPartStateComponent component = CreateReadyComponent(
                woundCapacity: 10,
                maxIntegrity: 100,
                focusConsumeTag: "action.focus.finish");

            Assert.IsTrue(component.TryBeginFrame(0, out CharacterTestBodyPartStateReadView firstView));
            Assert.IsTrue(firstView.TryGet(0, out CharacterTestBodyPartStateEntry firstRead));
            Assert.AreEqual(CharacterTestBodyPartWoundState.Closed, firstRead.WoundState);
            Assert.AreEqual(
                CharacterTestBodyPartStateStageResult.Staged,
                component.StageFocusConsume(Focus(frame: 0, correlation: 1)));
            Assert.AreEqual(
                CharacterTestBodyPartStateStageResult.Staged,
                component.StageAcceptedDamage(Damage(frame: 0, key: 1, damage: 10)));
            CharacterTestBodyPartStateCommitResult opened = component.CommitFrame();
            Assert.AreEqual(1, opened.AcceptedDamageFactCount);
            Assert.AreEqual(0, opened.FocusConsumeFactCount);
            Assert.IsTrue(firstView.TryGet(0, out firstRead));
            Assert.AreEqual(CharacterTestBodyPartWoundState.Closed, firstRead.WoundState);
            Assert.IsTrue(component.TryGetCurrentEntry(0, out CharacterTestBodyPartStateEntry entry));
            Assert.AreEqual(CharacterTestBodyPartWoundState.Open, entry.WoundState);
            Assert.AreEqual(90, entry.CurrentIntegrity);

            Assert.IsTrue(component.TryBeginFrame(1, out CharacterTestBodyPartStateReadView secondView));
            Assert.IsTrue(secondView.TryGet(0, out CharacterTestBodyPartStateEntry secondRead));
            Assert.AreEqual(CharacterTestBodyPartWoundState.Open, secondRead.WoundState);
            Assert.AreEqual(
                CharacterTestBodyPartStateStageResult.Staged,
                component.StageFocusConsume(Focus(frame: 1, correlation: 2)));
            Assert.AreEqual(
                CharacterTestBodyPartStateStageResult.Staged,
                component.StageAcceptedDamage(Damage(frame: 1, key: 2, damage: 5)));
            CharacterTestBodyPartStateCommitResult consumed = component.CommitFrame();
            Assert.AreEqual(1, consumed.FocusConsumeFactCount);
            Assert.AreEqual(1, consumed.TransitionCount);
            Assert.IsTrue(component.TryGetCurrentEntry(0, out entry));
            Assert.AreEqual(CharacterTestBodyPartWoundState.Consumed, entry.WoundState);
            Assert.AreEqual(10, entry.WoundValue);
            Assert.AreEqual(85, entry.CurrentIntegrity, "Damage is reduced before consume.");

            CharacterTestBodyPartStateCommitResult afterConsumed = CommitDamage(
                component,
                frame: 2,
                key: 3,
                damage: int.MaxValue);
            Assert.AreEqual(1, afterConsumed.TransitionCount);
            Assert.IsTrue(component.TryGetCurrentEntry(0, out entry));
            Assert.AreEqual(CharacterTestBodyPartWoundState.Consumed, entry.WoundState);
            Assert.AreEqual(10, entry.WoundValue);
            Assert.AreEqual(CharacterTestBodyPartStructureState.Broken, entry.StructureState);
            Assert.AreEqual(CharacterTestBodyPartTransitionReason.Broken,
                component.GetTransitionAt(0).Reason);
        }

        [Test]
        public void Reducer_SortsAndDedupes_WhileConflictsAndOverflowCommitAtomically()
        {
            CharacterTestBodyPartStateComponent first = CreateReadyComponent(100, 0);
            CharacterTestBodyPartStateComponent second = CreateReadyComponent(100, 0);
            CharacterTestBodyPartAcceptedDamageFact keyOne = Damage(0, 1, 3);
            CharacterTestBodyPartAcceptedDamageFact keyTwo = Damage(0, 2, 7);
            CommitFacts(first, keyTwo, keyOne, keyOne);
            CommitFacts(second, keyOne, keyTwo, keyOne);
            Assert.IsTrue(first.TryGetCurrentEntry(0, out CharacterTestBodyPartStateEntry firstEntry));
            Assert.IsTrue(second.TryGetCurrentEntry(0, out CharacterTestBodyPartStateEntry secondEntry));
            Assert.AreEqual(10, firstEntry.WoundValue);
            Assert.AreEqual(firstEntry.WoundValue, secondEntry.WoundValue);
            Assert.AreEqual(firstEntry.Revision, secondEntry.Revision);

            CharacterTestBodyPartStateComponent conflict = CreateReadyComponent(100, 0);
            Assert.IsTrue(conflict.TryBeginFrame(0, out _));
            Assert.AreEqual(
                CharacterTestBodyPartStateStageResult.Staged,
                conflict.StageAcceptedDamage(Damage(0, 4, 2)));
            Assert.AreEqual(
                CharacterTestBodyPartStateStageResult.Staged,
                conflict.StageAcceptedDamage(Damage(0, 4, 3)));
            CharacterTestBodyPartStateCommitResult conflictResult = conflict.CommitFrame();
            Assert.AreEqual(CharacterTestBodyPartStateCommitStatus.ConflictingDuplicate, conflictResult.Status);
            Assert.AreEqual(0, conflictResult.TransitionCount);
            Assert.AreEqual(0L, conflict.Revision);
            Assert.IsTrue(conflict.TryGetCurrentEntry(0, out CharacterTestBodyPartStateEntry unchanged));
            Assert.AreEqual(0, unchanged.WoundValue);
            Assert.AreEqual(0L, unchanged.Revision);

            var overflow = new CharacterTestBodyPartStateComponent(
                OwnerEntityId,
                damageFactCapacity: 1,
                focusConsumeFactCapacity: 1);
            Assert.AreEqual(
                CharacterTestBodyPartStateInitializationStatus.Ready,
                overflow.Reinitialize(CreateSingleBinding(100, 0)));
            Assert.IsTrue(overflow.TryBeginFrame(0, out _));
            Assert.AreEqual(
                CharacterTestBodyPartStateStageResult.Staged,
                overflow.StageAcceptedDamage(Damage(0, 1, 2)));
            Assert.AreEqual(
                CharacterTestBodyPartStateStageResult.CapacityOverflow,
                overflow.StageAcceptedDamage(Damage(0, 2, 3)));
            CharacterTestBodyPartStateCommitResult overflowResult = overflow.CommitFrame();
            Assert.AreEqual(CharacterTestBodyPartStateCommitStatus.CapacityOverflow, overflowResult.Status);
            Assert.AreEqual(1L, overflow.TotalDamageOverflowCount);
            Assert.AreEqual(0L, overflow.Revision);
            Assert.AreEqual(0, overflow.TransitionCount);
            Assert.IsTrue(overflow.TryGetCurrentEntry(0, out unchanged));
            Assert.AreEqual(0, unchanged.WoundValue);
        }

        [Test]
        public void Reducer_FocusFactsDeduplicateAndFocusOverflowIsAtomic()
        {
            var component = new CharacterTestBodyPartStateComponent(
                OwnerEntityId,
                damageFactCapacity: 4,
                focusConsumeFactCapacity: 2);
            component.Reinitialize(CreateSingleBinding(1, 0, "action.focus.finish"));
            CommitDamage(component, 0, 1, 1);

            Assert.IsTrue(component.TryBeginFrame(1, out _));
            CharacterTestBodyPartFocusConsumeFact first = Focus(1, 10);
            CharacterTestBodyPartFocusConsumeFact second = Focus(1, 20);
            Assert.AreEqual(CharacterTestBodyPartStateStageResult.Staged,
                component.StageFocusConsume(second));
            Assert.AreEqual(CharacterTestBodyPartStateStageResult.Staged,
                component.StageFocusConsume(first));
            Assert.AreEqual(CharacterTestBodyPartStateStageResult.CapacityOverflow,
                component.StageFocusConsume(first));
            CharacterTestBodyPartStateCommitResult overflow = component.CommitFrame();
            Assert.AreEqual(CharacterTestBodyPartStateCommitStatus.CapacityOverflow, overflow.Status);
            Assert.IsTrue(component.TryGetCurrentEntry(0, out CharacterTestBodyPartStateEntry open));
            Assert.AreEqual(CharacterTestBodyPartWoundState.Open, open.WoundState);
            Assert.AreEqual(1L, open.Revision);

            Assert.IsTrue(component.TryBeginFrame(2, out _));
            CharacterTestBodyPartFocusConsumeFact duplicate = Focus(2, 30);
            Assert.AreEqual(CharacterTestBodyPartStateStageResult.Staged,
                component.StageFocusConsume(duplicate));
            Assert.AreEqual(CharacterTestBodyPartStateStageResult.Staged,
                component.StageFocusConsume(duplicate));
            CharacterTestBodyPartStateCommitResult deduped = component.CommitFrame();
            Assert.AreEqual(CharacterTestBodyPartStateCommitStatus.Committed, deduped.Status);
            Assert.AreEqual(1, deduped.FocusConsumeFactCount);
            Assert.AreEqual(1, deduped.TransitionCount);
            Assert.AreEqual(CharacterTestBodyPartTransitionReason.Consumed,
                component.GetTransitionAt(0).Reason);
            Assert.IsTrue(component.TryGetCurrentEntry(0, out CharacterTestBodyPartStateEntry consumed));
            Assert.AreEqual(CharacterTestBodyPartWoundState.Consumed, consumed.WoundState);
            Assert.AreEqual(2L, consumed.Revision);
        }

        [Test]
        public void SpawnReinitializeAndRespawnReset_DeathAndReviveDoNotImplicitlyReset()
        {
            const string dragonRoot = "Assets/MxFrameworkGenerated/CharacterPackages/000003";
            CharacterTestCharacterDefinition definition = new CharacterTestCharacterDefinition(
                characterId: 14014,
                displayName: "Dragon14",
                prefabKey: default(ResourceKey),
                defaultTeamId: 2,
                animationBinding: default,
                generatedPackageRoot: dragonRoot);
            var world = new CharacterTestCharacterRuntimeWorld();
            CharacterTestCharacterEntity entity = world.Spawn(
                new CharacterTestCharacterSpawnRequest(
                    characterId: definition.CharacterId,
                    teamId: 2,
                    x: 0f,
                    y: 0f,
                    z: 0f,
                    controlOwner: CharacterTestCharacterControlOwner.AiControlled),
                definition);

            Assert.AreEqual(CharacterTestBodyPartStateInitializationStatus.Ready,
                entity.BodyPartState.InitializationStatus);
            Assert.AreEqual(4, entity.BodyPartState.DefinitionCount);
            Assert.IsTrue(entity.BodyPartState.TryGetDefinition(0, out CharacterTestBodyPartStateDefinition part));
            Assert.AreEqual("partstate.000003.head", part.PartStateId);
            CommitDamage(
                entity.BodyPartState,
                0,
                1,
                Math.Max(1, part.WoundCapacity),
                entity.Id.Value);
            Assert.IsTrue(entity.BodyPartState.TryGetCurrentEntry(0, out CharacterTestBodyPartStateEntry changed));
            Assert.Greater(changed.Revision, 0L);

            entity.BeginDeath(frame: 1, sourceEntityId: 1, traceId: 1);
            Assert.IsTrue(entity.BodyPartState.TryGetCurrentEntry(0, out CharacterTestBodyPartStateEntry retained));
            Assert.AreEqual(changed.Revision, retained.Revision);
            entity.BeginRevive(frame: 2, health: 100);
            entity.CompleteRevive(frame: 3);
            Assert.IsTrue(entity.BodyPartState.TryGetCurrentEntry(0, out retained));
            Assert.AreEqual(changed.Revision, retained.Revision);

            CharacterImportedPackage package = CharacterTestGeneratedPackageSource.LoadPackage(dragonRoot);
            Assert.NotNull(package);
            Assert.AreEqual(CharacterTestBodyPartStateInitializationStatus.Ready,
                entity.ReinitializeBodyPartState(package.Geometry));
            Assert.IsTrue(entity.BodyPartState.TryGetCurrentEntry(0, out CharacterTestBodyPartStateEntry reset));
            Assert.AreEqual(0L, reset.Revision);
            Assert.AreEqual(0, reset.WoundValue);
            Assert.AreEqual(part.MaxIntegrity, reset.CurrentIntegrity);

            CommitDamage(
                entity.BodyPartState,
                4,
                2,
                Math.Max(1, part.WoundCapacity),
                entity.Id.Value);
            Assert.IsTrue(world.Remove(entity.Id));
            CharacterTestCharacterEntity respawned = world.Spawn(
                new CharacterTestCharacterSpawnRequest(
                    characterId: definition.CharacterId,
                    teamId: 2,
                    x: 0f,
                    y: 0f,
                    z: 0f,
                    controlOwner: CharacterTestCharacterControlOwner.AiControlled),
                definition);
            Assert.IsTrue(respawned.BodyPartState.TryGetCurrentEntry(0, out reset));
            Assert.AreEqual(0L, reset.Revision);
            Assert.AreEqual(CharacterTestBodyPartWoundState.Closed, reset.WoundState);
            Assert.AreEqual(CharacterTestBodyPartStructureState.Intact, reset.StructureState);
        }

        [Test]
        public void Reducer_WarmedBeginStageCommit_AllocatesZeroBytes()
        {
            CharacterTestBodyPartStateComponent component = CreateReadyComponent(
                woundCapacity: 1_000_000,
                maxIntegrity: 0);
            CommitDamage(component, 0, 0, 1);

            CharacterTestBodyPartStateStageResult stageResult = default;
            CharacterTestBodyPartStateCommitResult commitResult = default;
            _ = GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 1; i <= 4096; i++)
            {
                component.TryBeginFrame(i, out _);
                stageResult = component.StageAcceptedDamage(Damage(i, i, 1));
                commitResult = component.CommitFrame();
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.AreEqual(CharacterTestBodyPartStateStageResult.Staged, stageResult);
            Assert.AreEqual(CharacterTestBodyPartStateCommitStatus.Committed, commitResult.Status);
            Assert.AreEqual(0L, allocated);
        }

        private static CharacterTestBodyPartStateComponent CreateReadyComponent(
            int woundCapacity,
            int maxIntegrity,
            string focusConsumeTag = "action.focus.finish",
            bool severable = false,
            string severRequiredTag = "",
            string presentationEventId = "")
        {
            var component = new CharacterTestBodyPartStateComponent(OwnerEntityId);
            Assert.AreEqual(
                CharacterTestBodyPartStateInitializationStatus.Ready,
                component.Reinitialize(CreateSingleBinding(
                    woundCapacity,
                    maxIntegrity,
                    focusConsumeTag,
                    severable,
                    severRequiredTag,
                    presentationEventId)));
            return component;
        }

        private static CharacterImportedGeometryBinding CreateSingleBinding(
            int woundCapacity,
            int maxIntegrity,
            string focusConsumeTag = "action.focus.finish",
            bool severable = false,
            string severRequiredTag = "",
            string presentationEventId = "")
        {
            return CreateBinding(
                new[]
                {
                    CreateDefinition(
                        definitionIndex: 0,
                        partStateId: 201,
                        memberPartId: 301,
                        partStateLabel: "part.test",
                        memberPartLabel: "head",
                        woundCapacity: woundCapacity,
                        maxIntegrity: maxIntegrity,
                        focusConsumeTag: focusConsumeTag,
                        severable: severable,
                        severRequiredTag: severRequiredTag,
                        presentationEventId: presentationEventId)
                },
                new[] { new CharacterBodyPartDamageLookupRuntimeBinding(301, 0, "head") });
        }

        private static CharacterImportedGeometryBinding CreateBinding(
            CharacterBodyPartDamageRuntimeBinding[] definitions,
            CharacterBodyPartDamageLookupRuntimeBinding[] lookup)
        {
            return new CharacterImportedGeometryBinding(
                format: "mx.characterGeometryBinding.v5",
                schemaVersion: "5.0",
                packageId: "test.body-part-state",
                bodyProfile: null,
                bodyColliders: Array.Empty<CharacterBodyColliderRuntimeBinding>(),
                hitZoneBindings: Array.Empty<CharacterHitZoneRuntimeBinding>(),
                damageZones: Array.Empty<CharacterDamageZoneRuntimeBinding>(),
                sockets: Array.Empty<CharacterSocketRuntimeBinding>(),
                weaponAttachments: Array.Empty<CharacterWeaponAttachmentRuntimeBinding>(),
                weaponTraces: Array.Empty<CharacterWeaponTraceRuntimeBinding>(),
                bodyPartDamageDefinitions: definitions,
                bodyPartDamagePartLookup: lookup,
                bodyPartDamageFormat: "mx.characterBodyPartDamage.v1",
                bodyPartDamageSchemaVersion: "1.0",
                bodyPartDamageRevision: 1,
                bodyPartDamageContentHash: "sha256:test",
                bodyPartDamageMode: CharacterBodyPartDamageMode.Authored);
        }

        private static CharacterBodyPartDamageRuntimeBinding CreateDefinition(
            int definitionIndex,
            int partStateId,
            int memberPartId,
            string partStateLabel,
            string memberPartLabel,
            int woundCapacity,
            int maxIntegrity,
            string focusConsumeTag = "action.focus.finish",
            bool severable = false,
            string severRequiredTag = "",
            string presentationEventId = "")
        {
            return new CharacterBodyPartDamageRuntimeBinding(
                definitionIndex,
                partStateId,
                new[] { memberPartId },
                damageZoneId: 500 + memberPartId,
                woundCapacity,
                openDamageScaleMilli: woundCapacity > 0 ? 1_250 : 0,
                focusConsumeTag: focusConsumeTag,
                maxIntegrity: maxIntegrity,
                integrityDamageScaleMilli: maxIntegrity > 0 ? 1_000 : 0,
                breakReactionId: maxIntegrity > 0 ? "reaction.break" : string.Empty,
                authoredPriorityDeltaOpen: 10,
                authoredPriorityDeltaBroken: 20,
                severable: severable,
                severRequiredTag: severRequiredTag,
                presentationEventId: presentationEventId,
                partStateLabel: partStateLabel,
                damageZoneLabel: "zone." + memberPartLabel,
                memberPartLabels: new[] { memberPartLabel });
        }

        private static CharacterTestBodyPartAcceptedDamageFact Damage(
            long frame,
            int key,
            int damage,
            int targetEntityId = OwnerEntityId)
        {
            return new CharacterTestBodyPartAcceptedDamageFact(
                authorityFrame: frame,
                sourceEntityId: 1,
                targetEntityId: targetEntityId,
                actionInstanceId: key,
                policyEventId: key,
                traceId: key,
                localFrame: key,
                definitionIndex: 0,
                finalDamage: damage);
        }

        private static CharacterTestBodyPartFocusConsumeFact Focus(
            long frame,
            long correlation,
            string tag = "action.focus.finish")
        {
            return new CharacterTestBodyPartFocusConsumeFact(
                authorityFrame: frame,
                adapterKind: CharacterTestBodyPartFocusConsumeAdapterKind.Action,
                sourceEntityId: 1,
                targetEntityId: OwnerEntityId,
                definitionIndex: 0,
                focusConsumeTag: tag,
                correlationId: correlation);
        }

        private static CharacterTestBodyPartSeverFact Sever(
            long frame,
            long correlation,
            string tag = "action.test.tail.sever")
        {
            return new CharacterTestBodyPartSeverFact(
                authorityFrame: frame,
                sourceEntityId: 1,
                targetEntityId: OwnerEntityId,
                definitionIndex: 0,
                severTag: tag,
                correlationId: correlation);
        }

        private static CharacterTestBodyPartSeverIngress SeverIngress(
            long frame,
            long correlation,
            string tag = "action.test.tail.sever")
        {
            return new CharacterTestBodyPartSeverIngress(
                authorityFrame: frame,
                sourceEntityId: 1,
                targetEntityId: OwnerEntityId,
                partStateId: global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0058,
                severTag: tag,
                correlationId: correlation,
                authorityAccepted: true);
        }

        private static CharacterTestBodyPartStateCommitResult CommitDamage(
            CharacterTestBodyPartStateComponent component,
            long frame,
            int key,
            int damage,
            int targetEntityId = OwnerEntityId)
        {
            Assert.IsTrue(component.TryBeginFrame(frame, out _));
            Assert.AreEqual(
                CharacterTestBodyPartStateStageResult.Staged,
                component.StageAcceptedDamage(Damage(frame, key, damage, targetEntityId)));
            return component.CommitFrame();
        }

        private static void CommitFacts(
            CharacterTestBodyPartStateComponent component,
            params CharacterTestBodyPartAcceptedDamageFact[] facts)
        {
            Assert.IsTrue(component.TryBeginFrame(0, out _));
            for (int i = 0; i < facts.Length; i++)
            {
                Assert.AreEqual(
                    CharacterTestBodyPartStateStageResult.Staged,
                    component.StageAcceptedDamage(facts[i]));
            }

            CharacterTestBodyPartStateCommitResult result = component.CommitFrame();
            Assert.AreEqual(CharacterTestBodyPartStateCommitStatus.Committed, result.Status);
            Assert.AreEqual(2, result.AcceptedDamageFactCount);
        }
    }
}
