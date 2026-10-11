using System;
using System.Collections.Generic;
using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>Selects vegetation presentation only; instance authority is unchanged.</summary>
    public enum CharacterTestVegetationClassificationMode
    {
        Auto = 0,
        CpuCoarse = 1,
        CpuInstanceReference = 2,
        GpuInstances = 3,
    }

    [Serializable]
    public struct CharacterTestVegetationInstance
    {
        public int Id;
        public int TypeId;
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 Scale;
        public int Seed;
    }

    [Serializable]
    public sealed class CharacterTestVegetationType
    {
        public int Id = 1;
        public string Name = "基础草";
        public string ResourceKey = "wgame|environment.grass.basic_clump|GameObject|";
        public GameObject Prefab;
        // Explicit mesh references are serialized dependencies, including in Player builds.
        // Missing or repeated tier meshes conservatively use the original prefab mesh.
        public Mesh Lod1Mesh;
        public Mesh Lod2Mesh;
        [Range(0.001f, 1f)] public float Lod0ScreenHeight = 0.08f;
        [Range(0.001f, 1f)] public float Lod1ScreenHeight = 0.025f;
        [Range(0f, 0.49f)] public float LodHysteresis = PresentationGeometryLodPolicy.DefaultHysteresis;
        public float DrawDistance = 100f;
        // Instanced grass uses no scene Renderer. Its fixed shadow is an explicit
        // independent input; prefab/RenderParams Off only disables URP shadow draws.
        public bool CastFixedDirectionalShadow = true;
    }

}
