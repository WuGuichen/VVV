using System;
using System.Collections.Generic;
using UnityEngine;
using Fgui = global::FairyGUI;

namespace MxFramework.UI.FairyGui
{
    public enum MxFairyGuiPopupPlacement
    {
        Auto,
        Above,
        Below,
        Left,
        Right
    }

    public enum MxFairyGuiPopupCloseReason
    {
        OutsidePointer,
        Toggle,
        Cancel,
        Replaced,
        OwnerClosed,
        ServiceDisposed
    }

    /// <summary>Configures a lightweight FairyGUI popup.</summary>
    /// <remarks>NoAlloc. The value is copied into the popup registration.</remarks>
    public readonly struct MxFairyGuiPopupOptions
    {
        public MxFairyGuiPopupOptions(
            MxFairyGuiPopupPlacement placement = MxFairyGuiPopupPlacement.Auto,
            float safeMargin = 12f,
            float anchorGap = 10f)
        {
            Placement = placement;
            SafeMargin = safeMargin;
            AnchorGap = anchorGap;
        }

        public MxFairyGuiPopupPlacement Placement { get; }
        public float SafeMargin { get; }
        public float AnchorGap { get; }

        internal MxFairyGuiPopupOptions Normalize()
        {
            return new MxFairyGuiPopupOptions(
                Placement,
                SafeMargin > 0f ? SafeMargin : 12f,
                AnchorGap >= 0f ? AnchorGap : 10f);
        }
    }

    public interface IMxFairyGuiPopupService : IDisposable
    {
        bool HasOpenPopup { get; }
        Fgui.GObject TopPopup { get; }

        /// <remarks>LowFreqAlloc: creates one lifecycle registration per newly shown popup.</remarks>
        bool Show(
            MxUiViewId ownerViewId,
            Fgui.GObject popup,
            Fgui.GObject anchor,
            MxFairyGuiPopupOptions options = default,
            Action<MxFairyGuiPopupCloseReason> onClosed = null);

        /// <remarks>LowFreqAlloc when opening; closing an existing popup does not allocate.</remarks>
        bool Toggle(
            MxUiViewId ownerViewId,
            Fgui.GObject popup,
            Fgui.GObject anchor,
            MxFairyGuiPopupOptions options = default,
            Action<MxFairyGuiPopupCloseReason> onClosed = null);

        bool Hide(
            Fgui.GObject popup,
            MxFairyGuiPopupCloseReason reason = MxFairyGuiPopupCloseReason.OwnerClosed);

        bool HideTop(
            MxFairyGuiPopupCloseReason reason = MxFairyGuiPopupCloseReason.Cancel);

        void HideOwner(
            MxUiViewId ownerViewId,
            MxFairyGuiPopupCloseReason reason = MxFairyGuiPopupCloseReason.OwnerClosed);

        void HideAll(
            MxFairyGuiPopupCloseReason reason = MxFairyGuiPopupCloseReason.OwnerClosed);

        bool IsOpen(Fgui.GObject popup);
    }

    /// <summary>
    /// Owns lightweight FairyGUI popups while preserving FairyGUI's native outside-click stack.
    /// </summary>
    /// <remarks>
    /// Low-frequency UI service. Show/Toggle may allocate a lifecycle registration; no method is
    /// intended for Tick, Update, rendering passes, or gameplay command loops.
    /// </remarks>
    public sealed class MxFairyGuiPopupService : IMxFairyGuiPopupService
    {
        private readonly List<Registration> _stack = new List<Registration>(4);
        private readonly IMxFairyGuiPopupRoot _popupRoot;
        private bool _disposed;

        public MxFairyGuiPopupService()
            : this(new MxFairyGuiNativePopupRoot())
        {
        }

        internal MxFairyGuiPopupService(IMxFairyGuiPopupRoot popupRoot)
        {
            _popupRoot = popupRoot ?? throw new ArgumentNullException(nameof(popupRoot));
        }

        public bool HasOpenPopup
        {
            get
            {
                PruneClosed();
                return _stack.Count > 0;
            }
        }

        public Fgui.GObject TopPopup
        {
            get
            {
                PruneClosed();
                return _stack.Count > 0 ? _stack[_stack.Count - 1].Popup : null;
            }
        }

        public bool Show(
            MxUiViewId ownerViewId,
            Fgui.GObject popup,
            Fgui.GObject anchor,
            MxFairyGuiPopupOptions options = default,
            Action<MxFairyGuiPopupCloseReason> onClosed = null)
        {
            ThrowIfDisposed();
            PruneClosed();
            if (!ownerViewId.IsValid
                || popup == null
                || anchor == null
                || popup.displayObject == null
                || popup.displayObject.isDisposed
                || anchor.displayObject == null
                || anchor.displayObject.isDisposed)
            {
                return false;
            }

            if (IsOpen(popup))
                return true;

            if (_stack.Count > 0 && !IsDescendantOf(anchor, _stack[_stack.Count - 1].Popup))
                HideAll(MxFairyGuiPopupCloseReason.Replaced);

            MxFairyGuiPopupOptions normalized = options.Normalize();
            if (popup.parent != null)
                popup.RemoveFromParent();
            var registration = new Registration(
                ownerViewId,
                popup,
                normalized,
                onClosed,
                OnPopupRemoved);
            popup.displayObject.onRemovedFromStage.Add(registration.RemovedHandler);
            _stack.Add(registration);

            _popupRoot.TogglePopup(
                popup,
                anchor,
                ResolveDirection(normalized.Placement),
                false);
            if (_popupRoot.IsAttached(popup))
            {
                ApplySidePlacement(_popupRoot, popup, anchor, normalized);
                ClampToSafeArea(_popupRoot, popup, normalized.SafeMargin);
                return true;
            }

            PruneClosed();
            return false;
        }

        public bool Toggle(
            MxUiViewId ownerViewId,
            Fgui.GObject popup,
            Fgui.GObject anchor,
            MxFairyGuiPopupOptions options = default,
            Action<MxFairyGuiPopupCloseReason> onClosed = null)
        {
            ThrowIfDisposed();
            if (IsOpen(popup))
                return Hide(popup, MxFairyGuiPopupCloseReason.Toggle);

            return Show(ownerViewId, popup, anchor, options, onClosed);
        }

        public bool Hide(
            Fgui.GObject popup,
            MxFairyGuiPopupCloseReason reason = MxFairyGuiPopupCloseReason.OwnerClosed)
        {
            ThrowIfDisposed();
            PruneClosed();
            int index = Find(popup);
            if (index < 0)
                return false;

            for (int i = index; i < _stack.Count; i++)
                _stack[i].PendingReason = reason;
            _popupRoot.HidePopup(popup);
            PruneClosed();
            return true;
        }

        public bool HideTop(
            MxFairyGuiPopupCloseReason reason = MxFairyGuiPopupCloseReason.Cancel)
        {
            ThrowIfDisposed();
            PruneClosed();
            return _stack.Count > 0 && Hide(_stack[_stack.Count - 1].Popup, reason);
        }

        public void HideOwner(
            MxUiViewId ownerViewId,
            MxFairyGuiPopupCloseReason reason = MxFairyGuiPopupCloseReason.OwnerClosed)
        {
            ThrowIfDisposed();
            PruneClosed();
            for (int i = _stack.Count - 1; i >= 0; i--)
            {
                if (_stack[i].OwnerViewId == ownerViewId)
                    Hide(_stack[i].Popup, reason);
            }
        }

        public void HideAll(
            MxFairyGuiPopupCloseReason reason = MxFairyGuiPopupCloseReason.OwnerClosed)
        {
            ThrowIfDisposed();
            PruneClosed();
            if (_stack.Count == 0)
                return;

            for (int i = 0; i < _stack.Count; i++)
                _stack[i].PendingReason = reason;
            _popupRoot.HideAll();
            PruneClosed();
        }

        public bool IsOpen(Fgui.GObject popup)
        {
            if (_disposed || popup == null)
                return false;

            PruneClosed();
            return Find(popup) >= 0 && _popupRoot.IsAttached(popup);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            HideAllCore(MxFairyGuiPopupCloseReason.ServiceDisposed);
            _disposed = true;
        }

        internal static Vector2 ClampPosition(
            Vector2 position,
            Vector2 popupSize,
            Vector2 rootSize,
            float safeMargin)
        {
            float margin = Mathf.Max(0f, safeMargin);
            float maxX = Mathf.Max(margin, rootSize.x - popupSize.x - margin);
            float maxY = Mathf.Max(margin, rootSize.y - popupSize.y - margin);
            return new Vector2(
                Mathf.Clamp(position.x, margin, maxX),
                Mathf.Clamp(position.y, margin, maxY));
        }

        private void HideAllCore(MxFairyGuiPopupCloseReason reason)
        {
            PruneClosed();
            if (_stack.Count == 0)
                return;

            for (int i = 0; i < _stack.Count; i++)
                _stack[i].PendingReason = reason;
            _popupRoot.HideAll();
            PruneClosed();
        }

        private void OnPopupRemoved(Registration registration)
        {
            if (registration == null || registration.Closed)
                return;

            registration.Closed = true;
            if (registration.Popup?.displayObject != null
                && !registration.Popup.displayObject.isDisposed)
            {
                registration.Popup.displayObject.onRemovedFromStage.Remove(
                    registration.RemovedHandler);
            }
            _stack.Remove(registration);
            registration.OnClosed?.Invoke(registration.PendingReason);
        }

        private void PruneClosed()
        {
            for (int i = _stack.Count - 1; i >= 0; i--)
            {
                Registration registration = _stack[i];
                if (registration.Popup != null
                    && registration.Popup.displayObject != null
                    && !registration.Popup.displayObject.isDisposed
                    && _popupRoot.IsAttached(registration.Popup))
                {
                    continue;
                }

                OnPopupRemoved(registration);
            }
        }

        private int Find(Fgui.GObject popup)
        {
            for (int i = 0; i < _stack.Count; i++)
            {
                if (ReferenceEquals(_stack[i].Popup, popup))
                    return i;
            }
            return -1;
        }

        private static bool IsDescendantOf(Fgui.GObject value, Fgui.GObject ancestor)
        {
            Fgui.GObject current = value;
            while (current != null)
            {
                if (ReferenceEquals(current, ancestor))
                    return true;
                current = current.parent;
            }
            return false;
        }

        private static Fgui.PopupDirection ResolveDirection(
            MxFairyGuiPopupPlacement placement)
        {
            switch (placement)
            {
                case MxFairyGuiPopupPlacement.Above:
                    return Fgui.PopupDirection.Up;
                case MxFairyGuiPopupPlacement.Below:
                    return Fgui.PopupDirection.Down;
                default:
                    return Fgui.PopupDirection.Auto;
            }
        }

        private static void ApplySidePlacement(
            IMxFairyGuiPopupRoot root,
            Fgui.GObject popup,
            Fgui.GObject anchor,
            MxFairyGuiPopupOptions options)
        {
            if (options.Placement != MxFairyGuiPopupPlacement.Left
                && options.Placement != MxFairyGuiPopupPlacement.Right)
            {
                return;
            }

            Vector2 anchorPosition = root.GetAnchorPosition(anchor);
            float leftX = anchorPosition.x - popup.width - options.AnchorGap;
            float rightX = anchorPosition.x + anchor.width + options.AnchorGap;
            float margin = options.SafeMargin;
            float x;
            if (options.Placement == MxFairyGuiPopupPlacement.Left)
            {
                x = leftX;
                if (x < margin
                    && rightX + popup.width <= root.Size.x - margin)
                {
                    x = rightX;
                }
            }
            else
            {
                x = rightX;
                if (x + popup.width > root.Size.x - margin
                    && leftX >= margin)
                {
                    x = leftX;
                }
            }
            float y = anchorPosition.y;
            popup.SetPosition(Mathf.Round(x), Mathf.Round(y), popup.z);
        }

        private static void ClampToSafeArea(
            IMxFairyGuiPopupRoot root,
            Fgui.GObject popup,
            float safeMargin)
        {
            Vector2 clamped = ClampPosition(
                popup.xy,
                popup.size,
                root.Size,
                safeMargin);
            popup.SetPosition(
                Mathf.Round(clamped.x),
                Mathf.Round(clamped.y),
                popup.z);
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(MxFairyGuiPopupService));
        }

        private sealed class Registration
        {
            public Registration(
                MxUiViewId ownerViewId,
                Fgui.GObject popup,
                MxFairyGuiPopupOptions options,
                Action<MxFairyGuiPopupCloseReason> onClosed,
                Action<Registration> removed)
            {
                OwnerViewId = ownerViewId;
                Popup = popup;
                Options = options;
                OnClosed = onClosed;
                PendingReason = MxFairyGuiPopupCloseReason.OutsidePointer;
                RemovedHandler = () => removed(this);
            }

            public MxUiViewId OwnerViewId { get; }
            public Fgui.GObject Popup { get; }
            public MxFairyGuiPopupOptions Options { get; }
            public Action<MxFairyGuiPopupCloseReason> OnClosed { get; }
            public Fgui.EventCallback0 RemovedHandler { get; }
            public MxFairyGuiPopupCloseReason PendingReason { get; set; }
            public bool Closed { get; set; }
        }
    }

    internal interface IMxFairyGuiPopupRoot
    {
        Vector2 Size { get; }
        bool IsAttached(Fgui.GObject popup);
        Vector2 GetAnchorPosition(Fgui.GObject anchor);
        void TogglePopup(
            Fgui.GObject popup,
            Fgui.GObject anchor,
            Fgui.PopupDirection direction,
            bool closeUntilUpEvent);
        void HidePopup(Fgui.GObject popup);
        void HideAll();
    }

    internal sealed class MxFairyGuiNativePopupRoot : IMxFairyGuiPopupRoot
    {
        private Fgui.GRoot Root => Fgui.GRoot.inst;

        public Vector2 Size => Root.size;

        public bool IsAttached(Fgui.GObject popup)
        {
            return popup != null && popup.parent == Root;
        }

        public Vector2 GetAnchorPosition(Fgui.GObject anchor)
        {
            return Root.GlobalToLocal(anchor.LocalToGlobal(Vector2.zero));
        }

        public void TogglePopup(
            Fgui.GObject popup,
            Fgui.GObject anchor,
            Fgui.PopupDirection direction,
            bool closeUntilUpEvent)
        {
            Root.TogglePopup(popup, anchor, direction, closeUntilUpEvent);
        }

        public void HidePopup(Fgui.GObject popup)
        {
            Root.HidePopup(popup);
        }

        public void HideAll()
        {
            Root.HidePopup();
        }
    }
}
