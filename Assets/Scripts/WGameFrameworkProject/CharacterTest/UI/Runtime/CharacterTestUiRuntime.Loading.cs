using MxFramework.Runtime;
using MxFramework.UI;

namespace MxFramework.Game.CharacterTest
{
    public sealed partial class CharacterTestUiRuntime
    {
        private CharacterTestFairyGuiLoadingHost _fairyGuiLoadingHost;
        public CharacterTestFairyGuiLoadingHost FairyGuiLoadingHost => _fairyGuiLoadingHost;
        public bool HasTacticalLoading => _fairyGuiLoadingHost?.BlocksInput == true;

        private void ConfigureTacticalLoading(GameSlice slice, IMxUiTextProvider text)
        {
            _fairyGuiLoadingHost = _owner.GetComponent<CharacterTestFairyGuiLoadingHost>();
            if (_fairyGuiLoadingHost == null)
                _fairyGuiLoadingHost = _owner.gameObject.AddComponent<CharacterTestFairyGuiLoadingHost>();
            _fairyGuiLoadingHost.Configure(slice, _fairyGuiRuntimeShell, text, _logger);
        }

        private void ReleaseTacticalLoading()
        {
            _fairyGuiLoadingHost?.Release();
            _fairyGuiLoadingHost = null;
        }
    }
}
