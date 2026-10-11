using System;
using System.Collections.Generic;
using System.Globalization;
using MxFramework.Editor.CharacterPreview;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Project.Editor.AnimationDesigner
{
    public sealed partial class AnimationDesignerLauncherWindow : EditorWindow
    {
        private CharacterModelPreviewChoice[] _characters = Array.Empty<CharacterModelPreviewChoice>();
        private CharacterModelPreviewLoadout[] _loadouts = Array.Empty<CharacterModelPreviewLoadout>();
        private CharacterModelPreviewClip[] _clips = Array.Empty<CharacterModelPreviewClip>();
        private readonly List<CharacterModelPreviewClip> _filteredClips = new List<CharacterModelPreviewClip>();
        private string _characterKey;
        private string _loadoutKey;
        private AnimationClip _clip;
        private string _missingClip;
        private bool _hideWeapons;
        private DropdownField _characterField;
        private DropdownField _loadoutField;
        private ObjectField _clipField;
        private ListView _clipList;
        private ToolbarSearchField _search;
        private Label _status;
        private Button _apply;
        private Button _clipPreview;
        private Button _rootMotionPreview;
        private Button _saveClip;
        private Label _saveClipStatus;
        private AnimationClip _saveStatusTarget;
        private bool _saveStatusWasDirty;
        private Button _focus;
        private Button _end;
        private string PreferenceKey => "MxFramework.AnimationDesignerLauncher." + Hash128.Compute(Application.dataPath) + ".";
        private CharacterModelPreviewChoice Character => Array.Find(_characters, c => c.Key == _characterKey);

        [MenuItem("MxFramework/Character/Animation Designer Launcher")]
        public static void Open() => GetWindow<AnimationDesignerLauncherWindow>("Animation Designer Launcher").Show();

        [MenuItem("Assets/MxFramework/Edit with Character in Animation Designer", true)]
        private static bool CanOpenClip() => Selection.activeObject is AnimationClip;

        [MenuItem("Assets/MxFramework/Edit with Character in Animation Designer")]
        private static void OpenSelectedClip()
        {
            var selected = Selection.activeObject as AnimationClip;
            var window = GetWindow<AnimationDesignerLauncherWindow>("Animation Designer Launcher");
            window.SelectClip(selected);
            window.Show();
        }

        private void OnEnable()
        {
            minSize = new Vector2(440, 600);
            if (!EditorPrefs.GetBool(PreferenceKey + "windowInitialized", false))
            {
                Rect main = EditorGUIUtility.GetMainWindowPosition();
                position = new Rect(main.center.x - 270, main.center.y - 300, 540, 600);
                EditorPrefs.SetBool(PreferenceKey + "windowInitialized", true);
            }
            _characterKey = EditorPrefs.GetString(PreferenceKey + "character", string.Empty);
            _hideWeapons = EditorPrefs.GetBool(PreferenceKey + "hideWeapons", false);
            RestoreClip();
            AnimationDesignerSession.Changed += RefreshStatus;
            AnimationClipReplacementService.Changed += RefreshStatus;
        }

        private void OnDisable()
        {
            AnimationDesignerSession.Changed -= RefreshStatus;
            AnimationClipReplacementService.Changed -= RefreshStatus;
        }

        public void CreateGUI()
        {
            rootVisualElement.Clear();
            rootVisualElement.style.paddingLeft = 14; rootVisualElement.style.paddingRight = 14;
            rootVisualElement.style.paddingTop = 12; rootVisualElement.style.paddingBottom = 12;
            var title = new Label("临时角色 · 动画编辑");
            title.style.fontSize = 18; title.style.unityFontStyleAndWeight = FontStyle.Bold;
            rootVisualElement.Add(title);
            var hint = new Label("选择角色、装备和片段，一键进入 Animation Designer。");
            hint.style.marginTop = 4; hint.style.marginBottom = 14; hint.style.whiteSpace = WhiteSpace.Normal;
            rootVisualElement.Add(hint);
            _characterField = new DropdownField("角色");
            _loadoutField = new DropdownField("装备组合");
            rootVisualElement.Add(_characterField); rootVisualElement.Add(_loadoutField);
            var hide = new Toggle("隐藏武器") { value = _hideWeapons, tooltip = "仅隐藏本次预览的武器，不改变角色装备。" };
            rootVisualElement.Add(hide);
            hide.RegisterValueChangedCallback(e => { _hideWeapons = e.newValue; EditorPrefs.SetBool(PreferenceKey + "hideWeapons", _hideWeapons); RefreshStatus(); });
            _characterField.RegisterValueChangedCallback(e =>
            {
                int index = Array.FindIndex(_characters, c => c.Label == e.newValue);
                if (index < 0) return;
                _characterKey = _characters[index].Key;
                EditorPrefs.SetString(PreferenceKey + "character", _characterKey);
                LoadCharacterContent();
            });
            _loadoutField.RegisterValueChangedCallback(e =>
            {
                int index = Array.FindIndex(_loadouts, c => c.Label == e.newValue);
                if (index < 0) return;
                _loadoutKey = _loadouts[index].Key;
                EditorPrefs.SetString(PreferenceKey + "equipment." + _characterKey, _loadoutKey);
                RefreshStatus();
            });
            _clipField = new ObjectField("动画片段") { objectType = typeof(AnimationClip), allowSceneObjects = false, value = _clip };
            _clipField.style.marginTop = 12;
            _clipField.RegisterValueChangedCallback(e => SelectClip(e.newValue as AnimationClip));
            rootVisualElement.Add(_clipField);
            _search = new ToolbarSearchField();
            _search.style.marginTop = 8;
            _search.RegisterValueChangedCallback(_ => FilterClips());
            rootVisualElement.Add(_search);
            _clipList = new ListView(_filteredClips, 25, () => new Label(), (element, index) =>
            {
                var label = (Label)element;
                label.text = _filteredClips[index].Label;
                label.tooltip = _filteredClips[index].Label;
            }) { selectionType = SelectionType.Single };
            _clipList.style.flexGrow = 1; _clipList.style.minHeight = 90;
            _clipList.style.flexBasis = 0; _clipList.style.flexShrink = 1;
            _clipList.selectionChanged += selected =>
            {
                foreach (object item in selected) if (item is CharacterModelPreviewClip choice) SelectClip(choice.Clip);
            };
            rootVisualElement.Add(_clipList);
            var previewActions = new VisualElement(); previewActions.style.flexDirection = FlexDirection.Row;
            previewActions.style.height = 32; previewActions.style.marginTop = 8;
            _rootMotionPreview = new Button(PreviewRootMotion) { text = "根运动曲线预览" };
            _rootMotionPreview.tooltip = "打开临时角色实时预览，并在 Animation 窗口选中 Root T.x/y/z 曲线；不修改曲线值。";
            _rootMotionPreview.style.flexGrow = 1;
            _clipPreview = new Button(PreviewSelectedClip) { text = "普通片段预览" };
            _clipPreview.tooltip = "使用临时角色和装备预览所选片段；Animation 窗口负责播放、拖动和曲线编辑。";
            _clipPreview.style.flexGrow = 1;
            _saveClip = new Button(SaveCurrentAnimation) { text = "保存当前动画" };
            _saveClip.style.width = 120;
            previewActions.Add(_rootMotionPreview); previewActions.Add(_clipPreview); previewActions.Add(_saveClip);
            rootVisualElement.Add(previewActions);
            _saveClipStatus = new Label(); _saveClipStatus.style.whiteSpace = WhiteSpace.Normal;
            _saveClipStatus.style.marginTop = 4;
            rootVisualElement.Add(_saveClipStatus);
            rootVisualElement.schedule.Execute(RefreshSaveStatus).Every(500);
            BuildReplacementControls();
            var buttons = new VisualElement(); buttons.style.flexDirection = FlexDirection.Row; buttons.style.marginTop = 12;
            buttons.style.height = 32;
            _apply = new Button(Apply) { text = "开始编辑" }; _apply.style.flexGrow = 1;
            _focus = new Button(AnimationDesignerSession.FocusModel) { text = "聚焦模型" };
            _end = new Button(() => AnimationDesignerSession.End(true)) { text = "结束编辑" };
            buttons.Add(_apply); buttons.Add(_focus); buttons.Add(_end); rootVisualElement.Add(buttons);
            var refresh = new Button(ReloadCatalog) { text = "刷新角色与动画目录", tooltip = "重新读取已导入内容；不会自动替换舞台中的模型。" };
            rootVisualElement.Add(refresh);
            _status = new Label(); _status.style.whiteSpace = WhiteSpace.Normal; _status.style.marginTop = 8;
            rootVisualElement.Add(_status);
            foreach (var element in rootVisualElement.Children())
                if (element != _clipList) element.style.flexShrink = 0;
            ReloadCatalog();
        }

        private void ReloadCatalog()
        {
            _characters = CharacterModelPreviewCatalog.DiscoverCharacters();
            var labels = new List<string>(); foreach (var item in _characters) labels.Add(item.Label);
            _characterField.choices = labels;
            if (string.IsNullOrEmpty(_characterKey) && _characters.Length == 1)
            { _characterKey = _characters[0].Key; EditorPrefs.SetString(PreferenceKey + "character", _characterKey); }
            _characterField.SetValueWithoutNotify(Character?.Label ?? (string.IsNullOrEmpty(_characterKey) ? "请选择角色" : "失效角色：" + _characterKey));
            _characterField.SetEnabled(_characters.Length > 1 || Character == null && _characters.Length > 0);
            LoadCharacterContent();
        }

        private void LoadCharacterContent()
        {
            var character = Character;
            _loadouts = CharacterModelPreviewCatalog.DiscoverLoadouts(character);
            _loadoutKey = EditorPrefs.GetString(PreferenceKey + "equipment." + _characterKey, character?.DefaultLoadoutKey ?? string.Empty);
            var labels = new List<string>(); foreach (var item in _loadouts) labels.Add(item.Label);
            _loadoutField.choices = labels;
            int selected = Array.FindIndex(_loadouts, c => c.Key == _loadoutKey);
            _loadoutField.SetValueWithoutNotify(character == null ? "先选择角色" : selected >= 0 ? _loadouts[selected].Label : "失效装备：" + _loadoutKey);
            _loadoutField.SetEnabled(_loadouts.Length > 1 || selected < 0 && _loadouts.Length > 0);
            _clips = character != null ? CharacterModelPreviewCatalog.DiscoverClips(character) : Array.Empty<CharacterModelPreviewClip>();
            FilterClips(); RefreshStatus();
        }

        private void FilterClips()
        {
            _clipList?.ClearSelection();
            _filteredClips.Clear();
            string search = _search?.value ?? string.Empty;
            foreach (var candidate in _clips)
                if (string.IsNullOrEmpty(search) || candidate.Label.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)
                    _filteredClips.Add(candidate);
            _clipList?.Rebuild();
        }

        private void SelectClip(AnimationClip clip)
        {
            _clip = clip; _missingClip = null;
            _clipField?.SetValueWithoutNotify(clip);
            string guid = string.Empty; long localId = 0;
            if (clip != null) AssetDatabase.TryGetGUIDAndLocalFileIdentifier(clip, out guid, out localId);
            EditorPrefs.SetString(PreferenceKey + "clipGuid", guid);
            EditorPrefs.SetString(PreferenceKey + "clipLocalId", localId.ToString(CultureInfo.InvariantCulture));
            RefreshStatus();
        }

        private void RestoreClip()
        {
            string guid = EditorPrefs.GetString(PreferenceKey + "clipGuid", string.Empty);
            if (string.IsNullOrEmpty(guid)) return;
            long.TryParse(EditorPrefs.GetString(PreferenceKey + "clipLocalId", "0"), out long expected);
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!string.IsNullOrEmpty(path))
                foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
                    if (asset is AnimationClip clip && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(clip, out string _, out long id) && id == expected)
                    { _clip = clip; return; }
            _missingClip = "上次动画片段已失效，请重新选择。";
        }

        private void Apply()
        {
            AnimationDesignerSession.Apply(Character, _loadoutKey, _hideWeapons, _clip);
            RefreshStatus();
        }

        private void PreviewSelectedClip()
        {
            AnimationDesignerSession.BeginClipPreview(Character, _loadoutKey, _hideWeapons, _clip);
            RefreshStatus();
        }

        private void PreviewRootMotion()
        {
            AnimationDesignerSession.BeginRootMotionPreview(Character, _loadoutKey, _hideWeapons, _clip);
            RefreshStatus();
        }

        private void SaveCurrentAnimation()
        {
            AnimationClip target = AnimationDesignerSession.PreviewClip ?? _clip;
            string result = AnimationDesignerClipSave.Save(target);
            RefreshSaveStatus();
            _saveClipStatus.text = result;
            ReloadReplacementHistory();
        }

        private void RefreshSaveStatus()
        {
            if (_saveClip == null) return;
            AnimationClip target = AnimationDesignerSession.PreviewClip ?? _clip;
            bool valid = AnimationDesignerClipSave.CanSave(target);
            bool dirty = valid && EditorUtility.IsDirty(target);
            if (_saveStatusTarget != target)
            {
                _saveStatusTarget = target;
                _saveClipStatus.text = valid ? "仅保存这一段 .anim；不会保存场景、其他资源或创建 Git 提交。"
                    : "请选择独立的 .anim；FBX 子片段需要先导出。";
                _saveStatusWasDirty = dirty;
            }
            else if (dirty != _saveStatusWasDirty)
            {
                _saveStatusWasDirty = dirty;
                _saveClipStatus.text = dirty ? "当前动画有未保存修改；点击“保存当前动画”写入磁盘。" : "当前动画已保存。";
            }
            _saveClip.text = !valid ? "无法保存片段" : dirty ? "保存当前动画" : "动画已保存";
            _saveClip.tooltip = valid ? AssetDatabase.GetAssetPath(target) : "只支持 Assets 下独立的 .anim。";
            _saveClip.SetEnabled(dirty && !AnimationClipReplacementService.Busy
                && !EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling);
        }

        private void RefreshStatus()
        {
            if (_status == null) return;
            bool active = AnimationDesignerSession.Active;
            bool nativePreview = AnimationDesignerSession.IsClipPreviewActive;
            _apply.text = active && !nativePreview ? "应用选择" : "开始编辑";
            bool valid = Character != null && Array.Exists(_loadouts, c => c.Key == _loadoutKey) && _clip != null;
            _apply.SetEnabled(valid && !EditorApplication.isPlayingOrWillChangePlaymode);
            _clipPreview.SetEnabled(valid && !EditorApplication.isPlayingOrWillChangePlaymode);
            _rootMotionPreview.SetEnabled(valid && !EditorApplication.isPlayingOrWillChangePlaymode);
            _clipPreview.text = nativePreview ? "重新预览片段" : "普通片段预览";
            _focus.SetEnabled(active); _end.SetEnabled(active);
            _end.text = nativePreview ? "结束预览" : "结束编辑";
            _status.text = _missingClip ?? (_characters.Length == 0 ? "没有可用的已导入角色，请先完成角色 Unity 导入。" :
                Character == null ? "请选择角色；失效选择不会自动替换。" :
                !Array.Exists(_loadouts, c => c.Key == _loadoutKey) ? "当前装备组合已失效，请重新选择。" : AnimationDesignerSession.Status);
            RefreshSaveStatus();
            RefreshReplacementStatus();
        }
    }
}
