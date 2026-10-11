using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>Frozen definition-level ordering; never derived from an installation index or entry override.</summary>
    internal sealed class CharacterTestBehaviorCardSelectionDefinition
    {
        internal CharacterTestBehaviorCardSelectionDefinition(string definitionId, float priority,
            IEnumerable<CharacterTestRuntimeAiPlannerBehaviorCapability> capabilities = null)
        {
            if (string.IsNullOrWhiteSpace(definitionId)) throw new ArgumentException("A stable card definition identity is required.");
            if (float.IsNaN(priority) || float.IsInfinity(priority)) throw new ArgumentOutOfRangeException(nameof(priority));
            DefinitionId = definitionId; Priority = priority;
            Capabilities = Array.AsReadOnly((capabilities ?? Array.Empty<CharacterTestRuntimeAiPlannerBehaviorCapability>()).Distinct().OrderBy(c => c).ToArray());
            var hash = new RuntimeContentHashAccumulator(); hash.AddStringStable("selection.contract", "card-definition.v1");
            hash.AddStringStable("selection.definition", definitionId); hash.AddInt("selection.priority", BitConverter.SingleToInt32Bits(priority));
            hash.AddInt("selection.capabilities", Capabilities.Count);
            foreach (var capability in Capabilities) hash.AddInt("selection.capability", (int)capability);
            StableHash = hash.ToHash();
        }
        internal string DefinitionId { get; }
        internal float Priority { get; }
        internal long StableHash { get; }
        internal ReadOnlyCollection<CharacterTestRuntimeAiPlannerBehaviorCapability> Capabilities { get; }
        internal static int Compare(CharacterTestBehaviorCardSelectionDefinition left, CharacterTestBehaviorCardSelectionDefinition right)
        {
            int priority = right.Priority.CompareTo(left.Priority);
            return priority != 0 ? priority : string.CompareOrdinal(left.DefinitionId, right.DefinitionId);
        }
        internal static CharacterTestBehaviorCardSelectionDefinition FromCard(CharacterTestRuntimeAiLabBehaviorCardDraft card)
            => new CharacterTestBehaviorCardSelectionDefinition(string.IsNullOrEmpty(card.PublishedStableId)
                    ? ReservedStableId(card.CardDraftId) : card.PublishedStableId,
                CharacterTestRuntimeAiPlannerPlayerDeckRuntimeCompiler.ResolveCanonicalScoreFromCapabilities(card.RequiredCapabilities, card.ResponsibilityKinds), card.RequiredCapabilities);
        internal static CharacterTestBehaviorCardSelectionDefinition FromPublished(CharacterTestRuntimeAiPlannerPublishedBehaviorCard card)
            => new CharacterTestBehaviorCardSelectionDefinition(card.StableCardId,
                CharacterTestRuntimeAiPlannerPlayerDeckRuntimeCompiler.ResolveCanonicalScore(card.Metadata), card.Metadata?.RequiredCapabilities);
        internal static CharacterTestBehaviorCardSelectionDefinition FromTacticalCard(CharacterTestRuntimeAiLabBehaviorCardDraft card,
            CharacterTestRuntimeAiLabCompiledLogicProgram program)
        {
            var capabilities = CharacterTestBehaviorExecutionUsage.Analyze(program, basicAttackSource: true).Capabilities;
            return new CharacterTestBehaviorCardSelectionDefinition(string.IsNullOrEmpty(card.PublishedStableId)
                    ? ReservedStableId(card.CardDraftId) : card.PublishedStableId,
                CharacterTestRuntimeAiPlannerPlayerDeckRuntimeCompiler.ResolveCanonicalScoreFromCapabilities(capabilities), capabilities);
        }
        internal static string ReservedStableId(string authoringIdentity)
        {
            // Deterministic migration for an existing unpublished authoring definition. The
            // source identity is created by the card owner, not during player installation.
            using var sha = SHA256.Create();
            byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes("card-definition:" + authoringIdentity));
            ulong value = 0; for (int i = 0; i < 8; i++) value |= (ulong)hash[i] << (i * 8);
            return CharacterTestRuntimeAiPlannerProjectCatalogAuthoringSession.FormatProjectCardStableId(value);
        }
    }

    internal sealed partial class CharacterTestRuntimeAiLabBehaviorCardDraft
    {
        internal void EnsureReservedTacticalIdentity()
        {
            if (string.IsNullOrEmpty(PublishedStableId) && SourcePublishedRevision == 0)
                PublishedStableId = CharacterTestBehaviorCardSelectionDefinition.ReservedStableId(CardDraftId);
        }
    }
}
