using System.Collections.Generic;
using MxFramework.Editor;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace MxFramework.Tests.Editor
{
    public sealed class MxAuthoringReferenceFieldTests
    {
        [Test]
        public void Create_WithNoOptions_ShowsDiagnosticWithoutInput()
        {
            VisualElement root = MxAuthoringReferenceField.Create(
                "作用目标",
                0,
                new List<MxAuthoringReferenceOption<int>>(),
                _ => Assert.Fail("An empty field cannot change a value."));

            Assert.That(
                root.ClassListContains(
                    MxAuthoringReferenceField.EmptyClassName),
                Is.True);
            Assert.That(
                root.Q<DropdownField>(
                    MxAuthoringReferenceField.ValueElementName),
                Is.Null);
            Assert.That(
                root.Q<HelpBox>(
                    MxAuthoringReferenceField.DiagnosticElementName),
                Is.Not.Null);
        }

        [Test]
        public void Create_WithOneOption_ShowsReadOnlyValue()
        {
            var options = new List<MxAuthoringReferenceOption<int>>
            {
                new MxAuthoringReferenceOption<int>(
                    "subject",
                    1,
                    "进入区域的对象",
                    "来自 Trigger Subject。")
            };

            VisualElement root = MxAuthoringReferenceField.Create(
                "作用目标",
                1,
                options,
                _ => Assert.Fail("A read-only field cannot change a value."));

            Assert.That(
                root.ClassListContains(
                    MxAuthoringReferenceField.SingleClassName),
                Is.True);
            Assert.That(
                root.Q<Label>(
                    MxAuthoringReferenceField.ValueElementName)?.text,
                Is.EqualTo("进入区域的对象"));
            Assert.That(
                root.Q<DropdownField>(
                    MxAuthoringReferenceField.ValueElementName),
                Is.Null);
        }

        [Test]
        public void Create_WithMultipleOptions_UsesOnlyRegisteredChoices()
        {
            var options = new List<MxAuthoringReferenceOption<int>>
            {
                new MxAuthoringReferenceOption<int>("none", 0, "未指定"),
                new MxAuthoringReferenceOption<int>("aura", 501008, "501008 · 守护光环")
            };

            VisualElement root = MxAuthoringReferenceField.Create(
                "效果归因",
                0,
                options,
                _ => { });
            DropdownField field = root.Q<DropdownField>(
                MxAuthoringReferenceField.ValueElementName);

            Assert.That(field, Is.Not.Null);
            CollectionAssert.AreEqual(
                new[] { "未指定", "501008 · 守护光环" },
                field.choices);
        }

        [Test]
        public void Create_WithUnknownCurrentValue_PreservesItAndWarns()
        {
            const int UnknownValue = 777777;
            bool changed = false;
            var options = new List<MxAuthoringReferenceOption<int>>
            {
                new MxAuthoringReferenceOption<int>("none", 0, "未指定"),
                new MxAuthoringReferenceOption<int>("aura", 501008, "501008 · 守护光环")
            };

            VisualElement root = MxAuthoringReferenceField.Create(
                "效果归因",
                UnknownValue,
                options,
                _ => changed = true,
                unknownValueFormatter: value => "未登记 " + value);
            DropdownField field = root.Q<DropdownField>(
                MxAuthoringReferenceField.ValueElementName);

            Assert.That(changed, Is.False);
            Assert.That(
                root.ClassListContains(
                    MxAuthoringReferenceField.UnknownClassName),
                Is.True);
            Assert.That(field.value, Is.EqualTo("未登记 777777"));
            CollectionAssert.AreEqual(
                new[] { "未指定", "501008 · 守护光环" },
                field.choices,
                "The compatibility value must not become a valid candidate.");
            Assert.That(
                root.Q<HelpBox>(
                    MxAuthoringReferenceField.DiagnosticElementName),
                Is.Not.Null);
        }
    }
}
