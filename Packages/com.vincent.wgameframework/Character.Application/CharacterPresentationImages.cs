using System;
using System.Collections.Generic;

namespace MxFramework.CharacterApplication
{
    /// <summary>LowFreqAlloc: display-only stable resource keys; independent of model preview and gameplay.</summary>
    public sealed class CharacterPresentationImages
    {
        public string SchemaVersion { get; set; } = "1.0";
        public string PortraitResourceKey { get; set; } = string.Empty;
        public string StandingResourceKey { get; set; } = string.Empty;
        public List<CharacterPresentationImageVariant> Variants { get; set; } = new List<CharacterPresentationImageVariant>();

        /// <summary>NoAlloc: an empty override inherits that slot from the base. Unknown variants use the base.</summary>
        public string ResolveResourceKey(bool standing, string variantId = "")
        {
            string inherited = standing ? StandingResourceKey : PortraitResourceKey;
            for (int i = 0; Variants != null && i < Variants.Count; i++)
            {
                CharacterPresentationImageVariant variant = Variants[i];
                if (variant == null || !string.Equals(variant.VariantId, variantId, StringComparison.Ordinal)) continue;
                string value = standing ? variant.StandingResourceKey : variant.PortraitResourceKey;
                return string.IsNullOrWhiteSpace(value) ? inherited ?? string.Empty : value;
            }
            return inherited ?? string.Empty;
        }
    }

    /// <summary>Appearance identity supplied explicitly by the consumer; never inferred from a tactical variant or display name.</summary>
    public sealed class CharacterPresentationImageVariant
    {
        public string VariantId { get; set; } = string.Empty;
        public string PortraitResourceKey { get; set; } = string.Empty;
        public string StandingResourceKey { get; set; } = string.Empty;
    }
}
