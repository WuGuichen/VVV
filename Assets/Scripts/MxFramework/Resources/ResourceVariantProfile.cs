using System.Collections.Generic;

namespace MxFramework.Resources
{
    public sealed class ResourceVariantProfile
    {
        private readonly List<string> _fallbackVariants;

        public ResourceVariantProfile(string activeVariant = "", IEnumerable<string> fallbackVariants = null)
        {
            ActiveVariant = activeVariant ?? string.Empty;
            _fallbackVariants = fallbackVariants != null
                ? new List<string>(fallbackVariants)
                : new List<string>();
        }

        public static ResourceVariantProfile Empty { get; } = new ResourceVariantProfile();

        public string ActiveVariant { get; }
        public IReadOnlyList<string> FallbackVariants => _fallbackVariants;

        internal List<string> CreateCandidateVariants(string requestedVariant)
        {
            var variants = new List<string>();
            var unique = new HashSet<string>(System.StringComparer.Ordinal);

            string primary = string.IsNullOrWhiteSpace(requestedVariant)
                ? ActiveVariant
                : requestedVariant;
            AddVariant(primary, variants, unique);

            for (int i = 0; i < _fallbackVariants.Count; i++)
                AddVariant(_fallbackVariants[i], variants, unique);

            if (variants.Count == 0)
                variants.Add(string.Empty);

            return variants;
        }

        private static void AddVariant(string variant, List<string> variants, HashSet<string> unique)
        {
            variant = variant ?? string.Empty;
            if (!unique.Add(variant))
                return;

            variants.Add(variant);
        }
    }
}
