using System;
using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>Scene-owned authoring values edited and saved by the Level Editor scene-rendering module.</summary>
    [Serializable]
    public sealed class CharacterTestShallowWaterConfiguration
    {
        public bool AuthoringCreated;
        public CharacterTestShallowWaterSettings Settings;
        [NonSerialized] internal CharacterTestShallowWaterSettings RuntimeSettingsOverride;
        public Vector3 Position = new Vector3(-1.3f, .04f, -1.01f);
        public float Yaw;
        public bool SurfaceEnabled = true;
        public bool ReflectionEnabled = true;
        public bool RipplesEnabled = true;
        public bool SplashesEnabled = true;
    }
}
