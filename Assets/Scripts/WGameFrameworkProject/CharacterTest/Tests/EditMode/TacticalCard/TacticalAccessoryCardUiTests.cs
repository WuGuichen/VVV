using System;
using System.IO;
using MxFramework.Game.CharacterTest;
using MxFramework.Game.CharacterTest.TacticalCard;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Fgui = global::FairyGUI;

namespace MxFramework.Tests.Game.CharacterTest
{
    [Parallelizable(ParallelScope.None)]
    public sealed class TacticalAccessoryCardUiTests
    {
        private const string Package = "MxCharacterTestRuntimeAiLabUi";
        private const string Prefix = "Assets/Bundles/FGUI/MxCharacterTestRuntimeAiLabUi/MxCharacterTestRuntimeAiLabUi";
        private bool _ownsPackage;
        private Fgui.GComponent _artwork;

        [SetUp]
        public void Setup()
        {
            CharacterTestFairyGuiFonts.Initialize();
            _ownsPackage = Fgui.UIPackage.GetByName(Package) == null;
            if (_ownsPackage) Fgui.UIPackage.AddPackage(File.ReadAllBytes(Prefix + "_fui.bytes"), Prefix, LoadAtlas);
            _artwork = Fgui.UIPackage.CreateObject(Package, "RuntimeAiLabTrialAccessoryArtwork").asCom;
        }

        private static object LoadAtlas(string name, string extension, Type type, out Fgui.DestroyMethod destroy)
        { destroy = Fgui.DestroyMethod.None; return AssetDatabase.LoadAssetAtPath<Texture2D>(name + extension); }

        [TearDown]
        public void Teardown()
        {
            _artwork?.Dispose();
            if (_ownsPackage) Fgui.UIPackage.RemovePackage(Package);
        }

        [Test]
        public void PublishedPackageContainsAllTwentySourceIcons()
        {
            Assert.NotNull(_artwork);
            foreach (var control in CharacterTestAiLabCardTrialControlContract.AccessoryArtworkControls)
            {
                var image = _artwork.GetChild(control.Name)?.asImage;
                Assert.NotNull(image, control.Name);
                Assert.NotNull(image.texture, control.Name);
                Assert.IsNotEmpty(image.resourceURL, control.Name);
                Assert.IsFalse(image.visible, "The artwork bank must never appear over the experiment UI.");
            }
            Assert.AreEqual(20, _artwork.numChildren);
        }

        [TestCase("RuntimeAiLabCardTrialRow", 184f)]
        [TestCase("RuntimeAiLabCardTrialHandCard", 220f)]
        public void PublishedIconColumnStaysOnTheCardAndAboveDescription(string component, float faceWidth)
        {
            var wrapper = Fgui.UIPackage.CreateObject(Package, component).asCom;
            using var skin = new CharacterTestFairyGuiTacticalCardSkin();
            try
            {
                var face = wrapper.GetChild("face").asCom;
                CharacterTestFairyGuiPlayerCardPresentation.Initialize(face);
                var data = new CharacterTestCardUiPreviewData { Id = "fixture.accessory-layout", Name = "主卡",
                    Cost = 3, Description = "主卡说明", IconKey = "ui.behavior-card.custom",
                    Actions = Array.Empty<CharacterTestTacticalActionLocation>() };
                CharacterTestFairyGuiPlayerCardPresentation.Bind(face, data, 34);
                skin.Bind(face, data); face.SetScale(faceWidth / 378f, faceWidth / 378f);
                var description = face.GetChild("description");
                float descriptionTop = face.y + description.y * face.scaleY;
                float previousBottom = 0;
                for (int i = 1; i <= 3; i++)
                {
                    var slot = wrapper.GetChild("slot" + i).asCom;
                    Assert.NotNull(slot);
                    Assert.GreaterOrEqual(slot.x, face.x);
                    Assert.LessOrEqual(slot.x + slot.width, face.x + faceWidth);
                    Assert.Less(slot.y + slot.height, descriptionTop, "A mounted icon must leave the main description untouched.");
                    if (i > 1) Assert.Greater(slot.y, previousBottom);
                    previousBottom = slot.y + slot.height;
                }
            }
            finally { wrapper.Dispose(); }
        }

        [Test]
        public void ReusedSlotClearsItsArtworkSelectionAndInteraction()
        {
            var slot = Fgui.UIPackage.CreateObject(Package, "RuntimeAiLabTrialAccessorySlot").asCom;
            try
            {
                var texture = _artwork.GetChild("borrowMomentum").asImage.texture;
                CharacterTestFairyGuiTacticalAccessoryPresentation.Bind(slot, texture, CharacterTacticalCardQuality.Gold, true, true);
                Assert.IsTrue(slot.GetChild("icon").visible);
                Assert.IsFalse(slot.GetChild("empty").visible);
                Assert.IsTrue(slot.GetChild("selected").visible);
                Assert.IsTrue(slot.touchable);
                CharacterTestFairyGuiTacticalAccessoryPresentation.Bind(slot, null, CharacterTacticalCardQuality.Silver, false, false);
                Assert.IsFalse(slot.GetChild("icon").visible);
                Assert.IsTrue(slot.GetChild("empty").visible);
                Assert.IsFalse(slot.GetChild("selected").visible);
                Assert.IsFalse(slot.touchable, "Read-only hand icons must pass input to their owning card.");
                Assert.AreSame(texture, _artwork.GetChild("borrowMomentum").asImage.texture);
            }
            finally { slot.Dispose(); }
        }
    }
}
