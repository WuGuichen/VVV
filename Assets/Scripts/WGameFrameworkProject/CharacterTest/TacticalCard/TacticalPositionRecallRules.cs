using System;
using Newtonsoft.Json;

namespace MxFramework.Game.CharacterTest.TacticalCard
{
    internal sealed class TacticalPositionRecallAuthoring
    {
        [JsonProperty(Required = Required.Always)] public string Operation { get; set; }
        [JsonProperty(Required = Required.Always)] public int LifetimeFrames { get; set; }
        internal TacticalPositionRecallRules Freeze()
        {
            if (Operation != "Record" && Operation != "Return") throw new ArgumentException("Position recall requires explicit Record or Return operation.");
            return new TacticalPositionRecallRules(Operation == "Record" ? CharacterTestPositionRecallOperation.Record : CharacterTestPositionRecallOperation.Return, LifetimeFrames);
        }
    }
    internal sealed class TacticalPositionRecallRules
    {
        internal CharacterTestPositionRecallOperation Operation { get; }
        internal int LifetimeFrames { get; }
        internal TacticalPositionRecallRules(CharacterTestPositionRecallOperation operation, int lifetime)
        {
            if (operation != CharacterTestPositionRecallOperation.Record && operation != CharacterTestPositionRecallOperation.Return
                || (operation == CharacterTestPositionRecallOperation.Record ? lifetime < 1 || lifetime > 3600 : lifetime != 0))
                throw new ArgumentException("Record lifetime must be 1..3600 simulation frames; Return lifetime must be zero.");
            Operation = operation; LifetimeFrames = lifetime;
        }
        internal bool SameContract(TacticalPositionRecallRules other) => other != null && Operation == other.Operation && LifetimeFrames == other.LifetimeFrames;
    }
}
