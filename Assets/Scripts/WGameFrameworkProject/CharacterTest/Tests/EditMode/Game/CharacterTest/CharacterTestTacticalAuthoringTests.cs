using System;
using System.IO;
using System.Linq;
using MxFramework.Game.CharacterTest;
using NUnit.Framework;
using UnityEngine;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterTestTacticalAuthoringTests
    {
        [Test]
        public void TimelineOperationObjectAndPositionRecallOperationTextKeepDistinctJsonTypes()
        {
            var attack=CharacterTestTacticalAuthoringTemplates.CreateSingleAttack();
            string attackJson=CharacterTestTacticalAuthoringJson.Write(attack);
            var restored=CharacterTestTacticalAuthoringJson.Read(attackJson);
            Assert.NotNull(restored.Timelines[0].Events[0].Operation);
            var recall=CharacterTestTacticalAuthoringTemplates.CreatePositionRecall("记录测试",
                new MxFramework.Game.CharacterTest.TacticalCard.TacticalPositionRecallAuthoring{Operation="Record",LifetimeFrames=600});
            string recallJson=CharacterTestTacticalAuthoringJson.Write(recall);
            Assert.AreEqual("Record",CharacterTestTacticalAuthoringJson.Read(recallJson).PositionRecall.Operation);
            var wrongRecall=Newtonsoft.Json.Linq.JObject.Parse(recallJson);wrongRecall["positionRecall"]["operation"]=1;
            Assert.Throws<Newtonsoft.Json.JsonSerializationException>(()=>CharacterTestTacticalAuthoringJson.Read(wrongRecall.ToString()));
            var wrongAttack=Newtonsoft.Json.Linq.JObject.Parse(attackJson);wrongAttack["timelines"][0]["events"][0]["operation"]="Movement";
            Assert.Throws<Newtonsoft.Json.JsonSerializationException>(()=>CharacterTestTacticalAuthoringJson.Read(wrongAttack.ToString()));
        }
        private static string Example(string name) => File.ReadAllText(Path.GetFullPath(Path.Combine(Application.dataPath,
            "../StorySource/玩法基石/任务/任务04作者源样例/" + name + ".json")));

        [TestCase("default", false)]
        [TestCase("no-approach", false)]
        [TestCase("offset-plus-four", false)]
        [TestCase("explicit-exit", false)]
        [TestCase("broken-action-reference", true)]
        public void CompleteDesignExamplesPreserveSourceAndAuthoringErrors(string name, bool invalid)
        {
            var source = CharacterTestTacticalAuthoringJson.Read(Example(name));
            string before = CharacterTestTacticalAuthoringJson.Write(source);
            var copy = source.Clone();
            Assert.AreEqual(before, CharacterTestTacticalAuthoringJson.Write(copy));
            Assert.AreEqual(CharacterTestTacticalAuthoringJson.SemanticFingerprint(source), CharacterTestTacticalAuthoringJson.SemanticFingerprint(copy));
            var diagnostics = CharacterTestTacticalAuthoringCompiler.Validate(copy);
            Assert.AreEqual(invalid, diagnostics.Count > 0, string.Join("\n", diagnostics));
            if (invalid) Assert.That(diagnostics.Any(d => d.Code == "MissingEvent"), Is.True);
            Assert.AreEqual(before, CharacterTestTacticalAuthoringJson.Write(source));
        }

        [TestCase("duplicate")]
        [TestCase("unknown")]
        [TestCase("numeric-string")]
        [TestCase("comment")]
        [TestCase("trailing-comma")]
        [TestCase("single-quotes")]
        [TestCase("future")]
        [TestCase("unknown-kind")]
        public void StrictReaderRejectsLossyOrNonStandardInput(string mutation)
        {
            string json = Example("default");
            switch (mutation)
            {
                case "duplicate": json = json.Replace("\"version\": 2", "\"version\": 2, \"version\": 2"); break;
                case "unknown": json = json.Replace("\"version\": 2", "\"version\": 2, \"unknownMeaning\": true"); break;
                case "numeric-string": json = json.Replace("\"version\": 2", "\"version\": \"2\""); break;
                case "comment": json = "/* source */" + json; break;
                case "trailing-comma": json = json.TrimEnd().Substring(0, json.TrimEnd().Length - 1) + ",}"; break;
                case "single-quotes": json = json.Replace("\"version\"", "'version'"); break;
                case "future": json = json.Replace("\"version\": 2", "\"version\": 3"); break;
                case "unknown-kind": json = json.Replace("\"kind\": \"CharacterAction\"", "\"kind\": \"FutureAction\""); break;
            }
            Assert.IsFalse(CharacterTestTacticalAuthoringJson.TryRead(json, out var source, out string issue));
            Assert.IsNull(source);
            Assert.IsNotEmpty(issue);
        }

        [Test]
        public void LiteralAndBoundPredicateCannotCoexistAndScopeIsNotOptional()
        {
            var source = CharacterTestTacticalAuthoringTemplates.CreateSingleAttack();
            var gate = source.Timelines[0].Events.Single(e => e.EventId == "release");
            var scope = gate.ConditionRoot.Children[1];
            scope.Children[0].Predicate = new CharacterTestRuntimeAiLabCardCondition();
            Assert.That(CharacterTestTacticalAuthoringCompiler.Validate(source).Any(d => d.Code == "AmbiguousPredicateSource"), Is.True);
            scope.Children[0].Predicate = null;
            gate.ConditionRoot.Children[1] = scope.Children[0];
            Assert.That(CharacterTestTacticalAuthoringCompiler.Validate(source).Any(d => d.Code == "TACTICAL_PREDICATE_SCOPE"), Is.True);
        }

        [Test]
        public void EndpointCycleIsRejectedButForwardEndReferenceIsLegal()
        {
            var source = CharacterTestTacticalAuthoringTemplates.CreateSingleAttack();
            var attack = source.Timelines[0].Events.Single(e => e.EventId == "attack");
            var approach = source.Timelines[0].Events.Single(e => e.EventId == "approach");
            attack.Start = CharacterTestTacticalAuthoringTemplates.Anchor("approach", "End");
            Assert.That(CharacterTestTacticalAuthoringCompiler.Validate(source).Any(d => d.Code == "AnchorCycle"), Is.True);
            attack.Start = CharacterTestTacticalAuthoringTemplates.Absolute(1);
            approach.Start = CharacterTestTacticalAuthoringTemplates.Anchor("attack", "End");
            approach.End = CharacterTestTacticalAuthoringTemplates.Anchor("attack", "End", 4);
            Assert.IsEmpty(CharacterTestTacticalAuthoringCompiler.Validate(source));
        }

        [Test]
        public void VersionOneImportGeneratesOnceAndDisplayMetadataDoesNotChangeExecutionIdentity()
        {
            var definition = new CharacterTestTacticalCardDefinition("导入", CharacterTestTacticalCardApproach.None);
            Assert.IsTrue(CharacterTestTacticalAuthoringTemplates.TryImportVersionOne(definition.Write(), out var source, out string issue), issue);
            Assert.That(source.Timelines[0].Events.Any(e => e.EventId == "approach"), Is.False);
            string before = CharacterTestTacticalAuthoringJson.SemanticFingerprint(source);
            source.Name = "重命名";
            source.Timelines[0].Title = "新显示名称";
            source.Timelines[0].Events[0].Title = "新事件标题";
            Assert.AreEqual(before, CharacterTestTacticalAuthoringJson.SemanticFingerprint(source));
            source.Timelines[0].Events[0].Priority++;
            Assert.AreNotEqual(before, CharacterTestTacticalAuthoringJson.SemanticFingerprint(source));
        }

        [Test]
        public void ActualEditsLowerToSharedExecutionTablesAndDoNotMutateSource()
        {
            using (var fixture = new CharacterTestTacticalCardRuntimeFixture())
            {
                foreach (string name in new[] { "default", "no-approach", "offset-plus-four", "explicit-exit" })
                {
                    var source = CharacterTestTacticalAuthoringJson.Read(Example(name));
                    string before = CharacterTestTacticalAuthoringJson.Write(source);
                    Assert.IsTrue(CharacterTestTacticalAuthoringCompiler.TryCompile(source, fixture.Inputs, out var compiled, out var issues), string.Join("\n", issues));
                    Assert.AreEqual(before, CharacterTestTacticalAuthoringJson.Write(source));
                    var events = compiled.Events.ToArray();
                    Assert.AreEqual(1, events.Single(e => e.EventId == "attack").StartFrame);
                    Assert.AreEqual(1 + fixture.Inputs.DurationFrames, events.Single(e => e.EventId == "attack").EndFrame);
                    if (name == "no-approach") Assert.That(events.Any(e => e.EventId == "approach"), Is.False);
                    else Assert.AreEqual(2 + fixture.Inputs.StartupEndFrame + (name == "offset-plus-four" ? 4 : 0),
                        events.Single(e => e.EventId == "approach").EndFrame);
                    Assert.AreEqual(name == "explicit-exit", events.Any(e => e.EventId == "exit"));
                    Assert.IsEmpty(compiled.Program.Stages);
                    Assert.IsTrue(CharacterTestTacticalAuthoringCompiler.TryCompile(source, fixture.Inputs, out var repeated, out _));
                    Assert.AreEqual(compiled.Program.Execution.Identity, repeated.Program.Execution.Identity);
                    if (name == "default")
                    {
                        Assert.AreEqual(1f, compiled.Program.Execution.Movements[0].Speed);
                        Assert.AreEqual(CharacterTestBehaviorExecutionMovement.DefaultStopTolerance, compiled.Program.Execution.Movements[0].StopTolerance);
                    }
                }
                var broken = CharacterTestTacticalAuthoringJson.Read(Example("broken-action-reference"));
                Assert.IsFalse(CharacterTestTacticalAuthoringCompiler.TryCompile(broken, fixture.Inputs, out var bad, out _));
                Assert.IsNull(bad);
            }
        }
    }
}
