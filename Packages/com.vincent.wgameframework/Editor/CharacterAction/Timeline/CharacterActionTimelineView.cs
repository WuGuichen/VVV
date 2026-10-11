using System;
using System.Collections.Generic;
using MxFramework.CharacterAction;
using MxFramework.Editor.CharacterAction;
using MxFramework.Editor.CharacterAction.Timeline.Adapters;
using MxFramework.Editor.CharacterAction.Timeline.Manipulators;
using MxFramework.Editor.CharacterAction.Timeline.Preview;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Editor.CharacterAction.Timeline
{
    internal sealed partial class CharacterActionTimelineView : VisualElement
    {
        private const string StylePackageRelativePath =
            "Editor/CharacterAction/Timeline/CharacterActionTimelineView.uss";

        private readonly CharacterActionTimelineState _state = new CharacterActionTimelineState();
        private readonly Dictionary<string, VisualElement> _itemElements = new Dictionary<string, VisualElement>();
        private readonly Dictionary<TimelineTrackKind, VisualElement> _rowCanvases = new Dictionary<TimelineTrackKind, VisualElement>();
        private readonly Dictionary<TimelineTrackKind, VisualElement> _labelRows = new Dictionary<TimelineTrackKind, VisualElement>();
        private readonly Dictionary<TimelineTrackKind, CharacterActionTimelineTrackLayout> _trackLayouts =
            new Dictionary<TimelineTrackKind, CharacterActionTimelineTrackLayout>();

        private CharacterActionTimelineHost _host;
        private CharacterActionTimelineSnapshot _snapshot;
        private VisualElement _labelColumn;
        private ScrollView _scrollView;
        private VisualElement _canvasRoot;
        private VisualElement _playhead;
        private VisualElement _selectionFrameGuide;
        private MinMaxSlider _viewportSlider;
        private Label _viewportLabel;
        private Button _saveButton;
        private Label _dirtyLabel;
        private ToolbarMenu _contextSelector;
        private VisualElement _itemPickerBackdrop;
        private VisualElement _itemPickerOverlay;
        private VisualElement _readOnlyBannerHost;
        private ScrollView _bodyScroll;
        private ToolbarSearchField _itemPickerSearch;
        private ScrollView _itemPickerScroll;
        private VisualElement _itemPickerList;
        private VisualElement _itemPickerFooter;
        private readonly List<CharacterActionTimelinePickerItem> _pickerVisibleItems =
            new List<CharacterActionTimelinePickerItem>();
        private ToolbarMenu _addMenu;
        private ToolbarMenu _moreMenu;
        private IntegerField _durationField;
        private TimelineTrackKind _focusedTrack = TimelineTrackKind.Motion;
        private ICharacterActionTimelinePlayback _playback;
        private bool _isApplyingViewportRange;
        private bool _isInteractingWithViewportRange;
        private bool _isItemGestureActive;
        private int _pickerFocusedIndex = -1;
        private string _pickerSearchText = string.Empty;

        public CharacterActionTimelineView()
        {
            name = "character-action-timeline";
            AddToClassList("ca-tl-root");
            style.flexGrow = 1f;
            style.minHeight = 280f;

            StyleSheet sheet = MxFrameworkPackageAssetLocator.TryResolveAssetPath(
                StylePackageRelativePath,
                out string stylePath)
                ? AssetDatabase.LoadAssetAtPath<StyleSheet>(stylePath)
                : null;
            if (sheet != null)
                styleSheets.Add(sheet);

            BuildChrome();
            focusable = true;
            RegisterCallback<KeyDownEvent>(OnKeyDown);
        }

        public CharacterActionTimelineSnapshot Snapshot => _snapshot;
        internal string CharacterStableId => _host?.CharacterStableId ?? string.Empty;
        public string SelectedItemId => _state.SelectedItemId;
        internal bool HasHost => _host != null;

        public void SetHost(CharacterActionTimelineHost host)
        {
            _host = host;
            RefreshCommandState();
        }

        public void Bind(CharacterActionJsonDto action)
        {
            _isItemGestureActive = false;
            int fallback = _host?.DefaultDurationFrames ?? 8;
            int duration = CharacterActionTimelineLayout.ResolveDurationFrames(action?.DurationFrames, fallback);
            if (!IsCombatAnchored(action)
                && CharacterActionPhaseTimelineUtility.NormalizePhases(action, duration))
            {
                _host?.CommitField(null, false);
            }

            _snapshot = CharacterActionTimelineSnapshot.FromAction(action, fallback);
            if (_state.SelectedItemId != null && _snapshot.FindItem(_state.SelectedItemId) == null)
                _state.SelectedItemId = string.Empty;

            if (_durationField != null)
                _durationField.SetValueWithoutNotify(_snapshot.ActionDurationFrames);

            RebuildCanvas(preserveScroll: false);
            UpdatePlayhead();
            RefreshCommandState();
        }

        public void BindAbilityEffectTimeline(AbilityEffectTimelineDraft timeline)
        {
            _isItemGestureActive = false;
            timeline = timeline ?? new AbilityEffectTimelineDraft();
            _snapshot = CharacterActionTimelineSnapshot.FromAbilityEffectTimeline(timeline);
            if (_state.SelectedItemId != null && _snapshot.FindItem(_state.SelectedItemId) == null)
                _state.SelectedItemId = string.Empty;

            if (_durationField != null)
                _durationField.SetValueWithoutNotify(_snapshot.ActionDurationFrames);

            RebuildCanvas(preserveScroll: false);
            UpdatePlayhead();
            RefreshCommandState();
        }

        public void SaveViewState()
        {
            float scrollX = _scrollView != null ? _scrollView.scrollOffset.x : 0f;
            _state.SaveViewState(scrollX);
        }

        public void RestoreViewState()
        {
            bool scaleChanged = _state.PixelsPerFrame != _state.SavedPixelsPerFrame;
            _state.RestoreViewState();
            if (_scrollView != null)
                _scrollView.scrollOffset = new Vector2(_state.SavedScrollOffsetX, 0f);
            if (scaleChanged)
                RebuildCanvas();
        }

        public void SelectItem(string itemId, bool notify = true)
        {
            string nextId = itemId ?? string.Empty;
            if (_state.SelectedItemId == nextId)
                return;
            _state.SelectedItemId = nextId;
            RefreshSelectionHighlight();
            if (notify)
                _host?.SelectionChanged?.Invoke(_state.SelectedItemId);
        }

        public void BeginGesture()
        {
            _host?.BeginGesture?.Invoke();
            _isItemGestureActive = true;
        }

        public void EndGesture()
        {
            _isItemGestureActive = false;
            _host?.EndGesture?.Invoke();
        }

        public int PixelToFrame(float localX)
        {
            float padded = localX - CharacterActionTimelineLayout.ContentPadding;
            return CharacterActionTimelineLayout.XToFrame(
                padded,
                _state.PixelsPerFrame,
                _state.SnapToFrame);
        }

        public int PixelToFrameCell(float localX)
        {
            float padded = localX - CharacterActionTimelineLayout.ContentPadding;
            return CharacterActionTimelineLayout.XToFrame(
                padded,
                _state.PixelsPerFrame,
                snapToFrame: false);
        }

        public float FrameStartX(int frame)
        {
            return CharacterActionTimelineLayout.ContentPadding
                + CharacterActionTimelineLayout.FrameToX(frame, _state.PixelsPerFrame);
        }

        public float ClipEndX(int endFrame)
        {
            return CharacterActionTimelineLayout.ContentPadding
                + CharacterActionTimelineLayout.FrameToX(endFrame + 1, _state.PixelsPerFrame);
        }

        public float MarkerCenterX(int frame)
        {
            return FrameStartX(frame) + _state.PixelsPerFrame * 0.5f;
        }

        public int MarkerCenterXToFrame(float localX)
        {
            float padded = localX
                - CharacterActionTimelineLayout.ContentPadding
                - _state.PixelsPerFrame * 0.5f;
            return CharacterActionTimelineLayout.XToFrame(
                padded,
                _state.PixelsPerFrame,
                _state.SnapToFrame);
        }

        public int ClipEndXToFrame(float localX)
        {
            float padded = localX - CharacterActionTimelineLayout.ContentPadding;
            int boundaryFrame = CharacterActionTimelineLayout.XToFrame(
                padded,
                _state.PixelsPerFrame,
                _state.SnapToFrame);
            return boundaryFrame - 1;
        }

        public float CanvasWorldToLocalX(float worldX)
        {
            if (_canvasRoot == null)
                return 0f;
            Vector2 local = _canvasRoot.WorldToLocal(new Vector2(worldX, 0f));
            return local.x;
        }

        public void Zoom(float deltaPixelsPerFrame)
        {
            _state.PixelsPerFrame = Mathf.Clamp(
                _state.PixelsPerFrame + deltaPixelsPerFrame,
                CharacterActionTimelineLayout.MinPixelsPerFrame,
                CharacterActionTimelineLayout.MaxPixelsPerFrame);
            RebuildCanvas(preserveScroll: true);
            UpdateViewportControl();
        }

        public void FitToWidth()
        {
            if (_scrollView == null || _snapshot == null)
                return;

            float width = _scrollView.contentViewport.layout.width;
            if (width < 40f)
                width = 400f;
            float usable = width - CharacterActionTimelineLayout.ContentPadding * 2f;
            int duration = Math.Max(1, _snapshot.DurationFrames);
            _state.PixelsPerFrame = Mathf.Clamp(
                usable / duration,
                CharacterActionTimelineLayout.MinPixelsPerFrame,
                CharacterActionTimelineLayout.MaxPixelsPerFrame);
            RebuildCanvas(preserveScroll: true);
            UpdateViewportControl();
        }

        public void NotifyViewportChanged()
        {
            UpdateViewportControl();
        }



        private static bool IsCombatAnchored(CharacterActionJsonDto action)
        {
            return string.Equals(
                action?.TimelineAuthority,
                CharacterActionTimelineAuthority.CombatAnchored.ToString(),
                StringComparison.Ordinal);
        }
    }
}
