using System.Collections.Generic;
using MxFramework.Combat.Physics;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Game.CharacterTest.Editor
{
    public enum CharacterTestCombatLevelSceneLabelMode
    {
        Off = 0,
        Issues = 1,
        Selected = 2,
        All = 3
    }

    [InitializeOnLoad]
    public static class CharacterTestCombatLevelScenePreview
    {
        private const string LabelModeKey =
            "MxFramework.CharacterTest.CombatLevelScenePreview.LabelMode";

        private static readonly List<CharacterTestCombatPhysicsSceneBridge> Bridges =
            new List<CharacterTestCombatPhysicsSceneBridge>(8);

        private static GUIStyle _labelStyle;

        static CharacterTestCombatLevelScenePreview()
        {
            SceneView.duringSceneGui -= OnSceneGui;
            SceneView.duringSceneGui += OnSceneGui;
            Selection.selectionChanged -= Repaint;
            Selection.selectionChanged += Repaint;
        }

        [System.Obsolete("Use LabelMode instead.")]
        public static bool LabelsVisible
        {
            get => LabelMode == CharacterTestCombatLevelSceneLabelMode.All;
            set
            {
                LabelMode = value
                    ? CharacterTestCombatLevelSceneLabelMode.All
                    : CharacterTestCombatLevelSceneLabelMode.Off;
            }
        }

        public static CharacterTestCombatLevelSceneLabelMode LabelMode
        {
            get
            {
                int raw = EditorPrefs.GetInt(
                    LabelModeKey,
                    (int)CharacterTestCombatLevelSceneLabelMode.Off);
                if (raw < (int)CharacterTestCombatLevelSceneLabelMode.Off
                    || raw > (int)CharacterTestCombatLevelSceneLabelMode.All)
                {
                    return CharacterTestCombatLevelSceneLabelMode.Off;
                }

                return (CharacterTestCombatLevelSceneLabelMode)raw;
            }

            set
            {
                if (LabelMode == value)
                {
                    return;
                }

                EditorPrefs.SetInt(LabelModeKey, (int)value);
                Repaint();
            }
        }

        public static void ResetLabelModePreferenceForTests()
        {
            EditorPrefs.DeleteKey(LabelModeKey);
            Repaint();
        }

        public static string CreateShortLabel(
            CharacterTestCombatLevelPreviewCollider collider)
        {
            return collider == null
                ? string.Empty
                : "B"
                + collider.BodyId
                + "/C"
                + collider.ColliderId
                + " "
                + collider.SurfaceMaterialStableId;
        }

        private static void Repaint()
        {
            SceneView.RepaintAll();
        }

        private static void OnSceneGui(SceneView sceneView)
        {
            CollectSelectedBridges(Bridges);
            for (int i = 0; i < Bridges.Count; i++)
            {
                DrawBridgePreview(Bridges[i]);
            }
        }

        private static void CollectSelectedBridges(
            List<CharacterTestCombatPhysicsSceneBridge> bridges)
        {
            bridges.Clear();
            GameObject[] selectedObjects = Selection.gameObjects;
            for (int i = 0; i < selectedObjects.Length; i++)
            {
                GameObject selected = selectedObjects[i];
                if (selected == null)
                {
                    continue;
                }

                CharacterTestCombatPhysicsSceneBridge bridge =
                    selected.GetComponent<CharacterTestCombatPhysicsSceneBridge>();
                if (bridge == null)
                {
                    bridge = selected.GetComponentInParent<CharacterTestCombatPhysicsSceneBridge>();
                }

                if (bridge != null && !bridges.Contains(bridge))
                {
                    bridges.Add(bridge);
                }
            }
        }

        private static void DrawBridgePreview(CharacterTestCombatPhysicsSceneBridge bridge)
        {
            if (bridge == null)
            {
                return;
            }

            CharacterTestCombatLevelPreviewModel model = ResolvePreviewModel(bridge, out GameManager manager);
            CharacterTestCombatLevelSceneLabelMode labelMode = LabelMode;
            HashSet<string> selectedStableIds = labelMode == CharacterTestCombatLevelSceneLabelMode.Selected
                ? CollectSelectedColliderStableIds(bridge, manager)
                : null;
            var corners = new Vector3[8];
            for (int i = 0; i < model.Colliders.Count; i++)
            {
                CharacterTestCombatLevelPreviewCollider collider = model.Colliders[i];
                Color color = CharacterTestCombatLevelPreviewColors.Resolve(collider.SurfaceMaterialKind);
                using (new Handles.DrawingScope(color))
                {
                    if (collider.ShapeKind == CombatPhysicsShapeKind.Obb)
                    {
                        collider.CopyWorldCornersTo(corners);
                        DrawObbEdges(corners);
                    }
                    else
                    {
                        Handles.DrawWireCube(collider.Center, collider.HalfExtents * 2f);
                    }
                    if (ShouldDrawLabel(collider, model, labelMode, selectedStableIds))
                    {
                        Handles.Label(
                            collider.Center + Vector3.up * Mathf.Max(0.25f, collider.HalfExtents.y + 0.15f),
                            CreateShortLabel(collider),
                            GetLabelStyle());
                    }
                }
            }
        }

        internal static CharacterTestCombatLevelPreviewModel BuildModelForBridgeForTests(
            CharacterTestCombatPhysicsSceneBridge bridge) => ResolvePreviewModel(bridge, out _);

        internal static HashSet<string> CollectSelectedStableIdsForTests(
            CharacterTestCombatPhysicsSceneBridge bridge)
        {
            GameManager manager = FindSceneGameManager(bridge);
            return CollectSelectedColliderStableIds(bridge, manager);
        }

        private static CharacterTestCombatLevelPreviewModel ResolvePreviewModel(
            CharacterTestCombatPhysicsSceneBridge bridge,
            out GameManager manager)
        {
            manager = FindSceneGameManager(bridge);
            if (manager != null)
            {
                return CharacterTestCombatLevelPreviewModel.Build(
                    manager.WorldObjectSceneRoot,
                    manager.WorldObjectSceneCache);
            }

            var markers = new List<CharacterTestCombatPhysicsAabbMarker>();
            bridge.GetComponentsInChildren(includeInactive: true, markers);
            return CharacterTestCombatLevelPreviewModel.Build(
                CharacterTestCombatLevelSceneAdapter.BuildLevel(bridge.transform, markers));
        }

        private static void DrawObbEdges(Vector3[] c)
        {
            Draw(0, 1); Draw(0, 2); Draw(0, 4);
            Draw(1, 3); Draw(1, 5);
            Draw(2, 3); Draw(2, 6);
            Draw(3, 7);
            Draw(4, 5); Draw(4, 6);
            Draw(5, 7); Draw(6, 7);
            void Draw(int a, int b) => Handles.DrawLine(c[a], c[b]);
        }

        private static bool ShouldDrawLabel(
            CharacterTestCombatLevelPreviewCollider collider,
            CharacterTestCombatLevelPreviewModel model,
            CharacterTestCombatLevelSceneLabelMode labelMode,
            HashSet<string> selectedStableIds)
        {
            if (labelMode == CharacterTestCombatLevelSceneLabelMode.Off)
            {
                return false;
            }

            if (labelMode == CharacterTestCombatLevelSceneLabelMode.All)
            {
                return true;
            }

            if (labelMode == CharacterTestCombatLevelSceneLabelMode.Selected)
            {
                return collider != null
                    && selectedStableIds != null
                    && selectedStableIds.Contains(collider.StableId);
            }

            return model != null
                && model.HasColliderIssue(collider);
        }

        private static GameManager FindSceneGameManager(CharacterTestCombatPhysicsSceneBridge bridge)
        {
            GameManager[] managers = Object.FindObjectsByType<GameManager>(FindObjectsInactive.Include);
            for (int i = 0; i < managers.Length; i++)
            {
                if (managers[i] != null && managers[i].gameObject.scene.handle == bridge.gameObject.scene.handle)
                    return managers[i];
            }
            return null;
        }

        private static HashSet<string> CollectSelectedColliderStableIds(
            CharacterTestCombatPhysicsSceneBridge bridge,
            GameManager manager)
        {
            var stableIds = new HashSet<string>();
            CharacterTestWorldObjectStaticColliderSpec[] cacheSpecs = manager == null
                ? null
                : manager.WorldObjectSceneCache.CollectStaticColliderSpecs(out _);
            GameObject[] selectedObjects = Selection.gameObjects;
            for (int i = 0; i < selectedObjects.Length; i++)
            {
                GameObject selected = selectedObjects[i];
                if (selected == null)
                {
                    continue;
                }

                if (cacheSpecs != null)
                {
                    for (int j = 0; j < cacheSpecs.Length; j++)
                    {
                        CharacterTestWorldObjectStaticColliderSpec spec = cacheSpecs[j];
                        if (spec.ShapeKind != CharacterTestMovableNaturalObjectShapeKind.Aabb
                            || spec.Transform == null
                            || !IsSameOrChild(selected.transform, spec.Transform))
                        {
                            continue;
                        }

                        stableIds.Add(CharacterTestCombatLevelPreviewModel.ResolveStaticColliderStableId(
                            manager.WorldObjectSceneRoot,
                            spec));
                    }
                }

                CharacterTestCombatPhysicsAabbMarker marker =
                    selected.GetComponent<CharacterTestCombatPhysicsAabbMarker>();
                if (marker == null)
                {
                    marker = selected.GetComponentInParent<CharacterTestCombatPhysicsAabbMarker>();
                }

                if (marker == null
                    || !marker.IsEnabled
                    || marker.GetComponentInParent<CharacterTestCombatPhysicsSceneBridge>() != bridge)
                {
                    continue;
                }

                stableIds.Add(CharacterTestCombatLevelPreviewModel.ResolveMarkerStableId(
                    bridge,
                    marker));
            }

            return stableIds;
        }

        private static bool IsSameOrChild(Transform candidate, Transform root)
        {
            Transform current = candidate;
            while (current != null)
            {
                if (current == root)
                {
                    return true;
                }

                current = current.parent;
            }

            return false;
        }

        private static GUIStyle GetLabelStyle()
        {
            if (_labelStyle != null)
            {
                return _labelStyle;
            }

            _labelStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                normal =
                {
                    textColor = Color.white
                },
                alignment = TextAnchor.MiddleCenter
            };
            return _labelStyle;
        }
    }
}
