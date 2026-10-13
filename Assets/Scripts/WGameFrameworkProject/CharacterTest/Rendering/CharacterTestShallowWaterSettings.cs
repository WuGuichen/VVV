using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    public enum CharacterTestWaterQuality { Custom = 0, Performance = 1, Balanced = 2, Quality = 3, SsprHigh = 4 }
    public enum CharacterTestWaterReflection { PlanarCamera = 0, ScreenSpacePlanar = 1, Environment = 2 }
    public enum CharacterTestRippleQuality { Custom = 0, Disabled = 1, Low = 2, Medium = 3, High = 4 }

    /// <summary>Local presentation settings; never enters gameplay, physics, replay or save authority.</summary>
    [CreateAssetMenu(menuName = "WGame/Environment/Shallow Water Settings")]
    public sealed class CharacterTestShallowWaterSettings : ScriptableObject
    {
        // Zero preserves existing authored settings when loading assets without this field.
        public CharacterTestWaterQuality QualityTier = CharacterTestWaterQuality.Custom;
        public bool RefractionEnabled = true;
        [Range(0, 60)] public int ReflectionFramesPerSecond;
        [Range(0, 32)] public int MaximumRipples = 32;
        public bool ReflectionShadows = true;
        public ComputeShader SsprCompute;
        public ComputeShader RippleCompute;
        public CharacterTestRippleQuality RippleQuality = CharacterTestRippleQuality.Low;
        [Range(128, 1024)] public int RippleFieldResolution = 512;
        [Range(8, 64)] public float RippleFieldExtent = 32;
        [Range(15, 60)] public int RippleFieldRate = 30;
        internal int EffectiveRippleResolution => RippleQuality == CharacterTestRippleQuality.Low ? 256
            : RippleQuality == CharacterTestRippleQuality.Medium ? 512 : RippleQuality == CharacterTestRippleQuality.High ? 1024
            : Mathf.Clamp(Mathf.ClosestPowerOfTwo(RippleFieldResolution), 128, 1024);
        internal float EffectiveRippleExtent => RippleQuality == CharacterTestRippleQuality.Low ? 24
            : RippleQuality == CharacterTestRippleQuality.Medium ? 32 : RippleQuality == CharacterTestRippleQuality.High ? 48 : Mathf.Clamp(RippleFieldExtent, 8, 64);
        internal int EffectiveRippleRate => RippleQuality == CharacterTestRippleQuality.High ? 60
            : RippleQuality == CharacterTestRippleQuality.Custom ? Mathf.Clamp(RippleFieldRate, 15, 60) : 30;
        public CharacterTestWaterReflection CustomReflection = CharacterTestWaterReflection.PlanarCamera;
        [Range(0, 2)] public int SsprHoleRadius = 1;
        internal CharacterTestWaterReflection ReflectionMode => QualityTier == CharacterTestWaterQuality.Custom ? CustomReflection
            : QualityTier == CharacterTestWaterQuality.Quality ? CharacterTestWaterReflection.PlanarCamera : CharacterTestWaterReflection.ScreenSpacePlanar;
        internal bool UsePlanarReflection => ReflectionMode == CharacterTestWaterReflection.PlanarCamera;
        internal bool UseSspr => ReflectionMode == CharacterTestWaterReflection.ScreenSpacePlanar;
        internal float EffectiveSsprScale => QualityTier == CharacterTestWaterQuality.Performance ? .25f
            : QualityTier == CharacterTestWaterQuality.Balanced ? .5f : QualityTier == CharacterTestWaterQuality.SsprHigh ? .75f : Mathf.Clamp(ReflectionScale, .1f, 1);
        internal int EffectiveSsprEdge => QualityTier == CharacterTestWaterQuality.Performance ? 256
            : QualityTier == CharacterTestWaterQuality.Balanced ? 512 : QualityTier == CharacterTestWaterQuality.SsprHigh ? 1024 : Mathf.Clamp(ReflectionMaximumEdge, 128, 1024);
        internal float EffectiveSsprFilterStrength => QualityTier == CharacterTestWaterQuality.Performance ? 1
            : QualityTier == CharacterTestWaterQuality.SsprHigh ? .3f : .55f;
        internal int EffectiveSsprHoleRadius => QualityTier == CharacterTestWaterQuality.Balanced || QualityTier == CharacterTestWaterQuality.SsprHigh ? 2
            : QualityTier == CharacterTestWaterQuality.Performance ? 1 : Mathf.Clamp(SsprHoleRadius, 0, 2);
        internal bool UseRefraction => QualityTier == CharacterTestWaterQuality.Quality
            || (QualityTier == CharacterTestWaterQuality.Custom && RefractionEnabled);
        internal float EffectiveReflectionScale => QualityTier == CharacterTestWaterQuality.Balanced ? .25f
            : QualityTier == CharacterTestWaterQuality.Quality ? .5f : Mathf.Clamp(ReflectionScale, .1f, 1);
        internal int EffectiveReflectionEdge => QualityTier == CharacterTestWaterQuality.Balanced ? 512
            : QualityTier == CharacterTestWaterQuality.Quality ? 1024 : Mathf.Clamp(ReflectionMaximumEdge, 128, 2048);
        internal float EffectiveReflectionDistance => QualityTier == CharacterTestWaterQuality.Balanced ? Mathf.Min(40, ReflectionDistance)
            : Mathf.Max(5, ReflectionDistance);
        internal int EffectiveReflectionRate => QualityTier == CharacterTestWaterQuality.Custom ? Mathf.Clamp(ReflectionFramesPerSecond, 0, 60) : 0;
        internal int EffectiveRippleLimit => RippleQuality == CharacterTestRippleQuality.Disabled ? 0 : Mathf.Clamp(MaximumRipples, 0, 32);
        internal bool UseReflectionShadows => QualityTier == CharacterTestWaterQuality.Quality
            || (QualityTier == CharacterTestWaterQuality.Custom && ReflectionShadows);
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
        public GameObject SplashPrefab;
        [Range(.2f, 1.5f)] public float SplashScale = 1;
        [Range(1, 2)] public float SplashLandingMultiplier = 1.55f;
        [Range(.001f, .03f)] public float SplashMinimumDepth = .006f;
        [Range(.1f, 2)] public float SplashMinimumSpeed = .25f;
        [Range(.08f, 1)] public float SplashCooldown = .18f;
        [Range(5, 60)] public float SplashDistance = 30;
        [Range(1, 16)] public int SplashMaximumActive = 12;
        [Range(1, 32)] public int SplashBurstsPerSecond = 16;
        [Range(4, 16)] public int SplashStepParticles = 10;
        private void OnValidate()
        {
            Size.x = Mathf.Max(.5f, Size.x); Size.y = Mathf.Max(.5f, Size.y);
            ReflectionDistance = Mathf.Max(5, ReflectionDistance);
            ReflectionMaximumEdge = Mathf.Clamp(ReflectionMaximumEdge, 128, 2048);
        }
    }
}
