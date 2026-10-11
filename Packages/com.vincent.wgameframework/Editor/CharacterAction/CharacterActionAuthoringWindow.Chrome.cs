using System;
using System.Collections.Generic;
using MxFramework.CharacterAction;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Editor.CharacterAction
{
    public sealed partial class CharacterActionAuthoringWindow
    {
        private void BuildShell()
        {
            if (_sceneEditMode)
                ExitSceneEditMode(restoreEmbeddedPreview: false);
            DisposeWorkbenchPreviewViewport();
            VisualElement root = rootVisualElement;
            root.Clear();
            root.AddToClassList("ca-window");

            StyleSheet sheet = MxFrameworkPackageAssetLocator.TryResolveAssetPath(
                StyleSheetPackageRelativePath,
                out string styleSheetPath)
                ? AssetDatabase.LoadAssetAtPath<StyleSheet>(styleSheetPath)
                : null;
            if (sheet != null)
                root.styleSheets.Add(sheet);

            _chromeRoot = new VisualElement();
            _chromeRoot.AddToClassList("ca-chrome");
            root.Add(_chromeRoot);

            _contentFrame = new VisualElement();
            _contentFrame.AddToClassList("ca-content-frame");
            root.Add(_contentFrame);
            _contentHost = new VisualElement();
            _contentHost.AddToClassList("ca-content-host");
            _contentHost.AddToClassList("ca-content-host--fixed");
            _contentFrame.Add(_contentHost);

            _footerBar = new VisualElement();
            _footerBar.AddToClassList("ca-footer");
            root.Add(_footerBar);

            BuildWorkbenchShell(_contentHost);
            BuildProblemsWorkbench();
            _shellBuilt = true;
        }

        private void RefreshChrome()
        {
            if (_chromeRoot == null)
                return;

            _chromeRoot.Clear();
            _chromeRoot.Add(BuildWorkbenchBreadcrumbRow());
            _chromeRoot.Add(BuildSourceRow());
        }

        private void RefreshDirtyChrome()
        {
            // Moving an event changes save status, not profile/character selectors.
            bool dirty = IsAnyDirty();
            if (_dirtyStatusLabel != null)
            {
                _dirtyStatusLabel.text = CreateDirtyStatusText();
                _dirtyStatusLabel.EnableInClassList("ca-dirty-label--dirty", dirty);
                _dirtyStatusLabel.EnableInClassList("ca-dirty-label--clean", !dirty);
            }
            _chromeRoot?.Q<Button>(className: "ca-workbench-save-button")
                ?.SetEnabled(IsAnyCharacterActionSourceDirty() || _abilityEffectTimelineDirty);
        }

        private void RefreshActionSelectionChrome()
        {
            if (_chromeRoot == null)
                return;
            _chromeRoot.Q(className: "ca-workbench-breadcrumb")?.RemoveFromHierarchy();
            _chromeRoot.Insert(0, BuildWorkbenchBreadcrumbRow());
            RefreshDirtyChrome();
        }

        private VisualElement BuildWorkbenchBreadcrumbRow()
        {
            var row = new VisualElement();
            row.AddToClassList("ca-workbench-breadcrumb");
            var workspaceMenu = new ToolbarMenu { text = "☰" };
            workspaceMenu.tooltip = "切换工作区";
            workspaceMenu.menu.AppendAction(
                "动作编排",
                _ => SetWorkspace(AuthoringWorkspace.ActionComposition),
                _ => _workspace == AuthoringWorkspace.ActionComposition
                    ? DropdownMenuAction.Status.Checked
                    : DropdownMenuAction.Status.Normal);
            workspaceMenu.menu.AppendAction(
                "映射与方案",
                _ => SetWorkspace(AuthoringWorkspace.Mappings),
                _ => _workspace == AuthoringWorkspace.Mappings
                    ? DropdownMenuAction.Status.Checked
                    : DropdownMenuAction.Status.Normal);
            workspaceMenu.menu.AppendAction(
                "技能编排",
                _ => OpenAbilityEffectTimelineWorkspace(),
                _ => _workspace == AuthoringWorkspace.SharedResources
                    && _sharedResourceSection == SharedResourceSection.AbilityEffectTimeline
                    ? DropdownMenuAction.Status.Checked
                    : DropdownMenuAction.Status.Normal);
            workspaceMenu.menu.AppendAction(
                "共享资源",
                _ => OpenSharedResourcesWorkspace(),
                _ => _workspace == AuthoringWorkspace.SharedResources
                    && _sharedResourceSection != SharedResourceSection.AbilityEffectTimeline
                    ? DropdownMenuAction.Status.Checked
                    : DropdownMenuAction.Status.Normal);
            row.Add(workspaceMenu);
            row.Add(BuildPrimaryAuthoringModeSwitch());
            row.Add(new Label("角色动作配置"));
            row.Add(new Label("/"));
            row.Add(new Label(WorkbenchDisplayName()));
            row.Add(new Label("/"));
            row.Add(new Label(_model?.Profile?.StableId ?? "未选择 Profile"));
            row.Add(new Label("/"));
            row.Add(new Label(_model?.ActionSet?.DisplayName ?? _model?.ActionSet?.StableId ?? "未选择 ActionSet"));
            string actionName = _model?.SelectedAction?.DisplayName;
            if (!string.IsNullOrWhiteSpace(actionName))
            {
                row.Add(new Label("/"));
                row.Add(new Label(actionName));
            }
            return row;
        }

        private VisualElement BuildPrimaryAuthoringModeSwitch()
        {
            var row = new VisualElement { name = "character-action-primary-mode-switch" };
            row.AddToClassList("ca-mode-switch");
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;

            bool editingActions = _workspace == AuthoringWorkspace.ActionComposition;
            var actionButton = new Button(OpenActionCompositionWorkspace)
            {
                name = "character-action-mode-action",
                text = "动作",
                tooltip = "编辑角色动作、轨道和预览",
            };
            actionButton.SetEnabled(!editingActions);
            row.Add(actionButton);

            bool editingAbilityTimeline = _workspace == AuthoringWorkspace.SharedResources
                && _sharedResourceSection == SharedResourceSection.AbilityEffectTimeline;
            var abilityButton = new Button(OpenAbilityEffectTimelineWorkspace)
            {
                name = "character-action-mode-ability",
                text = "技能",
                tooltip = "编辑可复用的技能效果时间线",
            };
            abilityButton.SetEnabled(!editingAbilityTimeline);
            row.Add(abilityButton);
            return row;
        }

        private void OpenActionCompositionWorkspace()
        {
            SetWorkspace(AuthoringWorkspace.ActionComposition);
        }

        private void OpenAbilityEffectTimelineWorkspace()
        {
            SetWorkspace(AuthoringWorkspace.SharedResources);
            SetSharedResourceSection(SharedResourceSection.AbilityEffectTimeline);
        }

        private void OpenSharedResourcesWorkspace()
        {
            MxFramework.Editor.ProjectileAuthoring.ProjectileAuthoringWindow.Open();
        }

        private VisualElement BuildSourceRow()
        {
            var row = new VisualElement
            {
                name = "character-action-toolbar-primary-row",
            };
            row.AddToClassList("ca-toolbar-row");
            row.AddToClassList("ca-toolbar-row--primary");

            var contextGroup = new VisualElement
            {
                name = "character-action-toolbar-context-group",
            };
            contextGroup.AddToClassList("ca-toolbar-context-group");
            contextGroup.Add(_documentSession != null
                ? BuildDocumentProfileToolbarMenu()
                : CreateSourcePopupField(
                    "Profile",
                    _profileSources,
                    _profileSourceDirectory,
                    "未保存 Profile",
                    index => LoadProfileSource(index, confirmDiscard: true),
                    showLabel: false));
            contextGroup.Add(_documentSession != null
                ? IsProfileCompositionOverview()
                    ? BuildDocumentActionSetContextLabel()
                    : BuildDocumentActionSetToolbarMenu()
                : CreateSourcePopupField(
                    "ActionSet",
                    _actionSetSources,
                    _actionSetSourceDirectory,
                    "未保存 ActionSet",
                    index => LoadActionSetSource(index, confirmDiscard: true),
                    showLabel: false));
            row.Add(contextGroup);

            var bindingGroup = new VisualElement
            {
                name = "character-action-toolbar-binding-group",
            };
            bindingGroup.AddToClassList("ca-toolbar-binding-group");
            var bindingScope = new Label("当前方案绑定")
            {
                name = "character-action-toolbar-binding-scope",
                tooltip = "角色和装备状态属于当前 ActionSet；修改后会产生 ActionSet 草稿。",
            };
            bindingScope.AddToClassList("ca-toolbar-binding-scope");
            bindingGroup.Add(bindingScope);
            if (TryFindMatchingCharacterProfileSource(
                    _model?.ActionSet?.CharacterStableId,
                    out int matchingProfileSourceIndex,
                    out string matchingProfileId)
                && !IsCurrentProfileForCharacter(_model?.ActionSet?.CharacterStableId))
            {
                int capturedSourceIndex = matchingProfileSourceIndex;
                var switchProfile = new Button(() =>
                    LoadProfileSource(capturedSourceIndex, confirmDiscard: true))
                {
                    name = "character-action-switch-matching-profile",
                    text = "切换到 " + matchingProfileId,
                    tooltip = "当前预览角色与 CharacterAction Profile 不匹配。切换前会处理所有未保存修改。",
                };
                switchProfile.AddToClassList("ca-toolbar-binding-scope");
                bindingGroup.Add(switchProfile);
            }
            bindingGroup.Add(BuildPreviewBindingToolbarMenu(
                "角色",
                CharacterActionPreviewBindingDiscovery.DiscoverCharacters(),
                CharacterActionAuthoringSearchCategory.Character,
                _model?.ActionSet?.CharacterStableId,
                value =>
                {
                    if (_model?.ActionSet == null)
                        return;
                    _model.ActionSet.CharacterStableId = value;
                    _model.ActionSet.EquipmentStateStableId = string.Empty;
                    _model.ReferenceContext.AddCharacterStableId(value);
                    CharacterActionPreviewBindingDiscovery.AddGeneratedReferences(_model.ReferenceContext);
                }));
            bindingGroup.Add(BuildPreviewBindingToolbarMenu(
                "装备",
                CharacterActionPreviewBindingDiscovery.DiscoverEquipmentStates(
                    _model?.ActionSet?.CharacterStableId ?? string.Empty),
                CharacterActionAuthoringSearchCategory.EquipmentState,
                _model?.ActionSet?.EquipmentStateStableId,
                value =>
                {
                    if (_model?.ActionSet == null)
                        return;
                    _model.ActionSet.EquipmentStateStableId = value;
                    _model.ReferenceContext.AddEquipmentStateStableId(value);
                    CharacterActionPreviewBindingDiscovery.AddGeneratedReferences(_model.ReferenceContext);
                }));
            row.Add(bindingGroup);

            row.Add(new Button(UndoEdit)
            {
                text = "↶",
                tooltip = "撤销"
            });
            row.Add(new Button(RedoEdit)
            {
                text = "↷",
                tooltip = "重做"
            });
            row.Add(CreateToolbarSpacer());

            row.Add(new Button(ToggleSceneEditMode)
            {
                text = _sceneEditMode ? "退出场景编辑" : "场景编辑",
                tooltip = "在 SceneView 中预览动作轨迹与命中体；投射物 Definition、Spawn、Presentation 请在 Projectile Authoring 编辑"
            });
            row.Add(new Button(RunWorkbenchValidation) { text = "校验" });
            var save = new Button(SaveAllDirtyCharacterActionSources) { text = "保存" };
            save.SetEnabled(IsAnyCharacterActionSourceDirty() || _abilityEffectTimelineDirty);
            save.AddToClassList("ca-workbench-save-button");
            row.Add(save);

            _dirtyStatusLabel = new Label(CreateDirtyStatusText());
            _dirtyStatusLabel.AddToClassList("ca-dirty-label");
            _dirtyStatusLabel.EnableInClassList("ca-dirty-label--dirty", IsAnyDirty());
            _dirtyStatusLabel.EnableInClassList("ca-dirty-label--clean", !IsAnyDirty());
            row.Add(_dirtyStatusLabel);

            var hotReload = new Button(HotReloadCharacterActions)
            {
                name = "character-action-hot-reload",
                text = "热重载",
                tooltip = EditorApplication.isPlaying
                    ? "保存当前修改，并将动作配置重载到正在运行的 CharacterTest。活动动作会终止，需要重新触发。"
                    : "进入 CharacterTest Play Mode 后可热重载动作配置。",
            };
            hotReload.SetEnabled(EditorApplication.isPlaying);
            hotReload.AddToClassList("ca-workbench-hot-reload-button");
            row.Add(hotReload);
            row.Add(CreateFileMenu());

            return row;
        }

        private ToolbarMenu BuildPreviewBindingToolbarMenu(
            string caption,
            CharacterActionPreviewBindingChoice[] choices,
            CharacterActionAuthoringSearchCategory category,
            string currentValue,
            Action<string> setter)
        {
            choices = choices ?? Array.Empty<CharacterActionPreviewBindingChoice>();
            string label = currentValue ?? string.Empty;
            for (int i = 0; i < choices.Length; i++)
            {
                if (string.Equals(choices[i].Value, currentValue, StringComparison.Ordinal))
                {
                    label = choices[i].Label;
                    break;
                }
            }
            var menu = new ToolbarMenu
            {
                name = "character-action-toolbar-binding-" + category,
                text = caption + "  " + (string.IsNullOrWhiteSpace(currentValue) ? "未选择" : currentValue)
            };
            menu.AddToClassList("ca-toolbar-binding-menu");
            menu.tooltip = caption + "属于当前 ActionSet。选择其他值会修改当前方案，并进入 Undo/dirty。"
                + (string.IsNullOrWhiteSpace(label) ? string.Empty : "\n当前项：" + label)
                + (string.IsNullOrWhiteSpace(currentValue) ? string.Empty : "\nStable Id：" + currentValue);
            CharacterActionAuthoringSearchCatalog catalog = category == CharacterActionAuthoringSearchCategory.Character
                ? CharacterActionAuthoringSearchCatalogBuilder.BuildCharacters(choices, _model?.ReferenceContext)
                : CharacterActionAuthoringSearchCatalogBuilder.BuildEquipmentStates(choices, _model?.ReferenceContext);
            menu.menu.AppendAction(
                "搜索选择…",
                _ => OpenSearchPicker(
                    menu,
                    "选择" + caption,
                    catalog,
                    category,
                    currentValue,
                    allowClear: true,
                    selected => ApplyPreviewBindingSelection(category, currentValue, selected, setter)),
                DropdownMenuAction.AlwaysEnabled);
            menu.menu.AppendSeparator();
            for (int i = 0; i < choices.Length; i++)
            {
                CharacterActionPreviewBindingChoice choice = choices[i];
                menu.menu.AppendAction(
                    choice.Label,
                    _ => ApplyPreviewBindingSelection(category, currentValue, choice.Value, setter),
                    _ => string.Equals(choice.Value, currentValue, StringComparison.Ordinal)
                        ? DropdownMenuAction.Status.Checked
                        : DropdownMenuAction.Status.Normal);
            }
            if (choices.Length == 0 && !string.IsNullOrWhiteSpace(currentValue))
            {
                menu.menu.AppendAction(
                    currentValue + "（未知引用，保留）",
                    _ => { },
                    DropdownMenuAction.Status.Checked);
            }
            return menu;
        }

        private void ApplyPreviewBindingSelection(
            CharacterActionAuthoringSearchCategory category,
            string currentValue,
            string selectedValue,
            Action<string> setter)
        {
            if (string.Equals(currentValue, selectedValue, StringComparison.Ordinal))
                return;
            // Profile navigation must not run inside a field edit: that would mark
            // the newly loaded ActionSet dirty, even when the switch was cancelled.
            if (category == CharacterActionAuthoringSearchCategory.Character
                && TrySwitchToCharacterProfile(selectedValue))
                return;
            ApplyActionSetDocumentChange(() => setter(selectedValue), UiRefreshScope.Content);
        }

        private bool TrySwitchToCharacterProfile(string characterStableId)
        {
            if (string.IsNullOrWhiteSpace(characterStableId)
                || IsCurrentProfileForCharacter(characterStableId)
                || !TryFindMatchingCharacterProfileSource(
                    characterStableId,
                    out int sourceIndex,
                    out _))
            {
                return false;
            }

            int actionId = _model?.SelectedAction?.ActionId ?? 0;
            string timelineItemId = _timelineView?.SelectedItemId ?? string.Empty;
            if (!LoadProfileSource(sourceIndex, confirmDiscard: true, rebuild: false))
                return true;

            if (actionId > 0 && SelectDocumentAction(actionId))
            {
                _documentSession.SelectTimelineItem(timelineItemId);
                _pendingTimelineSelectedItemId = timelineItemId;
            }
            using (CharacterActionPreviewBindingDiscovery.BeginReadScope())
            using (CharacterActionAuthoringDocumentState.BeginDirtyReadScope())
            {
                // All consumers use the new session's validation result. Only the
                // visible workspace needs rebuilding; others refresh on navigation.
                _validation = GetRuntimePreviewValidationContext();
                _validationDirty = false;
                RefreshUi(UiRefreshScope.Content);
            }
            return true;
        }

        private bool TryFindMatchingCharacterProfileSource(
            string characterStableId,
            out int sourceIndex,
            out string profileId)
        {
            sourceIndex = -1;
            profileId = string.Empty;
            string packageId = ResolveCharacterPackageId(characterStableId);
            if (string.IsNullOrWhiteSpace(packageId))
                return false;

            for (int i = 0; _profileSources != null && i < _profileSources.Length; i++)
            {
                CharacterActionAuthoringSourceEntry source = _profileSources[i];
                if (source == null
                    || !string.Equals(source.DisplayName, packageId, StringComparison.Ordinal))
                    continue;
                sourceIndex = i;
                profileId = packageId + ".default";
                return true;
            }
            return false;
        }

        private bool IsCurrentProfileForCharacter(string characterStableId)
        {
            string packageId = ResolveCharacterPackageId(characterStableId);
            string currentProfileId = _documentSession?.Selection.ProfileStableId ?? string.Empty;
            return !string.IsNullOrWhiteSpace(packageId)
                && (string.Equals(currentProfileId, packageId + ".default", StringComparison.Ordinal)
                    || currentProfileId.StartsWith(packageId + ".", StringComparison.Ordinal));
        }

        internal static string ResolveCharacterPackageId(string characterStableId)
        {
            const string prefix = "char.";
            characterStableId = (characterStableId ?? string.Empty).Trim();
            return characterStableId.StartsWith(prefix, StringComparison.Ordinal)
                && characterStableId.Length > prefix.Length
                    ? characterStableId.Substring(prefix.Length)
                    : string.Empty;
        }

        private ToolbarMenu BuildDocumentProfileToolbarMenu()
        {
            string current = _documentSession?.Selection.ProfileStableId ?? string.Empty;
            var menu = new ToolbarMenu
            {
                name = "character-action-toolbar-profile-selector",
                text = "Profile  " + (string.IsNullOrWhiteSpace(current) ? "未选择" : current)
            };
            CharacterActionProfileJsonDto[] profiles = GetDocumentProfiles();
            for (int i = 0; i < profiles.Length; i++)
            {
                CharacterActionProfileJsonDto profile = profiles[i];
                string stableId = profile?.StableId ?? string.Empty;
                string label = string.IsNullOrWhiteSpace(profile?.DisplayName)
                    ? stableId
                    : profile.DisplayName + "  ·  " + stableId;
                menu.menu.AppendAction(
                    label,
                    _ => SelectDocumentProfile(stableId),
                    _ => string.Equals(stableId, current, StringComparison.Ordinal)
                        ? DropdownMenuAction.Status.Checked
                        : DropdownMenuAction.Status.Normal);
            }
            menu.menu.AppendSeparator();
            for (int i = 0; i < _profileSources.Length; i++)
            {
                int sourceIndex = i;
                CharacterActionAuthoringSourceEntry source = _profileSources[i];
                menu.menu.AppendAction(
                    "打开来源/" + source.DisplayName,
                    _ => LoadProfileSource(sourceIndex, confirmDiscard: true),
                    DropdownMenuAction.AlwaysEnabled);
            }
            return menu;
        }

        private bool IsProfileCompositionOverview()
        {
            return _workspace == AuthoringWorkspace.Mappings
                && _mappingSection == MappingWorkspaceSection.ProfileComposition;
        }

        private Label BuildDocumentActionSetContextLabel()
        {
            CharacterActionSetJsonDto current = _documentSession?.CurrentActionSet;
            string stableId = current?.StableId ?? string.Empty;
            string displayName = string.IsNullOrWhiteSpace(current?.DisplayName)
                ? stableId
                : current.DisplayName;
            var label = new Label("当前方案  " + (string.IsNullOrWhiteSpace(displayName) ? "未选择" : displayName))
            {
                name = "character-action-toolbar-current-action-set",
                tooltip = string.IsNullOrWhiteSpace(stableId)
                    ? "请从下方装备动作方案卡片选择当前方案。"
                    : "Profile 总览通过下方方案卡片切换。\nActionSet · " + stableId,
            };
            label.AddToClassList("ca-toolbar-context-readonly");
            return label;
        }

        private ToolbarMenu BuildDocumentActionSetToolbarMenu()
        {
            string current = _documentSession?.Selection.ActionSetStableId ?? string.Empty;
            CharacterActionProfileJsonDto profile = _documentSession?.CurrentProfile;
            var menu = new ToolbarMenu
            {
                name = "character-action-toolbar-action-set-selector",
                text = "ActionSet  " + (string.IsNullOrWhiteSpace(current) ? "未选择" : current)
            };
            string[] ids = profile?.ActionSetIds ?? Array.Empty<string>();
            for (int i = 0; i < ids.Length; i++)
            {
                string stableId = ids[i] ?? string.Empty;
                bool resolved = _documentSession?.FindActionSetBundle(stableId) != null;
                menu.menu.AppendAction(
                    resolved ? stableId : stableId + "（未解析）",
                    _ => SelectDocumentActionSet(stableId),
                    _ => !resolved
                        ? DropdownMenuAction.Status.Disabled
                        : string.Equals(stableId, current, StringComparison.Ordinal)
                            ? DropdownMenuAction.Status.Checked
                            : DropdownMenuAction.Status.Normal);
            }
            return menu;
        }

        private void RunWorkbenchValidation()
        {
            EnsureValidation(includeSnapshots: true, force: true);
            _problemsExpanded = _validation?.HasErrors == true || _problemsExpanded;
            if (_problemsExpanded)
                EditorPrefs.SetBool(WorkbenchProblemsExpandedPreferenceKey, true);
            RefreshProblemsWorkbench();
            RefreshChrome();
            RebuildWorkbenchActionNavigation();
        }

        private ToolbarMenu CreateFileMenu()
        {
            var menu = new ToolbarMenu { text = "☰" };
            menu.tooltip = "文件、撤销、重做和 JSON 操作";

            menu.menu.AppendAction(
                "撤销",
                _ => UndoEdit(),
                _ => _undoHistory.Count > 0
                    ? DropdownMenuAction.Status.Normal
                    : DropdownMenuAction.Status.Disabled);
            menu.menu.AppendAction(
                "重做",
                _ => RedoEdit(),
                _ => _redoHistory.Count > 0
                    ? DropdownMenuAction.Status.Normal
                    : DropdownMenuAction.Status.Disabled);
            menu.menu.AppendAction("复制 JSON", _ => CopyJsonToClipboard(), DropdownMenuAction.AlwaysEnabled);
            menu.menu.AppendSeparator();

            menu.menu.AppendAction("新建配置", _ => NewConfig(), DropdownMenuAction.AlwaysEnabled);
            menu.menu.AppendSeparator();

            menu.menu.AppendAction(
                "保存 Profile",
                _ => SaveProfile(),
                _ => string.IsNullOrWhiteSpace(_profileSourceDirectory)
                    ? DropdownMenuAction.Status.Disabled
                    : DropdownMenuAction.Status.Normal);
            menu.menu.AppendAction("另存 Profile…", _ => SaveProfileAs());
            menu.menu.AppendAction(
                "保存 ActionSet",
                _ => SaveActionSet(),
                _ => string.IsNullOrWhiteSpace(_actionSetSourceDirectory)
                    ? DropdownMenuAction.Status.Disabled
                    : DropdownMenuAction.Status.Normal);
            menu.menu.AppendAction("另存 ActionSet…", _ => SaveActionSetAs());
            menu.menu.AppendAction(
                "保存动作库",
                _ => SaveActionLibrary(),
                _ => string.IsNullOrWhiteSpace(_actionLibrarySourceDirectory)
                    ? DropdownMenuAction.Status.Disabled
                    : DropdownMenuAction.Status.Normal);
            menu.menu.AppendAction("另存动作库…", _ => SaveActionLibraryAs());

            menu.menu.AppendSeparator();
            menu.menu.AppendAction(
                "删除 ActionSet",
                _ => DeleteActionSetSource(),
                _ => string.IsNullOrWhiteSpace(_actionSetSourceDirectory)
                    ? DropdownMenuAction.Status.Disabled
                    : DropdownMenuAction.Status.Normal);
            menu.menu.AppendAction(
                "删除动作库",
                _ => DeleteActionLibrarySource(),
                _ => string.IsNullOrWhiteSpace(_actionLibrarySourceDirectory)
                    ? DropdownMenuAction.Status.Disabled
                    : DropdownMenuAction.Status.Normal);

            return menu;
        }

        private void HotReloadCharacterActions()
        {
            if (!EditorApplication.isPlaying)
                return;

            if (IsAnyCharacterActionSourceDirty() || _abilityEffectTimelineDirty)
            {
                SaveAllDirtyCharacterActionSources();
                if (IsAnyCharacterActionSourceDirty() || _abilityEffectTimelineDirty)
                {
                    EditorUtility.DisplayDialog(
                        "角色动作热重载被阻止",
                        "当前修改未能保存。请处理问题面板中的校验错误或缺失来源，然后再次热重载。",
                        "OK");
                    return;
                }
            }

            if (!EditorApplication.ExecuteMenuItem(CharacterTestHotReloadMenuPath))
            {
                EditorUtility.DisplayDialog(
                    "角色动作热重载不可用",
                    "当前 Play Mode 中没有可用的 CharacterTest 热重载入口。请确认正在运行 CharacterTest 场景并等待初始化完成。",
                    "OK");
            }
        }

        private void CountValidationDiagnostics(out int errorCount, out int warningCount)
        {
            errorCount = 0;
            warningCount = 0;
            _problemsModel = CreateWorkbenchProblemsModel();
            errorCount = _problemsModel.ErrorCount;
            warningCount = _problemsModel.WarningCount;
        }

        private static void CountImportDiagnostics(
            CharacterActionJsonDiagnostic[] diagnostics,
            HashSet<string> seen,
            ref int errorCount,
            ref int warningCount)
        {
            diagnostics = diagnostics ?? Array.Empty<CharacterActionJsonDiagnostic>();
            for (int i = 0; i < diagnostics.Length; i++)
            {
                CharacterActionJsonDiagnostic diagnostic = diagnostics[i];
                CountDiagnostic(
                    diagnostic.SourcePath + "|" + diagnostic.JsonPath + "|" + diagnostic.Code + "|" + diagnostic.Message,
                    diagnostic.Severity,
                    seen,
                    ref errorCount,
                    ref warningCount);
            }
        }

        private static void CountValidationDiagnostics(
            CharacterActionValidationDiagnostic[] diagnostics,
            HashSet<string> seen,
            ref int errorCount,
            ref int warningCount)
        {
            diagnostics = diagnostics ?? Array.Empty<CharacterActionValidationDiagnostic>();
            for (int i = 0; i < diagnostics.Length; i++)
            {
                CharacterActionValidationDiagnostic diagnostic = diagnostics[i];
                CountDiagnostic(
                    diagnostic.SourcePath + "|" + diagnostic.StablePath + "|" + diagnostic.Code + "|" + diagnostic.Message,
                    diagnostic.Severity,
                    seen,
                    ref errorCount,
                    ref warningCount);
            }
        }

        private static void CountDiagnostic(
            string key,
            CharacterActionDiagnosticSeverity severity,
            HashSet<string> seen,
            ref int errorCount,
            ref int warningCount)
        {
            if (seen != null && !seen.Add(key ?? string.Empty))
                return;

            if (severity == CharacterActionDiagnosticSeverity.Error)
                errorCount++;
            else if (severity == CharacterActionDiagnosticSeverity.Warning)
                warningCount++;
        }

        private void RefreshFooter()
        {
            if (_footerBar == null)
                return;
            RefreshProblemsWorkbench();
        }

        private static string ShortPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return "—";

            string normalized = path.Replace('\\', '/');
            int last = normalized.LastIndexOf('/');
            return last >= 0 && last < normalized.Length - 1
                ? normalized.Substring(last + 1)
                : normalized;
        }

        private static string DisplayPath(string path)
        {
            return string.IsNullOrWhiteSpace(path) ? "（未绑定）" : path;
        }

        private bool IsAnyDirty()
        {
            return IsDocumentSessionDirty()
                || _profileDirty
                || _actionSetDirty
                || _actionLibraryDirty
                || _abilityEffectTimelineDirty
                || HasPendingMechanicTuningAuthoringChanges;
        }

        private string CreateDirtyStatusText()
        {
            var dirtySources = new List<string>(6);
            AppendDocumentSessionDirtySourceLabels(dirtySources);
            if (_profileDirty)
                AddDirtySourceLabel(dirtySources, "Profile");
            if (_actionSetDirty)
                AddDirtySourceLabel(dirtySources, "ActionSet");
            if (_actionLibraryDirty)
                AddDirtySourceLabel(dirtySources, "Library");
            if (HasPendingMechanicTuningAuthoringChanges)
                AddDirtySourceLabel(dirtySources, "Mechanic Tuning");
            if (_abilityEffectTimelineDirty)
                AddDirtySourceLabel(dirtySources, "Ability Timeline");
            if (dirtySources.Count > 0)
                return string.Join("/", dirtySources) + " modified";
            return "已保存";
        }
    }
}
