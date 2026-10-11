using System;
using UnityEngine;
using UnityEngine.InputSystem;
using Fgui = global::FairyGUI;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>Transient pointer decoration; never participates in UI layout, input or command acceptance.</summary>
    internal sealed class CharacterTestFairyGuiClickFeedback : Fgui.IMeshFactory, IDisposable
    {
        private const int Capacity = 4;
        private const float Duration = .38f;
        private static readonly Vector2[] Quad = { new(-1, -1), new(1, -1), new(1, 1), new(-1, 1) };
        private readonly Pulse[] _pulses = new Pulse[Capacity];
        private readonly Fgui.Stage _stage;
        private readonly RippleShape _shape;
        private int _next;
        private float _now;
        private bool _disposed;

        internal CharacterTestFairyGuiClickFeedback(Fgui.Stage stage)
        {
            _stage = stage;
            _shape = new RippleShape { name = "Pointer click ripples", touchable = false, visible = false };
            var shader = UnityEngine.Resources.Load<Shader>("MxFramework/UI/PointerRipple");
            if (shader == null) throw new InvalidOperationException("Pointer ripple shader is missing.");
            _shape.graphics.SetMaterial(new Material(shader) { hideFlags = HideFlags.DontSave });
            _shape.graphics.meshFactory = this;
            _stage.AddChild(_shape);
            _stage.beforeUpdate += Tick;
        }

        // Input/presentation boundary: this feedback acknowledges a physical press, not gameplay success.
        private void Tick()
        {
            if (_disposed) return;
            _now = Time.unscaledTime;
            if (!Application.isFocused || !Cursor.visible || Cursor.lockState != CursorLockMode.None)
            {
                Clear();
                return;
            }
            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
                Emit(mouse.position.ReadValue());

            bool visible = false;
            for (int i = 0; i < Capacity; i++)
            {
                if (_pulses[i].Active && _now - _pulses[i].Started >= Duration) _pulses[i].Active = false;
                visible |= _pulses[i].Active;
            }
            _shape.visible = visible;
            if (!visible) return;
            _shape.SetSize(Screen.width, Screen.height);
            _stage.SetChildIndex(_shape, _stage.numChildren - 1);
            _shape.graphics.SetMeshDirty();
        }

        internal void Emit(Vector2 screenPosition)
        {
            if (_disposed || screenPosition.x < 0 || screenPosition.y < 0
                || screenPosition.x >= Screen.width || screenPosition.y >= Screen.height) return;
            _pulses[_next] = new Pulse
            {
                Active = true,
                Started = Time.unscaledTime,
                // Stage uses top-left screen pixels; GRoot design scale must not be applied again.
                Center = new Vector2(screenPosition.x / Screen.width, 1f - screenPosition.y / Screen.height)
            };
            _next = (_next + 1) % Capacity;
        }

        private void Clear()
        {
            for (int i = 0; i < Capacity; i++) _pulses[i].Active = false;
            _shape.visible = false;
        }

        /// <remarks>NoAlloc after the reusable FairyGUI vertex buffer has reached its peak capacity.</remarks>
        public void OnPopulateMesh(Fgui.VertexBuffer buffer)
        {
            float scale = Mathf.Clamp(Screen.height / 1080f, .75f, 2f);
            for (int i = 0; i < Capacity; i++)
            {
                if (!_pulses[i].Active) continue;
                float progress = Mathf.Clamp01((_now - _pulses[i].Started) / Duration);
                float expand = 1f - Mathf.Pow(1f - progress, 3);
                // Readable at cursor size: a broad leading band and a smaller, quieter following ring.
                float opacity = Mathf.SmoothStep(0f, 1f, progress / .07f) * Mathf.Pow(1f - progress, 1.5f);
                Vector2 center = Vector2.Scale(_pulses[i].Center, new Vector2(Screen.width, Screen.height));
                AddRing(buffer, center, Mathf.Lerp(6f, 28f, expand) * scale,
                    scale * Mathf.Lerp(4f, 3f, progress), opacity * .78f);
                float echo = Mathf.Clamp01((progress - .18f) / .82f);
                if (progress > .18f)
                    AddRing(buffer, center, Mathf.Lerp(5f, 20f, 1f - (1f - echo) * (1f - echo)) * scale,
                        scale * Mathf.Lerp(2.8f, 2f, echo), (1f - echo) * (1f - echo) * .4f
                        * Mathf.SmoothStep(0f, 1f, echo / .18f));
            }
        }

        private static void AddRing(Fgui.VertexBuffer buffer, Vector2 center, float radius, float width, float alpha)
        {
            int start = buffer.currentVertCount;
            float extent = radius + width * 5f;
            var parameters = new Vector2(radius / extent, width / extent);
            Color32 color = new Color(1f, 1f, 1f, alpha);
            for (int corner = 0; corner < Quad.Length; corner++)
            {
                Vector2 point = center + Quad[corner] * extent;
                buffer.AddVert(new Vector3(point.x, point.y, 0f), color, Quad[corner]);
                buffer.uvs2.Add(parameters);
            }
            buffer.triangles.Add(start); buffer.triangles.Add(start + 1); buffer.triangles.Add(start + 2);
            buffer.triangles.Add(start); buffer.triangles.Add(start + 2); buffer.triangles.Add(start + 3);
        }
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _stage.beforeUpdate -= Tick;
            if (!_shape.isDisposed) _shape.Dispose();
        }

        private struct Pulse
        {
            internal bool Active;
            internal float Started;
            internal Vector2 Center;
        }

        private sealed class RippleShape : Fgui.Shape
        {
            // Disabled-control cursor detection performs non-touch hit tests too.
            protected override Fgui.DisplayObject HitTest() => null;
        }
    }
}
