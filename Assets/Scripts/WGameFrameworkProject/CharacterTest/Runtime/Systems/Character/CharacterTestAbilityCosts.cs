using System.Collections.Generic;
using MxFramework.Gameplay;
using MxFramework.Game.CharacterTest.TacticalCard;

namespace MxFramework.Game.CharacterTest
{
    // Same attribute store and MomentumLedger as the existing world. No second balance.
    internal static class CharacterTestAbilityCosts
    {
        internal static bool CanAfford(GameplayComponentWorld world,GameplayEntityId actor,IMomentumLedger motion,
            IReadOnlyList<AbilityCostConfig> costs,int additionalCardMotion,out int missing,int reservationCredit=0)
            => TryPrepare(world,actor,motion,costs,additionalCardMotion,reservationCredit,false,out _,out _,out _,out missing);

        internal static bool TryCommit(GameplayComponentWorld world,GameplayEntityId actor,IMomentumLedger motion,
            IReadOnlyList<AbilityCostConfig> costs,out int missing)
        {
            if(!TryPrepare(world,actor,motion,costs,0,0,true,out var store,out var attributes,out int motionCost,out missing))return false;
            // All attribute validation/copying is complete before the first authority write.
            // Single-threaded command boundary; no callbacks or world tick between these writes.
            if(motionCost>0 && !motion.TrySpend(motionCost)){missing=CharacterTestCharacterAttributeIds.Motion;return false;}
            if(store!=null)store.Set(actor,attributes);
            return true;
        }
        internal static bool TryGetMotionCost(IReadOnlyList<AbilityCostConfig> costs,out int total)
        {
            long sum=0;
            for(int i=0;costs!=null && i<costs.Count;i++)if(costs[i]!=null && costs[i].AttributeId==CharacterTestCharacterAttributeIds.Motion)sum+=costs[i].Amount;
            total=sum<=int.MaxValue?(int)sum:0;return sum>=0 && sum<=int.MaxValue;
        }
        private static bool TryPrepare(GameplayComponentWorld world,GameplayEntityId actor,IMomentumLedger motion,
            IReadOnlyList<AbilityCostConfig> costs,int extra,int reservationCredit,bool build,out GameplayComponentStore<GameplayAttributeSetComponent> store,
            out GameplayAttributeSetComponent next,out int motionCost,out int missing)
        {
            store=null;next=default;motionCost=0;missing=0;
            if(world==null || !world.IsAlive(actor) || extra<0 || reservationCredit<0 ||
                reservationCredit>(motion?.Reserved??0) || !TryGetMotionCost(costs,out motionCost))return false;
            if((long)motionCost+extra>0 && (motion==null || (long)motionCost+extra>(long)motion.Spendable+reservationCredit))
            {missing=CharacterTestCharacterAttributeIds.Motion;return false;}
            bool attributesNeeded=false;
            for(int i=0;costs!=null && i<costs.Count;i++)if(costs[i]!=null && costs[i].Amount>0 && costs[i].AttributeId!=CharacterTestCharacterAttributeIds.Motion)attributesNeeded=true;
            if(!attributesNeeded)return true;
            if(!world.TryGetStore(out store) || !store.TryGet(actor,out var before))return false;
            next=before;
            for(int i=0;i<costs.Count;i++){
                var cost=costs[i];if(cost==null || cost.Amount<=0 || cost.AttributeId==CharacterTestCharacterAttributeIds.Motion)continue;
                bool seen=false;for(int j=0;j<i;j++)if(costs[j]!=null && costs[j].Amount>0 && costs[j].AttributeId==cost.AttributeId){seen=true;break;}
                if(seen)continue;
                long total=0;for(int j=i;j<costs.Count;j++)if(costs[j]!=null && costs[j].AttributeId==cost.AttributeId)total+=costs[j].Amount;
                if(total>int.MaxValue || !before.TryGet(cost.AttributeId,out var value) || total>value.CurrentValue)
                {missing=cost.AttributeId;return false;}
                if(build)next=next.SetCurrentValue(cost.AttributeId,value.CurrentValue-(int)total);
            }
            return true;
        }
    }
}
