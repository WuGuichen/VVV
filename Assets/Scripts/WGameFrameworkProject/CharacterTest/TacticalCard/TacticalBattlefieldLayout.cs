using UnityEngine;

namespace MxFramework.Game.CharacterTest.TacticalCard
{
    internal static class TacticalBattlefieldLayout
    {
        internal const float Width = 80f;
        internal const float Depth = 60f;
        internal const float HalfWidth = Width * 0.5f;
        internal const float HalfDepth = Depth * 0.5f;
        internal const float PlacementMargin = 1f;

        internal static bool ContainsPlacement(Vector3 position)
        {
            return float.IsFinite(position.x) && float.IsFinite(position.z)
                && Mathf.Abs(position.x) <= HalfWidth - PlacementMargin
                && Mathf.Abs(position.z) <= HalfDepth - PlacementMargin;
        }
    }
}
