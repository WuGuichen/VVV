using MxFramework.UI.FairyGui;
using NUnit.Framework;
using UnityEngine;
using Fgui = global::FairyGUI;

namespace MxFramework.Tests.UI.FairyGui
{
    public sealed class MxFairyGuiTooltipServiceTests
    {
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
        public void CalculatePlacement_ForWideTarget_PrefersBelow()
        {
            MxFairyGuiTooltipPlacementResult result =
                MxFairyGuiTooltipService.CalculatePlacement(
                    new Rect(300f, 200f, 360f, 56f),
                    new Vector2(480f, 228f),
                    new Vector2(180f, 48f),
                    new Vector2(1280f, 720f),
                    MxFairyGuiTooltipOptions.Default);

            Assert.AreEqual(MxFairyGuiTooltipPlacement.Below, result.Placement);
            Assert.AreEqual(266f, result.Position.y);
        }

        [Test]
        public void CalculatePlacement_ForSquareTargetOnRight_PrefersInwardLeft()
        {
            MxFairyGuiTooltipPlacementResult result =
                MxFairyGuiTooltipService.CalculatePlacement(
                    new Rect(1120f, 200f, 64f, 64f),
                    new Vector2(1152f, 232f),
                    new Vector2(180f, 48f),
                    new Vector2(1280f, 720f),
                    MxFairyGuiTooltipOptions.Default);

            Assert.AreEqual(MxFairyGuiTooltipPlacement.Left, result.Placement);
            Assert.Less(result.Position.x, 1120f);
        }

        [Test]
        public void CalculatePlacement_WhenBelowDoesNotFit_FlipsAbove()
        {
            MxFairyGuiTooltipPlacementResult result =
                MxFairyGuiTooltipService.CalculatePlacement(
                    new Rect(300f, 660f, 360f, 44f),
                    new Vector2(480f, 682f),
                    new Vector2(180f, 48f),
                    new Vector2(1280f, 720f),
                    MxFairyGuiTooltipOptions.Default);

            Assert.AreEqual(MxFairyGuiTooltipPlacement.Above, result.Placement);
            Assert.Less(result.Position.y, 660f);
        }

        [Test]
        public void CalculatePlacement_WhenNoSideFits_ClampsInsideSafeMargin()
        {
            var options = new MxFairyGuiTooltipOptions(safeMargin: 16f);
            MxFairyGuiTooltipPlacementResult result =
                MxFairyGuiTooltipService.CalculatePlacement(
                    new Rect(10f, 10f, 20f, 20f),
                    new Vector2(20f, 20f),
                    new Vector2(300f, 160f),
                    new Vector2(320f, 180f),
                    options);

            Assert.AreEqual(16f, result.Position.x);
            Assert.AreEqual(16f, result.Position.y);
        }

        [Test]
        public void DefaultVisualMetrics_MatchReadablePlayerFacingScale()
        {
            Assert.AreEqual(18, MxFairyGuiTooltipService.DefaultFontSize);
            Assert.AreEqual(6, MxFairyGuiTooltipService.DefaultLineSpacing);
            Assert.AreEqual(16f, MxFairyGuiTooltipService.HorizontalPadding);
            Assert.AreEqual(11f, MxFairyGuiTooltipService.VerticalPadding);
        }

        [Test]
        public void TargetHierarchy_WhenClickedTargetHides_BecomesUnavailable()
        {
            var target = new Fgui.GButton();
            target.onClick.Add(() => target.visible = false);
            using var service = new MxFairyGuiTooltipService();
            service.Bind(target, "Hidden after click");

            target.onClick.Call();

            Assert.IsFalse(
                MxFairyGuiTooltipService.IsTargetVisibleAndEnabledInHierarchy(target));
        }

        [Test]
        public void TargetHierarchy_WhenParentHides_BecomesUnavailable()
        {
            var parent = new Fgui.GComponent();
            var target = new Fgui.GButton();
            parent.AddChild(target);

            parent.visible = false;

            Assert.IsFalse(
                MxFairyGuiTooltipService.IsTargetVisibleAndEnabledInHierarchy(target));
        }

        [Test]
        public void Bind_RegistersClickAndStageRemovalLifecycleHandlers()
        {
            var target = new Fgui.GButton();
            using var service = new MxFairyGuiTooltipService();
            service.Bind(target, "Lifecycle target");

            Assert.IsFalse(target.onClick.isEmpty);
            Assert.IsFalse(target.displayObject.onRemovedFromStage.isEmpty);
        }
    }
}
