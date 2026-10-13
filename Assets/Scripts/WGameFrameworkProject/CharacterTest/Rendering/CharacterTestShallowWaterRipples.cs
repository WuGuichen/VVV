using System;
using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>Fixed-capacity presentation wave packets. Oldest packets are replaced without allocating.</summary>
    internal sealed class CharacterTestShallowWaterRipples
    {
        internal const int Capacity=32;
        internal const int TileAxis = 16;
        internal readonly Vector4[] Sources=new Vector4[Capacity], Shapes=new Vector4[Capacity];
        internal readonly Vector4[] DrawSources = new Vector4[Capacity], DrawShapes = new Vector4[Capacity];
        internal readonly Vector4[] DrawSheen = new Vector4[Capacity / 4];
        // Four exactly representable bytes per tile; no float bit-casts or NaN payloads.
        internal readonly Vector4[] TileMasks = new Vector4[TileAxis * TileAxis];
        internal int Count { get; private set; }
        internal int Replaced { get; private set; }
        internal int Merged { get; private set; }
        internal int EmissionVersion { get; private set; }
        private readonly int[] _priorities = new int[Capacity];
        private int _limit = Capacity, _version, _builtVersion = -1;
        private float _builtTime, _builtLifetime, _builtSpeed;
        private Vector2 _builtSize;
        private Matrix4x4 _builtTransform;
        internal void Clear() { Count=0; Replaced=0; Merged=0; _version++; EmissionVersion++; }
        internal void SetLimit(int limit)
        {
            _limit = Mathf.Clamp(limit, 0, Capacity);
            while (Count > _limit) { int i = Oldest(); int last = --Count; Sources[i] = Sources[last]; Shapes[i] = Shapes[last]; _priorities[i] = _priorities[last]; _version++; }
        }
        private int Oldest()
        { int index = 0; for (int i = 1; i < Count; i++) if (_priorities[i] < _priorities[index] || (_priorities[i] == _priorities[index] && Sources[i].z < Sources[index].z)) index = i; return index; }
        internal void Add(Vector2 point,float time,float amplitude,float width=.25f,float frequency=18, int priority=1, bool merge=false)
        {
            if (!float.IsFinite(point.x)||!float.IsFinite(point.y)||!float.IsFinite(time)||!float.IsFinite(amplitude)||amplitude<=0)return;
            if (_limit == 0) return;
            priority = Mathf.Clamp(priority, 0, 2);
            if (merge)
            {
                for (int i = Count - 1; i >= 0; i--)
                {
                    Vector4 source = Sources[i];
                    if (_priorities[i] != priority || time < source.z || time - source.z > .08f
                        || (point - new Vector2(source.x, source.y)).sqrMagnitude > .0324f) continue;
                    float strength = Mathf.Max(source.w, Mathf.Min(.08f, amplitude));
                    if (strength != source.w) { source.w = strength; Sources[i] = source; _version++; EmissionVersion++; }
                    Merged++; return;
                }
            }
            int index=Count;
            if(Count<_limit)Count++;
            else { index=Oldest(); if (_priorities[index] > priority) return; Replaced++; }
            Sources[index]=new Vector4(point.x,point.y,time,Mathf.Min(.08f,amplitude));
            Shapes[index]=new Vector4(Mathf.Clamp(width,.1f,.6f),Mathf.Clamp(frequency,4,30),0,0);
            _priorities[index] = priority; _version++; EmissionVersion++;
        }
        internal void Expire(float time,float lifetime)
        {
            for(int i=Count-1;i>=0;i--)
                if(time-Sources[i].z>=lifetime || time<Sources[i].z)
                { int last=--Count; Sources[i]=Sources[last]; Shapes[i]=Shapes[last]; _priorities[i]=_priorities[last]; _version++; }
        }

        internal bool IntersectsField(Vector2 center, float extent, float time, float speed)
        {
            Vector2 half = Vector2.one * (extent * .5f);
            for (int n = 0; n < Count; n++)
            {
                var source = Sources[n]; float radius = Mathf.Max(0, time - source.z) * speed;
                float inner = Mathf.Max(0, radius - 4 * Shapes[n].x), outer = radius + 4 * Shapes[n].x;
                Vector2 delta = new Vector2(Mathf.Abs(source.x - center.x), Mathf.Abs(source.y - center.y));
                if (Vector2.Max(Vector2.zero, delta - half).sqrMagnitude <= outer * outer + .0001f
                    && (delta + half).sqrMagnitude >= inner * inner - .0001f) return true;
            }
            return false;
        }

        /// <summary>NoAlloc. Shared by all cameras; age/fade and conservative annular tile admission are evaluated once.</summary>
        internal void BuildDrawData(float time, float lifetime, float speed, Matrix4x4 worldToLocal, Vector2 size)
        {
            if (_builtVersion == _version && _builtTime == time && _builtLifetime == lifetime
                && _builtSpeed == speed && _builtSize == size && _builtTransform == worldToLocal) return;
            _builtVersion = _version; _builtTime = time; _builtLifetime = lifetime; _builtSpeed = speed;
            _builtSize = size; _builtTransform = worldToLocal;
            Array.Clear(TileMasks, 0, TileMasks.Length);
            Vector2 cell = size / TileAxis;
            for (int n = 0; n < Count; n++)
            {
                Vector4 source = Sources[n], shape = Shapes[n];
                float age = Mathf.Max(0, time - source.z), radius = age * speed;
                float fade = Mathf.Clamp01(1 - age / Mathf.Max(.001f, lifetime));
                float inner = Mathf.Max(0, radius - 4 * shape.x), outer = radius + 4 * shape.x;
                DrawSources[n] = new Vector4(source.x, source.y, radius, source.w * fade * fade);
                DrawShapes[n] = new Vector4(1 / (shape.x * shape.x), shape.y, inner * inner, outer * outer);
                Vector4 sheen = DrawSheen[n / 4]; sheen[n % 4] = Mathf.Clamp01(source.w / .02f) * fade * fade; DrawSheen[n / 4] = sheen;
                Vector3 local = worldToLocal.MultiplyPoint3x4(new Vector3(source.x, 0, source.y));
                Vector2 center = new Vector2(local.x, local.z) + size * .5f;
                int x0 = Mathf.Clamp(Mathf.FloorToInt((center.x - outer) / cell.x), 0, TileAxis - 1);
                int x1 = Mathf.Clamp(Mathf.FloorToInt((center.x + outer) / cell.x), 0, TileAxis - 1);
                int y0 = Mathf.Clamp(Mathf.FloorToInt((center.y - outer) / cell.y), 0, TileAxis - 1);
                int y1 = Mathf.Clamp(Mathf.FloorToInt((center.y + outer) / cell.y), 0, TileAxis - 1);
                for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++)
                {
                    Vector2 delta = new Vector2((x + .5f) * cell.x, (y + .5f) * cell.y) - center;
                    delta = new Vector2(Mathf.Abs(delta.x), Mathf.Abs(delta.y));
                    Vector2 nearest = Vector2.Max(Vector2.zero, delta - cell * .5f);
                    Vector2 farthest = delta + cell * .5f;
                    if (nearest.sqrMagnitude > outer * outer + .0001f || farthest.sqrMagnitude < inner * inner - .0001f) continue;
                    int tile = y * TileAxis + x, lane = n / 8;
                    Vector4 mask = TileMasks[tile]; mask[lane] = (int)mask[lane] | (1 << (n % 8)); TileMasks[tile] = mask;
                }
            }
        }
    }
}
