using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>Legacy serialized input retained only for explicit migration into the Level Editor.</summary>
    [DisallowMultipleComponent, AddComponentMenu("")]
    public sealed class CharacterTestShallowWaterSample : MonoBehaviour
    {
        public GameManager Owner;
        public CharacterTestShallowWaterSettings Settings;
        public bool SurfaceEnabled = true;
        public bool ReflectionEnabled = true;
        public bool RipplesEnabled = true;

        internal static Matrix4x4 ReflectionMatrix(float height) => CharacterTestShallowWaterSession.ReflectionMatrix(height);
        internal static Bounds SurfaceBounds(Vector3 position, Quaternion rotation, Vector2 size)
            => CharacterTestShallowWaterSession.SurfaceBounds(position, rotation, size);
    }
}
