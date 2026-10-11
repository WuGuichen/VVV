using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    internal enum CharacterTestGizmoDisplayOption
    {
        RuntimePhysicsWorld = 0,
        HardRoot = 1,
        BodyPresence = 2,
        AnimatedHurtbox = 3,
        ActiveTrace = 4,
        BodyPartContacts = 5
    }

    internal readonly struct CharacterTestGizmoDisplaySettings
    {
        internal CharacterTestGizmoDisplaySettings(
            bool runtimePhysicsWorld,
            bool hardRoot,
            bool bodyPresence,
            bool animatedHurtbox,
            bool activeTrace,
            bool bodyPartContacts)
        {
            RuntimePhysicsWorld = runtimePhysicsWorld;
            HardRoot = hardRoot;
            BodyPresence = bodyPresence;
            AnimatedHurtbox = animatedHurtbox;
            ActiveTrace = activeTrace;
            BodyPartContacts = bodyPartContacts;
        }

        internal static CharacterTestGizmoDisplaySettings Defaults =>
            new CharacterTestGizmoDisplaySettings(true, true, true, true, true, true);

        internal bool RuntimePhysicsWorld { get; }
        internal bool HardRoot { get; }
        internal bool BodyPresence { get; }
        internal bool AnimatedHurtbox { get; }
        internal bool ActiveTrace { get; }
        internal bool BodyPartContacts { get; }

        internal CharacterTestLargeCharacterPresenceDebugLayer LargeCharacterLayers
        {
            get
            {
                CharacterTestLargeCharacterPresenceDebugLayer layers =
                    CharacterTestLargeCharacterPresenceDebugLayer.None;
                if (HardRoot)
                    layers |= CharacterTestLargeCharacterPresenceDebugLayer.HardRoot;
                if (BodyPresence)
                    layers |= CharacterTestLargeCharacterPresenceDebugLayer.Presence;
                if (AnimatedHurtbox)
                    layers |= CharacterTestLargeCharacterPresenceDebugLayer.Hurtbox;
                if (ActiveTrace)
                    layers |= CharacterTestLargeCharacterPresenceDebugLayer.ActiveTrace;
                return layers;
            }
        }

        internal bool Get(CharacterTestGizmoDisplayOption option)
        {
            switch (option)
            {
                case CharacterTestGizmoDisplayOption.RuntimePhysicsWorld: return RuntimePhysicsWorld;
                case CharacterTestGizmoDisplayOption.HardRoot: return HardRoot;
                case CharacterTestGizmoDisplayOption.BodyPresence: return BodyPresence;
                case CharacterTestGizmoDisplayOption.AnimatedHurtbox: return AnimatedHurtbox;
                case CharacterTestGizmoDisplayOption.ActiveTrace: return ActiveTrace;
                case CharacterTestGizmoDisplayOption.BodyPartContacts: return BodyPartContacts;
                default: return false;
            }
        }

        internal CharacterTestGizmoDisplaySettings With(
            CharacterTestGizmoDisplayOption option,
            bool enabled)
        {
            return new CharacterTestGizmoDisplaySettings(
                option == CharacterTestGizmoDisplayOption.RuntimePhysicsWorld ? enabled : RuntimePhysicsWorld,
                option == CharacterTestGizmoDisplayOption.HardRoot ? enabled : HardRoot,
                option == CharacterTestGizmoDisplayOption.BodyPresence ? enabled : BodyPresence,
                option == CharacterTestGizmoDisplayOption.AnimatedHurtbox ? enabled : AnimatedHurtbox,
                option == CharacterTestGizmoDisplayOption.ActiveTrace ? enabled : ActiveTrace,
                option == CharacterTestGizmoDisplayOption.BodyPartContacts ? enabled : BodyPartContacts);
        }
    }

    internal interface ICharacterTestGizmoPreferenceStore
    {
        int GetInt(string key, int fallback);
        void SetInt(string key, int value);
        void Save();
    }

    internal sealed class CharacterTestPlayerPrefsGizmoPreferenceStore :
        ICharacterTestGizmoPreferenceStore
    {
        public int GetInt(string key, int fallback) => PlayerPrefs.GetInt(key, fallback);
        public void SetInt(string key, int value) => PlayerPrefs.SetInt(key, value);
        public void Save() => PlayerPrefs.Save();
    }

    internal sealed class CharacterTestGizmoPreferences
    {
        private const string KeyPrefix = "MxFramework.CharacterTest.Gizmos.";
        private readonly ICharacterTestGizmoPreferenceStore _store;

        internal CharacterTestGizmoPreferences(ICharacterTestGizmoPreferenceStore store = null)
        {
            _store = store ?? new CharacterTestPlayerPrefsGizmoPreferenceStore();
        }

        internal CharacterTestGizmoDisplaySettings Load()
        {
            CharacterTestGizmoDisplaySettings defaults = CharacterTestGizmoDisplaySettings.Defaults;
            return new CharacterTestGizmoDisplaySettings(
                Read(CharacterTestGizmoDisplayOption.RuntimePhysicsWorld, defaults.RuntimePhysicsWorld),
                Read(CharacterTestGizmoDisplayOption.HardRoot, defaults.HardRoot),
                Read(CharacterTestGizmoDisplayOption.BodyPresence, defaults.BodyPresence),
                Read(CharacterTestGizmoDisplayOption.AnimatedHurtbox, defaults.AnimatedHurtbox),
                Read(CharacterTestGizmoDisplayOption.ActiveTrace, defaults.ActiveTrace),
                Read(CharacterTestGizmoDisplayOption.BodyPartContacts, defaults.BodyPartContacts));
        }

        internal void Save(CharacterTestGizmoDisplaySettings settings)
        {
            foreach (CharacterTestGizmoDisplayOption option in
                System.Enum.GetValues(typeof(CharacterTestGizmoDisplayOption)))
            {
                _store.SetInt(Key(option), settings.Get(option) ? 1 : 0);
            }
            _store.Save();
        }

        private bool Read(CharacterTestGizmoDisplayOption option, bool fallback) =>
            _store.GetInt(Key(option), fallback ? 1 : 0) != 0;

        private static string Key(CharacterTestGizmoDisplayOption option) => KeyPrefix + option;
    }

    internal static class CharacterTestGizmoRuntimeDisplay
    {
        internal static CharacterTestGizmoDisplaySettings Current { get; private set; } =
            CharacterTestGizmoDisplaySettings.Defaults;

        internal static void Apply(CharacterTestGizmoDisplaySettings settings)
        {
            Current = settings;
            CharacterTestLargeCharacterPresenceDebug.EnabledLayers = settings.LargeCharacterLayers;
        }
    }
}
