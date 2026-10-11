using MxFramework.Gameplay;
using UnityEngine;

namespace MxFramework.Gameplay.Triggers.Unity
{
    /// <summary>Unity callback surface for one compiled Trigger occupancy source binding.</summary>
    [DisallowMultipleComponent]
    public sealed class GameplayTriggerUnityOccupancyZone : MonoBehaviour
    {
        [SerializeField] private int _sourceBindingId = 0;
        [SerializeField] private Collider _triggerCollider = null;

        private GameplayTriggerUnityOccupancyAdapter _adapter;
        private GameplayTriggerUnityZoneHandle _handle;

        /// <remarks>NoAlloc.</remarks>
        public GameplayTriggerUnityContactResult LastContactResult { get; private set; }

        /// <summary>Attaches this authoring component to the persistent occupancy coordinator.</summary>
        /// <remarks>NoAlloc after coordinator initialization.</remarks>
        public bool TryAttach(
            GameplayTriggerUnityOccupancyAdapter adapter,
            out GameplayTriggerUnityZoneRegistrationResult result)
        {
            if (adapter == null || _sourceBindingId <= 0 || _triggerCollider == null)
            {
                result = new GameplayTriggerUnityZoneRegistrationResult(
                    GameplayTriggerUnityZoneRegistrationStatus.InvalidArgument);
                return false;
            }

            if (!adapter.TryRegisterZone(
                    new GameplayTriggerSourceBindingId(_sourceBindingId),
                    _triggerCollider,
                    gameObject.scene.handle.GetRawData(),
                    out GameplayTriggerUnityZoneHandle handle,
                    out result))
                return false;

            _adapter = adapter;
            _handle = handle;
            return true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_adapter == null || !_handle.IsValid)
                return;
            LastContactResult = _adapter.SetContact(_handle, other, active: true);
        }

        private void OnTriggerExit(Collider other)
        {
            if (_adapter == null || !_handle.IsValid)
                return;
            LastContactResult = _adapter.SetContact(_handle, other, active: false);
        }

        private void OnDisable()
        {
            Retire();
        }

        private void OnDestroy()
        {
            Retire();
        }

        private void Retire()
        {
            if (_adapter == null || !_handle.IsValid)
                return;
            _adapter.TryRetireZone(_handle, out GameplayTriggerUnityLifecycleResult _);
        }
    }

    /// <summary>Maps explicitly authored Collider references to one runtime Gameplay entity generation.</summary>
    [DisallowMultipleComponent]
    public sealed class GameplayTriggerUnitySubjectBinding : MonoBehaviour
    {
        [SerializeField] private Collider[] _colliders = System.Array.Empty<Collider>();

        private GameplayTriggerUnityOccupancyAdapter _adapter;
        private GameplayTriggerUnitySubjectHandle _handle;

        /// <summary>Binds this subject to a live generation-safe Gameplay entity.</summary>
        /// <remarks>NoAlloc after coordinator initialization.</remarks>
        public bool TryBind(
            GameplayTriggerUnityOccupancyAdapter adapter,
            GameplayEntityId entityId,
            out GameplayTriggerUnitySubjectBindingResult result)
        {
            if (adapter == null || _colliders == null)
            {
                result = new GameplayTriggerUnitySubjectBindingResult(
                    GameplayTriggerUnitySubjectBindingStatus.InvalidArgument,
                    entityId);
                return false;
            }
            if (!adapter.TryBindSubject(
                    entityId,
                    _colliders,
                    gameObject.scene.handle.GetRawData(),
                    out GameplayTriggerUnitySubjectHandle handle,
                    out result))
                return false;

            _adapter = adapter;
            _handle = handle;
            return true;
        }

        /// <summary>Rebinds active Collider contacts to a new Gameplay entity generation.</summary>
        /// <remarks>NoAlloc after coordinator initialization.</remarks>
        public bool TryRebind(
            GameplayEntityId entityId,
            out GameplayTriggerUnitySubjectBindingResult result)
        {
            if (_adapter == null || !_handle.IsValid)
            {
                result = new GameplayTriggerUnitySubjectBindingResult(
                    GameplayTriggerUnitySubjectBindingStatus.StaleHandle,
                    entityId);
                return false;
            }
            return _adapter.TryRebindSubject(_handle, entityId, out result);
        }

        /// <summary>Unbinds all Collider contacts owned by this subject component.</summary>
        /// <remarks>NoAlloc after coordinator initialization.</remarks>
        public bool TryUnbind(out GameplayTriggerUnityLifecycleResult result)
        {
            if (_adapter == null || !_handle.IsValid)
            {
                result = new GameplayTriggerUnityLifecycleResult(
                    GameplayTriggerUnityLifecycleStatus.StaleHandle);
                return false;
            }
            bool success = _adapter.TryUnbindSubject(_handle, out result);
            if (success)
                _handle = default;
            return success;
        }

        private void OnDisable()
        {
            UnbindIfNeeded();
        }

        private void OnDestroy()
        {
            UnbindIfNeeded();
        }

        private void UnbindIfNeeded()
        {
            if (_adapter == null || !_handle.IsValid)
                return;
            if (_adapter.TryUnbindSubject(
                    _handle,
                    out GameplayTriggerUnityLifecycleResult _))
                _handle = default;
        }
    }
}
