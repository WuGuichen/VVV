using System;
using UnityEngine;

namespace MxFramework.Rendering
{
    /// <summary>Appearance of sparse airborne dust. Distances are world metres; color is authored in sRGB.</summary>
    [Serializable]
    public sealed class MxSceneDustSettings
    {
        [Range(0, 8192)] public int ParticleCount = 2400;
        [Min(1)] public float Radius = 18;
        public float BaseHeight;
        [Min(0.1f)] public float Height = 5;
        [Min(0.001f)] public float Size = 0.025f;
        [Range(0, 1)] public float Opacity = 0.24f;
        [Min(0)] public float DriftSpeed = 0.08f;
        [Range(-0.85f, 0.85f)] public float Anisotropy = 0.4f;
        [Min(0)] public float LightResponse = 2;
        public Color Color = new Color(0.82f, 0.76f, 0.65f, 1);
        public int Seed = 37;
    }
}
