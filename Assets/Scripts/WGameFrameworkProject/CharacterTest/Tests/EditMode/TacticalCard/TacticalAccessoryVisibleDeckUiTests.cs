using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using MxFramework.Game.CharacterTest;
using MxFramework.Game.CharacterTest.TacticalCard;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Fgui = global::FairyGUI;

namespace MxFramework.Tests.Game.CharacterTest
{
    [Parallelizable(ParallelScope.None)]
    internal sealed class TacticalAccessoryVisibleDeckUiTests
    {
        private const string Package = "MxCharacterTestRuntimeAiLabUi";
        private const string Prefix = "Assets/Bundles/FGUI/MxCharacterTestRuntimeAiLabUi/MxCharacterTestRuntimeAiLabUi";
        private const string SaveRootVariable = "MX_CHARACTERTEST_TACTICAL_SAVE_ROOT";
        private const string SessionKey = "MxFramework.VisibleAccessoryR2.";
        private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
        private bool _ownsPackage;
        private Fgui.GComponent _artwork, _offer;
        private CharacterTestFairyGuiAiLabCardTrialSurface _surface;

        [UnitySetUp]
        public IEnumerator Setup()
        {
            string previous = Environment.GetEnvironmentVariable(SaveRootVariable);
            SessionState.SetBool(SessionKey + "HadRoot", previous != null);
            SessionState.SetString(SessionKey + "PriorRoot", previous ?? "");
            string temporary = Path.Combine(Path.GetTempPath(), "MxAccessoryR2", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temporary);
            Environment.SetEnvironmentVariable(SaveRootVariable, temporary);
            yield return new EnterPlayMode();
            Assert.IsTrue(Application.isPlaying, "FairyGUI Stage and drag fixtures require a native Play context.");
            yield return null;
            string method = TestContext.CurrentContext.Test.MethodName;
            if (method == nameof(CurrentVisibleDeckWindowShowsAllSixCompleteDescriptionsAndActualFees)
                || method == nameof(CurrentVisibleDeckWindowRestoresArtworkAndLayoutAfterVirtualReuse)
                || method == nameof(CurrentVisibleDeckWindowEquipsReplacesAndRemovesLaterOffers)) {
                // The EditMode runner replaces the parked scene with its bootstrap scene before EnterPlayMode.
                yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/CharacterTestRuntimeAiLabScene.unity",
                    new LoadSceneParameters(LoadSceneMode.Single));
                for (int frame = 0; frame < 600 && UnityEngine.Object.FindAnyObjectByType<CharacterTestRuntimeAiPlannerDevSceneController>() == null;
                    frame++) yield return null;
            }
            var controller = UnityEngine.Object.FindAnyObjectByType<CharacterTestRuntimeAiPlannerDevSceneController>();
            if (controller != null) {
                for (int frame = 0; frame < 600 && typeof(CharacterTestRuntimeAiPlannerDevSceneController)
                    .GetField("_cardTrial", Private).GetValue(controller) == null; frame++) yield return null;
                controller.PrepareInitialSetupState(restorePersistedDraft: false);
                Assert.IsTrue(controller.UsesTransientDraftPersistence);
                Assert.IsTrue(controller.OpenCardTrial(), controller.StatusMessage);
                yield return null;
            }
            CharacterTestFairyGuiFonts.Initialize();
            _ownsPackage = Fgui.UIPackage.GetByName(Package) == null;
            if (_ownsPackage) Fgui.UIPackage.AddPackage(File.ReadAllBytes(Prefix + "_fui.bytes"), Prefix, LoadAtlas);
            _artwork = Fgui.UIPackage.CreateObject(Package, "RuntimeAiLabTrialAccessoryArtwork").asCom;
            _offer = Fgui.UIPackage.CreateObject(Package, "RuntimeAiLabTrialAccessoryOffer").asCom;
            _surface = (CharacterTestFairyGuiAiLabCardTrialSurface)FormatterServices.GetUninitializedObject(typeof(CharacterTestFairyGuiAiLabCardTrialSurface));
            Set("_accessoryIconLibrary", _artwork);
            var textures = new Fgui.NTexture[4];
            for (int i = 1; i <= 3; i++) textures[i] = _artwork.GetChild(CharacterTestFairyGuiTacticalAccessoryPresentation.ArtworkControl((TacticalAccessoryCombatKind)i)).asImage.texture;
            Set("_accessoryTextures", textures);
        }

        private static object LoadAtlas(string name, string extension, Type type, out Fgui.DestroyMethod destroy)
        { destroy = Fgui.DestroyMethod.None; return AssetDatabase.LoadAssetAtPath<Texture2D>(name + extension); }
        private void Set(string name, object value) => typeof(CharacterTestFairyGuiAiLabCardTrialSurface).GetField(name, Private).SetValue(_surface, value);
        private void Invoke(string name, params object[] args) => typeof(CharacterTestFairyGuiAiLabCardTrialSurface).GetMethod(name, Private).Invoke(_surface, args);
        private void Render(CharacterTacticalAccessoryDefinition definition)
        {
            Set("_accessoryOffers", new List<CharacterTacticalAccessoryDefinition> { definition });
            Invoke("RenderAccessoryOffer", 0, _offer);
        }

        [UnityTearDown]
        public IEnumerator Teardown()
        {
            if (_surface != null) Invoke("CancelAccessoryDrag");
            _offer?.Dispose(); _artwork?.Dispose();
            if (_ownsPackage) Fgui.UIPackage.RemovePackage(Package);
            if (Application.isPlaying) yield return new ExitPlayMode();
            Environment.SetEnvironmentVariable(SaveRootVariable, SessionState.GetBool(SessionKey + "HadRoot", false)
                ? SessionState.GetString(SessionKey + "PriorRoot", "") : null);
        }

        [TestCase(TacticalAccessoryCombatKind.DuelInvitation, "决斗邀请", "决", "费用 +1")]
        [TestCase(TacticalAccessoryCombatKind.ShatteredFantasy, "破碎幻想", "碎", "费用 +2")]
        [TestCase(TacticalAccessoryCombatKind.BlackHolePocket, "黑洞口袋", "洞", "费用 +2")]
        public void ActualOfferRendererShowsFirstBatchAtAnyIndexAndRestoresTheOldIconOnReuse(
            TacticalAccessoryCombatKind kind, string name, string marker, string fee)
        {
            Render(CharacterTestRuntimeAiLabFixedAssembly.Definition(kind));
            Assert.AreEqual(name, _offer.GetChild("name").text);
            StringAssert.StartsWith(fee + "\n", _offer.GetChild("detail").text);
            Assert.IsFalse(_offer.GetChild("icon").visible); Assert.IsNull(_offer.GetChild("icon").asImage.texture);
            var fallback = _offer.GetChild("accessoryFallback").asCom;
            Assert.IsTrue(fallback.visible); Assert.AreEqual(marker, fallback.GetChild("empty").text);
            Assert.IsFalse(fallback.touchable); Assert.IsFalse(fallback.GetChild("selected").visible);
            Assert.LessOrEqual(_offer.GetChild("name").asTextField.textWidth, _offer.GetChild("name").width);
            Assert.LessOrEqual(_offer.GetChild("detail").asTextField.textHeight, _offer.GetChild("detail").height);
            Render(CharacterTestRuntimeAiLabFixedAssembly.Definition(TacticalAccessoryCombatKind.BorrowMomentum));
            Assert.AreEqual("借力", _offer.GetChild("name").text); Assert.IsTrue(_offer.GetChild("icon").visible);
            Assert.AreSame(_artwork.GetChild("borrowMomentum").asImage.texture, _offer.GetChild("icon").asImage.texture);
            Assert.IsFalse(fallback.visible); Assert.AreEqual("", fallback.GetChild("empty").text);
            Assert.IsTrue(_offer.GetChild("select").asButton.draggable);
        }

        [Test]
        public void UnknownOfferHasGenericFallbackAndTheExactDefinitionInItsTooltip()
        {
            var definition = new CharacterTacticalAccessoryDefinition {
                Reference = new CharacterTacticalAccessoryReference { AccessoryId = "fixture.unknown.long-exact-id" },
                Execution = new TacticalAccessoryExecutionDefinition { Kind = (TacticalAccessoryCombatKind)700, ConfirmationMomentumCost = 8 } };
            Render(definition);
            Assert.AreEqual("配件", _offer.GetChild("name").text);
            StringAssert.StartsWith("费用 +8\n", _offer.GetChild("detail").text);
            StringAssert.Contains(definition.Reference.AccessoryId, _offer.GetChild("select").tooltips);
            Assert.AreEqual("配", _offer.GetChild("accessoryFallback").asCom.GetChild("empty").text);
            Assert.IsNull(_offer.GetChild("icon").asImage.texture);
        }

        [TestCase(TacticalAccessoryCombatKind.DuelInvitation, "决")]
        [TestCase(TacticalAccessoryCombatKind.ShatteredFantasy, "碎")]
        [TestCase(TacticalAccessoryCombatKind.BlackHolePocket, "洞")]
        public void DragFallbackCarriesExactKindAndClearsBeforeTheNextLegacyPreview(TacticalAccessoryCombatKind kind, string marker)
        {
            var manager = Fgui.DragDropManager.inst;
            Assert.IsFalse(manager.dragging, "Run this fixture when no pointer drag is active.");
            Invoke("StartAccessoryDrag", _offer.GetChild("select").asButton, CharacterTestRuntimeAiLabFixedAssembly.Display(kind), -1);
            Assert.IsTrue(manager.dragging); Assert.IsNotNull(manager.dragAgent.component);
            Assert.AreEqual(marker, manager.dragAgent.component.GetChild("empty").text);
            Assert.AreEqual(kind, typeof(Fgui.DragDropManager).GetField("_sourceData", Private).GetValue(manager));
            Invoke("CancelAccessoryDrag");
            Assert.IsFalse(manager.dragging); Assert.IsNull(manager.dragAgent.component); Assert.IsNull(manager.dragAgent.texture);
            Invoke("StartAccessoryDrag", _offer.GetChild("select").asButton,
                CharacterTestRuntimeAiLabFixedAssembly.Display(TacticalAccessoryCombatKind.BorrowMomentum), -1);
            Assert.IsTrue(manager.dragging); Assert.IsNull(manager.dragAgent.component);
            Assert.AreSame(_artwork.GetChild("borrowMomentum").asImage.texture, manager.dragAgent.texture);
        }

        [UnityTest]
        public IEnumerator CurrentVisibleDeckWindowShowsAllSixCompleteDescriptionsAndActualFees()
        {
            var opening = OpenCurrentDeckWindow();
            while (opening.MoveNext()) yield return opening.Current;
            var window = CurrentWindow(); var list = window.GetChild("accessoryOffers").asList;
            var definitions = CharacterTestRuntimeAiLabFixedAssembly.AvailableDefinitions();
            Assert.AreEqual(definitions.Count, list.numItems);
            for (int index = 0; index < definitions.Count; index++)
            {
                list.ScrollToView(index, false, true); yield return null; yield return null;
                int child = list.ItemIndexToChildIndex(index); Assert.GreaterOrEqual(child, 0);
                var row = list.GetChildAt(child).asCom;
                AssertVisibleOfferFits(window, list, row, CharacterTestTacticalAccessoryDisplay.From(definitions[index]));
            }
        }

        [UnityTest]
        public IEnumerator CurrentVisibleDeckWindowRestoresArtworkAndLayoutAfterVirtualReuse()
        {
            var opening = OpenCurrentDeckWindow();
            while (opening.MoveNext()) yield return opening.Current;
            var window = CurrentWindow(); var list = window.GetChild("accessoryOffers").asList;
            var definitions = CharacterTestRuntimeAiLabFixedAssembly.AvailableDefinitions();
            foreach (int index in new[] { 0, definitions.Count - 1, 1, definitions.Count - 2, 2, definitions.Count - 3, 6, 7, 8, 0 })
            {
                list.ScrollToView(index, false, true); yield return null; yield return null;
                int child = list.ItemIndexToChildIndex(index); Assert.GreaterOrEqual(child, 0);
                var row = list.GetChildAt(child).asCom;
                var display = CharacterTestTacticalAccessoryDisplay.From(definitions[index]);
                AssertVisibleOfferFits(window, list, row, display);
                var image = row.GetChild("icon").asImage; var fallback = row.GetChild("accessoryFallback")?.asCom;
                bool legacy = (int)display.Kind <= 3;
                Assert.AreEqual(legacy, image.visible);
                if (legacy) {
                    Assert.IsNotNull(image.texture);
                    if (fallback != null) { Assert.IsFalse(fallback.visible); Assert.AreEqual("", fallback.GetChild("empty").text); }
                } else {
                    Assert.IsNull(image.texture); Assert.IsNotNull(fallback); Assert.IsTrue(fallback.visible);
                    Assert.IsFalse(fallback.touchable); Assert.IsFalse(fallback.GetChild("selected").visible);
                }
            }
        }

        [UnityTest]
        public IEnumerator CurrentVisibleDeckWindowEquipsReplacesAndRemovesLaterOffers()
        {
            var opening = OpenCurrentDeckWindow(); while (opening.MoveNext()) yield return opening.Current;
            var window = CurrentWindow(); var offers = window.GetChild("accessoryOffers").asList;
            var cards = window.GetChild("initialHand").asList;
            var controller = UnityEngine.Object.FindAnyObjectByType<CharacterTestRuntimeAiPlannerDevSceneController>();
            var session = (CharacterTestRuntimeAiLabCardTrialSession)typeof(CharacterTestRuntimeAiPlannerDevSceneController).GetField("_cardTrial", Private).GetValue(controller);
            var definitions = CharacterTestRuntimeAiLabFixedAssembly.AvailableDefinitions();
            for (int index = 6; index < definitions.Count; index++) {
                cards.ScrollToView(0, false, true); offers.ScrollToView(0, false, true); yield return null; yield return null;
                var borrowed = offers.GetChildAt(offers.ItemIndexToChildIndex(0)).asCom;
                borrowed.GetChild("select").asButton.FireClick(false, true); yield return null; yield return null;
                Assert.AreEqual(TacticalAccessoryCombatKind.BorrowMomentum, session.Draft.DeckAt(0)[0].AccessorySlots[0]);
                offers.ScrollToView(index, false, true); yield return null; yield return null;
                var cardRow = cards.GetChildAt(cards.ItemIndexToChildIndex(0)).asCom;
                cardRow.GetChild("slot1").onClick.Call(); yield return null;
                var offer = offers.GetChildAt(offers.ItemIndexToChildIndex(index)).asCom;
                offer.GetChild("select").asButton.FireClick(false, true); yield return null; yield return null;
                var card = session.Draft.DeckAt(0)[0];
                Assert.AreEqual(definitions[index].Execution.Kind, card.AccessorySlots[0]);
                Assert.AreEqual(definitions[index].Execution.ConfirmationMomentumCost, CharacterTestRuntimeAiLabFixedAssembly.ConfirmationCost(card));
                cardRow = cards.GetChildAt(cards.ItemIndexToChildIndex(0)).asCom;
                cardRow.GetChild("slot1").onClick.Call(); yield return null;
                cardRow = cards.GetChildAt(cards.ItemIndexToChildIndex(0)).asCom;
                var remove = cardRow.GetChild("removeSlot1").asButton; Assert.IsTrue(remove.visible);
                remove.FireClick(false,true); yield return null; yield return null;
                Assert.AreEqual(0, (int)card.AccessorySlots[0]); Assert.AreEqual(0, CharacterTestRuntimeAiLabFixedAssembly.ConfirmationCost(card));
                cardRow = cards.GetChildAt(cards.ItemIndexToChildIndex(0)).asCom;
                Assert.AreEqual("＋", cardRow.GetChild("slot1").asCom.GetChild("empty").text);
                Assert.IsNull(cardRow.GetChild("slot1").asCom.GetChild("icon").asImage.texture);
            }
        }

        private IEnumerator OpenCurrentDeckWindow()
        {
            var controller = UnityEngine.Object.FindAnyObjectByType<CharacterTestRuntimeAiPlannerDevSceneController>();
            Assert.IsNotNull(controller, "The Editor owner must open CharacterTestRuntimeAiLabScene before this native suite.");
            for (int frame = 0; frame < 600 && typeof(CharacterTestRuntimeAiPlannerDevSceneController)
                .GetField("_cardTrial", Private).GetValue(controller) == null; frame++) yield return null;
            Assert.IsTrue(controller.UsesTransientDraftPersistence);
            Assert.IsTrue(controller.CardTrialOpen, "Setup must open the real current window through its normal session owner.");
            yield return null;
            var window = CurrentWindow();
            Assert.IsTrue(window.visible); Assert.IsTrue(window.onStage);
            var openDecks = window.GetChild("openDecks").asButton;
            Assert.IsTrue(openDecks.visible); Assert.IsTrue(openDecks.enabled);
            openDecks.FireClick(false, true);
            yield return null;
            Assert.AreEqual("卡组装配", window.GetChild("title").text);
            Assert.IsTrue(window.GetChild("accessoryOffers").visible);
            Assert.IsFalse(window.GetChild("pickerChoices").visible, "Use the current visible deck, never the old private Picker.");
            Assert.IsFalse(window.GetChild("pickerCards").visible);
        }

        private static Fgui.GComponent CurrentWindow()
        {
            var controller = UnityEngine.Object.FindAnyObjectByType<CharacterTestRuntimeAiPlannerDevSceneController>();
            var session = typeof(CharacterTestRuntimeAiPlannerDevSceneController).GetField("_cardTrial", Private).GetValue(controller);
            Assert.IsNotNull(session);
            var surface = typeof(CharacterTestRuntimeAiLabCardTrialSession).GetField("_surface", Private).GetValue(session);
            Assert.IsNotNull(surface);
            var root = (Fgui.GComponent)typeof(CharacterTestFairyGuiAiLabCardTrialSurface).GetField("_root", Private).GetValue(surface);
            Assert.IsNotNull(root); return root;
        }

        private static void AssertVisibleOfferFits(Fgui.GComponent window, Fgui.GList list, Fgui.GComponent row,
            CharacterTestTacticalAccessoryDisplay display)
        {
            Assert.IsTrue(window.onStage); Assert.IsTrue(window.visible); Assert.IsTrue(list.visible);
            Assert.IsTrue(row.onStage); Assert.IsTrue(row.visible);
            var name = row.GetChild("name").asTextField; var detail = row.GetChild("detail").asTextField;
            Assert.AreEqual(display.SlotText, name.text);
            Assert.AreEqual(display.FeeText + "\n" + display.Description, detail.text);
            Assert.AreEqual(20, name.textFormat.size); Assert.AreEqual(17, detail.textFormat.size);
            Assert.AreEqual(Fgui.AutoSizeType.None, name.autoSize); Assert.AreEqual(Fgui.AutoSizeType.None, detail.autoSize);
            Assert.LessOrEqual(name.textWidth, name.width); Assert.LessOrEqual(name.textHeight, name.height);
            Assert.LessOrEqual(detail.textWidth, detail.width); Assert.LessOrEqual(detail.textHeight, detail.height);
            Assert.GreaterOrEqual(detail.y, row.GetChild("icon").y + row.GetChild("icon").height);
            foreach (var field in new Fgui.GObject[] { name, detail, row.GetChild("icon") }) {
                Assert.GreaterOrEqual(field.x, 0); Assert.GreaterOrEqual(field.y, 0);
                Assert.LessOrEqual(field.x + field.width, row.width); Assert.LessOrEqual(field.y + field.height, row.height);
            }
            var rowOrigin = row.LocalToGlobal(Vector2.zero); var rowBottom = row.LocalToGlobal(new Vector2(row.width, row.height));
            var listOrigin = list.LocalToGlobal(Vector2.zero); var listBottom = list.LocalToGlobal(new Vector2(list.width, list.height));
            Assert.GreaterOrEqual(rowOrigin.x, listOrigin.x - 1); Assert.LessOrEqual(rowBottom.x, listBottom.x + 1);
            Assert.GreaterOrEqual(rowOrigin.y, listOrigin.y - 1); Assert.LessOrEqual(rowBottom.y, listBottom.y + 1);
            TestContext.WriteLine($"{Screen.width}x{Screen.height}: {display.Name}, fee={display.FeeText}, text={detail.textWidth}x{detail.textHeight}, box={detail.width}x{detail.height}");
        }
    }
}
