using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using MxFramework.Combat.Physics;
using MxFramework.Config.Levels;
using MxFramework.Editor.Levels;
using MxFramework.Game.CharacterTest;
using MxFramework.Game.CharacterTest.Editor;
using MxFramework.Gameplay;
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
    public sealed partial class CharacterTestWorldObjectLevelAuthoringTests
    {
        private const string ScenePath = CharacterTestWorldObjectAuthoringAdapter.DefaultScenePath;

        [Test]
        public void LevelUsage_ProjectsToSharedTargetClassification()
        {
            Assert.IsTrue(
                MxLevelAuthoringTargetClassification.TryResolve(
                    LevelAuthoringDefinitionUsage.Movable,
                    out GameplayTargetClassificationComponent movable));
            Assert.AreEqual(
                GameplayTargetIdentityTypes.WorldObject,
                movable.IdentityType);
            Assert.AreEqual(
                GameplayTargetCapabilities.Movable,
                movable.Capabilities);

            Assert.IsTrue(
                MxLevelAuthoringTargetClassification.TryResolve(
                    LevelAuthoringDefinitionUsage.StaticCollider,
                    out GameplayTargetClassificationComponent collider));
            Assert.AreEqual(
                GameplayTargetIdentityTypes.WorldObject,
                collider.IdentityType);
            Assert.AreEqual(
                GameplayTargetCapabilities.Static,
                collider.Capabilities);

            Assert.IsTrue(
                MxLevelAuthoringTargetClassification.TryResolve(
                    LevelAuthoringDefinitionUsage.Point,
                    out GameplayTargetClassificationComponent point));
            Assert.AreEqual(
                GameplayTargetIdentityTypes.SpatialPoint,
                point.IdentityType);

            Assert.IsTrue(
                MxLevelAuthoringTargetClassification.TryResolve(
                    LevelAuthoringDefinitionUsage.Region,
                    out GameplayTargetClassificationComponent region));
            Assert.AreEqual(
                GameplayTargetIdentityTypes.SpatialRegion,
                region.IdentityType);

            Assert.IsFalse(
                MxLevelAuthoringTargetClassification.TryResolve(
                    LevelAuthoringDefinitionUsage.Unknown,
                    out _));
        }

        [TearDown]
        public void TearDown()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        [Test]
        public void ChinesePresentation_CoversBuiltInDefinitionsAndEditorEnums()
        {
            var expectedDefinitions = new Dictionary<int, string>
            {
                { CharacterTestWorldObjectDefinitionIds.MovableSlickLight, "轻型滑动物" },
                { CharacterTestWorldObjectDefinitionIds.MovableRoughHeavy, "粗糙重物" },
                { CharacterTestWorldObjectDefinitionIds.MovableElasticSeed, "弹性种子" },
                { CharacterTestWorldObjectDefinitionIds.MovableContactA, "接触物 A" },
                { CharacterTestWorldObjectDefinitionIds.MovableContactB, "接触物 B" },
                { CharacterTestWorldObjectDefinitionIds.MovableSphere, "球体" },
                { CharacterTestWorldObjectDefinitionIds.MovableCapsule, "胶囊体" },
                { CharacterTestWorldObjectDefinitionIds.StaticDefaultBoundary, "默认边界" },
                { CharacterTestWorldObjectDefinitionIds.StaticRoughBoundary, "粗糙边界" },
                { CharacterTestWorldObjectDefinitionIds.StaticHardSlickSurface, "硬滑表面" },
                { CharacterTestWorldObjectDefinitionIds.StaticRoughSurface, "粗糙表面" },
                { CharacterTestWorldObjectDefinitionIds.StaticElasticSurface, "弹性表面" },
                { CharacterTestWorldObjectDefinitionIds.StaticBouncePadSurface, "弹跳垫表面" },
                { CharacterTestWorldObjectDefinitionIds.StaticSphereProbe, "静态球体探针" },
                { CharacterTestWorldObjectDefinitionIds.StaticCapsuleProbe, "静态胶囊体探针" },
                { CharacterTestWorldObjectDefinitionIds.PointPlayerSpawn, "玩家出生点" },
                { CharacterTestWorldObjectDefinitionIds.PointAiSpawn, "Runtime AI Planner 出生点" },
                { CharacterTestWorldObjectDefinitionIds.PointAiSpawnNearby, "附近 Runtime AI Planner 出生点" },
                { CharacterTestWorldObjectDefinitionIds.PointValidationCheckpoint, "验证检查点" },
                { CharacterTestWorldObjectDefinitionIds.PointValidationMarker, "验证标记点" },
                { CharacterTestWorldObjectDefinitionIds.RegionStartArea, "起始区域" },
                { CharacterTestWorldObjectDefinitionIds.RegionTestCourse, "测试路线区域" }
            };

            foreach (KeyValuePair<int, string> pair in expectedDefinitions)
            {
                Assert.AreEqual(
                    pair.Value,
                    CharacterTestLevelEditorChineseText.DefinitionName(pair.Key));
            }

            Assert.AreEqual("可移动物体", CharacterTestLevelEditorChineseText.Role(LevelWorldObjectRole.Movable));
            Assert.AreEqual("静态碰撞体", CharacterTestLevelEditorChineseText.Role(LevelWorldObjectRole.StaticCollider));
            Assert.AreEqual("场景 Transform", CharacterTestLevelEditorChineseText.ScaleSource(LevelWorldObjectScaleSource.SceneTransform));
            Assert.AreEqual("对象定义", CharacterTestLevelEditorChineseText.ScaleSource(LevelWorldObjectScaleSource.Definition));
            Assert.AreEqual("检查点", CharacterTestLevelEditorChineseText.PointKind(LevelWorldObjectPointKind.Checkpoint));
            Assert.AreEqual("触发区域", CharacterTestLevelEditorChineseText.RegionKind(LevelWorldObjectRegionKind.TriggerRegion));
            Assert.AreEqual("AABB 盒体", CharacterTestLevelEditorChineseText.Shape(CharacterTestMovableNaturalObjectShapeKind.Aabb));
            Assert.AreEqual("球体", CharacterTestLevelEditorChineseText.Shape(CharacterTestMovableNaturalObjectShapeKind.Sphere));
            Assert.AreEqual("胶囊体", CharacterTestLevelEditorChineseText.Shape(CharacterTestMovableNaturalObjectShapeKind.Capsule));
            Assert.AreEqual("硬滑材质", CharacterTestLevelEditorChineseText.Material(CombatSurfaceMaterialKind.HardSlick));
            Assert.AreEqual("警告", CharacterTestLevelEditorChineseText.Severity(LevelWorldObjectAuthoringSeverity.Warning));
            Assert.AreEqual("关卡点", CharacterTestLevelEditorChineseText.Usage(LevelAuthoringDefinitionUsage.Point));
            Assert.AreEqual(
                "区域",
                CharacterTestWorldObjectLevelEditorModule.PlacementRoleLabel(
                    CharacterTestWorldObjectLevelEditorModule.PlacementRole.Region));

            foreach (LevelWorldObjectRole value in Enum.GetValues(typeof(LevelWorldObjectRole)))
                Assert.AreNotEqual(value.ToString(), CharacterTestLevelEditorChineseText.Role(value), value.ToString());
            foreach (LevelAuthoringDefinitionUsage value in Enum.GetValues(typeof(LevelAuthoringDefinitionUsage)))
                Assert.AreNotEqual(value.ToString(), CharacterTestLevelEditorChineseText.Usage(value), value.ToString());
            foreach (LevelWorldObjectScaleSource value in Enum.GetValues(typeof(LevelWorldObjectScaleSource)))
                Assert.AreNotEqual(value.ToString(), CharacterTestLevelEditorChineseText.ScaleSource(value), value.ToString());
            foreach (LevelWorldObjectPointKind value in Enum.GetValues(typeof(LevelWorldObjectPointKind)))
                Assert.AreNotEqual(value.ToString(), CharacterTestLevelEditorChineseText.PointKind(value), value.ToString());
            foreach (LevelWorldObjectRegionKind value in Enum.GetValues(typeof(LevelWorldObjectRegionKind)))
                Assert.AreNotEqual(value.ToString(), CharacterTestLevelEditorChineseText.RegionKind(value), value.ToString());
            foreach (CharacterTestMovableNaturalObjectShapeKind value in Enum.GetValues(
                         typeof(CharacterTestMovableNaturalObjectShapeKind)))
            {
                Assert.AreNotEqual(value.ToString(), CharacterTestLevelEditorChineseText.Shape(value), value.ToString());
            }
            foreach (CombatSurfaceMaterialKind value in Enum.GetValues(typeof(CombatSurfaceMaterialKind)))
                Assert.AreNotEqual(value.ToString(), CharacterTestLevelEditorChineseText.Material(value), value.ToString());
            foreach (LevelWorldObjectAuthoringSeverity value in Enum.GetValues(
                         typeof(LevelWorldObjectAuthoringSeverity)))
            {
                Assert.AreNotEqual(value.ToString(), CharacterTestLevelEditorChineseText.Severity(value), value.ToString());
            }
            foreach (CharacterTestWorldObjectLevelEditorModule.PlacementRole value in Enum.GetValues(
                         typeof(CharacterTestWorldObjectLevelEditorModule.PlacementRole)))
            {
                Assert.AreNotEqual(
                    value.ToString(),
                    CharacterTestWorldObjectLevelEditorModule.PlacementRoleLabel(value),
                    value.ToString());
            }
        }

        [Test]
        public void ChinesePresentation_DiagnosticsKeepStableCodePathAndRawMessage()
        {
            var known = new LevelWorldObjectAuthoringIssue(
                LevelWorldObjectAuthoringSeverity.Error,
                "InvalidDefinitionId",
                "movableInstances[0].definitionId",
                "Definition id must be greater than 0.");
            string knownText = CharacterTestLevelEditorChineseText.DiagnosticText(known);
            Assert.That(knownText, Does.Contain("对象定义无效或不存在"));
            Assert.That(knownText, Does.Contain(known.Code));
            Assert.That(knownText, Does.Contain(known.Path));
            Assert.That(
                CharacterTestLevelEditorChineseText.DiagnosticTooltip(known),
                Does.Contain(known.Message));

            var unknown = new LevelWorldObjectAuthoringIssue(
                LevelWorldObjectAuthoringSeverity.Warning,
                "FutureValidationCode",
                "modules[0]",
                "Future raw message.");
            string unknownText = CharacterTestLevelEditorChineseText.DiagnosticText(unknown);
            Assert.That(unknownText, Does.Contain("未翻译的校验问题"));
            Assert.That(unknownText, Does.Contain(unknown.Code));
            Assert.That(unknownText, Does.Contain(unknown.Path));
            Assert.That(unknownText, Does.Contain(unknown.Message));
            Assert.That(
                CharacterTestLevelEditorChineseText.DiagnosticTooltip(unknown),
                Does.Contain(unknown.Message));
        }

        [Test]
        public void ChinesePresentation_DoesNotChangeSerializedModuleOrDefinitionHashes()
        {
            var document = new LevelWorldObjectAuthoringDocument
            {
                LevelId = CharacterTestWorldObjectAuthoringAdapter.DefaultLevelId,
                ScenePath = CharacterTestWorldObjectAuthoringAdapter.DefaultScenePath,
                RootPath = "CharacterTestSceneRoot"
            };
            MxLevelAuthoringDocument envelope =
                CharacterTestWorldObjectAuthoringAdapter.CreateEnvelopeFromDocument(document);
            string jsonBefore = MxLevelAuthoringJson.SaveToJson(envelope);
            string hashBefore = MxLevelAuthoringJson.ComputeContentHash(envelope);
            Assert.IsTrue(envelope.TryGetModule(
                CharacterTestWorldObjectAuthoringAdapter.WorldObjectsModuleId,
                out MxLevelAuthoringModuleDocument serializedModule));
            Assert.AreEqual("CharacterTest World Objects", serializedModule.DisplayName);

            var editorModule = new CharacterTestWorldObjectLevelEditorModule();
            Assert.AreEqual("CharacterTest 世界对象", editorModule.DisplayName);
            Assert.IsTrue(CharacterTestWorldObjectSceneSource.BuiltInCatalog.TryGetDefinition(
                CharacterTestWorldObjectDefinitionIds.MovableSlickLight,
                out CharacterTestWorldObjectDefinition definition));
            int definitionHashBefore = definition.GetHashCode();
            Assert.AreEqual("轻型滑动物", CharacterTestLevelEditorChineseText.DefinitionName(
                definition.DefinitionId,
                definition.DisplayName));

            Assert.AreEqual(jsonBefore, MxLevelAuthoringJson.SaveToJson(envelope));
            Assert.AreEqual(hashBefore, MxLevelAuthoringJson.ComputeContentHash(envelope));
            Assert.AreEqual(definitionHashBefore, definition.GetHashCode());
            Assert.AreEqual("Slick Light", definition.DisplayName);
        }

        private static void AssertPlacementCreated(
            CharacterTestWorldObjectLevelEditorModule.PlacementCreateResult result,
            Transform expectedRoot,
            PrimitiveType? expectedVisualPrimitive)
        {
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Success, result.Status);
            Assert.IsNotNull(result.GameObject);
            Assert.IsTrue(result.GameObject.transform.IsChildOf(expectedRoot));
            Assert.AreEqual(0, result.GameObject.GetComponents<Collider>().Length);
            Assert.Greater(result.GameObject.transform.localScale.x, 0f);
            Assert.Greater(result.GameObject.transform.localScale.y, 0f);
            Assert.Greater(result.GameObject.transform.localScale.z, 0f);
            Assert.IsFalse(string.IsNullOrWhiteSpace(result.RowKey));
            Assert.IsFalse(string.IsNullOrWhiteSpace(result.TreeNodeId));

            if (expectedVisualPrimitive.HasValue)
            {
                Assert.IsNotNull(result.GameObject.GetComponent<MeshFilter>());
                Assert.IsNotNull(result.GameObject.GetComponent<MeshRenderer>());
            }
            else
            {
                Assert.IsNull(result.GameObject.GetComponent<MeshFilter>());
                Assert.IsNull(result.GameObject.GetComponent<MeshRenderer>());
            }
        }

        private static MxLevelEditorListRow FindRowByKey(
            IReadOnlyList<MxLevelEditorListRow> rows,
            string key)
        {
            if (rows == null || string.IsNullOrWhiteSpace(key))
                return null;

            for (int i = 0; i < rows.Count; i++)
            {
                if (string.Equals(rows[i]?.Key, key, System.StringComparison.Ordinal))
                    return rows[i];
            }

            return null;
        }

        private static void DestroyPlacementObject(
            CharacterTestWorldObjectLevelEditorModule.PlacementCreateResult result)
        {
            if (result?.GameObject != null)
                Object.DestroyImmediate(result.GameObject);
        }

        private static GameObject CreateChild(Transform root, string name)
        {
            var child = new GameObject(name);
            child.transform.SetParent(root, worldPositionStays: false);
            return child;
        }

        private static CharacterTestWorldObjectSceneSource FindSceneSourceForTests()
        {
            CharacterTestWorldObjectSceneSource[] sources =
                Object.FindObjectsByType<CharacterTestWorldObjectSceneSource>(UnityEngine.FindObjectsInactive.Exclude);
            return sources.Length > 0 ? sources[0] : null;
        }

        private static GameManager FindGameManagerForTests()
        {
            GameManager[] managers = Object.FindObjectsByType<GameManager>(UnityEngine.FindObjectsInactive.Exclude);
            Assert.AreEqual(1, managers.Length, "CharacterTestScene should contain one GameManager scene entry.");
            return managers[0];
        }

        private static void AssertContainsChoice(PopupField<string> popup, string expected)
        {
            Assert.IsTrue(ContainsChoice(popup, expected), "Expected popup choice: " + expected);
        }

        private static void AssertDoesNotContainChoice(PopupField<string> popup, string unexpected)
        {
            Assert.IsFalse(ContainsChoice(popup, unexpected), "Unexpected popup choice: " + unexpected);
        }

        private static bool ContainsChoice(PopupField<string> popup, string value)
        {
            if (popup?.choices == null)
                return false;

            for (int i = 0; i < popup.choices.Count; i++)
            {
                if (string.Equals(popup.choices[i], value, System.StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private static MxLevelEditorListRow FindRowByDefinition(
            IReadOnlyList<MxLevelEditorListRow> rows,
            string definitionFragment)
        {
            if (rows == null)
                return null;

            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i] != null
                    && rows[i].Definition.IndexOf(
                        definitionFragment,
                        System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return rows[i];
                }
            }

            return null;
        }

        private static void AssertSceneOverlayContainsKind(
            IReadOnlyList<CharacterTestWorldObjectLevelEditorModule.SceneOverlayItem> items,
            string kind)
        {
            Assert.IsNotNull(
                FindSceneOverlayItemByKind(items, kind),
                "Expected Scene overlay item kind: " + kind);
        }

        private static CharacterTestWorldObjectLevelEditorModule.SceneOverlayItem FindSceneOverlayItem(
            IReadOnlyList<CharacterTestWorldObjectLevelEditorModule.SceneOverlayItem> items,
            string rowKey)
        {
            if (items == null)
                return null;

            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] != null
                    && string.Equals(items[i].RowKey, rowKey, System.StringComparison.Ordinal))
                {
                    return items[i];
                }
            }

            return null;
        }

        private static CharacterTestWorldObjectLevelEditorModule.SceneOverlayItem FindSceneOverlayItemByKind(
            IReadOnlyList<CharacterTestWorldObjectLevelEditorModule.SceneOverlayItem> items,
            string kind)
        {
            if (items == null)
                return null;

            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] != null
                    && string.Equals(items[i].Kind, kind, System.StringComparison.Ordinal))
                {
                    return items[i];
                }
            }

            return null;
        }

        private static int CountListViewItems(ListView listView)
        {
            return listView?.itemsSource == null ? 0 : listView.itemsSource.Count;
        }

        private static void SelectWorldObjectsModule(MxLevelEditorWindow window)
        {
            PopupField<string> modulePopup =
                window.rootVisualElement.Q<PopupField<string>>("mx-level-editor-module-popup");
            Assert.IsNotNull(modulePopup);
            modulePopup.value = "世界对象";
        }

        private static void AssertNoTopLevelIntegerField(VisualElement root, string label)
        {
            var fields = new List<IntegerField>();
            CollectVisualElements(root, fields);
            for (int i = 0; i < fields.Count; i++)
            {
                if (fields[i].label != label)
                    continue;

                Foldout parentFoldout = FindAncestor<Foldout>(fields[i], root);
                Assert.IsNotNull(parentFoldout, "Field should be inside Advanced Ids: " + label);
                Assert.AreEqual("character-test-advanced-ids", parentFoldout.name);
            }
        }

        private static T FindAncestor<T>(VisualElement element, VisualElement stopAt)
            where T : VisualElement
        {
            VisualElement current = element == null ? null : element.parent;
            while (current != null && current != stopAt.parent)
            {
                if (current is T typed)
                    return typed;
                if (current == stopAt)
                    return null;
                current = current.parent;
            }

            return null;
        }

        private static void CollectVisualElements<T>(VisualElement root, List<T> output)
            where T : VisualElement
        {
            if (root == null || output == null)
                return;

            if (root is T typed)
                output.Add(typed);
            for (int i = 0; i < root.childCount; i++)
                CollectVisualElements(root[i], output);
        }

        private static int CountRole(
            LevelWorldObjectAuthoringDocument document,
            LevelWorldObjectRole role)
        {
            int count = 0;
            for (int i = 0; i < document.Instances.Count; i++)
            {
                if (document.Instances[i] != null && document.Instances[i].Role == role)
                    count++;
            }

            return count;
        }

        private static string JoinIssues(LevelWorldObjectAuthoringValidationReport report)
        {
            if (report == null || report.Issues.Count == 0)
                return string.Empty;

            var lines = new string[report.Issues.Count];
            for (int i = 0; i < report.Issues.Count; i++)
            {
                LevelWorldObjectAuthoringIssue issue = report.Issues[i];
                lines[i] = issue.Code + " " + issue.Path + " " + issue.Message;
            }

            return string.Join("\n", lines);
        }

        private static string ToFullPath(string assetPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
        }
    }
}
