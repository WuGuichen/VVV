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
            var scroll = new ScrollView(ScrollViewMode.VerticalAndHorizontal);
            scroll.style.flexGrow = 1;
            scroll.style.minHeight = 0;
            scroll.style.minWidth = 0;
            scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            rootVisualElement.Add(scroll);

            var content = new VisualElement();
            content.style.flexGrow = 1;
            content.style.minHeight = 0;
            content.style.minWidth = 0;
            content.style.paddingLeft = 12;
            content.style.paddingRight = 12;
            content.style.paddingTop = 10;
            content.style.paddingBottom = 10;
            scroll.Add(content);

            content.Add(CreateContextBar());
            content.Add(CreateTimelineBody());
        }

        // ================================================================
        //  Context Bar — asset selection + status summary
        // ================================================================
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

            // Top accent bar
            var accentBar = new VisualElement();
            accentBar.style.height = 2.5f;
            accentBar.style.marginBottom = 6;
            accentBar.style.backgroundColor = UiColors.TimelineActive;
            accentBar.style.borderBottomLeftRadius = 2;
            accentBar.style.borderBottomRightRadius = 2;
            root.Add(accentBar);

            // Asset fields row
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

            // Status context label with subtle top separator
            _contextLabel = new Label();
            _contextLabel.style.minWidth = 460;
            _contextLabel.style.marginTop = 6;
            _contextLabel.style.whiteSpace = WhiteSpace.Normal;
            _contextLabel.style.fontSize = 11;
            _contextLabel.style.color = UiColors.TextSecondary;
            _contextLabel.style.paddingTop = 5;
            _contextLabel.style.borderTopWidth = 1;
            _contextLabel.style.borderTopColor = UiColors.Border;
            root.Add(_contextLabel);

            return root;
        }

        // ================================================================
        //  Toolbar — action buttons organized by category
        // ================================================================
        private VisualElement CreateToolbar()
        {
            var root = new VisualElement();
            _toolbarRoot = root;
            root.style.marginBottom = 8;

            // --- Layer 1: 常用操作（大按钮，带图标） ---
            root.Add(CreateToolbarSection("常用操作",
                CreateButtonWithIcon("⚡ 使用当前选择", UseSelection, isPrimary: false),
                CreateButtonWithIcon("✨ 创建 Action Asset", CreateActionAsset, isPrimary: true),
                CreateButtonWithIcon("🎬 打开 Timeline", () => OpenTimelineWindow(), isPrimary: false),
                CreateButtonWithIcon("📍 定位 Action", () => PingObject(_actionAsset), isPrimary: false),
                CreateButtonWithIcon("📍 定位 Binding", () => PingObject(_sceneBindingAsset), isPrimary: false)));

            // --- Layer 2: 创建与编辑 ---
            root.Add(CreateToolbarSection("创建与编辑",
                CreateButtonWithIcon("➕ 创建 Binding", CreateBindingAsset, isPrimary: false),
                CreateButtonWithIcon("🔍 从当前场景生成", GenerateBindingFromCurrentScene, isPrimary: false),
                CreateButtonWithIcon("🔗 重连选中对象", RelinkSelectedTransform, isPrimary: false),
                CreateButtonWithIcon("🗡️ 添加 Hitbox", () => AddShape("Hitbox", "hitboxes"), isPrimary: false),
                CreateButtonWithIcon("🛡️ 添加 Hurtbox", () => AddShape("Hurtbox", "hurtboxes"), isPrimary: false)));

            // --- Layer 3: 高级工具（折叠面板） ---
            var advancedSection = CreateToolbarSection("高级工具",
                CreateButtonWithIcon("✅ 执行验证", RefreshValidation, isPrimary: true),
                CreateButtonWithIcon("▶️ 运行 Showcase 预览", RunShowcasePreview, isPrimary: false),
                CreateButtonWithIcon("🧹 清除预览会话", ClearShowcasePreviewSession, isPrimary: false),
                CreateButtonWithIcon("📋 复制报告", CopyReport, isPrimary: false),
                CreateButtonWithIcon("📦 导出 JSON", ExportJson, isPrimary: false),
                CreateButtonWithIcon("📄 复制导出报告", CopyExportReport, isPrimary: false),
                CreateButtonWithIcon("💡 预览 Explain", PreviewExplain, isPrimary: false),
                CreateButtonWithIcon("📝 复制 Explain", CopyExplain, isPrimary: false));
            advancedSection.style.marginBottom = 0;

            // Validation status row — 增强版
            var statusRow = CreateValidationStatusRow();
            root.Add(statusRow);

            return root;
        }

        // ================================================================
        //  Toolbar Section — modern card with icon prefix support
        // ================================================================
        private static VisualElement CreateToolbarSection(string title, params VisualElement[] buttons)
        {
            var section = new VisualElement();
            section.style.marginBottom = 6;

            // Section header
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.marginBottom = 4;

            var titleIcon = new Label("▸");
            titleIcon.style.fontSize = 10;
            titleIcon.style.color = UiColors.TimelineActive;
            titleIcon.style.marginRight = 4;
            titleIcon.style.width = 14;
            titleIcon.style.unityTextAlign = TextAnchor.MiddleCenter;
            header.Add(titleIcon);

            var titleLabel = new Label(title);
            titleLabel.style.fontSize = 11;
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.color = UiColors.TextPrimary;
            header.Add(titleLabel);

            section.Add(header);

            // Button grid
            var grid = new VisualElement();
            grid.style.flexDirection = FlexDirection.Row;
            grid.style.flexWrap = Wrap.Wrap;
            foreach (var button in buttons)
            {
                grid.Add(button);
            }
            section.Add(grid);

            return section;
        }

        // ================================================================
        //  Validation Status Row — enhanced with color bar + summary
        // ================================================================
        private VisualElement CreateValidationStatusRow()
        {
            var statusRow = new VisualElement();
            statusRow.style.flexDirection = FlexDirection.Row;
            statusRow.style.alignItems = Align.Center;
            statusRow.style.marginTop = 6;
            statusRow.style.paddingTop = 6;
            statusRow.style.paddingLeft = 8;
            statusRow.style.paddingRight = 8;
            statusRow.style.backgroundColor = UiColors.SurfaceDeep;
            statusRow.style.borderBottomLeftRadius = 4;
            statusRow.style.borderBottomRightRadius = 4;
            statusRow.style.borderTopLeftRadius = 4;
            statusRow.style.borderTopRightRadius = 4;

            // Status indicator bar (color-coded)
            var statusIndicator = new VisualElement();
            statusIndicator.style.width = 4;
            statusIndicator.style.height = 18;
            statusIndicator.style.marginRight = 8;
            statusIndicator.style.borderBottomLeftRadius = 2;
            statusIndicator.style.borderTopLeftRadius = 2;
            statusIndicator.style.backgroundColor = UiColors.SuccessGreen; // default
            _validationStatusIndicator = statusIndicator;
            statusRow.Add(statusIndicator);

            _validationLabel = new Label();
            _validationLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _validationLabel.style.fontSize = 12;
            _validationLabel.style.color = UiColors.TextPrimary;
            _validationLabel.style.paddingLeft = 2;
            statusRow.Add(_validationLabel);

            _quickActionStatusLabel = new Label();
            _quickActionStatusLabel.style.marginLeft = 10;
            _quickActionStatusLabel.style.fontSize = 11;
            _quickActionStatusLabel.style.color = UiColors.TextSecondary;
            _quickActionStatusLabel.style.whiteSpace = WhiteSpace.Normal;
            _quickActionStatusLabel.style.flexGrow = 1;
            statusRow.Add(_quickActionStatusLabel);

            return statusRow;
        }

        // ================================================================
        //  Toolbar Group — card with left accent bar
        // ================================================================
        private static VisualElement CreateToolbarGroup(string title, params VisualElement[] buttons)
        {
            var group = new VisualElement();
            group.style.marginBottom = 6;
            group.style.backgroundColor = UiColors.SurfaceDeep;
            group.style.borderBottomLeftRadius = 6;
            group.style.borderBottomRightRadius = 6;
            group.style.borderTopLeftRadius = 6;
            group.style.borderTopRightRadius = 6;
            group.style.paddingLeft = 8;
            group.style.paddingRight = 8;
            group.style.paddingTop = 6;
            group.style.paddingBottom = 6;

            // Left accent bar
            group.style.borderLeftWidth = 3;
            group.style.borderLeftColor = UiColors.TimelineActive;

            var label = new Label(title);
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.fontSize = 10;
            label.style.color = UiColors.TextSecondary;
            label.style.unityTextAlign = TextAnchor.MiddleLeft;
            label.style.marginBottom = 4;
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

        // ================================================================
        //  Empty State — hero + welcome + quick action cards
        // ================================================================
        private VisualElement CreateEmptyState()
        {
            _emptyStateRoot = CreatePanel("🎯 快速开始");
            _emptyStateRoot.style.marginBottom = 8;

            // Hero section
            var hero = new VisualElement();
            hero.style.alignItems = Align.Center;
            hero.style.paddingTop = 16;
            hero.style.paddingBottom = 12;

            var heroIcon = new Label("⚔️");
            heroIcon.style.fontSize = 40;
            heroIcon.style.marginBottom = 8;
            hero.Add(heroIcon);

            var heroTitle = new Label("Combat Authoring");
            heroTitle.style.fontSize = 20;
            heroTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            heroTitle.style.color = UiColors.TextPrimary;
            heroTitle.style.marginBottom = 6;
            hero.Add(heroTitle);

            var heroDesc = new Label("选择或创建 Action Asset 开始编辑战斗动作。Scene Binding 可选，选择后可以校验 marker。");
            heroDesc.style.whiteSpace = WhiteSpace.Normal;
            heroDesc.style.color = UiColors.TextSecondary;
            heroDesc.style.maxWidth = 520;
            heroDesc.style.unityTextAlign = TextAnchor.MiddleCenter;
            heroDesc.style.fontSize = 11;
            hero.Add(heroDesc);
            _emptyStateRoot.Add(hero);

            // Step cards row
            var stepsRow = new VisualElement();
            stepsRow.style.flexDirection = FlexDirection.Row;
            stepsRow.style.justifyContent = Justify.Center;
            stepsRow.style.flexWrap = Wrap.Wrap;
            stepsRow.style.marginTop = 12;
            stepsRow.style.paddingLeft = 4;
            stepsRow.style.paddingRight = 4;

            stepsRow.Add(CreateStepCard("1", "选择 Action", "从 Project 窗口拖入、使用当前选择或新建资产",
                CreateButtonWithIcon("✨ 创建 Action Asset", CreateActionAsset, isPrimary: true)));
            stepsRow.Add(CreateStepCard("2", "创建 Binding", "绑定场景对象到 marker",
                CreateButtonWithIcon("➕ 创建 Binding", CreateBindingAsset, isPrimary: false)));
            stepsRow.Add(CreateStepCard("3", "编辑 & 验证", "添加 Hitbox、调整帧范围、执行验证",
                CreateButtonWithIcon("✅ 执行验证", RefreshValidation, isPrimary: false)));
            _emptyStateRoot.Add(stepsRow);

            // Quick actions section — compact grid
            var quickActionsTitle = new Label("⚡ 快捷操作");
            quickActionsTitle.style.fontSize = 11;
            quickActionsTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            quickActionsTitle.style.color = UiColors.TextPrimary;
            quickActionsTitle.style.marginTop = 12;
            quickActionsTitle.style.marginBottom = 6;
            quickActionsTitle.style.paddingLeft = 2;
            _emptyStateRoot.Add(quickActionsTitle);

            var quickActionsGrid = new VisualElement();
            quickActionsGrid.style.flexDirection = FlexDirection.Row;
            quickActionsGrid.style.flexWrap = Wrap.Wrap;
            quickActionsGrid.style.paddingTop = 2;
            quickActionsGrid.style.paddingLeft = 4;
            quickActionsGrid.style.paddingRight = 4;

            quickActionsGrid.Add(CreateCompactActionChip("🔍 从场景生成", "扫描场景自动生成绑定草案", GenerateBindingFromCurrentScene));
            quickActionsGrid.Add(CreateCompactActionChip("📌 使用当前选择", "从 Project 填入选中资产", UseSelection));
            quickActionsGrid.Add(CreateCompactActionChip("🔗 重连选中对象", "把 Scene 选中写入 Binding", RelinkSelectedTransform));
            quickActionsGrid.Add(CreateCompactActionChip("🗡️ 添加 Hitbox", "在当前帧添加攻击判定", () => AddShape("Hitbox", "hitboxes")));
            quickActionsGrid.Add(CreateCompactActionChip("🛡️ 添加 Hurtbox", "在当前帧添加受击判定", () => AddShape("Hurtbox", "hurtboxes")));
            quickActionsGrid.Add(CreateCompactActionChip("▶️ Showcase 预览", "进入测试场景预览效果", RunShowcasePreview));
            quickActionsGrid.Add(CreateCompactActionChip("📦 导出 JSON", "生成 Runtime JSON 包", ExportJson));
            quickActionsGrid.Add(CreateCompactActionChip("💡 预览 Explain", "生成判定解释报告", PreviewExplain));
            _emptyStateRoot.Add(quickActionsGrid);

            return _emptyStateRoot;
        }

        // ================================================================
        //  Compact Action Chip — small icon + text button for quick actions
        // ================================================================
        private static Button CreateCompactActionChip(string text, string tooltip, Action action)
        {
            var button = new Button(action);
            button.tooltip = tooltip;

            // Extract icon (first non-space chars before the space)
            int spaceIdx = text.IndexOf(' ');
            var iconLabel = new Label(spaceIdx >= 0 ? text.Substring(0, spaceIdx) : text);
            iconLabel.style.fontSize = 12;
            iconLabel.style.marginRight = 3;

            var textLabel = new Label(spaceIdx >= 0 ? text.Substring(spaceIdx + 1) : "");
            textLabel.style.fontSize = 10;

            button.Add(iconLabel);
            button.Add(textLabel);

            button.style.paddingLeft = 6;
            button.style.paddingRight = 6;
            button.style.paddingTop = 4;
            button.style.paddingBottom = 4;
            button.style.borderBottomLeftRadius = 4;
            button.style.borderBottomRightRadius = 4;
            button.style.borderTopLeftRadius = 4;
            button.style.borderTopRightRadius = 4;
            button.style.backgroundColor = UiColors.SurfaceAlt;
            button.style.borderBottomWidth = 1;
            button.style.borderLeftWidth = 1;
            button.style.borderRightWidth = 1;
            button.style.borderTopWidth = 1;
            button.style.borderBottomColor = UiColors.PanelBorder;
            button.style.borderLeftColor = UiColors.PanelBorder;
            button.style.borderRightColor = UiColors.PanelBorder;
            button.style.borderTopColor = UiColors.PanelBorder;
            button.style.flexDirection = FlexDirection.Row;
            button.style.alignItems = Align.Center;
            button.style.minWidth = 80;

            button.RegisterCallback<PointerEnterEvent>(_ => OnButtonHover(button, true, false));
            button.RegisterCallback<PointerLeaveEvent>(_ => OnButtonHover(button, false, false));
            return button;
        }

        // ================================================================
        //  Step Card — numbered guidance card for quick start
        // ================================================================
        private static VisualElement CreateStepCard(string number, string title, string desc, VisualElement button)
        {
            var card = new VisualElement();
            card.style.width = 210;
            card.style.marginRight = 8;
            card.style.marginBottom = 8;
            card.style.paddingLeft = 14;
            card.style.paddingRight = 14;
            card.style.paddingTop = 12;
            card.style.paddingBottom = 12;
            card.style.backgroundColor = UiColors.PanelSurfaceAlt;
            card.style.borderBottomLeftRadius = 10;
            card.style.borderBottomRightRadius = 10;
            card.style.borderTopLeftRadius = 10;
            card.style.borderTopRightRadius = 10;
            card.style.borderBottomWidth = 1;
            card.style.borderBottomColor = UiColors.PanelBorder;

            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.marginBottom = 8;

            // Number badge with gradient-like background
            var numLabel = new Label(number);
            numLabel.style.fontSize = 15;
            numLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            numLabel.style.color = UiColors.TimelineActive;
            numLabel.style.marginRight = 8;
            numLabel.style.minWidth = 26;
            numLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            numLabel.style.backgroundColor = new Color(0.15f, 0.15f, 0.18f);
            numLabel.style.borderBottomLeftRadius = 12;
            numLabel.style.borderBottomRightRadius = 12;
            numLabel.style.borderTopLeftRadius = 12;
            numLabel.style.borderTopRightRadius = 12;
            numLabel.style.paddingLeft = 6;
            numLabel.style.paddingRight = 6;
            numLabel.style.paddingTop = 3;
            numLabel.style.paddingBottom = 3;
            header.Add(numLabel);

            var titleLabel = new Label(title);
            titleLabel.style.fontSize = 13;
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.color = UiColors.TextPrimary;
            header.Add(titleLabel);
            card.Add(header);

            var descLabel = new Label(desc);
            descLabel.style.fontSize = 10;
            descLabel.style.color = UiColors.TextSecondary;
            descLabel.style.whiteSpace = WhiteSpace.Normal;
            descLabel.style.marginBottom = 10;
            card.Add(descLabel);
            card.Add(button);

            return card;
        }

        // ================================================================
        //  Inspector Body — field panels
        // ================================================================
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
            _detailRoot.style.paddingLeft = 2;
            _detailRoot.style.paddingRight = 2;
            selected.Add(_detailRoot);
            body.Add(selected);

            return body;
        }

        // ================================================================
        //  Timeline Body — horizontal strip + detail list
        // ================================================================
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
            // Remove overflow=Hidden so content can scroll vertically when window is small
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
            _timelineList.style.borderBottomLeftRadius = 6;
            _timelineList.style.borderBottomRightRadius = 6;
            _timelineList.style.borderTopLeftRadius = 6;
            _timelineList.style.borderTopRightRadius = 6;
            _timelineList.style.paddingLeft = 4;
            _timelineList.style.paddingRight = 4;
            _timelineList.selectionChanged += OnTimelineSelectionChanged;
            _timelineList.selectedIndicesChanged += OnTimelineSelectedIndicesChanged;
            center.Add(_timelineList);
            body.Add(center);

            return body;
        }

        // ================================================================
        //  Frame Scrubber — frame slider with label + play controls
        // ================================================================
        private VisualElement CreateFrameScrubber()
        {
            var root = new VisualElement();
            root.style.flexDirection = FlexDirection.Row;
            root.style.alignItems = Align.Center;
            root.style.marginBottom = 8;
            root.style.backgroundColor = UiColors.PanelSurfaceAlt;
            root.style.borderBottomLeftRadius = 8;
            root.style.borderBottomRightRadius = 8;
            root.style.borderTopLeftRadius = 8;
            root.style.borderTopRightRadius = 8;
            root.style.paddingLeft = 10;
            root.style.paddingRight = 10;
            root.style.paddingTop = 6;
            root.style.paddingBottom = 6;
            root.style.minHeight = 34;

            // Play/Pause toggle button
            var playBtn = new Button(() => TogglePlay());
            playBtn.tooltip = Tooltip("Frame");
            playBtn.style.marginRight = 4;
            playBtn.style.paddingLeft = 8;
            playBtn.style.paddingRight = 8;
            playBtn.style.paddingTop = 3;
            playBtn.style.paddingBottom = 3;
            playBtn.style.borderBottomLeftRadius = 4;
            playBtn.style.borderBottomRightRadius = 4;
            playBtn.style.borderTopLeftRadius = 4;
            playBtn.style.borderTopRightRadius = 4;
            playBtn.style.backgroundColor = UiColors.SurfaceDeep;
            playBtn.style.borderBottomWidth = 1;
            playBtn.style.borderLeftWidth = 1;
            playBtn.style.borderRightWidth = 1;
            playBtn.style.borderTopWidth = 1;
            playBtn.style.borderBottomColor = UiColors.PanelBorder;
            playBtn.style.borderLeftColor = UiColors.PanelBorder;
            playBtn.style.borderRightColor = UiColors.PanelBorder;
            playBtn.style.borderTopColor = UiColors.PanelBorder;
            _playButton = playBtn;
            UpdatePlayButton();
            root.Add(playBtn);

            // Speed button — cycles 0.5x → 1x → 2x → 4x
            var speedBtn = new Button(() => CyclePlaybackSpeed())
            {
                tooltip = Tooltip("播放速度"),
            };
            speedBtn.style.marginRight = 4;
            speedBtn.style.paddingLeft = 6;
            speedBtn.style.paddingRight = 6;
            speedBtn.style.paddingTop = 3;
            speedBtn.style.paddingBottom = 3;
            speedBtn.style.borderBottomLeftRadius = 4;
            speedBtn.style.borderBottomRightRadius = 4;
            speedBtn.style.borderTopLeftRadius = 4;
            speedBtn.style.borderTopRightRadius = 4;
            speedBtn.style.backgroundColor = UiColors.SurfaceDeep;
            speedBtn.style.borderBottomWidth = 1;
            speedBtn.style.borderLeftWidth = 1;
            speedBtn.style.borderRightWidth = 1;
            speedBtn.style.borderTopWidth = 1;
            speedBtn.style.borderBottomColor = UiColors.PanelBorder;
            speedBtn.style.borderLeftColor = UiColors.PanelBorder;
            speedBtn.style.borderRightColor = UiColors.PanelBorder;
            speedBtn.style.borderTopColor = UiColors.PanelBorder;
            _speedButton = speedBtn;
            UpdateSpeedButton();
            root.Add(speedBtn);

            // Mode button — cycles PingPong → Once → Reverse
            var modeBtn = new Button(() => TogglePlayMode())
            {
                tooltip = Tooltip("播放模式"),
            };
            modeBtn.style.marginRight = 6;
            modeBtn.style.paddingLeft = 6;
            modeBtn.style.paddingRight = 6;
            modeBtn.style.paddingTop = 3;
            modeBtn.style.paddingBottom = 3;
            modeBtn.style.borderBottomLeftRadius = 4;
            modeBtn.style.borderBottomRightRadius = 4;
            modeBtn.style.borderTopLeftRadius = 4;
            modeBtn.style.borderTopRightRadius = 4;
            modeBtn.style.backgroundColor = UiColors.SurfaceDeep;
            modeBtn.style.borderBottomWidth = 1;
            modeBtn.style.borderLeftWidth = 1;
            modeBtn.style.borderRightWidth = 1;
            modeBtn.style.borderTopWidth = 1;
            modeBtn.style.borderBottomColor = UiColors.PanelBorder;
            modeBtn.style.borderLeftColor = UiColors.PanelBorder;
            modeBtn.style.borderRightColor = UiColors.PanelBorder;
            modeBtn.style.borderTopColor = UiColors.PanelBorder;
            _modeButton = modeBtn;
            UpdateModeButton();
            root.Add(modeBtn);

            _frameSlider = new SliderInt("Frame", 0, 0);
            _frameSlider.tooltip = Tooltip("Frame");
            _frameSlider.style.flexGrow = 1;
            _frameSlider.style.flexShrink = 1;
            _frameSlider.style.minWidth = 0;
            _frameSlider.RegisterValueChangedCallback(evt =>
            {
                if (_frameLabel != null)
                {
                    _frameLabel.text = "⏱ 帧 " + evt.newValue;
                }

                CombatAuthoringSceneState.SetFrame(evt.newValue);
            });
            root.Add(_frameSlider);

            _frameLabel = new Label("⏱ 帧 0");
            _frameLabel.style.width = 80;
            _frameLabel.style.flexShrink = 0;
            _frameLabel.style.marginLeft = 8;
            _frameLabel.style.fontSize = 12;
            _frameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _frameLabel.style.color = UiColors.TimelineActive;
            _frameLabel.style.unityTextAlign = TextAnchor.MiddleRight;
            root.Add(_frameLabel);

            return root;
        }

        private void CyclePlaybackSpeed()
        {
            _playbackSpeed = _playbackSpeed >= 4f ? 0.5f :
                             _playbackSpeed >= 2f ? 4f :
                             _playbackSpeed >= 1f ? 2f :
                             _playbackSpeed >= 0.5f ? 1f : 0.5f;
            UpdateSpeedButton();
        }

        // ================================================================
        //  Timeline Strip View — horizontal time-strip
        // ================================================================
        private VisualElement CreateTimelineStripView()
        {
            var root = new VisualElement();
            root.style.flexGrow = 0;
            root.style.flexShrink = 0;
            root.style.minHeight = 0;

            _timelineStripEmptyRoot = new HelpBox("请选择 Action Asset 后查看横向时间轴。", HelpBoxMessageType.Info);
            _timelineStripEmptyRoot.style.marginBottom = 6;
            _timelineStripEmptyRoot.style.borderBottomLeftRadius = 4;
            _timelineStripEmptyRoot.style.borderBottomRightRadius = 4;
            _timelineStripEmptyRoot.style.borderTopLeftRadius = 4;
            _timelineStripEmptyRoot.style.borderTopRightRadius = 4;
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
            _timelineStripScroll.style.borderBottomLeftRadius = 6;
            _timelineStripScroll.style.borderBottomRightRadius = 6;
            _timelineStripScroll.style.borderTopLeftRadius = 6;
            _timelineStripScroll.style.borderTopRightRadius = 6;
            _timelineStripContent = new VisualElement();
            _timelineStripContent.style.position = Position.Relative;
            _timelineStripScroll.Add(_timelineStripContent);
            timelineRow.Add(_timelineStripScroll);
            root.Add(timelineRow);

            // Viewport control — placed directly after the scroll with minimal gap
            var viewportControl = CreateTimelineViewportControl();
            viewportControl.style.marginTop = 0;
            viewportControl.style.marginBottom = 0;
            viewportControl.style.paddingLeft = 0;
            viewportControl.style.paddingRight = 0;
            root.Add(viewportControl);

            return root;
        }

        // ================================================================
        //  Timeline Viewport Control — view range slider
        // ================================================================
        private VisualElement CreateTimelineViewportControl()
        {
            var root = new VisualElement();
            root.style.flexDirection = FlexDirection.Row;
            root.style.alignItems = Align.Center;
            root.style.marginTop = 0;
            root.style.marginBottom = 0;
            root.style.paddingLeft = 12;
            root.style.paddingRight = 12;
            root.style.paddingTop = 4;
            root.style.paddingBottom = 4;
            root.style.backgroundColor = UiColors.PanelDeep;
            root.style.borderBottomLeftRadius = 4;
            root.style.borderBottomRightRadius = 4;
            root.style.borderTopLeftRadius = 4;
            root.style.borderTopRightRadius = 4;

            var label = new Label("View");
            label.tooltip = Tooltip("Timeline View Range");
            label.style.fontSize = 10;
            label.style.color = UiColors.TextSecondary;
            label.style.width = 44;
            label.style.flexShrink = 0;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
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
            _timelineViewportLabel.style.width = 120;
            _timelineViewportLabel.style.flexShrink = 0;
            _timelineViewportLabel.style.marginLeft = 8;
            _timelineViewportLabel.style.fontSize = 10;
            _timelineViewportLabel.style.color = UiColors.TextSecondary;
            _timelineViewportLabel.style.unityTextAlign = TextAnchor.MiddleRight;
            root.Add(_timelineViewportLabel);

            return root;
        }

        // ================================================================
        //  Issue Drawer — validation report + preview
        // ================================================================
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
            _issueList.style.borderBottomLeftRadius = 4;
            _issueList.style.borderBottomRightRadius = 4;
            _issueList.style.borderTopLeftRadius = 4;
            _issueList.style.borderTopRightRadius = 4;
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
            _reportPreview.style.borderBottomLeftRadius = 4;
            _reportPreview.style.borderBottomRightRadius = 4;
            _reportPreview.style.borderTopLeftRadius = 4;
            _reportPreview.style.borderTopRightRadius = 4;
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
            _previewExplain.style.borderBottomLeftRadius = 4;
            _previewExplain.style.borderBottomRightRadius = 4;
            _previewExplain.style.borderTopLeftRadius = 4;
            _previewExplain.style.borderTopRightRadius = 4;
            _previewExplain.value = "点击“预览 Explain”生成当前帧 query / candidate / resolve 解释。";
            previewColumn.Add(_previewExplain);
            split.Add(previewColumn);

            drawer.Add(split);
            return drawer;
        }

        // ================================================================
        //  Mini Button — compact size for secondary actions
        // ================================================================
        private static Button CreateMiniButton(string text, string tooltip, Action action)
        {
            var button = new Button(action)
            {
                text = text,
                tooltip = tooltip,
            };
            button.style.marginLeft = 3;
            button.style.marginRight = 0;
            button.style.marginBottom = 3;
            button.style.minWidth = 30;
            button.style.fontSize = 10;
            button.style.paddingLeft = 8;
            button.style.paddingRight = 8;
            button.style.paddingTop = 3;
            button.style.paddingBottom = 3;
            button.style.backgroundColor = UiColors.SurfaceAlt;
            button.style.borderBottomWidth = 1;
            button.style.borderLeftWidth = 1;
            button.style.borderRightWidth = 1;
            button.style.borderTopWidth = 1;
            button.style.borderBottomColor = UiColors.PanelBorder;
            button.style.borderLeftColor = UiColors.PanelBorder;
            button.style.borderRightColor = UiColors.PanelBorder;
            button.style.borderTopColor = UiColors.PanelBorder;
            button.style.borderBottomLeftRadius = 4;
            button.style.borderBottomRightRadius = 4;
            button.style.borderTopLeftRadius = 4;
            button.style.borderTopRightRadius = 4;
            button.RegisterCallback<PointerEnterEvent>(_ => OnButtonHover(button, true, false));
            button.RegisterCallback<PointerLeaveEvent>(_ => OnButtonHover(button, false, false));
            return button;
        }

        // ================================================================
        //  Panel — card container with optional accent bar
        // ================================================================
        private static VisualElement CreatePanel(string title)
        {
            return CreatePanel(title, UiColors.TimelineActive);
        }

        private static VisualElement CreatePanel(string title, Color accentColor)
        {
            var panel = new VisualElement();
            panel.style.backgroundColor = UiColors.PanelSurface;
            panel.style.borderBottomColor = UiColors.PanelBorder;
            panel.style.borderLeftColor = UiColors.PanelBorder;
            panel.style.borderRightColor = UiColors.PanelBorder;
            panel.style.borderTopColor = UiColors.PanelBorder;
            panel.style.borderBottomWidth = 1;
            panel.style.borderLeftWidth = 1;
            panel.style.borderRightWidth = 1;
            panel.style.borderTopWidth = 1;
            panel.style.borderBottomLeftRadius = 8;
            panel.style.borderBottomRightRadius = 8;
            panel.style.borderTopLeftRadius = 8;
            panel.style.borderTopRightRadius = 8;
            panel.style.paddingBottom = 10;
            panel.style.paddingLeft = 12;
            panel.style.paddingRight = 12;
            panel.style.paddingTop = 10;

            // Top accent bar — slightly thicker for better visual hierarchy
            var accentBar = new VisualElement();
            accentBar.style.height = 3f;
            accentBar.style.marginBottom = 8;
            accentBar.style.backgroundColor = accentColor;
            accentBar.style.borderBottomLeftRadius = 3;
            accentBar.style.borderBottomRightRadius = 3;
            panel.Add(accentBar);

            panel.Add(SectionTitle(title));
            return panel;
        }

        // ================================================================
        //  Section Title — label + thin divider line
        // ================================================================
        private static VisualElement SectionTitle(string text)
        {
            var container = new VisualElement();
            container.style.marginBottom = 8;

            var label = new Label(text);
            label.tooltip = Tooltip(text);
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.color = UiColors.TextPrimary;
            label.style.fontSize = 12;
            label.style.paddingBottom = 4;
            container.Add(label);

            // Subtle divider line — slightly more visible
            var divider = new VisualElement();
            divider.style.height = 1.5f;
            divider.style.backgroundColor = UiColors.PanelBorder;
            divider.style.opacity = 0.7f;
            container.Add(divider);

            return container;
        }

        // ================================================================
        //  Button — standard action button with hover
        // ================================================================
        private static Button CreateButton(string text, Action action, bool isPrimary = false)
        {
            var button = new Button(action) { text = text };
            button.tooltip = Tooltip(text);
            button.style.marginRight = 4;
            button.style.marginBottom = 4;
            button.style.paddingLeft = 12;
            button.style.paddingRight = 12;
            button.style.paddingTop = 5;
            button.style.paddingBottom = 5;
            button.style.borderBottomLeftRadius = 6;
            button.style.borderBottomRightRadius = 6;
            button.style.borderTopLeftRadius = 6;
            button.style.borderTopRightRadius = 6;
            button.style.fontSize = 11;
            button.style.borderBottomWidth = 1;
            button.style.borderLeftWidth = 1;
            button.style.borderRightWidth = 1;
            button.style.borderTopWidth = 1;

            if (isPrimary)
            {
                button.style.backgroundColor = UiColors.ButtonPrimaryBg;
                button.style.borderBottomColor = UiColors.ButtonPrimaryBorder;
                button.style.borderLeftColor = new Color(0.35f, 0.35f, 0.40f);
                button.style.borderRightColor = new Color(0.35f, 0.35f, 0.40f);
                button.style.borderTopColor = new Color(0.35f, 0.35f, 0.40f);
            }
            else
            {
                button.style.backgroundColor = UiColors.SurfaceAlt;
                button.style.borderBottomColor = UiColors.PanelBorder;
                button.style.borderLeftColor = UiColors.PanelBorder;
                button.style.borderRightColor = UiColors.PanelBorder;
                button.style.borderTopColor = UiColors.PanelBorder;
            }

            button.RegisterCallback<PointerEnterEvent>(_ => OnButtonHover(button, true, isPrimary));
            button.RegisterCallback<PointerLeaveEvent>(_ => OnButtonHover(button, false, isPrimary));
            return button;
        }

        // ================================================================
        //  Button With Icon — button with emoji/unicode icon prefix
        // ================================================================
        private static Button CreateButtonWithIcon(string text, Action action, bool isPrimary = false)
        {
            var button = new Button(action);
            button.tooltip = text.StartsWith("⚡") ? Tooltip("使用当前选择")
                : text.StartsWith("✨") ? Tooltip("创建 Action Asset")
                : text.StartsWith("🎬") ? Tooltip("打开 Timeline")
                : text.StartsWith("📍") ? (text.Contains("Action") ? Tooltip("定位 Action") : Tooltip("定位 Binding"))
                : text.StartsWith("➕") ? Tooltip("创建 Binding")
                : text.StartsWith("🔍") ? Tooltip("从当前场景生成")
                : text.StartsWith("🔗") ? Tooltip("重连选中对象")
                : text.StartsWith("🗡") ? Tooltip("添加 Hitbox")
                : text.StartsWith("🛡") ? Tooltip("添加 Hurtbox")
                : text.StartsWith("✅") ? Tooltip("执行验证")
                : text.StartsWith("▶") ? Tooltip("运行 Showcase 预览")
                : text.StartsWith("🧹") ? Tooltip("清除预览会话")
                : text.StartsWith("📋") ? Tooltip("复制报告")
                : text.StartsWith("📦") ? Tooltip("导出 JSON")
                : text.StartsWith("📄") ? Tooltip("复制导出报告")
                : text.StartsWith("💡") ? Tooltip("预览 Explain")
                : text.StartsWith("📝") ? Tooltip("复制 Explain")
                : Tooltip(text);

            // Icon + text layout
            var iconLabel = new Label(text.Substring(0, text.IndexOf(' ', 1)));
            iconLabel.style.fontSize = 13;
            iconLabel.style.marginRight = 4;
            iconLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            iconLabel.style.width = 20;

            var textLabel = new Label(text.Substring(text.IndexOf(' ', 1) + 1));
            textLabel.style.fontSize = 11;

            button.Add(iconLabel);
            button.Add(textLabel);

            button.style.marginRight = 4;
            button.style.marginBottom = 4;
            button.style.paddingLeft = 8;
            button.style.paddingRight = 8;
            button.style.paddingTop = 5;
            button.style.paddingBottom = 5;
            button.style.borderBottomLeftRadius = 6;
            button.style.borderBottomRightRadius = 6;
            button.style.borderTopLeftRadius = 6;
            button.style.borderTopRightRadius = 6;
            button.style.fontSize = 11;
            button.style.flexDirection = FlexDirection.Row;
            button.style.alignItems = Align.Center;
            button.style.justifyContent = Justify.Center;
            button.style.borderBottomWidth = 1;
            button.style.borderLeftWidth = 1;
            button.style.borderRightWidth = 1;
            button.style.borderTopWidth = 1;

            if (isPrimary)
            {
                button.style.backgroundColor = UiColors.ButtonPrimaryBg;
                button.style.borderBottomColor = UiColors.ButtonPrimaryBorder;
                button.style.borderLeftColor = new Color(0.35f, 0.35f, 0.40f);
                button.style.borderRightColor = new Color(0.35f, 0.35f, 0.40f);
                button.style.borderTopColor = new Color(0.35f, 0.35f, 0.40f);
            }
            else
            {
                button.style.backgroundColor = UiColors.SurfaceAlt;
                button.style.borderBottomColor = UiColors.PanelBorder;
                button.style.borderLeftColor = UiColors.PanelBorder;
                button.style.borderRightColor = UiColors.PanelBorder;
                button.style.borderTopColor = UiColors.PanelBorder;
            }

            button.RegisterCallback<PointerEnterEvent>(_ => OnButtonHoverWithIcon(button, true, isPrimary));
            button.RegisterCallback<PointerLeaveEvent>(_ => OnButtonHoverWithIcon(button, false, isPrimary));
            return button;
        }

        // ================================================================
        //  Button Hover — visual feedback on pointer enter / leave
        // ================================================================
        private static void OnButtonHover(VisualElement button, bool hover, bool isPrimary = false)
        {
            if (hover)
            {
                button.style.backgroundColor = isPrimary
                    ? UiColors.ButtonPrimaryHover
                    : UiColors.ButtonHover;
                button.style.borderBottomColor = UiColors.TextSecondary;
                button.style.borderLeftColor = UiColors.TextSecondary;
                button.style.borderRightColor = UiColors.TextSecondary;
                button.style.borderTopColor = UiColors.TextSecondary;
            }
            else
            {
                if (isPrimary)
                {
                    button.style.backgroundColor = UiColors.ButtonPrimaryBg;
                    button.style.borderBottomColor = UiColors.ButtonPrimaryBorder;
                    button.style.borderLeftColor = new Color(0.35f, 0.35f, 0.40f);
                    button.style.borderRightColor = new Color(0.35f, 0.35f, 0.40f);
                    button.style.borderTopColor = new Color(0.35f, 0.35f, 0.40f);
                }
                else
                {
                    button.style.backgroundColor = UiColors.SurfaceAlt;
                    button.style.borderBottomColor = UiColors.PanelBorder;
                    button.style.borderLeftColor = UiColors.PanelBorder;
                    button.style.borderRightColor = UiColors.PanelBorder;
                    button.style.borderTopColor = UiColors.PanelBorder;
                }
            }
        }

        private static void OnButtonHoverWithIcon(VisualElement button, bool hover, bool isPrimary = false)
        {
            if (hover)
            {
                button.style.backgroundColor = isPrimary
                    ? UiColors.ButtonPrimaryHover
                    : UiColors.ButtonHover;
                button.style.borderBottomColor = UiColors.TextSecondary;
                button.style.borderLeftColor = UiColors.TextSecondary;
                button.style.borderRightColor = UiColors.TextSecondary;
                button.style.borderTopColor = UiColors.TextSecondary;
            }
            else
            {
                if (isPrimary)
                {
                    button.style.backgroundColor = UiColors.ButtonPrimaryBg;
                    button.style.borderBottomColor = UiColors.ButtonPrimaryBorder;
                    button.style.borderLeftColor = new Color(0.35f, 0.35f, 0.40f);
                    button.style.borderRightColor = new Color(0.35f, 0.35f, 0.40f);
                    button.style.borderTopColor = new Color(0.35f, 0.35f, 0.40f);
                }
                else
                {
                    button.style.backgroundColor = UiColors.SurfaceAlt;
                    button.style.borderBottomColor = UiColors.PanelBorder;
                    button.style.borderLeftColor = UiColors.PanelBorder;
                    button.style.borderRightColor = UiColors.PanelBorder;
                    button.style.borderTopColor = UiColors.PanelBorder;
                }
            }
        }

        // ================================================================
        //  Shared helpers (unchanged)
        // ================================================================

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
                case "\u2699 基础字段":
                    return "当前 Action 和 Scene Binding 的核心字段。数组字段可以展开查看，但复杂编辑优先通过时间轴和 Scene View。";
                case "Timeline":
                case "\u23F1 Timeline":
                    return "固定帧时间轴。横向条展示 Startup、Active、Recovery、Hitbox、Hurtbox 和 Trace 的生效帧。";
                case "详细信息":
                case "详情列表":
                    return "当前时间轴条目的列表视图。选择一行后，右侧会显示可轻编辑字段。";
                case "选中项":
                case "\u25B6 选中项":
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
                case "暂停播放":
                    return "暂停帧播放。";
                case "播放帧":
                    return "自动播放帧，在 Startup / Active / Recovery 范围内来回扫描。";
                case "播放速度":
                    return "循环切换播放速度：0.5x → 1x → 2x → 4x。";
                case "播放模式":
                    return "循环切换播放模式：🔁 来回 → 🔚 单次 → ◀️ 倒放。";
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

        // ================================================================
        //  Play/Pause Control — frame animation with speed & mode
        // ================================================================
        private bool _isPlaying;
        private int _playDirection = 1;
        private float _playbackSpeed = 1f;
        private PlayMode _playMode = PlayMode.PingPong;
        private float _playAccumulator = 0f;
        private double _lastPlayTime = 0;
        private const float BaseFrameInterval = 1f / 60f; // 1x = 60 FPS, ~16.67ms per frame

        private enum PlayMode
        {
            PingPong,  // 来回循环
            Once,      // 单次播放，到终点停止
            Reverse,   // 倒放，到起点停止
        }

        private void TogglePlay()
        {
            _isPlaying = !_isPlaying;
            UpdatePlayButton();
            if (_isPlaying)
            {
                _lastPlayTime = EditorApplication.timeSinceStartup;
                _playAccumulator = 0f;
                EditorApplication.update += OnPlayUpdate;
            }
            else
            {
                EditorApplication.update -= OnPlayUpdate;
            }
        }

        private void SetPlaybackSpeed(float speed)
        {
            _playbackSpeed = speed;
            UpdateSpeedButton();
        }

        private void TogglePlayMode()
        {
            _playMode = _playMode == PlayMode.PingPong ? PlayMode.Once :
                        _playMode == PlayMode.Once ? PlayMode.Reverse : PlayMode.PingPong;
            UpdateModeButton();
        }

        private void UpdatePlayButton()
        {
            if (_playButton != null)
            {
                _playButton.text = _isPlaying ? "⏸" : "▶";
                _playButton.tooltip = _isPlaying ? Tooltip("暂停播放") : Tooltip("播放帧");
            }
        }

        private void UpdateSpeedButton()
        {
            if (_speedButton != null)
            {
                string label = _playbackSpeed == 0.5f ? "⏪ 0.5x" :
                               _playbackSpeed == 1f ? "⏩ 1x" :
                               _playbackSpeed == 2f ? "⏩ 2x" : "⏩ 4x";
                _speedButton.text = label;
                _speedButton.tooltip = Tooltip("播放速度");
            }
        }

        private void UpdateModeButton()
        {
            if (_modeButton != null)
            {
                string label = _playMode == PlayMode.PingPong ? "🔁 来回" :
                               _playMode == PlayMode.Once ? "🔚 单次" : "◀️ 倒放";
                _modeButton.text = label;
                _modeButton.tooltip = Tooltip("播放模式");
            }
        }

        private void OnPlayUpdate()
        {
            if (!_isPlaying || _actionAsset == null)
            {
                return;
            }

            // Calculate delta time for consistent frame advancement
            double currentTime = EditorApplication.timeSinceStartup;
            float deltaTime = (float)(currentTime - _lastPlayTime);
            _lastPlayTime = currentTime;

            int currentFrame = CombatAuthoringSceneState.Frame;
            int totalFrames = _actionAsset.TotalFrames;

            if (totalFrames <= 1)
            {
                _isPlaying = false;
                UpdatePlayButton();
                EditorApplication.update -= OnPlayUpdate;
                return;
            }

            // Accumulate time to control frame advance rate based on speed
            _playAccumulator += deltaTime;
            float frameInterval = BaseFrameInterval / _playbackSpeed;

            if (_playAccumulator < frameInterval)
            {
                return;
            }
            _playAccumulator -= frameInterval;

            // Advance frame based on mode
            switch (_playMode)
            {
                case PlayMode.PingPong:
                    currentFrame += _playDirection;
                    if (currentFrame >= totalFrames - 1)
                    {
                        currentFrame = totalFrames - 1;
                        _playDirection = -1;
                    }
                    else if (currentFrame <= 0)
                    {
                        currentFrame = 0;
                        _playDirection = 1;
                    }
                    break;

                case PlayMode.Once:
                    currentFrame += 1;
                    if (currentFrame >= totalFrames - 1)
                    {
                        currentFrame = totalFrames - 1;
                        _isPlaying = false;
                        UpdatePlayButton();
                        EditorApplication.update -= OnPlayUpdate;
                    }
                    break;

                case PlayMode.Reverse:
                    currentFrame -= 1;
                    if (currentFrame <= 0)
                    {
                        currentFrame = 0;
                        _isPlaying = false;
                        UpdatePlayButton();
                        EditorApplication.update -= OnPlayUpdate;
                    }
                    break;
            }

            CombatAuthoringSceneState.SetFrame(currentFrame);
            if (_frameSlider != null)
            {
                _frameSlider.SetValueWithoutNotify(currentFrame);
            }
            if (_frameLabel != null)
            {
                _frameLabel.text = "⏱ 帧 " + currentFrame;
            }
        }
    }
}
