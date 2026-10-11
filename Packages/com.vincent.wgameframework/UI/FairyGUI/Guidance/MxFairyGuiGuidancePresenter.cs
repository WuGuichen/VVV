using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Fgui = global::FairyGUI;

namespace MxFramework.UI.FairyGui
{
    /// <summary>Fixed diagnostic outcomes for the FairyGUI guidance presenter.</summary>
    public enum MxFairyGuiGuidanceDiagnosticCode : byte
    {
        None = 0,
        WrongThread = 1,
        CardRejected = 2,
        NoticeRejected = 3,
        CardViewOpenFailed = 4,
        NoticeViewOpenFailed = 5,
        InvalidLayoutPreset = 6
    }

    /// <summary>
    /// Renders the shared one-card, two-notice guidance state through FairyGUI.
    /// </summary>
    /// <remarks>
    /// AllocByDesign during construction and low-frequency submissions. After views are open,
    /// <see cref="Tick"/> performs fixed-slot timing and interpolation without managed allocation.
    /// This object does not own the supplied navigator.
    /// </remarks>
    public sealed class MxFairyGuiGuidancePresenter : IMxUiGuidanceSink, IDisposable
    {
        public const float CardEnterDurationSeconds = 0.18f;
        public const float CardUpdateFadeDurationSeconds = 0.10f;
        public const float NoticeEnterDurationSeconds = 0.16f;
        public const float NoticeExitDurationSeconds = 0.14f;

        private readonly MxFairyGuiNavigator _navigator;
        private readonly IMxUiTextProvider _textProvider;
        private readonly MxUiGuidanceState _state = new MxUiGuidanceState();
        private readonly MxFairyGuiGuidanceCardViewModel _cardViewModel =
            new MxFairyGuiGuidanceCardViewModel();
        private readonly MxFairyGuiGuidanceNoticeStackViewModel _noticeViewModel =
            new MxFairyGuiGuidanceNoticeStackViewModel();
        private readonly int _ownerThreadId;

        private Fgui.GComponent _cardRoot;
        private Fgui.GComponent _noticeRoot;
        private NoticeVisualCache _notice0Visuals;
        private NoticeVisualCache _notice1Visuals;
        private long _notice0VisualSequence;
        private long _notice1VisualSequence;
        private bool _notice0Entering;
        private bool _notice1Entering;
        private CardAnimationKind _cardAnimation;
        private float _cardAnimationElapsed;
        private float _cardBaseX;
        private long _localeRevision;
        private bool _disposed;
        private bool _cardViewReady;
        private bool _noticeViewReady;
        private string _lastDiagnosticMessage = string.Empty;

        /// <summary>Creates and opens both non-interactive guidance views.</summary>
        /// <remarks>AllocByDesign. Call on the thread that will own subsequent submissions and Tick.</remarks>
        public MxFairyGuiGuidancePresenter(
            MxFairyGuiNavigator navigator,
            IMxUiTextProvider textProvider = null,
            MxFairyGuiGuidanceLayoutPreset layoutPreset = MxFairyGuiGuidanceLayoutPreset.Default)
        {
            _navigator = navigator ?? throw new ArgumentNullException(nameof(navigator));
            _textProvider = textProvider ?? MxUiNullTextProvider.Instance;
            _ownerThreadId = Thread.CurrentThread.ManagedThreadId;
            LayoutPreset = IsValidLayoutPreset(layoutPreset)
                ? layoutPreset
                : MxFairyGuiGuidanceLayoutPreset.Default;
            _localeRevision = _textProvider.Revision;
            RefreshCardView();
            RefreshNoticeView();
        }

        public bool IsReady => !_disposed && _cardViewReady && _noticeViewReady;
        public bool HasCard => _state.HasCard;
        public int NoticeCount => _state.NoticeCount;
        public int OwnerThreadViolationCount { get; private set; }
        public int CardOpenFailureCount { get; private set; }
        public int NoticeOpenFailureCount { get; private set; }
        public MxFairyGuiGuidanceDiagnosticCode LastDiagnosticCode { get; private set; }
        public string LastDiagnosticMessage => _lastDiagnosticMessage;
        public MxUiGuidanceRejectionReason LastStateRejectionReason =>
            _state.LastRejectionReason;
        public int NoticeOverflowCount => _state.NoticeOverflowCount;
        public MxFairyGuiGuidanceLayoutPreset LayoutPreset { get; private set; }

        /// <summary>Builds the two generated view registrations for a runtime shell.</summary>
        /// <remarks>AllocByDesign; intended for composition time only.</remarks>
        public static MxFairyGuiRuntimeCatalog CreateCatalog(
            IMxUiTextProvider textProvider = null)
        {
            IMxUiTextProvider safeTextProvider = textProvider ?? MxUiNullTextProvider.Instance;
            MxUiViewContract cardContract = MxPlayerGuidanceCardManifest.CreateViewContract();
            cardContract.Descriptor.Modal = false;
            cardContract.Descriptor.KeepAlive = false;
            cardContract.Descriptor.CloseOnSceneChange = true;
            cardContract.DiagnosticsTags = new[]
            {
                "player-guidance", "fairygui", "hud", "generated-manifest"
            };

            MxUiViewContract noticeContract = MxPlayerGuidanceNoticeStackManifest.CreateViewContract();
            noticeContract.Descriptor.Modal = false;
            noticeContract.Descriptor.KeepAlive = false;
            noticeContract.Descriptor.CloseOnSceneChange = true;
            noticeContract.DiagnosticsTags = new[]
            {
                "player-guidance", "fairygui", "toast", "generated-manifest"
            };

            var catalog = new MxFairyGuiRuntimeCatalog();
            catalog.Register(
                new MxFairyGuiRuntimeViewRegistration<MxFairyGuiGuidanceCardViewModel>(
                    cardContract,
                    MxPlayerGuidanceCardManifest.CreatePackageDescriptor(),
                    new MxPlayerGuidanceCardBinder(
                        null,
                        MxPlayerGuidanceFairyGuiIds.CardViewId,
                        safeTextProvider)));
            catalog.Register(
                new MxFairyGuiRuntimeViewRegistration<MxFairyGuiGuidanceNoticeStackViewModel>(
                    noticeContract,
                    MxPlayerGuidanceNoticeStackManifest.CreatePackageDescriptor(),
                    new MxPlayerGuidanceNoticeStackBinder(
                        null,
                        MxPlayerGuidanceFairyGuiIds.NoticeViewId,
                        safeTextProvider)));
            return catalog;
        }

        /// <summary>Shows or updates the one card when its scope revision is monotonic.</summary>
        /// <remarks>LowFreqAlloc. Rejected requests retain the previous valid card.</remarks>
        public void PresentCard(in MxUiGuidanceCardSnapshot snapshot)
        {
            if (!EnsureOwnerThread() || _disposed)
                return;

            bool hadVisibleCard = _state.HasCard && _state.Card.Visible;
            MxUiGuidanceScopeId previousScope = _state.HasCard
                ? _state.Card.ScopeId
                : default;
            if (!_state.TryPresentCard(in snapshot))
            {
                SetDiagnostic(MxFairyGuiGuidanceDiagnosticCode.CardRejected, string.Empty);
                return;
            }

            RefreshCardView();
            if (!snapshot.Visible || _cardRoot == null)
                return;

            bool newScope = !hadVisibleCard || previousScope != snapshot.ScopeId;
            _cardAnimation = newScope
                ? CardAnimationKind.Enter
                : CardAnimationKind.UpdateFade;
            _cardAnimationElapsed = 0f;
            _cardRoot.alpha = 0f;
            _cardRoot.x = newScope ? _cardBaseX - 12f : _cardBaseX;
        }

        /// <summary>Attempts to render one transient notice in the fixed two-slot capacity.</summary>
        /// <remarks>LowFreqAlloc. Returns false for invalid, wrong-thread, or overflow submissions.</remarks>
        public bool TryPushNotice(in MxUiGuidanceNotice notice)
        {
            if (!EnsureOwnerThread() || _disposed)
                return false;

            if (!_state.TryPushNotice(in notice))
            {
                SetDiagnostic(MxFairyGuiGuidanceDiagnosticCode.NoticeRejected, string.Empty);
                return false;
            }

            RefreshNoticeView();
            return true;
        }

        /// <summary>Clears only presentation state owned by the supplied scope.</summary>
        /// <remarks>NoAlloc after view initialization.</remarks>
        public void ClearScope(MxUiGuidanceScopeId scopeId)
        {
            if (!EnsureOwnerThread() || _disposed)
                return;

            long version = _state.Version;
            _state.ClearScope(scopeId);
            if (_state.LastRejectionReason != MxUiGuidanceRejectionReason.None)
            {
                SetDiagnostic(MxFairyGuiGuidanceDiagnosticCode.CardRejected, string.Empty);
                return;
            }

            if (_state.Version == version)
                return;

            RefreshCardView();
            RefreshNoticeView();
        }

        /// <summary>Changes between the frozen default and top-corners-occupied layouts.</summary>
        /// <remarks>LowFreqAlloc because active views are rebound.</remarks>
        public bool SetLayoutPreset(MxFairyGuiGuidanceLayoutPreset layoutPreset)
        {
            if (!EnsureOwnerThread() || _disposed)
                return false;
            if (!IsValidLayoutPreset(layoutPreset))
            {
                SetDiagnostic(MxFairyGuiGuidanceDiagnosticCode.InvalidLayoutPreset, string.Empty);
                return false;
            }
            if (LayoutPreset == layoutPreset)
                return true;

            LayoutPreset = layoutPreset;
            RefreshCardView();
            RefreshNoticeView();
            return true;
        }

        /// <summary>Advances notice lifetime, locale refresh, and fixed visual interpolation.</summary>
        /// <remarks>NoAlloc after successful view initialization.</remarks>
        public void Tick(float deltaSeconds)
        {
            if (!EnsureOwnerThread() || _disposed)
                return;
            if (!(deltaSeconds >= 0f) || float.IsNaN(deltaSeconds) || float.IsInfinity(deltaSeconds))
                return;

            long version = _state.Version;
            _state.Advance(deltaSeconds);
            if (_state.Version != version)
                RefreshNoticeView();

            long localeRevision = _textProvider.Revision;
            if (localeRevision != _localeRevision)
            {
                _localeRevision = localeRevision;
                RefreshCardView();
                RefreshNoticeView();
            }

            TickCardAnimation(deltaSeconds);
            TickNoticeAnimation();
        }

        /// <summary>Closes both views while leaving the shared navigator owned by the caller.</summary>
        /// <remarks>LowFreqAlloc teardown path.</remarks>
        public void Dispose()
        {
            if (_disposed)
                return;
            if (!EnsureOwnerThread())
                return;

            _disposed = true;
            _navigator.Close(MxPlayerGuidanceFairyGuiIds.CardViewId);
            _navigator.Close(MxPlayerGuidanceFairyGuiIds.NoticeViewId);
            _cardRoot = null;
            _noticeRoot = null;
            _notice0Visuals = null;
            _notice1Visuals = null;
            _cardViewReady = false;
            _noticeViewReady = false;
        }

        private void RefreshCardView()
        {
            BuildCardViewModel();
            MxUiOpenResult result = _navigator.Open(
                MxPlayerGuidanceFairyGuiIds.CardViewId,
                _cardViewModel);
            _cardViewReady = result.Success;
            if (!result.Success)
            {
                CardOpenFailureCount++;
                SetDiagnostic(
                    MxFairyGuiGuidanceDiagnosticCode.CardViewOpenFailed,
                    result.Message);
                return;
            }

            if (TryGetRoot(MxPlayerGuidanceFairyGuiIds.CardViewId, out Fgui.GComponent root))
            {
                _cardRoot = root;
                _cardBaseX = root.x;
            }
            ClearViewDiagnosticIfReady();
        }

        private void RefreshNoticeView()
        {
            long previous0 = _notice0VisualSequence;
            long previous1 = _notice1VisualSequence;
            BuildNoticeViewModel();
            MxUiOpenResult result = _navigator.Open(
                MxPlayerGuidanceFairyGuiIds.NoticeViewId,
                _noticeViewModel);
            _noticeViewReady = result.Success;
            if (!result.Success)
            {
                NoticeOpenFailureCount++;
                SetDiagnostic(
                    MxFairyGuiGuidanceDiagnosticCode.NoticeViewOpenFailed,
                    result.Message);
                return;
            }

            if (TryGetRoot(MxPlayerGuidanceFairyGuiIds.NoticeViewId, out Fgui.GComponent root))
            {
                _noticeRoot = root;
                EnsureNoticeVisualCaches(root);
                _notice0Visuals.CaptureBaseline();
                _notice1Visuals.CaptureBaseline();
            }

            _notice0VisualSequence = _noticeViewModel.Notice1Sequence;
            _notice1VisualSequence = _noticeViewModel.Notice2Sequence;
            _notice0Entering = _notice0VisualSequence > 0L
                && _notice0VisualSequence != previous0
                && _notice0VisualSequence != previous1;
            _notice1Entering = _notice1VisualSequence > 0L
                && _notice1VisualSequence != previous0
                && _notice1VisualSequence != previous1;
            ClearViewDiagnosticIfReady();
        }

        private void BuildCardViewModel()
        {
            _cardViewModel.LayoutPreset = LayoutPreset;
            if (!_state.HasCard)
            {
                _cardViewModel.Visible = false;
                _cardViewModel.Title = string.Empty;
                _cardViewModel.ActionLabel = string.Empty;
                _cardViewModel.ActionBody = string.Empty;
                _cardViewModel.FocusLabel = string.Empty;
                _cardViewModel.FocusBody = string.Empty;
                _cardViewModel.StatusLabel = string.Empty;
                _cardViewModel.StatusBody = string.Empty;
                return;
            }

            MxUiGuidanceCardSnapshot card = _state.Card;
            _cardViewModel.Visible = card.Visible;
            _cardViewModel.Title = Resolve(card.Title);
            _cardViewModel.HeaderGlyph = MxUiGuidanceGlyph.Context;
            ApplyLine(_cardViewModel, card.ActionLine, GuidanceLineSlot.Action, true);
            ApplyLine(_cardViewModel, card.FocusLine, GuidanceLineSlot.Focus, true);
            ApplyLine(_cardViewModel, card.StatusLine, GuidanceLineSlot.Status, true);
        }

        private void BuildNoticeViewModel()
        {
            _noticeViewModel.LayoutPreset = LayoutPreset;
            if (_state.TryGetNotice(0, out MxUiGuidanceNoticeState notice0))
            {
                _noticeViewModel.Notice1Visible = true;
                _noticeViewModel.Notice1Text = Resolve(notice0.Notice.Text);
                _noticeViewModel.Notice1Glyph = notice0.Notice.Glyph;
                _noticeViewModel.Notice1Tone = notice0.Notice.Tone;
                _noticeViewModel.Notice1Sequence = notice0.Sequence;
                _noticeViewModel.Notice1Revision = notice0.Revision;
            }
            else
            {
                _noticeViewModel.Notice1Visible = false;
                _noticeViewModel.Notice1Text = string.Empty;
                _noticeViewModel.Notice1Sequence = 0L;
                _noticeViewModel.Notice1Revision = 0L;
            }

            if (_state.TryGetNotice(1, out MxUiGuidanceNoticeState notice1))
            {
                _noticeViewModel.Notice2Visible = true;
                _noticeViewModel.Notice2Text = Resolve(notice1.Notice.Text);
                _noticeViewModel.Notice2Glyph = notice1.Notice.Glyph;
                _noticeViewModel.Notice2Tone = notice1.Notice.Tone;
                _noticeViewModel.Notice2Sequence = notice1.Sequence;
                _noticeViewModel.Notice2Revision = notice1.Revision;
            }
            else
            {
                _noticeViewModel.Notice2Visible = false;
                _noticeViewModel.Notice2Text = string.Empty;
                _noticeViewModel.Notice2Sequence = 0L;
                _noticeViewModel.Notice2Revision = 0L;
            }
        }

        private void TickCardAnimation(float deltaSeconds)
        {
            if (_cardAnimation == CardAnimationKind.None || _cardRoot == null)
                return;

            _cardAnimationElapsed += deltaSeconds;
            float duration = _cardAnimation == CardAnimationKind.Enter
                ? CardEnterDurationSeconds
                : CardUpdateFadeDurationSeconds;
            float progress = duration <= 0f
                ? 1f
                : Mathf.Clamp01(_cardAnimationElapsed / duration);
            _cardRoot.alpha = progress;
            _cardRoot.x = _cardAnimation == CardAnimationKind.Enter
                ? _cardBaseX - 12f * (1f - progress)
                : _cardBaseX;
            if (progress < 1f)
                return;

            _cardRoot.alpha = 1f;
            _cardRoot.x = _cardBaseX;
            _cardAnimation = CardAnimationKind.None;
        }

        private void TickNoticeAnimation()
        {
            if (_noticeRoot == null)
                return;

            if (_state.TryGetNotice(0, out MxUiGuidanceNoticeState notice0))
            {
                AnimateNotice(
                    _notice0Visuals,
                    in notice0,
                    ref _notice0Entering);
            }
            if (_state.TryGetNotice(1, out MxUiGuidanceNoticeState notice1))
            {
                AnimateNotice(
                    _notice1Visuals,
                    in notice1,
                    ref _notice1Entering);
            }
        }

        private static void AnimateNotice(
            NoticeVisualCache visuals,
            in MxUiGuidanceNoticeState state,
            ref bool entering)
        {
            if (visuals == null)
                return;

            float enterProgress = 1f;
            if (entering)
            {
                enterProgress = Mathf.Clamp01(
                    state.ElapsedSeconds / NoticeEnterDurationSeconds);
                if (enterProgress >= 1f)
                    entering = false;
            }

            float exitProgress = state.RemainingSeconds < NoticeExitDurationSeconds
                ? Mathf.Clamp01(state.RemainingSeconds / NoticeExitDurationSeconds)
                : 1f;
            float alpha = Mathf.Min(enterProgress, exitProgress);
            float xOffset = 24f * (1f - enterProgress);
            float yOffset = -8f * (1f - exitProgress);
            visuals.Apply(alpha, xOffset, yOffset);
        }

        private void EnsureNoticeVisualCaches(Fgui.GComponent root)
        {
            if (_notice0Visuals == null || !ReferenceEquals(_notice0Visuals.Root, root))
            {
                _notice0Visuals = new NoticeVisualCache(
                    root,
                    MxPlayerGuidanceFairyGuiIds.Notice1Chrome,
                    MxPlayerGuidanceFairyGuiIds.Notice1ToneOverlay,
                    MxPlayerGuidanceFairyGuiIds.Notice1Icon,
                    MxPlayerGuidanceFairyGuiIds.Notice1Text);
                _notice1Visuals = new NoticeVisualCache(
                    root,
                    MxPlayerGuidanceFairyGuiIds.Notice2Chrome,
                    MxPlayerGuidanceFairyGuiIds.Notice2ToneOverlay,
                    MxPlayerGuidanceFairyGuiIds.Notice2Icon,
                    MxPlayerGuidanceFairyGuiIds.Notice2Text);
            }
        }

        private bool TryGetRoot(MxUiViewId viewId, out Fgui.GComponent root)
        {
            root = null;
            if (!_navigator.TryGetOpenView(viewId, out IMxUiView view))
                return false;
            var fairyGuiView = view as IMxFairyGuiView;
            var handle = fairyGuiView?.Component as MxFairyGuiComponentHandle;
            root = handle?.Component;
            return root != null;
        }

        private string Resolve(MxUiLocalizedTextRequest request)
        {
            if (_textProvider.TryGetText(request, out string text)
                && !string.IsNullOrEmpty(text))
            {
                return text;
            }
            return request.FallbackText ?? string.Empty;
        }

        private static void ApplyLine(
            MxFairyGuiGuidanceCardViewModel target,
            in MxUiGuidanceLine line,
            GuidanceLineSlot slot)
        {
            switch (slot)
            {
                case GuidanceLineSlot.Action:
                    target.ActionGlyph = line.Glyph;
                    target.ActionTone = line.Tone;
                    break;
                case GuidanceLineSlot.Focus:
                    target.FocusGlyph = line.Glyph;
                    target.FocusTone = line.Tone;
                    break;
                default:
                    target.StatusGlyph = line.Glyph;
                    target.StatusTone = line.Tone;
                    break;
            }
        }

        private void ApplyLine(
            MxFairyGuiGuidanceCardViewModel target,
            in MxUiGuidanceLine line,
            GuidanceLineSlot slot,
            bool resolveText = true)
        {
            ApplyLine(target, in line, slot);
            if (!resolveText)
                return;
            switch (slot)
            {
                case GuidanceLineSlot.Action:
                    target.ActionLabel = Resolve(line.Label);
                    target.ActionBody = Resolve(line.Body);
                    break;
                case GuidanceLineSlot.Focus:
                    target.FocusLabel = Resolve(line.Label);
                    target.FocusBody = Resolve(line.Body);
                    break;
                default:
                    target.StatusLabel = Resolve(line.Label);
                    target.StatusBody = Resolve(line.Body);
                    break;
            }
        }

        private bool EnsureOwnerThread()
        {
            if (Thread.CurrentThread.ManagedThreadId == _ownerThreadId)
                return true;

            OwnerThreadViolationCount++;
            SetDiagnostic(MxFairyGuiGuidanceDiagnosticCode.WrongThread, string.Empty);
            return false;
        }

        private void SetDiagnostic(
            MxFairyGuiGuidanceDiagnosticCode code,
            string message)
        {
            LastDiagnosticCode = code;
            _lastDiagnosticMessage = message ?? string.Empty;
        }

        private void ClearViewDiagnosticIfReady()
        {
            if (!_cardViewReady || !_noticeViewReady)
                return;
            if (LastDiagnosticCode == MxFairyGuiGuidanceDiagnosticCode.CardViewOpenFailed
                || LastDiagnosticCode == MxFairyGuiGuidanceDiagnosticCode.NoticeViewOpenFailed)
            {
                SetDiagnostic(MxFairyGuiGuidanceDiagnosticCode.None, string.Empty);
            }
        }

        private static bool IsValidLayoutPreset(
            MxFairyGuiGuidanceLayoutPreset preset)
        {
            return preset >= MxFairyGuiGuidanceLayoutPreset.Default
                && preset <= MxFairyGuiGuidanceLayoutPreset.TopCornersOccupied;
        }

        private enum GuidanceLineSlot : byte
        {
            Action = 0,
            Focus = 1,
            Status = 2
        }

        private enum CardAnimationKind : byte
        {
            None = 0,
            Enter = 1,
            UpdateFade = 2
        }

        private sealed class NoticeVisualCache
        {
            private readonly Fgui.GObject[] _objects = new Fgui.GObject[4];
            private readonly float[] _baseX = new float[4];
            private readonly float[] _baseY = new float[4];

            public NoticeVisualCache(
                Fgui.GComponent root,
                string chromeName,
                string overlayName,
                string iconName,
                string textName)
            {
                Root = root;
                _objects[0] = root.GetChild(chromeName);
                _objects[1] = root.GetChild(overlayName);
                _objects[2] = root.GetChild(iconName);
                _objects[3] = root.GetChild(textName);
                CaptureBaseline();
            }

            public Fgui.GComponent Root { get; }

            public void CaptureBaseline()
            {
                for (int i = 0; i < _objects.Length; i++)
                {
                    Fgui.GObject item = _objects[i];
                    if (item == null)
                        continue;
                    _baseX[i] = item.x;
                    _baseY[i] = item.y;
                    item.alpha = 1f;
                }
            }

            public void Apply(float alpha, float xOffset, float yOffset)
            {
                for (int i = 0; i < _objects.Length; i++)
                {
                    Fgui.GObject item = _objects[i];
                    if (item == null || !item.visible)
                        continue;
                    item.alpha = alpha;
                    item.SetXY(_baseX[i] + xOffset, _baseY[i] + yOffset);
                }
            }
        }
    }
}
