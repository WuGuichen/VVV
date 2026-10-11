using System;

namespace MxFramework.CharacterCloth
{
    /// <summary>
    /// Describes the local state requested for one character cloth controller.
    /// </summary>
    public enum CharacterClothState
    {
        /// <summary>Simulation and presentation output are active.</summary>
        Running = 0,

        /// <summary>The current simulation pose is retained without advancing time.</summary>
        Paused = 1,

        /// <summary>Simulation is disabled and the presentation returns to its authored pose.</summary>
        DisabledReset = 2,

        /// <summary>Simulation is disabled while retaining its current pose for a later resume.</summary>
        DisabledKeep = 3,
    }

    /// <summary>
    /// Describes a system-level override applied without losing each controller's local request.
    /// </summary>
    public enum CharacterClothSystemOverride
    {
        /// <summary>Use each controller's local requested state.</summary>
        None = 0,

        /// <summary>Pause locally running controllers while preserving locally disabled states.</summary>
        Paused = 1,

        /// <summary>Disable every registered controller and reset its presentation pose.</summary>
        DisabledReset = 2,

        /// <summary>Disable every registered controller while retaining its current pose.</summary>
        DisabledKeep = 3,
    }

    /// <summary>
    /// Resolves local cloth requests against a system-level presentation override.
    /// </summary>
    public static class CharacterClothStateResolver
    {
        /// <summary>
        /// Resolves the effective state without changing the local request.
        /// </summary>
        /// <remarks>NoAlloc on the normal path.</remarks>
        /// <exception cref="ArgumentOutOfRangeException">The request or override is not a defined value.</exception>
        public static CharacterClothState Resolve(
            CharacterClothState requestedState,
            CharacterClothSystemOverride systemOverride)
        {
            ValidateState(requestedState);

            switch (systemOverride)
            {
                case CharacterClothSystemOverride.None:
                    return requestedState;
                case CharacterClothSystemOverride.Paused:
                    return requestedState == CharacterClothState.Running
                        ? CharacterClothState.Paused
                        : requestedState;
                case CharacterClothSystemOverride.DisabledReset:
                    return CharacterClothState.DisabledReset;
                case CharacterClothSystemOverride.DisabledKeep:
                    return CharacterClothState.DisabledKeep;
                default:
                    throw new ArgumentOutOfRangeException(nameof(systemOverride), systemOverride, "Unknown cloth system override.");
            }
        }

        /// <summary>
        /// Returns whether the state disables the backing simulation.
        /// </summary>
        /// <remarks>NoAlloc.</remarks>
        public static bool IsDisabled(CharacterClothState state)
        {
            ValidateState(state);
            return state == CharacterClothState.DisabledReset
                || state == CharacterClothState.DisabledKeep;
        }

        private static void ValidateState(CharacterClothState state)
        {
            if (state < CharacterClothState.Running || state > CharacterClothState.DisabledKeep)
                throw new ArgumentOutOfRangeException(nameof(state), state, "Unknown character cloth state.");
        }
    }

    /// <summary>
    /// Controls one cloth presentation group without exposing a vendor or engine type.
    /// </summary>
    public interface ICharacterClothController
    {
        /// <summary>Gets the controller's persistent local request.</summary>
        CharacterClothState RequestedState { get; }

        /// <summary>Gets the state currently resolved from the local request and system override.</summary>
        CharacterClothState EffectiveState { get; }

        /// <summary>Gets the system override currently applied to the controller.</summary>
        CharacterClothSystemOverride SystemOverride { get; }

        /// <summary>Changes the local request and immediately reapplies the effective state.</summary>
        /// <remarks>NoAlloc after the backing adapter is initialized.</remarks>
        void SetRequestedState(CharacterClothState state);

        /// <summary>Changes the system override without replacing the local request.</summary>
        /// <remarks>NoAlloc after the backing adapter is initialized.</remarks>
        void SetSystemOverride(CharacterClothSystemOverride systemOverride);

        /// <summary>Requests a simulation reset from the backing adapter.</summary>
        /// <remarks>NoAlloc after the backing adapter is initialized.</remarks>
        void Reset(bool keepPose = false);
    }

    /// <summary>
    /// Coordinates system-level state across explicitly registered cloth controllers.
    /// </summary>
    public interface ICharacterClothService
    {
        /// <summary>Gets the current system-level override.</summary>
        CharacterClothSystemOverride SystemOverride { get; }

        /// <summary>Gets the number of registered controllers.</summary>
        int ControllerCount { get; }

        /// <summary>Registers a controller and applies the current override.</summary>
        /// <remarks>LowFreqAlloc when the internal capacity grows.</remarks>
        bool Register(ICharacterClothController controller);

        /// <summary>Removes a controller without changing the controller's last applied override.</summary>
        /// <remarks>NoAlloc.</remarks>
        bool Unregister(ICharacterClothController controller);

        /// <summary>Applies one system override to all registered controllers.</summary>
        /// <remarks>NoAlloc after registration capacity is established.</remarks>
        void SetSystemOverride(CharacterClothSystemOverride systemOverride);

        /// <summary>Requests a reset from every registered controller.</summary>
        /// <remarks>NoAlloc after registration capacity is established.</remarks>
        void ResetAll(bool keepPose = false);

        /// <summary>Forgets all registrations without mutating the controllers.</summary>
        /// <remarks>NoAlloc.</remarks>
        void Clear();
    }
}
