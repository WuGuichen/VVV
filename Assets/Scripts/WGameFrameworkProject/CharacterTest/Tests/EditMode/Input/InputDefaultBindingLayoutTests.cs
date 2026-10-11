using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MxFramework.Input;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MxFramework.Tests.Input
{
    public sealed class InputDefaultBindingLayoutTests
    {
        private const string DefaultAssetPath =
            "Assets/Input/MxFramework/Config/MxFrameworkInputActions.inputactions";

        [Test]
        public void DefaultInputBindingCatalog_HasNoActiveDomainConflicts()
        {
            IReadOnlyList<DefaultInputBindingConflict> conflicts =
                DefaultInputBindingCatalog.FindConflicts();

            Assert.AreEqual(0, conflicts.Count, string.Join("\n", conflicts));
        }

        [Test]
        public void DefaultInputActionAssetFactory_UsesUnifiedFRowDebugBindings()
        {
            InputActionAsset asset = DefaultInputActionAssetFactory.Create();
            try
            {
                AssertHasBinding(asset, "Gameplay/Pause", "<Keyboard>/escape");
                AssertHasBinding(asset, "Gameplay/Pause", "<Gamepad>/start");
                AssertHasBinding(asset, "Gameplay/CameraModeNext", "<Keyboard>/v");
                AssertHasBinding(asset, "Gameplay/TargetLock", "<Keyboard>/l");
                AssertHasBinding(asset, "Gameplay/TargetLock", "<Mouse>/middleButton");
                AssertHasBinding(asset, "Gameplay/TargetLock", "<Gamepad>/rightStickPress");
                AssertHasBinding(asset, "Gameplay/Dodge", "<Keyboard>/leftShift");
                AssertHasBinding(asset, "Gameplay/Sprint", "<Keyboard>/leftCtrl");
                AssertDoesNotHaveBinding(asset, "Gameplay/Dodge", "<Keyboard>/leftCtrl");
                AssertDoesNotHaveBinding(asset, "Gameplay/Sprint", "<Keyboard>/leftShift");
                AssertHasBinding(asset, "Gameplay/GuidancePrevious", "<Keyboard>/leftBracket");
                AssertHasBinding(asset, "Gameplay/GuidanceNext", "<Keyboard>/rightBracket");
                Assert.IsNull(
                    asset.FindAction("Gameplay/MiddleClick", false),
                    "Gameplay middle click must be owned by semantic TargetLock rather than a duplicate pointer action.");
                for (int i = 1; i <= 9; i++)
                {
                    AssertHasBinding(
                        asset,
                        "Gameplay/SkillSlot" + i,
                        "<Keyboard>/" + i);
                }

                AssertDoesNotHaveBinding(asset, "Gameplay/Pause", "<Keyboard>/p");
                AssertDoesNotHaveBinding(asset, "Gameplay/AttackSecondary", "<Keyboard>/f");
                AssertHasBinding(asset, "Debug/ToggleHud", "<Keyboard>/f1");
                AssertHasBinding(asset, "Debug/DebugCycle", "<Keyboard>/f2");
                AssertHasBinding(asset, "Debug/DebugStep", "<Keyboard>/f3");
                AssertHasBinding(asset, "Debug/Restart", "<Keyboard>/f4");
                AssertHasBinding(asset, "Debug/DebugPrimary", "<Keyboard>/f5");
                AssertHasBinding(asset, "Debug/DebugSecondary", "<Keyboard>/f6");
                AssertHasBinding(asset, "Debug/AudioPrimary", "<Keyboard>/f7");
                AssertHasBinding(asset, "Debug/AudioSecondary", "<Keyboard>/f8");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void DefaultInputActionAssetFactory_DoesNotUseLegacyDebugKeys()
        {
            InputActionAsset asset = DefaultInputActionAssetFactory.Create();
            var legacyPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "<Keyboard>/p",
                "<Keyboard>/f",
                "<Keyboard>/h",
                "<Keyboard>/j",
                "<Keyboard>/q",
                "<Keyboard>/x",
                "<Keyboard>/t",
                "<Keyboard>/r"
            };

            try
            {
                foreach (InputActionMap map in asset.actionMaps)
                {
                    foreach (InputBinding binding in map.bindings)
                    {
                        Assert.IsFalse(
                            legacyPaths.Contains(binding.path ?? string.Empty),
                            map.name + "/" + binding.action + " still uses " + binding.path);
                    }
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void SerializedInputActions_MatchesDefaultFactory()
        {
            InputActionAsset serialized = AssetDatabase.LoadAssetAtPath<InputActionAsset>(DefaultAssetPath);
            Assert.IsNotNull(serialized, "Missing default input action asset at " + DefaultAssetPath);
            InputActionAsset generated = DefaultInputActionAssetFactory.Create();
            try
            {
                Assert.AreEqual(CreateSignature(generated), CreateSignature(serialized));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(generated);
            }
        }

        [Test]
        public void DemoSources_DoNotUseDebugInputsAsGameplayAliases()
        {
            AssertSourceDoesNotContain(
                "Assets/Scripts/WGameFrameworkProject/Demos/Tetris/TetrisPlayableDemo.cs",
                "input.PausePressed || input.DebugSecondaryPressed",
                "input.DebugCyclePressed");
            AssertSourceDoesNotContain(
                "Assets/Scripts/WGameFrameworkProject/Demos/Breakout/BreakoutPlayableDemo.cs",
                "input.PausePressed || input.DebugSecondaryPressed");
            AssertSourceDoesNotContain(
                "Assets/Scripts/WGameFrameworkProject/Demos/MarbleMaze/MarbleMazePhysicsDemo.cs",
                "input.PausePressed || input.DebugSecondaryPressed");
            AssertSourceDoesNotContain(
                "Assets/Scripts/WGameFrameworkProject/Demos/CharacterControl/CharacterControlPlayableDemo.cs",
                "input.PausePressed || input.DebugSecondaryPressed",
                "input.AttackPrimaryPressed || input.DebugPrimaryPressed",
                "input.AttackSecondaryPressed || input.ToggleHudPressed",
                "input.DebugStepPressed || input.DebugCyclePressed");
        }

        private static void AssertHasBinding(InputActionAsset asset, string actionId, string path)
        {
            InputAction action = asset.FindAction(actionId, false);
            Assert.IsNotNull(action, "Missing action " + actionId);
            Assert.IsTrue(
                action.bindings.Any(binding => string.Equals(binding.path, path, StringComparison.OrdinalIgnoreCase)),
                actionId + " missing " + path);
        }

        private static void AssertDoesNotHaveBinding(InputActionAsset asset, string actionId, string path)
        {
            InputAction action = asset.FindAction(actionId, false);
            Assert.IsNotNull(action, "Missing action " + actionId);
            Assert.IsFalse(
                action.bindings.Any(binding => string.Equals(binding.path, path, StringComparison.OrdinalIgnoreCase)),
                actionId + " should not use " + path);
        }

        private static void AssertSourceDoesNotContain(string path, params string[] patterns)
        {
            string source = File.ReadAllText(path);
            for (int i = 0; i < patterns.Length; i++)
                Assert.That(source, Does.Not.Contain(patterns[i]), path + " contains " + patterns[i]);
        }

        private static string CreateSignature(InputActionAsset asset)
        {
            var rows = new List<string>();
            foreach (InputActionMap map in asset.actionMaps)
            {
                rows.Add("M|" + map.name);
                foreach (InputAction action in map.actions)
                {
                    rows.Add("A|" + map.name + "|" + action.name + "|" + action.type + "|"
                        + (action.expectedControlType ?? string.Empty));
                }

                foreach (InputBinding binding in map.bindings)
                {
                    rows.Add("B|" + map.name + "|" + (binding.action ?? string.Empty) + "|"
                        + (binding.name ?? string.Empty) + "|"
                        + (binding.path ?? string.Empty) + "|"
                        + binding.isComposite + "|"
                        + binding.isPartOfComposite);
                }
            }

            return string.Join("\n", rows);
        }
    }
}
