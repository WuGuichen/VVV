using System;
using System.Collections.Generic;

namespace MxFramework.CharacterCloth
{
    /// <summary>
    /// Default in-memory coordinator for character cloth presentation controllers.
    /// </summary>
    public sealed class CharacterClothService : ICharacterClothService
    {
        private readonly List<ICharacterClothController> _controllers;
        private CharacterClothSystemOverride _systemOverride;

        /// <summary>
        /// Creates a service with optional preallocated registration capacity.
        /// </summary>
        /// <remarks>LowFreqAlloc during construction.</remarks>
        public CharacterClothService(int initialCapacity = 8)
        {
            if (initialCapacity < 0)
                throw new ArgumentOutOfRangeException(nameof(initialCapacity), initialCapacity, "Initial capacity cannot be negative.");

            _controllers = new List<ICharacterClothController>(initialCapacity);
        }

        /// <inheritdoc />
        public CharacterClothSystemOverride SystemOverride => _systemOverride;

        /// <inheritdoc />
        public int ControllerCount => _controllers.Count;

        /// <inheritdoc />
        public bool Register(ICharacterClothController controller)
        {
            if (controller == null)
                throw new ArgumentNullException(nameof(controller));

            for (int i = 0; i < _controllers.Count; i++)
            {
                if (ReferenceEquals(_controllers[i], controller))
                    return false;
            }

            controller.SetSystemOverride(_systemOverride);
            _controllers.Add(controller);
            return true;
        }

        /// <inheritdoc />
        public bool Unregister(ICharacterClothController controller)
        {
            if (controller == null)
                return false;

            for (int i = 0; i < _controllers.Count; i++)
            {
                if (!ReferenceEquals(_controllers[i], controller))
                    continue;

                _controllers.RemoveAt(i);
                return true;
            }

            return false;
        }

        /// <inheritdoc />
        public void SetSystemOverride(CharacterClothSystemOverride systemOverride)
        {
            CharacterClothStateResolver.Resolve(CharacterClothState.Running, systemOverride);
            if (_systemOverride == systemOverride)
                return;

            _systemOverride = systemOverride;
            for (int i = 0; i < _controllers.Count; i++)
                _controllers[i].SetSystemOverride(systemOverride);
        }

        /// <inheritdoc />
        public void ResetAll(bool keepPose = false)
        {
            for (int i = 0; i < _controllers.Count; i++)
                _controllers[i].Reset(keepPose);
        }

        /// <inheritdoc />
        public void Clear()
        {
            _controllers.Clear();
        }
    }
}
