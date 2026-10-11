using System;
using System.Collections.Generic;

namespace MxFramework.CharacterApplication
{
    /// <summary>Authoring defaults only. Player ownership and running executions never write this document.</summary>
    public sealed class CharacterTacticalDefaults
    {
        public const string RelativePath = "config/character_tactical_defaults.json";
        public string Schema { get; set; } = "mx.character.tactical-defaults.v1";
        public string CharacterStableId { get; set; } = string.Empty;
        public List<string> ActionIds { get; set; } = new List<string>();
        public List<CharacterTacticalCardReference> Cards { get; set; } = new List<CharacterTacticalCardReference>();
        public List<CharacterTacticalSlotBinding> Bindings { get; set; } = new List<CharacterTacticalSlotBinding>();
        public List<string> Diagnostics { get; set; } = new List<string>();

        /// <summary>Low-frequency structural validation; catalog resolution belongs to the consuming application.</summary>
        public bool TryValidate(out string issue)
        {
            issue = string.Empty;
            if (Schema != "mx.character.tactical-defaults.v1" || string.IsNullOrWhiteSpace(CharacterStableId)
                || ActionIds == null || Cards == null || Bindings == null || Diagnostics == null)
                return Fail("TACTICAL_DEFAULTS_DOCUMENT_INVALID", out issue);
            var actions = new HashSet<string>(StringComparer.Ordinal);
            foreach (string id in ActionIds)
                if (string.IsNullOrWhiteSpace(id) || !actions.Add(id))
                    return Fail("TACTICAL_DEFAULTS_ACTION_ID_INVALID", out issue);
            bool instanceMode = Cards.Exists(card => card?.InstanceId > 0);
            var cards = new HashSet<string>(StringComparer.Ordinal);
            var instances = new HashSet<ulong>();
            foreach (var card in Cards)
                if (card == null || string.IsNullOrWhiteSpace(card.CardId) || card.Revision <= 0
                    || (instanceMode ? card.InstanceId == 0 || !instances.Add(card.InstanceId) : !cards.Add(card.CardId)))
                    return Fail("TACTICAL_DEFAULTS_CARD_REFERENCE_INVALID", out issue);
            var slots = new HashSet<string>(StringComparer.Ordinal);
            foreach (var binding in Bindings)
                if (binding == null || (instanceMode
                        ? binding.CardInstanceId == 0 || !instances.Contains(binding.CardInstanceId)
                        : !cards.Contains(binding.CardId ?? string.Empty))
                    || !actions.Contains(binding.TacticalActionId ?? string.Empty)
                    || string.IsNullOrWhiteSpace(binding.SlotPath)
                    || !slots.Add((instanceMode ? binding.CardInstanceId.ToString() : binding.CardId) + "\n" + binding.SlotPath))
                    return Fail("TACTICAL_DEFAULTS_BINDING_INVALID", out issue);
            return true;
        }

        private static bool Fail(string value, out string issue) { issue = value; return false; }
    }

    /// <summary>Stable published card reference, independent of input commands or physical action roles.</summary>
    public sealed class CharacterTacticalCardReference
    {
        /// <summary>Zero in authoring defaults/catalog references; positive in player deck instances.</summary>
        public ulong InstanceId { get; set; }
        public string CardId { get; set; } = string.Empty;
        public int Revision { get; set; }
        public string ContentHash { get; set; } = string.Empty;
    }

    /// <summary>A card-local slot or expanded composition instance path bound to an owned tactical action.</summary>
    public sealed class CharacterTacticalSlotBinding
    {
        /// <summary>Zero in authoring defaults; positive for a player deck instance binding.</summary>
        public ulong CardInstanceId { get; set; }
        public string CardId { get; set; } = string.Empty;
        public string SlotPath { get; set; } = string.Empty;
        public string TacticalActionId { get; set; } = string.Empty;
    }
}
