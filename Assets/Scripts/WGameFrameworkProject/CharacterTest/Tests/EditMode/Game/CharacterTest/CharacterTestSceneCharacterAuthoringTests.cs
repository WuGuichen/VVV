using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using MxFramework.Config.Levels;
using MxFramework.Editor.Levels;
using MxFramework.Game.CharacterTest;
using MxFramework.Game.CharacterTest.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed partial class CharacterTestSceneCharacterAuthoringTests
    {
        [TearDown]
        public void TearDown()
        {
            foreach (GameObject gameObject in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include))
            {
                if (gameObject != null && gameObject.name.StartsWith("CharacterAuthoringTest", System.StringComparison.Ordinal))
                    Object.DestroyImmediate(gameObject);
            }
        }

        private static CharacterTestSceneCharacterAuthoringDocument CreateTwoCharacterDocument(
            Transform root,
            Transform player,
            Transform enemy)
        {
            return new CharacterTestSceneCharacterAuthoringDocument
            {
                LevelId = CharacterTestSceneCharacterAuthoringAdapter.DefaultLevelId,
                ScenePath = CharacterTestSceneCharacterAuthoringAdapter.DefaultScenePath,
                RootPath = root.name,
                Characters = new List<CharacterTestSceneCharacterDefinition>
                {
                    CreateCharacter(root, player, "player.local", 1, CharacterTestCharacterControlOwner.LocalPlayer),
                    CreateCharacter(root, enemy, "enemy.ai", 2, CharacterTestCharacterControlOwner.AiControlled)
                }
            };
        }

        private static CharacterTestCharacterSpawnSpec[] InvokeCollectLevelCharacterSpawnSpecs(
            CharacterTestRuntimeSessionBuilder builder)
        {
            MethodInfo method = typeof(CharacterTestRuntimeSessionBuilder).GetMethod(
                "CollectLevelCharacterSpawnSpecs",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(method);
            return (CharacterTestCharacterSpawnSpec[])method.Invoke(builder, Array.Empty<object>());
        }

        private static CharacterTestRuntimeAiPlannerProfileBinding[] InvokeBuildRuntimeAiPlannerProfileBindings(
            CharacterTestRuntimeSessionBuilder builder,
            CharacterTestCharacterSpawnSpec[] specs,
            out CharacterTestCharacterSpawnSpec[] assignedSpecs)
        {
            MethodInfo method = typeof(CharacterTestRuntimeSessionBuilder).GetMethod(
                "BuildRuntimeAiPlannerProfileBindings",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(method);
            object[] arguments = { specs, null };
            var bindings = (CharacterTestRuntimeAiPlannerProfileBinding[])method.Invoke(builder, arguments);
            assignedSpecs = (CharacterTestCharacterSpawnSpec[])arguments[1];
            return bindings;
        }

        private static CharacterTestSceneCharacterAuthoringDocument CreateEmptyCharacterDocument()
        {
            return new CharacterTestSceneCharacterAuthoringDocument
            {
                LevelId = CharacterTestSceneCharacterAuthoringAdapter.DefaultLevelId,
                ScenePath = CharacterTestSceneCharacterAuthoringAdapter.DefaultScenePath,
                RootPath = "CharacterAuthoringTestRoot",
                Characters = new List<CharacterTestSceneCharacterDefinition>()
            };
        }

        private static CharacterTestSceneCharacterDefinition CreateCharacter(
            Transform root,
            Transform transform,
            string stableId,
            int teamId,
            CharacterTestCharacterControlOwner owner)
        {
            LevelTransformReference transformRef =
                CharacterTestWorldObjectAuthoringAdapter.CreateTransformRef(root, transform);
            return new CharacterTestSceneCharacterDefinition
            {
                Enabled = true,
                StableId = stableId,
                DisplayName = stableId,
                TransformRef = transformRef,
                TransformPath = transformRef.Path,
                CharacterId = CharacterTestCharacterDefinitionRegistry.IronVanguardCharacterId,
                TeamId = teamId,
                ControlOwner = owner,
                RuntimeAiPlannerProfilePath = owner == CharacterTestCharacterControlOwner.AiControlled
                    ? "MxFramework/CharacterTest/runtime_ai_planner/skill_caster.json"
                    : string.Empty,
                Group = owner == CharacterTestCharacterControlOwner.LocalPlayer ? "Players" : "Enemies"
            };
        }

        private static GameObject CreateRoot()
        {
            return new GameObject("CharacterAuthoringTestRoot");
        }

        private static MxLevelEditorSceneViewContext CreateSceneViewContext(
            CharacterTestSceneCharacterLevelEditorModule module,
            MxLevelEditorListRow selectedRow,
            IReadOnlyList<MxLevelEditorListRow> rows)
        {
            return new MxLevelEditorSceneViewContext(
                new MxLevelEditorContext(null, module, () => { }),
                "document",
                selectedRow,
                rows);
        }

        private static MxLevelEditorListRow CreateEditorRow(
            string title,
            CharacterTestCharacterControlOwner owner,
            int teamId,
            bool enabled,
            int index = 0)
        {
            var row = new CharacterTestSceneCharacterLevelEditorModule.Row(
                index,
                "Character",
                owner == CharacterTestCharacterControlOwner.LocalPlayer ? "Players" : "Enemies",
                "Iron Vanguard",
                title.Replace(" ", ".").ToLowerInvariant(),
                title,
                enabled ? "OK" : "Disabled",
                title,
                owner,
                teamId,
                enabled);
            return new MxLevelEditorListRow(
                row.Key,
                row.Kind,
                row.Group,
                row.Definition,
                row.Id,
                row.Transform,
                row.Status,
                row.SearchText,
                row);
        }

        private static CharacterTestSceneCharacterLevelEditorModule.SceneOverlayItem CreateOverlayItem(
            MxLevelEditorListRow listRow,
            Vector3 position,
            bool selected)
        {
            var row = (CharacterTestSceneCharacterLevelEditorModule.Row)listRow.Payload;
            return new CharacterTestSceneCharacterLevelEditorModule.SceneOverlayItem(
                listRow,
                position,
                row.Title,
                row.Owner,
                row.Enabled,
                selected);
        }

        private static GameObject CreateChild(Transform parent, string name)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent, worldPositionStays: false);
            return child;
        }

        private static void AssertIssue(
            LevelWorldObjectAuthoringValidationReport report,
            string code)
        {
            for (int i = 0; i < report.Issues.Count; i++)
            {
                if (report.Issues[i].Code == code)
                    return;
            }

            Assert.Fail("Expected issue " + code + "\n" + JoinIssues(report));
        }

        private static void AssertSpecOwner(
            IReadOnlyList<CharacterTestCharacterSpawnSpec> specs,
            CharacterTestCharacterControlOwner owner,
            int teamId)
        {
            for (int i = 0; i < specs.Count; i++)
            {
                if (specs[i].ControlOwner == owner)
                {
                    Assert.AreEqual(teamId, specs[i].TeamId);
                    return;
                }
            }

            Assert.Fail("Expected spawn spec owner " + owner + ".");
        }

        private static CharacterTestCharacterSpawnSpec FindSpec(
            IReadOnlyList<CharacterTestCharacterSpawnSpec> specs,
            CharacterTestCharacterControlOwner owner)
        {
            for (int i = 0; i < specs.Count; i++)
            {
                if (specs[i].ControlOwner == owner)
                    return specs[i];
            }

            Assert.Fail("Expected spawn spec owner " + owner + ".");
            return default;
        }

        private static CharacterTestSceneCharacterDefinition FindCharacter(
            IReadOnlyList<CharacterTestSceneCharacterDefinition> characters,
            CharacterTestCharacterControlOwner owner)
        {
            for (int i = 0; i < characters.Count; i++)
            {
                if (characters[i].ControlOwner == owner)
                    return characters[i];
            }

            Assert.Fail("Expected character owner " + owner + ".");
            return null;
        }

        private static string JoinIssues(LevelWorldObjectAuthoringValidationReport report)
        {
            if (report == null || report.Issues.Count == 0)
                return string.Empty;

            var lines = new List<string>();
            for (int i = 0; i < report.Issues.Count; i++)
            {
                LevelWorldObjectAuthoringIssue issue = report.Issues[i];
                lines.Add(issue.Severity + " " + issue.Code + " " + issue.Path + " " + issue.Message);
            }

            return string.Join("\n", lines);
        }

        private static Button FindButton(VisualElement root, string text)
        {
            if (root == null)
                return null;
            if (root is Button button && button.text == text)
                return button;

            for (int i = 0; i < root.childCount; i++)
            {
                Button child = FindButton(root[i], text);
                if (child != null)
                    return child;
            }

            return null;
        }
    }
}
