using MxFramework.CharacterAction;
using MxFramework.Combat.Animation;
using NUnit.Framework;

namespace MxFramework.Tests.CharacterAction
{
    public sealed class CharacterActionPhaseAuthorityTests
    {
        [Test]
        public void CombatAnchoredPhaseWithoutAnchor_ReturnsStableDiagnosticCode()
        {
            CharacterActionPhase[] phases =
            {
                new CharacterActionPhase(CharacterActionPhaseKind.Startup, 0, 3),
                new CharacterActionPhase(CharacterActionPhaseKind.Active, 4, 6, CombatActionPhase.Active),
                new CharacterActionPhase(CharacterActionPhaseKind.Recovery, 7, 10, CombatActionPhase.Recovery),
            };

            CharacterActionValidationIssue[] issues = CharacterActionPhaseValidator.Validate(
                CharacterActionTimelineAuthority.CombatAnchored,
                phases,
                CreateTimeline());

            Assert.AreEqual(1, issues.Length);
            Assert.AreEqual(CharacterActionDiagnostics.PhaseCombatAnchorMissing, issues[0].Code);
            Assert.AreEqual(0, issues[0].PhaseIndex);
        }

        [Test]
        public void CombatAnchoredPhaseRangeMismatch_ReturnsStableDiagnosticCode()
        {
            CharacterActionPhase[] phases =
            {
                new CharacterActionPhase(CharacterActionPhaseKind.Startup, 0, 3, CombatActionPhase.Startup),
                new CharacterActionPhase(CharacterActionPhaseKind.Active, 5, 6, CombatActionPhase.Active),
                new CharacterActionPhase(CharacterActionPhaseKind.Recovery, 7, 10, CombatActionPhase.Recovery),
            };

            CharacterActionValidationIssue[] issues = CharacterActionPhaseValidator.Validate(
                CharacterActionTimelineAuthority.CombatAnchored,
                phases,
                CreateTimeline());

            Assert.AreEqual(1, issues.Length);
            Assert.AreEqual(CharacterActionDiagnostics.PhaseCombatRangeMismatch, issues[0].Code);
            Assert.AreEqual(1, issues[0].PhaseIndex);
        }

        [Test]
        public void CharacterAuthoredPhase_DoesNotRequireCombatAnchorOrCombatRangeMatch()
        {
            CharacterActionPhase[] phases =
            {
                new CharacterActionPhase(CharacterActionPhaseKind.Startup, 0, 1),
                new CharacterActionPhase(CharacterActionPhaseKind.Active, 2, 8),
                new CharacterActionPhase(CharacterActionPhaseKind.Recovery, 9, 12),
            };

            CharacterActionValidationIssue[] issues = CharacterActionPhaseValidator.Validate(
                CharacterActionTimelineAuthority.CharacterAuthored,
                phases,
                CreateTimeline());

            Assert.AreEqual(0, issues.Length);
        }

        private static CombatActionTimeline CreateTimeline()
        {
            return new CombatActionTimeline(
                actionId: 1001,
                totalFrames: 11,
                startup: new CombatFrameRange(0, 3),
                active: new CombatFrameRange(4, 6),
                recovery: new CombatFrameRange(7, 10),
                windows: null,
                events: null);
        }
    }
}
