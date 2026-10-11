using System;
using System.Collections.Generic;
using MxFramework.CharacterApplication;

namespace MxFramework.Authoring
{
    public static partial class CharacterResourcePackageValidator
    {
        private static void ValidatePresentationImages(CharacterAuthoringValidationReport report, CharacterResourcePackage package)
        {
            CharacterPresentationImages images = package.ApplicationConfig?.PresentationImages;
            if (images == null) return; // Old packages display the consumer's visible fallback.
            if (images.SchemaVersion != "1.0") ImageIssue(report, "schemaVersion", "Unsupported presentation image schema.");
            ValidateImageKey(report, package, images.PortraitResourceKey, "characterPortrait", "portraitResourceKey");
            ValidateImageKey(report, package, images.StandingResourceKey, "characterStanding", "standingResourceKey");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; images.Variants != null && i < images.Variants.Count; i++)
            {
                CharacterPresentationImageVariant variant = images.Variants[i];
                string path = "variants[" + i + "]";
                if (variant == null || !CharacterPackageResourceKeyGenerator.IsValidResourceKey(variant.VariantId) || !ids.Add(variant.VariantId))
                { ImageIssue(report, path, "Appearance variant requires a unique stable variantId."); continue; }
                ValidateImageKey(report, package, variant.PortraitResourceKey, "characterPortrait", path + ".portraitResourceKey");
                ValidateImageKey(report, package, variant.StandingResourceKey, "characterStanding", path + ".standingResourceKey");
            }
        }

        private static void ValidateImageKey(CharacterAuthoringValidationReport report, CharacterResourcePackage package, string key, string usage, string path)
        {
            if (string.IsNullOrWhiteSpace(key)) return;
            CharacterPackageResourceEntry found = package.ResourceCatalog?.Entries?.Find(entry => entry?.ResourceKey == key);
            if (!CharacterPackageResourceKeyGenerator.IsValidResourceKey(key) || found == null || found.TypeId != "texture" || found.Usage != usage || !string.IsNullOrEmpty(found.Variant)
                || !(found.SourceFormat == "png" || found.SourceFormat == "jpg" || found.SourceFormat == "jpeg"))
                ImageIssue(report, path, "Image key must reference an unqualified package texture with usage=" + usage + " and PNG/JPEG format; preview images are not game art.");
        }

        private static void ImageIssue(CharacterAuthoringValidationReport report, string field, string message)
        {
            Add(report, CharacterAuthoringValidationSeverity.Error, CharacterAuthoringValidationGate.ExportBlocked,
                "CHAR_PRESENTATION_IMAGE_INVALID", "config/character_application.json", "presentationImages", field, message,
                "Choose a registered image for this slot, or clear the key for visible fallback / inheritance.");
        }
    }
}
