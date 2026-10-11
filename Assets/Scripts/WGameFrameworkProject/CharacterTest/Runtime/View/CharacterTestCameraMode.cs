using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    public enum CharacterTestCameraMode
    {
        [InspectorName("第三人称手动控制")]
        ThirdPersonControlled,

        [InspectorName("多目标自动取景")]
        MultiTargetAuto
    }
}
