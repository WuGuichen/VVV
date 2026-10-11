using System;
using System.Collections.Generic;
using MxFramework.Camera.URP;
using MxFramework.Combat.Damage;
using MxFramework.Combat.Hit;
using MxFramework.Core.Math;
using MxFramework.Gameplay;
using MxFramework.Runtime;
using MxFramework.UI;
using MxFramework.UI.FairyGui;
using UnityEngine;
using Fgui = global::FairyGUI;
using UnityCamera = UnityEngine.Camera;

namespace MxFramework.Game.CharacterTest
{
    public sealed class CharacterTestFloatingCombatTextPool : IDisposable
    {
        public const int DefaultPrewarmCount = 64;
        public const int DefaultMaxActiveCount = 96;
        private const float TextWidth = 160f;
        private const float TextHeight = 34f;

        private readonly Fgui.GComponent _layerRoot;
        private readonly int _maxActiveCount;
        private readonly List<Entry> _active;
        private readonly List<Entry> _inactive;
        private bool _disposed;

        public CharacterTestFloatingCombatTextPool(
            Fgui.GComponent layerRoot,
            int prewarmCount = DefaultPrewarmCount,
            int maxActiveCount = DefaultMaxActiveCount)
        {
            _layerRoot = layerRoot ?? throw new ArgumentNullException(nameof(layerRoot));
            _maxActiveCount = maxActiveCount <= 0 ? DefaultMaxActiveCount : maxActiveCount;
            _active = new List<Entry>(_maxActiveCount);
            _inactive = new List<Entry>(Math.Max(0, prewarmCount));
            Prewarm(prewarmCount);
        }

        public int ActiveCount => _active.Count;
        public int InactiveCount => _inactive.Count;
        public int CreatedCount { get; private set; }
        public int RecycledActiveCount { get; private set; }
        public int DroppedSpawnCount { get; private set; }

        public void Prewarm(int count)
        {
            ThrowIfDisposed();
            int safeCount = Math.Max(0, Math.Min(count, _maxActiveCount));
            while (CreatedCount < safeCount)
                _inactive.Add(CreateEntry());
        }

        public bool Spawn(CharacterTestFloatingCombatTextEvent evt)
        {
            ThrowIfDisposed();
            if (!evt.IsValid)
            {
                DroppedSpawnCount++;
                return false;
            }

            Entry entry = Rent(evt.Priority);
            if (entry == null)
            {
                DroppedSpawnCount++;
                return false;
            }

            entry.Configure(evt, CalculateDrift(evt));
            _active.Add(entry);
            return true;
        }

        /// <remarks>LowFreqAlloc: presentation ticks may update labels, diagnostics, or backend views; simulation hot paths should use lower-level NoAlloc APIs.</remarks>
        public void Tick(float deltaTime, CharacterTestFloatingCombatTextProjector projector)
        {
            ThrowIfDisposed();
            float safeDeltaTime = deltaTime > 0f ? deltaTime : 1f / 60f;
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                Entry entry = _active[i];
                if (!entry.Tick(safeDeltaTime, projector))
                {
                    _active.RemoveAt(i);
                    Return(entry);
                }
            }
        }

        public void Clear()
        {
            ThrowIfDisposed();
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                Entry entry = _active[i];
                _active.RemoveAt(i);
                Return(entry);
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            for (int i = 0; i < _active.Count; i++)
                _active[i].Dispose();
            for (int i = 0; i < _inactive.Count; i++)
                _inactive[i].Dispose();
            _active.Clear();
            _inactive.Clear();
            _disposed = true;
        }

        private Entry Rent(int priority)
        {
            if (_inactive.Count > 0)
            {
                int index = _inactive.Count - 1;
                Entry entry = _inactive[index];
                _inactive.RemoveAt(index);
                return entry;
            }

            if (CreatedCount < _maxActiveCount)
                return CreateEntry();

            int recycleIndex = FindLowestPriorityActiveIndex();
            if (recycleIndex < 0 || _active[recycleIndex].Priority > priority)
                return null;

            Entry recycled = _active[recycleIndex];
            _active.RemoveAt(recycleIndex);
            RecycledActiveCount++;
            return recycled;
        }

        private Entry CreateEntry()
        {
            var text = new Fgui.GTextField
            {
                name = "CharacterTest.FloatingCombatText",
                touchable = false,
                autoSize = Fgui.AutoSizeType.None,
                singleLine = true,
                align = Fgui.AlignType.Center,
                verticalAlign = Fgui.VertAlignType.Middle,
                visible = false,
                alpha = 0f
            };
            text.SetSize(TextWidth, TextHeight);
            _layerRoot.AddChild(text);
            CreatedCount++;
            return new Entry(text);
        }

        private int FindLowestPriorityActiveIndex()
        {
            int index = -1;
            int priority = int.MaxValue;
            for (int i = 0; i < _active.Count; i++)
            {
                if (_active[i].Priority < priority)
                {
                    priority = _active[i].Priority;
                    index = i;
                }
            }

            return index;
        }

        private void Return(Entry entry)
        {
            entry.Reset();
            _inactive.Add(entry);
        }

        private static float CalculateDrift(CharacterTestFloatingCombatTextEvent evt)
        {
            unchecked
            {
                int hash = evt.TargetEntityId;
                hash = (hash * 397) ^ evt.SourceEntityId;
                hash = (hash * 397) ^ evt.Frame.Value.GetHashCode();
                hash = (hash * 397) ^ (int)evt.Kind;
                hash = (hash * 397) ^ (evt.Text == null ? 0 : StringComparer.Ordinal.GetHashCode(evt.Text));
                float normalized = (Math.Abs(hash % 2001) / 1000f) - 1f;
                return normalized * evt.Style.DriftPixels;
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(CharacterTestFloatingCombatTextPool));
        }

        private sealed class Entry : IDisposable
        {
            private readonly Fgui.GTextField _text;
            private CharacterTestFloatingCombatTextEvent _event;
            private float _elapsedSeconds;
            private float _driftPixels;

            public Entry(Fgui.GTextField text)
            {
                _text = text;
            }

            public int Priority => _event.Priority;

            public void Configure(CharacterTestFloatingCombatTextEvent evt, float driftPixels)
            {
                if (!HasLiveDisplayObject())
                {
                    _text.RemoveFromParent();
                    return;
                }

                _event = evt;
                _elapsedSeconds = 0f;
                _driftPixels = driftPixels;
                Fgui.TextFormat format = _text.textFormat;
                format.size = evt.Style.FontSize;
                format.color = evt.Style.Color;
                _text.textFormat = format;
                _text.color = evt.Style.Color;
                _text.text = evt.Text;
                _text.alpha = 1f;
                _text.visible = true;
                SetScale(evt.Style.StartScale);
            }

            /// <remarks>LowFreqAlloc: presentation ticks may update labels, diagnostics, or backend views; simulation hot paths should use lower-level NoAlloc APIs.</remarks>
            public bool Tick(float deltaTime, CharacterTestFloatingCombatTextProjector projector)
            {
                if (!HasLiveDisplayObject())
                {
                    _text.RemoveFromParent();
                    return false;
                }

                _elapsedSeconds += deltaTime;
                float lifetime = _event.Style.LifetimeSeconds;
                if (_elapsedSeconds >= lifetime)
                    return false;

                float t = Mathf.Clamp01(_elapsedSeconds / lifetime);
                if (projector != null && projector.TryProject(_event.WorldPosition, out Vector2 stagePosition))
                {
                    float eased = EaseOutCubic(t);
                    float x = stagePosition.x - TextWidth * 0.5f + _driftPixels * eased;
                    float y = stagePosition.y - TextHeight - _event.Style.RisePixels * eased;
                    _text.SetXY(Mathf.Round(x), Mathf.Round(y));
                    _text.visible = true;
                }
                else
                {
                    _text.visible = false;
                }

                float fadeT = t <= 0.62f ? 0f : Mathf.Clamp01((t - 0.62f) / 0.38f);
                _text.alpha = 1f - fadeT * fadeT;
                SetScale(Mathf.Lerp(_event.Style.StartScale, _event.Style.EndScale, t));
                return true;
            }

            public void Reset()
            {
                _event = default;
                _elapsedSeconds = 0f;
                _driftPixels = 0f;
                if (!HasLiveDisplayObject())
                {
                    _text.RemoveFromParent();
                    return;
                }

                _text.visible = false;
                _text.alpha = 0f;
                _text.text = string.Empty;
                SetScale(1f);
            }

            public void Dispose()
            {
                if (!HasLiveDisplayObject())
                {
                    _text.RemoveFromParent();
                    return;
                }

                _text.RemoveFromParent();
                _text.Dispose();
            }

            private void SetScale(float scale)
            {
                _text.scaleX = scale;
                _text.scaleY = scale;
            }

            private static float EaseOutCubic(float t)
            {
                float inv = 1f - Mathf.Clamp01(t);
                return 1f - inv * inv * inv;
            }

            private bool HasLiveDisplayObject()
            {
                Fgui.DisplayObject displayObject = _text.displayObject;
                return displayObject != null && !displayObject.isDisposed && displayObject.gameObject != null;
            }
        }
    }

}
