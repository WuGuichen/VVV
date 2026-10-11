using System;

namespace MxFramework.Authoring
{
    public static class CharacterGeneratedAnimationClipAssetPaths
    {
        public static bool RequiresExtraction(CharacterPackageResourceEntry entry)
        {
            return entry != null
                && string.Equals(entry.TypeId, CharacterPackageResourceTypeIds.Animation, StringComparison.OrdinalIgnoreCase)
                && string.Equals(
                    CharacterPackageResourcePipeline.GetEffectiveSourceFormat(entry),
                    CharacterPackageResourceFormatIds.Fbx,
                    StringComparison.OrdinalIgnoreCase);
        }

        public static string Resolve(string packageId, string resourceKey)
        {
            string packageSegment = CharacterPackageResourceKeyGenerator.NormalizeSegment(packageId);
            string resourceSegment = CharacterPackageResourceKeyGenerator.NormalizeSegment(resourceKey);
            return "Assets/MxFrameworkGenerated/CharacterPackages/"
                + packageSegment
                + "/runtime_assets/animation_clips/"
                + resourceSegment
                + ".anim";
        }
    }
}
