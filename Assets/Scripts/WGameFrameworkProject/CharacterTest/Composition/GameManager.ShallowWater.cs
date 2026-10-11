using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    public sealed partial class GameManager
    {
        [SerializeField, HideInInspector] private CharacterTestShallowWaterConfiguration _shallowWater;
        private CharacterTestShallowWaterSession _shallowWaterSession;
        public CharacterTestShallowWaterConfiguration ShallowWater => _shallowWater?.AuthoringCreated == true ? _shallowWater : null;
        internal CharacterTestShallowWaterSession ShallowWaterSession => _shallowWaterSession;
        internal CharacterTestCharacterRuntimeModule RenderingCharacterRuntime => _context?.Slice?.CharacterRuntime;

        // Scene-rendering authoring records Undo on the owner before replacing configuration.
        public void SetShallowWater(CharacterTestShallowWaterConfiguration configuration)
        {
            if (configuration != null) configuration.AuthoringCreated = true;
            _shallowWater = configuration;
            SynchronizeShallowWater();
        }
        internal CharacterTestShallowWaterSession SynchronizeShallowWater()
        {
            if (!isActiveAndEnabled || ShallowWater == null || !_shallowWater.SurfaceEnabled || _shallowWater.Settings == null)
            { ReleaseShallowWater(); return null; }
            if (_shallowWaterSession != null && !ReferenceEquals(_shallowWaterSession.Configuration, _shallowWater))
                ReleaseShallowWater();
            if (_shallowWaterSession == null)
                _shallowWaterSession = new CharacterTestShallowWaterSession(this, _shallowWater);
            return _shallowWaterSession;
        }
        internal void ReleaseShallowWater(CharacterTestShallowWaterSession expected = null)
        {
            if (expected != null && !ReferenceEquals(_shallowWaterSession, expected)) return;
            _shallowWaterSession?.Dispose(); _shallowWaterSession = null;
        }
        private void PrepareShallowWaterPresentation(CharacterTestPresentationLod lod)
        {
            var water = SynchronizeShallowWater();
            if (water == null) return;
            water.PrepareView(); water.BindPresentationLod(lod);
        }
    }
}
