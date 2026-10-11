using System;
using System.Collections.Generic;

namespace MxFramework.CharacterCloth
{
    /// <summary>A character-owned cloth group with an independent session override.</summary>
    public interface ICharacterClothGroup
    {
        /// <summary>Raised before the group releases its backing controllers.</summary>
        event Action<ICharacterClothGroup> Destroyed;
        /// <summary>Sets the session override without replacing the group's local request.</summary>
        /// <remarks>NoAlloc after initialization.</remarks>
        void SetPresentationOverride(CharacterClothSystemOverride value);
        /// <summary>Resets simulation after a presentation discontinuity.</summary>
        /// <remarks>LowFreqAlloc if a backing simulation has not finished building.</remarks>
        void ResetAll(bool keepPose = false);
    }

    /// <summary>Owns only explicitly registered gameplay groups; previews remain independent.</summary>
    public sealed class CharacterClothPresentationService
    {
        private readonly List<ICharacterClothGroup> _groups = new List<ICharacterClothGroup>(8);
        private readonly HashSet<ICharacterClothGroup> _lodPaused = new HashSet<ICharacterClothGroup>();
        private readonly Dictionary<ICharacterClothGroup, CharacterClothSystemOverride> _applied =
            new Dictionary<ICharacterClothGroup, CharacterClothSystemOverride>();
        private bool _enabled = true;
        private bool _paused;
        private bool _keepPose;
        /// <summary>Gets the number of live registered groups.</summary>
        public int GroupCount => _groups.Count;
        /// <summary>Gets the resolved session override.</summary>
        public CharacterClothSystemOverride SystemOverride => !_enabled
            ? (_keepPose ? CharacterClothSystemOverride.DisabledKeep : CharacterClothSystemOverride.DisabledReset)
            : (_paused ? CharacterClothSystemOverride.Paused : CharacterClothSystemOverride.None);

        /// <summary>Registers a spawned group and immediately applies the current session state.</summary>
        /// <remarks>LowFreqAlloc on list growth and event subscription.</remarks>
        public bool Register(ICharacterClothGroup group)
        {
            if (group == null || _groups.Contains(group)) return false;
            group.SetPresentationOverride(SystemOverride);
            _groups.Add(group);
            _applied.Add(group, SystemOverride);
            group.Destroyed += OnDestroyed;
            return true;
        }
        /// <summary>Releases a group. Destruction callers can avoid restoring running state.</summary>
        /// <remarks>NoAlloc.</remarks>
        public bool Unregister(ICharacterClothGroup group, bool restoreLocalState = true)
        {
            if (group == null || !_groups.Remove(group)) return false;
            group.Destroyed -= OnDestroyed;
            _lodPaused.Remove(group);
            _applied.Remove(group);
            if (restoreLocalState) group.SetPresentationOverride(CharacterClothSystemOverride.None);
            return true;
        }
        /// <summary>Pauses the session, preserving an independent session disable.</summary>
        /// <remarks>NoAlloc.</remarks>
        public void SetPaused(bool paused)
        {
            if (_paused == paused) return;
            _paused = paused;
            Apply();
        }
        /// <summary>Sets a group's independent LOD pause; session pause/disable remain owned by the session.</summary>
        /// <remarks>NoAlloc after the registered group first enters the LOD pause set.
        /// The existing group resolver still preserves locally disabled requests. Unknown/released groups return false.</remarks>
        public bool SetLodPaused(ICharacterClothGroup group, bool paused)
        {
            if (group == null || !_applied.ContainsKey(group)) return false;
            bool changed = paused ? _lodPaused.Add(group) : _lodPaused.Remove(group);
            if (changed) Apply(group);
            return true;
        }
        /// <summary>Enables or disables the session, preserving its current pause request.</summary>
        /// <remarks>NoAlloc.</remarks>
        public void SetEnabled(bool enabled, bool keepPose = false)
        {
            if (_enabled == enabled && _keepPose == keepPose) return;
            _enabled = enabled;
            _keepPose = keepPose;
            Apply();
        }
        /// <summary>Resets all registered gameplay groups without changing requests.</summary>
        /// <remarks>LowFreqAlloc if a backing simulation is still building.</remarks>
        public void ResetAll(bool keepPose = false)
        {
            for (int i = 0; i < _groups.Count; i++) _groups[i].ResetAll(keepPose);
        }
        /// <summary>Forgets every registration, optionally restoring the local states.</summary>
        /// <remarks>NoAlloc.</remarks>
        public void Clear(bool restoreLocalState = true)
        {
            while (_groups.Count > 0) Unregister(_groups[_groups.Count - 1], restoreLocalState);
        }
        private void OnDestroyed(ICharacterClothGroup group) => Unregister(group, false);
        private void Apply()
        {
            for (int i = 0; i < _groups.Count; i++) Apply(_groups[i]);
        }
        private void Apply(ICharacterClothGroup group)
        {
            CharacterClothSystemOverride value = SystemOverride;
            if (value == CharacterClothSystemOverride.None && _lodPaused.Contains(group))
                value = CharacterClothSystemOverride.Paused;
            if (_applied[group] == value) return;
            _applied[group] = value;
            group.SetPresentationOverride(value);
        }
    }
}
