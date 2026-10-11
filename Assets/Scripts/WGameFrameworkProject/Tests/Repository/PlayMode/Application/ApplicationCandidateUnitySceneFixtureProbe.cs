using UnityEngine;

namespace MxFramework.Tests.PlayMode.Application
{
    public sealed class ApplicationCandidateUnitySceneFixtureProbe : MonoBehaviour
    {
        public static int AwakeCount { get; private set; }
        public static int OnEnableCount { get; private set; }

        public static void ResetCounters()
        {
            AwakeCount = 0;
            OnEnableCount = 0;
        }

        private void Awake() => AwakeCount++;
        private void OnEnable() => OnEnableCount++;
    }
}
