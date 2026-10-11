using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>Instance lifetime/structural invalidation only; never polls or owns a gameplay tick.</summary>
    [DisallowMultipleComponent]
    internal sealed class CharacterTestPresentationLodLifetime : MonoBehaviour
    {
        private CharacterTestPresentationLod _owner;
        private PresentationLodHandle _handle;
        private int _entityId;
        internal void Bind(CharacterTestPresentationLod owner, PresentationLodHandle handle, int entityId)
        { _owner = owner; _handle = handle; _entityId = entityId; }
        internal void Detach() => _owner = null;
        private void OnTransformChildrenChanged() => _owner?.InvalidateGeometry(_handle, _entityId);
        private void OnDestroy()
        {
            CharacterTestPresentationLod owner = _owner; _owner = null;
            owner?.Unregister(_handle, _entityId, destroyed: true);
        }
    }
}
