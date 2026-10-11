using MxFramework.Gameplay;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    internal enum BehaviorCommandReceiptMatch { Fresh, Replay, Rejected }
    // Storage owned by the existing behavior receipt publisher. Buffer sequence, not frame trace, is the cause.
    // No eviction within a frame. Older consumed frames and overload reject without repeating effects.
    internal sealed class CharacterTestBehaviorCommandReceipts<T> where T:struct
    {
        private struct Entry {internal int Source,Operation,Resource,Payload2;internal long Sequence;internal GameplayEntityId Actor;internal RuntimeTraceIdentity Trace;internal RuntimeCommandEnvelope Envelope;internal bool Complete;internal T Receipt;}
        private readonly Entry[] _entries;
        private long _frame=-1,_minimumFrame;private int _count;
        internal CharacterTestBehaviorCommandReceipts(int capacity=256){if(capacity<=0)throw new System.ArgumentOutOfRangeException(nameof(capacity));_entries=new Entry[capacity];}
        internal BehaviorCommandReceiptMatch Match(long frame,int source,long sequence,GameplayEntityId actor,int operation,int resource,RuntimeTraceIdentity trace,out T receipt,int payload2=0,RuntimeCommandEnvelope envelope=default)
        {
            receipt=default;
            if(frame<0 || frame<_minimumFrame || frame<_frame || source<0 || sequence<0 || !actor.IsValid)return BehaviorCommandReceiptMatch.Rejected;
            if(frame>_frame){System.Array.Clear(_entries,0,_entries.Length);_count=0;_frame=frame;}
            for(int i=0;i<_count;i++)if(_entries[i].Source==source && _entries[i].Sequence==sequence){var e=_entries[i];
                if(!e.Complete || e.Actor!=actor || e.Operation!=operation || e.Resource!=resource || e.Trace!=trace || e.Payload2!=payload2 || e.Envelope!=envelope)return BehaviorCommandReceiptMatch.Rejected;
                receipt=e.Receipt;return BehaviorCommandReceiptMatch.Replay;}
            return _count<_entries.Length?BehaviorCommandReceiptMatch.Fresh:BehaviorCommandReceiptMatch.Rejected;
        }
        internal int Begin(int source,long sequence,GameplayEntityId actor,int operation,int resource,RuntimeTraceIdentity trace,int payload2=0,RuntimeCommandEnvelope envelope=default)
        {
            if(_count==_entries.Length)throw new System.InvalidOperationException("Behavior command receipt capacity changed during commit.");
            int index=_count++;
            _entries[index]=new Entry {Source=source,Sequence=sequence,Actor=actor,Operation=operation,Resource=resource,Trace=trace,Payload2=payload2,Envelope=envelope};
            return index;
        }
        internal void Complete(int index,in T receipt){var entry=_entries[index];entry.Receipt=receipt;entry.Complete=true;_entries[index]=entry;}
        // Mirror the existing command buffer's inclusive first replay/submission frame.
        // World/Ability/resource restoration remains with the existing authority restore owner.
        internal void ResetFromFrame(long frame)
        {
            if(frame<0)throw new System.ArgumentOutOfRangeException(nameof(frame));
            System.Array.Clear(_entries,0,_entries.Length);_count=0;_frame=frame;_minimumFrame=frame;
        }
    }
}
