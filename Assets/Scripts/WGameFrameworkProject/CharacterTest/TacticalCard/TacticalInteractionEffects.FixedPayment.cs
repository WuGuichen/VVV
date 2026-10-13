using System;
using System.Collections.Generic;

namespace MxFramework.Game.CharacterTest.TacticalCard
{
    internal sealed partial class TacticalInteractionEffectAuthority
    {
        /// <summary>Cold complete-manifest validation before payment, followed by one allocation-free
        /// activation/authority publication. No JSON or incoming state creates a trusted journal.</summary>
        internal bool TryPrepareConfirmedAccessoryBatch(IReadOnlyList<TacticalAccessoryCombatState> candidates,
            TacticalFixedAccessoryAdmissionAuthority authority, out PreparedConfirmedAccessoryBatch prepared,
            out string issue)
        {
            prepared = null; issue = "fixed-payment:InvalidAccessoryBatch";
            if (authority == null || !authority.IsRestorable || candidates == null
                || ConfirmedFixedAssembly != null && !authority.Extends(ConfirmedFixedAssembly)) return false;
            // Existing authoring contract requires a declared overlap policy. It is still absent for these
            // candidates; this diagnostic is an implementation gap, not an invented stacking/replacement rule.
            for (int i = 0; i < candidates.Count; i++)
            {
                for (int j = 0; j < _accessories.Count; j++)
                    if (_accessories[j].Status == TacticalAccessoryCombatStatus.Armed && _accessories[j].Kind == candidates[i].Kind)
                    { issue = "fixed-payment:OverlapPolicyUnspecified"; return false; }
                for (int j = 0; j < i; j++)
                    if (candidates[j].Kind == candidates[i].Kind)
                    { issue = "fixed-payment:OverlapPolicyUnspecified"; return false; }
            }
            if (!TryPrepareAccessoryBatch(candidates, authority, out var batch)) return false;
            var snapshot = CaptureAccessories() ?? new TacticalAccessoryAuthoritySnapshot
            {
                Sequence = AccessoryReceiptSequence, LastFrame = -1,
                Activations = Array.Empty<TacticalAccessoryActivationData>(),
                Receipts = Array.Empty<TacticalAccessoryReceiptData>(), Consumptions = Array.Empty<TacticalAccessoryConsumptionData>()
            };
            var joined = new TacticalAccessoryActivationData[snapshot.Activations.Length + candidates.Count];
            Array.Copy(snapshot.Activations, joined, snapshot.Activations.Length);
            for (int i = 0; i < candidates.Count; i++) joined[snapshot.Activations.Length + i] = TacticalAccessoryActivationData.From(candidates[i]);
            snapshot.Activations = joined;
            if (!authority.ValidateRestoration(snapshot, Stage, Turn, Actor)) return false;
            prepared = new PreparedConfirmedAccessoryBatch(this, batch, authority);
            issue = null; return true;
        }

        internal sealed class PreparedConfirmedAccessoryBatch
        {
            private readonly TacticalInteractionEffectAuthority _owner;
            private readonly PreparedAccessoryBatch _batch;
            private readonly TacticalFixedAccessoryAdmissionAuthority _previous, _confirmed;
            private bool _published;
            internal PreparedConfirmedAccessoryBatch(TacticalInteractionEffectAuthority owner,
                PreparedAccessoryBatch batch, TacticalFixedAccessoryAdmissionAuthority confirmed)
            { _owner = owner; _batch = batch; _previous = owner.ConfirmedFixedAssembly; _confirmed = confirmed; }
            internal bool IsCurrent => !_published && _batch.IsCurrent && ReferenceEquals(_previous, _owner.ConfirmedFixedAssembly);
            internal void Publish()
            {
                if (!IsCurrent) throw new InvalidOperationException("fixed-payment:PreparedAccessoriesChanged");
                _batch.Publish(); _owner.ConfirmedFixedAssembly = _confirmed; _published = true;
            }
        }
    }
}
