using System;
using System.Collections.Generic;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>Authoring-save preparation only; candidate compilation never allocates identities.</summary>
    internal static class CharacterTestRuntimeAiLabDraftNumericIdentities
    {
        /// <summary>
        /// Batch resolver: resolves or allocates all supplied (domainId, sourceKey) pairs inside
        /// a single catalog transaction. Preferred over <see cref="ResolveForAuthoring"/> when set.
        /// </summary>
        internal static Func<IEnumerable<(int DomainId, string SourceKey)>,
            IReadOnlyDictionary<(int DomainId, string SourceKey), int>> BatchResolveForAuthoring;

        /// <summary>Single-request resolver; used as fallback when BatchResolveForAuthoring is null.</summary>
        internal static Func<int, string, int> ResolveForAuthoring;

        internal static void PrepareForSave(CharacterTestRuntimeAiLabDraftWorkspace workspace,
            CharacterTestRuntimeAiLabBehaviorGroupDraft selectedPresentation = null)
        {
            using var startupProfile = CharacterTestStartupProfiler.PrepareNumericIds.Auto();
            if (workspace == null) throw new ArgumentNullException(nameof(workspace));

            var groups = new List<CharacterTestRuntimeAiLabBehaviorGroupDraft>(workspace.Groups.Values);
            if (selectedPresentation != null && !groups.Contains(selectedPresentation)) groups.Add(selectedPresentation);

            // --- Collect all (entry, card) pairs that need identity resolution ---
            var needsResolution = new List<(
                CharacterTestRuntimeAiPlannerBehaviorModuleEntry Entry,
                int EntryDomain, string EntrySourceKey,
                int DefDomain, string DefSourceKey)>();

            foreach (var group in groups)
            {
                foreach (var entry in group.Entries)
                {
                    var runtime = entry.RuntimeEntry;
                    if (!workspace.Cards.TryGetValue(entry.CardDraftId, out var card)
                        || string.IsNullOrWhiteSpace(card.PublishedStableId))
                        throw new InvalidOperationException("A behavior draft must reserve its stable card identity before saving numeric references.");

                    if (BatchResolveForAuthoring == null && ResolveForAuthoring == null)
                    {
                        // No resolver registered (e.g. non-Editor context): require IDs already present.
                        if (runtime.NumericId <= 0 || runtime.DefinitionNumericId <= 0)
                            throw new InvalidOperationException("Numeric draft identities require the authoring identity directory before saving.");
                        continue;
                    }

                    needsResolution.Add((
                        runtime,
                        420, "behavior-entry/" + entry.EntryId,
                        417, "behavior-card/" + card.PublishedStableId));
                }
            }

            if (needsResolution.Count == 0)
                goto SyncProfile;

            // --- Resolve identities ---
            if (BatchResolveForAuthoring != null)
            {
                // Preferred path: single catalog transaction for all requests.
                var batchRequests = new List<(int DomainId, string SourceKey)>(needsResolution.Count * 2);
                foreach (var item in needsResolution)
                {
                    batchRequests.Add((item.EntryDomain, item.EntrySourceKey));
                    batchRequests.Add((item.DefDomain, item.DefSourceKey));
                }
                IReadOnlyDictionary<(int DomainId, string SourceKey), int> resolved =
                    BatchResolveForAuthoring(batchRequests);

                foreach (var item in needsResolution)
                {
                    int id = resolved[(item.EntryDomain, item.EntrySourceKey)];
                    int definition = resolved[(item.DefDomain, item.DefSourceKey)];
                    ValidateAndApply(item.Entry, id, definition);
                }
            }
            else
            {
                // Fallback path: single-request resolver (legacy / test compatibility).
                foreach (var item in needsResolution)
                {
                    int id = ResolveForAuthoring(item.EntryDomain, item.EntrySourceKey);
                    int definition = ResolveForAuthoring(item.DefDomain, item.DefSourceKey);
                    ValidateAndApply(item.Entry, id, definition);
                }
            }

            SyncProfile:
            // Standalone editing holds a separate profile clone for candidate compilation.
            foreach (var group in groups)
                foreach (var profileEntry in group.Profile.Entries)
                    foreach (var authoredEntry in group.Entries)
                        if (string.Equals(profileEntry.EntryId, authoredEntry.EntryId, StringComparison.Ordinal))
                        {
                            profileEntry.NumericId = authoredEntry.RuntimeEntry.NumericId;
                            profileEntry.DefinitionNumericId = authoredEntry.RuntimeEntry.DefinitionNumericId;
                            break;
                        }
        }

        private static void ValidateAndApply(
            CharacterTestRuntimeAiPlannerBehaviorModuleEntry entry,
            int id, int definition)
        {
            if (id <= 0 || definition <= 0
                || (entry.NumericId > 0 && entry.NumericId != id)
                || (entry.DefinitionNumericId > 0 && entry.DefinitionNumericId != definition))
                throw new InvalidOperationException("Behavior draft numeric identity conflicts with its authoring directory.");
            entry.NumericId = id;
            entry.DefinitionNumericId = definition;
        }
    }
}
