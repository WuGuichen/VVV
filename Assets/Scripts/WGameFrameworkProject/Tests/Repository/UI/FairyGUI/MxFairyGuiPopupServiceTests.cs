using System.Collections.Generic;
using MxFramework.UI;
using MxFramework.UI.FairyGui;
using NUnit.Framework;
using UnityEngine;
using Fgui = global::FairyGUI;

namespace MxFramework.Tests.UI.FairyGui
{
    public sealed class MxFairyGuiPopupServiceTests
    {
        private static readonly MxUiViewId Owner = new MxUiViewId("ui.popup.owner");

        [SetUp]
        public void SetUp()
        {
            FairyGuiTestStageReset.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            FairyGuiTestStageReset.Reset();
        }

        [Test]
        public void ClampPosition_ConstrainsPopupToSafeArea()
        {
            Vector2 result = MxFairyGuiPopupService.ClampPosition(
                new Vector2(1180f, -20f),
                new Vector2(240f, 320f),
                new Vector2(1280f, 720f),
                16f);

            Assert.AreEqual(new Vector2(1024f, 16f), result);
        }

        [TestCase(1280f, 720f)]
        [TestCase(1920f, 1080f)]
        [TestCase(2560f, 1440f)]
        [TestCase(3440f, 1440f)]
        public void SidePlacement_StaysInsideSafeAreaAtSupportedResolutions(
            float rootWidth,
            float rootHeight)
        {
            var popupRoot = new FakePopupRoot(rootWidth, rootHeight);
            var anchor = new Fgui.GButton();
            anchor.SetSize(316f, 54f);
            anchor.SetPosition(rootWidth - 390f, 370f, 0f);
            var popup = new Fgui.GComponent();
            popup.SetSize(570f, 600f);
            using var service = new MxFairyGuiPopupService(popupRoot);

            Assert.IsTrue(service.Show(
                Owner,
                popup,
                anchor,
                new MxFairyGuiPopupOptions(
                    MxFairyGuiPopupPlacement.Left,
                    safeMargin: 16f,
                    anchorGap: 18f)));

            Assert.GreaterOrEqual(popup.x, 16f);
            Assert.GreaterOrEqual(popup.y, 16f);
            Assert.LessOrEqual(popup.x + popup.width, rootWidth - 16f);
            Assert.LessOrEqual(popup.y + popup.height, rootHeight - 16f);
        }

        [Test]
        public void Toggle_OpenThenClose_ReportsToggleAndMaintainsStack()
        {
            var anchor = new Fgui.GButton();
            anchor.SetSize(120f, 40f);
            var popup = new Fgui.GComponent();
            popup.SetSize(260f, 180f);
            var reasons = new List<MxFairyGuiPopupCloseReason>();
            var popupRoot = new FakePopupRoot();
            using var service = new MxFairyGuiPopupService(popupRoot);

            Assert.IsTrue(service.Toggle(Owner, popup, anchor, onClosed: reasons.Add));
            Assert.IsTrue(service.IsOpen(popup));
            Assert.AreSame(popup, service.TopPopup);

            Assert.IsTrue(service.Toggle(Owner, popup, anchor));
            Assert.IsFalse(service.IsOpen(popup));
            CollectionAssert.AreEqual(
                new[] { MxFairyGuiPopupCloseReason.Toggle },
                reasons);
        }

        [Test]
        public void Show_SiblingPopup_ReplacesPreviousPopup()
        {
            var firstAnchor = new Fgui.GButton();
            var secondAnchor = new Fgui.GButton();
            var first = new Fgui.GComponent();
            var second = new Fgui.GComponent();
            first.SetSize(100f, 100f);
            second.SetSize(100f, 100f);
            MxFairyGuiPopupCloseReason reason = default;
            using var service = new MxFairyGuiPopupService(new FakePopupRoot());

            Assert.IsTrue(service.Show(Owner, first, firstAnchor, onClosed: value => reason = value));
            Assert.IsTrue(service.Show(Owner, second, secondAnchor));

            Assert.AreEqual(MxFairyGuiPopupCloseReason.Replaced, reason);
            Assert.IsFalse(service.IsOpen(first));
            Assert.IsTrue(service.IsOpen(second));
        }

        [Test]
        public void NativeOutsideClose_IsObservedAsOutsidePointer()
        {
            var anchor = new Fgui.GButton();
            var popup = new Fgui.GComponent();
            popup.SetSize(100f, 100f);
            MxFairyGuiPopupCloseReason reason = default;
            var popupRoot = new FakePopupRoot();
            using var service = new MxFairyGuiPopupService(popupRoot);
            Assert.IsTrue(service.Show(Owner, popup, anchor, onClosed: value => reason = value));

            popupRoot.HidePopup(popup);

            Assert.IsFalse(service.IsOpen(popup));
            Assert.AreEqual(MxFairyGuiPopupCloseReason.OutsidePointer, reason);
        }

        [Test]
        public void FocusInputBridge_CancelClosesPopupBeforeModal()
        {
            var anchor = new Fgui.GButton();
            var popup = new Fgui.GComponent();
            popup.SetSize(100f, 100f);
            using var service = new MxFairyGuiPopupService(new FakePopupRoot());
            Assert.IsTrue(service.Show(Owner, popup, anchor));
            var bridge = new MxFairyGuiFocusInputBridge(
                new MxFairyGuiLayerHost(),
                popupService: service);

            Assert.IsTrue(bridge.Process(Owner, MxFairyGuiFocusNavigationIntent.Cancel));
            Assert.IsFalse(service.HasOpenPopup);
        }

        private sealed class FakePopupRoot : IMxFairyGuiPopupRoot
        {
            private readonly Fgui.GComponent _root = new Fgui.GComponent();

            public FakePopupRoot(float width = 1280f, float height = 720f)
            {
                _root.SetSize(width, height);
            }

            public Vector2 Size => _root.size;

            public bool IsAttached(Fgui.GObject popup)
            {
                return popup != null && popup.parent == _root;
            }

            public Vector2 GetAnchorPosition(Fgui.GObject anchor)
            {
                return anchor?.xy ?? Vector2.zero;
            }

            public void TogglePopup(
                Fgui.GObject popup,
                Fgui.GObject anchor,
                Fgui.PopupDirection direction,
                bool closeUntilUpEvent)
            {
                if (IsAttached(popup))
                    popup.RemoveFromParent();
                else
                    _root.AddChild(popup);
            }

            public void HidePopup(Fgui.GObject popup)
            {
                if (IsAttached(popup))
                    popup.RemoveFromParent();
            }

            public void HideAll()
            {
                while (_root.numChildren > 0)
                    _root.RemoveChildAt(_root.numChildren - 1);
            }
        }
    }
}
