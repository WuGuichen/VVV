using System.Reflection;
using MxFramework.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace MxFramework.Game.CharacterTest.Tests
{
    public sealed class CharacterTestRuntimeAiLabFrameSelectionTransactionTests
    {
        [Test]
        public void FrameZeroIsAValidCommittedSelectionTarget()
        {
            var target = new CharacterTestRuntimeAiLabFrameSelectionTarget(
                0,
                1,
                2L,
                3L,
                CharacterTestRuntimeAiLabSeekOrigin.Jump);

            Assert.That(target.IsValid, Is.True);
        }

        [Test]
        public void ScrubRestoreSerializesToFinalPointerTargetWithoutPublishingSupersededFrame()
        {
            var coordinator = new CharacterTestRuntimeAiLabFrameSelectionCoordinator();
            CharacterTestRuntimeAiLabFrameSelectionTarget frame10 = Target(10, 1);
            CharacterTestRuntimeAiLabFrameSelectionTarget frame11 = Target(11, 2);
            CharacterTestRuntimeAiLabFrameSelectionTarget frame12 = Target(12, 3);

            Assert.That(coordinator.BeginScrub(frame10, out string failure), Is.True, failure);
            Assert.That(coordinator.TryTakeNextRestore(out CharacterTestRuntimeAiLabFrameSelectionTarget active), Is.True);
            long transaction = coordinator.TransactionId;
            Assert.That(active.Frame, Is.EqualTo(10));
            Assert.That(coordinator.UpdateScrub(frame11, out failure), Is.True, failure);
            Assert.That(coordinator.EndScrub(frame12, out failure), Is.True, failure);

            CharacterTestRuntimeAiLabFrameSelectionCompletion first = coordinator.CompleteRestore(
                transaction,
                active,
                7,
                Bundle(10, 1),
                out CharacterTestRuntimeAiLabCommittedFrameReceipt receipt);

            Assert.That(first, Is.EqualTo(CharacterTestRuntimeAiLabFrameSelectionCompletion.Superseded));
            Assert.That(receipt.IsValid, Is.False);
            Assert.That(coordinator.TryTakeNextRestore(out active), Is.True);
            Assert.That(active.Frame, Is.EqualTo(12));

            CharacterTestRuntimeAiLabFrameSelectionCompletion final = coordinator.CompleteRestore(
                transaction,
                active,
                7,
                Bundle(12, 3),
                out receipt);

            Assert.That(final, Is.EqualTo(CharacterTestRuntimeAiLabFrameSelectionCompletion.Committed));
            Assert.That(receipt.Frame, Is.EqualTo(12));
            Assert.That(receipt.CommitGeneration, Is.EqualTo(1));
        }

        [Test]
        public void ExplicitSeekCannotPolluteActiveScrubOrLockedSession()
        {
            var coordinator = new CharacterTestRuntimeAiLabFrameSelectionCoordinator();
            Assert.That(coordinator.BeginScrub(Target(10, 1), out _), Is.True);

            Assert.That(coordinator.TryBeginExplicit(Target(20, 2), out string failure), Is.False);
            Assert.That(failure, Is.EqualTo(CharacterTestRuntimeAiLabFrameSelectionFailureCodes.SeekBusy));

            coordinator.BeginRecovery();
            coordinator.CompleteRecovery(success: false);
            Assert.That(coordinator.TryBeginExplicit(Target(20, 2), out failure), Is.False);
            Assert.That(failure, Is.EqualTo(CharacterTestRuntimeAiLabFrameSelectionFailureCodes.SessionLocked));
        }

        [Test]
        public void StaleAgentOrRecordGenerationCannotCommit()
        {
            var coordinator = new CharacterTestRuntimeAiLabFrameSelectionCoordinator();
            CharacterTestRuntimeAiLabFrameSelectionTarget target = Target(10, 4, agent: 101);
            Assert.That(coordinator.TryBeginExplicit(target, out _), Is.True);
            Assert.That(coordinator.TryTakeNextRestore(out CharacterTestRuntimeAiLabFrameSelectionTarget active), Is.True);

            CharacterTestRuntimeAiLabFrameSelectionCompletion result = coordinator.CompleteRestore(
                coordinator.TransactionId,
                Target(10, 4, agent: 202),
                202,
                Bundle(10, 4),
                out _);
            Assert.That(result, Is.EqualTo(CharacterTestRuntimeAiLabFrameSelectionCompletion.Rejected));

            result = coordinator.CompleteRestore(
                coordinator.TransactionId,
                active,
                101,
                Bundle(10, 5),
                out _);
            Assert.That(result, Is.EqualTo(CharacterTestRuntimeAiLabFrameSelectionCompletion.Rejected));
        }

        [Test]
        public void SameFrameWithDifferentAgentProducesNewCommitGeneration()
        {
            var coordinator = new CharacterTestRuntimeAiLabFrameSelectionCoordinator();
            Commit(coordinator, Target(10, 1, agent: 101), Bundle(10, 1), 101);
            long firstGeneration = coordinator.CommittedReceipt.CommitGeneration;
            Commit(coordinator, Target(10, 1, agent: 202), Bundle(10, 1), 202);

            Assert.That(coordinator.CommittedReceipt.CommitGeneration, Is.EqualTo(firstGeneration + 1));
            Assert.That(coordinator.CommittedReceipt.ResolvedAgentEntityId, Is.EqualTo(202));
        }

        [Test]
        public void RestoreReceiptPreservesPreferredAgentWhenFallbackResolvesAnotherAgent()
        {
            var coordinator = new CharacterTestRuntimeAiLabFrameSelectionCoordinator();
            CharacterTestRuntimeAiLabFrameSelectionTarget target = Target(10, 1, agent: 101);
            Assert.That(coordinator.TryBeginExplicit(target, out _), Is.True);
            Assert.That(coordinator.TryTakeNextRestore(out var active), Is.True);
            Assert.That(
                coordinator.CompleteRestore(
                    coordinator.TransactionId,
                    active,
                    202,
                    Bundle(10, 1),
                    out _),
                Is.EqualTo(CharacterTestRuntimeAiLabFrameSelectionCompletion.Committed));
            Assert.That(coordinator.CommittedReceipt.PreferredAgentEntityId, Is.EqualTo(101));
            Assert.That(coordinator.CommittedReceipt.ResolvedAgentEntityId, Is.EqualTo(202));
        }

        [Test]
        public void SameFullIdentityIsNoOpButChangedContentCreatesCommit()
        {
            var coordinator = new CharacterTestRuntimeAiLabFrameSelectionCoordinator();
            CharacterTestRuntimeAiLabFrameSelectionTarget target = Target(10, 1, agent: 101);
            CharacterTestRuntimeAiLabCommittedFrameBundle original = Bundle(10, 1);
            Commit(coordinator, target, original, 101);
            long generation = coordinator.CommittedReceipt.CommitGeneration;

            Assert.That(coordinator.TryBeginExplicit(target, out _), Is.True);
            Assert.That(coordinator.TryTakeNextRestore(out CharacterTestRuntimeAiLabFrameSelectionTarget active), Is.True);
            Assert.That(
                coordinator.CompleteRestore(coordinator.TransactionId, active, 101, original, out _),
                Is.EqualTo(CharacterTestRuntimeAiLabFrameSelectionCompletion.Unchanged));
            Assert.That(coordinator.CommittedReceipt.CommitGeneration, Is.EqualTo(generation));

            CharacterTestRuntimeAiLabCommittedFrameBundle changed = Bundle(10, 2);
            CharacterTestRuntimeAiLabFrameSelectionTarget changedTarget = Target(10, 2, agent: 101);
            Commit(coordinator, changedTarget, changed, 101);
            Assert.That(coordinator.CommittedReceipt.CommitGeneration, Is.EqualTo(generation + 1));
        }

        [Test]
        public void ChangedPreferredAgentCreatesCommitWhenResolvedAgentIsUnchanged()
        {
            var coordinator = new CharacterTestRuntimeAiLabFrameSelectionCoordinator();
            CharacterTestRuntimeAiLabCommittedFrameBundle bundle = Bundle(10, 1);
            Commit(coordinator, Target(10, 1, agent: 101), bundle, 202);
            long generation = coordinator.CommittedReceipt.CommitGeneration;

            CharacterTestRuntimeAiLabFrameSelectionTarget changedPreferred =
                Target(10, 1, agent: 303);
            Commit(coordinator, changedPreferred, bundle, 202);

            Assert.That(coordinator.CommittedReceipt.CommitGeneration, Is.EqualTo(generation + 1));
            Assert.That(coordinator.CommittedReceipt.PreferredAgentEntityId, Is.EqualTo(303));
            Assert.That(coordinator.CommittedReceipt.ResolvedAgentEntityId, Is.EqualTo(202));
        }

        [Test]
        public void CompletionFromDifferentSessionGenerationIsRejected()
        {
            var coordinator = new CharacterTestRuntimeAiLabFrameSelectionCoordinator();
            CharacterTestRuntimeAiLabFrameSelectionTarget target = Target(10, 1, agent: 101);
            Assert.That(coordinator.TryBeginExplicit(target, out _), Is.True);
            Assert.That(coordinator.TryTakeNextRestore(out CharacterTestRuntimeAiLabFrameSelectionTarget active), Is.True);

            CharacterTestRuntimeAiLabFrameSelectionCompletion result = coordinator.CompleteRestore(
                coordinator.TransactionId,
                active,
                101,
                Bundle(10, 1, sessionGeneration: 2),
                out _);

            Assert.That(result, Is.EqualTo(CharacterTestRuntimeAiLabFrameSelectionCompletion.Rejected));
        }

        [Test]
        public void RejectedCompletionCanBePreflightedBeforePresentationMutation()
        {
            var coordinator = new CharacterTestRuntimeAiLabFrameSelectionCoordinator();
            CharacterTestRuntimeAiLabFrameSelectionTarget target = Target(10, 4, agent: 101);
            Assert.That(coordinator.TryBeginExplicit(target, out _), Is.True);
            Assert.That(coordinator.TryTakeNextRestore(out CharacterTestRuntimeAiLabFrameSelectionTarget active), Is.True);

            bool presentationMutated = false;
            CharacterTestRuntimeAiLabCommittedFrameBundle staleBundle = Bundle(10, 5);
            if (coordinator.CanCompleteRestore(coordinator.TransactionId, active, staleBundle))
                presentationMutated = true;

            Assert.That(presentationMutated, Is.False,
                "A stale bundle must be rejected before the formal preview surface is touched.");
            Assert.That(
                coordinator.CompleteRestore(coordinator.TransactionId, active, 101, staleBundle, out _),
                Is.EqualTo(CharacterTestRuntimeAiLabFrameSelectionCompletion.Rejected));
        }

        [Test]
        public void PresentationContentHashTracksVisibleCharacterAndOverlayState()
        {
            CharacterTestRuntimeAiLabPresentationDiagnosticSample original = PresentationSample(
                actionKey: "Move",
                sourceX: 1f);
            CharacterTestRuntimeAiLabPresentationDiagnosticSample equivalent = PresentationSample(
                actionKey: "Move",
                sourceX: 1f);
            CharacterTestRuntimeAiLabPresentationDiagnosticSample changedAction = PresentationSample(
                actionKey: "Attack",
                sourceX: 1f);
            CharacterTestRuntimeAiLabPresentationDiagnosticSample changedOverlay = PresentationSample(
                actionKey: "Move",
                sourceX: 2f);

            long originalHash = CharacterTestRuntimeAiLabPresentationDiagnosticProbe.ComputeContentHash(original);
            Assert.That(
                CharacterTestRuntimeAiLabPresentationDiagnosticProbe.ComputeContentHash(equivalent),
                Is.EqualTo(originalHash));
            Assert.That(
                CharacterTestRuntimeAiLabPresentationDiagnosticProbe.ComputeContentHash(changedAction),
                Is.Not.EqualTo(originalHash));
            Assert.That(
                CharacterTestRuntimeAiLabPresentationDiagnosticProbe.ComputeContentHash(changedOverlay),
                Is.Not.EqualTo(originalHash));
        }

        [Test]
        public void TimelineRecordDefensivelyFreezesCommandAndAnimationArrays()
        {
            var commands = new[]
            {
                new RuntimeCommand(new RuntimeFrame(10), 1, 2, 3)
            };
            var inputs = new[]
            {
                new CharacterTestAnimationInputPresentationState(
                    7,
                    new CharacterTestCharacterAnimationComponent())
            };
            var record = new CharacterTestRuntimeAiPlannerDevTimelineRecord(
                10,
                0f,
                0d,
                CharacterTestRuntimeAiPlannerDevFrameSnapshot.Empty,
                commands,
                null,
                false,
                1000L,
                inputs,
                1L);

            commands[0] = new RuntimeCommand(new RuntimeFrame(10), 9, 9, 9);
            inputs[0] = new CharacterTestAnimationInputPresentationState(
                99,
                new CharacterTestCharacterAnimationComponent());

            Assert.That(record.DrainedCommands[0].SourceId, Is.EqualTo(1));
            Assert.That(record.AnimationInputs[0].EntityId, Is.EqualTo(7));
            RuntimeCommand[] exported = record.DrainedCommands.ToArray();
            exported[0] = new RuntimeCommand(new RuntimeFrame(10), 8, 8, 8);
            Assert.That(record.DrainedCommands[0].SourceId, Is.EqualTo(1));
        }

        [Test]
        public void ControllerRejectsAgentIngressWhileRestoreBusyOrSessionLocked()
        {
            var host = new GameObject("Runtime AI Lab Agent Ingress Gate Test");
            try
            {
                var controller = host.AddComponent<CharacterTestRuntimeAiPlannerDevSceneController>();
                var field = typeof(CharacterTestRuntimeAiPlannerDevSceneController).GetField(
                    "_frameSelection",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(field, Is.Not.Null);
                var coordinator = (CharacterTestRuntimeAiLabFrameSelectionCoordinator)field.GetValue(controller);
                Assert.That(coordinator.TryBeginExplicit(Target(10, 1), out _), Is.True);
                Assert.That(coordinator.TryTakeNextRestore(out _), Is.True);

                Assert.That(controller.SelectNextAgent(), Is.False);
                StringAssert.Contains("SEEK_BUSY", controller.StatusMessage);

                coordinator.BeginRecovery();
                coordinator.CompleteRecovery(success: false);
                Assert.That(controller.SelectPreviousAgent(), Is.False);
                StringAssert.Contains("SESSION_LOCKED", controller.StatusMessage);

                FieldInfo branchArmed = typeof(CharacterTestRuntimeAiPlannerDevSceneController).GetField(
                    "_branchRecomputeArmed",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(branchArmed, Is.Not.Null);
                branchArmed.SetValue(controller, true);
                Assert.That(controller.ToggleBranchRecompute(), Is.False);
                Assert.That(controller.BranchRecomputeArmed, Is.True,
                    "FailedLocked must reject the command before it mutates branch state.");
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        private static void Commit(
            CharacterTestRuntimeAiLabFrameSelectionCoordinator coordinator,
            CharacterTestRuntimeAiLabFrameSelectionTarget target,
            CharacterTestRuntimeAiLabCommittedFrameBundle bundle,
            int agent)
        {
            Assert.That(coordinator.TryBeginExplicit(target, out string failure), Is.True, failure);
            Assert.That(coordinator.TryTakeNextRestore(out CharacterTestRuntimeAiLabFrameSelectionTarget active), Is.True);
            Assert.That(
                coordinator.CompleteRestore(coordinator.TransactionId, active, agent, bundle, out _),
                Is.EqualTo(CharacterTestRuntimeAiLabFrameSelectionCompletion.Committed));
        }

        private static CharacterTestRuntimeAiLabFrameSelectionTarget Target(
            int frame,
            long generation,
            int agent = 7)
        {
            return new CharacterTestRuntimeAiLabFrameSelectionTarget(
                frame,
                agent,
                generation,
                1,
                CharacterTestRuntimeAiLabSeekOrigin.Scrub);
        }

        private static CharacterTestRuntimeAiLabCommittedFrameBundle Bundle(
            int frame,
            long generation,
            long sessionGeneration = 1)
        {
            var record = new CharacterTestRuntimeAiPlannerDevTimelineRecord(
                frame,
                0f,
                0d,
                CharacterTestRuntimeAiPlannerDevFrameSnapshot.Empty,
                null,
                null,
                false,
                frame * 100L,
                null,
                generation);
            return new CharacterTestRuntimeAiLabCommittedFrameBundle(
                new CharacterTestRuntimeAiLabTimelineSessionIdentity((int)sessionGeneration, 1, 900),
                record,
                fullRecordContentHash: generation * 17L,
                typedEventEvidenceHash: generation * 19L,
                appliedBehaviorRevisionHash: generation * 23L,
                presentationGeneration: generation,
                presentationHash: generation * 31L,
                presentationAvailable: true);
        }

        private static CharacterTestRuntimeAiLabPresentationDiagnosticSample PresentationSample(
            string actionKey,
            float sourceX)
        {
            var character = new CharacterTestRuntimeAiLabPresentationCharacterSample(
                entityId: 7,
                hasUnityView: true,
                unityViewXMillimeters: 1000,
                unityViewYMillimeters: 0,
                unityViewZMillimeters: 2000,
                unityViewYawMilliDegrees: 90000,
                presentationState: null,
                animationBlendXMilli: 100,
                animationBlendYMilli: 200,
                animationSpeed01Milli: 750,
                animationActionKey: actionKey,
                animationGrounded: true,
                animationAirborne: false);
            var overlay = new CharacterTestRuntimeAiLabFrameSyncOverlaySample(
                available: true,
                frame: 10,
                agentEntityId: 7,
                targetEntityId: 8,
                sourceCenter: new Vector3(sourceX, 0f, 0f),
                targetCenter: new Vector3(4f, 0f, 0f),
                moveEnd: new Vector3(3f, 0f, 0f),
                attackRange: 1.5f);
            return new CharacterTestRuntimeAiLabPresentationDiagnosticSample(
                new[] { character },
                default,
                overlay);
        }

        [Test]
        public void RejectedFrameRecoveryRestoresCommittedUnityViewRootPose()
        {
            var instance = new GameObject("Runtime AI Lab committed pose test");
            try
            {
                var entityId = new CharacterTestCharacterEntityId(7);
                var view = new CharacterTestRuntimeCharacterView(
                    entityId,
                    instance,
                    characterHandle: null,
                    resourceManager: null,
                    locomotionController: null);
                var bridge = new CharacterTestCharacterUnityViewBridge(
                    resources: null,
                    logger: null,
                    installDefaultProceduralHitFeedbackChain: false);
                FieldInfo viewsField = typeof(CharacterTestCharacterUnityViewBridge).GetField(
                    "_views",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(viewsField, Is.Not.Null);
                var views = (System.Collections.Generic.Dictionary<int, CharacterTestRuntimeCharacterView>)
                    viewsField.GetValue(bridge);
                views.Add(entityId.Value, view);

                instance.transform.SetPositionAndRotation(
                    new Vector3(8f, 0f, 9f),
                    Quaternion.Euler(0f, 12f, 0f));
                Assert.That(
                    bridge.TryRestoreRuntimeAiLabViewPose(
                        entityId,
                        new Vector3(1f, 2f, 3f),
                        Quaternion.Euler(0f, 90f, 0f)),
                    Is.True);

                Assert.That(instance.transform.position, Is.EqualTo(new Vector3(1f, 2f, 3f)));
                Assert.That(instance.transform.eulerAngles.y, Is.EqualTo(90f).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }
    }
}
