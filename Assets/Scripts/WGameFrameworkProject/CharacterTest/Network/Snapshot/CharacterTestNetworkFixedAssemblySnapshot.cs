using System;
using MxFramework.Game.CharacterTest.TacticalCard;
using MxFramework.Runtime;
using MxFramework.Network;
using Newtonsoft.Json;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>Optional state projection. Transported identities never authorize reconstruction of a trusted journal.</summary>
    internal sealed class CharacterTestNetworkFixedAssemblySnapshot
    {
        public int Runtime, ActorIndex, ActorGeneration;
        public TacticalInteractionSnapshot Effects;
        public MomentumSnapshot Ledger;
        public string CommitFingerprint;
        internal CharacterTestNetworkFixedAssemblySnapshot Clone() => new CharacterTestNetworkFixedAssemblySnapshot {
            Runtime = Runtime, ActorIndex = ActorIndex, ActorGeneration = ActorGeneration, Effects = Effects?.Clone(),
            Ledger = Ledger == null ? null : new MomentumSnapshot { Current = Ledger.Current, Reserved = Ledger.Reserved,
                NextToken = Ledger.NextToken, ActiveToken = Ledger.ActiveToken }, CommitFingerprint = CommitFingerprint };
    }

    public sealed partial class CharacterTestNetworkSnapshotParticipant
    {
        internal const string MomentumLedgerAggregateRestoreUnavailableMessage =
            "network:MomentumLedgerAggregateRestoreUnavailable: World-bound MomentumLedger requires an aggregate ledger/deck restore owner.";

        private bool HasFixedAssemblyWorld()
        {
            if (_characterRuntimeModule == null) return false;
            foreach (var actor in _characterRuntimeModule.Entities) if (actor.InteractionEffects.HasFixedAssemblyState) return true;
            return false;
        }
        internal CharacterTestNetworkFixedAssemblySnapshot[] CaptureFixedAssemblyState(long frame)
        {
            var rows = new System.Collections.Generic.List<CharacterTestNetworkFixedAssemblySnapshot>();
            if (!HasFixedAssemblyWorld()) return null;
            foreach (var actor in _characterRuntimeModule.Entities) {
                var effects = actor.InteractionEffects;
                bool fixedState = effects.HasFixedAssemblyState;
                bool resourceOwner = _characterRuntimeModule.World.TryGetTacticalMotion(actor.Id.Value, out var bound);
                if (!fixedState && !resourceOwner) continue;
                if (fixedState && (effects.ConfirmedFixedAssembly == null || !effects.ConfirmedFixedAssembly.IsRestorable)
                    || !(bound is MomentumLedger ledger))
                    throw new InvalidOperationException("network:FixedAssemblyTrustedJournalUnavailable");
                var state = effects.Capture();
                if (!state.IsValid(frame) || fixedState && !effects.ConfirmedFixedAssembly.ValidateRestoration(state.Accessories, state.Stage, state.Turn, state.Actor))
                    throw new InvalidOperationException("network:FixedAssemblyStateJoin");
                rows.Add(new CharacterTestNetworkFixedAssemblySnapshot { Runtime = actor.Id.Value,
                    ActorIndex = actor.GameplayEntityId.Index, ActorGeneration = actor.GameplayEntityId.Generation,
                    Effects = state, Ledger = ledger.CaptureSessionState(),
                    CommitFingerprint = TacticalFixedAssemblyRestoreWitness.CommitFingerprint(effects.ConfirmedFixedAssembly) });
            }
            return rows.Count == 0 ? null : rows.ToArray();
        }

        // NoAlloc. Read the actual World binding; no resource mirror or snapshot allocation in the hash path.
        private void AddFixedAssemblyHash(ref RuntimeHashAccumulator hash, CharacterTestCharacterEntity actor)
        {
            bool hasLedger = _characterRuntimeModule.World.TryGetTacticalMotion(actor.Id.Value, out var bound) && bound is MomentumLedger;
            if (!hasLedger && !actor.InteractionEffects.HasFixedAssemblyState) return;
            actor.InteractionEffects.AddHash(ref hash);
            hash.AddBool(357, hasLedger);
            if (hasLedger) ((MomentumLedger)bound).AddFixedAssemblyHash(ref hash);
            bool confirmed = actor.InteractionEffects.ConfirmedFixedAssembly != null;
            hash.AddBool(366, confirmed);
            if (confirmed) actor.InteractionEffects.ConfirmedFixedAssembly.AddHash(ref hash);
        }

        internal NetworkSnapshotApplyResult PreflightFixedAssemblyRollback(CharacterTestNetworkSnapshotDocument document)
        {
            _authorityAvailabilityGuard?.Invoke();
            if (document == null || !IsSupportedSchemaVersion(document.SchemaVersion))
                return NetworkSnapshotApplyResult.Failed("CharacterTest rollback snapshot schema mismatch.");
            return ValidateFixedAssemblyImport(document, null);
        }

        internal NetworkSnapshotApplyResult PreflightFixedAssemblyRollback(RuntimeCustomState snapshot)
        {
            _authorityAvailabilityGuard?.Invoke();
            if (snapshot == null || snapshot.NumericTypeId != NumericParticipantId || !IsSupportedSchemaVersion(snapshot.SchemaVersion))
                return NetworkSnapshotApplyResult.Failed("CharacterTest rollback snapshot participant/schema mismatch.");
            try {
                var document = JsonConvert.DeserializeObject<CharacterTestNetworkSnapshotDocument>(snapshot.PayloadJson, JsonSettings);
                if (document == null || document.SchemaVersion != snapshot.SchemaVersion)
                    return NetworkSnapshotApplyResult.Failed("CharacterTest rollback envelope/document schema mismatch.");
                return PreflightFixedAssemblyRollback(document);
            }
            catch (Exception ex) { return NetworkSnapshotApplyResult.Failed("CharacterTest rollback JSON parse failed: " + ex.Message); }
        }

        private NetworkSnapshotApplyResult ValidateFixedAssemblyImport(CharacterTestNetworkSnapshotDocument document,
            Func<CharacterTestNetworkFixedAssemblySnapshot[], long, bool> frozenPausedPreflight)
        {
            if (document == null) return NetworkSnapshotApplyResult.Failed("CharacterTest snapshot payload is empty.");
            // Match the per-actor production hash boundary, including a real ledger with no assembly.
            // The generic importer has no aggregate owner for that ledger/deck authority.
            bool currentHasState = false, currentHasLedger = false;
            if (_characterRuntimeModule != null) foreach (var actor in _characterRuntimeModule.Entities) {
                if (actor.InteractionEffects.HasFixedAssemblyState) currentHasState = true;
                if (_characterRuntimeModule.World.TryGetTacticalMotion(actor.Id.Value, out var ledger) && ledger is MomentumLedger)
                    currentHasLedger = true;
            }
            bool incomingHasState = document.FixedAssemblyState != null;
            if (frozenPausedPreflight != null)
                return (!incomingHasState || document.SchemaVersion >= FixedAssemblySchemaVersionValue) && frozenPausedPreflight(document.FixedAssemblyState, document.Frame)
                    ? NetworkSnapshotApplyResult.Succeeded() : NetworkSnapshotApplyResult.Failed("network:FixedAssemblyPausedWitnessMismatch");
            if (!currentHasState && !incomingHasState)
                return currentHasLedger ? NetworkSnapshotApplyResult.Failed(MomentumLedgerAggregateRestoreUnavailableMessage)
                    : NetworkSnapshotApplyResult.Succeeded();
            return NetworkSnapshotApplyResult.Failed("network:FixedAssemblyAggregateRestoreUnavailable");
        }
    }
}
