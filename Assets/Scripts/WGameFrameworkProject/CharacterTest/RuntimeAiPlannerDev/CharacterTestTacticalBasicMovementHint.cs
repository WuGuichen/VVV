using System.Globalization;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>Parameter-bearing regular movement term shared by player descriptions.</summary>
    /// <remarks>LowFreqAlloc. Immutable display projection of authored controls, independent of action measurements.</remarks>
    internal sealed class CharacterTestTacticalBasicMovementHint
    {
        internal const string TemplateId = "basic-movement.v1";
        internal const string Title = "常规移动";
        internal CharacterTestTacticalBasicMovementHint(string nodeId, string moveEventId, string turnEventId,
            float movementMultiplier, float rotationMultiplier)
        {
            NodeId = nodeId; MovementEventId = moveEventId; RotationEventId = turnEventId;
            MovementMultiplier = movementMultiplier; RotationMultiplier = rotationMultiplier;
        }
        internal string NodeId { get; }
        internal string MovementEventId { get; }
        internal string RotationEventId { get; }
        internal float MovementMultiplier { get; }
        internal float RotationMultiplier { get; }
        internal string MovementText => Multiplier(MovementMultiplier);
        internal string RotationText => Multiplier(RotationMultiplier);
        private static string Multiplier(float value) => float.IsNaN(value) || float.IsInfinity(value)
            ? "—" : "×" + value.ToString("0.###", CultureInfo.InvariantCulture);
    }
}
