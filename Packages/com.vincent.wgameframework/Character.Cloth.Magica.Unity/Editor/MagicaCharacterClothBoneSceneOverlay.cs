using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace MxFramework.CharacterCloth.Magica.Unity.Editor
{
    internal enum MagicaCharacterClothBonePickMode
    {
        None,
        CreateCloth,
        AddRoot,
    }

    internal sealed class MagicaCharacterClothBoneSceneOverlay : IDisposable
    {
        private readonly Func<Transform> _bodyModel;
        private readonly Func<MagicaCharacterClothAuthoringDocument> _document;
        private readonly Func<bool> _visible;
        private readonly Func<string> _selectedRootPath;
        private readonly Func<MagicaCharacterClothBonePickMode> _pickMode;
        private readonly Action<string> _picked;
        private readonly Action _cancel;
        private GUIStyle _labelStyle;
        private GUIStyle _panelTitleStyle;
        private string _search = string.Empty;
        private int _filterMode = 1;
        private Vector2 _resultScroll;
        private string _focusedPath = string.Empty;
        private bool _registered;

        internal MagicaCharacterClothBoneSceneOverlay(
            Func<Transform> bodyModel,
            Func<MagicaCharacterClothAuthoringDocument> document,
            Func<bool> visible,
            Func<string> selectedRootPath,
            Func<MagicaCharacterClothBonePickMode> pickMode,
            Action<string> picked,
            Action cancel)
        {
            _bodyModel = bodyModel;
            _document = document;
            _visible = visible;
            _selectedRootPath = selectedRootPath;
            _pickMode = pickMode;
            _picked = picked;
            _cancel = cancel;
        }

        internal void Begin()
        {
            if (_registered) return;
            _registered = true;
            SceneView.duringSceneGui += OnSceneGui;
            SceneView.RepaintAll();
        }

        public void Dispose()
        {
            if (!_registered) return;
            _registered = false;
            SceneView.duringSceneGui -= OnSceneGui;
            SceneView.RepaintAll();
        }

        internal static Transform[] CollectSkeletonBones(Transform bodyModel)
        {
            if (bodyModel == null) return Array.Empty<Transform>();
            var unique = new HashSet<Transform>();
            SkinnedMeshRenderer[] renderers =
                bodyModel.GetComponentsInChildren<SkinnedMeshRenderer>(includeInactive: true);
            for (int i = 0; i < renderers.Length; i++)
            {
                SkinnedMeshRenderer renderer = renderers[i];
                if (renderer == null) continue;
                if (renderer.rootBone != null && renderer.rootBone.IsChildOf(bodyModel))
                {
                    unique.Add(renderer.rootBone);
                    Transform[] hierarchy = renderer.rootBone.GetComponentsInChildren<Transform>(includeInactive: true);
                    for (int j = 0; j < hierarchy.Length; j++)
                        if (IsBoneCandidate(hierarchy[j], bodyModel)) unique.Add(hierarchy[j]);
                }
                Transform[] bones = renderer.bones ?? Array.Empty<Transform>();
                for (int j = 0; j < bones.Length; j++)
                    if (bones[j] != null && bones[j].IsChildOf(bodyModel)) unique.Add(bones[j]);
            }

            var result = new List<Transform>(unique);
            result.Sort((left, right) => string.CompareOrdinal(
                AnimationUtility.CalculateTransformPath(left, bodyModel),
                AnimationUtility.CalculateTransformPath(right, bodyModel)));
            return result.ToArray();
        }

        internal static Transform[] CollectClothBones(
            Transform bodyModel,
            MagicaCharacterClothAuthoringDocument document)
        {
            if (bodyModel == null) return Array.Empty<Transform>();
            var skeleton = new HashSet<Transform>(CollectSkeletonBones(bodyModel));
            var result = new HashSet<Transform>();
            HashSet<string> roots = CollectRootPaths(document);
            foreach (string path in roots)
            {
                Transform root = MagicaCharacterClothAuthoringValidator.FindTransform(
                    bodyModel,
                    path,
                    out bool ambiguous);
                if (root == null || ambiguous) continue;
                Transform[] descendants = root.GetComponentsInChildren<Transform>(true);
                for (int descendantIndex = 0; descendantIndex < descendants.Length; descendantIndex++)
                    if (skeleton.Contains(descendants[descendantIndex])) result.Add(descendants[descendantIndex]);
            }
            var ordered = new List<Transform>(result);
            ordered.Sort((left, right) => string.CompareOrdinal(
                AnimationUtility.CalculateTransformPath(left, bodyModel),
                AnimationUtility.CalculateTransformPath(right, bodyModel)));
            return ordered.ToArray();
        }

        private static Transform[] CollectRootSubtree(Transform bodyModel, string rootPath)
        {
            if (bodyModel == null || string.IsNullOrWhiteSpace(rootPath)) return Array.Empty<Transform>();
            var skeleton = new HashSet<Transform>(CollectSkeletonBones(bodyModel));
            Transform root = MagicaCharacterClothAuthoringValidator.FindTransform(
                bodyModel,
                rootPath,
                out bool ambiguous);
            if (root == null || ambiguous) return Array.Empty<Transform>();
            Transform[] descendants = root.GetComponentsInChildren<Transform>(true);
            var result = new List<Transform>();
            for (int descendantIndex = 0; descendantIndex < descendants.Length; descendantIndex++)
                if (skeleton.Contains(descendants[descendantIndex])) result.Add(descendants[descendantIndex]);
            return result.ToArray();
        }

        private static bool IsBoneCandidate(Transform candidate, Transform bodyModel)
        {
            if (candidate == null || !candidate.IsChildOf(bodyModel)) return false;
            if (candidate.name.StartsWith("MagicaCloth_", StringComparison.Ordinal)
                || candidate.name.StartsWith("MagicaCollider_", StringComparison.Ordinal))
                return false;
            if (candidate.GetComponent<Renderer>() != null) return false;
            return true;
        }

        private void OnSceneGui(SceneView sceneView)
        {
            if (!_visible()) return;
            EnsureStyles();
            Transform body = _bodyModel();
            if (body == null) return;
            Transform[] bones = CollectSkeletonBones(body);
            if (bones.Length == 0) return;
            Dictionary<Transform, Vector3> displayPositions = CollectDisplayPositions(body, bones);

            MagicaCharacterClothBonePickMode mode = _pickMode();
            Event evt = Event.current;
            if (mode != MagicaCharacterClothBonePickMode.None)
            {
                if (evt.type == EventType.Layout) HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
                if (evt.type == EventType.KeyDown && evt.keyCode == KeyCode.Escape)
                {
                    _cancel?.Invoke();
                    evt.Use();
                    return;
                }
            }

            var boneSet = new HashSet<Transform>(bones);
            HashSet<string> roots = CollectRootPaths(_document());
            HashSet<string> colliderParents = CollectColliderPaths(_document());
            var clothBones = new HashSet<Transform>(CollectClothBones(body, _document()));
            string selectedRootPath = Normalize(_selectedRootPath?.Invoke());
            var selectedChainBones = new HashSet<Transform>(CollectRootSubtree(body, selectedRootPath));
            if (!string.IsNullOrWhiteSpace(selectedRootPath)) _focusedPath = selectedRootPath;
            HashSet<Transform> humanoidCore = CollectHumanoidCore(body);
            List<Transform> visibleBones = FilterBones(
                body,
                bones,
                humanoidCore,
                roots,
                colliderParents,
                clothBones);
            CompareFunction previousZTest = Handles.zTest;
            Matrix4x4 previousMatrix = Handles.matrix;
            Handles.zTest = CompareFunction.Always;
            // Scene GUI callbacks share the global Handles state. Collider or other
            // editor tools may leave a local-to-world matrix active, which would
            // transform these already-world-space bone positions a second time.
            Handles.matrix = Matrix4x4.identity;
            for (int i = 0; i < visibleBones.Count; i++)
            {
                Transform bone = visibleBones[i];
                Transform parent = bone.parent;
                while (parent != null && !boneSet.Contains(parent) && parent != body) parent = parent.parent;
                if (parent == null || parent == body) continue;
                Vector3 parentPosition = DisplayPosition(parent, displayPositions);
                Vector3 bonePosition = DisplayPosition(bone, displayPositions);
                using (new Handles.DrawingScope(new Color(0.04f, 0.05f, 0.07f, 0.95f)))
                    Handles.DrawAAPolyLine(7f, parentPosition, bonePosition);
                bool clothBone = clothBones.Contains(bone);
                bool selectedChain = selectedChainBones.Count == 0 || selectedChainBones.Contains(bone);
                using (new Handles.DrawingScope(
                           clothBone && selectedChain
                               ? new Color(0.08f, 1f, 0.62f, 1f)
                               : clothBone
                                    ? new Color(0.08f, 0.55f, 0.42f, 0.28f)
                                : new Color(0.18f, 0.90f, 1f, 0.78f)))
                    Handles.DrawAAPolyLine(clothBone && selectedChain ? 5f : clothBone ? 2.5f : 3f, parentPosition, bonePosition);
            }

            for (int i = 0; i < visibleBones.Count; i++)
            {
                Transform bone = visibleBones[i];
                string path = AnimationUtility.CalculateTransformPath(bone, body).Replace('\\', '/');
                bool root = roots.Contains(path);
                bool clothBone = clothBones.Contains(bone);
                bool selectedChain = selectedChainBones.Count == 0 || selectedChainBones.Contains(bone);
                bool colliderParent = colliderParents.Contains(path);
                bool focused = string.Equals(_focusedPath, path, StringComparison.Ordinal);
                Vector3 bonePosition = DisplayPosition(bone, displayPositions);
                Color color = focused
                    ? new Color(1f, 0.88f, 0.12f, 1f)
                    : root && selectedChain
                        ? new Color(0.05f, 0.95f, 1f, 1f)
                        : clothBone && selectedChain
                            ? new Color(0.08f, 1f, 0.62f, 1f)
                        : clothBone
                            ? new Color(0.08f, 0.55f, 0.42f, 0.32f)
                        : colliderParent
                            ? new Color(1f, 0.52f, 0.06f, 1f)
                            : new Color(0.18f, 0.90f, 1f, 1f);
                float size = HandleUtility.GetHandleSize(bonePosition) * (focused || root ? 0.075f : 0.055f);
                Vector3 facing = sceneView.camera != null ? sceneView.camera.transform.forward : Vector3.forward;
                using (new Handles.DrawingScope(new Color(0.03f, 0.04f, 0.05f, 0.98f)))
                    Handles.DrawSolidDisc(bonePosition, facing, size * 1.18f);
                using (new Handles.DrawingScope(color))
                    Handles.DrawSolidDisc(bonePosition, facing, size * 0.78f);
                if (focused)
                {
                    Vector3 right = sceneView.camera != null ? sceneView.camera.transform.right : Vector3.right;
                    Vector3 up = sceneView.camera != null ? sceneView.camera.transform.up : Vector3.up;
                    using (new Handles.DrawingScope(new Color(1f, 0.88f, 0.08f, 1f)))
                    {
                        Handles.DrawWireDisc(bonePosition, facing, size * 2.2f);
                        Handles.DrawAAPolyLine(3f, bonePosition - right * size * 1.7f, bonePosition + right * size * 1.7f);
                        Handles.DrawAAPolyLine(3f, bonePosition - up * size * 1.7f, bonePosition + up * size * 1.7f);
                    }
                }
                using (new Handles.DrawingScope(color))
                {
                    bool clicked = mode != MagicaCharacterClothBonePickMode.None
                        && Handles.Button(bonePosition, Quaternion.identity, size, size * 1.7f, Handles.CircleHandleCap);
                    if (clicked)
                    {
                        _picked?.Invoke(path);
                        evt.Use();
                        break;
                    }
                }

                Vector2 gui = HandleUtility.WorldToGUIPoint(bonePosition);
                bool nearMouse = Vector2.Distance(gui, evt.mousePosition) < 24f;
                if (focused || nearMouse)
                    Handles.Label(bonePosition + Vector3.up * size * 1.5f, bone.name, _labelStyle);
            }
            Handles.zTest = previousZTest;
            Handles.matrix = previousMatrix;

            DrawOverlayGui(sceneView, body, mode, bones.Length, visibleBones, displayPositions);
        }

        private void DrawOverlayGui(
            SceneView sceneView,
            Transform body,
            MagicaCharacterClothBonePickMode mode,
            int boneCount,
            List<Transform> visibleBones,
            Dictionary<Transform, Vector3> displayPositions)
        {
            Handles.BeginGUI();
            float panelHeight = mode == MagicaCharacterClothBonePickMode.None ? 132f : 286f;
            Rect panel = new Rect(12f, 12f, 390f, panelHeight);
            EditorGUI.DrawRect(panel, new Color(0.075f, 0.085f, 0.105f, 0.96f));
            GUILayout.BeginArea(new Rect(panel.x + 10f, panel.y + 8f, panel.width - 20f, panel.height - 16f));
            GUILayout.Label(
                "Cloth 骨骼选择器  ·  " + visibleBones.Count + " / " + boneCount,
                _panelTitleStyle);
            using (new EditorGUILayout.HorizontalScope())
            {
                _search = EditorGUILayout.TextField(_search, EditorStyles.toolbarSearchField);
                if (GUILayout.Button("×", GUILayout.Width(24f)))
                {
                    _search = string.Empty;
                    GUI.FocusControl(null);
                }
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawFilterButton("全部", 0);
                DrawFilterButton("次级骨", 1);
                DrawFilterButton("未使用", 2);
                GUILayout.FlexibleSpace();
                if (mode != MagicaCharacterClothBonePickMode.None && GUILayout.Button("取消 Esc", GUILayout.Width(72f)))
                    _cancel?.Invoke();
            }
            GUILayout.Label(
                mode == MagicaCharacterClothBonePickMode.CreateCloth
                    ? "先搜索或点击列表聚焦，再点场景骨点或“添加”。"
                    : mode == MagicaCharacterClothBonePickMode.AddRoot
                        ? "选择要追加到当前 Cloth 的 Root Bone。"
                        : "亮青：Cloth Root  ·  绿色：Cloth 模拟骨链  ·  橙色：Collider Parent",
                EditorStyles.miniLabel);
            if (mode != MagicaCharacterClothBonePickMode.None)
            {
                _resultScroll = EditorGUILayout.BeginScrollView(_resultScroll, GUILayout.Height(166f));
                int shown = Mathf.Min(visibleBones.Count, 40);
                for (int i = 0; i < shown; i++)
                {
                    Transform bone = visibleBones[i];
                    string path = AnimationUtility.CalculateTransformPath(bone, body).Replace('\\', '/');
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        bool focused = string.Equals(_focusedPath, path, StringComparison.Ordinal);
                        using (new EditorGUI.DisabledScope(focused))
                            if (GUILayout.Button(bone.name, GUILayout.MinWidth(150f)))
                            {
                                _focusedPath = path;
                                Vector3 bonePosition = DisplayPosition(bone, displayPositions);
                                sceneView.LookAt(bonePosition);
                                sceneView.size = Mathf.Max(0.15f, HandleUtility.GetHandleSize(bonePosition) * 2.5f);
                                SceneView.RepaintAll();
                            }
                        GUILayout.Label(LeafPath(path), EditorStyles.miniLabel, GUILayout.MinWidth(110f));
                        if (GUILayout.Button(mode == MagicaCharacterClothBonePickMode.CreateCloth ? "添加 Cloth" : "添加 Root", GUILayout.Width(78f)))
                            _picked?.Invoke(path);
                    }
                }
                EditorGUILayout.EndScrollView();
            }
            GUILayout.EndArea();
            Handles.EndGUI();
        }

        internal static Dictionary<Transform, Vector3> CollectDisplayPositions(
            Transform bodyModel,
            Transform[] bones)
        {
            var result = new Dictionary<Transform, Vector3>();
            if (EditorApplication.isPlaying)
            {
                for (int boneIndex = 0; boneIndex < bones.Length; boneIndex++)
                    if (bones[boneIndex] != null) result[bones[boneIndex]] = bones[boneIndex].position;
                return result;
            }

            var skeleton = new HashSet<Transform>(bones);
            SkinnedMeshRenderer[] renderers =
                bodyModel.GetComponentsInChildren<SkinnedMeshRenderer>(includeInactive: true);
            for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
            {
                SkinnedMeshRenderer renderer = renderers[rendererIndex];
                Mesh mesh = renderer != null ? renderer.sharedMesh : null;
                Transform[] rendererBones = renderer != null
                    ? renderer.bones ?? Array.Empty<Transform>()
                    : Array.Empty<Transform>();
                Matrix4x4[] bindPoses = mesh != null ? mesh.bindposes : Array.Empty<Matrix4x4>();
                int count = Mathf.Min(rendererBones.Length, bindPoses.Length);
                for (int boneIndex = 0; boneIndex < count; boneIndex++)
                {
                    Transform bone = rendererBones[boneIndex];
                    if (bone == null || !skeleton.Contains(bone) || result.ContainsKey(bone)) continue;
                    Vector3 position = (renderer.transform.localToWorldMatrix * bindPoses[boneIndex].inverse)
                        .MultiplyPoint3x4(Vector3.zero);
                    if (IsFinite(position)) result.Add(bone, position);
                }
            }

            for (int boneIndex = 0; boneIndex < bones.Length; boneIndex++)
            {
                Transform bone = bones[boneIndex];
                if (bone != null && !result.ContainsKey(bone)) result.Add(bone, bone.position);
            }
            return result;
        }

        internal static Vector3 DisplayPosition(
            Transform bone,
            Dictionary<Transform, Vector3> positions) =>
            bone != null && positions.TryGetValue(bone, out Vector3 position)
                ? position
                : bone != null ? bone.position : Vector3.zero;

        private static bool IsFinite(Vector3 value) =>
            !float.IsNaN(value.x) && !float.IsInfinity(value.x)
            && !float.IsNaN(value.y) && !float.IsInfinity(value.y)
            && !float.IsNaN(value.z) && !float.IsInfinity(value.z);

        private void EnsureStyles()
        {
            if (_labelStyle != null) return;
            _labelStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                normal = { textColor = new Color(0.82f, 0.88f, 0.95f, 0.95f) },
                fontSize = 10,
                padding = new RectOffset(3, 3, 1, 1),
            };
            _panelTitleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                normal = { textColor = new Color(0.92f, 0.96f, 1f, 1f) },
                fontSize = 12,
            };
        }

        private void DrawFilterButton(string label, int mode)
        {
            bool selected = _filterMode == mode;
            Color previous = GUI.backgroundColor;
            if (selected) GUI.backgroundColor = new Color(0.20f, 0.55f, 0.85f);
            if (GUILayout.Button(label, EditorStyles.miniButton, GUILayout.Width(58f)))
                _filterMode = mode;
            GUI.backgroundColor = previous;
        }

        private List<Transform> FilterBones(
            Transform body,
            Transform[] bones,
            HashSet<Transform> humanoidCore,
            HashSet<string> roots,
            HashSet<string> colliderParents,
            HashSet<Transform> clothBones)
        {
            var result = new List<Transform>();
            bool searching = !string.IsNullOrWhiteSpace(_search);
            for (int i = 0; i < bones.Length; i++)
            {
                Transform bone = bones[i];
                string path = AnimationUtility.CalculateTransformPath(bone, body).Replace('\\', '/');
                bool matchesSearch = !searching
                    || bone.name.IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0
                    || path.IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0;
                if (!matchesSearch) continue;
                if (searching)
                {
                    result.Add(bone);
                    continue;
                }

                bool used = roots.Contains(path) || colliderParents.Contains(path) || clothBones.Contains(bone);
                bool secondary = !humanoidCore.Contains(bone) && !LooksLikeCoreBone(bone.name);
                bool include = _filterMode == 0
                    || _filterMode == 1 && (secondary || used)
                    || _filterMode == 2 && secondary && !used;
                if (include) result.Add(bone);
            }
            result.Sort((left, right) =>
            {
                string leftPath = AnimationUtility.CalculateTransformPath(left, body).Replace('\\', '/');
                string rightPath = AnimationUtility.CalculateTransformPath(right, body).Replace('\\', '/');
                bool leftFocused = string.Equals(leftPath, _focusedPath, StringComparison.Ordinal);
                bool rightFocused = string.Equals(rightPath, _focusedPath, StringComparison.Ordinal);
                if (leftFocused != rightFocused) return leftFocused ? -1 : 1;
                return string.CompareOrdinal(left.name, right.name);
            });
            return result;
        }

        private static HashSet<Transform> CollectHumanoidCore(Transform body)
        {
            var result = new HashSet<Transform>();
            Animator animator = body.GetComponentInParent<Animator>();
            if (animator == null || !animator.isHuman) return result;
            for (int i = 0; i < (int)HumanBodyBones.LastBone; i++)
            {
                Transform bone = animator.GetBoneTransform((HumanBodyBones)i);
                while (bone != null && bone != body)
                {
                    result.Add(bone);
                    bone = bone.parent;
                }
            }
            return result;
        }

        private static bool LooksLikeCoreBone(string name)
        {
            string value = (name ?? string.Empty).ToLowerInvariant();
            string[] tokens =
            {
                "root", "pelvis", "hip", "spine", "neck", "head", "clavicle", "shoulder",
                "arm", "hand", "finger", "thumb", "leg", "thigh", "calf", "foot", "toe",
            };
            for (int i = 0; i < tokens.Length; i++)
                if (value.Contains(tokens[i])) return true;
            return false;
        }

        private static string LeafPath(string path)
        {
            string normalized = Normalize(path);
            int last = normalized.LastIndexOf('/');
            if (last <= 0) return normalized;
            int previous = normalized.LastIndexOf('/', last - 1);
            return previous >= 0 ? normalized.Substring(previous + 1) : normalized;
        }

        private static HashSet<string> CollectRootPaths(MagicaCharacterClothAuthoringDocument document)
        {
            var result = new HashSet<string>(StringComparer.Ordinal);
            MagicaCharacterClothAuthoringEntry[] cloths = document?.cloths ?? Array.Empty<MagicaCharacterClothAuthoringEntry>();
            for (int i = 0; i < cloths.Length; i++)
            {
                string[] paths = cloths[i]?.rootBonePaths ?? Array.Empty<string>();
                for (int j = 0; j < paths.Length; j++) result.Add(Normalize(paths[j]));
            }
            return result;
        }

        private static HashSet<string> CollectColliderPaths(MagicaCharacterClothAuthoringDocument document)
        {
            var result = new HashSet<string>(StringComparer.Ordinal);
            MagicaCharacterClothAuthoringEntry[] cloths = document?.cloths ?? Array.Empty<MagicaCharacterClothAuthoringEntry>();
            for (int i = 0; i < cloths.Length; i++)
            {
                MagicaCharacterClothAuthoringCollider[] colliders =
                    cloths[i]?.colliders ?? Array.Empty<MagicaCharacterClothAuthoringCollider>();
                for (int j = 0; j < colliders.Length; j++)
                    if (colliders[j] != null) result.Add(Normalize(colliders[j].parentBonePath));
            }
            return result;
        }

        private static string Normalize(string value) =>
            (value ?? string.Empty).Replace('\\', '/').Trim().Trim('/');
    }
}
