using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using Fgui = global::FairyGUI;

[assembly: InternalsVisibleTo("MxFramework.Tests.UI.FairyGUI")]

namespace MxFramework.UI.FairyGui
{
    /// <summary>
    /// Defines the preferred side used to place a FairyGUI tooltip.
    /// </summary>
    public enum MxFairyGuiTooltipPlacement
    {
        Auto = 0,
        Below = 1,
        Above = 2,
        Right = 3,
        Left = 4,
        Cursor = 5
    }

    /// <summary>
    /// Configures tooltip positioning, sizing, and optional pin behavior.
    /// </summary>
    /// <remarks>
    /// Allocation: NoAlloc. The value is copied into the tooltip binding.
    /// </remarks>
    public readonly struct MxFairyGuiTooltipOptions
    {
        /// <summary>
        /// Creates tooltip options. Zero-valued sizing arguments use framework defaults.
        /// </summary>
        public MxFairyGuiTooltipOptions(
            MxFairyGuiTooltipPlacement preferredPlacement = MxFairyGuiTooltipPlacement.Auto,
            float gap = 10f,
            float safeMargin = 12f,
            float minimumTextWidth = 96f,
            float maximumTextWidth = 420f,
            bool allowPin = true)
        {
            PreferredPlacement = preferredPlacement;
            Gap = gap;
            SafeMargin = safeMargin;
            MinimumTextWidth = minimumTextWidth;
            MaximumTextWidth = maximumTextWidth;
            AllowPin = allowPin;
        }

        /// <summary>Gets the preferred placement policy.</summary>
        public MxFairyGuiTooltipPlacement PreferredPlacement { get; }

        /// <summary>Gets the gap between the target and tooltip in logical pixels.</summary>
        public float Gap { get; }

        /// <summary>Gets the root-edge safety margin in logical pixels.</summary>
        public float SafeMargin { get; }

        /// <summary>Gets the minimum tooltip text width.</summary>
        public float MinimumTextWidth { get; }

        /// <summary>Gets the maximum tooltip text width.</summary>
        public float MaximumTextWidth { get; }

        /// <summary>Gets whether the tooltip may enter a pinned state.</summary>
        public bool AllowPin { get; }

        /// <summary>Gets the framework default tooltip options.</summary>
        public static MxFairyGuiTooltipOptions Default =>
            new MxFairyGuiTooltipOptions();

        internal MxFairyGuiTooltipOptions Normalize()
        {
            float gap = Gap > 0f ? Gap : 10f;
            float safeMargin = SafeMargin > 0f ? SafeMargin : 12f;
            float minimumWidth = MinimumTextWidth > 0f ? MinimumTextWidth : 96f;
            float maximumWidth = MaximumTextWidth >= minimumWidth
                ? MaximumTextWidth
                : Mathf.Max(420f, minimumWidth);
            bool isDefaultValue = PreferredPlacement == MxFairyGuiTooltipPlacement.Auto
                && Gap == 0f
                && SafeMargin == 0f
                && MinimumTextWidth == 0f
                && MaximumTextWidth == 0f
                && !AllowPin;
            return new MxFairyGuiTooltipOptions(
                PreferredPlacement,
                gap,
                safeMargin,
                minimumWidth,
                maximumWidth,
                AllowPin || isDefaultValue);
        }
    }

    internal readonly struct MxFairyGuiTooltipPlacementResult
    {
        public MxFairyGuiTooltipPlacementResult(
            Vector2 position,
            MxFairyGuiTooltipPlacement placement)
        {
            Position = position;
            Placement = placement;
        }

        public Vector2 Position { get; }

        public MxFairyGuiTooltipPlacement Placement { get; }
    }

    /// <summary>
    /// Owns FairyGUI hover-tooltip lifetime, optional pinning, and edge-aware placement.
    /// </summary>
    /// <remarks>
    /// Allocation: AllocByDesign. Construction creates bounded binding storage; the first visible
    /// tooltip creates its visual tree. Bind and text updates are low-frequency UI operations.
    /// Clicks do not dismiss transient tooltips while the pointer remains over the bound target.
    /// </remarks>
    public sealed class MxFairyGuiTooltipService : IDisposable
    {
        internal const int DefaultFontSize = 18;
        internal const int DefaultLineSpacing = 6;
        internal const float HorizontalPadding = 16f;
        internal const float VerticalPadding = 11f;
        private static readonly float[] CornerRadii = { 6f, 6f, 6f, 6f };
        private static readonly MxFairyGuiTooltipPlacement[] CursorPlacementOrder =
        {
            MxFairyGuiTooltipPlacement.Cursor,
            MxFairyGuiTooltipPlacement.Below,
            MxFairyGuiTooltipPlacement.Above,
            MxFairyGuiTooltipPlacement.Right,
            MxFairyGuiTooltipPlacement.Left
        };
        private static readonly MxFairyGuiTooltipPlacement[] WidePlacementOrder =
        {
            MxFairyGuiTooltipPlacement.Below,
            MxFairyGuiTooltipPlacement.Above,
            MxFairyGuiTooltipPlacement.Right,
            MxFairyGuiTooltipPlacement.Left
        };
        private static readonly MxFairyGuiTooltipPlacement[] AbovePlacementOrder =
        {
            MxFairyGuiTooltipPlacement.Above,
            MxFairyGuiTooltipPlacement.Below,
            MxFairyGuiTooltipPlacement.Right,
            MxFairyGuiTooltipPlacement.Left
        };
        private static readonly MxFairyGuiTooltipPlacement[] RightPlacementOrder =
        {
            MxFairyGuiTooltipPlacement.Right,
            MxFairyGuiTooltipPlacement.Left,
            MxFairyGuiTooltipPlacement.Below,
            MxFairyGuiTooltipPlacement.Above
        };
        private static readonly MxFairyGuiTooltipPlacement[] LeftPlacementOrder =
        {
            MxFairyGuiTooltipPlacement.Left,
            MxFairyGuiTooltipPlacement.Right,
            MxFairyGuiTooltipPlacement.Below,
            MxFairyGuiTooltipPlacement.Above
        };
        private static readonly List<WeakReference<MxFairyGuiTooltipService>> Instances =
            new List<WeakReference<MxFairyGuiTooltipService>>(8);

        private readonly Dictionary<Fgui.GObject, Binding> _bindings =
            new Dictionary<Fgui.GObject, Binding>(32);
        private readonly Fgui.TimerCallback _validateActiveTargetCallback;
        private Fgui.GComponent _window;
        private Fgui.GGraph _border;
        private Fgui.GGraph _background;
        private Fgui.GTextField _label;
        private Fgui.GObject _activeTarget;
        private string _activeText = string.Empty;
        private bool _isPinned;
        private bool _validationPending;
        private bool _disposed;

        /// <summary>Creates and registers a tooltip service for shared root-level dismissal.</summary>
        /// <remarks>Allocation: LowFreqAlloc.</remarks>
        public MxFairyGuiTooltipService()
        {
            _validateActiveTargetCallback = ValidateActiveTargetAfterInteraction;
            Instances.Add(new WeakReference<MxFairyGuiTooltipService>(this));
        }

        /// <summary>Gets whether the shared tooltip window is currently visible.</summary>
        public bool IsVisible => _window != null && _window.parent != null;

        /// <summary>Gets whether the active tooltip is pinned.</summary>
        public bool IsPinned => _isPinned;

        /// <summary>Gets the currently displayed tooltip text.</summary>
        public string ActiveText => _activeText;

        /// <summary>Gets the object that currently owns the tooltip.</summary>
        public Fgui.GObject ActiveTarget => _activeTarget;

        /// <summary>
        /// Adds or updates a tooltip binding. An empty text removes the binding.
        /// </summary>
        /// <remarks>
        /// Allocation: LowFreqAlloc. A new or changed binding may allocate dictionary storage.
        /// </remarks>
        public void Bind(
            Fgui.GObject target,
            string text,
            MxFairyGuiTooltipOptions options = default)
        {
            ThrowIfDisposed();
            if (target == null)
                return;

            target.tooltips = string.Empty;
            target.onRollOver.Remove(HandleRollOver);
            target.onRollOut.Remove(HandleRollOut);
            target.onClick.Remove(HandleTargetClick);
            RemoveTargetLifecycleHandler(target);
            if (string.IsNullOrEmpty(text))
            {
                _bindings.Remove(target);
                if (ReferenceEquals(_activeTarget, target))
                    Hide();
                return;
            }

            Binding binding = new Binding(text, options.Normalize());
            _bindings[target] = binding;
            target.onRollOver.Add(HandleRollOver);
            target.onRollOut.Add(HandleRollOut);
            target.onClick.Add(HandleTargetClick);
            AddTargetLifecycleHandler(target);
            if (!ReferenceEquals(_activeTarget, target))
                return;

            if (IsTargetAvailable(target))
                Show(target, binding);
            else
                Hide();
        }

        /// <summary>Removes a tooltip binding from a target.</summary>
        /// <remarks>Allocation: NoAlloc after the binding dictionary is initialized.</remarks>
        public void Unbind(Fgui.GObject target)
        {
            if (_disposed || target == null)
                return;
            Bind(target, string.Empty);
        }

        /// <summary>
        /// Pins the active tooltip so pointer exit does not dismiss it.
        /// </summary>
        /// <remarks>Allocation: NoAlloc.</remarks>
        public bool PinActive()
        {
            ThrowIfDisposed();
            if (_activeTarget == null
                || !_bindings.TryGetValue(_activeTarget, out Binding binding)
                || !binding.Options.AllowPin)
            {
                return false;
            }

            _isPinned = true;
            if (_window != null)
                _window.touchable = true;
            return true;
        }

        /// <summary>Returns the active tooltip to transient hover behavior.</summary>
        /// <remarks>Allocation: NoAlloc.</remarks>
        public bool Unpin()
        {
            ThrowIfDisposed();
            if (!_isPinned)
                return false;
            _isPinned = false;
            if (_window != null)
                _window.touchable = false;
            if (!IsTargetAvailable(_activeTarget))
            {
                Hide();
            }
            return true;
        }

        /// <summary>Immediately hides the active tooltip and clears its state.</summary>
        /// <remarks>Allocation: NoAlloc.</remarks>
        public void Hide()
        {
            if (_disposed)
                return;
            _window?.RemoveFromParent();
            _activeTarget = null;
            _activeText = string.Empty;
            _isPinned = false;
            if (_window != null)
                _window.touchable = false;
        }

        /// <summary>Hides every live custom tooltip window across FairyGUI views.</summary>
        /// <remarks>
        /// Allocation: NoAlloc after tooltip services are initialized. Intended for low-frequency
        /// navigation boundaries such as opening a popover or replacing a full-screen surface.
        /// </remarks>
        public static void HideAllVisible()
        {
            for (int i = Instances.Count - 1; i >= 0; i--)
            {
                if (!Instances[i].TryGetTarget(out MxFairyGuiTooltipService service)
                    || service == null
                    || service._disposed)
                {
                    Instances.RemoveAt(i);
                    continue;
                }

                service.Hide();
            }
        }

        /// <summary>Releases bindings and the lazily-created visual tree.</summary>
        /// <remarks>Allocation: NoAlloc.</remarks>
        public void Dispose()
        {
            if (_disposed)
                return;
            foreach (KeyValuePair<Fgui.GObject, Binding> pair in _bindings)
            {
                Fgui.GObject target = pair.Key;
                if (target == null || target.isDisposed)
                    continue;
                target.onRollOver.Remove(HandleRollOver);
                target.onRollOut.Remove(HandleRollOut);
                target.onClick.Remove(HandleTargetClick);
                RemoveTargetLifecycleHandler(target);
            }
            _bindings.Clear();
            if (_validationPending)
            {
                Fgui.Timers.inst.Remove(_validateActiveTargetCallback);
                _validationPending = false;
            }
            _window?.RemoveFromParent();
            _window?.Dispose();
            _window = null;
            _border = null;
            _background = null;
            _label = null;
            _activeTarget = null;
            _activeText = string.Empty;
            _disposed = true;
        }

        private void HandleRollOver(Fgui.EventContext context)
        {
            Fgui.GObject target = context?.sender as Fgui.GObject;
            if (target == null || !_bindings.TryGetValue(target, out Binding binding))
                return;
            Show(target, binding);
        }

        private void HandleRollOut(Fgui.EventContext context)
        {
            Fgui.GObject target = context?.sender as Fgui.GObject;
            if (target == null
                || !ReferenceEquals(_activeTarget, target)
                || _isPinned)
            {
                return;
            }
            Hide();
        }

        private void HandleTargetClick(Fgui.EventContext context)
        {
            Fgui.GObject target = context?.sender as Fgui.GObject;
            if (!ReferenceEquals(_activeTarget, target))
                return;

            // The action callback may hide the target before or after this listener runs.
            // Validate now for the former case, then once more after the click dispatch finishes.
            ValidateActiveTargetAfterInteraction(null);
            if (_activeTarget == null || _validationPending)
                return;

            _validationPending = true;
            Fgui.Timers.inst.CallLater(_validateActiveTargetCallback);
        }

        private void HandleTargetRemovedFromStage(Fgui.EventContext context)
        {
            Fgui.DisplayObject displayObject = context?.sender as Fgui.DisplayObject;
            if (displayObject != null && ReferenceEquals(_activeTarget, displayObject.gOwner))
                Hide();
        }

        private void ValidateActiveTargetAfterInteraction(object _)
        {
            _validationPending = false;
            if (!_disposed && _activeTarget != null && !IsTargetAvailable(_activeTarget))
                Hide();
        }

        private void Show(Fgui.GObject target, Binding binding)
        {
            if (!IsTargetAvailable(target))
                return;

            Fgui.GRoot root = target.root;
            if (root == null)
                return;

            EnsureWindow();
            _label.autoSize = Fgui.AutoSizeType.Both;
            _label.text = binding.Text;
            float textWidth = Mathf.Clamp(
                Mathf.Ceil(_label.textWidth),
                binding.Options.MinimumTextWidth,
                binding.Options.MaximumTextWidth);
            _label.autoSize = Fgui.AutoSizeType.Height;
            _label.SetSize(textWidth, _label.height);
            float textHeight = Mathf.Ceil(_label.textHeight);
            _label.SetXY(HorizontalPadding, VerticalPadding);
            _label.SetSize(textWidth, textHeight);

            Vector2 tooltipSize = new Vector2(
                textWidth + HorizontalPadding * 2f,
                textHeight + VerticalPadding * 2f);
            _window.SetSize(tooltipSize.x, tooltipSize.y);
            _border.SetSize(tooltipSize.x, tooltipSize.y);
            _border.DrawRoundRect(
                tooltipSize.x,
                tooltipSize.y,
                new Color32(70, 91, 101, 255),
                CornerRadii);
            _background.SetXY(1f, 1f);
            _background.SetSize(tooltipSize.x - 2f, tooltipSize.y - 2f);
            _background.DrawRoundRect(
                tooltipSize.x - 2f,
                tooltipSize.y - 2f,
                new Color32(11, 22, 29, 248),
                CornerRadii);

            Rect targetRect = ResolveTargetRect(root, target);
            Vector2 pointer = root.GlobalToLocal(Fgui.Stage.inst.touchPosition);
            MxFairyGuiTooltipPlacementResult placement = CalculatePlacement(
                targetRect,
                pointer,
                tooltipSize,
                new Vector2(root.width, root.height),
                binding.Options);
            _window.SetXY(
                Mathf.Round(placement.Position.x),
                Mathf.Round(placement.Position.y));
            _window.RemoveFromParent();
            root.AddChild(_window);

            bool keepPinned = _isPinned && ReferenceEquals(_activeTarget, target);
            _activeTarget = target;
            _activeText = binding.Text;
            _isPinned = keepPinned;
            _window.touchable = _isPinned;
        }

        private void EnsureWindow()
        {
            if (_window != null && !_window.isDisposed)
                return;

            _window = new Fgui.GComponent
            {
                name = "mxFairyGuiTooltip",
                touchable = false
            };
            _border = new Fgui.GGraph
            {
                name = "border",
                touchable = false
            };
            _background = new Fgui.GGraph
            {
                name = "background",
                touchable = false
            };
            _label = new Fgui.GTextField
            {
                name = "label",
                touchable = false,
                color = new Color32(218, 228, 232, 255),
                autoSize = Fgui.AutoSizeType.Height
            };
            Fgui.TextFormat format = _label.textFormat;
            format.size = DefaultFontSize;
            format.lineSpacing = DefaultLineSpacing;
            format.color = _label.color;
            _label.textFormat = format;
            _window.AddChild(_border);
            _window.AddChild(_background);
            _window.AddChild(_label);
        }

        private static Rect ResolveTargetRect(Fgui.GRoot root, Fgui.GObject target)
        {
            Vector2 topLeft = root.GlobalToLocal(target.LocalToGlobal(Vector2.zero));
            Vector2 bottomRight = root.GlobalToLocal(
                target.LocalToGlobal(new Vector2(target.width, target.height)));
            return Rect.MinMaxRect(
                Mathf.Min(topLeft.x, bottomRight.x),
                Mathf.Min(topLeft.y, bottomRight.y),
                Mathf.Max(topLeft.x, bottomRight.x),
                Mathf.Max(topLeft.y, bottomRight.y));
        }

        private void AddTargetLifecycleHandler(Fgui.GObject target)
        {
            if (target.displayObject == null || target.displayObject.isDisposed)
                return;
            target.displayObject.onRemovedFromStage.Remove(HandleTargetRemovedFromStage);
            target.displayObject.onRemovedFromStage.Add(HandleTargetRemovedFromStage);
        }

        private void RemoveTargetLifecycleHandler(Fgui.GObject target)
        {
            if (target.displayObject == null || target.displayObject.isDisposed)
                return;
            target.displayObject.onRemovedFromStage.Remove(HandleTargetRemovedFromStage);
        }

        private static bool IsTargetAvailable(Fgui.GObject target)
        {
            if (target == null
                || target.isDisposed
                || target.displayObject == null
                || target.displayObject.isDisposed
                || target.root == null
                || !IsTargetVisibleAndEnabledInHierarchy(target))
            {
                return false;
            }

            return true;
        }

        internal static bool IsTargetVisibleAndEnabledInHierarchy(Fgui.GObject target)
        {
            if (target == null || target.isDisposed || !target.visible || !target.enabled)
                return false;
            for (Fgui.GObject current = target.parent; current != null; current = current.parent)
            {
                if (!current.visible || !current.enabled)
                    return false;
            }
            return true;
        }

        internal static MxFairyGuiTooltipPlacementResult CalculatePlacement(
            Rect target,
            Vector2 pointer,
            Vector2 tooltipSize,
            Vector2 rootSize,
            MxFairyGuiTooltipOptions options)
        {
            options = options.Normalize();
            MxFairyGuiTooltipPlacement[] order = BuildPlacementOrder(
                target,
                rootSize,
                options.PreferredPlacement);
            Rect safe = Rect.MinMaxRect(
                options.SafeMargin,
                options.SafeMargin,
                Mathf.Max(options.SafeMargin, rootSize.x - options.SafeMargin),
                Mathf.Max(options.SafeMargin, rootSize.y - options.SafeMargin));
            for (int i = 0; i < order.Length; i++)
            {
                Vector2 candidate = CalculateCandidate(
                    order[i],
                    target,
                    pointer,
                    tooltipSize,
                    options.Gap);
                if (Fits(candidate, tooltipSize, safe))
                    return new MxFairyGuiTooltipPlacementResult(candidate, order[i]);
            }

            MxFairyGuiTooltipPlacement fallback = order[0];
            Vector2 unclamped = CalculateCandidate(
                fallback,
                target,
                pointer,
                tooltipSize,
                options.Gap);
            Vector2 clamped = new Vector2(
                Mathf.Clamp(
                    unclamped.x,
                    safe.xMin,
                    Mathf.Max(safe.xMin, safe.xMax - tooltipSize.x)),
                Mathf.Clamp(
                    unclamped.y,
                    safe.yMin,
                    Mathf.Max(safe.yMin, safe.yMax - tooltipSize.y)));
            return new MxFairyGuiTooltipPlacementResult(clamped, fallback);
        }

        private static MxFairyGuiTooltipPlacement[] BuildPlacementOrder(
            Rect target,
            Vector2 rootSize,
            MxFairyGuiTooltipPlacement preferred)
        {
            if (preferred == MxFairyGuiTooltipPlacement.Cursor)
                return CursorPlacementOrder;
            if (preferred != MxFairyGuiTooltipPlacement.Auto)
                return BuildExplicitOrder(preferred);

            bool wide = target.width >= target.height * 2f;
            if (wide)
                return WidePlacementOrder;

            bool onLeftHalf = target.center.x <= rootSize.x * 0.5f;
            return onLeftHalf ? RightPlacementOrder : LeftPlacementOrder;
        }

        private static MxFairyGuiTooltipPlacement[] BuildExplicitOrder(
            MxFairyGuiTooltipPlacement preferred)
        {
            switch (preferred)
            {
                case MxFairyGuiTooltipPlacement.Above:
                    return AbovePlacementOrder;
                case MxFairyGuiTooltipPlacement.Right:
                    return RightPlacementOrder;
                case MxFairyGuiTooltipPlacement.Left:
                    return LeftPlacementOrder;
                default:
                    return WidePlacementOrder;
            }
        }

        private static Vector2 CalculateCandidate(
            MxFairyGuiTooltipPlacement placement,
            Rect target,
            Vector2 pointer,
            Vector2 tooltipSize,
            float gap)
        {
            switch (placement)
            {
                case MxFairyGuiTooltipPlacement.Above:
                    return new Vector2(
                        target.center.x - tooltipSize.x * 0.5f,
                        target.yMin - tooltipSize.y - gap);
                case MxFairyGuiTooltipPlacement.Right:
                    return new Vector2(
                        target.xMax + gap,
                        target.center.y - tooltipSize.y * 0.5f);
                case MxFairyGuiTooltipPlacement.Left:
                    return new Vector2(
                        target.xMin - tooltipSize.x - gap,
                        target.center.y - tooltipSize.y * 0.5f);
                case MxFairyGuiTooltipPlacement.Cursor:
                    return pointer + new Vector2(gap, gap * 2f);
                default:
                    return new Vector2(
                        target.center.x - tooltipSize.x * 0.5f,
                        target.yMax + gap);
            }
        }

        private static bool Fits(Vector2 position, Vector2 size, Rect safe)
        {
            return position.x >= safe.xMin
                && position.y >= safe.yMin
                && position.x + size.x <= safe.xMax
                && position.y + size.y <= safe.yMax;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(MxFairyGuiTooltipService));
        }

        private readonly struct Binding
        {
            public Binding(string text, MxFairyGuiTooltipOptions options)
            {
                Text = text;
                Options = options;
            }

            public string Text { get; }

            public MxFairyGuiTooltipOptions Options { get; }
        }
    }
}
