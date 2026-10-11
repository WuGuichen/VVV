using System;
using MxFramework.Gameplay.Triggers;
using MxFramework.Gameplay.Triggers.Config;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class
        CharacterTestLevelGameplayTriggerAuthoringDocument
    {
        /// <summary>
        /// Returns the immutable Runtime-authoritative condition tree referenced
        /// by one level Trigger entry.
        /// </summary>
        /// <remarks>NoAlloc.</remarks>
        internal bool TryGetConditionRoot(
            CharacterTestLevelGameplayTriggerEntry entry,
            out GameplayTriggerConditionNodeAuthoring root)
        {
            root = null;
            if (entry == null || Authoring?.Conditions == null)
                return false;

            GameplayTriggerConditionAuthoring[] rows =
                Authoring.Conditions;
            for (int i = 0; i < rows.Length; i++)
            {
                GameplayTriggerConditionAuthoring row = rows[i];
                if (row?.ConditionId != entry.ConditionId)
                    continue;

                root = row.Root;
                return root != null;
            }

            return false;
        }

        /// <summary>
        /// Replaces one entry's existing condition tree without changing its
        /// stable ConditionId or any Route reference.
        /// </summary>
        /// <remarks>
        /// AllocByDesign. Editor authoring mutation only; allocates a replacement
        /// condition array and document shell.
        /// </remarks>
        internal bool TryUpdateConditionRoot(
            string stableId,
            GameplayTriggerConditionNodeAuthoring root)
        {
            if (root == null
                || !TryFindEntry(
                    stableId,
                    out CharacterTestLevelGameplayTriggerEntry entry)
                || Authoring?.Conditions == null)
            {
                return false;
            }

            GameplayTriggerConditionAuthoring[] source =
                Authoring.Conditions;
            var replacement =
                new GameplayTriggerConditionAuthoring[source.Length];
            bool found = false;
            for (int i = 0; i < source.Length; i++)
            {
                GameplayTriggerConditionAuthoring row = source[i];
                if (row?.ConditionId == entry.ConditionId)
                {
                    replacement[i] =
                        new GameplayTriggerConditionAuthoring(
                            row.ConditionId,
                            root);
                    found = true;
                }
                else
                {
                    replacement[i] = row;
                }
            }

            if (!found)
                return false;

            Authoring = new GameplayTriggerAuthoringDocument(
                Authoring.SchemaId,
                Authoring.SchemaVersion,
                Authoring.Sources,
                replacement,
                Authoring.Definitions,
                Authoring.Bindings);
            return true;
        }

        /// <summary>
        /// Resolves the existing source kind used to constrain Editor condition
        /// candidates to semantically valid options.
        /// </summary>
        /// <remarks>NoAlloc.</remarks>
        internal bool TryGetSourceKind(
            CharacterTestLevelGameplayTriggerEntry entry,
            out GameplayTriggerSourceKind sourceKind)
        {
            sourceKind = default;
            if (entry == null || Authoring?.Sources == null)
                return false;

            GameplayTriggerSourceAuthoring[] rows = Authoring.Sources;
            for (int i = 0; i < rows.Length; i++)
            {
                GameplayTriggerSourceAuthoring row = rows[i];
                if (row?.SourceDefinitionId
                    != entry.SourceDefinitionId)
                {
                    continue;
                }

                sourceKind = row.SourceKind;
                return true;
            }

            return false;
        }
    }
}
