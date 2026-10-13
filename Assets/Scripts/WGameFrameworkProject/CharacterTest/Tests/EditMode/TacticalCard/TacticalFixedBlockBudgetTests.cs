using System;
using NUnit.Framework;

namespace MxFramework.Game.CharacterTest.TacticalCard
{
    public sealed class TacticalFixedBlockBudgetTests
    {
        private static CharacterTestBehaviorExecutionProgram Program(bool rewind, bool sameFrame)
        {
            var events = rewind ? new[] {
                TacticalFixedPaymentCoreTests.Event(0, 0, 0, CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.CharacterAction, 0),
                TacticalFixedPaymentCoreTests.Event(0, 0, 1, CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.LoopRegion, 0, duration: 4) }
                : new[] { TacticalFixedPaymentCoreTests.Event(0, 0, 0, CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.CharacterAction, 0),
                    TacticalFixedPaymentCoreTests.Event(0, sameFrame ? 0 : 2, 1, CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.CharacterAction, 1) };
            return new CharacterTestBehaviorExecutionProgram(0, -1, "blocks.fixture", new[] { new CharacterTestBehaviorExecutionTimeline(0, 2, 4, -1) },
                events, new[] { 0, 1 }, new[] { new CharacterTestBehaviorExecutionAction(17), new CharacterTestBehaviorExecutionAction(18) },
                Array.Empty<CharacterTestBehaviorExecutionMovement>(), rewind ? new[] { new CharacterTestBehaviorExecutionFlow(0, 2) }
                    : Array.Empty<CharacterTestBehaviorExecutionFlow>(), Array.Empty<CharacterTestBehaviorExecutionCondition>(),
                Array.Empty<CharacterTestBehaviorExecutionPredicate>(), Array.Empty<int>(),
                blocks: new[] { new CharacterTestBehaviorExecutionBlock(0, 0, 4, CharacterTestBehaviorBlockCompletion.Action, 0, -1, true) });
        }
        [TestCase(false)] [TestCase(true)] public void GoalBlocksProveConservativeBudgetWithoutChangingExecution(bool sameFrame)
        {
            var program = Program(false, sameFrame);
            Assert.IsTrue(CharacterTestFixedActionBudgetProof.TryCreate(program, id => id == 17 ? 7 : 11, out var proof, out var issue), issue);
            Assert.AreEqual(18, proof.MaximumMomentum); Assert.IsTrue(proof.Matches(program));
        }
        [Test] public void BlockRewindRemainsExplicitUnsupportedProofRatherThanUnderReservation()
        {
            Assert.IsFalse(CharacterTestFixedActionBudgetProof.TryCreate(Program(true, false), _ => 7, out _, out var issue));
            Assert.AreEqual("budget:UnsupportedBlockRewind", issue);
        }
    }
}
