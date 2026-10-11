using MxFramework.Combat.Levels;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Game.CharacterTest.Editor
{
    [CustomEditor(typeof(CharacterTestCombatPhysicsSceneBridge))]
    public sealed class CharacterTestCombatPhysicsSceneBridgeEditor : UnityEditor.Editor
    {
        private static bool _showColliders;
        private static bool _showIssues = true;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.Space();

            var bridge = (CharacterTestCombatPhysicsSceneBridge)target;
            CharacterTestCombatLevelPreviewModel model =
                CharacterTestCombatLevelPreviewModel.Build(bridge);
            DrawSummary(model);
            DrawToolbar(model);
            DrawIssues(model);
            DrawColliders(model);
        }

        private static void DrawSummary(CharacterTestCombatLevelPreviewModel model)
        {
            EditorGUILayout.LabelField("Level Preview", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Stable Id", model.LevelStableId);
            EditorGUILayout.LabelField("Hash", model.ContentHash);
            EditorGUILayout.LabelField("AABB Colliders", model.ColliderCount.ToString());
            EditorGUILayout.LabelField("Spawn Points", model.SpawnPointCount.ToString());
            EditorGUILayout.LabelField("spawn.default", FormatVector3(model.DefaultSpawnPosition));
            EditorGUILayout.LabelField(
                "Diagnostics",
                model.ErrorCount + " error(s), " + model.WarningCount + " warning(s)");
            EditorGUILayout.LabelField("Materials", CreateMaterialSummary(model));
        }

        private static void DrawToolbar(CharacterTestCombatLevelPreviewModel model)
        {
            EditorGUILayout.BeginHorizontal();
            CharacterTestCombatLevelScenePreview.LabelMode =
                (CharacterTestCombatLevelSceneLabelMode)EditorGUILayout.EnumPopup(
                    "Scene Labels",
                    CharacterTestCombatLevelScenePreview.LabelMode,
                    GUILayout.MinWidth(200));
            if (GUILayout.Button("Copy Hash"))
            {
                EditorGUIUtility.systemCopyBuffer = model.ContentHash;
            }

            if (GUILayout.Button("Copy JSON Preview"))
            {
                EditorGUIUtility.systemCopyBuffer = model.CreateJsonPreview();
            }

            EditorGUILayout.EndHorizontal();
        }

        private static void DrawIssues(CharacterTestCombatLevelPreviewModel model)
        {
            _showIssues = EditorGUILayout.Foldout(_showIssues, "Diagnostics", true);
            if (!_showIssues)
            {
                return;
            }

            if (model.Issues.Count == 0)
            {
                EditorGUILayout.HelpBox("No level preview diagnostics.", MessageType.Info);
                return;
            }

            for (int i = 0; i < model.Issues.Count; i++)
            {
                CharacterTestCombatLevelPreviewIssue issue = model.Issues[i];
                EditorGUILayout.HelpBox(
                    issue.Severity
                    + " "
                    + issue.Code
                    + " ["
                    + issue.Source
                    + "] "
                    + issue.Path
                    + "\n"
                    + issue.Message,
                    ToMessageType(issue.Severity));
            }
        }

        private static void DrawColliders(CharacterTestCombatLevelPreviewModel model)
        {
            _showColliders = EditorGUILayout.Foldout(
                _showColliders,
                "AABB Colliders ("
                + model.ColliderCount
                + " / "
                + CreateMaterialSummary(model)
                + ")",
                true);
            if (!_showColliders)
            {
                return;
            }

            for (int i = 0; i < model.Colliders.Count; i++)
            {
                CharacterTestCombatLevelPreviewCollider collider = model.Colliders[i];
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField(collider.StableId, EditorStyles.boldLabel);
                EditorGUILayout.LabelField(
                    "Ids",
                    "entity="
                    + collider.EntityId
                    + " body="
                    + collider.BodyId
                    + " collider="
                    + collider.ColliderId);
                EditorGUILayout.LabelField("Layer", collider.Layer.ToString());
                EditorGUILayout.LabelField(
                    "Material",
                    collider.SurfaceMaterialKind
                    + " / "
                    + collider.SurfaceMaterialStableId
                    + " friction="
                    + collider.SurfaceFriction.ToString("0.###")
                    + " elasticity="
                    + collider.SurfaceElasticity.ToString("0.###")
                    + " fixedHeight="
                    + collider.SurfaceFixedBounceHeight.ToString("0.###"));
                EditorGUILayout.LabelField("Position", FormatVector3(collider.Position));
                EditorGUILayout.LabelField("Local Min", FormatVector3(collider.LocalMin));
                EditorGUILayout.LabelField("Local Max", FormatVector3(collider.LocalMax));
                EditorGUILayout.EndVertical();
            }
        }

        private static string CreateMaterialSummary(CharacterTestCombatLevelPreviewModel model)
        {
            int defaultCount = 0;
            int hardSlickCount = 0;
            int roughCount = 0;
            int elasticCount = 0;
            int bouncePadCount = 0;
            for (int i = 0; i < model.Colliders.Count; i++)
            {
                switch (model.Colliders[i].SurfaceMaterialKind)
                {
                    case MxFramework.Combat.Physics.CombatSurfaceMaterialKind.HardSlick:
                        hardSlickCount++;
                        break;
                    case MxFramework.Combat.Physics.CombatSurfaceMaterialKind.Rough:
                        roughCount++;
                        break;
                    case MxFramework.Combat.Physics.CombatSurfaceMaterialKind.Elastic:
                        elasticCount++;
                        break;
                    case MxFramework.Combat.Physics.CombatSurfaceMaterialKind.BouncePad:
                        bouncePadCount++;
                        break;
                    default:
                        defaultCount++;
                        break;
                }
            }

            return "default="
                + defaultCount
                + ", hard_slick="
                + hardSlickCount
                + ", rough="
                + roughCount
                + ", elastic="
                + elasticCount
                + ", bounce_pad="
                + bouncePadCount;
        }

        private static MessageType ToMessageType(CombatLevelValidationSeverity severity)
        {
            switch (severity)
            {
                case CombatLevelValidationSeverity.Error:
                    return MessageType.Error;
                case CombatLevelValidationSeverity.Warning:
                    return MessageType.Warning;
                default:
                    return MessageType.Info;
            }
        }

        private static string FormatVector3(Vector3 value)
        {
            return "("
                + value.x.ToString("0.###")
                + ", "
                + value.y.ToString("0.###")
                + ", "
                + value.z.ToString("0.###")
                + ")";
        }
    }
}
