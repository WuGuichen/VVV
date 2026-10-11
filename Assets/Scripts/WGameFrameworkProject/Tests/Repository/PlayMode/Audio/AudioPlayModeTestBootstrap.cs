using NUnit.Framework;
using UnityEngine;

namespace MxFramework.Tests.PlayMode.Audio
{
    /// <summary>Keeps listener ownership stable across Test Runner fixture gaps.</summary>
    [SetUpFixture]
    internal sealed class AudioPlayModeTestBootstrap
    {
        [OneTimeSetUp]
        public void InstallListener()
        {
            if (global::FMODUnity.StudioListener.ListenerCount == 0)
            {
                var listener = new GameObject("MxFramework Audio PlayMode Test Listener");
                listener.tag = "MainCamera";
                listener.AddComponent<Camera>();
                listener.AddComponent<AudioListener>();
                listener.AddComponent<global::FMODUnity.StudioListener>();
                Object.DontDestroyOnLoad(listener);
            }

            // Test fixtures intentionally create and dispose independent audio services. Keep one
            // Test Runner-owned reference so RuntimeManager can continue updating bus:/ between them.
            global::FMODUnity.RuntimeManager.LoadBank("Master.strings", false);
            global::FMODUnity.RuntimeManager.LoadBank("Master", false);
        }
    }
}
