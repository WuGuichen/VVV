using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace MxFramework.Rendering
{
    /// <summary>Borrowed world-light resources for this camera graph. The lighting provider owns all storage.</summary>
    public sealed class MxSceneLightFieldData : ContextItem
    {
        public BufferHandle Probes, Coefficients, Visibility, CellBricks, BrickIndices, ClearDistances;
        public TextureHandle SunVisibility;
        public Vector4 GridOrigin, GridSize, SunRange, SunDirection, SunColor;
        public Matrix4x4 SunView, SunViewProjection;
        public float IndirectStrength;
        public bool IsValid => Probes.IsValid() && Coefficients.IsValid() && Visibility.IsValid() && SunVisibility.IsValid();

        public override void Reset()
        {
            Probes = Coefficients = Visibility = BufferHandle.nullHandle;
            CellBricks = BrickIndices = ClearDistances = BufferHandle.nullHandle;
            SunVisibility = TextureHandle.nullHandle;
            GridOrigin = GridSize = SunRange = SunDirection = SunColor = Vector4.zero;
            SunView = SunViewProjection = Matrix4x4.identity;
            IndirectStrength = 0;
        }
    }
}
