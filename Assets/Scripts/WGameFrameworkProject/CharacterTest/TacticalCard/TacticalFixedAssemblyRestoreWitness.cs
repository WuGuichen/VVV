using System;
using System.Linq;
using Newtonsoft.Json;

namespace MxFramework.Game.CharacterTest.TacticalCard
{
    /// <summary>LowFreqAlloc. Private evidence from the actual frozen barrier, never from incoming JSON.</summary>
    /// <remarks>It witnesses mutable state at one frame, not payment history. The trusted joint owner must
    /// supply confirmed records after real payment. A promoted manifest alone cannot prove payment.</remarks>
    internal sealed class TacticalFixedAssemblyRestoreWitness
    {
        private readonly string _effects, _ledger, _deck;
        internal readonly long Frame;
        internal readonly TacticalFixedAccessoryAdmissionAuthority Admission;

        private TacticalFixedAssemblyRestoreWitness(long frame, TacticalFixedAccessoryAdmissionAuthority admission,
            TacticalInteractionSnapshot effects, MomentumSnapshot ledger, TacticalDeckSnapshot deck)
        {
            Frame = frame; Admission = admission;
            _effects = JsonConvert.SerializeObject(effects); _ledger = JsonConvert.SerializeObject(ledger);
            _deck = JsonConvert.SerializeObject(deck);
        }

        internal static bool TryCapture(TacticalInteractionEffectAuthority effects, MomentumLedger ledger, long frame,
            TacticalDeckManager deck, TacticalCardDefinition[] definitions, out TacticalFixedAssemblyRestoreWitness witness)
        {
            witness = null;
            if (effects == null || ledger == null || frame < 0 || deck == null || definitions == null) return false;
            var admission = effects.ConfirmedFixedAssembly;
            var state = effects.Capture();
            if (admission == null || !admission.IsRestorable || !state.IsValid(frame)
                || !admission.ValidateRestoration(state.Accessories, state.Stage, state.Turn, state.Actor)) return false;
            var cards = deck.DrawPile.Concat(deck.Hand).Concat(deck.DiscardPile).Concat(deck.ExhaustPile)
                .Concat(deck.PendingAction == null ? Array.Empty<TacticalCardInstance>() : new[] { deck.PendingAction })
                .Concat(deck.ResolvingAction == null ? Array.Empty<TacticalCardInstance>() : new[] { deck.ResolvingAction }).ToArray();
            for (int i = 0; i < admission.CommitCount; i++) {
                var commit = admission.CommitAt(i);
                var matches = cards.Where(card => card.InstanceId == commit.BattleCardInstance).ToArray();
                if (matches.Length != 1 || !ReferenceEquals(matches[0].Definition.FixedAssembly, commit.Assembly)
                    || commit.Frame > frame || commit.Turn > state.Turn || commit.Stage != state.Stage || commit.Actor != state.Actor) return false;
            }
            witness = new TacticalFixedAssemblyRestoreWitness(frame, admission, state, ledger.CaptureSessionState(), deck.CaptureSessionState(definitions));
            return true;
        }

        internal bool Matches(TacticalInteractionSnapshot effects, MomentumSnapshot ledger, TacticalDeckSnapshot deck, long frame)
            => frame == Frame && effects != null && ledger != null && deck != null && effects.IsValid(frame)
                && Admission.ValidateRestoration(effects.Accessories, effects.Stage, effects.Turn, effects.Actor)
                && _effects == JsonConvert.SerializeObject(effects) && _ledger == JsonConvert.SerializeObject(ledger)
                && _deck == JsonConvert.SerializeObject(deck);

        internal static string CommitFingerprint(TacticalFixedAccessoryAdmissionAuthority authority)
        {
            if (authority == null || !authority.IsRestorable) return null;
            var records = Enumerable.Range(0, authority.CommitCount).Select(i => authority.CommitAt(i).Digest).ToArray();
            var digest = MxFramework.Runtime.RuntimeHashDigest.ComputeContentDigest(JsonConvert.SerializeObject(new {
                Format = TacticalFixedCardCommitManifest.FormatVersion, Ordering = TacticalFixedAssemblyManifest.OrderingVersion, Records = records }));
            return digest.Word0.ToString("x16") + digest.Word1.ToString("x16") + digest.Word2.ToString("x16") + digest.Word3.ToString("x16");
        }
    }
}
