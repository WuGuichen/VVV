using UnityEditor;

namespace MxFramework.Game.CharacterTest
{
    internal sealed class CharacterTestTacticalPreparationInvalidation : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            Editor.CharacterTestWeaponTraceAutoBake.Notify(imported, deleted, moved, movedFrom);
            if (!Relevant(imported) && !Relevant(deleted) && !Relevant(moved) && !Relevant(movedFrom)) return;
            CharacterTestGeneratedPackageSource.InvalidateCache();
            CharacterTestDefaultBakedWeaponTraceSource.InvalidateCachedInstanceForMeasurement();
            CharacterTestDefaultWeaponTraceBindingResolver.InvalidateCachedInstanceForMeasurement();
            CharacterTestTacticalPreparationCatalog.Invalidate();
        }
        private static bool Relevant(string[] paths)
        {
            foreach (string path in paths)
                if (path.StartsWith("Assets/MxFrameworkGenerated/") || path.StartsWith("Assets/Config/")
                    || path.StartsWith("Assets/StreamingAssets/MxFramework/") || path.StartsWith("Assets/Art/MxFramework/")) return true;
            return false;
        }
    }
}
