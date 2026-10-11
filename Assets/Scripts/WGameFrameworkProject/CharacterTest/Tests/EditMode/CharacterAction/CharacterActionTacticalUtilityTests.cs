using System;
using System.IO;
using MxFramework.CharacterAction;
using NUnit.Framework;

namespace MxFramework.Tests.CharacterAction
{
    public sealed class CharacterActionTacticalUtilityTests
    {
        [TestCase(301)]
        [TestCase(324)]
        public void Timing_RealActionsMatchInclusiveSourcePhases(int actionId)
        {
            string path = Path.Combine(UnityEngine.Application.dataPath, "../CharacterActionSource/Libraries/Common/character_actions.json");
            var imported = CharacterActionJsonImporter.ImportActions(File.ReadAllText(path));
            Assert.IsTrue(imported.Success);
            var config = Array.Find(imported.Actions, action => action.ActionId.Value == actionId);
            Assert.IsNotNull(config);
            var timing = CharacterActionTacticalUtility.ExtractTiming(config);
            var startup = new System.Collections.Generic.HashSet<int>();
            var active = new System.Collections.Generic.HashSet<int>();
            int end = -1;
            foreach (var phase in config.Phases)
            {
                var target = phase.Kind == CharacterActionPhaseKind.Startup ? startup
                    : phase.Kind == CharacterActionPhaseKind.Active ? active : null;
                if (target == null) continue;
                for (int frame = phase.StartFrame; frame <= phase.EndFrame; frame++) target.Add(frame);
                if (phase.Kind == CharacterActionPhaseKind.Startup) end = Math.Max(end, phase.EndFrame);
            }
            Assert.Greater(startup.Count, 0);
            Assert.Greater(active.Count, 0);
            Assert.AreEqual(startup.Count, timing.StartupFrames);
            Assert.AreEqual(end, timing.StartupEndFrame);
            Assert.AreEqual(active.Count, timing.ActiveFrames);
            Assert.AreEqual(config.DurationFrames, timing.TotalDurationFrames);
        }

        [Test]
        public void Timing_CountsInclusiveUnionOfUnsortedOverlappingPhases()
        {
            var config = Config(new[] {
                new CharacterActionPhase(CharacterActionPhaseKind.Startup, 5, 8),
                new CharacterActionPhase(CharacterActionPhaseKind.Startup, 0, 6),
                new CharacterActionPhase(CharacterActionPhaseKind.Startup, 12, 12),
                new CharacterActionPhase(CharacterActionPhaseKind.Active, 13, 15) });
            var timing = CharacterActionTacticalUtility.ExtractTiming(config);
            Assert.AreEqual(10, timing.StartupFrames);
            Assert.AreEqual(12, timing.StartupEndFrame);
            Assert.AreEqual(3, timing.ActiveFrames);
            Assert.IsNull(timing.TotalDurationFrames);
        }

        [Test]
        public void Timing_MissingAndMaximumEndpointAreSafe()
        {
            var missing = CharacterActionTacticalUtility.ExtractTiming(Config());
            Assert.AreEqual(0, missing.StartupFrames);
            Assert.AreEqual(-1, missing.StartupEndFrame);
            Assert.AreEqual(0, missing.ActiveFrames);
            var extreme = CharacterActionTacticalUtility.ExtractTiming(Config(new[] {
                new CharacterActionPhase(CharacterActionPhaseKind.Active, 0, int.MaxValue) }));
            Assert.AreEqual(2147483648L, extreme.ActiveFrames);
        }

        [TestCase(CharacterActionRequestType.BasicAttack, CharacterCancelInputMask.AttackPrimary, 8)]
        [TestCase(CharacterActionRequestType.BasicAttack, CharacterCancelInputMask.AttackSecondary, 12)]
        [TestCase(CharacterActionRequestType.Movement, CharacterCancelInputMask.Any, 20)]
        [TestCase(CharacterActionRequestType.Any, CharacterCancelInputMask.AttackPrimary, -1)]
        [TestCase(CharacterActionRequestType.BasicAttack, CharacterCancelInputMask.Any, -1)]
        [TestCase(CharacterActionRequestType.Skill, CharacterCancelInputMask.Skill, -1)]
        [TestCase(CharacterActionRequestType.BasicAttack | CharacterActionRequestType.Skill,
            CharacterCancelInputMask.AttackPrimary | CharacterCancelInputMask.Skill, 8)]
        public void Transition_MatchesConcreteRequests(CharacterActionRequestType type, CharacterCancelInputMask slot, int expected)
        {
            var config = Config(rules: new[] {
                new CharacterCancelRule(12, 15, CharacterActionRequestType.BasicAttack, CharacterCancelInputMask.AttackSecondary),
                new CharacterCancelRule(8, 10, CharacterActionRequestType.BasicAttack, CharacterCancelInputMask.AttackPrimary),
                new CharacterCancelRule(20, 30, CharacterActionRequestType.Movement) });
            Assert.AreEqual(expected >= 0, CharacterActionTacticalUtility.TryGetEarliestTransitionFrame(config, type, slot, out int frame));
            Assert.AreEqual(expected, frame);
        }

        [Test]
        public void Transition_WildcardRulesAcceptZeroInputsButEmptyRulesDoNot()
        {
            Assert.IsTrue(CharacterActionTacticalUtility.TryGetEarliestTransitionFrame(
                Config(rules: new[] { new CharacterCancelRule(3, 4) }), 0, 0, out int frame));
            Assert.AreEqual(3, frame);
            Assert.IsFalse(CharacterActionTacticalUtility.TryGetEarliestTransitionFrame(Config(), 0, 0, out frame));
            Assert.AreEqual(-1, frame);
        }

        private static CharacterActionConfig Config(CharacterActionPhase[] phases = null, CharacterCancelRule[] rules = null)
        {
            return new CharacterActionConfig(new CharacterActionId(1), "test", CharacterActionCategory.BasicAttack,
                CharacterActionTimelineAuthority.CharacterAuthored, null, 0, null, null, phases, rules, null);
        }
    }
}
