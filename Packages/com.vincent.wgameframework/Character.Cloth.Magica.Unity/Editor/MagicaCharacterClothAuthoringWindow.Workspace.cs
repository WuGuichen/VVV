using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace MxFramework.CharacterCloth.Magica.Unity.Editor
{
    public sealed partial class MagicaCharacterClothAuthoringWindow
    {
        private Vector2 _clothListScroll;
        private Vector2 _rootListScroll;
        private Vector2 _colliderListScroll;

        private void DrawUnifiedWorkspace()
        {
            EditorGUILayout.Space(4f);
            using (new EditorGUILayout.HorizontalScope(GUILayout.ExpandHeight(true)))
            {
                DrawObjectNavigator();
                DrawWorkspaceContent();
            }
        }

        private void DrawObjectNavigator()
        {
            using (new EditorGUILayout.VerticalScope(
                       _panelStyle,
                       GUILayout.Width(370f),
                       GUILayout.ExpandHeight(true)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label("对象", _sectionTitleStyle);
                    GUILayout.FlexibleSpace();
                    bool showBones = GUILayout.Toggle(_showBoneOverlay, "显示骨骼", GUILayout.Width(76f));
                    if (showBones != _showBoneOverlay)
                    {
                        _showBoneOverlay = showBones;
                        if (!showBones) _bonePickMode = MagicaCharacterClothBonePickMode.None;
                        SceneView.RepaintAll();
                    }
                }

                _clothSearch = EditorGUILayout.TextField(_clothSearch, EditorStyles.toolbarSearchField);
                MagicaCharacterClothAuthoringEntry[] cloths =
                    _session.document?.cloths ?? Array.Empty<MagicaCharacterClothAuthoringEntry>();

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label("Cloth", EditorStyles.miniBoldLabel);
                    GUILayout.FlexibleSpace();
                    GUILayout.Label(cloths.Length.ToString(), EditorStyles.miniLabel);
                }
                _clothListScroll = EditorGUILayout.BeginScrollView(
                    _clothListScroll,
                    GUIStyle.none,
                    GUI.skin.verticalScrollbar,
                    GUILayout.Height(110f));
                for (int i = 0; i < cloths.Length; i++)
                {
                    MagicaCharacterClothAuthoringEntry cloth = cloths[i];
                    string title = string.IsNullOrWhiteSpace(cloth?.id) ? "<未命名>" : cloth.id;
                    if (!string.IsNullOrWhiteSpace(_clothSearch)
                        && title.IndexOf(_clothSearch, StringComparison.OrdinalIgnoreCase) < 0)
                        continue;
                    string preset = PresetDisplayName(cloth?.presetAssetPath);
                    int roots = cloth?.rootBonePaths?.Length ?? 0;
                    bool selectedCloth = _clothIndex == i;
                    using (BackgroundColor(selectedCloth ? new Color(0.20f, 0.43f, 0.68f) : GUI.backgroundColor))
                    {
                        string presetLabel = string.IsNullOrWhiteSpace(preset) ? "未选择 Preset" : preset;
                        string label = $"<b>{title}</b>\n<size=10>BoneCloth  ·  {roots} Root  ·  {presetLabel}</size>";
                        if (GUILayout.Button(label, selectedCloth ? _selectedObjectRowStyle : _objectRowStyle))
                        {
                            _clothIndex = i;
                            _rootIndex = 0;
                            _colliderIndex = 0;
                            _workspaceMode = 0;
                            Validate();
                            SceneView.RepaintAll();
                        }
                    }
                }
                EditorGUILayout.EndScrollView();

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("＋ 新建", GUILayout.Height(24f))) AddCloth();
                    using (new EditorGUI.DisabledScope(SelectedCloth() == null))
                    {
                        if (GUILayout.Button("复制", GUILayout.Height(24f))) DuplicateCloth();
                        using (BackgroundColor(new Color(0.72f, 0.32f, 0.30f)))
                            if (GUILayout.Button("删除", GUILayout.Height(24f))) DeleteCloth();
                    }
                }
                if (GUILayout.Button("从 3D 骨骼新建 Cloth", GUILayout.Height(24f)))
                    StartSceneBonePick(MagicaCharacterClothBonePickMode.CreateCloth);
                if (_session.configExists || cloths.Length > 0)
                {
                    bool hasEmptyColliders = Array.Exists(
                        cloths,
                        value => (value?.colliders?.Length ?? 0) == 0);
                    bool allCollidersEmpty = Array.TrueForAll(
                        cloths,
                        value => (value?.colliders?.Length ?? 0) == 0);
                    string label = !hasEmptyColliders
                        ? "根据模型重新初始化…"
                        : allCollidersEmpty
                            ? "根据模型整理 Cloth + Collider"
                            : "根据模型补全 Collider";
                    using (new EditorGUI.DisabledScope(CurrentBodyModel() == null))
                        if (GUILayout.Button(label, GUILayout.Height(24f)))
                        {
                            if (hasEmptyColliders) FillMissingCollidersFromModelData();
                            else ReinitializeFromModelData();
                        }
                }

                MagicaCharacterClothAuthoringEntry selectedForRoots = SelectedCloth();
                string[] rootPaths = selectedForRoots?.rootBonePaths ?? Array.Empty<string>();
                EditorGUILayout.Space(6f);
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label("骨链", EditorStyles.miniBoldLabel);
                    GUILayout.FlexibleSpace();
                    GUILayout.Label(
                        selectedForRoots == null
                            ? "请先选择 Cloth"
                            : selectedForRoots.id + "  ·  " + rootPaths.Length,
                        EditorStyles.miniLabel);
                }
                _rootListScroll = EditorGUILayout.BeginScrollView(
                    _rootListScroll,
                    GUIStyle.none,
                    GUI.skin.verticalScrollbar,
                    GUILayout.Height(rootPaths.Length > 0 ? 82f : 38f));
                if (selectedForRoots == null)
                    GUILayout.Label("选择一个 Cloth 后查看其中的 Root 骨链。", EditorStyles.centeredGreyMiniLabel);
                else if (rootPaths.Length == 0)
                    GUILayout.Label("当前 Cloth 还没有 Root 骨链。", EditorStyles.centeredGreyMiniLabel);
                for (int rootIndex = 0; rootIndex < rootPaths.Length; rootIndex++)
                {
                    int index = rootIndex;
                    string path = rootPaths[rootIndex];
                    bool selectedRoot = _rootIndex == rootIndex;
                    int nodes = CountRootChainNodes(path);
                    string label = $"<b>{rootIndex + 1:00}  {Leaf(path)}</b>\n<size=10>{nodes} 个骨骼节点  ·  点击选择并聚焦</size>";
                    using (BackgroundColor(selectedRoot ? new Color(0.20f, 0.50f, 0.42f) : GUI.backgroundColor))
                        if (GUILayout.Button(label, selectedRoot ? _selectedObjectRowStyle : _objectRowStyle))
                            SelectRootChain(index, path);
                }
                EditorGUILayout.EndScrollView();

                EditorGUILayout.Space(7f);
                Rect separator = EditorGUILayout.GetControlRect(false, 1f);
                EditorGUI.DrawRect(separator, new Color(0.18f, 0.18f, 0.18f, 0.8f));
                EditorGUILayout.Space(5f);

                MagicaCharacterClothAuthoringEntry selectedClothEntry = SelectedCloth();
                MagicaCharacterClothAuthoringCollider[] colliders =
                    selectedClothEntry?.colliders ?? Array.Empty<MagicaCharacterClothAuthoringCollider>();
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label("Collider", EditorStyles.miniBoldLabel);
                    GUILayout.FlexibleSpace();
                    GUILayout.Label(
                        selectedClothEntry == null
                            ? "请先选择 Cloth"
                            : selectedClothEntry.id + "  ·  " + colliders.Length,
                        EditorStyles.miniLabel);
                }
                _colliderListScroll = EditorGUILayout.BeginScrollView(
                    _colliderListScroll,
                    GUIStyle.none,
                    GUI.skin.verticalScrollbar,
                    GUILayout.MinHeight(92f),
                    GUILayout.ExpandHeight(true));
                if (selectedClothEntry == null)
                {
                    EditorGUILayout.HelpBox("选择一个 Cloth 后，在这里管理它的碰撞体。", MessageType.Info);
                }
                else if (colliders.Length == 0)
                {
                    GUILayout.Label("当前 Cloth 还没有 Collider。", EditorStyles.centeredGreyMiniLabel);
                }
                for (int colliderIndex = 0; colliderIndex < colliders.Length; colliderIndex++)
                {
                    MagicaCharacterClothAuthoringCollider collider = colliders[colliderIndex];
                    bool selected = _colliderIndex == colliderIndex && _workspaceMode == 1;
                    string icon = string.Equals(collider?.type, "Capsule", StringComparison.Ordinal) ? "◇" : "○";
                    string row = $"<b>{icon}  {collider?.id}</b>\n"
                        + $"<size=10>{collider?.type}  ·  {Leaf(collider?.parentBonePath)}</size>";
                    using (BackgroundColor(selected ? new Color(0.18f, 0.42f, 0.67f) : GUI.backgroundColor))
                    {
                        if (GUILayout.Button(row, selected ? _selectedObjectRowStyle : _objectRowStyle))
                        {
                            _colliderIndex = colliderIndex;
                            _workspaceMode = 1;
                            Validate();
                            SceneView.RepaintAll();
                        }
                    }
                }
                EditorGUILayout.EndScrollView();

                using (new EditorGUI.DisabledScope(selectedClothEntry == null))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("＋ Sphere", GUILayout.Height(24f))) AddCollider("Sphere");
                        if (GUILayout.Button("＋ Capsule", GUILayout.Height(24f))) AddCollider("Capsule");
                    }
                    using (new EditorGUI.DisabledScope(colliders.Length == 0))
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            if (GUILayout.Button("复制", GUILayout.Height(24f))) DuplicateCollider();
                            using (BackgroundColor(new Color(0.72f, 0.32f, 0.30f)))
                                if (GUILayout.Button("删除", GUILayout.Height(24f))) DeleteCollider();
                            if (GUILayout.Button("↑", GUILayout.Width(30f), GUILayout.Height(24f))) MoveCollider(-1);
                            if (GUILayout.Button("↓", GUILayout.Width(30f), GUILayout.Height(24f))) MoveCollider(1);
                        }
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    WorkspaceTab("Cloth", 0);
                    WorkspaceTab("Collider", 1);
                    WorkspaceTab("Diagnostics", 2);
                }
            }
        }

        private void DrawWorkspaceContent()
        {
            using (new EditorGUILayout.VerticalScope(
                       _panelStyle,
                       GUILayout.ExpandWidth(true),
                       GUILayout.ExpandHeight(true)))
            {
                switch (_workspaceMode)
                {
                    case 1:
                        DrawColliderWorkspace();
                        break;
                    case 2:
                        _diagnosticsExpanded = true;
                        DrawDiagnostics();
                        break;
                    default:
                        DrawClothWorkspace();
                        break;
                }
            }
        }

        private void SelectRootChain(int index, string path)
        {
            _rootIndex = index;
            _workspaceMode = 0;
            _showBoneOverlay = true;
            _bonePickMode = MagicaCharacterClothBonePickMode.None;
            FocusRootChain(path);
            Repaint();
            SceneView.RepaintAll();
        }

        private int CountRootChainNodes(string path)
        {
            Transform body = CurrentBodyModel();
            Transform root = MagicaCharacterClothAuthoringValidator.FindTransform(body, path, out bool ambiguous);
            return root == null || ambiguous ? 0 : root.GetComponentsInChildren<Transform>(true).Length;
        }

        private void FocusRootChain(string path)
        {
            Transform body = CurrentBodyModel();
            Transform root = MagicaCharacterClothAuthoringValidator.FindTransform(body, path, out bool ambiguous);
            if (root == null || ambiguous || SceneView.lastActiveSceneView == null) return;
            Transform[] skeletonBones = MagicaCharacterClothBoneSceneOverlay.CollectSkeletonBones(body);
            var skeleton = new System.Collections.Generic.HashSet<Transform>(skeletonBones);
            System.Collections.Generic.Dictionary<Transform, Vector3> displayPositions =
                MagicaCharacterClothBoneSceneOverlay.CollectDisplayPositions(body, skeletonBones);
            Transform[] chain = root.GetComponentsInChildren<Transform>(true);
            Bounds bounds = new Bounds(
                MagicaCharacterClothBoneSceneOverlay.DisplayPosition(root, displayPositions),
                Vector3.zero);
            for (int boneIndex = 0; boneIndex < chain.Length; boneIndex++)
            {
                if (!skeleton.Contains(chain[boneIndex])) continue;
                bounds.Encapsulate(
                    MagicaCharacterClothBoneSceneOverlay.DisplayPosition(
                        chain[boneIndex],
                        displayPositions));
            }
            bounds.Expand(Mathf.Max(0.12f, bounds.size.magnitude * 0.35f));
            SceneView.lastActiveSceneView.Frame(bounds, false);
        }

        private void DrawClothWorkspace()
        {
            MagicaCharacterClothAuthoringEntry cloth = SelectedCloth();
            if (cloth == null)
            {
                EditorGUILayout.HelpBox("请选择或创建 Cloth。", MessageType.Info);
                MagicaCharacterClothAuthoringEntry[] cloths =
                    _session.document?.cloths ?? Array.Empty<MagicaCharacterClothAuthoringEntry>();
                if (cloths.Length == 0 && !_session.configExists)
                {
                    EditorGUILayout.Space(12f);
                    using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                    {
                        GUILayout.Label("从当前模型开始", _sectionTitleStyle);
                        EditorGUILayout.LabelField(
                            "扫描 SkinnedMeshRenderer 的骨骼层级和实际顶点权重，排除 WGame 基础骨、胸部骨、单骨链和零权重链，按骨盆、上胸和上臂区域合并 Root，并创建最小防穿模 Collider 草稿。",
                            EditorStyles.wordWrappedLabel);
                        EditorGUILayout.Space(6f);
                        using (new EditorGUI.DisabledScope(CurrentBodyModel() == null))
                        using (BackgroundColor(new Color(0.30f, 0.55f, 0.95f)))
                            if (GUILayout.Button("根据模型数据初始化", _primaryButtonStyle, GUILayout.Height(30f)))
                                InitializeFromModelData();
                        if (CurrentBodyModel() == null)
                            GUILayout.Label("Preview Prefab 尚未打开，暂时无法读取模型数据。", EditorStyles.centeredGreyMiniLabel);
                    }
                }
                return;
            }

            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                bool enabled = _session.document.enabled;
                bool nextEnabled = GUILayout.Toggle(enabled, "整体启用 Cloth", GUILayout.Width(120f));
                if (nextEnabled != enabled)
                    Mutate("Toggle Character Cloth Enabled", () => _session.document.enabled = nextEnabled);
                GUILayout.FlexibleSpace();
                GUILayout.Label(
                    enabled ? "重建后生成 Magica 组件" : "重建后移除 Magica 组件",
                    EditorStyles.miniLabel);
            }
            if (!_session.document.enabled)
                EditorGUILayout.HelpBox(
                    "当前角色 Cloth 已整体停用；重建 Prefab 会得到 0 个 Cloth/Collider。重新初始化会自动恢复启用。",
                    MessageType.Warning);

            DrawPresetSelector(cloth);
            DrawClothParameterOverrides(cloth);
            EditorGUILayout.Space(6f);
            _advancedClothExpanded = EditorGUILayout.BeginFoldoutHeaderGroup(
                _advancedClothExpanded,
                "Cloth 结构与 Preset");
            if (_advancedClothExpanded)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("3D 添加 Root", GUILayout.Width(108f)))
                        StartSceneBonePick(MagicaCharacterClothBonePickMode.AddRoot);
                    if (GUILayout.Button("显示全部蒙皮骨骼", GUILayout.Width(126f)))
                    {
                        _showBoneOverlay = true;
                        _bonePickMode = MagicaCharacterClothBonePickMode.None;
                        FocusBodyInScene();
                    }
                    GUILayout.FlexibleSpace();
                    GUILayout.Label("青色：Cloth Root  ·  琥珀：Collider Parent", EditorStyles.miniLabel);
                }
                AssignText("Cloth Id", cloth.id, value => cloth.id = value, "Edit Cloth Id");
                EditorGUILayout.LabelField("Type", cloth.clothType);
                AssignText("Connection", cloth.connectionMode, value => cloth.connectionMode = value, "Edit Connection Mode");
                DrawRootBones(cloth);
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void InitializeFromModelData()
        {
            if (_session.configExists || (_session.document?.cloths?.Length ?? 0) > 0)
            {
                _session.message = "模型数据初始化仅用于尚未创建配置的空 Cloth 草稿，不会覆盖已有内容。";
                return;
            }

            MagicaCharacterClothModelInitializationResult result =
                MagicaCharacterClothModelInitializer.Create(CurrentBodyModel());
            if (!result.Succeeded)
            {
                _session.message = result.Message;
                Repaint();
                return;
            }

            Mutate("Initialize Character Cloth From Model", () =>
            {
                _session.document.enabled = true;
                _session.document.cloths = result.Cloths;
                _clothIndex = 0;
                _rootIndex = 0;
                _colliderIndex = 0;
                _workspaceMode = result.CreatedColliderCount > 0 ? 1 : 0;
            });
            _showBoneOverlay = true;
            _showOnlySelected = true;
            _session.message = result.Message + " 当前结果仍是未保存草稿。";
            Repaint();
            SceneView.RepaintAll();
        }

        private void FillMissingCollidersFromModelData()
        {
            Transform bodyModel = CurrentBodyModel();
            if (bodyModel == null)
            {
                _session.message = "无法补全 Collider：当前 Preview 中没有 BodyModel。";
                return;
            }

            MagicaCharacterClothAuthoringEntry[] cloths =
                _session.document?.cloths ?? Array.Empty<MagicaCharacterClothAuthoringEntry>();
            bool allCollidersEmpty = cloths.Length > 0 && Array.TrueForAll(
                cloths,
                value => (value?.colliders?.Length ?? 0) == 0);
            if (allCollidersEmpty)
            {
                MagicaCharacterClothModelInitializationResult regrouped =
                    MagicaCharacterClothModelInitializer.Create(bodyModel);
                if (!regrouped.Succeeded)
                {
                    _session.message = regrouped.Message;
                    Repaint();
                    return;
                }
                if (!EditorUtility.DisplayDialog(
                        "根据模型整理 Cloth 与 Collider",
                        $"将当前 {cloths.Length} 个空 Collider Cloth 按身体区域整理为 {regrouped.Cloths.Length} 个 Cloth、{regrouped.CreatedColliderCount} 个 Collider。当前磁盘配置只有在点击保存并重建后才会改变。",
                        "整理为模型建议",
                        "取消"))
                    return;
                Mutate("Regroup Character Cloth And Fill Colliders From Model", () =>
                {
                    _session.document.enabled = true;
                    _session.document.cloths = regrouped.Cloths;
                    _clothIndex = 0;
                    _rootIndex = 0;
                    _colliderIndex = 0;
                    _workspaceMode = 1;
                });
                _showBoneOverlay = true;
                _showOnlySelected = true;
                _session.message = regrouped.Message + " 当前结果仍是未保存草稿。";
                Repaint();
                SceneView.RepaintAll();
                return;
            }

            var additions = new System.Collections.Generic.Dictionary<
                MagicaCharacterClothAuthoringEntry,
                MagicaCharacterClothAuthoringCollider[]>();
            int colliderCount = 0;
            for (int clothIndex = 0; clothIndex < cloths.Length; clothIndex++)
            {
                MagicaCharacterClothAuthoringEntry cloth = cloths[clothIndex];
                if (cloth == null || (cloth.colliders?.Length ?? 0) > 0) continue;
                MagicaCharacterClothAuthoringCollider[] created =
                    MagicaCharacterClothModelInitializer.CreateCollidersForCloth(bodyModel, cloth);
                if (created.Length == 0) continue;
                additions.Add(cloth, created);
                colliderCount += created.Length;
            }
            if (colliderCount == 0)
            {
                _session.message = "模型中没有可补全的 Collider；已有 Collider 不会被覆盖。";
                Repaint();
                return;
            }

            Mutate("Fill Character Cloth Colliders From Model", () =>
            {
                foreach (System.Collections.Generic.KeyValuePair<
                             MagicaCharacterClothAuthoringEntry,
                             MagicaCharacterClothAuthoringCollider[]> addition in additions)
                    addition.Key.colliders = addition.Value;
                _colliderIndex = 0;
                _workspaceMode = 1;
            });
            _showBoneOverlay = true;
            _showOnlySelected = true;
            _session.message = $"已根据模型为 {additions.Count} 个 Cloth 补全 {colliderCount} 个防穿模 Collider；已有 Collider 未修改。当前结果仍是未保存草稿。";
            Repaint();
            SceneView.RepaintAll();
        }

        private void ReinitializeFromModelData()
        {
            Transform bodyModel = CurrentBodyModel();
            if (bodyModel == null)
            {
                _session.message = "无法重新初始化：当前 Preview 中没有 BodyModel。";
                return;
            }

            MagicaCharacterClothModelInitializationResult result =
                MagicaCharacterClothModelInitializer.Create(bodyModel);
            if (!result.Succeeded)
            {
                _session.message = result.Message;
                Repaint();
                return;
            }

            MagicaCharacterClothAuthoringEntry[] current =
                _session.document?.cloths ?? Array.Empty<MagicaCharacterClothAuthoringEntry>();
            int currentColliders = 0;
            for (int clothIndex = 0; clothIndex < current.Length; clothIndex++)
                currentColliders += current[clothIndex]?.colliders?.Length ?? 0;
            if (!EditorUtility.DisplayDialog(
                    "根据模型重新初始化",
                    $"当前草稿：{current.Length} 个 Cloth、{currentColliders} 个 Collider。\n"
                    + $"模型建议：{result.Cloths.Length} 个 Cloth、{result.CreatedColliderCount} 个 Collider。\n\n"
                    + "继续会替换当前内存草稿中的 Root 分组、Preset、参数和 Collider，并可通过 Undo 撤销。磁盘配置只有在点击保存并重建后才会改变。",
                    "重新初始化草稿",
                    "取消"))
                return;

            Mutate("Reinitialize Character Cloth From Model", () =>
            {
                _session.document.enabled = true;
                _session.document.cloths = result.Cloths;
                _clothIndex = 0;
                _rootIndex = 0;
                _colliderIndex = 0;
                _workspaceMode = result.CreatedColliderCount > 0 ? 1 : 0;
            });
            _showBoneOverlay = true;
            _showOnlySelected = true;
            _session.message = result.Message + " 已替换当前内存草稿；保存并重建前不会修改磁盘配置。";
            Repaint();
            SceneView.RepaintAll();
        }

        private void DrawColliderWorkspace()
        {
            MagicaCharacterClothAuthoringEntry cloth = SelectedCloth();
            MagicaCharacterClothAuthoringCollider collider = SelectedCollider();
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("Collider 调校", _sectionTitleStyle);
                GUILayout.FlexibleSpace();
                if (cloth != null && collider != null)
                    GUILayout.Label(cloth.id + " / " + collider.id, EditorStyles.miniButton);
            }
            EditorGUILayout.Space(6f);
            _detailScroll = EditorGUILayout.BeginScrollView(
                _detailScroll,
                GUIStyle.none,
                GUI.skin.verticalScrollbar);
            if (collider == null)
                EditorGUILayout.HelpBox("请选择或新增一个 Collider。", MessageType.Info);
            else
                DrawOptimizedColliderInspector(collider);
            EditorGUILayout.EndScrollView();
        }

        private void StartSceneBonePick(MagicaCharacterClothBonePickMode mode)
        {
            if (CurrentBodyModel() == null)
            {
                _session.message = "请先打开有效 Preview Prefab 或运动预览，再使用 3D 骨骼选择。";
                return;
            }
            if (mode == MagicaCharacterClothBonePickMode.AddRoot && SelectedCloth() == null)
            {
                _session.message = "请先选择一个 Cloth，再从 3D 场景追加 Root Bone。";
                return;
            }
            _showBoneOverlay = true;
            _bonePickMode = mode;
            _session.message = mode == MagicaCharacterClothBonePickMode.CreateCloth
                ? "3D 添加 Cloth：请在 Scene View 点击一个蒙皮骨骼；Esc 取消。"
                : "3D 添加 Root：请在 Scene View 点击一个蒙皮骨骼；Esc 取消。";
            FocusBodyInScene();
        }

        private void CancelSceneBonePick()
        {
            _bonePickMode = MagicaCharacterClothBonePickMode.None;
            _session.message = "已取消 3D 骨骼选择。";
            Repaint();
            SceneView.RepaintAll();
        }

        private void HandleSceneBonePicked(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            MagicaCharacterClothBonePickMode mode = _bonePickMode;
            _bonePickMode = MagicaCharacterClothBonePickMode.None;
            if (mode == MagicaCharacterClothBonePickMode.AddRoot)
            {
                MagicaCharacterClothAuthoringEntry cloth = SelectedCloth();
                if (cloth == null) return;
                string[] existing = cloth.rootBonePaths ?? Array.Empty<string>();
                if (Array.Exists(existing, value => string.Equals(value, path, StringComparison.Ordinal)))
                {
                    _session.message = "该骨骼已经是当前 Cloth 的 Root Bone：" + Leaf(path);
                    return;
                }
                Mutate("Add Cloth Root Bone From Scene", () =>
                {
                    var roots = new System.Collections.Generic.List<string>(existing) { path };
                    cloth.rootBonePaths = roots.ToArray();
                });
                _session.message = "已从 Scene View 添加 Root Bone：" + Leaf(path);
            }
            else if (mode == MagicaCharacterClothBonePickMode.CreateCloth)
            {
                MagicaCharacterClothAuthoringEntry template = SelectedCloth();
                Mutate("Create Character Cloth From Scene Bone", () =>
                {
                    var list = new System.Collections.Generic.List<MagicaCharacterClothAuthoringEntry>(
                        _session.document.cloths ?? Array.Empty<MagicaCharacterClothAuthoringEntry>());
                    MagicaCharacterClothAuthoringEntry created = CreateClothFromSceneBone(
                        template,
                        path,
                        list.ConvertAll(value => value.id));
                    list.Add(created);
                    _session.document.cloths = list.ToArray();
                    _clothIndex = list.Count - 1;
                    _rootIndex = 0;
                    _colliderIndex = 0;
                    _workspaceMode = 0;
                });
                _session.message = template != null
                    ? "已从 Scene View 创建 Cloth，并继承当前 Cloth 的 Preset、参数和 Collider：" + Leaf(path)
                    : "已从 Scene View 创建空 Cloth：" + Leaf(path);
            }
            Repaint();
            SceneView.RepaintAll();
        }

        internal static MagicaCharacterClothAuthoringEntry CreateClothFromSceneBone(
            MagicaCharacterClothAuthoringEntry template,
            string path,
            System.Collections.Generic.List<string> existingIds)
        {
            MagicaCharacterClothAuthoringEntry created;
            if (template != null)
            {
                var cloneDocument = new MagicaCharacterClothAuthoringDocument
                {
                    cloths = new[] { template },
                };
                created = cloneDocument.Clone().cloths[0];
            }
            else
            {
                created = new MagicaCharacterClothAuthoringEntry
                {
                    colliders = Array.Empty<MagicaCharacterClothAuthoringCollider>(),
                };
            }
            created.id = UniqueId(Leaf(path), existingIds ?? new System.Collections.Generic.List<string>());
            created.rootBonePaths = new[] { path };
            return created;
        }

        private void FocusBodyInScene()
        {
            Transform body = CurrentBodyModel();
            if (body == null) return;
            SceneView.FocusWindowIfItsOpen<SceneView>();
            Transform[] bones = MagicaCharacterClothBoneSceneOverlay.CollectSkeletonBones(body);
            if (bones.Length > 0)
            {
                Bounds bounds = new Bounds(bones[0].position, Vector3.zero);
                for (int i = 1; i < bones.Length; i++) bounds.Encapsulate(bones[i].position);
                bounds.Expand(Mathf.Max(0.25f, bounds.size.magnitude * 0.12f));
                SceneView.lastActiveSceneView?.Frame(bounds, false);
            }
            else
            {
                SceneView.lastActiveSceneView?.LookAt(body.position);
            }
            SceneView.RepaintAll();
        }
    }
}
