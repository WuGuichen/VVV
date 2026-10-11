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
        private void BuildProfileTab(VisualElement schemesRoot, VisualElement detailRoot)
        {
            CharacterActionProfileJsonDto profile = _model.Profile ?? new CharacterActionProfileJsonDto();
            _model.Profile = profile;
            CharacterActionProfileOverviewProjection overview =
                CharacterActionProfileOverviewProjection.Build(_documentSession, profile, _model.ActionSet);

            BuildProfileOverviewHeader(schemesRoot, overview);
            BuildProfileActionSetCards(schemesRoot, overview);
            BuildProfileCurrentScheme(detailRoot, overview);
        }

        private void BuildProfileOverviewHeader(
            VisualElement root,
            CharacterActionProfileOverviewProjection overview)
        {
            var header = new VisualElement { name = "character-action-profile-overview-header" };
            header.AddToClassList("ca-profile-overview-header");

            var copy = new VisualElement();
            copy.AddToClassList("ca-profile-overview-header__copy");
            var displayName = new TextField
            {
                name = "character-action-profile-display-name",
                value = overview.Profile.DisplayName ?? string.Empty,
                isDelayed = true,
                tooltip = "面向创作者的方案名称；不会修改 Stable ID。",
            };
            displayName.AddToClassList("ca-profile-overview-title-field");
            displayName.RegisterValueChangedCallback(evt => ApplyFieldChange(
                () => overview.Profile.DisplayName = evt.newValue ?? string.Empty));
            copy.Add(displayName);

            string characterId = overview.SelectedActionSet?.ActionSet?.CharacterStableId ?? string.Empty;
            copy.Add(new Label(
                "角色 " + (string.IsNullOrWhiteSpace(characterId) ? "未指定" : characterId)
                + "  ·  " + overview.ActionSets.Length + " 个装备方案")
            {
                name = "character-action-profile-context",
            });
            header.Add(copy);

            var actions = new VisualElement();
            actions.AddToClassList("ca-profile-overview-header__actions");
            actions.Add(CreateProfileHealthBadge());
            actions.Add(new Button(() => LoadProfileReferencedSources(rebuild: true))
            {
                name = "character-action-profile-reload",
                text = "重新加载 Profile 引用",
                tooltip = "重新读取当前 Profile 声明的 ActionSet 与动作库。",
            });
            header.Add(actions);
            root.Add(header);
        }

        private Label CreateProfileHealthBadge()
        {
            string text;
            string extraClass;
            CountValidationDiagnostics(out int errors, out int warnings);
            if (_validationDirty || _validation == null)
            {
                text = "文档会话 · 待校验";
                extraClass = "ca-profile-status--pending";
            }
            else if (errors > 0)
            {
                text = "文档会话 · " + errors + " 个错误";
                extraClass = "ca-profile-status--error";
            }
            else if (warnings > 0)
            {
                text = "文档会话 · " + warnings + " 个警告";
                extraClass = "ca-profile-status--warning";
            }
            else
            {
                text = "文档会话 · ✓ 正常";
                extraClass = "ca-profile-status--success";
            }

            var badge = new Label(text) { name = "character-action-profile-health" };
            badge.AddToClassList("ca-profile-status");
            badge.AddToClassList(extraClass);
            return badge;
        }

        private void BuildProfileActionSetCards(
            VisualElement root,
            CharacterActionProfileOverviewProjection overview)
        {
            var heading = new Label("装备动作方案");
            heading.AddToClassList("ca-profile-section-title");
            root.Add(heading);

            if (overview.ActionSets.Length == 0)
            {
                root.Add(new HelpBox(
                    "当前 Profile 尚未包含装备动作方案。请添加已有 ActionSet，或先在 CharacterStudio 创建组合并重新生成。",
                    HelpBoxMessageType.Info));
            }
            for (int i = 0; i < overview.ActionSets.Length; i++)
                root.Add(BuildProfileActionSetCard(overview.ActionSets[i]));

            var add = new Button(() => ShowAddProfileActionSetMenu(overview))
            {
                name = "character-action-profile-add-action-set",
                text = "+  添加装备方案",
            };
            add.AddToClassList("ca-profile-add-button");
            root.Add(add);
        }

        private VisualElement BuildProfileActionSetCard(CharacterActionProfileActionSetSummary summary)
        {
            CharacterActionSetJsonDto actionSet = summary.ActionSet;
            var card = new VisualElement
            {
                name = "character-action-profile-action-set-" + (actionSet.StableId ?? string.Empty),
            };
            card.AddToClassList("ca-profile-scheme-card");
            card.EnableInClassList("ca-profile-scheme-card--selected", summary.IsSelected);
            card.tooltip = "点击卡片切换当前编辑方案；不会修改 Profile。";
            card.RegisterCallback<ClickEvent>(evt =>
            {
                if (IsProfileActionSetCardControl(card, evt.target as VisualElement))
                    return;

                SelectDocumentActionSet(actionSet.StableId);
                evt.StopPropagation();
            });

            var header = new VisualElement();
            header.AddToClassList("ca-profile-scheme-card__header");
            var select = new Button(() => SelectDocumentActionSet(actionSet.StableId))
            {
                name = "character-action-profile-select-action-set",
                text = string.IsNullOrWhiteSpace(actionSet.DisplayName)
                    ? actionSet.StableId
                    : actionSet.DisplayName,
                tooltip = "切换当前编辑方案；不会修改 Profile。",
            };
            select.AddToClassList("ca-profile-scheme-card__select");
            header.Add(select);
            if (summary.IsSelected)
            {
                var current = new Label("✓ 当前方案")
                {
                    name = "character-action-profile-current-scheme",
                };
                current.AddToClassList("ca-profile-current-scheme-badge");
                header.Add(current);
            }
            var edit = new Button(() =>
            {
                SelectDocumentActionSet(actionSet.StableId);
                SetMappingSection(MappingWorkspaceSection.ActionSet);
            })
            {
                name = "character-action-profile-edit-action-set",
                text = "方案基础设置",
                tooltip = "编辑 ActionSet 的显示名称、Movement、Reaction Profile 等基础属性。",
            };
            edit.AddToClassList("ca-profile-card-action");
            header.Add(edit);
            var menu = new ToolbarMenu
            {
                name = "character-action-profile-action-set-menu",
                text = "⋮",
            };
            menu.menu.AppendAction(
                "从 Profile 移除…",
                _ => RemoveProfileActionSet(actionSet.StableId, requireConfirmation: true),
                _ => DropdownMenuAction.Status.Normal);
            header.Add(menu);
            card.Add(header);

            var stableId = new Label(actionSet.StableId ?? string.Empty);
            stableId.AddToClassList("ca-profile-secondary-text");
            card.Add(stableId);
            card.Add(new Label(
                summary.ReferencedActionIds.Length + " 个关联动作  ·  "
                + summary.CommandBindingCount + " 个输入触发  ·  "
                + summary.AbilityBindingCount + " 个技能触发")
            {
                name = "character-action-profile-scheme-counts",
            });

            var statusRow = new VisualElement
            {
                name = "character-action-profile-scheme-status",
            };
            statusRow.AddToClassList("ca-profile-scheme-status-row");
            AddProfileStatusChip(
                statusRow,
                summary.MissingActionCount == 0,
                summary.MissingActionCount == 0
                    ? "动作可解析"
                    : summary.MissingActionCount + " 个动作缺失");
            AddProfileStatusChip(
                statusRow,
                summary.HasReactionProfile,
                summary.HasReactionProfile ? "受击已配置" : "受击缺失");
            if (!summary.HasDefaultAction)
                AddProfileReminderChip(statusRow, "未设置默认动作");
            card.Add(statusRow);

            return card;
        }

        private static bool IsProfileActionSetCardControl(
            VisualElement card,
            VisualElement eventTarget)
        {
            for (VisualElement current = eventTarget;
                 current != null && current != card;
                 current = current.parent)
            {
                if (current is Button || current is ToolbarMenu)
                    return true;
            }

            return false;
        }

        private static void AddProfileStatusChip(VisualElement parent, bool success, string text)
        {
            var label = new Label((success ? "✓  " : "!  ") + text);
            label.AddToClassList("ca-profile-scheme-status-chip");
            label.AddToClassList(success ? "ca-profile-status-line--success" : "ca-profile-status-line--error");
            parent.Add(label);
        }

        private static void AddProfileReminderChip(VisualElement parent, string text)
        {
            var label = new Label("!  " + text);
            label.AddToClassList("ca-profile-scheme-status-chip");
            label.AddToClassList("ca-profile-status-line--warning");
            parent.Add(label);
        }

        private void ShowAddProfileActionSetMenu(CharacterActionProfileOverviewProjection overview)
        {
            if (overview.AvailableActionSets.Length == 0)
            {
                EditorUtility.DisplayDialog(
                    "没有可添加的装备方案",
                    "当前来源中没有尚未加入 Profile 的 ActionSet。\n\n请先在 CharacterStudio 的组合武器工作台创建组合并生成 ActionSet，然后返回这里重新加载 Profile 引用。",
                    "知道了");
                return;
            }

            var menu = new GenericMenu();
            for (int i = 0; i < overview.AvailableActionSets.Length; i++)
            {
                CharacterActionSetJsonDto actionSet = overview.AvailableActionSets[i];
                string label = (string.IsNullOrWhiteSpace(actionSet.DisplayName)
                    ? actionSet.StableId
                    : actionSet.DisplayName) + "  ·  " + actionSet.StableId;
                menu.AddItem(new GUIContent(label), false, () => AddProfileActionSet(actionSet.StableId));
            }
            menu.ShowAsContext();
        }

        private void AddProfileActionSet(string stableId)
        {
            CharacterActionProfileJsonDto profile = _model?.Profile;
            if (profile == null || ContainsId(profile.ActionSetIds, stableId))
                return;

            ApplyFieldChange(() => profile.ActionSetIds = AppendId(profile.ActionSetIds, stableId));
            SelectDocumentActionSet(stableId);
        }

        private void RemoveProfileActionSet(string stableId, bool requireConfirmation)
        {
            CharacterActionProfileJsonDto profile = _model?.Profile;
            if (profile == null || !ContainsId(profile.ActionSetIds, stableId))
                return;
            if (requireConfirmation && !EditorUtility.DisplayDialog(
                    "从 Profile 移除装备方案",
                    "将从当前 Profile 移除“" + stableId + "”。\n\n这不会删除 ActionSet 文件、动作、输入/技能绑定或受击配置。",
                    "移除",
                    "取消"))
                return;

            ApplyFieldChange(() => profile.ActionSetIds = RemoveId(profile.ActionSetIds, stableId));
        }

        private void BuildProfileCurrentScheme(
            VisualElement root,
            CharacterActionProfileOverviewProjection overview)
        {
            CharacterActionProfileActionSetSummary selected = overview.SelectedActionSet;
            if (selected == null)
            {
                root.Add(new HelpBox("请选择或添加一个装备动作方案。", HelpBoxMessageType.Info));
                BuildProfileTechnicalInformation(root, overview.Profile);
                return;
            }

            var title = new Label("当前方案：" + (string.IsNullOrWhiteSpace(selected.ActionSet.DisplayName)
                ? selected.ActionSet.StableId
                : selected.ActionSet.DisplayName));
            title.AddToClassList("ca-profile-current-title");
            root.Add(title);
            BuildProfileTaskSteps(root);
            BuildProfileConfigurationOverview(root, selected);
            BuildProfileLibraries(root, overview);
            BuildProfileTechnicalInformation(root, overview.Profile);
        }

        private void BuildProfileTaskSteps(VisualElement root)
        {
            var steps = new VisualElement { name = "character-action-profile-steps" };
            steps.AddToClassList("ca-profile-steps");
            steps.Add(CreateProfileStepButton("1", "动作内容", OpenProfileActions));
            steps.Add(CreateProfileStepButton("2", "输入与技能", OpenProfileCommandBindings));
            steps.Add(CreateProfileStepButton("3", "受击响应", OpenProfileReactions));
            steps.Add(CreateProfileStepButton("4", "验证", OpenProfileProblems));
            root.Add(steps);
        }

        private static Button CreateProfileStepButton(string number, string label, Action action)
        {
            var button = new Button(action)
            {
                text = number + "  " + label,
                name = "character-action-profile-step-" + number,
            };
            button.AddToClassList("ca-profile-step");
            return button;
        }

        private void BuildProfileConfigurationOverview(
            VisualElement root,
            CharacterActionProfileActionSetSummary selected)
        {
            VisualElement card = AddCard(root, "配置概览");
            card.AddToClassList("ca-profile-overview-card");
            AddProfileOverviewRow(
                card,
                "动作内容",
                selected.ReferencedActionIds.Length + " 个关联动作，" + selected.MissingActionCount + " 个缺失",
                "编辑动作",
                OpenProfileActions);

            var triggerActions = new VisualElement();
            triggerActions.AddToClassList("ca-profile-overview-row__actions");
            triggerActions.Add(new Button(OpenProfileCommandBindings) { text = "输入触发" });
            triggerActions.Add(new Button(OpenProfileAbilityBindings) { text = "技能触发" });
            AddProfileOverviewRow(
                card,
                "输入与技能",
                selected.CommandBindingCount + " 个输入触发，"
                + selected.AbilityBindingCount + " 个技能触发",
                triggerActions);

            AddProfileOverviewRow(
                card,
                "受击响应",
                selected.HasReactionProfile
                    ? (selected.ReactionProfile.Rules?.Length ?? 0) + " 条规则已配置"
                    : "未找到当前 Reaction Profile",
                "编辑受击",
                OpenProfileReactions);

            string validationText = selected.MissingActionCount > 0
                ? selected.MissingActionCount + " 个关联动作缺失"
                : selected.HasDefaultAction ? "当前方案没有结构提醒" : "1 个提醒：未设置默认动作";
            AddProfileOverviewRow(
                card,
                "当前方案状态",
                "当前方案 · " + validationText,
                "查看问题",
                OpenProfileProblems,
                warning: true);
        }

        private static void AddProfileOverviewRow(
            VisualElement parent,
            string title,
            string summary,
            string buttonText,
            Action action,
            bool warning = false)
        {
            var actions = new VisualElement();
            actions.AddToClassList("ca-profile-overview-row__actions");
            actions.Add(new Button(action) { text = buttonText });
            AddProfileOverviewRow(parent, title, summary, actions, warning);
        }

        private static void AddProfileOverviewRow(
            VisualElement parent,
            string title,
            string summary,
            VisualElement actions,
            bool warning = false)
        {
            var row = new VisualElement();
            row.AddToClassList("ca-profile-overview-row");
            var titleLabel = new Label(title);
            titleLabel.AddToClassList("ca-profile-overview-row__title");
            row.Add(titleLabel);
            var summaryLabel = new Label(summary);
            summaryLabel.AddToClassList("ca-profile-overview-row__summary");
            if (warning)
                summaryLabel.AddToClassList("ca-profile-status-line--warning");
            row.Add(summaryLabel);
            row.Add(actions);
            parent.Add(row);
        }

        private void OpenProfileActions()
        {
            CharacterActionProfileActionSetSummary selected =
                CharacterActionProfileOverviewProjection.Build(_documentSession, _model?.Profile, _model?.ActionSet)
                    .SelectedActionSet;
            if (_model?.SelectedAction == null && selected != null)
            {
                for (int i = 0; i < selected.ReferencedActionIds.Length; i++)
                {
                    if (SelectDocumentAction(selected.ReferencedActionIds[i]))
                        break;
                }
            }
            SetWorkspace(AuthoringWorkspace.ActionComposition);
        }

        private void OpenProfileCommandBindings()
        {
            SetMappingSection(MappingWorkspaceSection.CommandBindings);
        }

        private void OpenProfileAbilityBindings()
        {
            SetMappingSection(MappingWorkspaceSection.AbilityBindings);
        }

        private void OpenProfileReactions()
        {
            SetMappingSection(MappingWorkspaceSection.Reactions);
        }

        private void OpenProfileProblems()
        {
            _problemsQuery = string.Empty;
            _problemsScope = CharacterActionAuthoringProblemScope.CurrentSource;
            _problemsSeverity = CharacterActionAuthoringProblemSeverityFilter.All;
            _problemsExpanded = true;
            EditorPrefs.SetBool(WorkbenchProblemsExpandedPreferenceKey, true);
            SaveSessionState();
            RefreshProblemsWorkbench();
        }

        private void BuildProfileLibraries(
            VisualElement root,
            CharacterActionProfileOverviewProjection overview)
        {
            VisualElement card = AddCard(root, "可用动作库");
            card.AddToClassList("ca-profile-libraries");
            for (int i = 0; i < overview.Libraries.Length; i++)
                card.Add(BuildProfileLibraryRow(overview.Libraries[i]));
            if (overview.Libraries.Length == 0)
                card.Add(new HelpBox("当前 Profile 尚未引用可解析的动作库。", HelpBoxMessageType.Warning));
        }

        private VisualElement BuildProfileLibraryRow(CharacterActionProfileLibrarySummary summary)
        {
            CharacterActionAuthoringActionLibraryDocument library = summary.Library;
            var row = new VisualElement
            {
                name = "character-action-profile-library-" + library.LibraryId,
            };
            row.AddToClassList("ca-profile-library-row");

            var copy = new VisualElement();
            copy.AddToClassList("ca-profile-library-row__copy");
            var title = new Label((summary.IsCommon ? "公共动作 · " : "角色专属 · ") + library.LibraryId);
            title.AddToClassList("ca-profile-library-row__title");
            copy.Add(title);
            var details = new Label(
                summary.ActionCount + " 个动作  ·  当前 Profile 直接引用 "
                + summary.DirectReferences.Length + " 项");
            details.AddToClassList("ca-profile-secondary-text");
            copy.Add(details);
            row.Add(copy);

            var actions = new VisualElement();
            actions.AddToClassList("ca-profile-library-row__actions");
            if (summary.IsSelected)
            {
                var target = new Label("新建目标");
                target.AddToClassList("ca-profile-library-target");
                actions.Add(target);
            }
            else
            {
                actions.Add(new Button(() => SelectDocumentActionLibrary(library.LibraryId))
                {
                    text = "设为新建目标",
                });
            }
            actions.Add(new Button(() =>
            {
                SelectDocumentActionLibrary(library.LibraryId);
                SetWorkspace(AuthoringWorkspace.ActionComposition);
            })
            {
                name = "character-action-profile-browse-library",
                text = "浏览动作",
            });
            var references = new Button { text = "查看引用" };
            actions.Add(references);
            var menu = new ToolbarMenu { text = "⋮" };
            menu.menu.AppendAction(
                "从 Profile 移除…",
                _ => RemoveProfileActionLibrary(summary, requireConfirmation: true),
                _ => DropdownMenuAction.Status.Normal);
            actions.Add(menu);
            row.Add(actions);

            var referenceList = new VisualElement();
            referenceList.AddToClassList("ca-profile-library-references");
            referenceList.style.display = DisplayStyle.None;
            for (int i = 0; i < summary.DirectReferences.Length; i++)
            {
                CharacterActionProfileDirectReference reference = summary.DirectReferences[i];
                referenceList.Add(new Label(reference.SourceLabel + "  →  " + reference.ActionId));
            }
            if (summary.DirectReferences.Length == 0)
                referenceList.Add(new Label("当前 Profile 没有直接引用。"));
            bool referencesVisible = false;
            references.clicked += () =>
            {
                referencesVisible = !referencesVisible;
                referenceList.style.display = referencesVisible ? DisplayStyle.Flex : DisplayStyle.None;
            };
            row.Add(referenceList);
            return row;
        }

        private void RemoveProfileActionLibrary(
            CharacterActionProfileLibrarySummary summary,
            bool requireConfirmation)
        {
            if (summary.DirectReferences.Length > 0)
            {
                EditorUtility.DisplayDialog(
                    "动作库仍被引用",
                    "当前 Profile 仍有 " + summary.DirectReferences.Length
                    + " 项直接引用指向动作库“" + summary.Library.LibraryId
                    + "”。请先查看并处理这些引用。",
                    "查看引用后处理");
                return;
            }
            if (requireConfirmation && !EditorUtility.DisplayDialog(
                    "从 Profile 移除动作库",
                    "将移除对动作库“" + summary.Library.LibraryId
                    + "”的 Profile 引用。动作库文件和其中动作不会被删除。",
                    "移除",
                    "取消"))
                return;

            CharacterActionProfileJsonDto profile = _model?.Profile;
            if (profile != null)
                ApplyFieldChange(() => profile.ActionLibraryIds = RemoveId(
                    profile.ActionLibraryIds,
                    summary.Library.LibraryId));
        }

        private void BuildProfileTechnicalInformation(
            VisualElement root,
            CharacterActionProfileJsonDto profile)
        {
            Foldout technical = CreateSectionFoldout(
                root,
                "profile-technical-information",
                "技术信息  ·  Profile Stable ID、数据来源与内部 ID",
                false);
            technical.name = "profile-technical-information";
            technical.AddToClassList("ca-profile-technical");
            AddReadOnlyProfileField(technical, "Stable ID", profile.StableId);
            AddReadOnlyProfileField(technical, "内部 ID", profile.Id.ToString());
            AddReadOnlyProfileField(
                technical,
                "数据来源",
                string.IsNullOrWhiteSpace(_profileSourceDirectory) ? "未保存" : _profileSourceDirectory);

            Foldout maintenance = CreateSectionFoldout(
                technical,
                "profile-reference-maintenance",
                "高级引用维护",
                false);
            maintenance.name = "profile-reference-maintenance";
            maintenance.Add(new HelpBox(
                "仅在修复未解析历史引用时直接编辑 ID；日常成员管理请使用方案卡片和动作库卡片。",
                HelpBoxMessageType.Info));
            AddTextField(
                maintenance,
                "Action Set IDs",
                string.Join(", ", profile.ActionSetIds ?? Array.Empty<string>()),
                value => profile.ActionSetIds = SplitCsv(value));
            AddTextField(
                maintenance,
                "Action Library IDs",
                string.Join(", ", profile.ActionLibraryIds ?? Array.Empty<string>()),
                value => profile.ActionLibraryIds = SplitCsv(value));
        }

        private static void AddReadOnlyProfileField(VisualElement parent, string label, string value)
        {
            var field = new TextField(label)
            {
                value = value ?? string.Empty,
                isReadOnly = true,
            };
            field.AddToClassList("ca-profile-readonly-field");
            parent.Add(field);
        }
    }
}
