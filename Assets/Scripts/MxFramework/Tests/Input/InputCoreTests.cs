using System;
using System.Collections.Generic;
using MxFramework.Input;
using NUnit.Framework;
using UnityEngine;

namespace MxFramework.Tests.Input
{
    public sealed class InputCoreTests
    {
        [Test]
        public void ContextStack_OverlayKeepsLowerContexts()
        {
            var stack = new InputContextStack();
            var enabled = new List<InputContext>();

            stack.Set(InputContext.Gameplay);
            using (stack.Push(InputContext.UI, InputContextPolicy.Overlay))
            {
                stack.FillEnabledContexts(enabled);

                Assert.AreEqual(2, enabled.Count);
                Assert.AreEqual(InputContext.UI, enabled[0]);
                Assert.AreEqual(InputContext.Gameplay, enabled[1]);
                Assert.IsTrue(stack.IsContextEnabled(InputContext.UI));
                Assert.IsTrue(stack.IsContextEnabled(InputContext.Gameplay));
            }

            stack.FillEnabledContexts(enabled);
            Assert.AreEqual(1, enabled.Count);
            Assert.AreEqual(InputContext.Gameplay, enabled[0]);
            Assert.IsTrue(stack.IsContextEnabled(InputContext.Gameplay));
            Assert.IsFalse(stack.IsContextEnabled(InputContext.UI));
        }

        [Test]
        public void ContextStack_ExclusiveSuppressesLowerContexts()
        {
            var stack = new InputContextStack();
            var enabled = new List<InputContext>();

            stack.Set(InputContext.Gameplay);
            using (stack.Push(InputContext.UI))
            {
                stack.FillEnabledContexts(enabled);

                Assert.AreEqual(1, enabled.Count);
                Assert.AreEqual(InputContext.UI, enabled[0]);
                Assert.IsTrue(stack.IsContextEnabled(InputContext.UI));
                Assert.IsFalse(stack.IsContextEnabled(InputContext.Gameplay));
            }
        }

        [Test]
        public void CommandQueue_DrainsDueCommandsInStableOrder()
        {
            var queue = new InputCommandQueue();
            queue.Enqueue(new InputCommand(1, 2, InputIntent.Cancel));
            queue.Enqueue(new InputCommand(0, 2, InputIntent.AttackPrimary));
            queue.Enqueue(new InputCommand(0, 1, InputIntent.Jump));
            queue.Enqueue(new InputCommand(0, 1, InputIntent.AttackPrimary));

            IReadOnlyList<InputCommand> drained = queue.DrainForFrame(0);

            Assert.AreEqual(3, drained.Count);
            Assert.AreEqual(InputIntent.Jump, drained[0].Intent);
            Assert.AreEqual(InputIntent.AttackPrimary, drained[1].Intent);
            Assert.AreEqual(1, drained[1].SourceId);
            Assert.AreEqual(InputIntent.AttackPrimary, drained[2].Intent);
            Assert.AreEqual(2, drained[2].SourceId);
            Assert.AreEqual(1, queue.PendingCount);
        }

        [Test]
        public void CommandQueue_RejectsLateCommands()
        {
            var queue = new InputCommandQueue();
            queue.DrainForFrame(3);

            bool accepted = queue.TryEnqueue(new InputCommand(2, 1, InputIntent.Jump), out _);

            Assert.IsFalse(accepted);
        }

        [Test]
        public void CommandQueue_PeekForFrameDoesNotDrainOrAdvanceFrame()
        {
            var queue = new InputCommandQueue();
            queue.Enqueue(new InputCommand(0, 1, InputIntent.Jump));
            queue.Enqueue(new InputCommand(0, 1, InputIntent.ToggleHud));
            var peeked = new List<InputCommand>();

            int count = queue.PeekForFrame(0, peeked, command => command.Intent == InputIntent.ToggleHud);

            Assert.AreEqual(1, count);
            Assert.AreEqual(InputIntent.ToggleHud, peeked[0].Intent);
            Assert.AreEqual(2, queue.PendingCount);
            Assert.AreEqual(0, queue.CurrentFrame);
        }

        [Test]
        public void FakeInputProvider_CanDriveSnapshotWithoutDevices()
        {
            var provider = new FakeInputProvider();
            var snapshot = new InputSnapshot(
                move: new Vector2(1f, 0f),
                look: Vector2.zero,
                navigate: Vector2.zero,
                point: Vector2.zero,
                scroll: Vector2.zero,
                throttle: 0f,
                jumpPressed: true,
                jumpHeld: true,
                jumpReleased: false,
                attackPrimaryPressed: false,
                attackPrimaryHeld: false,
                attackSecondaryPressed: false,
                interactPressed: false,
                dodgePressed: false,
                sprintHeld: false,
                submitPressed: false,
                cancelPressed: false,
                pausePressed: false,
                debugTogglePressed: false);

            provider.SetContext(InputContext.Gameplay);
            provider.SetSnapshot(snapshot);
            InputCommand accepted = provider.Enqueue(new InputCommand(0, 10, InputIntent.Jump));

            Assert.AreEqual(InputContext.Gameplay, provider.CurrentContext);
            Assert.AreEqual(snapshot, provider.Snapshot);
            Assert.AreEqual(0, accepted.Sequence);
        }

        [Test]
        public void RecordedInputProvider_ReplaysSnapshotsAndRewinds()
        {
            var snapshots = new[]
            {
                InputSnapshot.Empty,
                new InputSnapshot(
                    move: new Vector2(0f, 1f),
                    look: Vector2.zero,
                    navigate: Vector2.zero,
                    point: Vector2.zero,
                    scroll: Vector2.zero,
                    throttle: 0f,
                    jumpPressed: false,
                    jumpHeld: false,
                    jumpReleased: false,
                    attackPrimaryPressed: true,
                    attackPrimaryHeld: true,
                    attackSecondaryPressed: false,
                    interactPressed: false,
                    dodgePressed: false,
                    sprintHeld: false,
                    submitPressed: false,
                    cancelPressed: false,
                    pausePressed: false,
                    debugTogglePressed: false)
            };

            var provider = new RecordedInputProvider(snapshots);

            Assert.IsTrue(provider.Advance());
            Assert.AreEqual(InputSnapshot.Empty, provider.Snapshot);
            Assert.IsTrue(provider.Advance());
            Assert.IsTrue(provider.Snapshot.AttackPrimaryPressed);
            Assert.IsFalse(provider.Advance());

            provider.Rewind();
            Assert.AreEqual(0, provider.Index);
            Assert.AreEqual(InputSnapshot.Empty, provider.Snapshot);
        }
    }
}
