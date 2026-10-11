using System;
using MxFramework.Game.CharacterTest.TacticalCard;

namespace MxFramework.Game.CharacterTest
{
    internal static partial class CharacterTestTacticalAuthoringTemplates
    {
        internal static CharacterTestTacticalAuthoringSource CreateInteractionPreparation(TacticalInteractionKind kind)
        {
            var contract = new TacticalInteractionPreparationAuthoring { Effect = kind.ToString() };
            contract.Freeze();
            var source = CreateBlank(TacticalInteractionTrialCards.Name(kind));
            source.Schema = CharacterTestTacticalAuthoringSource.PreparationSchema; source.Version = 5;
            source.Slots.Clear(); source.InteractionPreparation = contract;
            return source.Clone();
        }
    }
}
