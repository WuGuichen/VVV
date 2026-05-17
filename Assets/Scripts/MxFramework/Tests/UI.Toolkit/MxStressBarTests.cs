using System.IO;
using MxFramework.UI.Toolkit;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace MxFramework.Tests.UI.Toolkit
{
    public sealed class MxStressBarTests
    {
        [Test]
        public void SetValue_ClampsCurrentAndHandlesZeroMax()
        {
            var bar = new MxStressBar();

            bar.SetValue(150, 100);
            Assert.AreEqual(100, bar.CurrentValue);
            Assert.AreEqual(100, bar.MaxValue);
            Assert.AreEqual(1f, bar.NormalizedValue);

            bar.SetValue(-5, 100);
            Assert.AreEqual(0, bar.CurrentValue);
            Assert.AreEqual(0f, bar.NormalizedValue);

            bar.SetValue(25, 0);
            Assert.AreEqual(0, bar.CurrentValue);
            Assert.AreEqual(0, bar.MaxValue);
            Assert.AreEqual(0f, bar.NormalizedValue);
        }

        [Test]
        public void SetBreakLine_ClampsAgainstSafeMax()
        {
            var bar = new MxStressBar();

            bar.SetBreakLine(150, 100);
            Assert.AreEqual(100, bar.BreakLineValue);
            Assert.AreEqual(1f, bar.BreakLineNormalizedValue);
            Assert.AreEqual(DisplayStyle.Flex, bar.Q<VisualElement>("stress-bar-break-line").style.display.value);

            bar.SetBreakLine(-10, 100);
            Assert.AreEqual(0, bar.BreakLineValue);
            Assert.AreEqual(0f, bar.BreakLineNormalizedValue);

            bar.SetBreakLine(10, 0);
            Assert.AreEqual(0, bar.BreakLineValue);
            Assert.AreEqual(0f, bar.BreakLineNormalizedValue);
            Assert.AreEqual(DisplayStyle.None, bar.Q<VisualElement>("stress-bar-break-line").style.display.value);
        }

        [Test]
        public void SetActive_TogglesExclusiveActiveClasses()
        {
            var bar = new MxStressBar();

            bar.SetActive(false);
            Assert.IsFalse(bar.IsActive);
            Assert.IsFalse(bar.ClassListContains(MxUiThemeTokens.StressBarActive));
            Assert.IsTrue(bar.ClassListContains(MxUiThemeTokens.StressBarInactive));

            bar.SetActive(true);
            Assert.IsTrue(bar.IsActive);
            Assert.IsTrue(bar.ClassListContains(MxUiThemeTokens.StressBarActive));
            Assert.IsFalse(bar.ClassListContains(MxUiThemeTokens.StressBarInactive));
        }

        [Test]
        public void SetTone_AppliesOneToneClassToFill()
        {
            var bar = new MxStressBar();
            VisualElement fill = bar.Q<VisualElement>("stress-bar-fill");

            bar.SetTone(MxUiTone.Warning);
            AssertTone(fill, MxUiThemeTokens.StatusWarning);

            bar.SetTone(MxUiTone.Danger);
            AssertTone(fill, MxUiThemeTokens.StatusDanger);
            Assert.AreEqual(MxUiTone.Danger, bar.Tone);
        }

        [Test]
        public void UiToolkitAssembly_DoesNotReferenceGameplayOrCombat()
        {
            string asmdef = File.ReadAllText("Assets/Scripts/MxFramework/UI.Toolkit/MxFramework.UI.Toolkit.asmdef");
            Assert.That(asmdef, Does.Not.Contain("MxFramework.Gameplay"));
            Assert.That(asmdef, Does.Not.Contain("MxFramework.Combat"));

            string source = File.ReadAllText("Assets/Scripts/MxFramework/UI.Toolkit/Runtime/MxStressBar.cs");
            Assert.That(source, Does.Not.Contain("MxFramework.Gameplay"));
            Assert.That(source, Does.Not.Contain("MxFramework.Combat"));
        }

        private static void AssertTone(VisualElement element, string expectedClass)
        {
            int count = 0;
            count += element.ClassListContains(MxUiThemeTokens.StatusNeutral) ? 1 : 0;
            count += element.ClassListContains(MxUiThemeTokens.StatusPositive) ? 1 : 0;
            count += element.ClassListContains(MxUiThemeTokens.StatusWarning) ? 1 : 0;
            count += element.ClassListContains(MxUiThemeTokens.StatusDanger) ? 1 : 0;

            Assert.AreEqual(1, count);
            Assert.IsTrue(element.ClassListContains(expectedClass));
        }
    }
}
