using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;

namespace MxFramework.Game.CharacterTest.Tests
{
    public sealed class CharacterTestRuntimeAiLabBehaviorCardStageEditingTests
    {
        [Test]
        public void Selection_UsesStableIdsAcrossRangeToggleAndReconcile()
        {
            var selection = new CharacterTestRuntimeAiLabStageSelectionState();
            CharacterTestRuntimeAiLabLogicStageViewModel[] stages = Views("a", "b", "c", "d");
            var identity = Identity(1);
            selection.Reconcile(identity, stages, stages.Length);

            Assert.IsTrue(selection.Select("b", CharacterTestRuntimeAiLabStageSelectionModifier.None, stages, stages.Length));
            Assert.IsTrue(selection.Select("d", CharacterTestRuntimeAiLabStageSelectionModifier.AddRange, stages, stages.Length));
            CollectionAssert.AreEqual(new[] { "b", "c", "d" }, Selected(selection));
            Assert.AreEqual("d", selection.PrimaryStageId);

            Assert.IsTrue(selection.Select("c", CharacterTestRuntimeAiLabStageSelectionModifier.Toggle, stages, stages.Length));
            CollectionAssert.AreEqual(new[] { "b", "d" }, Selected(selection));

            CharacterTestRuntimeAiLabLogicStageViewModel[] reordered = Views("d", "a", "b");
            selection.Reconcile(new CharacterTestRuntimeAiLabStageEditIdentity(
                1, "workspace", "group", "entry", "card", 2), reordered, reordered.Length);
            CollectionAssert.AreEqual(new[] { "d", "b" }, Selected(selection));
            Assert.AreEqual("d", selection.PrimaryStageId);
        }

        [Test]
        public void Selection_UsesSemanticOrderInsteadOfProjectionArrayOrder()
        {
            CharacterTestRuntimeAiLabLogicStageViewModel[] stages =
            {
                new CharacterTestRuntimeAiLabLogicStageViewModel { StageId = "c", Order = 2 },
                new CharacterTestRuntimeAiLabLogicStageViewModel { StageId = "a", Order = 0 },
                new CharacterTestRuntimeAiLabLogicStageViewModel { StageId = "d", Order = 3 },
                new CharacterTestRuntimeAiLabLogicStageViewModel { StageId = "b", Order = 1 }
            };
            var selection = new CharacterTestRuntimeAiLabStageSelectionState();
            selection.Reconcile(Identity(1), stages, stages.Length);

            Assert.IsTrue(selection.Select(
                "a",
                CharacterTestRuntimeAiLabStageSelectionModifier.None,
                stages,
                stages.Length));
            Assert.IsTrue(selection.Select(
                "d",
                CharacterTestRuntimeAiLabStageSelectionModifier.AddRange,
                stages,
                stages.Length));
            CollectionAssert.AreEqual(new[] { "a", "b", "c", "d" }, Selected(selection));

            Assert.IsTrue(selection.ReplaceFromStableIds(
                stages,
                stages.Length,
                new[] { "d", "b" },
                2,
                CharacterTestRuntimeAiLabStageSelectionModifier.None));
            CollectionAssert.AreEqual(new[] { "b", "d" }, Selected(selection));
            Assert.AreEqual("b", selection.PrimaryStageId);
        }

        [Test]
        public void Selection_AuthorityGenerationChangeClearsEvenWhenRouteAndRevisionMatch()
        {
            var selection = new CharacterTestRuntimeAiLabStageSelectionState();
            CharacterTestRuntimeAiLabLogicStageViewModel[] stages = Views("a", "b");
            CharacterTestRuntimeAiLabStageEditIdentity first = Identity(4);
            selection.Reconcile(first, stages, stages.Length);
            Assert.IsTrue(selection.Select(
                "b",
                CharacterTestRuntimeAiLabStageSelectionModifier.None,
                stages,
                stages.Length));
            Assert.IsTrue(selection.ToggleExpanded("b"));

            var replacement = new CharacterTestRuntimeAiLabStageEditIdentity(
                first.AuthorityGeneration + 1,
                first.WorkspaceId,
                first.GroupDraftId,
                first.EntryId,
                first.CardDraftId,
                first.DraftRevision);
            selection.Reconcile(replacement, stages, stages.Length);

            Assert.AreEqual(0, selection.Count);
            Assert.AreEqual(string.Empty, selection.PrimaryStageId);
            Assert.AreEqual(string.Empty, selection.ExpandedStageId);
        }

        [Test]
        public void Selection_ClearIsIdempotentAndClearsValidIdentity()
        {
            var selection = new CharacterTestRuntimeAiLabStageSelectionState();
            CharacterTestRuntimeAiLabLogicStageViewModel[] stages = Views("a");
            selection.Reconcile(Identity(1), stages, stages.Length);
            long beforeClear = selection.Revision;

            selection.Clear();

            Assert.AreEqual(0, selection.Count);
            Assert.AreEqual(string.Empty, selection.PrimaryStageId);
            Assert.AreEqual(beforeClear + 1, selection.Revision,
                "Clearing a valid editing identity must invalidate its presentation once.");

            selection.Clear();

            Assert.AreEqual(beforeClear + 1, selection.Revision,
                "Repeated cleanup of an already-empty selection must be idempotent.");
        }

        [Test]
        public void Selection_ClearWithoutStateKeepsRevisionStable()
        {
            var selection = new CharacterTestRuntimeAiLabStageSelectionState();
            long revision = selection.Revision;

            selection.Clear();

            Assert.AreEqual(revision, selection.Revision);
        }

        [Test]
        public void Selection_ReconcileWithoutPresentationChangeKeepsRevisionStable()
        {
            var selection = new CharacterTestRuntimeAiLabStageSelectionState();
            CharacterTestRuntimeAiLabLogicStageViewModel[] stages = Views("a", "b");
            CharacterTestRuntimeAiLabStageEditIdentity first = Identity(1);
            selection.Reconcile(first, stages, stages.Length);
            Assert.IsTrue(selection.Select(
                "b",
                CharacterTestRuntimeAiLabStageSelectionModifier.None,
                stages,
                stages.Length));
            Assert.IsTrue(selection.ToggleExpanded("b"));
            long selectedRevision = selection.Revision;

            selection.Reconcile(first, stages, stages.Length);
            Assert.AreEqual(selectedRevision, selection.Revision,
                "A stable refresh must not invalidate the retained Behavior Card projection.");

            var newerContent = new CharacterTestRuntimeAiLabStageEditIdentity(
                first.AuthorityGeneration,
                first.WorkspaceId,
                first.GroupDraftId,
                first.EntryId,
                first.CardDraftId,
                first.DraftRevision + 1);
            selection.Reconcile(newerContent, stages, stages.Length);
            Assert.AreEqual(selectedRevision, selection.Revision,
                "Content revision changes are already part of the Builder source revision.");
            Assert.AreEqual("b", selection.ExpandedStageId);
        }

        [Test]
        public void Marquee_ReplaceAddAndToggleRemainDeterministic()
        {
            var selection = new CharacterTestRuntimeAiLabStageSelectionState();
            CharacterTestRuntimeAiLabLogicStageViewModel[] stages = Views("a", "b", "c");
            selection.Reconcile(Identity(1), stages, stages.Length);
            Assert.IsTrue(selection.ReplaceFromOrderedMask(
                stages, stages.Length, new[] { true, false, true },
                CharacterTestRuntimeAiLabStageSelectionModifier.None));
            CollectionAssert.AreEqual(new[] { "a", "c" }, Selected(selection));

            Assert.IsTrue(selection.ReplaceFromOrderedMask(
                stages, stages.Length, new[] { false, true, false },
                CharacterTestRuntimeAiLabStageSelectionModifier.AddRange));
            CollectionAssert.AreEqual(new[] { "a", "b", "c" }, Selected(selection));

            Assert.IsTrue(selection.ReplaceFromOrderedMask(
                stages, stages.Length, new[] { true, false, true },
                CharacterTestRuntimeAiLabStageSelectionModifier.Toggle));
            CollectionAssert.AreEqual(new[] { "b" }, Selected(selection));
        }

        [Test]
        public void Gesture_PreviewsSnapWithoutMutatingAndRejectsStaleIdentity()
        {
            var selection = new CharacterTestRuntimeAiLabStageSelectionState();
            CharacterTestRuntimeAiLabLogicStageViewModel[] stages = Views("a", "b");
            stages[0].StartFrame = 10;
            stages[0].DurationFrames = 30;
            stages[1].StartFrame = 50;
            stages[1].DurationFrames = 20;
            var identity = Identity(1);
            selection.Reconcile(identity, stages, stages.Length);
            selection.Select("a", CharacterTestRuntimeAiLabStageSelectionModifier.None, stages, stages.Length);
            selection.Select("b", CharacterTestRuntimeAiLabStageSelectionModifier.Toggle, stages, stages.Length);

            var gesture = new CharacterTestRuntimeAiLabStageGestureSession();
            Assert.IsTrue(gesture.Begin(
                CharacterTestRuntimeAiLabStageGestureKind.Move,
                identity,
                selection,
                stages,
                stages.Length,
                "b",
                100f,
                2f,
                snapEnabled: true));
            long epoch = gesture.Epoch;
            CharacterTestRuntimeAiLabStageGesturePreview snapped = gesture.Update(identity, epoch, 131f, invertSnap: false);
            Assert.AreEqual(15, snapped.FrameDelta);
            CharacterTestRuntimeAiLabStageGesturePreview precise = gesture.Update(identity, epoch, 131f, invertSnap: true);
            Assert.AreEqual(16, precise.FrameDelta);
            Assert.AreEqual(10, stages[0].StartFrame, "Preview must not mutate the ViewModel.");
            Assert.IsFalse(gesture.Update(Identity(2), epoch, 131f, invertSnap: false).Active);
            Assert.IsFalse(gesture.Active, "An authority identity change must cancel the captured gesture.");
            Assert.IsFalse(gesture.TryFinish(identity, epoch, out _, out _));

            Assert.IsTrue(gesture.Begin(
                CharacterTestRuntimeAiLabStageGestureKind.Move,
                identity,
                selection,
                stages,
                stages.Length,
                "b",
                100f,
                2f,
                snapEnabled: false));
            long replacementEpoch = gesture.Epoch;
            Assert.IsFalse(gesture.Update(identity, epoch, 140f, invertSnap: false).Active);
            Assert.IsTrue(gesture.Active, "A late event from an older epoch must not cancel the replacement gesture.");
            Assert.IsTrue(gesture.Update(identity, replacementEpoch, 140f, invertSnap: false).Active);
            Assert.IsTrue(gesture.TryFinish(identity, replacementEpoch, out var kind, out int delta));
            Assert.AreEqual(CharacterTestRuntimeAiLabStageGestureKind.Move, kind);
            Assert.AreEqual(20, delta);
        }

        [Test]
        public void Gesture_ClampsMoveAndResizeAtTimelineBounds()
        {
            var selection = new CharacterTestRuntimeAiLabStageSelectionState();
            CharacterTestRuntimeAiLabLogicStageViewModel[] stages = Views("a");
            stages[0].StartFrame = 5;
            stages[0].DurationFrames = 20;
            var identity = Identity(1);
            selection.Reconcile(identity, stages, 1);
            selection.Select("a", CharacterTestRuntimeAiLabStageSelectionModifier.None, stages, 1);
            var gesture = new CharacterTestRuntimeAiLabStageGestureSession();

            Assert.IsTrue(gesture.Begin(
                CharacterTestRuntimeAiLabStageGestureKind.Move,
                identity, selection, stages, 1, "a", 0f, 1f, snapEnabled: false));
            Assert.AreEqual(-5, gesture.Update(identity, gesture.Epoch, -500f, false).FrameDelta);
            gesture.Cancel();

            Assert.IsTrue(gesture.Begin(
                CharacterTestRuntimeAiLabStageGestureKind.Resize,
                identity, selection, stages, 1, "a", 0f, 1f, snapEnabled: false));
            Assert.AreEqual(-19, gesture.Update(identity, gesture.Epoch, -500f, false).FrameDelta);
        }

        [Test]
        public void Gesture_PreviewsPrimaryActionConflictWithoutRunningValidator()
        {
            CharacterTestRuntimeAiLabLogicStageViewModel[] stages = Views("a", "b");
            stages[0].StartFrame = 0;
            stages[0].DurationFrames = 30;
            stages[0].OwnsPrimaryAction = true;
            stages[1].StartFrame = 40;
            stages[1].DurationFrames = 30;
            stages[1].OwnsPrimaryAction = true;
            var identity = Identity(1);
            var selection = new CharacterTestRuntimeAiLabStageSelectionState();
            selection.Reconcile(identity, stages, stages.Length);
            Assert.IsTrue(selection.Select(
                "a",
                CharacterTestRuntimeAiLabStageSelectionModifier.None,
                stages,
                stages.Length));

            var gesture = new CharacterTestRuntimeAiLabStageGestureSession();
            Assert.IsTrue(gesture.Begin(
                CharacterTestRuntimeAiLabStageGestureKind.Move,
                identity,
                selection,
                stages,
                stages.Length,
                "a",
                0f,
                1f,
                snapEnabled: false));
            long moveEpoch = gesture.Epoch;
            Assert.IsFalse(gesture.Update(identity, moveEpoch, 5f, invertSnap: false).HasActionConflict);
            Assert.IsTrue(gesture.Update(identity, moveEpoch, 15f, invertSnap: false).HasActionConflict);
            gesture.Cancel();

            Assert.IsTrue(gesture.Begin(
                CharacterTestRuntimeAiLabStageGestureKind.Resize,
                identity,
                selection,
                stages,
                stages.Length,
                "a",
                0f,
                1f,
                snapEnabled: false));
            Assert.IsTrue(gesture.Update(identity, gesture.Epoch, 15f, invertSnap: false).HasActionConflict);
        }

        [Test]
        public void Gesture_UpdateAndEdgeAutoScroll_AreNoAllocAfterWarmup()
        {
            var selection = new CharacterTestRuntimeAiLabStageSelectionState();
            CharacterTestRuntimeAiLabLogicStageViewModel[] stages = Views("a", "b");
            var identity = Identity(9);
            selection.Reconcile(identity, stages, stages.Length);
            selection.Select("a", CharacterTestRuntimeAiLabStageSelectionModifier.None, stages, stages.Length);
            selection.Select("b", CharacterTestRuntimeAiLabStageSelectionModifier.Toggle, stages, stages.Length);
            var gesture = new CharacterTestRuntimeAiLabStageGestureSession();
            Assert.IsTrue(gesture.Begin(
                CharacterTestRuntimeAiLabStageGestureKind.Move,
                identity,
                selection,
                stages,
                stages.Length,
                "a",
                100f,
                2f,
                snapEnabled: true));

            for (int i = 0; i < 32; i++)
            {
                gesture.Update(identity, gesture.Epoch, 100f + i, invertSnap: false);
                CharacterTestRuntimeAiLabStageEdgeAutoScroll.Advance(
                    120, 240, 0, 1870f, 1880f, 2f);
            }
            long before = GC.GetAllocatedBytesForCurrentThread();
            int viewportOffset = 0;
            for (int i = 0; i < 1000; i++)
            {
                gesture.Update(identity, gesture.Epoch, 100f + i % 180, invertSnap: (i & 1) != 0);
                viewportOffset = CharacterTestRuntimeAiLabStageEdgeAutoScroll.Advance(
                    120, 240, viewportOffset, 1870f, 1880f, 2f);
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.AreEqual(0L, allocated);
            Assert.Greater(viewportOffset, 0);
            Assert.AreEqual(
                0,
                CharacterTestRuntimeAiLabStageEdgeAutoScroll.Advance(
                    0, 240, 0, -10f, 1880f, 2f),
                "Left edge scrolling must clamp at frame zero.");
            Assert.AreEqual(
                viewportOffset,
                CharacterTestRuntimeAiLabStageEdgeAutoScroll.Advance(
                    120, 240, viewportOffset, 940f, 1880f, 2f),
                "The center of the viewport must not auto-scroll.");
        }

        [Test]
        public void GestureTooltipCache_IsBoundedAndUsesOverflowLabels()
        {
            var binder = new CharacterTestFairyGuiRuntimeAiLabBehaviorCardTimelineBinder(
                null,
                null,
                default);
            Type type = typeof(CharacterTestFairyGuiRuntimeAiLabBehaviorCardTimelineBinder);
            var normal = (string[])type.GetField(
                "_stageGesturePreviewTextCache",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(binder);
            var conflict = (string[])type.GetField(
                "_stageGestureConflictPreviewTextCache",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(binder);
            MethodInfo resolve = type.GetMethod(
                "ResolveStageGesturePreviewText",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var resolveText = (Func<int, bool, string>)resolve.CreateDelegate(
                typeof(Func<int, bool, string>),
                binder);

            Assert.AreEqual(1201, normal.Length);
            Assert.AreEqual(1201, conflict.Length);
            StringAssert.StartsWith("≥ +600f", resolveText(36000, false));
            StringAssert.Contains("动作冲突", resolveText(-36000, true));

            for (int i = 0; i < 32; i++)
                _ = resolveText((i & 1) == 0 ? i : -i, (i & 2) != 0);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1000; i++)
                _ = resolveText(i % 2401 - 1200, (i & 1) != 0);
            Assert.AreEqual(0L, GC.GetAllocatedBytesForCurrentThread() - before);
        }

        [Test]
        public void FieldEditSession_RequiresMatchingEpochIdentityStageAndField()
        {
            var session = new CharacterTestRuntimeAiLabStageFieldEditSession();
            var identity = Identity(5);
            Assert.IsTrue(session.Begin(
                identity,
                "a",
                CharacterTestRuntimeAiLabStageFieldKind.Title,
                out long epoch));
            Assert.IsTrue(session.CanCommit(
                identity, epoch, "a", CharacterTestRuntimeAiLabStageFieldKind.Title));
            Assert.IsFalse(session.CanCommit(
                Identity(6), epoch, "a", CharacterTestRuntimeAiLabStageFieldKind.Title));
            Assert.IsFalse(session.CanCommit(
                identity, epoch, "b", CharacterTestRuntimeAiLabStageFieldKind.Title));
            session.Cancel(deactivating: true);
            Assert.IsFalse(session.CanCommit(
                identity, epoch, "a", CharacterTestRuntimeAiLabStageFieldKind.Title));
        }

        [Test]
        public void Mutations_MoveResizeAndInvalidFieldAreAtomicOnProvidedPayload()
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = Card("a", "b");
            Assert.IsTrue(CharacterTestRuntimeAiLabBehaviorCardStageMutation.TryMoveStages(
                payload, new[] { "a", "b" }, 2, -20, out int applied, out _));
            Assert.AreEqual(-10, applied);
            Assert.AreEqual(0, payload.Stages[0].PlannedStartFrame);
            Assert.AreEqual(40, payload.Stages[1].PlannedStartFrame);

            Assert.IsTrue(CharacterTestRuntimeAiLabBehaviorCardStageMutation.TryResizeStage(
                payload, "a", 15, out int resize, out _));
            Assert.AreEqual(15, resize);
            Assert.AreEqual(45, payload.Stages[0].PlannedDurationFrames);
            payload.Stages[0].DurationMode = CharacterTestRuntimeAiLabLogicDurationMode.UntilEvent;
            Assert.IsFalse(CharacterTestRuntimeAiLabBehaviorCardStageMutation.TryResizeStage(
                payload, "a", 15, out _, out _));
            Assert.AreEqual(45, payload.Stages[0].PlannedDurationFrames);

            string oldTitle = payload.Stages[0].Title;
            Assert.IsFalse(CharacterTestRuntimeAiLabBehaviorCardStageMutation.TryApplyField(
                payload,
                "a",
                CharacterTestRuntimeAiLabStageFieldKind.Title,
                new CharacterTestRuntimeAiLabStageFieldValue(stringValue: "   "),
                out _));
            Assert.AreEqual(oldTitle, payload.Stages[0].Title);
        }

        [Test]
        public void FieldMutation_NoOpSucceedsWithoutChangingPayload()
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = Card("a");
            string before = CharacterTestRuntimeAiLabBehaviorCardLogicJson.Serialize(payload);

            Assert.IsTrue(CharacterTestRuntimeAiLabBehaviorCardStageMutation.TryApplyField(
                payload,
                "a",
                CharacterTestRuntimeAiLabStageFieldKind.Title,
                new CharacterTestRuntimeAiLabStageFieldValue(stringValue: "a"),
                out bool changed,
                out string diagnostic), diagnostic);

            Assert.IsFalse(changed);
            StringAssert.Contains("没有变化", diagnostic);
            Assert.AreEqual(before, CharacterTestRuntimeAiLabBehaviorCardLogicJson.Serialize(payload));
        }

        [Test]
        public void MovementTargetFields_AreEditableAndResetWhenStageStopsMoving()
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = Card("a");
            CharacterTestRuntimeAiLabLogicStage stage = payload.Stages[0];
            ApplyKind(payload, CharacterTestRuntimeAiLabCardStepKind.MoveToTarget);

            Assert.IsTrue(CharacterTestRuntimeAiLabBehaviorCardStageMutation.TryApplyField(
                payload,
                "a",
                CharacterTestRuntimeAiLabStageFieldKind.MovementStrategy,
                new CharacterTestRuntimeAiLabStageFieldValue(
                    movementStrategy: CharacterTestRuntimeAiLabMovementStrategy.OrbitTarget),
                out string diagnostic), diagnostic);
            Assert.IsTrue(CharacterTestRuntimeAiLabBehaviorCardStageMutation.TryApplyField(
                payload,
                "a",
                CharacterTestRuntimeAiLabStageFieldKind.MovementTargetKind,
                new CharacterTestRuntimeAiLabStageFieldValue(
                    movementTargetKind: CharacterTestRuntimeAiLabMovementTargetKind.WorldPosition),
                out diagnostic), diagnostic);
            Assert.IsTrue(CharacterTestRuntimeAiLabBehaviorCardStageMutation.TryApplyField(
                payload,
                "a",
                CharacterTestRuntimeAiLabStageFieldKind.MoveTargetPosition,
                new CharacterTestRuntimeAiLabStageFieldValue(floatValue: 12.5f, secondFloatValue: -3f),
                out diagnostic), diagnostic);

            Assert.AreEqual(CharacterTestRuntimeAiLabMovementStrategy.OrbitTarget, stage.MovementStrategy);
            Assert.AreEqual(CharacterTestRuntimeAiLabMovementTargetKind.WorldPosition, stage.MovementTargetKind);
            Assert.AreEqual(12.5f, stage.MoveTargetPositionX);
            Assert.AreEqual(-3f, stage.MoveTargetPositionZ);

            Assert.IsTrue(CharacterTestRuntimeAiLabBehaviorCardStageMutation.TryApplyField(
                payload,
                "a",
                CharacterTestRuntimeAiLabStageFieldKind.MovementTargetKind,
                new CharacterTestRuntimeAiLabStageFieldValue(
                    movementTargetKind: CharacterTestRuntimeAiLabMovementTargetKind.Entity),
                out diagnostic), diagnostic);
            Assert.IsFalse(CharacterTestRuntimeAiLabBehaviorCardStageMutation.TryApplyField(
                payload,
                "a",
                CharacterTestRuntimeAiLabStageFieldKind.MoveTargetEntityId,
                new CharacterTestRuntimeAiLabStageFieldValue(intValue: -1),
                out _));
            Assert.IsTrue(CharacterTestRuntimeAiLabBehaviorCardStageMutation.TryApplyField(
                payload,
                "a",
                CharacterTestRuntimeAiLabStageFieldKind.MoveTargetEntityId,
                new CharacterTestRuntimeAiLabStageFieldValue(intValue: 42),
                out diagnostic), diagnostic);
            Assert.AreEqual(42, stage.MoveTargetEntityId);

            ApplyKind(payload, CharacterTestRuntimeAiLabCardStepKind.Wait);
            Assert.AreEqual(CharacterTestRuntimeAiLabMovementStrategy.ApproachTarget, stage.MovementStrategy);
            Assert.AreEqual(CharacterTestRuntimeAiLabMovementTargetKind.CurrentCombatTarget, stage.MovementTargetKind);
            Assert.AreEqual(0f, stage.MoveTargetPositionX);
            Assert.AreEqual(0f, stage.MoveTargetPositionZ);
            Assert.AreEqual(0, stage.MoveTargetEntityId);
        }

        [Test]
        public void AtomicMovementDistanceEdit_KeepsCardActivationThresholdAligned()
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = Card("a");
            CharacterTestRuntimeAiLabLogicStage stage = payload.Stages[0];
            ApplyKind(payload, CharacterTestRuntimeAiLabCardStepKind.MoveToTarget);
            stage.MovementStrategy = CharacterTestRuntimeAiLabMovementStrategy.ApproachTarget;
            stage.MovementTargetKind = CharacterTestRuntimeAiLabMovementTargetKind.CurrentCombatTarget;
            stage.MoveMinDistance = 1.5f;
            stage.MoveMaxDistance = 999f;
            payload.ActivationConditions.Add(new CharacterTestRuntimeAiLabCardCondition
            {
                ConditionId = "activation.distance",
                Fact = CharacterTestRuntimeAiLabCardConditionFact.TargetDistance,
                Operator = CharacterTestRuntimeAiLabCardConditionOperator.GreaterThan,
                Value = 1.5f
            });

            Assert.IsTrue(CharacterTestRuntimeAiLabBehaviorCardStageMutation.TryApplyField(
                payload,
                "a",
                CharacterTestRuntimeAiLabStageFieldKind.MoveDistanceBand,
                new CharacterTestRuntimeAiLabStageFieldValue(floatValue: 2.25f, secondFloatValue: 999f),
                out string diagnostic), diagnostic);

            Assert.AreEqual(2.25f, stage.MoveMinDistance);
            Assert.AreEqual(2.25f, payload.ActivationConditions[0].Value);
        }

        [Test]
        public void MovementDistanceEdit_DoesNotRewriteNonAtomicCardActivation()
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = Card("a", "b");
            CharacterTestRuntimeAiLabLogicStage stage = payload.Stages[0];
            ApplyKind(payload, CharacterTestRuntimeAiLabCardStepKind.MoveToTarget);
            stage.MovementStrategy = CharacterTestRuntimeAiLabMovementStrategy.ApproachTarget;
            stage.MovementTargetKind = CharacterTestRuntimeAiLabMovementTargetKind.CurrentCombatTarget;
            stage.MoveMinDistance = 1.5f;
            stage.MoveMaxDistance = 999f;
            payload.ActivationConditions.Add(new CharacterTestRuntimeAiLabCardCondition
            {
                ConditionId = "activation.distance",
                Fact = CharacterTestRuntimeAiLabCardConditionFact.TargetDistance,
                Operator = CharacterTestRuntimeAiLabCardConditionOperator.GreaterThan,
                Value = 1.5f
            });

            Assert.IsTrue(CharacterTestRuntimeAiLabBehaviorCardStageMutation.TryApplyField(
                payload,
                "a",
                CharacterTestRuntimeAiLabStageFieldKind.MoveDistanceBand,
                new CharacterTestRuntimeAiLabStageFieldValue(floatValue: 2.25f, secondFloatValue: 999f),
                out string diagnostic), diagnostic);

            Assert.AreEqual(2.25f, stage.MoveMinDistance);
            Assert.AreEqual(1.5f, payload.ActivationConditions[0].Value);
        }

        [Test]
        public void KindAndLegacySelectors_CanonicalizeHiddenParameters()
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = Card("a");
            CharacterTestRuntimeAiLabLogicStage stage = payload.Stages[0];
            stage.Kind = CharacterTestRuntimeAiLabCardStepKind.LegacyMigrationOnly;
            stage.LegacyActionKind = CharacterTestRuntimeAiPlannerActionKind.CastAbilitySlot;
            stage.AbilityId = 42;
            stage.CharacterActionId = MxFramework.Tests.CharacterActionLegacyFixtureIds.From("stale");
            stage.MoveMinDistance = 3f;
            stage.MoveMaxDistance = 7f;
            stage.WaitFrames = 90;

            Assert.IsTrue(CharacterTestRuntimeAiLabBehaviorCardStageMutation.TryApplyField(
                payload,
                "a",
                CharacterTestRuntimeAiLabStageFieldKind.Kind,
                new CharacterTestRuntimeAiLabStageFieldValue(stageKind: CharacterTestRuntimeAiLabCardStepKind.UseAbility),
                out _));
            Assert.AreEqual(42, stage.AbilityId);
            Assert.AreEqual(string.Empty, stage.CharacterActionId);
            Assert.AreEqual(0f, stage.MoveMaxDistance);
            Assert.AreEqual(0, stage.WaitFrames);
            Assert.AreEqual(CharacterTestRuntimeAiPlannerActionKind.Wait, stage.LegacyActionKind);

            Assert.IsTrue(CharacterTestRuntimeAiLabBehaviorCardStageMutation.TryApplyField(
                payload,
                "a",
                CharacterTestRuntimeAiLabStageFieldKind.Kind,
                new CharacterTestRuntimeAiLabStageFieldValue(stageKind: CharacterTestRuntimeAiLabCardStepKind.LegacyMigrationOnly),
                out _));
            Assert.AreEqual(CharacterTestRuntimeAiPlannerActionKind.CastAbilitySlot, stage.LegacyActionKind);
            Assert.AreEqual(42, stage.AbilityId);
            Assert.IsTrue(CharacterTestRuntimeAiLabBehaviorCardStageMutation.TryApplyField(
                payload,
                "a",
                CharacterTestRuntimeAiLabStageFieldKind.LegacyActionKind,
                new CharacterTestRuntimeAiLabStageFieldValue(
                    legacyActionKind: CharacterTestRuntimeAiPlannerActionKind.MoveToTarget),
                out _));
            Assert.AreEqual(0, stage.AbilityId);
            Assert.AreEqual(0f, stage.MoveMinDistance);
            Assert.AreEqual(1.5f, stage.MoveMaxDistance);
        }

        [Test]
        public void AllStageKinds_CanonicalizeUnusedParametersDeterministically()
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = Card("a");
            CharacterTestRuntimeAiLabLogicStage stage = payload.Stages[0];
            stage.PlannedDurationFrames = 75;

            ApplyKind(payload, CharacterTestRuntimeAiLabCardStepKind.UseAbility);
            Assert.AreEqual(0, stage.AbilityId);
            AssertActionParametersReset(stage, allowAbility: true);

            stage.CharacterActionId = MxFramework.Tests.CharacterActionLegacyFixtureIds.From(" action.primary ");
            ApplyKind(payload, CharacterTestRuntimeAiLabCardStepKind.CharacterAction);
            Assert.AreEqual(string.Empty, stage.CharacterActionId,
                "Switching from another kind must not revive a stale action reference.");
            AssertActionParametersReset(stage, allowCharacterAction: true);

            ApplyKind(payload, CharacterTestRuntimeAiLabCardStepKind.MoveToTarget);
            Assert.AreEqual(0f, stage.MoveMinDistance);
            Assert.AreEqual(1.5f, stage.MoveMaxDistance);
            AssertActionParametersReset(stage, allowMoveBand: true);

            ApplyKind(payload, CharacterTestRuntimeAiLabCardStepKind.Wait);
            Assert.AreEqual(75, stage.WaitFrames);
            AssertActionParametersReset(stage, allowWait: true);

            ApplyKind(payload, CharacterTestRuntimeAiLabCardStepKind.Logic);
            AssertActionParametersReset(stage);

            stage.AbilityId = 19;
            ApplyKind(payload, CharacterTestRuntimeAiLabCardStepKind.UseAbility);
            stage.AbilityId = 19;
            ApplyKind(payload, CharacterTestRuntimeAiLabCardStepKind.LegacyMigrationOnly);
            Assert.AreEqual(CharacterTestRuntimeAiPlannerActionKind.CastAbilitySlot, stage.LegacyActionKind);
            Assert.AreEqual(19, stage.AbilityId);
            AssertActionParametersReset(stage, allowAbility: true, allowLegacyKind: true);
        }

        [Test]
        public void EveryLegacyActionSelector_ResetsHiddenParametersAndChangesSemanticHash()
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = Card("a");
            ApplyKind(payload, CharacterTestRuntimeAiLabCardStepKind.LegacyMigrationOnly);
            CharacterTestRuntimeAiLabLogicStage stage = payload.Stages[0];
            long previousHash = CompileMigratedHash(payload);
            CharacterTestRuntimeAiPlannerActionKind[] kinds =
            {
                CharacterTestRuntimeAiPlannerActionKind.MoveToTarget,
                CharacterTestRuntimeAiPlannerActionKind.Strafe,
                CharacterTestRuntimeAiPlannerActionKind.Retreat,
                CharacterTestRuntimeAiPlannerActionKind.PrimaryAttack,
                CharacterTestRuntimeAiPlannerActionKind.CastAbilitySlot,
                CharacterTestRuntimeAiPlannerActionKind.Dodge,
                CharacterTestRuntimeAiPlannerActionKind.Wait
            };

            for (int i = 0; i < kinds.Length; i++)
            {
                stage.AbilityId = 23;
                stage.CharacterActionId = MxFramework.Tests.CharacterActionLegacyFixtureIds.From("stale.action");
                stage.MoveMinDistance = 4f;
                stage.MoveMaxDistance = 8f;
                stage.WaitFrames = 91;
                Assert.IsTrue(CharacterTestRuntimeAiLabBehaviorCardStageMutation.TryApplyField(
                    payload,
                    "a",
                    CharacterTestRuntimeAiLabStageFieldKind.LegacyActionKind,
                    new CharacterTestRuntimeAiLabStageFieldValue(legacyActionKind: kinds[i]),
                    out string diagnostic), diagnostic);

                Assert.AreEqual(kinds[i], stage.LegacyActionKind);
                Assert.AreEqual(0, stage.AbilityId,
                    "Changing the legacy selector must not retain a value from a different legacy action.");
                Assert.AreEqual(string.Empty, stage.CharacterActionId);
                Assert.AreEqual(kinds[i] == CharacterTestRuntimeAiPlannerActionKind.MoveToTarget ? 1.5f : 0f,
                    stage.MoveMaxDistance);
                Assert.AreEqual(0, stage.WaitFrames);
                if (kinds[i] == CharacterTestRuntimeAiPlannerActionKind.CastAbilitySlot)
                {
                    stage.AbilityId = 1;
                    Assert.AreEqual(CharacterTestRuntimeAiPlannerActionKind.CastAbilitySlot, stage.LegacyActionKind);
                }
                long currentHash = CompileMigratedHash(payload);
                Assert.AreNotEqual(previousHash, currentHash,
                    "The selected legacy runtime action is behavior semantics and must affect the hash.");
                previousHash = currentHash;
            }
        }

        [Test]
        public void Duplicate_CopiesOnlySelectedStageSubgraphAndRemapsInternalTargets()
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = SubgraphCard();
            Assert.IsTrue(CharacterTestRuntimeAiLabBehaviorCardStageMutation.TryDuplicate(
                payload,
                new[] { "a", "b" },
                2,
                out string[] created,
                out string diagnostic), diagnostic);
            Assert.AreEqual(2, created.Length);
            Assert.AreEqual(5, payload.Stages.Count);
            Assert.AreEqual(5, payload.Detectors.Count, "Only two stage-scoped detectors are cloned.");
            Assert.AreEqual(5, payload.Transitions.Count, "Only transitions owned by cloned detectors are cloned.");

            CharacterTestRuntimeAiLabLogicDetector clonedA = FindDetectorForStage(payload, created[0]);
            CharacterTestRuntimeAiLabLogicDetector clonedB = FindDetectorForStage(payload, created[1]);
            Assert.IsNotNull(clonedA);
            Assert.IsNotNull(clonedB);
            CharacterTestRuntimeAiLabLogicTransition internalClone = FindTransition(payload, clonedA.DetectorId, created[1]);
            CharacterTestRuntimeAiLabLogicTransition externalClone = FindTransition(payload, clonedB.DetectorId, "c");
            Assert.IsNotNull(internalClone, "Internal selected target must be remapped.");
            Assert.IsNotNull(externalClone, "Outgoing target outside selection remains external.");
        }

        [Test]
        public void Duplicate_DraftInvalidDuplicateGraphIdsAreRejectedAtomically()
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload duplicateStage = Card("a", "b");
            duplicateStage.Stages.Add(duplicateStage.Stages[0].Clone());
            AssertDuplicateRejectedWithoutMutation(duplicateStage, "Stage");

            CharacterTestRuntimeAiLabBehaviorCardLogicPayload duplicateDetector = SubgraphCard();
            duplicateDetector.Detectors.Add(duplicateDetector.Detectors[0].Clone());
            AssertDuplicateRejectedWithoutMutation(duplicateDetector, "Detector");

            CharacterTestRuntimeAiLabBehaviorCardLogicPayload duplicateTransition = SubgraphCard();
            duplicateTransition.Transitions.Add(duplicateTransition.Transitions[0].Clone());
            AssertDuplicateRejectedWithoutMutation(duplicateTransition, "Transition");
        }

        [Test]
        public void Delete_RejectsEntryAndCascadesStageOwnedSubgraph()
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = SubgraphCard();
            Assert.IsFalse(CharacterTestRuntimeAiLabBehaviorCardStageMutation.TryDelete(
                payload.Clone(), new[] { "a" }, 1, out _, out _));

            Assert.IsTrue(CharacterTestRuntimeAiLabBehaviorCardStageMutation.TryDelete(
                payload, new[] { "b" }, 1, out string focus, out string diagnostic), diagnostic);
            Assert.AreEqual(2, payload.Stages.Count);
            Assert.AreEqual("c", focus);
            Assert.IsNull(FindStage(payload, "b"));
            Assert.IsNull(FindDetector(payload, "db"));
            Assert.IsNull(FindTransitionById(payload, "tb.out"));
            Assert.AreEqual(
                CharacterTestRuntimeAiLabLogicTransition.EndTarget,
                FindTransitionById(payload, "incoming").TargetStageId);
        }

        [Test]
        public void DraftWorkspace_OneMutationProducesUndoRedoAndNewMutationClearsRedo()
        {
            CharacterTestRuntimeAiLabDraftWorkspace workspace =
                CharacterTestRuntimeAiLabDraftWorkspace.Import(CharacterTestArenaMatchConfig.CreateDefault());
            Assert.IsTrue(workspace.TryGetGroupForSlot(1, 0, out CharacterTestRuntimeAiLabBehaviorGroupDraft group));
            int entryIndex = FindEditableEntryIndex(group);
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload before =
                workspace.Cards[group.Entries[entryIndex].CardDraftId].LogicPayload.Clone();
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload changed = before.Clone();
            changed.Stages[0].Title = "changed";
            Assert.IsTrue(workspace.TryReplaceCardLogicPayload(
                1, 0, entryIndex, changed, "编辑 Stage Title", out string message), message);
            Assert.IsTrue(workspace.CanUndo);
            Assert.IsFalse(workspace.CanRedo);
            Assert.IsTrue(workspace.TryUndo(out message), message);
            Assert.IsTrue(workspace.CanRedo);
            Assert.IsTrue(workspace.TryRedo(out message), message);
            Assert.AreEqual("changed", CurrentPayload(workspace, entryIndex).Stages[0].Title);
            Assert.IsTrue(workspace.TryUndo(out message), message);
            before.Stages[0].Note = "new branch";
            Assert.IsTrue(workspace.TryReplaceCardLogicPayload(
                1, 0, entryIndex, before, "编辑 Stage Note", out message), message);
            Assert.IsFalse(workspace.CanRedo);
        }

        [Test]
        public void PublishedPackage_ContainsUnifiedV8TimingAndConditionTreeControls()
        {
            string root = Directory.GetCurrentDirectory();
            string packageRoot = Path.Combine(root, "FGUIProject/assets/MxCharacterTestRuntimeAiLabUi");
            string package = File.ReadAllText(Path.Combine(packageRoot, "package.xml"));
            string timeline = File.ReadAllText(Path.Combine(
                packageRoot, "Components/RuntimeAiLabBehaviorTimeline.xml"));
            string workspace = File.ReadAllText(Path.Combine(
                packageRoot, "Components/RuntimeAiLabTimeline.xml"));
            string inspector = File.ReadAllText(Path.Combine(
                packageRoot, "Components/RuntimeAiLabBehaviorRecipeInspector.xml"));
            string actionTimelinePreview = File.ReadAllText(Path.Combine(
                packageRoot, "Components/RuntimeAiLabCharacterActionTimelinePreview.xml"));
            string actionWorkspace = File.ReadAllText(Path.Combine(
                packageRoot, "Components/RuntimeAiLabCharacterActionWorkspace.xml"));

            StringAssert.Contains("RuntimeAiLabTimelineTrack.xml", package);
            StringAssert.Contains("RuntimeAiLabTimelineSpan.xml", package);
            StringAssert.Contains("RuntimeAiLabTimelineMarker.xml", package);
            StringAssert.Contains("RuntimeAiLabBehaviorTriggerConditionRow.xml", package);
            StringAssert.Contains("RuntimeAiLabBehaviorRecipeInspector.xml", package);
            StringAssert.Contains("name=\"timelineGroupHost\"", timeline);
            StringAssert.Contains("name=\"timelineGroupPrototype\"", timeline);
            StringAssert.Contains("name=\"trackPrototype\"", timeline);
            StringAssert.Contains("name=\"spanPrototype\"", timeline);
            StringAssert.Contains("name=\"markerPrototype\"", timeline);
            StringAssert.Contains("name=\"timelinePlayhead\"", timeline);
            StringAssert.Contains("name=\"timelineHorizontalScrollTrack\"", timeline);
            StringAssert.Contains("name=\"timelineVerticalScrollTrack\"", timeline);
            StringAssert.DoesNotContain("name=\"stagePrototype\"", timeline);
            StringAssert.DoesNotContain("name=\"detectorPrototype\"", timeline);
            StringAssert.DoesNotContain("name=\"transitionPrototype\"", timeline);
            StringAssert.DoesNotContain("name=\"endPort\"", timeline);
            StringAssert.Contains("name=\"behaviorRecipeInspector\"", workspace);
            StringAssert.Contains("name=\"conditionList\"", inspector);
            StringAssert.Contains("name=\"btnDurationMode\"", inspector);
            StringAssert.Contains("name=\"durationFramesLabel\"", inspector);
            StringAssert.Contains("name=\"inputDurationFrames\"", inspector);
            StringAssert.Contains("name=\"btnOutcomeReturn\"", inspector);
            StringAssert.Contains("name=\"inputMaxExecutions\"", inspector);
            StringAssert.Contains("name=\"btnMaxExecutionsMinus\"", inspector);
            StringAssert.Contains("name=\"btnMaxExecutionsPlus\"", inspector);
            StringAssert.Contains("name=\"inputCooldown\"", inspector);
            StringAssert.Contains("name=\"btnCooldownMinus\"", inspector);
            StringAssert.Contains("name=\"btnCooldownPlus\"", inspector);
            StringAssert.Contains("name=\"btnMoveSpeedMinus\"", inspector);
            StringAssert.Contains("name=\"btnMoveSpeedPlus\"", inspector);
            StringAssert.Contains("name=\"moveSpeedHint\"", inspector);
            StringAssert.Contains("name=\"executionLimitHint\"", inspector);
            StringAssert.Contains("name=\"cooldownHint\"", inspector);
            StringAssert.Contains("name=\"btnCharacterActionParameter\"", inspector);
            StringAssert.Contains("name=\"btnViewCharacterActionTimeline\"", inspector);
            StringAssert.Contains("RuntimeAiLabCharacterActionPicker.xml", package);
            StringAssert.Contains("RuntimeAiLabCharacterActionPickerRow.xml", package);
            StringAssert.Contains("RuntimeAiLabCharacterActionTimelinePreview.xml", package);
            StringAssert.Contains("RuntimeAiLabCharacterActionTimelinePreviewRow.xml", package);
            StringAssert.Contains("name=\"trackList\"", actionTimelinePreview);
            StringAssert.Contains("name=\"btnEdit\"", actionTimelinePreview);
            StringAssert.Contains("name=\"btnRetry\"", actionTimelinePreview);
            StringAssert.Contains("RuntimeAiLabCharacterActionWorkspace.xml", package);
            StringAssert.Contains("RuntimeAiLabCharacterActionWorkspaceActionRow.xml", package);
            StringAssert.Contains("RuntimeAiLabCharacterActionWorkspaceTrackRow.xml", package);
            StringAssert.Contains("name=\"actionList\"", actionWorkspace);
            StringAssert.Contains("name=\"trackList\"", actionWorkspace);
            StringAssert.Contains("name=\"inspectorList\"", actionWorkspace);
            StringAssert.Contains("name=\"problemsList\"", actionWorkspace);
            StringAssert.Contains("name=\"btnPreviewPlay\"", actionWorkspace);
            StringAssert.Contains("name=\"scenePreviewGuide\"", actionWorkspace);
            StringAssert.Contains("name=\"previewStateOverlay\"", actionWorkspace);
            StringAssert.Contains("name=\"btnPreviewRetry\"", actionWorkspace);
            StringAssert.Contains("name=\"btnPreviewFit\"", actionWorkspace);
            StringAssert.Contains("name=\"sourceContextTitle\"", actionWorkspace);
            StringAssert.Contains("name=\"sourceContextCard\"", actionWorkspace);
            StringAssert.Contains("name=\"sourceContextRoute\"", actionWorkspace);
            StringAssert.Contains("name=\"btnSave\"", actionWorkspace);
            Assert.IsFalse(File.Exists(Path.Combine(
                packageRoot, "Components/RuntimeAiLabTimelineStageCard.xml")));
            Assert.IsFalse(File.Exists(Path.Combine(
                packageRoot, "Components/RuntimeAiLabTimelineDetectorBand.xml")));
            Assert.IsFalse(File.Exists(Path.Combine(
                packageRoot, "Components/RuntimeAiLabTimelineTransitionChip.xml")));
        }

        private static CharacterTestRuntimeAiLabStageEditIdentity Identity(int revision) =>
            new CharacterTestRuntimeAiLabStageEditIdentity(
                1, "workspace", "group", "entry", "card", revision);

        private static CharacterTestRuntimeAiLabLogicStageViewModel[] Views(params string[] ids)
        {
            var result = new CharacterTestRuntimeAiLabLogicStageViewModel[ids.Length];
            for (int i = 0; i < ids.Length; i++)
            {
                result[i] = new CharacterTestRuntimeAiLabLogicStageViewModel
                {
                    StageId = ids[i],
                    Order = i,
                    Title = ids[i],
                    StartFrame = i * 40,
                    DurationFrames = 30,
                    DurationMode = CharacterTestRuntimeAiLabLogicDurationMode.Fixed
                };
            }
            return result;
        }

        private static void AssertDuplicateRejectedWithoutMutation(
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload,
            string expectedKind)
        {
            string before = CharacterTestRuntimeAiLabBehaviorCardLogicJson.Serialize(payload);
            bool result = true;
            string[] created = null;
            string diagnostic = null;
            Assert.DoesNotThrow(() => result = CharacterTestRuntimeAiLabBehaviorCardStageMutation.TryDuplicate(
                payload,
                new[] { "a" },
                1,
                out created,
                out diagnostic));
            Assert.IsFalse(result);
            Assert.IsEmpty(created);
            StringAssert.Contains(expectedKind, diagnostic);
            Assert.AreEqual(before, CharacterTestRuntimeAiLabBehaviorCardLogicJson.Serialize(payload));
        }

        private static string[] Selected(CharacterTestRuntimeAiLabStageSelectionState selection)
        {
            var result = new string[selection.Count];
            for (int i = 0; i < result.Length; i++)
                result[i] = selection.GetSelectedId(i);
            return result;
        }

        private static CharacterTestRuntimeAiLabBehaviorCardLogicPayload Card(params string[] ids)
        {
            var payload = new CharacterTestRuntimeAiLabBehaviorCardLogicPayload
            {
                EntryStageId = ids[0]
            };
            for (int i = 0; i < ids.Length; i++)
            {
                payload.Stages.Add(new CharacterTestRuntimeAiLabLogicStage
                {
                    StageId = ids[i],
                    Order = i,
                    Title = ids[i],
                    Kind = CharacterTestRuntimeAiLabCardStepKind.Wait,
                    WaitFrames = 30,
                    PlannedStartFrame = 10 + i * 40,
                    PlannedDurationFrames = 30,
                    PreviewDurationFrames = 30,
                    DurationMode = CharacterTestRuntimeAiLabLogicDurationMode.Fixed
                });
                payload.Layout.Stages.Add(new CharacterTestRuntimeAiLabLogicStageLayout
                {
                    StageId = ids[i],
                    Column = i
                });
            }
            return payload;
        }

        private static void ApplyKind(
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload,
            CharacterTestRuntimeAiLabCardStepKind kind)
        {
            Assert.IsTrue(CharacterTestRuntimeAiLabBehaviorCardStageMutation.TryApplyField(
                payload,
                "a",
                CharacterTestRuntimeAiLabStageFieldKind.Kind,
                new CharacterTestRuntimeAiLabStageFieldValue(stageKind: kind),
                out string diagnostic), diagnostic);
        }

        private static long CompileHash(CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload)
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult result =
                CharacterTestRuntimeAiLabBehaviorCardLogicCompiler.Compile(payload);
            string diagnostic = string.Empty;
            for (int i = 0; i < result.Diagnostics.Count; i++)
            {
                if (i > 0)
                    diagnostic += " | ";
                diagnostic += result.Diagnostics[i].Code + ":" + result.Diagnostics[i].Message;
            }
            Assert.IsTrue(result.Success, "Expected canonical Stage payload to compile. " + diagnostic);
            return result.Program.SemanticHash;
        }

        private static long CompileMigratedHash(CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload)
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload migrated =
                CharacterTestRuntimeAiLabBehaviorCardLogicJson.Deserialize(
                    CharacterTestRuntimeAiLabBehaviorCardLogicJson.Serialize(payload));
            return CompileHash(migrated);
        }

        private static void AssertActionParametersReset(
            CharacterTestRuntimeAiLabLogicStage stage,
            bool allowAbility = false,
            bool allowCharacterAction = false,
            bool allowMoveBand = false,
            bool allowWait = false,
            bool allowLegacyKind = false)
        {
            if (!allowAbility)
                Assert.AreEqual(0, stage.AbilityId);
            if (!allowCharacterAction)
                Assert.AreEqual(string.Empty, stage.CharacterActionId);
            if (!allowMoveBand)
            {
                Assert.AreEqual(0f, stage.MoveMinDistance);
                Assert.AreEqual(0f, stage.MoveMaxDistance);
            }
            if (!allowWait)
                Assert.AreEqual(0, stage.WaitFrames);
            if (!allowLegacyKind)
                Assert.AreEqual(CharacterTestRuntimeAiPlannerActionKind.Wait, stage.LegacyActionKind);
        }

        private static CharacterTestRuntimeAiLabBehaviorCardLogicPayload SubgraphCard()
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = Card("a", "b", "c");
            payload.Detectors.Add(StageDetector("da", "a"));
            payload.Detectors.Add(StageDetector("db", "b"));
            payload.Detectors.Add(new CharacterTestRuntimeAiLabLogicDetector
            {
                DetectorId = "card",
                Title = "card",
                Scope = CharacterTestRuntimeAiLabLogicDetectorScope.Card,
                Priority = 1
            });
            payload.Transitions.Add(Transition("ta.internal", "da", "a", "b"));
            payload.Transitions.Add(Transition("tb.out", "db", "b", "c"));
            payload.Transitions.Add(Transition("incoming", "card", "c", "b"));
            return payload;
        }

        private static CharacterTestRuntimeAiLabLogicDetector StageDetector(string id, string stageId) =>
            new CharacterTestRuntimeAiLabLogicDetector
            {
                DetectorId = id,
                Title = id,
                Scope = CharacterTestRuntimeAiLabLogicDetectorScope.Stage,
                StageId = stageId,
                Priority = 1
            };

        private static CharacterTestRuntimeAiLabLogicTransition Transition(
            string id,
            string detectorId,
            string source,
            string target) =>
            new CharacterTestRuntimeAiLabLogicTransition
            {
                TransitionId = id,
                DetectorId = detectorId,
                SourceStageId = source,
                TargetStageId = target,
                Priority = 1,
                Policy = CharacterTestRuntimeAiLabLogicTransitionPolicy.Interrupt
            };

        private static int FindEditableEntryIndex(CharacterTestRuntimeAiLabBehaviorGroupDraft group)
        {
            for (int i = 0; i < group.Entries.Count; i++)
            {
                CharacterTestRuntimeAiPlannerBehaviorCoreSlotKind slot = group.Entries[i].CoreSlotKind;
                if (slot != CharacterTestRuntimeAiPlannerBehaviorCoreSlotKind.Targeting
                    && slot != CharacterTestRuntimeAiPlannerBehaviorCoreSlotKind.Fallback)
                {
                    return i;
                }
            }
            Assert.Fail("Expected the default behavior group to contain an editable card entry.");
            return -1;
        }

        private static CharacterTestRuntimeAiLabBehaviorCardLogicPayload CurrentPayload(
            CharacterTestRuntimeAiLabDraftWorkspace workspace,
            int entryIndex)
        {
            Assert.IsTrue(workspace.TryGetGroupForSlot(1, 0, out CharacterTestRuntimeAiLabBehaviorGroupDraft group));
            return workspace.Cards[group.Entries[entryIndex].CardDraftId].LogicPayload;
        }

        private static CharacterTestRuntimeAiLabLogicStage FindStage(
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload,
            string id)
        {
            for (int i = 0; i < payload.Stages.Count; i++)
            {
                if (string.Equals(payload.Stages[i].StageId, id, StringComparison.Ordinal))
                    return payload.Stages[i];
            }
            return null;
        }

        private static CharacterTestRuntimeAiLabLogicDetector FindDetector(
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload,
            string id)
        {
            for (int i = 0; i < payload.Detectors.Count; i++)
            {
                if (string.Equals(payload.Detectors[i].DetectorId, id, StringComparison.Ordinal))
                    return payload.Detectors[i];
            }
            return null;
        }

        private static CharacterTestRuntimeAiLabLogicDetector FindDetectorForStage(
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload,
            string stageId)
        {
            for (int i = 0; i < payload.Detectors.Count; i++)
            {
                CharacterTestRuntimeAiLabLogicDetector candidate = payload.Detectors[i];
                if (candidate.Scope == CharacterTestRuntimeAiLabLogicDetectorScope.Stage
                    && string.Equals(candidate.StageId, stageId, StringComparison.Ordinal))
                    return candidate;
            }
            return null;
        }

        private static CharacterTestRuntimeAiLabLogicTransition FindTransition(
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload,
            string detectorId,
            string targetStageId)
        {
            for (int i = 0; i < payload.Transitions.Count; i++)
            {
                CharacterTestRuntimeAiLabLogicTransition candidate = payload.Transitions[i];
                if (string.Equals(candidate.DetectorId, detectorId, StringComparison.Ordinal)
                    && string.Equals(candidate.TargetStageId, targetStageId, StringComparison.Ordinal))
                    return candidate;
            }
            return null;
        }

        private static CharacterTestRuntimeAiLabLogicTransition FindTransitionById(
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload,
            string id)
        {
            for (int i = 0; i < payload.Transitions.Count; i++)
            {
                if (string.Equals(payload.Transitions[i].TransitionId, id, StringComparison.Ordinal))
                    return payload.Transitions[i];
            }
            return null;
        }
    }
}
