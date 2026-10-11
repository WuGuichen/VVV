using System;
using Newtonsoft.Json;

namespace MxFramework.Game.CharacterTest.TacticalCard
{
    /// <summary>Fixed authored contract: players cannot alter trigger, lifetime, cost, stacking or cleanup.</summary>
    internal sealed class TacticalInteractionPreparationAuthoring
    {
        [JsonProperty(Required = Required.Always)] public string Ruleset { get; set; } = "trial.v1";
        [JsonProperty(Required = Required.Always)] public string Effect { get; set; }
        internal InteractionPreparationPayload Freeze()
        {
            if (Ruleset != "trial.v1" || !Enum.TryParse<TacticalInteractionKind>(Effect, false, out var kind)
                || !Enum.IsDefined(typeof(TacticalInteractionKind), kind) || Effect != kind.ToString())
                throw new ArgumentException("Unknown fixed interaction preparation contract.");
            return new InteractionPreparationPayload(kind);
        }
    }
}
