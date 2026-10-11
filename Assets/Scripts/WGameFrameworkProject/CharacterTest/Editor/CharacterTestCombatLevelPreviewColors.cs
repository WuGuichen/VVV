using MxFramework.Combat.Physics;
using UnityEngine;

namespace MxFramework.Game.CharacterTest.Editor
{
    internal static class CharacterTestCombatLevelPreviewColors
    {
        private static readonly Color DefaultColor = new Color(0.20f, 0.72f, 0.95f, 0.95f);
        private static readonly Color HardSlickColor = new Color(0.12f, 0.55f, 1.00f, 0.95f);
        private static readonly Color RoughColor = new Color(0.10f, 0.75f, 0.32f, 0.95f);
        private static readonly Color ElasticColor = new Color(0.95f, 0.10f, 0.90f, 0.95f);
        private static readonly Color BouncePadColor = new Color(1.00f, 0.52f, 0.06f, 0.95f);

        public static Color Resolve(CombatSurfaceMaterialKind kind)
        {
            switch (kind)
            {
                case CombatSurfaceMaterialKind.HardSlick:
                    return HardSlickColor;
                case CombatSurfaceMaterialKind.Rough:
                    return RoughColor;
                case CombatSurfaceMaterialKind.Elastic:
                    return ElasticColor;
                case CombatSurfaceMaterialKind.BouncePad:
                    return BouncePadColor;
                default:
                    return DefaultColor;
            }
        }
    }
}
