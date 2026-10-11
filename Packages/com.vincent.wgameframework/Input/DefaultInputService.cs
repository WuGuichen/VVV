using UnityEngine.InputSystem;

namespace MxFramework.Input
{
    public sealed class DefaultInputService : InputService
    {
        protected override InputActionAsset CreateRuntimeActions(out bool ownsActions)
        {
            ownsActions = true;
            return DefaultInputActionAssetFactory.Create();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            PushContext(InputContext.Debug, InputContextPolicy.Overlay);
        }
    }
}
