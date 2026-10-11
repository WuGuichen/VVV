using System;
using MxFramework.Game.CharacterTest.TacticalCard;

namespace MxFramework.Game.CharacterTest
{
    internal static partial class CharacterTestTacticalAuthoringTemplates
    {
        internal static CharacterTestTacticalAuthoringSource CreatePositionRecall(string name, TacticalPositionRecallAuthoring contract)
        {
            if (contract == null) throw new ArgumentNullException(nameof(contract));
            contract.Freeze();
            var source = CreateBlank(name);
            source.Schema = CharacterTestTacticalAuthoringSource.TimingSchema; source.Version = 4;
            source.Slots.Clear(); source.PositionRecall = contract;
            return source.Clone();
        }
    }
}
