using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using MxFramework.CharacterAction;
using MxFramework.Game.CharacterTest;
using MxFramework.Game.CharacterTest.TacticalCard;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterTestCardUiPreviewTests
    {
        private GameObject _owner;
        private CharacterTestRuntimeAiPlannerDevSceneController _controller;
        private CharacterTestConfiguredCardLibrary _store;
        private string _directory, _preference;

        [SetUp]
        public void SetUp()
        {
            _directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/CardUiPreviewTests", Guid.NewGuid().ToString("N")));
            _store = new CharacterTestConfiguredCardLibrary(Path.Combine(_directory, "cards"), Path.Combine(_directory, "recovery"));
            _owner = new GameObject("Card UI preview test");
            _owner.SetActive(false);
            _controller = _owner.AddComponent<CharacterTestRuntimeAiPlannerDevSceneController>();
            _controller.PrepareInitialSetupState(restorePersistedDraft: false);
            typeof(CharacterTestRuntimeAiPlannerDevSceneController).GetField("_testCardLibrary", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(_controller, _store);
            _preference = PlayerPrefs.GetString("RuntimeAiLab.TestCard.Last."+_controller.TestCardRole, "");
        }

        [TearDown]
        public void TearDown()
        {
            string key = "RuntimeAiLab.TestCard.Last."+_controller.TestCardRole;
            if (_preference.Length == 0) PlayerPrefs.DeleteKey(key); else PlayerPrefs.SetString(key, _preference);
            UnityEngine.Object.DestroyImmediate(_owner);
            string allowed = Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/CardUiPreviewTests")) + Path.DirectorySeparatorChar;
            if (_directory.StartsWith(allowed, StringComparison.OrdinalIgnoreCase) && Directory.Exists(_directory))
                Directory.Delete(_directory, true);
        }

        private CharacterTestConfiguredCard Create(bool attack = false)
        {
            var source = attack ? CharacterTestTacticalAuthoringTemplates.CreateSingleAttack()
                : CharacterTestTacticalAuthoringTemplates.CreateMovement(CharacterTestRuntimeAiLabMovementStrategy.MaintainDistance);
            source.Schema = CharacterTestTacticalAuthoringSource.CurrentSchema;
            source.Version = 2;
            if (!attack) source.Slots.Clear();
            var card = new CharacterTestConfiguredCard {
                Id = Guid.NewGuid().ToString("N"), Name = "预览样例", OwnerCharacter = "",
                Source = new JObject { ["schema"] = CharacterTestTacticalPublishedSource.Schema,
                    ["legacy"] = JObject.Parse(CharacterTestTacticalAuthoringJson.Write(source)) }
            };
            card.Metadata.Cost = 7;
            card.Metadata.ShortDescription = "不能冒充自动说明的旧制作备注";
            if (attack) card.Actions["primary"] = 301;
            Assert.IsTrue(_store.Save(card, 0, null, out string issue), issue);
            return card;
        }

        [Test]
        public void PreviewReadsSourceWithoutOpeningEditingOrWritingCard()
        {
            var card = Create();
            var workspace = _controller.DraftWorkspace;
            string selection = _controller.TacticalLibrarySelectedKey;
            var saved = _store.List(_controller.TestCardRole).Single();
            Assert.IsTrue(_controller.TryGetConfiguredCardUiPreview(card.Id, out var data, out string issue), issue);
            Assert.AreEqual(card.Name, data.Name);
            Assert.AreEqual(7, data.Cost);
            Assert.IsFalse(data.Dirty);
            Assert.IsNotEmpty(data.Description);
            Assert.AreNotEqual(card.Metadata.ShortDescription, data.Description);
            Assert.AreSame(workspace, _controller.DraftWorkspace);
            Assert.AreEqual(selection, _controller.TacticalLibrarySelectedKey);
            Assert.IsNull(_controller.TacticalCardContext);
            Assert.IsNull(_store.ReadRecovery(_controller.TestCardRole, card.Id));
            Assert.AreEqual(saved.FileHash, _store.List(_controller.TestCardRole).Single().FileHash);
        }

        [Test]
        public void PreviewShowsUnsavedFaceChangesWithoutSavingThem()
        {
            var card = Create();
            var face = _controller.ConfiguredCardFaces().Single();
            Assert.IsTrue(_controller.EditConfiguredCardFace(card.Id, _controller.TestCardRole, face.Token,
                "未保存名称", null, null, out string issue), issue);
            Assert.IsTrue(_controller.TryGetConfiguredCardUiPreview(card.Id, out var data, out issue), issue);
            Assert.AreEqual("未保存名称", data.Name);
            Assert.IsTrue(data.Dirty);
            Assert.AreEqual(card.Name, _store.List(_controller.TestCardRole).Single().Name);
            Assert.IsNull(_controller.TacticalCardContext);
        }

        [Test]
        public void ActualConfiguredProducerAndExactPublishedBindingSharePresentation()
        {
            Assert.IsTrue(_controller.SelectSetupCharacterId(618034977), "Use the same actual role as the main acceptance slice.");
            var published = CharacterTestRuntimeAiPlannerProjectCatalogLoader.Load();
            Assert.IsTrue(published.Success, published.Diagnostic);
            var sourceCard = published.CardCatalog.Cards.Where(value => value.StableCardId == "tactical.bound-action")
                .OrderByDescending(value => value.Revision).First();
            Assert.IsTrue(published.CardCatalog.TryGetExact(sourceCard.StableCardId, sourceCard.Revision, out var selectedRevision));
            Assert.AreSame(sourceCard, selectedRevision, "Keep one exact formal revision throughout both real producers.");
            var hero = CharacterTestPlayableCharacterCatalog.Discover().First(value => value.CharacterId == _controller.CardDescriptionCharacterId);
            Assert.NotNull(hero.TacticalDefaults);
            var catalog = Newtonsoft.Json.JsonConvert.DeserializeObject<CharacterTacticalContentCatalog>(
                File.ReadAllText("Assets/StreamingAssets/MxFramework/CharacterTest/tactical_content.json"));
            CharacterTacticalActionEligibility.Load().Project(catalog, CharacterTestActionBrowserCatalog.Get());
            catalog.SetPublishedCards(new[] { sourceCard });
            var action = catalog.Actions.First(value => value.ActionId == 301 && value.CharacterStableIds.Contains(hero.TacticalDefaults.CharacterStableId));
            var configured = new CharacterTestConfiguredCard {
                Id = Guid.NewGuid().ToString("N"), Name = sourceCard.Metadata.DisplayName, Source = sourceCard.TacticalSource.ToJson(),
                BaseKey = sourceCard.StableCardId, BaseRevision = sourceCard.Revision.ToString(System.Globalization.CultureInfo.InvariantCulture),
                BaseFingerprint = sourceCard.SemanticHash.ToString(System.Globalization.CultureInfo.InvariantCulture),
                Metadata = new CharacterTestConfiguredCardMetadata { Cost = sourceCard.Metadata.Cost, IconKey = sourceCard.Metadata.IconKey,
                    ShortDescription = sourceCard.Metadata.ShortDescription },
                Actions = new Dictionary<string, int> { [catalog.Cards.Single().ActionSlotPath] = action.ActionId }
            };
            Assert.IsTrue(_store.Save(configured, 0, null, out string issue), issue);
            string savedHash = _store.List(_controller.TestCardRole).Single().FileHash;
            // Call the real test-library producer; never inject preparation's DTO into the preview.
            Assert.IsTrue(_controller.TryGetConfiguredCardUiPreview(configured.Id, null, out var preview, out issue,
                automaticallyPrepareSummaries: true), issue);
            var tickSummary = typeof(CharacterTestRuntimeAiPlannerDevSceneController)
                .GetMethod("TickRuntimeSummary", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(tickSummary);
            var measurementWait = System.Diagnostics.Stopwatch.StartNew();
            for (int frame = 0; !preview.Prepared && frame < 20000 && measurementWait.Elapsed.TotalSeconds < 90; frame++)
            {
                tickSummary.Invoke(_controller, null);
                Assert.IsTrue(_controller.TryGetConfiguredCardUiPreview(configured.Id, null, out preview, out issue,
                    automaticallyPrepareSummaries: true), issue);
            }
            Assert.IsTrue(preview.Prepared, "Real automatic measurement must finish before comparing ready producers: " + preview.Notice);
            var reference = new MxFramework.CharacterApplication.CharacterTacticalCardReference { InstanceId = 1,
                CardId = sourceCard.StableCardId, Revision = sourceCard.Revision,
                ContentHash = sourceCard.ContentHash.ToString(System.Globalization.CultureInfo.InvariantCulture) };
            var defaults = new MxFramework.CharacterApplication.CharacterTacticalDefaults { CharacterStableId = hero.TacticalDefaults.CharacterStableId,
                ActionIds = new List<string> { action.Id }, Cards = new List<MxFramework.CharacterApplication.CharacterTacticalCardReference> { reference } };
            T Complete<T>(Task<T> task) { Assert.IsTrue(task.IsCompleted, "MemoryStore must not block Unity's context."); return task.GetAwaiter().GetResult(); }
            var player = Complete(CharacterTacticalPlayerService.OpenAsync(catalog, new[] { defaults }, new PresentationMemoryStore()));
            var preparation = Complete(TacticalBattlePreparationService.OpenAsync(player, new PresentationMemoryStore(), new[] { hero }));
            var entity = preparation.Working.Entities.Single();
            Assert.AreEqual(action.Id, entity.Loadout.Bindings.Single().TacticalActionId);
            var adapter = _owner.AddComponent<TacticalBattlePreparationController>(); adapter.Select(entity.EntityId);
            var view = new CharacterTestFairyGuiTacticalPreparationView();
            void Field(string name, object value) => typeof(CharacterTestFairyGuiTacticalPreparationView)
                .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(view, value);
            Field("_controller", adapter); Field("_preparation", preparation); Field("_player", player);
            try
            {
                reference = entity.Loadout.Cards.Single(); var exact = player.Catalog.ExactCard(reference); Assert.NotNull(exact);
                var display = TacticalCardDisplayData.FromDefinition(new TacticalActionCardDefinition(exact.CardId, exact.DisplayName,
                    exact.Description, exact.MotionCost, exact.CardId, exact.Revision, exact.ContentHash, reference.InstanceId, exact.IconKey));
                var actual = (CharacterTestCardUiPreviewData)typeof(CharacterTestFairyGuiTacticalPreparationView)
                    .GetMethod("PresentationData", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(view, new object[] { exact, reference, display });
                Assert.NotNull(exact.PresentationSource, "Initial catalog clone must retain the immutable published source.");
                Assert.AreEqual(sourceCard.TacticalSource.SemanticHash, exact.PresentationSource.SemanticHash);
                Assert.IsTrue(player.TryRefreshPublishedCards(catalog.Cards, out int invalidReferences, out issue), issue);
                Assert.AreEqual(0, invalidReferences);
                Assert.AreEqual(sourceCard.TacticalSource.SemanticHash, player.Catalog.ExactCard(reference).PresentationSource.SemanticHash,
                    "Catalog refresh must retain the same exact published presentation source.");
                Assert.AreEqual(preview.Name, actual.Name); Assert.AreEqual(preview.Cost, actual.Cost); Assert.AreEqual(preview.IconKey, actual.IconKey);
                Assert.AreEqual(preview.Description, actual.Description); Assert.AreEqual(preview.DescriptionDetails, actual.DescriptionDetails);
                Assert.AreEqual(preview.Prepared, actual.Prepared); Assert.AreEqual(preview.HasStructuredDescription, actual.HasStructuredDescription);
                Assert.IsTrue(actual.Prepared, actual.Notice);
                Assert.IsTrue(preview.Prepared, preview.Notice);
                Assert.IsNotEmpty(actual.Hints);
                Assert.IsTrue(actual.Hints[0].MinimumDistance.HasValue);
                Assert.IsTrue(actual.Hints[0].MaximumDistance.HasValue);
                Assert.IsTrue(actual.Hints[0].FacingTolerance.HasValue);
                var measured = new CharacterActionTacticalMetricsStore().Query(
                    CharacterTestTacticalPreparationCatalog.Role(hero.CharacterId).Input(action.ActionId),
                    CharacterTestTacticalSummaryPreparation.StandardMeasurement());
                Assert.AreEqual(CharacterActionTacticalMetricsLoadStatus.Valid, measured.Status, measured.Issue);
                Assert.IsTrue(CharacterTestTacticalCardInputs.TrySelectRange(measured.SpatialSummary,
                    out var selectedRange, out string rangeIssue), rangeIssue);
                Assert.AreEqual(selectedRange.MinDistance, actual.Hints[0].MinimumDistance);
                Assert.AreEqual(selectedRange.MaxDistance, actual.Hints[0].MaximumDistance);
                Assert.AreEqual(selectedRange.Sector.ToleranceMilliDegrees / 1000d, actual.Hints[0].FacingTolerance);
                Assert.AreEqual(selectedRange.IgnoresFurtherIntervals, actual.Hints[0].UsesFirstRange);
                Assert.AreNotEqual("—", actual.Hints[0].RangeText);
                Assert.AreNotEqual("—", actual.Hints[0].FacingText);
                Assert.AreEqual(preview.HasSwordArtwork, actual.HasSwordArtwork); Assert.AreEqual(preview.HintActionPath, actual.HintActionPath);
                Assert.AreEqual(Newtonsoft.Json.JsonConvert.SerializeObject(preview.Segments), Newtonsoft.Json.JsonConvert.SerializeObject(actual.Segments));
                Assert.AreEqual(savedHash, _store.List(_controller.TestCardRole).Single().FileHash);
                Assert.IsNull(_store.ReadRecovery(_controller.TestCardRole, configured.Id));
                Assert.AreEqual(sourceCard.ContentHash.ToString(System.Globalization.CultureInfo.InvariantCulture), reference.ContentHash);
                var face = _controller.ConfiguredCardFaces().Single(value => value.Id == configured.Id);
                Assert.IsTrue(_controller.EditConfiguredCardFace(configured.Id, _controller.TestCardRole, face.Token,
                    "仅工作副本的新名称", null, null, out issue), issue);
                Assert.IsTrue(_controller.TryGetConfiguredCardUiPreview(configured.Id, out var dirtyPreview, out issue), issue);
                Assert.AreEqual("仅工作副本的新名称", dirtyPreview.Name); Assert.IsTrue(dirtyPreview.Dirty);
                var stillPublished = (CharacterTestCardUiPreviewData)typeof(CharacterTestFairyGuiTacticalPreparationView)
                    .GetMethod("PresentationData", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(view, new object[] { exact, reference, display });
                Assert.AreEqual(sourceCard.Metadata.DisplayName, stillPublished.Name);
                Assert.AreEqual(savedHash, _store.List(_controller.TestCardRole).Single().FileHash);
            }
            finally { view.Dispose(); }
        }

        private sealed class PresentationMemoryStore : ICharacterTacticalStateStore
        {
            private string _json;
            public Task<string> LoadAsync() => Task.FromResult(_json);
            public Task SaveAsync(string json) { _json = json; return Task.CompletedTask; }
            public Task ResetAsync(string json) { _json = json; return Task.CompletedTask; }
        }

        [Test]
        public void PublishedCardSkinsDeclareEveryAtlasPage()
        {
            const string package = CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PackageId;
            var manifest = CharacterTestFairyGuiRuntimeAiPlannerDevPanelManifest.Create();
            var atlases = Directory.GetFiles(Path.Combine(Application.dataPath, "Bundles/FGUI", package), package + "_atlas*.png");
            Assert.Greater(atlases.Length, 0);
            foreach (string path in atlases)
            {
                string suffix = Path.GetFileNameWithoutExtension(path).Substring(package.Length + 1);
                var expected = new MxFramework.Resources.ResourceKey("ui.fairygui.charactertest.runtime_ai_lab_ui." + suffix,
                    MxFramework.UI.FairyGui.MxFairyGuiResourceTypeIds.PackageAtlas, packageId: package);
                Assert.IsTrue(manifest.Packages[0].RequiredResources.Any(r => r.Key.Equals(expected)), suffix);
                Assert.IsTrue(manifest.Views[0].RequiredResources.Any(r => r.Equals(expected)), suffix);
            }
        }

        [Test]
        public void MissingCardHasExplicitFailureInsteadOfStalePreviewData()
        {
            Assert.IsFalse(_controller.TryGetConfiguredCardUiPreview("missing", out var data, out string issue));
            Assert.IsNull(data);
            Assert.IsNotEmpty(issue);
        }

        [Test]
        public void MissingSummaryAutomaticallyPreparesAndRefreshesWithoutLosingAuthoredTiming()
        {
            var card = Create(attack: true);
            var source = CharacterTestTacticalPublishedSource.Read(card.Source);
            var document = CharacterTestTacticalCardPresentationProjection.Document(card.Id, card.Name, source);
            int character = CharacterTestCharacterDefinitionRegistry.DefaultPlayableCharacterId;
            var role = CharacterTestTacticalPreparationCatalog.Role(character);
            var metrics = new CharacterActionTacticalMetricsStore(Path.Combine(_directory, "automatic-metrics"));
            using var automatic = new CharacterTestCardSummaryPreparation(metrics);
            var locations = new[] { new CharacterTestTacticalActionLocation { Path = "primary", ActionId = "action:301", Label = "动作" } };
            CharacterTestCardUiPreviewData Project()
            {
                var notes = new List<string>();
                var inputs = CharacterTestTacticalCardPresentationProjection.Prepare(source, document, locations, character,
                    notes, () => role, null, out string issue, automatic.Prepare, metrics);
                var data = new CharacterTestCardUiPreviewData { Id = card.Id, Name = card.Name };
                CharacterTestTacticalCardPresentationProjection.Project(source, document, data, inputs, issue,
                    null, null, 1d / 30d, notes);
                return data;
            }
            string before = _store.List(_controller.TestCardRole).Single().FileHash;
            var pending = Project();
            Assert.IsFalse(pending.Prepared);
            StringAssert.Contains("自动测量", pending.Notice);
            Assert.AreEqual("—", pending.Hints.Single().RangeText);
            Assert.AreNotEqual("—", pending.Hints.Single().DurationText, "Authored duration does not need a range summary.");
            long revision = automatic.Revision;
            for (int frame = 0; frame < 3000 && automatic.Revision == revision; frame++) automatic.Tick();
            var ready = Project();
            Assert.IsTrue(ready.Prepared, ready.Notice);
            Assert.AreNotEqual("—", ready.Hints.Single().RangeText);
            Assert.AreEqual(pending.Hints.Single().DurationText, ready.Hints.Single().DurationText);
            Assert.AreEqual(before, _store.List(_controller.TestCardRole).Single().FileHash);
        }

        [Test]
        public void TemporaryUnbindingAndInvalidReplacementNeverChangeTheSavedBinding()
        {
            var card = Create(attack: true);
            var saved = _store.List(_controller.TestCardRole).Single();
            var overrides = new Dictionary<string, int> { ["primary"] = 0 };
            Assert.IsTrue(_controller.TryGetConfiguredCardUiPreview(card.Id, overrides, out var unbound, out string issue), issue);
            Assert.IsFalse(unbound.Prepared);
            StringAssert.Contains("发动装配动作", unbound.Description);
            Assert.IsFalse(unbound.Hints.Single().HasSelectedAction);
            Assert.AreEqual("未装配", unbound.Actions.Single().ActionName);
            overrides["primary"] = int.MaxValue;
            Assert.IsTrue(_controller.TryGetConfiguredCardUiPreview(card.Id, overrides, out var invalid, out issue), issue);
            Assert.IsFalse(invalid.Prepared);
            StringAssert.Contains("动作已不存在", invalid.Notice);
            StringAssert.DoesNotContain("Light Attack", invalid.Description);
            Assert.AreEqual(saved.FileHash, _store.List(_controller.TestCardRole).Single().FileHash);
            Assert.AreEqual(301, _store.List(_controller.TestCardRole).Single().Actions["primary"]);
            Assert.IsNull(_store.ReadRecovery(_controller.TestCardRole, card.Id));
            Assert.IsNull(_controller.TacticalCardContext);
            overrides["removed-slot"] = 301;
            Assert.IsFalse(_controller.TryGetConfiguredCardUiPreview(card.Id, overrides, out var stale, out issue));
            Assert.IsNull(stale);
        }

        [Test]
        public void ExplicitBindingChangesNamesAndClearingItRemovesThePreviousName()
        {
            CharacterTestTacticalCardInputs Prepare(int actionId)
            {
                var measurement = CharacterTestTacticalSummaryPreparation.StandardMeasurement();
                using var probe = new CharacterActionReferenceProbeSession(new CharacterActionId(actionId), measurement.ReferenceDummy);
                var store = new CharacterActionTacticalMetricsStore(Path.Combine(_directory, "metrics", actionId.ToString()));
                var measured = store.Refresh(probe, measurement);
                Assert.AreEqual(CharacterActionTacticalMetricsLoadStatus.Valid, measured.Status, measured.Issue);
                Assert.IsTrue(CharacterTestTacticalCardInputs.TryPrepare(probe.Runtime.ActionRuntime, probe.Source, probe.Runtime.PhysicsRuntime,
                    probe.Runtime.CombatTraceSystem.BakedTraceSource, probe.Runtime.CombatTraceSystem.WeaponTraceBindingResolver,
                    store, measurement, out var inputs, out string issue, testActionId: actionId), issue);
                return inputs;
            }
            var first = Prepare(301);
            var second = Prepare(324);
            var shared = CharacterTestTacticalAuthoringTemplates.CreateSingleAttack();
            var activation = shared.ActivationConditionRoot; shared.ActivationConditionRoot = null;
            shared.Timelines[0].Events.Single(e => e.EventId == "approach").Operation = new CharacterTestTacticalAuthoringOperation {
                Kind = "Movement", Movement = new CharacterTestTacticalMovement {
                    Strategy = CharacterTestRuntimeAiLabMovementStrategy.ApproachTarget,
                    Target = CharacterTestRuntimeAiLabMovementTargetKind.CurrentCombatTarget,
                    DistanceSource = "ActionOptimal", ActionSlotId = "primary" } };
            var source = new CharacterTestTacticalDslSource { CardId = "preview", Name = "装配预览", ActivationConditionRoot = activation,
                Root = new CharacterTestTacticalDslNode { NodeId = "root", Kind = "Sequence",
                    Children = new List<CharacterTestTacticalDslNode> { CharacterTestTacticalDslTrialTemplates.Reference("instance", "definition") } },
                Definitions = new List<CharacterTestTacticalSharedDefinition> {
                    new CharacterTestTacticalSharedDefinition { DefinitionId = "definition", Source = shared } } };
            CharacterTestTacticalDslDescription Parse(CharacterTestTacticalCardInputs value, bool face = false, IReadOnlyDictionary<string, string> names = null)
            {
                var bindings = new Dictionary<string, CharacterTestTacticalCardInputs>();
                if (value != null) bindings.Add("instance/primary", value);
                var description = CharacterTestTacticalDslDescription.Generate(source,
                    CharacterTestTacticalCardInputs.BindSlots(first.SourceCharacterId, bindings), cardFace: face, selectedActions: names);
                if (value != null) Assert.IsTrue(description.Executable, description.CompilationIssue);
                return description;
            }
            string Describe(CharacterTestTacticalCardInputs value, bool face = false, IReadOnlyDictionary<string, string> names = null)
                => Parse(value, face, names).Text;
            string original = Describe(first), replacement = Describe(second), empty = Describe(null);
            Assert.AreEqual("以常规移动接敌就位，发动" + first.Action.DisplayName + "。", original);
            Assert.AreEqual("以常规移动接敌就位，发动" + second.Action.DisplayName + "。", replacement);
            StringAssert.DoesNotContain("其中相应行为", replacement);
            StringAssert.Contains("发动装配动作", empty);
            StringAssert.DoesNotContain(second.Action.DisplayName, empty);
            string Number(double value) => value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
            var parsed = Parse(second, true);
            var hint = parsed.Hints.Single();
            string face = parsed.Text;
            Assert.AreEqual(replacement, face);
            Assert.AreEqual(second.SelectedRange.OptimalDistance, hint.ApproachDistance);
            Assert.AreEqual(second.SelectedRange.MinDistance, hint.MinimumDistance);
            Assert.AreEqual(second.SelectedRange.MaxDistance, hint.MaximumDistance);
            Assert.AreEqual(second.SelectedRange.Sector.ToleranceMilliDegrees / 1000d, hint.FacingTolerance);
            Assert.AreEqual("instance/primary", hint.ActionSlotPath);
            Assert.AreEqual("≈ " + Number(second.Action.DurationFrames.Value / 60d) + " 秒", hint.DurationText);
            Assert.AreEqual(second.SelectedRange.IgnoresFurtherIntervals, hint.UsesFirstRange);
            Assert.AreEqual(Number(second.SelectedRange.MinDistance.Value) + "\uFF5E" + Number(second.SelectedRange.MaxDistance.Value) + " \u7C73"
                + (second.SelectedRange.IgnoresFurtherIntervals ? "（仅第一段，不采用远段）" : ""), hint.RangeText);
            StringAssert.DoesNotContain("米", face);
            StringAssert.DoesNotContain("°", face);
            StringAssert.DoesNotContain("“", face);
            StringAssert.DoesNotContain("尝试", face);
            StringAssert.DoesNotContain("可使用", face);
            StringAssert.DoesNotContain("合适", face);
            Assert.AreEqual("×1", hint.Movement.MovementText);
            Assert.AreEqual("×1", hint.Movement.RotationText);
            shared.Timelines[0].Events.Single(e => e.EventId == "approach").Operation.Speed = 0.35f;
            shared.Timelines[0].Events.Single(e => e.EventId == "facing").Operation.Speed = 0.8f;
            var changedMovement = Parse(second, true);
            Assert.AreEqual(face, changedMovement.Text, "Control parameters belong to the special term, not the main sentence.");
            Assert.AreEqual("×0.35", changedMovement.Hints.Single().Movement.MovementText);
            Assert.AreEqual("×0.8", changedMovement.Hints.Single().Movement.RotationText);
            Assert.AreEqual("approach", changedMovement.Hints.Single().Movement.MovementEventId);
            Assert.AreEqual("facing", changedMovement.Hints.Single().Movement.RotationEventId);
            Assert.AreEqual("×1", hint.Movement.MovementText, "Previously generated hint snapshots must remain immutable.");
            var unboundMovement = Parse(null, true).Hints.Single().Movement;
            Assert.AreEqual("×0.35", unboundMovement.MovementText);
            Assert.AreEqual("×0.8", unboundMovement.RotationText, "Authored control multipliers must remain available without action measurements.");
            var movement = shared.Timelines[0].Events.Single(e => e.EventId == "approach").Operation.Movement;
            shared.Name = "改名也不能改写实际行为";
            Assert.AreEqual(face, Describe(second, true));
            movement.Strategy = CharacterTestRuntimeAiLabMovementStrategy.MaintainDistance;
            movement.DistanceSource = "ActionBand";
            Assert.IsEmpty(Parse(second, true).Hints, "A renamed or modified wrapper must match actual semantics, not its display name.");
            string distance = Describe(second);
            StringAssert.Contains("按“" + second.Action.DisplayName + "”的有效距离带", distance.Replace("” 的", "”的"));
            StringAssert.DoesNotContain("动作槽 primary", distance);
            movement.DistanceSource = "Manual"; movement.ActionSlotId = "";
            movement.Minimum = 0.725f; movement.Maximum = 1.225f;
            StringAssert.Contains("与目标保持 0.725～1.225 米距离", Describe(second, true));
            movement.DistanceSource = "ActionOptimal"; movement.ActionSlotId = "primary";
            movement.Strategy = CharacterTestRuntimeAiLabMovementStrategy.ApproachTarget;
            movement.Minimum = 987.65f; movement.Maximum = 987.66f;
            var incomplete = Parse(null, true, new Dictionary<string, string> { ["instance/primary"] = second.Action.DisplayName });
            Assert.AreEqual(face, incomplete.Text);
            Assert.IsNull(incomplete.Hints.Single().ApproachDistance);
            Assert.AreEqual("—", incomplete.Hints.Single().RangeText);
            StringAssert.DoesNotContain("987", incomplete.Text);
            source.Root.Children.Add(CharacterTestTacticalDslTrialTemplates.Reference("second-instance", "definition"));
            var both = CharacterTestTacticalDslDescription.Generate(source, CharacterTestTacticalCardInputs.BindSlots(first.SourceCharacterId,
                new Dictionary<string, CharacterTestTacticalCardInputs> { ["instance/primary"] = first, ["second-instance/primary"] = second }), cardFace: true);
            Assert.IsTrue(both.Executable, both.CompilationIssue);
            Assert.AreEqual(2, both.Hints.Length);
            Assert.AreEqual(first.Action.DisplayName, both.Hints[0].ActionName);
            Assert.AreEqual(second.Action.DisplayName, both.Hints[1].ActionName);
            Assert.AreEqual("second-instance/primary", both.Hints[1].ActionSlotPath);
            Assert.AreEqual(second.SelectedRange.MaxDistance, both.Hints[1].MaximumDistance);
            source.Root.Children.RemoveAt(1);
            shared.Timelines[0].Events.Single(e => e.EventId == "attack").Start = CharacterTestTacticalAuthoringTemplates.Absolute(0);
            Assert.IsEmpty(Parse(second, true).Hints, "A same-frame action must not claim it waits for positioning.");
        }
    }

    // Plain projection contract; no GameObject, Play state, authoring save or preparation measurement.
    public sealed class CharacterTestInteractionPreparationCardUiPreviewTests
    {
        private static JObject Source(TacticalInteractionKind kind)
            => new JObject { ["schema"] = CharacterTestTacticalPublishedSource.Schema,
                ["legacy"] = JObject.Parse(CharacterTestTacticalAuthoringJson.Write(
                    CharacterTestTacticalAuthoringTemplates.CreateInteractionPreparation(kind))) };

        [TestCase((int)TacticalInteractionKind.PursuitMark, "追加8点基础伤害")]
        [TestCase((int)TacticalInteractionKind.MomentumRecovery, "获得3动能")]
        [TestCase((int)TacticalInteractionKind.DefensePreparation, "12点散势压力")]
        [TestCase((int)TacticalInteractionKind.ReturnMark, "尝试回返一次")]
        [TestCase((int)TacticalInteractionKind.HurtCharge, "外来实际HP伤害")]
        public void FixedPreparationProjectsSharedAuthorityWithoutDslOrActionInputs(int kindValue, string effectText)
        {
            var kind = (TacticalInteractionKind)kindValue;
            var json = Source(kind);
            json["legacy"]["name"] = "误导的行动卡名称";
            var source = CharacterTestTacticalPublishedSource.Read(json);
            var authority = CharacterTestTacticalDslDescription.GenerateInteractionPreparation(source.InteractionPreparation);
            var data = new CharacterTestCardUiPreviewData { Name = "过时副本名称", Description = "。",
                DescriptionDetails = "过时动作规则", HintActionPath = "stale/action", HasStructuredDescription = true,
                Hints = new CharacterTestTacticalApproachActionHint[1], Segments = new CharacterTestTacticalDescriptionSegment[1] };
            var notes = new List<string> { "保留来源诊断", "保留来源诊断" };
            var provider = MxFramework.UI.MxUiNullTextProvider.Instance;
            var document = CharacterTestTacticalCardPresentationProjection.Document("preview", "误导的行动卡名称", source);
            var inputs = CharacterTestTacticalCardInputs.BindSlots(0,
                new Dictionary<string, CharacterTestTacticalCardInputs>(StringComparer.Ordinal));
            CharacterTestTacticalCardPresentationProjection.Project(source, document, data, inputs,
                null, null, provider, 1d / 60, notes);

            Assert.AreEqual(authority.Text, data.Description, "Game and Web must use the same existing authority.");
            StringAssert.Contains(effectText, data.Description);
            Assert.IsTrue(data.Description.Any(char.IsLetterOrDigit));
            Assert.AreEqual(authority.Details + "\n" + string.Join("\n", authority.Warnings), data.DescriptionDetails);
            StringAssert.Contains("F+600", data.DescriptionDetails);
            StringAssert.Contains("同类每回合一次", data.DescriptionDetails);
            StringAssert.Contains("派生伤害不触发准备效果", data.DescriptionDetails);
            Assert.AreEqual(data.Description, data.UnboundDescription);
            Assert.IsEmpty(data.Hints); Assert.IsEmpty(data.Segments);
            Assert.IsNull(data.DescriptionFlow); Assert.IsFalse(data.HasStructuredDescription);
            Assert.IsNull(data.HintActionPath); Assert.AreEqual(1d / 60, data.FrameSeconds);
            Assert.AreEqual(CharacterTestUiTextProviderSignature.Create(provider), data.TextSignature);
            Assert.AreEqual("保留来源诊断\n" + string.Join("\n", authority.Warnings), data.Notice);
            Assert.IsTrue(data.Prepared, "The fixed source needs no action binding or measurement.");
            CharacterTestTacticalCardPresentationProjection.Project(source, null, data, null,
                "不需要动作测量", null, provider, 1d / 60, new List<string>());
            Assert.AreEqual(authority.Text, data.Description);
            Assert.IsTrue(data.Prepared);
        }

        [TestCase("UnknownEffect")]
        [TestCase("")]
        [TestCase(" ")]
        [TestCase(null)]
        [TestCase("0")]
        public void UnknownOrEmptyEffectCannotReachPresentation(string effect)
        {
            var json = Source(TacticalInteractionKind.MomentumRecovery);
            json["legacy"]["interactionPreparation"]["effect"] = effect;
            Assert.Catch(() => CharacterTestTacticalPublishedSource.Read(json),
                "A title or empty DSL body cannot supply a missing effect contract.");
        }

        [TestCase("effect")]
        [TestCase("ruleset")]
        public void MissingRequiredPreparationFieldCannotReachPresentation(string field)
        {
            var json = Source(TacticalInteractionKind.MomentumRecovery);
            ((JObject)json["legacy"]["interactionPreparation"]).Remove(field);
            Assert.Catch(() => CharacterTestTacticalPublishedSource.Read(json));
        }

        [TestCase("future.v2")]
        [TestCase("")]
        [TestCase(null)]
        public void UnknownOrEmptyRulesetCannotReachPresentation(string ruleset)
        {
            var json = Source(TacticalInteractionKind.MomentumRecovery);
            json["legacy"]["interactionPreparation"]["ruleset"] = ruleset;
            Assert.Catch(() => CharacterTestTacticalPublishedSource.Read(json));
        }

        [Test]
        public void EmptyPreparationObjectCannotReachPresentation()
        {
            var json = Source(TacticalInteractionKind.MomentumRecovery);
            json["legacy"]["interactionPreparation"] = new JObject();
            Assert.Catch(() => CharacterTestTacticalPublishedSource.Read(json));
        }
    }

    public sealed class CharacterTestLegacyApproachHintContractTests
    {
        private static CharacterTestTacticalSharedDefinition Definition(bool legacyActionGuard = false)
        {
            var source = CharacterTestTacticalAuthoringTemplates.CreateSingleAttack();
            var oldActivation = source.ActivationConditionRoot; source.ActivationConditionRoot = null;
            if (legacyActionGuard) source.Timelines[0].Events.Single(e => e.EventId == "attack").ConditionRoot = oldActivation;
            return new CharacterTestTacticalSharedDefinition { DefinitionId = "definition", Source = source };
        }

        [TestCase(false)]
        [TestCase(true)]
        public void CurrentAndHistoricalPlaybackConditionShareTheVerifiedHint(bool legacyActionGuard)
        {
            var definition = Definition(legacyActionGuard);
            var events = definition.Source.Timelines[0].Events.ToArray();
            Assert.IsTrue(CharacterTestTacticalApproachActionHint.TryParse(definition, "instance", events, null,
                new Dictionary<string, string> { ["instance/primary"] = "当前选择动作" }, null, out var hint));
            Assert.AreEqual("instance/primary", hint.ActionSlotPath);
            Assert.AreEqual("以常规移动接敌就位，发动当前选择动作。", hint.Text);
            Assert.IsTrue(hint.HasSelectedAction);
            Assert.AreEqual("—", hint.RangeText); Assert.AreEqual("—", hint.FacingText);
            Assert.AreEqual("—", hint.DurationText, "Names alone must never fabricate measured timing.");
        }

        [Test]
        public void UnboundCurrentTemplateKeepsItsActionAndMeasurementPlaceholders()
        {
            var definition = Definition();
            Assert.IsTrue(CharacterTestTacticalApproachActionHint.TryParse(definition, "instance",
                definition.Source.Timelines[0].Events.ToArray(), null, null, null, out var hint));
            Assert.AreEqual("以常规移动接敌就位，发动装配动作。", hint.Text);
            Assert.IsFalse(hint.HasSelectedAction);
            Assert.AreEqual("—", hint.RangeText); Assert.AreEqual("—", hint.FacingText); Assert.AreEqual("—", hint.DurationText);
        }

        [TestCase("missing-gate")]
        [TestCase("missing-alive")]
        [TestCase("other-movement")]
        [TestCase("same-frame")]
        public void ActualGateMovementAndTimingStillControlRecognition(string change)
        {
            var definition = Definition(); var events = definition.Source.Timelines[0].Events;
            if (change == "missing-gate") events.RemoveAll(e => e.Operation.Kind == "WaitForCondition");
            if (change == "missing-alive") events.Single(e => e.EventId == "release").ConditionRoot.Children
                .RemoveAll(CharacterTestTacticalApproachActionHint.Alive);
            if (change == "other-movement") events.Single(e => e.EventId == "approach").Operation = new CharacterTestTacticalAuthoringOperation {
                Kind = "Movement", Movement = new CharacterTestTacticalMovement {
                    Strategy = CharacterTestRuntimeAiLabMovementStrategy.MaintainDistance,
                    Target = CharacterTestRuntimeAiLabMovementTargetKind.CurrentCombatTarget,
                    DistanceSource = "ActionBand", ActionSlotId = "primary" } };
            if (change == "same-frame") events.Single(e => e.EventId == "attack").Start = CharacterTestTacticalAuthoringTemplates.Absolute(0);
            Assert.IsFalse(CharacterTestTacticalApproachActionHint.TryParse(definition, "instance", events.ToArray(),
                null, null, null, out var hint));
            Assert.IsNull(hint);
        }
    }
}
