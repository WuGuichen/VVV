using System.Collections.Generic;

namespace MxFramework.Authoring
{
    public static class AuthoringResourceCollectionMerger
    {
        public static AuthoringResourceCollection Merge(params AuthoringResourceCollection[] collections)
        {
            var merged = new AuthoringResourceCollection();
            if (collections == null)
                return merged;

            for (int i = 0; i < collections.Length; i++)
                MergeInto(merged, collections[i]);

            return merged;
        }

        public static void MergeInto(AuthoringResourceCollection target, AuthoringResourceCollection source)
        {
            if (target == null || source == null)
                return;

            if (string.IsNullOrWhiteSpace(target.ScopeId))
                target.ScopeId = source.ScopeId ?? string.Empty;

            CopyMetadata(target.Metadata, source.Metadata);
            if (source.Providers != null)
                target.Providers.AddRange(source.Providers);
            if (source.Items != null)
                target.Items.AddRange(source.Items);
            if (source.Diagnostics != null)
                target.Diagnostics.AddRange(source.Diagnostics);
        }

        private static void CopyMetadata(Dictionary<string, string> target, Dictionary<string, string> source)
        {
            if (target == null || source == null)
                return;

            foreach (KeyValuePair<string, string> pair in source)
            {
                if (!string.IsNullOrWhiteSpace(pair.Key) && !target.ContainsKey(pair.Key))
                    target[pair.Key] = pair.Value ?? string.Empty;
            }
        }
    }
}
