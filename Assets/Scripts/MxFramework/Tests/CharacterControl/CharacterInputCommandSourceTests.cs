using MxFramework.CharacterControl.Input;
using MxFramework.Core.Math;
using MxFramework.Gameplay;
using MxFramework.Runtime;
using NUnit.Framework;
using UnityEngine;
using MxInput = MxFramework.Input;
using CharacterControlEntityRef = MxFramework.CharacterControl.CharacterControlEntityRef;
using CharacterCommand = MxFramework.CharacterControl.CharacterCommand;
using CharacterActionButtons = MxFramework.CharacterControl.CharacterActionButtons;
using CharacterActionKind = MxFramework.CharacterControl.CharacterActionKind;

namespace MxFramework.Tests.CharacterControl
{
    public sealed class CharacterInputCommandSourceTests
    {
        private const int LightAttackId = 1001;
        private const int AbilityId = 300001;

        [Test]
        public void SnapshotInput_MapsMoveLookJumpSprintAndPrimaryAction()
        {
            var input = new MxInput.FakeInputProvider();
            input.SetContext(MxInput.InputContext.Gameplay);
            input.SetSnapshot(CreateSnapshot(
                move: new Vector2(2f, -0.5f),
                look: new Vector2(0f, 1f),
                jumpPressed: true,
                sprintHeld: true,
                attackPrimaryPressed: true));
            var source = new InputCharacterCommandSource(input, new InputCharacterCommandSourceOptions
            {
                SourceId = 7,
                UseLookAsFacing = true,
                ActionBindings = new[]
                {
                    CharacterInputActionBinding.CombatAction(
                        MxInput.InputIntent.AttackPrimary,
                        CharacterActionKind.Attack,
                        LightAttackId,
                        queueIfBusy: true)
                }
            });

            Assert.IsTrue(source.TryGetCommand(new RuntimeFrame(5), CreateEntity(), out CharacterCommand command));

            Assert.AreEqual(new RuntimeFrame(5), command.Frame);
            Assert.AreEqual(7, command.SourceId);
            Assert.AreEqual(Fix64.One, command.MoveDirection.X);
            Assert.AreEqual(Fix64.FromRatio(-1, 2), command.MoveDirection.Z);
            Assert.IsTrue(command.JumpPressed);
            Assert.IsTrue(command.SprintHeld);
            Assert.IsTrue((command.ActionButtons & CharacterActionButtons.Primary) != 0);
            Assert.AreEqual(LightAttackId, command.ActionRequest.CombatActionId);
            Assert.IsTrue(command.ActionRequest.QueueIfBusy);
            Assert.AreEqual("input:5:AttackPrimary", command.TraceId);
        }

        [Test]
        public void CommandQueue_MapsExplicitTargetGameplayAbility()
        {
            var input = new MxInput.FakeInputProvider();
            input.SetContext(MxInput.InputContext.Gameplay);
            input.Commands.Enqueue(new MxInput.InputCommand(
                frame: 2,
                sourceId: 99,
                intent: MxInput.InputIntent.AttackSecondary,
                targetId: 12,
                traceId: "cmd-trace"));
            var source = new InputCharacterCommandSource(input, new InputCharacterCommandSourceOptions
            {
                SourceId = 9,
                TargetResolver = id => new GameplayEntityId(id, 1),
                ActionBindings = new[]
                {
                    CharacterInputActionBinding.GameplayAbility(MxInput.InputIntent.AttackSecondary, AbilityId)
                }
            });

            Assert.IsTrue(source.TryGetCommand(new RuntimeFrame(2), CreateEntity(), out CharacterCommand command));

            Assert.AreEqual(CharacterActionKind.GameplayAbility, command.ActionRequest.Kind);
            Assert.AreEqual(AbilityId, command.ActionRequest.GameplayAbilityId);
            Assert.AreEqual(new GameplayEntityId(12, 1), command.ActionRequest.TargetGameplayEntityId);
            Assert.AreEqual("cmd-trace", command.TraceId);
        }

        [Test]
        public void NonGameplayContext_DoesNotOutputCharacterCommand()
        {
            var input = new MxInput.FakeInputProvider();
            input.SetContext(MxInput.InputContext.UI);
            input.SetSnapshot(CreateSnapshot(move: Vector2.right));
            var source = new InputCharacterCommandSource(input);

            Assert.IsFalse(source.TryGetCommand(RuntimeFrame.Zero, CreateEntity(), out _));
        }

        [Test]
        public void NonGameplayContext_DrainsQueuedCommandsThroughFrame()
        {
            var input = new MxInput.FakeInputProvider();
            input.SetContext(MxInput.InputContext.UI);
            input.Commands.Enqueue(new MxInput.InputCommand(
                frame: 1,
                sourceId: 99,
                intent: MxInput.InputIntent.AttackPrimary,
                traceId: "stale-attack"));
            var source = new InputCharacterCommandSource(input, new InputCharacterCommandSourceOptions
            {
                ActionBindings = new[]
                {
                    CharacterInputActionBinding.CombatAction(
                        MxInput.InputIntent.AttackPrimary,
                        CharacterActionKind.Attack,
                        LightAttackId)
                }
            });

            Assert.IsFalse(source.TryGetCommand(new RuntimeFrame(1), CreateEntity(), out _));
            Assert.AreEqual(0, input.Commands.PendingCount);
            Assert.AreEqual(2L, input.Commands.CurrentFrame);

            input.SetContext(MxInput.InputContext.Gameplay);
            Assert.IsTrue(source.TryGetCommand(new RuntimeFrame(2), CreateEntity(), out CharacterCommand command));
            Assert.AreEqual(CharacterActionKind.None, command.ActionRequest.Kind);
            Assert.AreEqual(CharacterActionButtons.None, command.ActionButtons);
        }

        [Test]
        public void SnapshotInput_MapsInteractDodgeAndCancelButtons()
        {
            var input = new MxInput.FakeInputProvider();
            input.SetContext(MxInput.InputContext.Gameplay);
            input.SetSnapshot(CreateSnapshot(interactPressed: true, dodgePressed: true, cancelPressed: true));
            var source = new InputCharacterCommandSource(input, new InputCharacterCommandSourceOptions
            {
                ActionBindings = new[]
                {
                    CharacterInputActionBinding.Cancel(MxInput.InputIntent.Cancel)
                }
            });

            Assert.IsTrue(source.TryGetCommand(RuntimeFrame.Zero, CreateEntity(), out CharacterCommand command));

            Assert.IsTrue((command.ActionButtons & CharacterActionButtons.Interact) != 0);
            Assert.IsTrue((command.ActionButtons & CharacterActionButtons.Dodge) != 0);
            Assert.IsTrue((command.ActionButtons & CharacterActionButtons.Cancel) != 0);
            Assert.AreEqual(CharacterActionKind.Cancel, command.ActionRequest.Kind);
        }

        private static CharacterControlEntityRef CreateEntity()
        {
            return CharacterControlEntityRef.FromGameplay(new GameplayEntityId(10, 1), stableId: 1);
        }

        private static MxInput.InputSnapshot CreateSnapshot(
            Vector2 move = default,
            Vector2 look = default,
            bool jumpPressed = false,
            bool sprintHeld = false,
            bool attackPrimaryPressed = false,
            bool interactPressed = false,
            bool dodgePressed = false,
            bool cancelPressed = false)
        {
            return new MxInput.InputSnapshot(
                move,
                look,
                Vector2.zero,
                Vector2.zero,
                Vector2.zero,
                0f,
                jumpPressed,
                jumpHeld: jumpPressed,
                jumpReleased: false,
                attackPrimaryPressed,
                attackPrimaryHeld: attackPrimaryPressed,
                attackSecondaryPressed: false,
                interactPressed,
                dodgePressed,
                sprintHeld,
                submitPressed: false,
                cancelPressed,
                pausePressed: false,
                debugTogglePressed: false);
        }
    }
}
