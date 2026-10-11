using System;
namespace MxFramework.Game.CharacterTest.TacticalCard
{
    public sealed partial class TacticalActionCardDefinition
    {
        internal TacticalPositionRecallRules PositionRecall { get; private set; }
        internal bool DefersPaymentUntilStarted => PointMove != null || PositionRecall != null;
        internal static TacticalActionCardDefinition CreatePositionRecall(string id, string name, string description, int cost, TacticalPositionRecallRules rules,
            string publishedId = null, int revision = 0, string contentHash = null, ulong instance = 0, string iconKey = null)
        {
            if (rules == null) throw new ArgumentNullException(nameof(rules));
            if (publishedId != null && (string.IsNullOrWhiteSpace(publishedId) || revision <= 0 || string.IsNullOrWhiteSpace(contentHash))) throw new ArgumentException("Exact published recall reference required.");
            var card = new TacticalActionCardDefinition(id, name, description, cost, default(MxFramework.CharacterAction.CharacterActionId), 0f,
                TacticalTargetMode.None, TacticalApproachMode.None) { PositionRecall = rules };
            if (publishedId != null)
            { card.PublishedCardId = publishedId; card.PublishedRevision = revision; card.PublishedContentHash = contentHash; card.DeckEntryInstanceId = instance; card.IconKey = iconKey ?? string.Empty; }
            return card;
        }
    }
}
