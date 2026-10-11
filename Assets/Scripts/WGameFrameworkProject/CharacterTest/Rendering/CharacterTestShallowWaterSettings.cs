using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>Local presentation settings; never enters gameplay, physics, replay or save authority.</summary>
    [CreateAssetMenu(menuName = "WGame/Environment/Shallow Water Settings")]
    public sealed class CharacterTestShallowWaterSettings : ScriptableObject
    {
        public Shader SurfaceShader;
        public Vector2 Size = new Vector2(72, 50);
        [Range(.1f, 1)] public float ReflectionScale = .5f;
        [Range(128, 2048)] public int ReflectionMaximumEdge = 1024;
        [Min(5)] public float ReflectionDistance = 65;
        public LayerMask ReflectionLayers = ~0;
        [Range(0, 1)] public float ReflectionStrength = .48f;
        [Range(0, .75f)] public float MinimumReflection = .32f;
        [Range(0, .08f)] public float ReflectionDistortion = .03f;
        [Range(0, .15f)] public float RippleSheen = .065f;
        [Range(.1f, .6f)] public float RippleWidth = .35f;
        [Range(0, .15f)] public float BaseReflectivity = .04f;
        [Range(0, .3f)] public float TintOpacity = .025f;
        public Color Tint = new Color(.38f, .52f, .55f, 1);
        [Range(0, .05f)] public float BackgroundSlope = .006f;
        [Range(0, .04f)] public float RefractionStrength = .006f;
        [Min(.001f)] public float ContactSoftness = .08f;
        [Range(.04f, .3f)] public float MaximumWaterDepth = .15f;
        [Min(.01f)] public float EdgeSoftness = .5f;
        [Range(0, .15f)] public float HighlightStrength = .015f;
        [Range(.1f, 3)] public float RippleLifetime = 1.7f;
        [Range(.2f, 4)] public float RippleSpeed = 1.2f;
        [Range(0, .06f)] public float FootstepAmplitude = .015f;
        [Range(0, .08f)] public float LandingAmplitude = .045f;
        [Min(.1f)] public float WakeSpacing = .55f;
        private void OnValidate()
        {
            Size.x = Mathf.Max(.5f, Size.x); Size.y = Mathf.Max(.5f, Size.y);
            ReflectionDistance = Mathf.Max(5, ReflectionDistance);
            ReflectionMaximumEdge = Mathf.Clamp(ReflectionMaximumEdge, 128, 2048);
        }
    }
}
