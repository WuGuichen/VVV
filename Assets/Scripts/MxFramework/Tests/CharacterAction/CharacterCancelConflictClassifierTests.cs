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
                new CharacterCancelRule(0, 10, targetActionId: 2002, sourceKind: CharacterActionSourceKind.PlayerIntervention),
            };

            CharacterCancelConflictResult result = CharacterCancelConflictClassifier.Classify(
                CharacterActionTimelineAuthority.CombatAnchored,
                rules,
                CreateTimeline(cancelTargetActionId: 3003),
                localFrame: 5,
                targetActionId: 2002,
                sourceKind: CharacterActionSourceKind.PlayerIntervention);

            Assert.IsFalse(result.Allowed);
            Assert.AreEqual(CharacterCancelRejectionAuthority.Combat, result.RejectedBy);
            Assert.AreEqual(CharacterActionDiagnostics.CombatCancelRejected, result.Code);
        }

        [Test]
        public void CombatAllowsButCharacterRuleRejects_ReturnsCharacterCancelRejected()
        {
            CharacterCancelRule[] rules =
            {
                new CharacterCancelRule(0, 10, targetActionId: 2002, sourceKind: CharacterActionSourceKind.PlayerIntervention, allow: false),
            };

            CharacterCancelConflictResult result = CharacterCancelConflictClassifier.Classify(
                CharacterActionTimelineAuthority.CombatAnchored,
                rules,
                CreateTimeline(cancelTargetActionId: 2002),
                localFrame: 5,
                targetActionId: 2002,
                sourceKind: CharacterActionSourceKind.PlayerIntervention);

            Assert.IsFalse(result.Allowed);
            Assert.AreEqual(CharacterCancelRejectionAuthority.Character, result.RejectedBy);
            Assert.AreEqual(CharacterActionDiagnostics.CharacterCancelRejected, result.Code);
        }

        [Test]
        public void CharacterAndCombatBothAllow_ReturnsAccepted()
        {
            CharacterCancelRule[] rules =
            {
                new CharacterCancelRule(0, 10, targetActionId: 2002, sourceKind: CharacterActionSourceKind.PlayerIntervention),
            };

            CharacterCancelConflictResult result = CharacterCancelConflictClassifier.Classify(
                CharacterActionTimelineAuthority.CombatAnchored,
                rules,
                CreateTimeline(cancelTargetActionId: 2002),
                localFrame: 5,
                targetActionId: 2002,
                sourceKind: CharacterActionSourceKind.PlayerIntervention);

            Assert.IsTrue(result.Allowed);
            Assert.AreEqual(CharacterCancelRejectionAuthority.None, result.RejectedBy);
            Assert.AreEqual(string.Empty, result.Code);
        }

        [Test]
        public void CharacterAuthoredCancel_DoesNotRequireCombatWindow()
        {
            CharacterCancelRule[] rules =
            {
                new CharacterCancelRule(0, 10, targetActionId: 2002, sourceKind: CharacterActionSourceKind.PlayerIntervention),
            };

            CharacterCancelConflictResult result = CharacterCancelConflictClassifier.Classify(
                CharacterActionTimelineAuthority.CharacterAuthored,
                rules,
                combatTimeline: null,
                localFrame: 5,
                targetActionId: 2002,
                sourceKind: CharacterActionSourceKind.PlayerIntervention);

            Assert.IsTrue(result.Allowed);
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
