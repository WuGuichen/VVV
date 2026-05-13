using System;
using MxFramework.Combat.Authoring;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Combat.Editor
{
    public sealed partial class CombatAuthoringWindow
    {
        public void CreateGUI()
        {
            ConfigureWindowForMode();
            rootVisualElement.Clear();
            rootVisualElement.style.paddingLeft = 0;
            rootVisualElement.style.paddingRight = 0;
            rootVisualElement.style.paddingTop = 0;
            rootVisualElement.style.paddingBottom = 0;

            if (_mode == CombatAuthoringWindowMode.Timeline)
            {
                CreateTimelineGui();
            }
            else
            {
                CreateInspectorGui();
            }

            RegisterKeyboardShortcuts();
            RegisterTimelineDragRecoveryCallbacks();
            if (_actionAsset == null && _sceneBindingAsset == null)
            {
                ApplySharedContextToLocal();
            }

            SetActionAsset(_actionAsset);
            SetSceneBindingAsset(_sceneBindingAsset);
            ApplySharedSelectionToLocal(false);
            _observedDataRevision = CombatAuthoringSceneState.DataRevision;
            RefreshValidation();
        }

        private void CreateInspectorGui()
        {
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexGrow = 1;
            scroll.style.minHeight = 0;
            rootVisualElement.Add(scroll);

            var content = new VisualElement();
            content.style.paddingLeft = 12;
            content.style.paddingRight = 12;
            content.style.paddingTop = 10;
            content.style.paddingBottom = 10;
            content.style.minWidth = 0;
            scroll.Add(content);

            content.Add(CreateContextBar());
            content.Add(CreateToolbar());
            content.Add(CreateEmptyState());
            content.Add(CreateInspectorBody());
            content.Add(CreateIssueDrawer());
        }

        private void CreateTimelineGui()
        {
            var content = new VisualElement();
            content.style.flexGrow = 1;
            content.style.minHeight = 0;
            content.style.paddingLeft = 12;
            content.style.paddingRight = 12;
            content.style.paddingTop = 10;
            content.style.paddingBottom = 10;
            rootVisualElement.Add(content);

            content.Add(CreateContextBar());
            content.Add(CreateTimelineBody());
        }

        private VisualElement CreateContextBar()
        {
            var root = new VisualElement();
            root.style.flexDirection = FlexDirection.Column;
            root.style.marginBottom = 8;
            root.style.backgroundColor = UiColors.SurfaceAlt;
            root.style.borderBottomLeftRadius = 6;
            root.style.borderBottomRightRadius = 6;
            root.style.borderTopLeftRadius = 6;
            root.style.borderTopRightRadius = 6;
            root.style.paddingLeft = 10;
            root.style.paddingRight = 10;
            root.style.paddingTop = 8;
            root.style.paddingBottom = 8;

            var headerRow = new VisualElement();
            headerRow.style.flexDirection = FlexDirection.Row;
            headerRow.style.alignItems = Align.Center;
            headerRow.style.marginBottom = 6;

            var assetRow = new VisualElement();
            assetRow.style.flexDirection = FlexDirection.Row;
            assetRow.style.flexWrap = Wrap.Wrap;
            assetRow.style.alignItems = Align.Center;

            _actionField = CreateCompactObjectField("Action Asset", typeof(CombatActionAuthoringAsset));
            _actionField.RegisterValueChangedCallback(evt => SetActionAsset(evt.newValue as CombatActionAuthoringAsset));
            assetRow.Add(_actionField);

            _bindingField = CreateCompactObjectField("Scene Binding", typeof(CombatSceneBindingAsset));
            _bindingField.RegisterValueChangedCallback(evt => SetSceneBindingAsset(evt.newValue as CombatSceneBindingAsset));
            assetRow.Add(_bindingField);
            root.Add(assetRow);

            _contextLabel = new Label();
            _contextLabel.style.minWidth = 460;
            _contextLabel.style.marginTop = 4;
            _contextLabel.style.whiteSpace = WhiteSpace.Normal;
            _contextLabel.style.fontSize = 11;
            _contextLabel.style.color = UiColors.TextSecondary;
            root.Add(_contextLabel);

            return root;
        }

        private VisualElement CreateToolbar()
        {
            var root = new VisualElement();
            _toolbarRoot = root;
            root.style.marginBottom = 8;

            root.Add(CreateToolbarGroup("Asset",
                CreateButton("使用当前选择", UseSelection),
                CreateButton("创建 Action Asset", CreateActionAsset),
                CreateButton("打开 Timeline", () => OpenTimelineWindow()),
                CreateButton("定位 Action", () => PingObject(_actionAsset)),
                CreateButton("定位 Binding", () => PingObject(_sceneBindingAsset))));

            root.Add(CreateToolbarGroup("创建",
                CreateButton("创建 Binding", CreateBindingAsset),
                CreateButton("从当前场景生成", GenerateBindingFromCurrentScene),
                CreateButton("重连选中对象", RelinkSelectedTransform),
                CreateButton("添加 Hitbox", () => AddShape("Hitbox", "hitboxes")),
                CreateButton("添加 Hurtbox", () => AddShape("Hurtbox", "hurtboxes"))));

            root.Add(CreateToolbarGroup("工具",
                CreateButton("执行验证", RefreshValidation),
                CreateButton("运行 Showcase 预览", RunShowcasePreview),
                CreateButton("清除预览会话", ClearShowcasePreviewSession),
                CreateButton("复制报告", CopyReport),
                CreateButton("导出 JSON", ExportJson),
                CreateButton("复制导出报告", CopyExportReport),
                CreateButton("预览 Explain", PreviewExplain),
                CreateButton("复制 Explain", CopyExplain)));

            var statusRow = new VisualElement();
            statusRow.style.flexDirection = FlexDirection.Row;
            statusRow.style.alignItems = Align.Center;
            statusRow.style.marginTop = 4;

            _validationLabel = new Label();
            _validationLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _validationLabel.style.fontSize = 12;
            statusRow.Add(_validationLabel);

            _quickActionStatusLabel = new Label();
            _quickActionStatusLabel.style.marginLeft = 12;
            _quickActionStatusLabel.style.fontSize = 11;
            _quickActionStatusLabel.style.color = UiColors.TextSecondary;
            _quickActionStatusLabel.style.whiteSpace = WhiteSpace.Normal;
            _quickActionStatusLabel.style.flexGrow = 1;
            statusRow.Add(_quickActionStatusLabel);
            root.Add(statusRow);

            return root;
        }

        private static VisualElement CreateToolbarGroup(string title, params VisualElement[] buttons)
        {
            var group = new VisualElement();
            group.style.marginBottom = 6;

            var label = new Label(title);
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.fontSize = 10;
            label.style.color = UiColors.TextSecondary;
            label.style.unityTextAlign = TextAnchor.MiddleLeft;
            label.style.marginBottom = 2;
            group.Add(label);

            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.flexWrap = Wrap.Wrap;
            row.style.alignItems = Align.Center;
            foreach (var button in buttons)
            {
                row.Add(button);
            }

            group.Add(row);
            return group;
        }

        private VisualElement CreateEmptyState()
        {
            _emptyStateRoot = CreatePanel("快速开始");
            _emptyStateRoot.style.marginBottom = 8;

            var hero = new VisualElement();
            hero.style.alignItems = Align.Center;
            hero.style.paddingTop = 16;
            hero.style.paddingBottom = 12;

            var heroIcon = new Label("\u2694");
            heroIcon.style.fontSize = 32;
            heroIcon.style.marginBottom = 8;
            hero.Add(heroIcon);

            var heroTitle = new Label("Combat Authoring");
            heroTitle.style.fontSize = 16;
            heroTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            heroTitle.style.color = UiColors.TextPrimary;
            heroTitle.style.marginBottom = 4;
            hero.Add(heroTitle);

            var heroDesc = new Label("选择或创建 Action Asset 开始编辑战斗动作。Scene Binding 可选，选择后可以校验 marker。");
            heroDesc.style.whiteSpace = WhiteSpace.Normal;
            heroDesc.style.color = UiColors.TextSecondary;
            heroDesc.style.maxWidth = 480;
            heroDesc.style.unityTextAlign = TextAnchor.MiddleCenter;
            hero.Add(heroDesc);
            _emptyStateRoot.Add(hero);

            var stepsRow = new VisualElement();
            stepsRow.style.flexDirection = FlexDirection.Row;
            stepsRow.style.justifyContent = Justify.Center;
            stepsRow.style.flexWrap = Wrap.Wrap;
            stepsRow.style.marginTop = 4;

            stepsRow.Add(CreateStepCard("1", "选择 Action", "从 Project 窗口拖入、使用当前选择或新建资产", CreateButton("创建 Action Asset", CreateActionAsset)));
            stepsRow.Add(CreateStepCard("2", "创建 Binding", "绑定场景对象到 marker", CreateButton("创建 Binding", CreateBindingAsset)));
            stepsRow.Add(CreateStepCard("3", "编辑 & 验证", "添加 Hitbox、调整帧范围、执行验证", CreateButton("执行验证", RefreshValidation)));
            _emptyStateRoot.Add(stepsRow);

            var secondaryActions = new VisualElement();
            secondaryActions.style.flexDirection = FlexDirection.Row;
            secondaryActions.style.flexWrap = Wrap.Wrap;
            secondaryActions.style.justifyContent = Justify.Center;
            secondaryActions.style.marginTop = 8;
            secondaryActions.style.paddingTop = 8;
            secondaryActions.style.borderTopWidth = 1;
            secondaryActions.style.borderTopColor = UiColors.Border;

            secondaryActions.Add(CreateButton("从当前场景生成 Binding", GenerateBindingFromCurrentScene));
            secondaryActions.Add(CreateButton("使用当前选择", UseSelection));
            secondaryActions.Add(CreateButton("重连选中对象", RelinkSelectedTransform));
            secondaryActions.Add(CreateButton("添加 Hitbox", () => AddShape("Hitbox", "hitboxes")));
            secondaryActions.Add(CreateButton("添加 Hurtbox", () => AddShape("Hurtbox", "hurtboxes")));
            secondaryActions.Add(CreateButton("运行 Showcase 预览", RunShowcasePreview));
            secondaryActions.Add(CreateButton("导出 JSON", ExportJson));
            secondaryActions.Add(CreateButton("预览 Explain", PreviewExplain));
            _emptyStateRoot.Add(secondaryActions);

            return _emptyStateRoot;
        }

        private static VisualElement CreateStepCard(string number, string title, string desc, VisualElement button)
        {
            var card = new VisualElement();
            card.style.width = 200;
            card.style.marginRight = 8;
            card.style.marginBottom = 8;
            card.style.paddingLeft = 10;
            card.style.paddingRight = 10;
            card.style.paddingTop = 8;
            card.style.paddingBottom = 8;
            card.style.backgroundColor = UiColors.SurfaceAlt;
            card.style.borderBottomLeftRadius = 6;
            card.style.borderBottomRightRadius = 6;
            card.style.borderTopLeftRadius = 6;
            card.style.borderTopRightRadius = 6;

            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.marginBottom = 4;
            var num = new Label(number);
            num.style.fontSize = 14;
            num.style.unityFontStyleAndWeight = FontStyle.Bold;
            num.style.color = UiColors.TimelineActive;
            num.style.marginRight = 6;
            header.Add(num);
            var titleLabel = new Label(title);
            titleLabel.style.fontSize = 11;
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.color = UiColors.TextPrimary;
            header.Add(titleLabel);
            card.Add(header);

            var descLabel = new Label(desc);
            descLabel.style.fontSize = 10;
            descLabel.style.color = UiColors.TextSecondary;
            descLabel.style.whiteSpace = WhiteSpace.Normal;
            descLabel.style.marginBottom = 6;
            card.Add(descLabel);
            card.Add(button);
            return card;
        }

        private VisualElement CreateInspectorBody()
        {
            var body = new VisualElement();
            body.style.flexDirection = FlexDirection.Column;
            body.style.flexGrow = 1;
            body.style.minHeight = 0;

            var fields = CreatePanel("\u2699 基础字段");
            fields.style.marginBottom = 8;
            fields.Add(SectionTitle("Frame"));
            fields.Add(CreateFrameScrubber());
            fields.Add(SectionTitle("Action"));
            _actionFieldsRoot = new VisualElement();
            fields.Add(_actionFieldsRoot);
            fields.Add(SectionTitle("Scene Binding"));
            _bindingFieldsRoot = new VisualElement();
            fields.Add(_bindingFieldsRoot);
            body.Add(fields);

            var selected = CreatePanel("\u25B6 选中项");
            selected.style.marginBottom = 8;
            _detailRoot = new ScrollView(ScrollViewMode.Vertical);
            _detailRoot.style.flexGrow = 1;
            _detailRoot.style.minHeight = 160;
            _detailRoot.style.maxHeight = 420;
            selected.Add(_detailRoot);
            body.Add(selected);

            return body;
        }

        private VisualElement CreateTimelineBody()
        {
            var body = new VisualElement();
            body.style.flexDirection = FlexDirection.Column;
            body.style.flexGrow = 1;
            body.style.minHeight = 0;
            body.style.minWidth = 0;

            var center = CreatePanel("\u23F1 Timeline");
            center.style.flexGrow = 1;
            center.style.flexShrink = 1;
            center.style.minWidth = 0;
            center.style.minHeight = 0;
            center.style.overflow = Overflow.Hidden;
            center.Add(CreateFrameScrubber());
            center.Add(CreateTimelineStripView());
            center.Add(SectionTitle("详细信息"));
            _timelineList = new ListView(_timelineRows, TimelineRowHeight, MakeTimelineRow, BindTimelineRow)
            {
                selectionType = SelectionType.Single,
                virtualizationMethod = CollectionVirtualizationMethod.FixedHeight,
            };
            _timelineList.style.height = TimelineDetailsHeight;
            _timelineList.style.flexGrow = 0;
            _timelineList.style.flexShrink = 0;
            _timelineList.style.minHeight = TimelineDetailsHeight;
            _timelineList.style.borderBottomLeftRadius = 4;
            _timelineList.style.borderBottomRightRadius = 4;
            _timelineList.style.borderTopLeftRadius = 4;
            _timelineList.style.borderTopRightRadius = 4;
            _timelineList.selectionChanged += OnTimelineSelectionChanged;
            _timelineList.selectedIndicesChanged += OnTimelineSelectedIndicesChanged;
            center.Add(_timelineList);
            body.Add(center);

            return body;
        }

        private VisualElement CreateFrameScrubber()
        {
            var root = new VisualElement();
            root.style.flexDirection = FlexDirection.Row;
            root.style.alignItems = Align.Center;
            root.style.marginBottom = 6;

            _frameSlider = new SliderInt("Frame", 0, 0);
            _frameSlider.tooltip = Tooltip("Frame");
            _frameSlider.style.flexGrow = 1;
            _frameSlider.style.flexShrink = 1;
            _frameSlider.style.minWidth = 0;
            _frameSlider.RegisterValueChangedCallback(evt =>
            {
                if (_frameLabel != null)
                {
                    _frameLabel.text = "当前帧 " + evt.newValue;
                }

                CombatAuthoringSceneState.SetFrame(evt.newValue);
            });
            root.Add(_frameSlider);

            _frameLabel = new Label("当前帧 0");
            _frameLabel.style.width = 96;
            _frameLabel.style.flexShrink = 0;
            _frameLabel.style.marginLeft = 8;
            root.Add(_frameLabel);

            return root;
        }

        private VisualElement CreateTimelineStripView()
        {
            var root = new VisualElement();
            root.style.flexGrow = 0;
            root.style.flexShrink = 0;
            root.style.height = TimelineStripMinHeight;
            root.style.minHeight = TimelineStripMinHeight;
            root.style.marginBottom = 10;

            _timelineStripEmptyRoot = new HelpBox("请选择 Action Asset 后查看横向时间轴。", HelpBoxMessageType.Info);
            _timelineStripEmptyRoot.style.marginBottom = 4;
            root.Add(_timelineStripEmptyRoot);

            var timelineRow = new VisualElement();
            timelineRow.style.flexDirection = FlexDirection.Row;
            timelineRow.style.flexGrow = 1;
            timelineRow.style.minHeight = 0;

            _timelineStripScroll = new ScrollView(ScrollViewMode.VerticalAndHorizontal);
            _timelineStripScroll.style.flexGrow = 1;
            _timelineStripScroll.style.minHeight = 0;
            _timelineStripScroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            _timelineStripScroll.style.borderBottomColor = UiColors.TimelineScrollBorder;
            _timelineStripScroll.style.borderLeftColor = UiColors.TimelineScrollBorder;
            _timelineStripScroll.style.borderRightColor = UiColors.TimelineScrollBorder;
            _timelineStripScroll.style.borderTopColor = UiColors.TimelineScrollBorder;
            _timelineStripScroll.style.borderBottomWidth = 1;
            _timelineStripScroll.style.borderLeftWidth = 1;
            _timelineStripScroll.style.borderRightWidth = 1;
            _timelineStripScroll.style.borderTopWidth = 1;
            _timelineStripScroll.style.borderBottomLeftRadius = 4;
            _timelineStripScroll.style.borderBottomRightRadius = 4;
            _timelineStripScroll.style.borderTopLeftRadius = 4;
            _timelineStripScroll.style.borderTopRightRadius = 4;
            _timelineStripContent = new VisualElement();
            _timelineStripContent.style.position = Position.Relative;
            _timelineStripScroll.Add(_timelineStripContent);
            timelineRow.Add(_timelineStripScroll);
            root.Add(timelineRow);
            root.Add(CreateTimelineViewportControl());

            return root;
        }

        private VisualElement CreateTimelineViewportControl()
        {
            var root = new VisualElement();
            root.style.flexDirection = FlexDirection.Row;
            root.style.alignItems = Align.Center;
            root.style.marginTop = 6;

            var label = new Label("View");
            label.tooltip = Tooltip("Timeline View Range");
            label.style.fontSize = 10;
            label.style.color = UiColors.TextSecondary;
            label.style.width = 44;
            label.style.flexShrink = 0;
            root.Add(label);

            _timelineViewportSlider = new MinMaxSlider(
                0f,
                1f,
                0f,
                1f)
            {
                tooltip = Tooltip("Timeline View Range"),
            };
            _timelineViewportSlider.style.flexGrow = 1;
            _timelineViewportSlider.style.flexShrink = 1;
            _timelineViewportSlider.style.minWidth = 0;
            _timelineViewportSlider.RegisterCallback<PointerDownEvent>(BeginTimelineViewportInteraction, TrickleDown.TrickleDown);
            _timelineViewportSlider.RegisterCallback<PointerUpEvent>(EndTimelineViewportInteraction, TrickleDown.TrickleDown);
            _timelineViewportSlider.RegisterCallback<PointerCancelEvent>(EndTimelineViewportInteraction, TrickleDown.TrickleDown);
            _timelineViewportSlider.RegisterCallback<PointerCaptureOutEvent>(EndTimelineViewportInteraction, TrickleDown.TrickleDown);
            _timelineViewportSlider.RegisterValueChangedCallback(OnTimelineViewportRangeChanged);
            root.Add(_timelineViewportSlider);

            _timelineViewportLabel = new Label();
            _timelineViewportLabel.style.width = 112;
            _timelineViewportLabel.style.flexShrink = 0;
            _timelineViewportLabel.style.marginLeft = 8;
            _timelineViewportLabel.style.fontSize = 10;
            _timelineViewportLabel.style.color = UiColors.TextSecondary;
            root.Add(_timelineViewportLabel);

            UpdateTimelineViewportControl();
            return root;
        }

        private VisualElement CreateIssueDrawer()
        {
            var drawer = CreatePanel("Validation Report");
            drawer.tooltip = Tooltip("Validation Report");
            drawer.style.height = _mode == CombatAuthoringWindowMode.Timeline ? 0 : ValidationReportHeight;
            drawer.style.display = _mode == CombatAuthoringWindowMode.Timeline ? DisplayStyle.None : DisplayStyle.Flex;
            drawer.style.marginTop = 8;

            var split = new VisualElement();
            split.style.flexDirection = _mode == CombatAuthoringWindowMode.Inspector ? FlexDirection.Column : FlexDirection.Row;
            split.style.flexGrow = 1;
            split.style.minHeight = 0;

            var issueColumn = new VisualElement();
            issueColumn.style.flexGrow = _mode == CombatAuthoringWindowMode.Inspector ? 0 : 1;
            issueColumn.style.flexShrink = 0;
            if (_mode == CombatAuthoringWindowMode.Inspector)
            {
                issueColumn.style.height = IssueRowHeight + 42f;
            }
            else
            {
                issueColumn.style.flexBasis = 0;
            }
            issueColumn.style.minWidth = 0;
            issueColumn.style.marginRight = _mode == CombatAuthoringWindowMode.Inspector ? 0 : 8;
            issueColumn.style.marginBottom = _mode == CombatAuthoringWindowMode.Inspector ? 8 : 0;
            issueColumn.Add(SectionTitle("Issues"));

            _issueList = new ListView(_issueRows, IssueRowHeight, MakeIssueRow, BindIssueRow)
            {
                selectionType = SelectionType.Single,
                virtualizationMethod = CollectionVirtualizationMethod.FixedHeight,
            };
            _issueList.style.flexGrow = 1;
            _issueList.style.height = _mode == CombatAuthoringWindowMode.Inspector ? IssueRowHeight + 12f : StyleKeyword.Auto;
            _issueList.style.minHeight = _mode == CombatAuthoringWindowMode.Inspector ? IssueRowHeight + 12f : 0f;
            issueColumn.Add(_issueList);
            split.Add(issueColumn);

            var previewColumn = new VisualElement();
            if (_mode == CombatAuthoringWindowMode.Inspector)
            {
                previewColumn.style.width = StyleKeyword.Auto;
                previewColumn.style.flexGrow = 1;
                previewColumn.style.minHeight = 0;
            }
            else
            {
                previewColumn.style.width = 340;
            }

            previewColumn.style.minWidth = 0;
            previewColumn.style.flexShrink = 0;
            previewColumn.Add(SectionTitle("Report Preview"));

            _reportPreview = new TextField
            {
                multiline = true,
                isReadOnly = true,
            };
            _reportPreview.tooltip = Tooltip("Report Preview");
            _reportPreview.style.flexGrow = 1;
            _reportPreview.style.minHeight = 0;
            previewColumn.Add(_reportPreview);

            previewColumn.Add(SectionTitle("Preview Explain"));
            _previewExplain = new TextField
            {
                multiline = true,
                isReadOnly = true,
            };
            _previewExplain.tooltip = Tooltip("Preview Explain");
            _previewExplain.style.flexGrow = 1;
            _previewExplain.style.minHeight = 0;
            _previewExplain.value = "点击“预览 Explain”生成当前帧 query / candidate / resolve 解释。";
            previewColumn.Add(_previewExplain);
            split.Add(previewColumn);

            drawer.Add(split);
            return drawer;
        }

        private static Button CreateMiniButton(string text, string tooltip, Action action)
        {
            var button = new Button(action)
            {
                text = text,
                tooltip = tooltip,
            };
            button.style.marginLeft = 3;
            button.style.marginRight = 0;
            button.style.marginBottom = 2;
            button.style.minWidth = 26;
            return button;
        }

        private static VisualElement CreatePanel(string title)
        {
            var panel = new VisualElement();
            panel.style.backgroundColor = UiColors.Surface;
            panel.style.borderBottomColor = UiColors.Border;
            panel.style.borderLeftColor = UiColors.Border;
            panel.style.borderRightColor = UiColors.Border;
            panel.style.borderTopColor = UiColors.Border;
            panel.style.borderBottomWidth = 1;
            panel.style.borderLeftWidth = 1;
            panel.style.borderRightWidth = 1;
            panel.style.borderTopWidth = 1;
            panel.style.borderBottomLeftRadius = 6;
            panel.style.borderBottomRightRadius = 6;
            panel.style.borderTopLeftRadius = 6;
            panel.style.borderTopRightRadius = 6;
            panel.style.paddingBottom = 8;
            panel.style.paddingLeft = 10;
            panel.style.paddingRight = 10;
            panel.style.paddingTop = 8;

            panel.Add(SectionTitle(title));
            return panel;
        }

        private static Label SectionTitle(string text)
        {
            var label = new Label(text);
            label.tooltip = Tooltip(text);
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.color = UiColors.TextPrimary;
            label.style.fontSize = 12;
            label.style.marginTop = 2;
            label.style.marginBottom = 6;
            label.style.paddingBottom = 4;
            label.style.borderBottomWidth = 1;
            label.style.borderBottomColor = UiColors.Border;
            return label;
        }

        private static string Tooltip(string key)
        {
            switch (key)
            {
                case "Action Asset":
                    return "战斗动作配置资产。这里记录动作帧数、阶段、Hitbox、Hurtbox 和武器轨迹等编辑数据。";
                case "Scene Binding":
                    return "场景绑定配置。把 Authoring 数据里的 marker / actor / collider 绑定到当前场景对象，用于预览、校验和导出说明。";
                case "使用当前选择":
                    return "从 Project 当前选中的 Combat Action 或 Scene Binding 填入窗口。";
                case "打开 Timeline":
                    return "打开 Combat Timeline 专用窗口，使用更宽的横向时间轴查看和拖动帧范围。";
                case "创建 Action Asset":
                    return "创建新的 Combat Action Authoring Asset，用来编辑一个战斗动作。";
                case "创建 Binding":
                case "创建 Scene Binding":
                    return "创建新的 Combat Scene Binding Asset，用来保存场景对象与战斗 marker 的绑定关系。";
                case "从当前场景生成 Binding":
                    return "扫描当前场景中名称包含 Player、Enemy、Marker 或 Combat 的对象，自动生成 marker / actor 绑定草案。";
                case "重连选中对象":
                    return "把当前 Scene 中选中的 Transform 写入 Binding，作为一个可被 Authoring 数据引用的 marker。";
                case "使用当前选择重连":
                    return "把当前 Scene 中选中的 Transform 写入 Binding，并把当前 Shape 的 markerId 指向这个 marker。";
                case "添加 Hitbox":
                    return "在当前帧添加一个攻击判定体。创建后可在时间轴选中，并在 Scene View 用手柄编辑位置和大小。";
                case "添加 Hurtbox":
                    return "在当前帧添加一个受击判定体。创建后可在时间轴选中，并在 Scene View 用手柄编辑位置和大小。";
                case "定位 Action":
                    return "在 Project 中高亮当前 Action Asset。";
                case "定位 Binding":
                    return "在 Project 中高亮当前 Scene Binding Asset。";
                case "执行验证":
                    return "重新检查 Action 和 Binding 的结构、帧范围、marker 引用、shape 参数等问题。";
                case "运行 Showcase 预览":
                    return "进入 Combat 测试场景并把当前 Action / Binding 应用到 Runtime Showcase。这是测试预览，不是导出 Runtime 数据。";
                case "清除预览会话":
                    return "清除本次 Authoring -> Runtime Showcase 测试预览会话，不会修改 Runtime 数据或 Authoring Asset。";
                case "复制报告":
                    return "复制当前 Validation Report，便于粘贴给策划或开发定位问题。";
                case "导出 JSON":
                    return "执行 validation gate，通过后生成 JSON 包，并可选择目录写出 manifest、schema、action、binding 和报告文件。";
                case "复制导出报告":
                    return "复制最近一次导出报告；如果还没导出，会先在内存中生成一次报告但不写磁盘。";
                case "预览 Explain":
                    return "用当前帧和当前 Scene Binding 生成 Query / Candidate / Resolve 的解释报告，帮助确认判定链路。";
                case "复制 Explain":
                    return "复制最近一次 Preview Explain 文本，便于记录或发给开发排查。";
                case "Action":
                    return "动作基础数据分组，包括动作编号、总帧数和三个阶段帧范围。";
                case "基础字段":
                    return "当前 Action 和 Scene Binding 的核心字段。数组字段可以展开查看，但复杂编辑优先通过时间轴和 Scene View。";
                case "Timeline":
                    return "固定帧时间轴。横向条展示 Startup、Active、Recovery、Hitbox、Hurtbox 和 Trace 的生效帧。";
                case "详情列表":
                    return "当前时间轴条目的列表视图。选择一行后，右侧会显示可轻编辑字段。";
                case "选中项":
                    return "当前选中时间轴条目的字段详情。Shape 字段可配合 Scene View 手柄一起调整。";
                case "Validation Report":
                    return "验证报告区域。左侧是问题列表，右侧是可复制的完整报告和预览解释。";
                case "Issues":
                    return "验证发现的问题列表。Error 会阻断导出，Warning 允许导出但应尽量处理。";
                case "Report Preview":
                    return "完整验证或导出报告预览。内容可用复制按钮带走。";
                case "Preview Explain":
                    return "当前帧的战斗判定解释，包括 query、candidate、resolve 和稳定 hash。";
                case "Frame":
                    return "当前预览帧。拖动后会同步 Scene View 的 Authoring 预览。";
                case "Frame Width":
                    return "每一帧在 Timeline 中占用的像素宽度。调大后可以横向滚动，调小后仍会至少填满当前窗口宽度。";
                case "Timeline View Range":
                    return "双端滑块表示当前 Timeline 可视帧范围。拖动整体可横向滚动；拖动任一端改变范围宽度，同时改变每帧像素宽度。";
                case "Action Id":
                    return "动作编号。后续导出文件名和运行时索引会使用这个稳定 ID。";
                case "Total Frames":
                    return "动作总帧数。时间轴和帧滑条会按这个值限制范围。";
                case "Schema Version":
                    return "Authoring 数据结构版本。用于后续兼容和迁移。";
                case "Startup":
                    return "动作前摇帧范围。一般用于起手、准备和可被打断的阶段。";
                case "Active":
                    return "动作生效帧范围。攻击判定或关键效果通常发生在这里。";
                case "Recovery":
                    return "动作收招帧范围。一般用于硬直、恢复和后续衔接。";
                case "Scene Guid":
                    return "绑定所属场景的稳定 GUID。生成 Binding 时会自动填入当前场景。";
                case "Binding Profile Id":
                    return "绑定配置名。导出文件名会使用它生成稳定路径。";
                case "Actors":
                    return "参与预览的战斗实体绑定，例如玩家、敌人及其 body / collider。";
                case "Markers":
                    return "可被 Action 数据引用的场景 marker 列表。markerId 必须和 Hitbox、Hurtbox、Trace 引用一致。";
                case "Shape 类型":
                    return "判定体形状类型。当前优先支持 Sphere 和 Capsule 的 Scene View 手柄编辑。";
                case "生效帧范围":
                    return "该条判定体在哪些固定帧生效。起止帧都包含在内。";
                case "绑定 Marker":
                    return "该判定体跟随的 markerId。优先从 Scene Binding 的 marker 下拉选择；找不到时会在窗口内提示。";
                case "Marker Id（高级）":
                    return "高级文本入口。仅在需要手动填写稳定 markerId 时使用；普通编辑优先用 marker 下拉或重连按钮。";
                case "Frame Start":
                    return "生效帧起点。修改时自动 clamp 到 Action TotalFrames 范围内；如果超过 End 会同步修正 End。";
                case "Frame End":
                    return "生效帧终点。修改时自动 clamp 到 Action TotalFrames 范围内；如果小于 Start 会同步修正 Start。";
                case "Frame Step Down":
                    return "向前微调 1 帧，并自动 clamp 到合法范围。";
                case "Frame Step Up":
                    return "向后微调 1 帧，并自动 clamp 到合法范围。";
                case "本地中心":
                    return "相对绑定 marker 的本地空间中心偏移。可用 Scene View 移动手柄调整。";
                case "半径 Raw":
                    return "半径的 fixed raw 值，1,000,000 等于 1 Unity unit。";
                case "Capsule 高度 Raw":
                    return "胶囊总高度的 fixed raw 值，1,000,000 等于 1 Unity unit。建议不小于直径。";
                case "高度=直径":
                    return "把 Capsule heightRaw 修正为 radiusRaw * 2，消除高度小于直径的 warning。";
                case "Raw Preset":
                    return "常用 raw 预设按钮，用点击替代手动输入数值。";
                case "排序":
                    return "稳定排序用字段。用于确保验证、预览和导出结果不依赖 Unity 对象返回顺序。";
                default:
                    return string.Empty;
            }
        }

        private static string BuildTimelineTooltip(TimelineRow row)
        {
            return "类型：" + TimelineSectionName(row.Section)
                + "\n条目：" + TimelineLabelName(row.Label)
                + "\nTrackId：" + row.TrackId
                + "\n帧范围：" + FormatRange(row.FrameRange)
                + "\n字段路径：" + row.PropertyPath;
        }

        private static string TimelineSectionName(string section)
        {
            switch (section)
            {
                case "Action":
                    return "动作阶段";
                case "Hitbox":
                    return "攻击判定体";
                case "Hurtbox":
                    return "受击判定体";
                case "WeaponTrace":
                    return "武器轨迹";
                default:
                    return section;
            }
        }

        private static string TimelineLabelName(string label)
        {
            switch (label)
            {
                case "Startup":
                    return "前摇";
                case "Active":
                    return "生效";
                case "Recovery":
                    return "收招";
                default:
                    return label;
            }
        }

        private static ObjectField CreateCompactObjectField(string label, Type objectType)
        {
            var field = new ObjectField(label)
            {
                objectType = objectType,
                allowSceneObjects = false,
            };
            field.style.width = 360;
            field.style.minWidth = 280;
            field.style.height = 22;
            field.style.marginRight = 8;
            field.style.marginBottom = 4;
            field.labelElement.style.minWidth = 88;
            field.labelElement.style.width = 88;
            field.tooltip = Tooltip(label);
            return field;
        }

        private static Button CreateButton(string text, Action action)
        {
            var button = new Button(action) { text = text };
            button.tooltip = Tooltip(text);
            button.style.marginRight = 4;
            button.style.marginBottom = 4;
            button.style.paddingLeft = 10;
            button.style.paddingRight = 10;
            button.style.paddingTop = 4;
            button.style.paddingBottom = 4;
            button.style.borderBottomLeftRadius = 4;
            button.style.borderBottomRightRadius = 4;
            button.style.borderTopLeftRadius = 4;
            button.style.borderTopRightRadius = 4;
            button.style.fontSize = 11;
            button.style.backgroundColor = UiColors.SurfaceAlt;
            button.style.borderBottomColor = UiColors.Border;
            button.style.borderLeftColor = UiColors.Border;
            button.style.borderRightColor = UiColors.Border;
            button.style.borderTopColor = UiColors.Border;
            button.style.borderBottomWidth = 1;
            button.style.borderLeftWidth = 1;
            button.style.borderRightWidth = 1;
            button.style.borderTopWidth = 1;
            button.RegisterCallback<PointerEnterEvent>(_ => OnButtonHover(button, true));
            button.RegisterCallback<PointerLeaveEvent>(_ => OnButtonHover(button, false));
            return button;
        }

        private static void OnButtonHover(VisualElement button, bool hover)
        {
            button.style.backgroundColor = hover ? UiColors.ButtonHover : UiColors.SurfaceAlt;
            button.style.borderBottomColor = hover ? UiColors.TextSecondary : UiColors.Border;
            button.style.borderLeftColor = hover ? UiColors.TextSecondary : UiColors.Border;
            button.style.borderRightColor = hover ? UiColors.TextSecondary : UiColors.Border;
            button.style.borderTopColor = hover ? UiColors.TextSecondary : UiColors.Border;
        }

        private static void AddProperty(VisualElement root, SerializedObject serialized, string propertyName, string label)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null)
            {
                root.Add(new HelpBox("缺少字段：" + propertyName, HelpBoxMessageType.Warning));
                return;
            }

            var field = new PropertyField(property, label);
            field.tooltip = Tooltip(label);
            root.Add(field);
        }

        private static Label CreateTimelineCell(string text, float left, float top, float width, float height, Color backgroundColor)
        {
            var label = new Label(text);
            label.tooltip = Tooltip(text);
            label.style.position = Position.Absolute;
            label.style.left = left;
            label.style.top = top;
            label.style.width = width;
            label.style.height = height;
            label.style.paddingLeft = 6;
            label.style.paddingRight = 4;
            label.style.unityTextAlign = TextAnchor.MiddleLeft;
            label.style.whiteSpace = WhiteSpace.NoWrap;
            label.style.overflow = Overflow.Hidden;
            label.style.fontSize = 12;
            label.style.color = UiColors.TimelineHeaderText;
            label.style.backgroundColor = backgroundColor;
            return label;
        }

        private static VisualElement CreateTimelineBlock(float left, float top, float width, float height, Color color)
        {
            var block = new VisualElement();
            block.style.position = Position.Absolute;
            block.style.left = left;
            block.style.top = top;
            block.style.width = width;
            block.style.height = height;
            block.style.backgroundColor = color;
            return block;
        }

        private static VisualElement CreateTimelineRangeHandle(bool left)
        {
            var handle = new VisualElement();
            handle.pickingMode = PickingMode.Ignore;
            handle.style.position = Position.Absolute;
            handle.style.top = 0;
            handle.style.width = TimelineRangeEdgeHandleWidth;
            handle.style.height = Length.Percent(100);
            handle.style.backgroundColor = UiColors.HandleDefault;
            if (left)
            {
                handle.style.left = 0;
            }
            else
            {
                handle.style.right = 0;
            }

            return handle;
        }

        private static void SetTimelineHandleOpacity(VisualElement leftHandle, VisualElement rightHandle, float opacity)
        {
            if (leftHandle != null)
            {
                leftHandle.style.opacity = opacity;
            }

            if (rightHandle != null)
            {
                rightHandle.style.opacity = opacity;
            }
        }

        private static Color GetTimelineBarColor(TimelineRow row)
        {
            if (string.Equals(row.Section, "Action", StringComparison.Ordinal))
            {
                if (string.Equals(row.Label, "Startup", StringComparison.Ordinal))
                {
                    return UiColors.BarStartup;
                }

                if (string.Equals(row.Label, "Active", StringComparison.Ordinal))
                {
                    return UiColors.BarActive;
                }

                return UiColors.BarRecovery;
            }

            if (string.Equals(row.Section, "Hitbox", StringComparison.Ordinal))
            {
                return UiColors.BarHitbox;
            }

            if (string.Equals(row.Section, "Hurtbox", StringComparison.Ordinal))
            {
                return UiColors.BarHurtbox;
            }

            return UiColors.BarTrace;
        }
    }
}
