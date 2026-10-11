using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MxFramework.Game.CharacterTest;
using MxFramework.Runtime;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEngine;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterTestTacticalDslLocalEndTests
    {
        private CharacterTestTacticalCardRuntimeFixture _compile;
        [OneTimeSetUp] public void Setup() => _compile = new CharacterTestTacticalCardRuntimeFixture();
        [OneTimeTearDown] public void Cleanup() => _compile?.Dispose();

        private CharacterTestTacticalDslCompilation Compile(CharacterTestTacticalDslSource source)
        {
            Assert.IsTrue(CharacterTestTacticalDslCompiler.TryCompile(source, _compile.Inputs, out var compiled, out string issue), issue);
            return compiled;
        }

        [Test]
        public void TrialRetreatDoesNotChangeV2ReaderOrCompiler()
        {
            var source = CharacterTestTacticalDslTrialTemplates.Create();
            var retreat = source.Definitions.Single(d => d.DefinitionId == "shared.retreat").Source;
            Assert.Throws<JsonSerializationException>(() => CharacterTestTacticalAuthoringJson.Read(CharacterTestTacticalAuthoringJson.Write(retreat)));
            Assert.IsFalse(CharacterTestTacticalAuthoringCompiler.TryCompile(retreat, _compile.Inputs, out _, out _));
            var result = Compile(source);
            Assert.That(result.Program.Execution.Movements.ToArray().Any(m => m.Strategy == CharacterTestRuntimeAiLabMovementStrategy.AvoidHazard));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void OuterLocalEndWinsNestedConflictAndWholeCardExitStillWins(bool wholeExit)
        {
            var source = CharacterTestTacticalDslTrialTemplates.Create();
            var assault = source.Root.Children[0];
            var attack = assault.Children[0];
            assault.EndCondition = Alive(); attack.EndCondition = Alive();
            if (wholeExit)
                source.Definitions[0].Source.Timelines[0].Events.Add(new CharacterTestTacticalAuthoringEvent { EventId = "whole-exit",
                    Start = CharacterTestTacticalAuthoringTemplates.Absolute(0), End = CharacterTestTacticalAuthoringTemplates.Absolute(1),
                    Operation = new CharacterTestTacticalAuthoringOperation { Kind = "ReturnToPlanner" } });
            var compiled = Compile(source);
            var runtime = new CharacterTestBehaviorExecutionEvaluatorTests.RuntimeStub();
            var evaluator = new CharacterTestBehaviorExecutionEvaluator(compiled.Program.Execution, runtime);
            Assert.IsTrue(evaluator.Start(0)); var visited = new HashSet<string>();
            for (int i = 1; i < 65; i++)
            {
                evaluator.Tick(i); runtime.Commit();
                visited.Add(compiled.Program.ExecutionDebugMap.Timeline(evaluator.CurrentTimelineIndex));
            }
            Assert.IsFalse(visited.Contains("tail"), "The outer exit skips the rest of the group.");
            Assert.AreEqual(!wholeExit, visited.Contains("retreat"));
            Assert.AreEqual(CharacterTestRuntimeAiLabLogicRuntimeState.Completed, evaluator.State);
            Assert.AreEqual(0, runtime.StartRequests, "The exit wins before a new action starts.");
        }

        [Test]
        public void SuccessorConditionFalseSkipsRetreatWithoutWaitingOrRestartingSelection()
        {
            var source = CharacterTestTacticalDslTrialTemplates.Create();
            source.Root.Children[0].EndCondition = Alive();
            source.Root.Children[1].Condition = new CharacterTestRuntimeAiLabBehaviorRecipeConditionNode { NodeId = "never",
                Kind = CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.Not, Children = { Alive() } };
            var compiled = Compile(source); var runtime = new CharacterTestBehaviorExecutionEvaluatorTests.RuntimeStub();
            var evaluator = new CharacterTestBehaviorExecutionEvaluator(compiled.Program.Execution, runtime);
            Assert.IsTrue(evaluator.Start(0)); var visited = new HashSet<string>();
            for (int f = 1; f < 20; f++) { evaluator.Tick(f); runtime.Commit(); visited.Add(compiled.Program.ExecutionDebugMap.Timeline(evaluator.CurrentTimelineIndex)); }
            Assert.IsFalse(visited.Contains("retreat")); Assert.IsFalse(visited.Contains("tail"));
            Assert.AreEqual(CharacterTestRuntimeAiLabLogicRuntimeState.Completed, evaluator.State);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void RealHostLocalEndDuringApproachOrAttackCleansUpThenRetreats(bool duringAttack)
        {
            using (var run = new Run(CharacterTestTacticalDslTrialTemplates.Create()))
            {
                run.Fixture.PlaceTarget(duringAttack ? run.Fixture.Inputs.Metrics.SpatialSummary.VerifiedOptimalMillimeters.Value : 4500);
                run.Start();
                if (duringAttack)
                {
                    for (int i = 0; i < 100 && run.Host.OwnedActionInstanceId == 0; i++) run.Step();
                    Assert.Greater(run.Host.OwnedActionInstanceId, 0, "An actual action must have started before the local exit.");
                    run.Step();
                }
                else { for (int i = 0; i < 8; i++) run.Step(); Assert.AreEqual(0, run.Host.OwnedActionInstanceId); }
                run.Fixture.Source.SyncCombatHealth(100, 20);
                for (int i = 0; i < 100 && run.Host.State != CharacterTestRuntimeAiLabLogicRuntimeState.Completed; i++) run.Step();
                Assert.IsFalse(run.Rows.Any(r => r.Timeline == "tail"));
                var retreat = run.Rows.Where(r => r.Timeline == "retreat" && r.HasMove).ToArray();
                Assert.Greater(retreat.Length, 5, "Old event cleanup must not clear the successor's repeated movement output.");
                Assert.IsTrue(retreat.Any(r => r.MoveZ < 0));
                Assert.AreEqual(0, run.Host.OwnedActionInstanceId);
                Assert.AreEqual(CharacterTestRuntimeAiLabLogicRuntimeState.Completed, run.Host.State);
                Assert.IsFalse(run.Fixture.Source.Input.HasActiveMove);
                run.Save(duringAttack ? "local-end-during-action" : "local-end-during-gate");
            }
        }

        [Test]
        public void NoConfiguredLocalEndDoesNotInjectLowHealthExit()
        {
            var source = CharacterTestTacticalDslTrialTemplates.Create(); source.Root.Children[0].EndCondition = null;
            using (var run = new Run(source))
            {
                run.Fixture.PlaceTarget(4500); run.Fixture.Source.SyncCombatHealth(100, 20); run.Start();
                for (int i = 0; i < 12; i++) run.Step();
                Assert.AreEqual("attack", run.Rows.Last().Timeline);
                Assert.AreEqual(0, run.Host.LocalFrame, "The authored Gate is still waiting; low health does not add an exit.");
                Assert.IsFalse(run.Rows.Any(r => r.Timeline == "retreat")); run.Save("no-implicit-low-health-exit");
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void RealHostCurrentTargetLossAndReplacementClearsThenResumesWithoutRestart(bool afterActionStart)
        {
            var source = CharacterTestTacticalDslTrialTemplates.Create(); source.Root.Children[0].EndCondition = null;
            using (var run = new Run(source))
            {
                run.Fixture.PlaceTarget(afterActionStart ? run.Fixture.Inputs.Metrics.SpatialSummary.VerifiedOptimalMillimeters.Value : 4500);
                run.Start();
                for (int i = 0; i < (afterActionStart ? 100 : 8); i++)
                { run.Step(); if (afterActionStart && run.Host.OwnedActionInstanceId > 0) break; }
                long action = run.Host.OwnedActionInstanceId;
                if (afterActionStart) Assert.Greater(action, 0); else Assert.AreEqual(0, action);
                int local = run.Host.LocalFrame;
                run.Target = null;
                for (int i = 0; i < 3; i++) run.Step();
                Assert.IsFalse(run.Rows.Last().HasMove); Assert.AreNotEqual(2, run.Rows.Last().FacingMode);
                if (afterActionStart) { Assert.AreEqual(action, run.Host.OwnedActionInstanceId); Assert.Greater(run.Host.LocalFrame, local); }
                else Assert.AreEqual(local, run.Host.LocalFrame, "Only the explicit Gate holds the cursor.");
                run.Target = run.SpawnB();
                int yawBeforeB = run.Fixture.Source.Transform.YawMilliDegrees;
                for (int i = 0; i < 4; i++) run.Step();
                Assert.AreEqual(2, run.Rows.Last().FacingMode);
                Assert.AreEqual(run.Target.Transform.XMillimeters, run.Rows.Last().FacingX);
                if (afterActionStart)
                {
                    Assert.AreEqual(action, run.Host.OwnedActionInstanceId);
                    Assert.Less(Mathf.Abs(Mathf.DeltaAngle(run.Rows.Last().Yaw / 1000f, 270f)),
                        Mathf.Abs(Mathf.DeltaAngle(yawBeforeB / 1000f, 270f)), "Actual permitted facing must turn toward B, not just change the command target.");
                    var frames = run.Rows.Where(r => r.Action == action).Select(r => r.ActionFrame).ToArray();
                    for (int i = 1; i < frames.Length; i++) Assert.GreaterOrEqual(frames[i], frames[i - 1], "Target changes must not restart action progress.");
                }
                else Assert.Less(run.Rows.Last().MoveX, 0);
                Assert.AreEqual(afterActionStart ? 1 : 0, run.Rows.Where(r => r.Action > 0).Select(r => r.Action).Distinct().Count());
                run.Save(afterActionStart ? "target-a-none-b-during-action" : "target-a-none-b-before-action");
            }
        }

        [Test]
        public void ActualDescriptionAndSharedTransactionsReflectSourceNotNames()
        {
            var workspace = new CharacterTestTacticalDslTrialWorkspace(); var card = workspace.Cards[0];
            var description = CharacterTestTacticalDslDescription.Generate(card, _compile.Inputs);
            Assert.IsTrue(description.Executable, description.CompilationIssue);
            StringAssert.Contains("30%", description.Text); StringAssert.Contains("中止这段", description.Text);
            StringAssert.Contains("后撤", description.Text); StringAssert.Contains(_compile.Inputs.Action.DisplayName, description.Text);
            StringAssert.Contains("正常结束或提前中止后", description.Text);
            StringAssert.Contains("继续面向当前目标片刻", description.Text, "The extra internal range must not disappear from the summary.");
            StringAssert.DoesNotContain("其中相应行为", description.Text);
            StringAssert.DoesNotContain("可参与使用", description.Text);
            Assert.Less(description.Text.Length, description.Details.Length);
            var withoutApproach = workspace.Card(card.CardId);
            withoutApproach.Definitions[0].Source.Timelines[0].Events.RemoveAll(e => e.Operation.Kind == "ApproachOptimalRange");
            StringAssert.DoesNotContain("较远时先接近", CharacterTestTacticalDslDescription.Generate(withoutApproach, _compile.Inputs).Text);
            var partial = workspace.Card(card.CardId);
            partial.Root.Children[0].EndCondition.Children[0].Predicate.Fact = CharacterTestRuntimeAiLabCardConditionFact.SelfActionInterruptible;
            partial.Root.Children[0].EndCondition.Children[0].Predicate.Operator = CharacterTestRuntimeAiLabCardConditionOperator.Equals;
            partial.Root.Children[0].EndCondition.Children[0].Predicate.BoolValue = true;
            partial.Root.Children[0].EndCondition.Children[0].Predicate.Value = 0f;
            var partialDescription = CharacterTestTacticalDslDescription.Generate(partial, _compile.Inputs);
            Assert.IsTrue(partialDescription.Executable, partialDescription.CompilationIssue);
            Assert.Greater(partialDescription.Warnings.Length, 0, "Partial explanation must be a warning, not a compile failure.");
            string before = workspace.Capture();
            var shared = card.Definitions[0].Source.Clone(); shared.Timelines[0].Events.Single(e => e.EventId == "approach").End.OffsetFrames += 4;
            var pending = workspace.PrepareShared("shared.attack", CharacterTestTacticalAuthoringJson.Write(shared));
            Assert.AreEqual(2, pending.Impact.Length); Assert.AreEqual(before, workspace.Capture(), "Showing/cancelling impact does not write.");
            workspace.ApplyShared(pending);
            foreach (var affected in workspace.Cards)
                Assert.AreEqual(4, affected.Definitions[0].Source.Timelines[0].Events.Single(e => e.EventId == "approach").End.OffsetFrames);
            Assert.Throws<InvalidOperationException>(() => workspace.ApplyShared(pending));
            Assert.IsTrue(workspace.Undo()); Assert.AreEqual(before, workspace.Capture());
            Assert.IsTrue(workspace.Undo(true));
            workspace.DuplicateDefinitionAndReplace(workspace.Version, card.CardId, "attack", "shared.independent");
            Assert.AreEqual("shared.independent", CharacterTestTacticalDslTrialWorkspace.Nodes(workspace.Card(card.CardId).Root).Single(n => n.NodeId == "attack").DefinitionId);
            Assert.AreEqual("shared.attack", CharacterTestTacticalDslTrialWorkspace.Nodes(workspace.Cards[1].Root).Single(n => n.NodeId == "attack").DefinitionId);
            var renamed = workspace.Card(card.CardId); string prior = CharacterTestTacticalDslDescription.Generate(renamed, _compile.Inputs).Text;
            renamed.Definitions.ForEach(d => d.Source.Name = "必定命中并回血");
            Assert.AreEqual(prior, CharacterTestTacticalDslDescription.Generate(renamed, _compile.Inputs).Text);
            workspace.DuplicateCard(workspace.Version, card.CardId, "card.copy");
            Assert.AreEqual("shared.independent", CharacterTestTacticalDslTrialWorkspace.Nodes(workspace.Card("card.copy").Root).Single(n => n.NodeId == "attack").DefinitionId);
            string directory = EvidenceDirectory();
            File.WriteAllText(Path.Combine(directory, "actual-description.json"), JsonConvert.SerializeObject(new
                { description.Text, description.Details, description.Warnings, description.Executable, description.DependencyIdentity, sharedImpact = pending.Impact }, Formatting.Indented));
            File.WriteAllText(Path.Combine(directory, "local-end-source.json"), CharacterTestTacticalDslJson.Write(CharacterTestTacticalDslTrialTemplates.Create()));
        }

        private static CharacterTestRuntimeAiLabBehaviorRecipeConditionNode Alive() => CharacterTestTacticalAuthoringTemplates.CreateSingleAttack().ActivationConditionRoot;

        [Test]
        public void RealHostGroupEntranceDoesNotRecheckAfterTargetAndDistanceChange()
        {
            var source = CharacterTestTacticalDslTrialTemplates.Create();
            // This scenario authors an unconditional attack to isolate the group's one-shot entrance.
            // Explicit Gate behavior is covered separately; an unavailable opportunity must not be bypassed at Runtime.
            var attackSource = source.Definitions[0].Source;
            attackSource.Timelines[0].Events.RemoveAll(e => e.Operation.Kind == "WaitForCondition");
            attackSource.Timelines[0].Events.Single(e => e.EventId == "attack").ConditionRoot = null;
            attackSource.PredicateBindings.Clear();
            var group = source.Root.Children[0]; group.Kind = "If"; group.EndCondition = null;
            group.Condition = new CharacterTestRuntimeAiLabBehaviorRecipeConditionNode { NodeId = "group.target",
                Kind = CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.SubjectScope,
                Subject = CharacterTestRuntimeAiLabBehaviorRecipeConditionSubjectKind.CurrentCombatTarget,
                Children = { new CharacterTestRuntimeAiLabBehaviorRecipeConditionNode { NodeId = "group.distance",
                    Kind = CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.FactPredicate,
                    Predicate = new CharacterTestRuntimeAiLabCardCondition { ConditionId = "group.distance.value",
                        Fact = CharacterTestRuntimeAiLabCardConditionFact.TargetRootDistance,
                        Operator = CharacterTestRuntimeAiLabCardConditionOperator.GreaterThan, Value = 3f } } } };
            using (var run = new Run(source))
            {
                run.Fixture.PlaceTarget(4500); run.Start();
                for (int i = 0; i < 10; i++) run.Step();
                Assert.AreEqual("attack", run.Rows.Last().Timeline);
                var target = run.SpawnB();
                target.SetTransform(CharacterTestCharacterTransformComponent.FromDeterministic(run.Fixture.Source.Transform.XMillimeters, 0,
                    run.Fixture.Source.Transform.ZMillimeters + run.Fixture.Inputs.Metrics.SpatialSummary.VerifiedOptimalMillimeters.Value, 0));
                run.Target = target;
                Assert.Less(Vector2.Distance(new Vector2(run.Fixture.Source.Transform.X, run.Fixture.Source.Transform.Z),
                    new Vector2(target.Transform.X, target.Transform.Z)), 3f);
                for (int i = 0; i < 200 && run.Host.State != CharacterTestRuntimeAiLabLogicRuntimeState.Completed; i++) run.Step();
                Assert.IsTrue(run.Rows.Any(r => r.Timeline == "tail"), "The group does not discard its second item when the entry condition becomes false.");
                Assert.IsTrue(run.Rows.Any(r => r.Timeline == "retreat"));
                Assert.AreEqual(CharacterTestRuntimeAiLabLogicRuntimeState.Completed, run.Host.State);
                run.Save("group-entrance-target-and-fact-change");
            }
        }

        [Test]
        public void EndedTurnDoesNotRestartWhenNewTargetAppears()
        {
            var source = CharacterTestTacticalDslTrialTemplates.Create(); source.Root.Children[0].EndCondition = null;
            source.Definitions[0].Source.Timelines[0].Events.Single(e => e.EventId == "facing").End = CharacterTestTacticalAuthoringTemplates.Absolute(5);
            using (var run = new Run(source))
            {
                run.Fixture.PlaceTarget(run.Fixture.Inputs.Metrics.SpatialSummary.VerifiedOptimalMillimeters.Value); run.Start();
                for (int i = 0; i < 35 && run.Host.LocalFrame < 7; i++) run.Step();
                Assert.GreaterOrEqual(run.Host.LocalFrame, 7); Assert.Greater(run.Host.OwnedActionInstanceId, 0);
                run.Target = null; run.Step(); run.Target = run.SpawnB();
                for (int i = 0; i < 3; i++) run.Step();
                Assert.AreNotEqual(2, run.Rows.Last().FacingMode);
                Assert.Greater(run.Host.OwnedActionInstanceId, 0);
                run.Save("ended-turn-not-reopened");
            }
        }

        [Test]
        public void MissingCurrentTargetDoesNotClearIndependentWorldPointFacing()
        {
            var builder = new CharacterTestBehaviorExecutionBuilder(CharacterTestBehaviorExecutionEvaluatorTests.References());
            builder.AddTimeline("main", 30);
            builder.AddMovement("target-move", "main", 0, 30, CharacterTestBehaviorExecutionMovement.ApproachOptimalRange(0.5f, 2f, 1f));
            builder.AddTurn("world-facing", "main", 0, 30,
                new CharacterTestBehaviorExecutionTurn(CharacterTestRuntimeAiLabMovementTargetKind.WorldPosition, x: -10f, z: 0f));
            builder.AddGate("hold", "main", 0, -1);
            using (var run = new Run(null, builder.Build()))
            {
                run.Fixture.PlaceTarget(4500); run.Start();
                for (int i = 0; i < 5; i++) run.Step();
                Assert.IsTrue(run.Rows.Last().HasMove);
                run.Target = null;
                for (int i = 0; i < 3; i++) run.Step();
                Assert.IsFalse(run.Rows.Last().HasMove);
                Assert.AreEqual(2, run.Rows.Last().FacingMode);
                Assert.That(run.Rows.Last().FacingX, Is.EqualTo(-10000).Within(1));
                run.Save("target-loss-preserves-independent-facing");
            }
        }

        [TestCase("null")]
        [TestCase("root")]
        [TestCase("duplicate")]
        [TestCase("missing")]
        [TestCase("children")]
        public void DescriptionPreservesDiagnosticsInsteadOfThrowingOnReadableErrors(string broken)
        {
            var source = CharacterTestTacticalDslTrialTemplates.Create();
            if (broken == "null") source = null;
            else if (broken == "root") source.Root = null;
            else if (broken == "duplicate") source.Definitions.Add(source.Definitions[0]);
            else if (broken == "missing") source.Definitions.RemoveAt(0);
            else source.Root.Children[1].Condition.Children.Clear();
            CharacterTestTacticalDslDescription result = null;
            Assert.DoesNotThrow(() => result = CharacterTestTacticalDslDescription.Generate(source, _compile.Inputs));
            Assert.IsFalse(result.Executable); Assert.IsNotEmpty(result.CompilationIssue); Assert.IsNotEmpty(result.Warnings);
        }

        [Test]
        public void GateDescriptionUsesOnlyBindingsInThatConditionTree()
        {
            var source = CharacterTestTacticalDslTrialTemplates.Create();
            var shared = source.Definitions[0].Source;
            shared.Timelines[0].Events.Single(e => e.EventId == "release").ConditionRoot = Alive();
            // Retain valid bindings in an independent event, not in this Gate.
            shared.Timelines[0].Events.Single(e => e.EventId == "attack").ConditionRoot = CharacterTestTacticalAuthoringTemplates.CreateSingleAttack()
                .Timelines[0].Events.Single(e => e.EventId == "release").ConditionRoot;
            shared.PredicateBindings.RemoveAll(b => b.NodeId.StartsWith("attack.", StringComparison.Ordinal));
            var description = CharacterTestTacticalDslDescription.Generate(source, _compile.Inputs);
            Assert.IsTrue(description.Executable, description.CompilationIssue);
            StringAssert.DoesNotContain("包括距离、朝向和攻击机会", description.Text);
            StringAssert.DoesNotContain("包括距离、朝向和攻击机会", description.Details);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void SharedExitDescriptionDistinguishesUnreachableAndConditionalSuccessor(bool conditional)
        {
            var source = CharacterTestTacticalDslTrialTemplates.Create(); source.Root.Children[0].EndCondition = null;
            source.Definitions[0].Source.Timelines[0].Events.Add(new CharacterTestTacticalAuthoringEvent { EventId = "exit-now",
                Start = CharacterTestTacticalAuthoringTemplates.Absolute(0), End = CharacterTestTacticalAuthoringTemplates.Absolute(1),
                ConditionRoot = conditional ? CharacterTestTacticalDslTrialTemplates.LowHealth() : null,
                Operation = new CharacterTestTacticalAuthoringOperation { Kind = "ReturnToPlanner" } });
            var description = CharacterTestTacticalDslDescription.Generate(source, _compile.Inputs);
            Assert.IsTrue(description.Executable, description.CompilationIssue);
            if (!conditional)
            {
                StringAssert.DoesNotContain("后撤", description.Text); StringAssert.DoesNotContain("后撤", description.Details);
                StringAssert.DoesNotContain("正常走完整段后继续", description.Details);
                StringAssert.DoesNotContain("尝试施放", description.Text);
            }
            else { StringAssert.Contains("后撤", description.Text); StringAssert.Contains("未触发整卡退出时", description.Details); }
        }

        [TestCase("wait", false)]
        [TestCase("if", true)]
        [TestCase("local", true)]
        public void WaitExitPropagatesUnreachabilityButIfAndOuterLocalEndRetainSuccessor(string kind, bool reachable)
        {
            var source = CharacterTestTacticalDslTrialTemplates.Create();
            var wrapper = new CharacterTestTacticalDslNode { NodeId = "wrapper", Kind = kind == "if" ? "If" : "Wait",
                Condition = Alive(), Children = new List<CharacterTestTacticalDslNode> {
                    new CharacterTestTacticalDslNode { NodeId = "inner-exit", Kind = "Exit" } } };
            if (kind == "local") wrapper = new CharacterTestTacticalDslNode { NodeId = "local-range", Kind = "Sequence",
                EndCondition = CharacterTestTacticalDslTrialTemplates.LowHealth(), Children = new List<CharacterTestTacticalDslNode> { wrapper } };
            source.Root.Children = new List<CharacterTestTacticalDslNode> { wrapper, CharacterTestTacticalDslTrialTemplates.Reference("after", "shared.retreat") };
            var description = CharacterTestTacticalDslDescription.Generate(source, _compile.Inputs);
            Assert.IsTrue(description.Executable, description.CompilationIssue);
            Assert.AreEqual(reachable, description.Text.Contains("后撤"));
            Assert.AreEqual(reachable, description.Details.Contains("后撤"));
        }

        [Test]
        public void SameFrameFalseGateDoesNotBlockActionOrProduceWaitThenAttackSummary()
        {
            var source = CharacterTestTacticalDslTrialTemplates.Create(); source.Root.Children[0].EndCondition = null;
            var shared = source.Definitions[0].Source;
            shared.PredicateBindings.Clear();
            shared.Timelines[0].Events.Single(e => e.EventId == "release").ConditionRoot = CharacterTestTacticalDslTrialTemplates.LowHealth();
            var action = shared.Timelines[0].Events.Single(e => e.EventId == "attack");
            action.Start = CharacterTestTacticalAuthoringTemplates.Absolute(0); action.ConditionRoot = null;
            using (var run = new Run(source))
            {
                run.Fixture.PlaceTarget(run.Fixture.Inputs.Metrics.SpatialSummary.VerifiedOptimalMillimeters.Value); run.Start();
                for (int i = 0; i < 8; i++) run.Step();
                Assert.Greater(run.Fixture.Source.CombatState.Health, run.Fixture.Source.CombatState.MaxHealth * 0.3f);
                Assert.AreEqual(0, run.Host.LocalFrame, "The false Gate still holds the cursor.");
                Assert.Greater(run.Host.OwnedActionInstanceId, 0, "Same-frame action sampling is not gated.");
                Assert.AreEqual(1, run.Rows.Where(r => r.Action > 0).Select(r => r.Action).Distinct().Count());
                var description = CharacterTestTacticalDslDescription.Generate(source, run.Fixture.Inputs);
                Assert.IsTrue(description.Executable, description.CompilationIssue);
                StringAssert.DoesNotContain("后尝试施放", description.Text);
                StringAssert.Contains("等待点不拦截同时开始", description.Text);
                StringAssert.Contains("等待点不拦截同时开始", description.Details);
                Assert.IsTrue(description.Warnings.Any(w => w.Contains("同帧")));
                run.Save("same-frame-gate-and-unconditional-action");
                File.WriteAllText(Path.Combine(EvidenceDirectory(), "same-frame-gate-description.json"), JsonConvert.SerializeObject(new
                    { description.Text, description.Details, description.Warnings, description.Executable }, Formatting.Indented));
            }
        }
        private static string EvidenceDirectory()
        {
            string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/Task04ExecutionEvidence/DSL-LocalEnd"));
            Directory.CreateDirectory(directory); return directory;
        }
        private sealed class Row
        {
            public int World, Local, Target, FacingMode, FacingX, ActionFrame, X, Z, Yaw;
            public string Timeline; public long Action; public bool HasMove; public float MoveX, MoveZ;
        }
        private sealed class Run : IDisposable
        {
            internal readonly CharacterTestTacticalCardRuntimeFixture Fixture = new CharacterTestTacticalCardRuntimeFixture();
            internal readonly CharacterTestTacticalDslCompilation Compiled;
            internal readonly CharacterTestBehaviorExecutionHost Host;
            internal readonly List<Row> Rows = new List<Row>();
            internal CharacterTestCharacterEntity Target;
            internal Run(CharacterTestTacticalDslSource source, CharacterTestRuntimeAiLabCompiledLogicProgram program = null)
            {
                try
                {
                    Target = Fixture.Target;
                    if (program == null) Assert.IsTrue(CharacterTestTacticalDslCompiler.TryCompile(source, Fixture.Inputs, out Compiled, out string issue), issue);
                    else Compiled = new CharacterTestTacticalDslCompilation(program, "native-channel-isolation",
                        new Dictionary<string, int>(), new Dictionary<string, string>());
                    Host = new CharacterTestBehaviorExecutionHost(Compiled.Program, Fixture.Runtime, Fixture.Runtime.ActionRuntime,
                        null, Fixture.Commands.Enqueue, CharacterTestDirectAiNavigationService.Instance, Fixture.Runtime.PerceptionFrameCache);
                }
                catch { Fixture.Dispose(); throw; }
            }
            internal void Facts()
            {
                if (Target == null) { Fixture.Facts.SetNoTarget(false); return; }
                CharacterTestDirectAiNavigationService.Instance.TryResolveMove(Fixture.Source, Target, 1f, out var navigation);
                float distance = Vector2.Distance(new Vector2(Fixture.Source.Transform.X, Fixture.Source.Transform.Z), new Vector2(Target.Transform.X, Target.Transform.Z));
                Fixture.Facts.SetTarget(Target.Id.Value, 0f, targetDistanceAvailable: false, targetRootDistance: distance,
                    navigation: navigation, selfActionRunning: Fixture.Source.Action.State == MxFramework.Game.CharacterTest.CharacterTestActionStateId.Running, targetVisible: true,
                    targetLineOfSightBlocked: false, targetLastSeenAgeFrames: 0, targetThreatScore: 1f, targetDead: !Target.CombatState.IsAlive);
            }
            internal void Start()
            { Fixture.AdvanceWorldOnly(); Facts(); Assert.IsTrue(Host.Start(Fixture.Source, Fixture.Facts, new RuntimeFrame(Fixture.NextFrame)), Host.LastFailureReason); }
            internal void Step()
            {
                Facts(); Host.Tick(Fixture.Source, Fixture.Facts, new RuntimeFrame(Fixture.NextFrame));
                var state = Host.CreateSnapshot().Execution;
                Fixture.AdvanceWorldOnly();
                var input = Fixture.Source.Input; var control = input.BehaviorControl;
                var sample = Fixture.Samples.Last();
                Rows.Add(new Row { World = Fixture.NextFrame - 1, Local = state.LocalFrame, Target = Target?.Id.Value ?? 0,
                    Timeline = Compiled.Program.ExecutionDebugMap.Timeline(state.Timeline), Action = sample.ActionInstance, ActionFrame = sample.ActionFrame,
                    HasMove = input.HasActiveMove, MoveX = sample.MoveX, MoveZ = sample.MoveZ,
                    FacingMode = control.FacingMode, FacingX = control.TargetXMillimeters, X = sample.SourceX, Z = sample.SourceZ, Yaw = sample.Yaw });
            }
            internal CharacterTestCharacterEntity SpawnB()
            {
                var physics = new CharacterTestCharacterPhysicsComponent(0.3f, 1.8f, 0.9f, 80, blocksCharacters: false, canBePushed: false);
                var definition = new CharacterTestCharacterDefinition(int.MaxValue - 1, "Target B", default, 1, default, physics);
                var target = Fixture.Runtime.World.Spawn(new CharacterTestCharacterSpawnRequest(definition.CharacterId, 1, 0, 0, 0,
                    CharacterTestCharacterControlOwner.None), definition);
                target.SetTransform(CharacterTestCharacterTransformComponent.FromDeterministic(Fixture.Source.Transform.XMillimeters - 4000,
                    0, Fixture.Source.Transform.ZMillimeters, 0));
                return target;
            }
            internal void Save(string name) => File.WriteAllText(Path.Combine(EvidenceDirectory(), name + ".json"), JsonConvert.SerializeObject(new
                { Compiled.DependencyIdentity, rows = Rows, state = Host.State.ToString(), source = "formal Host/command/action/Motion world", formalUiAccepted = false }, Formatting.Indented));
            public void Dispose()
            {
                try
                {
                    Save("trace-" + System.Text.RegularExpressions.Regex.Replace(TestContext.CurrentContext.Test.Name, "[^a-zA-Z0-9_-]", "_"));
                    Facts(); Host.Stop(Fixture.Source, Fixture.Facts, new RuntimeFrame(Fixture.NextFrame)); for (int i = 0; i < 10; i++) Step();
                }
                finally { Fixture.Dispose(); }
            }
        }
    }
}
