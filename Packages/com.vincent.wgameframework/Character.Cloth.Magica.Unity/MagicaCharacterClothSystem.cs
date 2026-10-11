using System;
using System.Collections.Generic;
using MxFramework.CharacterCloth;
using UnityEngine;

namespace MxFramework.CharacterCloth.Magica.Unity
{
    /// <summary>
    /// Unity composition component that owns one framework cloth service and its Magica adapters.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("MxFramework/Character/Cloth/Magica Cloth System")]
    public sealed class MagicaCharacterClothSystem : MonoBehaviour, ICharacterClothGroup
    {
        [SerializeField] private CharacterClothSystemOverride _initialOverride;
        [SerializeField] private bool _autoCollectFromChildren = true;
        [SerializeField] private bool _includeInactive = true;
        [SerializeField] private MagicaCharacterClothController[] _controllers =
            Array.Empty<MagicaCharacterClothController>();

        private CharacterClothService _service;
        private CharacterClothSystemOverride _presentationOverride;
        private readonly List<MagicaCharacterClothController> _registeredControllers =
            new List<MagicaCharacterClothController>(8);

        /// <inheritdoc />
        public event Action<ICharacterClothGroup> Destroyed;

        /// <summary>Gets the current system-level override.</summary>
        public CharacterClothSystemOverride SystemOverride =>
            _service != null ? _service.SystemOverride : _initialOverride;

        /// <summary>Gets the number of controllers currently registered with this system.</summary>
        public int ControllerCount => _service != null ? _service.ControllerCount : 0;

        /// <summary>Gets the configured controller list, including missing entries.</summary>
        public IReadOnlyList<MagicaCharacterClothController> Controllers => _controllers;

        private void Awake()
        {
            EnsureControllerArray();
            EnsureService();
            RefreshControllers();
            SetSystemOverride(_initialOverride);
        }

        private void OnDestroy()
        {
            Destroyed?.Invoke(this);
            Destroyed = null;
            if (_service == null)
                return;

            for (int i = 0; i < _registeredControllers.Count; i++)
            {
                MagicaCharacterClothController controller = _registeredControllers[i];
                if (controller == null || !ReferenceEquals(controller.OwningSystem, this))
                    continue;

                controller.DetachSystem(this);
                _service.Unregister(controller);
            }

            _service.Clear();
            _registeredControllers.Clear();
        }

        private void OnValidate()
        {
            EnsureControllerArray();
        }

        /// <summary>
        /// Enables all locally requested cloth states, or disables every controller.
        /// </summary>
        /// <param name="enabled">True restores local requests; false applies a global disable.</param>
        /// <param name="keepPose">When disabling, true retains the current simulated pose.</param>
        /// <remarks>NoAlloc after controller discovery.</remarks>
        public void SetEnabled(bool enabled, bool keepPose = false)
        {
            SetSystemOverride(
                enabled
                    ? CharacterClothSystemOverride.None
                    : keepPose
                        ? CharacterClothSystemOverride.DisabledKeep
                        : CharacterClothSystemOverride.DisabledReset);
        }

        /// <summary>
        /// Pauses locally running cloth without enabling controllers that were locally disabled.
        /// </summary>
        /// <remarks>NoAlloc after controller discovery.</remarks>
        public void SetPaused(bool paused)
        {
            SetSystemOverride(
                paused
                    ? CharacterClothSystemOverride.Paused
                    : CharacterClothSystemOverride.None);
        }

        /// <summary>Applies an exact system-level override to every registered controller.</summary>
        /// <remarks>NoAlloc after controller discovery.</remarks>
        public void SetSystemOverride(CharacterClothSystemOverride systemOverride)
        {
            CharacterClothStateResolver.Resolve(CharacterClothState.Running, systemOverride);
            _initialOverride = systemOverride;
            EnsureService();
            ApplyOverrides();
        }

        /// <inheritdoc />
        public void SetPresentationOverride(CharacterClothSystemOverride value)
        {
            CharacterClothStateResolver.Resolve(CharacterClothState.Running, value);
            _presentationOverride = value;
            EnsureService();
            ApplyOverrides();
        }

        private void ApplyOverrides()
        {
            // Session pause never revives a locally disabled system.
            CharacterClothSystemOverride value = _presentationOverride;
            if (value == CharacterClothSystemOverride.None
                || (value == CharacterClothSystemOverride.Paused
                    && _initialOverride != CharacterClothSystemOverride.None))
                value = _initialOverride;
            _service.SetSystemOverride(value);
        }

        /// <summary>Requests a simulation reset from every registered controller.</summary>
        /// <remarks>LowFreqAlloc when a reset must wait for asynchronous build completion.</remarks>
        public void ResetAll(bool keepPose = false)
        {
            EnsureService();
            _service.ResetAll(keepPose);
        }

        /// <summary>Registers one runtime-created controller with this system.</summary>
        /// <remarks>LowFreqAlloc when the service registration capacity grows.</remarks>
        public bool Register(MagicaCharacterClothController controller)
        {
            if (controller == null)
                return false;

            EnsureService();
            bool alreadyAttached = ReferenceEquals(controller.OwningSystem, this);
            if (!controller.TryAttachSystem(this))
                return false;

            bool registered = _service.Register(controller);
            if (registered) _registeredControllers.Add(controller);
            if (!registered && !alreadyAttached)
                controller.DetachSystem(this);
            return registered;
        }

        /// <summary>Unregisters a controller and restores its local requested state.</summary>
        /// <remarks>NoAlloc.</remarks>
        public bool Unregister(MagicaCharacterClothController controller)
        {
            if (controller == null || _service == null)
                return false;
            if (!ReferenceEquals(controller.OwningSystem, this))
                return false;

            controller.SetSystemOverride(CharacterClothSystemOverride.None);
            controller.DetachSystem(this);
            _registeredControllers.Remove(controller);
            return _service.Unregister(controller);
        }

        /// <summary>
        /// Rebuilds registrations from the serialized list or from child adapters.
        /// </summary>
        /// <remarks>LowFreqAlloc through Unity component discovery.</remarks>
        public void RefreshControllers()
        {
            EnsureService();
            EnsureControllerArray();

            for (int i = 0; i < _registeredControllers.Count; i++)
            {
                MagicaCharacterClothController controller = _registeredControllers[i];
                if (controller == null || !ReferenceEquals(controller.OwningSystem, this))
                    continue;

                controller.SetSystemOverride(CharacterClothSystemOverride.None);
                controller.DetachSystem(this);
            }

            _service.Clear();
            _registeredControllers.Clear();
            if (_autoCollectFromChildren)
            {
                _controllers = GetComponentsInChildren<MagicaCharacterClothController>(_includeInactive);
            }

            for (int i = 0; i < _controllers.Length; i++)
            {
                MagicaCharacterClothController controller = _controllers[i];
                if (controller != null && IsFirstOccurrence(i))
                    Register(controller);
            }
        }

        internal void NotifyControllerDestroyed(MagicaCharacterClothController controller)
        {
            _registeredControllers.Remove(controller);
            _service?.Unregister(controller);
        }

        private bool IsFirstOccurrence(int index)
        {
            MagicaCharacterClothController candidate = _controllers[index];
            for (int i = 0; i < index; i++)
            {
                if (ReferenceEquals(_controllers[i], candidate))
                    return false;
            }

            return true;
        }

        private void EnsureService()
        {
            if (_service == null)
            {
                _service = new CharacterClothService(Math.Max(8, _controllers != null ? _controllers.Length : 0));
                ApplyOverrides();
            }
        }

        private void EnsureControllerArray()
        {
            if (_controllers == null)
                _controllers = Array.Empty<MagicaCharacterClothController>();
        }
    }
}
