using UnityEngine;

namespace MxFramework.Rendering
{
    [CreateAssetMenu(menuName="MxFramework/Rendering/Scene Lighting Look")]
    public sealed class MxSceneLightingLook : ScriptableObject
    {
        [Min(0)] public float IndirectStrength = 1f;
        [Min(0)] public float DirectStrength = 1f;
        [Range(0,1)] public float MicroOcclusion = 0.35f;
        [Range(0,1)] public float CapsuleOcclusion = 0.35f;
        [Range(0,1)] public float BandStrength = 0.25f;
        [Tooltip("Material-profile specular response to the main light. Zero disables this highlight lobe.")]
        [Range(0,2)] public float SpecularStrength;
        [Tooltip("Material-profile grazing response on the lit side. Uses the main light's visibility, not unlit outlines.")]
        [Range(0,2)] public float LightRimStrength;
        public Color ShadowTint = new Color(0.82f,0.89f,1f);
        public Color Sky = new Color(0.44f,0.52f,0.64f);
        public Color Equator = new Color(0.30f,0.34f,0.40f);
        public Color Ground = new Color(0.27f,0.23f,0.19f);
        [Range(0,3)] public int DebugView;
    }

    internal static class MxSceneLightingShaderIds
    {
        internal static readonly int Enabled=Shader.PropertyToID("_MxSceneLightingEnabled");
        internal static readonly int Screen=Shader.PropertyToID("_MxSceneIrradiance");
        internal static readonly int Look=Shader.PropertyToID("_MxSceneLightingLook");
        internal static readonly int ShadowTint=Shader.PropertyToID("_MxSceneShadowTint");
        internal static readonly int SurfaceHighlights=Shader.PropertyToID("_MxSceneSurfaceHighlights");
        internal static readonly int TexelSize=Shader.PropertyToID("_MxSceneIrradiance_TexelSize");
        internal static readonly int BoundsMin=Shader.PropertyToID("_MxSceneLightingBoundsMin");
        internal static readonly int BoundsMax=Shader.PropertyToID("_MxSceneLightingBoundsMax");
    }
}
