using MxFramework.Combat.Core;
using UnityEngine;

namespace MxFramework.Runtime.Unity
{
    public interface ICombatEntityPoseSource
    {
        bool TryGetPose(CombatEntityId entityId, out Vector3 position, out Quaternion rotation);
    }
}
