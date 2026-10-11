using MxFramework.CharacterAction;
using MxFramework.Combat.Animation;
using NUnit.Framework;

namespace MxFramework.Tests.CharacterAction
{
    public sealed class CharacterCancelConflictClassifierTests
    {
        [Test]
        public void CharacterAllowsButCombatWindowRejects_ReturnsCombatCancelRejected()
        {
            CharacterCancelRule[] rules =
            {
                new CharacterCancelRule(0, 10),
            };

            CharacterCancelConflictResult result = CharacterCancelConflictClassifier.Classify(
                CharacterActionTimelineAuthority.CombatAnchored,
                rules,
                CreateTimeline(cancelTargetActionId: 3003),
                localFrame: 5,
                targetActionId: new CharacterActionId(2002),
                requestCategory: CharacterActionCategory.Dodge);

            Assert.IsFalse(result.Allowed);
            Assert.AreEqual(CharacterCancelRejectionAuthority.Combat, result.RejectedBy);
            Assert.AreEqual(CharacterActionDiagnostics.CombatCancelRejected, result.Code);
        }

        [Test]
        public void CombatAllowsButCharacterRuleRejects_ReturnsCharacterCancelRejected()
        {
            CharacterCancelRule[] rules =
            {
                new CharacterCancelRule(0, 10, requestTypeMask: CharacterActionRequestType.Skill),
            };

            CharacterCancelConflictResult result = CharacterCancelConflictClassifier.Classify(
                CharacterActionTimelineAuthority.CombatAnchored,
                rules,
                CreateTimeline(cancelTargetActionId: 2002),
                localFrame: 5,
                targetActionId: new CharacterActionId(2002),
                requestCategory: CharacterActionCategory.Dodge);

            Assert.IsFalse(result.Allowed);
            Assert.AreEqual(CharacterCancelRejectionAuthority.Character, result.RejectedBy);
            Assert.AreEqual(CharacterActionDiagnostics.CharacterCancelRejected, result.Code);
        }

        [Test]
        public void CharacterAndCombatBothAllow_ReturnsAccepted()
        {
            CharacterCancelRule[] rules =
            {
                new CharacterCancelRule(0, 10),
            };

            CharacterCancelConflictResult result = CharacterCancelConflictClassifier.Classify(
                CharacterActionTimelineAuthority.CombatAnchored,
                rules,
                CreateTimeline(cancelTargetActionId: 2002),
                localFrame: 5,
                targetActionId: new CharacterActionId(2002),
                requestCategory: CharacterActionCategory.Dodge);

            Assert.IsTrue(result.Allowed);
            Assert.AreEqual(CharacterCancelRejectionAuthority.None, result.RejectedBy);
            Assert.AreEqual(string.Empty, result.Code);
        }

        [Test]
        public void CharacterAuthoredCancel_DoesNotRequireCombatWindow()
        {
            CharacterCancelRule[] rules =
            {
                new CharacterCancelRule(0, 10),
            };

            CharacterCancelConflictResult result = CharacterCancelConflictClassifier.Classify(
                CharacterActionTimelineAuthority.CharacterAuthored,
                rules,
                combatTimeline: null,
                localFrame: 5,
                targetActionId: new CharacterActionId(2002),
                requestCategory: CharacterActionCategory.Dodge);

            Assert.IsTrue(result.Allowed);
        }

        [Test]
        public void CharacterRule_WithActionSlotMask_MatchesRequestedSlot()
        {
            const int attackPrimary = (int)CharacterCancelInputMask.AttackPrimary;
            const int dodge = (int)CharacterCancelInputMask.Dodge;
            CharacterCancelRule[] rules =
            {
                new CharacterCancelRule(6, 75, cancelInputMask: CharacterCancelInputMask.Dodge),
            };

            CharacterCancelConflictResult dodgeResult = CharacterCancelConflictClassifier.Classify(
                CharacterActionTimelineAuthority.CharacterAuthored,
                rules,
                combatTimeline: null,
                localFrame: 10,
                targetActionId: new CharacterActionId(2002),
                requestCategory: CharacterActionCategory.Dodge,
                actionSlotId: dodge);
            CharacterCancelConflictResult attackResult = CharacterCancelConflictClassifier.Classify(
                CharacterActionTimelineAuthority.CharacterAuthored,
                rules,
                combatTimeline: null,
                localFrame: 10,
                targetActionId: new CharacterActionId(2002),
                requestCategory: CharacterActionCategory.Dodge,
                actionSlotId: attackPrimary);

            Assert.IsTrue(dodgeResult.Allowed);
            Assert.IsFalse(attackResult.Allowed);
            Assert.AreEqual(CharacterActionDiagnostics.CharacterCancelRejected, attackResult.Code);
        }

        [Test]
        public void CharacterRule_RequestTypeAndSlotMasksMustBothMatch()
        {
            var rule = new CharacterCancelRule(
                0,
                10,
                CharacterActionRequestType.Dodge,
                CharacterCancelInputMask.Dodge);

            Assert.IsTrue(rule.Matches(5, CharacterActionCategory.Dodge, (int)CharacterCancelInputMask.Dodge));
            Assert.IsFalse(rule.Matches(5, CharacterActionCategory.Skill, (int)CharacterCancelInputMask.Dodge));
            Assert.IsFalse(rule.Matches(5, CharacterActionCategory.Dodge, (int)CharacterCancelInputMask.Skill));
        }

        [Test]
        public void CharacterRule_AnyMasksAcceptIntentWithoutSlot()
        {
            var rule = new CharacterCancelRule(0, 10);

            Assert.IsTrue(rule.Matches(5, CharacterActionCategory.BasicAttack, actionSlotId: 0));
            Assert.IsTrue(rule.Matches(5, CharacterActionCategory.Jump, actionSlotId: 0));
        }

        [Test]
        public void UntargetedMovement_MatchesOnlyCharacterAuthoredNoSlotWindow()
        {
            CharacterCancelRule[] rules =
            {
                new CharacterCancelRule(2, 8, CharacterActionRequestType.Movement),
            };

            CharacterCancelConflictResult accepted = CharacterCancelConflictClassifier.ClassifyUntargeted(
                CharacterActionTimelineAuthority.CharacterAuthored,
                rules,
                localFrame: 5,
                requestType: CharacterActionRequestType.Movement);
            CharacterCancelConflictResult wrongType = CharacterCancelConflictClassifier.ClassifyUntargeted(
                CharacterActionTimelineAuthority.CharacterAuthored,
                rules,
                localFrame: 5,
                requestType: CharacterActionRequestType.Dodge);
            CharacterCancelConflictResult combatAnchored = CharacterCancelConflictClassifier.ClassifyUntargeted(
                CharacterActionTimelineAuthority.CombatAnchored,
                rules,
                localFrame: 5,
                requestType: CharacterActionRequestType.Movement);

            Assert.IsTrue(accepted.Allowed);
            Assert.IsFalse(wrongType.Allowed);
            Assert.AreEqual(CharacterCancelRejectionAuthority.Character, wrongType.RejectedBy);
            Assert.IsFalse(combatAnchored.Allowed);
            Assert.AreEqual(CharacterCancelRejectionAuthority.Combat, combatAnchored.RejectedBy);
        }

        private static CombatActionTimeline CreateTimeline(int cancelTargetActionId)
        {
            return new CombatActionTimeline(
                actionId: 1001,
                totalFrames: 11,
                startup: new CombatFrameRange(0, 3),
                active: new CombatFrameRange(4, 6),
                recovery: new CombatFrameRange(7, 10),
                windows: new[]
                {
                    new CombatActionWindow(
                        CombatActionWindowKind.Cancel,
                        new CombatFrameRange(4, 6),
                        cancelTargetActionId),
                },
                events: null);
        }
    }
}
