using UnityEngine;

namespace MxFramework.Input
{
    public static class InputProviderResolver
    {
        public static IInputProvider ResolveOrCreateDefault(MonoBehaviour owner, InputService explicitService = null)
        {
            if (explicitService != null)
            {
                return explicitService;
            }

            InputService existing = Object.FindFirstObjectByType<InputService>();
            if (existing != null)
            {
                return existing;
            }

            var gameObject = new GameObject("MxFramework Default Input Service");
            if (owner != null)
            {
                gameObject.transform.SetParent(owner.transform.root, false);
            }

            return gameObject.AddComponent<DefaultInputService>();
        }
    }
}
