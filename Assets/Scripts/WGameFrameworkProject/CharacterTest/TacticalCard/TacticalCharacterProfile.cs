using System;
using System.Collections.Generic;
using MxFramework.Game.CharacterTest;

namespace MxFramework.Game.CharacterTest.TacticalCard
{
    /// <summary>
    /// Tactical card loadout assigned to a character during combat deployment.
    /// Manages the deck and initial tactical resources without duplicating character data.
    /// </summary>
    /// <remarks>
    /// Deliberately carries no display text. A hero's name, archetype and description belong to the
    /// character package (or its localization table), not to the card slice; earlier revisions kept
    /// invented Chinese titles here and the armory presented them as if they were authored data.
    /// </remarks>
    public sealed class TacticalCardLoadout
    {
        public string LoadoutId { get; }
        public int InitialMomentum { get; }
        public IReadOnlyList<TacticalCardDefinition> Cards { get; }

        public TacticalCardLoadout(
            string loadoutId,
            int initialMomentum,
            IReadOnlyList<TacticalCardDefinition> cards)
        {
            LoadoutId = loadoutId ?? string.Empty;
            InitialMomentum = initialMomentum;
            Cards = cards ?? Array.Empty<TacticalCardDefinition>();
        }
    }

    /// <summary>One deployable hero: its runtime character identity plus its dedicated tactical loadout.</summary>
    public readonly struct TacticalHeroEntry
    {
        public TacticalHeroEntry(int characterId, TacticalCardLoadout loadout)
        {
            CharacterId = characterId;
            Loadout = loadout;
        }

        public int CharacterId { get; }
        public TacticalCardLoadout Loadout { get; }
    }

    /// <summary>Projects the discovered characters and their saved tactical inventory; contains no authored content.</summary>
    public static class TacticalDeckCatalog
    {
        private static readonly TacticalCardLoadout Empty = new TacticalCardLoadout("unconfigured", 0, Array.Empty<TacticalCardDefinition>());
        public static TacticalCardLoadout DefaultLoadout => Empty;
        public static int ResolveCharacterId(string packageId) => CharacterTestPlayableCharacterCatalog.ComputeCharacterId(packageId);
        public static IReadOnlyList<TacticalHeroEntry> HeroRoster
        {
            get
            {
                var roster = new List<TacticalHeroEntry>();
                foreach (var character in CharacterTestPlayableCharacterCatalog.Discover())
                    roster.Add(new TacticalHeroEntry(character.CharacterId, GetLoadout(character)));
                return roster;
            }
        }
        public static bool TryGetPackageId(CharacterTestCharacterDefinition definition, out string packageId)
        {
            packageId = System.IO.Path.GetFileName((definition.GeneratedPackageRoot ?? string.Empty).TrimEnd('/', '\\'));
            return !string.IsNullOrEmpty(packageId);
        }
        public static bool IsHero(int characterId)
        {
            foreach (var character in CharacterTestPlayableCharacterCatalog.Discover())
                if (character.CharacterId == characterId) return character.TacticalDefaults != null;
            return false;
        }
        public static bool IsHero(CharacterTestCharacterDefinition definition) => definition.TacticalDefaults != null;
        public static TacticalCardLoadout GetLoadout(int characterId)
        {
            foreach (var character in CharacterTestPlayableCharacterCatalog.Discover())
                if (character.CharacterId == characterId) return GetLoadout(character);
            return Empty;
        }
        public static TacticalCardLoadout GetLoadout(CharacterTestCharacterDefinition definition)
        {
            var service = CharacterTacticalServices.Player;
            var state = definition.TacticalDefaults == null ? null : service?.Read(definition.TacticalDefaults.CharacterStableId);
            if (state == null) return Empty;
            var cards = new List<TacticalCardDefinition>();
            foreach (var reference in state.Cards)
            {
                var card = service.Catalog.Card(reference);
                string name = string.IsNullOrWhiteSpace(card?.DisplayName) ? reference.CardId + "（名称待完善）" : card.DisplayName;
                cards.Add(new TacticalActionCardDefinition(reference.CardId, name,
                    "执行已发布的行为编排；具体动作由本卡槽位装配决定。", card?.MotionCost ?? 0,
                    reference.CardId, reference.Revision, reference.ContentHash, reference.InstanceId,
                    service.Catalog.ExactCard(reference)?.IconKey));
            }
            return new TacticalCardLoadout(state.CharacterStableId, service.Catalog.InitialMotion, cards);
        }
        public static TacticalCardLoadout GetLoadout(CharacterTestCharacterDefinition definition, out bool isSpecialized)
        { isSpecialized = IsHero(definition); return GetLoadout(definition); }
    }
}
