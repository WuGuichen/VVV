using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MxFramework.CharacterAction;
using MxFramework.Game.CharacterTest;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace MxFramework.Tests.Game.CharacterTest
{
    // Pure projection fixtures: no scene, Unity object, player service or persistent write.
    public sealed class CharacterTestActionIntentDescriptionTests
    {
        private static CharacterTestTacticalPublishedSource Published(string file)
        {
            string path = Path.Combine("Assets/StreamingAssets/MxFramework/CharacterTest/runtime_ai_planner/behavior_cards", file);
            var card = ((JArray)JObject.Parse(File.ReadAllText(path))["cards"]).Single();
            return CharacterTestTacticalPublishedSource.Read((JObject)card["tacticalSource"]);
        }

        private static IEnumerable<CharacterTestTacticalDslNode> References(CharacterTestTacticalDslNode node)
        {
            if (node.Kind == "Reference") yield return node;
            foreach (var child in node.Children ?? new List<CharacterTestTacticalDslNode>())
                foreach (var value in References(child)) yield return value;
        }

        private static CharacterTestTacticalCardInputs Bound(CharacterTestTacticalDslSource document, string name)
        {
            var action = new CharacterActionConfig(new CharacterActionId(455), name,
                CharacterActionCategory.BasicAttack, CharacterActionTimelineAuthority.CharacterAuthored,
                Array.Empty<string>(), 0, 95, null, null, null, null);
            var summary = new CharacterActionSpatialSummary(action.ActionId, "description-action", "description-dummy",
                new CharacterActionSpatialMeasurement(CharacterActionRangeProber.Version, 50, 0, 5000,
                    new CharacterActionReferenceDummy(1, 1800, 300, 0, 900, 0)), CharacterActionProberStatus.Valid,
                new[] { new CharacterActionSpatialInterval(200, 800) }, 400, false, false,
                CharacterActionTacticalSector.Fallback);
            var metrics = new CharacterActionTacticalMetrics(action, summary.ActionContentHash, summary);
            var constructor = typeof(CharacterTestTacticalCardInputs).GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic).Single();
            var slot = (CharacterTestTacticalCardInputs)constructor.Invoke(new object[] { action, metrics, "description-slot", "", 1, true,
                null, null, summary, 1, null, CharacterTestTacticalInputRequirements.PrimaryAction | CharacterTestTacticalInputRequirements.SpatialSummary });
            var bindings = new Dictionary<string, CharacterTestTacticalCardInputs>(StringComparer.Ordinal);
            foreach (var reference in References(document.Root))
                foreach (var primary in document.Definitions.Single(d => d.DefinitionId == reference.DefinitionId).Source.Slots.Where(s => s.SlotId == "primary"))
                    bindings.Add(reference.NodeId + "/" + primary.SlotId, slot);
            return CharacterTestTacticalCardInputs.BindSlots(1, bindings);
        }

        private static CharacterTestTacticalDslSource ActionOnly(CharacterTestTacticalPublishedSource published)
        {
            var document = CharacterTestTacticalCardPresentationProjection.Document("isolated-action", "动作意图", published);
            var definition = document.Definitions.Single();
            var timeline = definition.Source.Timelines.Single();
            var block = timeline.EventBlocks.Single(b => b.Completion == "Action");
            block.Start = new CharacterTestTacticalFrameReference { Kind = "Absolute", Frame = 0 };
            timeline.EventBlocks.RemoveAll(b => b != block);
            timeline.Events.RemoveAll(e => !block.EventIds.Contains(e.EventId));
            document.Root = new CharacterTestTacticalDslNode { NodeId = "isolated-use", Kind = "Reference", DefinitionId = definition.DefinitionId };
            return document;
        }

        private static CharacterTestCardUiPreviewData Project(CharacterTestTacticalPublishedSource source,
            CharacterTestTacticalDslSource document, CharacterTestTacticalCardInputs inputs, MxFramework.UI.IMxUiTextProvider provider = null)
        {
            var data = new CharacterTestCardUiPreviewData { Id = document.CardId, Description = "旧静态制作说明" };
            CharacterTestTacticalCardPresentationProjection.Project(source, document, data, inputs,
                "projection fixture; execution preparation is tested separately", null, provider,
                1d / CharacterTestDeterministicUnits.DefaultTickRate, new List<string>());
            Assert.IsTrue(data.HasStructuredDescription, data.Description);
            return data;
        }

        [Test]
        public void BoundActionIntentUsesActualNameInTextInlineTemplateAndPreviewProjection()
        {
            var source = Published("tactical.bound-action.json");
            foreach (string name in new[] { "持剑四连击 01 · 第1段", "换气动作" })
            {
                var document = ActionOnly(source);
                var inputs = Bound(document, name);
                var data = Project(source, document, inputs);
                Assert.AreEqual("发动 " + name, data.Description);
                var action = data.Segments.Single().Action;
                Assert.IsTrue(action.IsActionOnly);
                Assert.IsTrue(action.HasSelectedAction);
                Assert.AreEqual(name, action.ActionName);
                Assert.AreEqual("发动 {action}", action.TextTemplate);
                Assert.AreEqual(data.Description, action.Text);
                Assert.AreEqual("发动 装配动作", data.UnboundDescription);
                var author = CharacterTestTacticalDslDescription.GenerateLocalized(document, inputs, attempted: true, cardFace: false);
                Assert.AreEqual(data.Description, author.Text, "The author description uses the same intent projection.");
            }
        }

        [Test]
        public void TheSameIntentRuleAppliesAcrossDifferentPublishedCompositions()
        {
            foreach (string file in new[] { "tactical.bound-action.json", "bc_2b0ehvtbzb.json", "bc_0tfz8nr4dz.json", "bc_n1xa3efw08.json" })
            {
                var source = Published(file);
                var document = CharacterTestTacticalCardPresentationProjection.Document(file, "组合预览", source);
                var data = Project(source, document, Bound(document, "实际装配动作"));
                var action = data.Segments.Single(s => s.Action?.IsActionOnly == true).Action;
                Assert.AreEqual("发动 实际装配动作", action.Text, file);
                Assert.That(data.Description, Does.Contain(CharacterTestTacticalDescriptionFlow.Term(action.Text)), file);
                Assert.That(data.Description, Does.Not.Contain("面向目标，发动"), file);
                Assert.IsTrue(data.Segments.Any(s => s.Arrival != null), "Other movement intent text remains separate.");
                if (file == "bc_n1xa3efw08.json")
                {
                    Assert.AreEqual("等待2秒", data.Segments[0].Text);
                    Assert.That(data.Description, Does.StartWith("等待2秒"));
                    Assert.That(data.Description, Does.Not.Contain("原地等待两秒"));
                    Assert.That(data.Description, Does.Not.Contain("继续后续行为"));
                }
            }
        }

        [Test]
        public void UnboundActionKeepsExistingActionPlaceholderAndChoicePrompt()
        {
            var source = Published("tactical.bound-action.json");
            var data = Project(source, ActionOnly(source), CharacterTestTacticalCardInputs.Unbound(1));
            var action = data.Segments.Single().Action;
            Assert.AreEqual("发动 装配动作", data.Description);
            Assert.AreEqual("选择动作", action.ActionPrompt);
            Assert.IsFalse(action.HasSelectedAction);
            Assert.AreEqual("—", action.DurationText);
        }

        [Test]
        public void ProjectionDoesNotChangePublishedIdentityFacingOperationsOrAdmissionConditions()
        {
            var source = Published("tactical.bound-action.json");
            string original = source.ToJson().ToString();
            long hash = source.SemanticHash;
            var document = CharacterTestTacticalCardPresentationProjection.Document("identity", "预览", source);
            string documentBefore = CharacterTestTacticalDslJson.Write(document);
            var data = Project(source, document, Bound(document, "真实绑定"));
            Assert.IsTrue(data.Segments.Any(s => s.Action?.IsActionOnly == true));
            Assert.AreEqual(documentBefore, CharacterTestTacticalDslJson.Write(document));
            Assert.AreEqual(original, source.ToJson().ToString());
            Assert.AreEqual(hash, source.SemanticHash);
            var definition = document.Definitions.Single();
            Assert.IsTrue(definition.Source.Timelines.Single().Events.Any(e => e.Operation.Kind == "TurnToTarget" && e.Operation.Target == "currentCombatTarget"));
            Assert.IsTrue(definition.Source.PredicateBindings.Any(b => b.Kind == "RootDistanceRange"));
            Assert.IsTrue(definition.Source.PredicateBindings.Any(b => b.Kind == "TargetFacing"));
        }

        [Test]
        public void ActualTsvProviderUsesApprovedActionAndWaitDescriptions()
        {
            var localization = CharacterTestLocalizationRuntime.Create("Assets/StreamingAssets/MxFramework/Localization/character_test.tsv");
            Assert.AreEqual("发动 {action}", localization.Resolve("ui.character_test.tactical.description.face_action", "incorrect fallback"));
            Assert.AreEqual("等待2秒", localization.Resolve("ui.character_test.tactical.description.wait_two_seconds", "等待2秒"));
            var source = Published("tactical.bound-action.json");
            var document = ActionOnly(source);
            var data = Project(source, document, Bound(document, "Light Attack"), localization.TextProvider);
            Assert.AreEqual("发动 Light Attack", data.Description);
            Assert.AreEqual("发动 {action}", data.Segments.Single().TextTemplate);
            var wait = CharacterTestTacticalAuthoringTemplates.CreateWaitTwoSecondsDefinition();
            var waitDocument = new CharacterTestTacticalDslSource { CardId = "wait-provider",
                Root = new CharacterTestTacticalDslNode { NodeId = "wait", Kind = "Reference", DefinitionId = wait.DefinitionId } };
            waitDocument.Definitions.Add(wait);
            Assert.AreEqual("等待2秒", CharacterTestTacticalDslDescription.GenerateLocalized(waitDocument,
                CharacterTestTacticalCardInputs.Unbound(1), attempted: true, cardFace: true,
                textProvider: localization.TextProvider).Text);
        }

        [Test]
        public void ActualProviderComposesWaitApproachAndBoundActionExactlyWithoutChangingParameters()
        {
            var provider = CharacterTestLocalizationRuntime.Create("Assets/StreamingAssets/MxFramework/Localization/character_test.tsv").TextProvider;
            var source = Published("bc_n1xa3efw08.json");
            var document = CharacterTestTacticalCardPresentationProjection.Document("approved-copy", "错拍再攻 · 测试", source);
            string before = CharacterTestTacticalDslJson.Write(document);
            var data = Project(source, document, Bound(document, "Light Attack"), provider);
            Assert.AreEqual("等待2秒 → 常规移动 → 发动 Light Attack", data.Description);
            var arrival = data.Segments.Single(s => s.Arrival != null).Arrival;
            Assert.IsFalse(arrival.IsRetreat);
            Assert.AreEqual("常规移动", arrival.Text);
            Assert.IsNotEmpty(arrival.DurationText);
            Assert.IsNotEmpty(arrival.RangeText);
            Assert.IsNotNull(arrival.Movement);
            var action = data.Segments.Single(s => s.Action?.IsActionOnly == true).Action;
            Assert.AreEqual("Light Attack", action.ActionName);
            Assert.IsNotEmpty(action.FacingText);
            Assert.IsNotEmpty(action.RangeText);
            Assert.AreEqual(before, CharacterTestTacticalDslJson.Write(document));
            Console.WriteLine("ACTUAL_PROVIDER_COMPOSED_DESCRIPTION = " + data.Description);
        }

        [Test]
        public void OrdinaryOrbitUsesActualProviderAndRetainsDurationRangeDirectionAndExecutionSource()
        {
            var localization = CharacterTestLocalizationRuntime.Create("Assets/StreamingAssets/MxFramework/Localization/character_test.tsv");
            Assert.AreEqual("环绕移动", localization.Resolve("ui.character_test.tactical.description.orbit_face", "incorrect fallback"));
            var source = Published("bc_nfwdrgtcyb.json");
            var document = CharacterTestTacticalCardPresentationProjection.Document("orbit-text", "绕侧进攻", source);
            string before = CharacterTestTacticalDslJson.Write(document);
            string sourceBefore = source.ToJson().ToString();
            var data = Project(source, document, Bound(document, "Light Attack"), localization.TextProvider);
            Assert.AreEqual("环绕移动 → 常规移动 → 发动 Light Attack", data.Description);
            var orbit = data.Segments.Single(s => s.Orbit != null).Orbit;
            Assert.AreEqual("环绕移动", orbit.Text);
            Assert.AreEqual(12, orbit.DurationFrames);
            Assert.AreEqual("1.5～3 米", orbit.RangeText);
            Assert.AreEqual(0, orbit.Side);
            Assert.AreEqual("自动", orbit.DirectionText);
            Assert.IsNotNull(orbit.Movement);
            Assert.AreEqual(before, CharacterTestTacticalDslJson.Write(document));
            Assert.AreEqual(sourceBefore, source.ToJson().ToString());
            Console.WriteLine("ACTUAL_PROVIDER_ORBIT_DESCRIPTION = " + data.Description);
        }

        [Test]
        public void RealIfBranchKeepsCommonContinuationOutsideTheBranchAndNeverExposesActionAdmissionAsBranch()
        {
            var source = Published("tactical.bound-action.json");
            var document = ActionOnly(source);
            var action = document.Root;
            var wait = CharacterTestTacticalAuthoringTemplates.CreateWaitTwoSecondsDefinition();
            document.Definitions.Add(wait);
            var condition = new CharacterTestRuntimeAiLabBehaviorRecipeConditionNode { NodeId = "if.health",
                Kind = CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.FactPredicate,
                Predicate = new CharacterTestRuntimeAiLabCardCondition { ConditionId = "if.health.value",
                    Fact = CharacterTestRuntimeAiLabCardConditionFact.SelfHealthRatio,
                    Operator = CharacterTestRuntimeAiLabCardConditionOperator.LessOrEqual, Value = 0.5f } };
            var branch = new CharacterTestTacticalDslNode { NodeId = "if", Kind = "If", Condition = condition,
                Children = new List<CharacterTestTacticalDslNode> { new CharacterTestTacticalDslNode { NodeId = "conditional-wait", Kind = "Reference", DefinitionId = wait.DefinitionId } } };
            document.Root = new CharacterTestTacticalDslNode { NodeId = "flow-root", Kind = "Sequence",
                Children = new List<CharacterTestTacticalDslNode> { branch, action } };
            string before = CharacterTestTacticalDslJson.Write(document);
            var provider = CharacterTestLocalizationRuntime.Create("Assets/StreamingAssets/MxFramework/Localization/character_test.tsv").TextProvider;
            var data = Project(source, document, Bound(document, "Light Attack"), provider);
            Assert.AreEqual("若自身血量不高于50%：\n  等待2秒\n不成立：略过本段\n↓\n发动 Light Attack", data.Description);
            var flow = data.DescriptionFlow;
            Assert.IsTrue(flow.Complete);
            Assert.AreEqual("if", flow.Root.Children[0].Kind);
            Assert.AreEqual("sequence", flow.Root.Children[1].Kind);
            Assert.AreEqual(action.NodeId, flow.Root.Children[1].NodeId);
            Assert.AreEqual(1, flow.Rows.Single(r => r.SegmentIndex == 0).Indent);
            Assert.AreEqual(0, flow.Rows.Single(r => r.SegmentIndex == 1).Indent);
            Assert.That(data.Description, Does.Not.Contain("朝向"));
            Assert.That(data.Description, Does.Not.Contain("存活目标"));
            Assert.AreEqual(before, CharacterTestTacticalDslJson.Write(document));
            Console.WriteLine("IF_FLOW = " + data.Description.Replace("\n", " | "));
            branch.Children = new List<CharacterTestTacticalDslNode> { new CharacterTestTacticalDslNode { NodeId = "exit-true", Kind = "Exit" } };
            data = Project(source, document, Bound(document, "Light Attack"), provider);
            Assert.That(data.Description, Does.EndWith("发动 Light Attack"), "A conditional exit cannot remove the false path's common continuation.");
            branch.Kind = "Wait"; branch.MaximumWaitFrames = 120;
            var timeout = CharacterTestTacticalDslDescription.GenerateLocalized(document, Bound(document, "Light Attack"), attempted: true,
                cardFace: true, textProvider: provider);
            Assert.That(timeout.Text, Does.Contain("超时：结束本卡"));
            Assert.That(timeout.Text, Does.Not.Contain("Light Attack"), "Exit on success and exit on timeout make the following action unreachable.");
        }

        [Test]
        public void SequentialArrowContractDoesNotInventParallelCompositionOrModifySingleIntent()
        {
            var source = Published("bc_nfwdrgtcyb.json");
            var document = CharacterTestTacticalCardPresentationProjection.Document("arrow", "arrow", source);
            var data = Project(source, document, Bound(document, "Light Attack"));
            Assert.AreEqual("环绕移动 → 常规移动 → 发动 Light Attack", data.Description);
            Assert.AreEqual(data.Description, data.DescriptionFlow.Text);
            Assert.AreEqual(3, data.DescriptionFlow.Rows.Length);
            Assert.IsTrue(data.DescriptionFlow.Rows.Skip(1).All(r => r.Prefix == " → "));
            Assert.That(data.Description, Does.Not.Contain("。"));
            Assert.That(data.Description, Does.Not.Contain("随后"));
            document.Root.Kind = "Parallel";
            var unknown = CharacterTestTacticalDslDescription.GenerateLocalized(document, Bound(document, "Light Attack"), attempted: true, cardFace: true);
            Assert.IsFalse(unknown.DescriptionFlow.Complete);
            Assert.That(unknown.Text, Does.Not.Contain(" → "), "Unsupported parallel semantics cannot be flattened into an arrow chain.");
            var single = ActionOnly(Published("tactical.bound-action.json"));
            var one = Project(Published("tactical.bound-action.json"), single, Bound(single, "Light Attack"));
            Assert.AreEqual("发动 Light Attack", one.Description);
        }

        [Test]
        public void ConciseApproachRequiresExistingValidatedIntentSignatureAndDoesNotRenameRetreat()
        {
            var source = Published("tactical.bound-action.json");
            var document = CharacterTestTacticalCardPresentationProjection.Document("signature", "signature", source);
            var definition = document.Definitions.Single();
            var timeline = definition.Source.Timelines.Single();
            var block = timeline.EventBlocks.Single(b => b.Completion == "Arrived");
            var events = timeline.Events.Where(e => block.EventIds.Contains(e.EventId)).ToArray();
            var movement = events.Single(e => e.Operation.Kind == "Movement").Operation.Movement;
            bool Parse(out CharacterTestTacticalDistanceArrivalHint hint) => CharacterTestTacticalDistanceArrivalHint.TryParse(
                definition, "signature", block, events, 30, 1d / CharacterTestDeterministicUnits.DefaultTickRate,
                CharacterTestTacticalCardInputs.Unbound(1), null, out hint);
            Assert.IsTrue(Parse(out var approach));
            Assert.AreEqual("常规移动", approach.Text);
            movement.Strategy = CharacterTestRuntimeAiLabMovementStrategy.RetreatWhenClose;
            Assert.IsTrue(Parse(out var retreat));
            Assert.IsTrue(retreat.IsRetreat);
            Assert.That(retreat.Text, Does.Contain("后撤"));
            Assert.AreNotEqual(approach.Text, retreat.Text);
            events.Single(e => e.Operation.Kind == "TurnToTarget").Operation.Target = "differentTarget";
            Assert.IsFalse(Parse(out _), "Different facing semantics must not use the concise approach intent.");
        }

        [Test]
        public void WaitTwoSecondsTextIsConciseWhileTimingAndPassiveRuleRemainUnchanged()
        {
            var definition = CharacterTestTacticalAuthoringTemplates.CreateWaitTwoSecondsDefinition();
            var document = new CharacterTestTacticalDslSource { CardId = "wait-two-seconds", Name = "原地等待两秒",
                Root = new CharacterTestTacticalDslNode { NodeId = "wait-use", Kind = "Reference", DefinitionId = definition.DefinitionId } };
            document.Definitions.Add(definition);
            string before = CharacterTestTacticalDslJson.Write(document);
            var description = CharacterTestTacticalDslDescription.GenerateLocalized(document, CharacterTestTacticalCardInputs.Unbound(1), attempted: true, cardFace: true);
            Assert.AreEqual("等待2秒", description.Text);
            Assert.AreEqual(description.Text, description.Segments.Single().Text);
            var author = CharacterTestTacticalDslDescription.GenerateLocalized(document, CharacterTestTacticalCardInputs.Unbound(1), attempted: true, cardFace: false);
            Assert.AreEqual(description.Text, author.Text);
            var block = definition.Source.Timelines.Single().EventBlocks.Single();
            Assert.AreEqual("Delay", block.Completion);
            Assert.IsTrue(block.PassiveWait);
            Assert.AreEqual(CharacterTestDeterministicUnits.DefaultTickRate * 2, block.Timing.MinimumFrames);
            Assert.AreEqual(CharacterTestDeterministicUnits.DefaultTickRate * 2, block.Timing.DurationFrames);
            Assert.AreEqual(before, CharacterTestTacticalDslJson.Write(document));
            Console.WriteLine("WAIT_TWO_SECONDS actual Description = " + description.Text);
        }

        [Test]
        public void FacingAndWaitShortTextsRetainTheirExactLimitsAndTargetRulesInDetails()
        {
            var provider = CharacterTestLocalizationRuntime.Create("Assets/StreamingAssets/MxFramework/Localization/character_test.tsv").TextProvider;
            foreach (var definition in new[] { CharacterTestTacticalAuthoringTemplates.CreateFacingTargetDefinition(7, 45),
                CharacterTestTacticalAuthoringTemplates.CreateWaitOpportunityDefinition(90),
                CharacterTestTacticalAuthoringTemplates.CreateWaitTargetDistanceReferenceDefinition(180) })
            {
                var document = new CharacterTestTacticalDslSource { CardId = "short-rules", Root = new CharacterTestTacticalDslNode {
                    NodeId = "intent", Kind = "Reference", DefinitionId = definition.DefinitionId },
                    Definitions = new List<CharacterTestTacticalSharedDefinition> { definition } };
                string before = CharacterTestTacticalDslJson.Write(document);
                var result = CharacterTestTacticalDslDescription.GenerateLocalized(document, CharacterTestTacticalCardInputs.Unbound(0),
                    attempted: true, cardFace: true, textProvider: provider);
                string expected = definition.DefinitionId == CharacterTestTacticalAuthoringTemplates.FacingTargetDefinitionId ? "面向目标"
                    : definition.DefinitionId == CharacterTestTacticalAuthoringTemplates.WaitTargetDistanceReferenceDefinitionId ? "等待目标进入指定距离" : "等待条件成立";
                Assert.AreEqual(expected, result.Text);
                Assert.AreEqual(before, CharacterTestTacticalDslJson.Write(document));
                StringAssert.Contains("超时", result.Details);
                if (expected == "面向目标") { StringAssert.Contains("7°", result.Details); StringAssert.Contains("0.75", result.Details); }
                else if (expected == "等待条件成立") { StringAssert.Contains("1.5", result.Details); StringAssert.Contains("攻击窗口", result.Details); StringAssert.Contains("不自动攻击", result.Details); }
                else { StringAssert.Contains("3 秒", result.Details); StringAssert.Contains("不主动接近", result.Details); }
            }
        }

        [Test]
        public void GuardShortTextKeepsFrontBlockSectorCohesionAndDurationInDetails()
        {
            var definition = CharacterTestTacticalAuthoringTemplates.CreateOrdinaryGuardDefinition(180);
            string before = CharacterTestTacticalCompositionJson.WriteShared(definition);
            var timeline = definition.Source.Timelines.Single(); var block = timeline.EventBlocks.Single();
            var provider = CharacterTestLocalizationRuntime.Create("Assets/StreamingAssets/MxFramework/Localization/character_test.tsv").TextProvider;
            Assert.IsTrue(CharacterTestTacticalBlockDescription.TryOrdinaryGuardText(definition, "guard", block,
                timeline.Events.ToArray(), 1d / 60, null, provider, out var text));
            Assert.AreEqual("防御", text);
            Assert.IsTrue(CharacterTestTacticalBlockDescription.TryOrdinaryGuardText(definition, "guard", block,
                timeline.Events.ToArray(), 1d / 60, null, provider, out var details, includeDetails: true));
            StringAssert.Contains("3 秒", details); StringAssert.Contains("90°", details); StringAssert.Contains("散势", details);
            string tooltip = CharacterTestFairyGuiTacticalDescriptionHints.TooltipText(new CharacterTestCardUiPreviewData {
                Description = text, DescriptionDetails = details });
            StringAssert.StartsWith("防御\n\n", tooltip); StringAssert.Contains("90°", tooltip); StringAssert.Contains("散势", tooltip);
            Assert.AreEqual(before, CharacterTestTacticalCompositionJson.WriteShared(definition));
            provider.SetLocale(new MxFramework.UI.MxUiLocaleId("en-US"));
            Assert.IsTrue(CharacterTestTacticalBlockDescription.TryOrdinaryGuardText(definition, "guard", block,
                timeline.Events.ToArray(), 1d / 60, null, provider, out text));
            Assert.AreEqual("Guard", text);
        }

        [Test]
        public void PointMoveShortTextRetainsReviewedRangeSpeedArrivalAndSettlementRules()
        {
            var legacy = CharacterTestTacticalAuthoringTemplates.CreatePointMove("point rules",
                new MxFramework.Game.CharacterTest.TacticalCard.TacticalPointMoveAuthoring {
                    RangeMillimeters = 7500, ArrivalMillimeters = 250, MaximumFrames = 240, SpeedMilli = 800, RequireReachable = true });
            var source = CharacterTestTacticalPublishedSource.Read(new JObject { ["schema"] = CharacterTestTacticalPublishedSource.Schema,
                ["legacy"] = JObject.Parse(CharacterTestTacticalAuthoringJson.Write(legacy)) });
            string before = source.ToJson().ToString(); long hash = source.SemanticHash;
            var data = new CharacterTestCardUiPreviewData();
            var provider = CharacterTestLocalizationRuntime.Create("Assets/StreamingAssets/MxFramework/Localization/character_test.tsv").TextProvider;
            CharacterTestTacticalCardPresentationProjection.Project(source,
                CharacterTestTacticalCardPresentationProjection.Document("point", "point", source), data,
                CharacterTestTacticalCardInputs.Unbound(0), null, null, provider, 1d / 60, new List<string>());
            Assert.AreEqual("移动至指定位置", data.Description);
            foreach (string rule in new[] { "7.5 米", "0.8 倍", "0.25 米", "4 秒", "有效移动接纳", "取消退回预留", "路径" })
            {
                StringAssert.Contains(rule, data.DescriptionDetails);
                StringAssert.Contains(rule, CharacterTestFairyGuiTacticalDescriptionHints.TooltipText(data));
            }
            Assert.AreEqual(before, source.ToJson().ToString()); Assert.AreEqual(hash, source.SemanticHash);
        }

        [Test]
        public void BehindTargetShortTextsDistinguishCompletionFromContinuousMaintenance()
        {
            var provider = CharacterTestLocalizationRuntime.Create("Assets/StreamingAssets/MxFramework/Localization/character_test.tsv").TextProvider;
            foreach (bool maintain in new[] { false, true })
            {
                var definition = CharacterTestTacticalAuthoringTemplates.CreateBehindTargetDefinition(maintain,
                    35, 45, 1, 2, .1f, .2f, 180, 1, 1, 12);
                string before = CharacterTestTacticalCompositionJson.WriteShared(definition);
                var timeline = definition.Source.Timelines.Single(); var block = timeline.EventBlocks.Single();
                Assert.IsTrue(CharacterTestTacticalOrbitHint.TryParse(definition, "behind", block, timeline.Events.ToArray(),
                    180, 1d / 60, null, provider, out var hint));
                Assert.AreEqual(maintain ? "保持在目标背后" : "绕至目标背后", hint.Text);
                StringAssert.Contains(maintain ? "持续保持" : "停止并完成", hint.DetailsText);
                StringAssert.Contains("较短环绕弧", hint.DetailsText); StringAssert.Contains("3 秒", hint.DetailsText);
                Assert.AreEqual(maintain ? "Timed" : "Arrived", block.Completion);
                Assert.AreEqual(before, CharacterTestTacticalCompositionJson.WriteShared(definition));
            }
        }
    }
}
