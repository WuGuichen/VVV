using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using MxFramework.Gameplay;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MxFramework.Game.CharacterTest.TacticalCard
{
    // One transport fragment within the existing session envelope. The actor component remains the authority.
    internal sealed class CharacterTestPausedInteractionSnapshot
    {
        public int Runtime, Actor, Generation;
        public TacticalInteractionSnapshot State;
    }

    internal static partial class CharacterTestPausedSessionCodec
    {
        internal static JObject CaptureDefinitionConfiguration(TacticalCardDefinition definition)
        {
            // Json reads the common publication identity for both Action and Utility definitions.
            var result = JObject.FromObject(definition);
            if (definition is TacticalUtilityCardDefinition utility && utility.Payload is InteractionPreparationPayload preparation)
                result["InteractionEffect"] = (int)preparation.Effect; // Internal fixed discriminator is not a public JSON property.
            if (definition.FixedAssembly != null)
                result["FixedAssembly"] = new JObject {
                    ["FormatVersion"] = TacticalFixedAssemblyManifest.FormatVersion,
                    ["OrderingVersion"] = TacticalFixedAssemblyManifest.OrderingVersion,
                    ["Fingerprint"] = definition.FixedAssembly.Fingerprint };
            return result;
        }

        internal static string CaptureConfiguration(CharacterTestTacticalStageAuthoringDocument authoring, TacticalDeployment deployment)
            => JsonConvert.SerializeObject(new { Authoring = authoring,
                DefinitionIdentityCatalog = new { Revision = CharacterTestAuthoredContentIds.Revision,
                    Hash = CharacterTestAuthoredContentIds.CatalogHash },
                Cards = deployment.Participants.Select(participant => participant.Cards.Select(CaptureDefinitionConfiguration).ToArray()).ToArray() });

        internal static void ValidateConfigurationIdentity(string configuration)
        {
            JObject identity = null;
            if (configuration != null) {
                try { identity = StrictObject(configuration)["DefinitionIdentityCatalog"] as JObject; }
                catch (JsonException) { }
                catch (InvalidOperationException) { }
            }
            // Fail before a numeric reference can resolve against a different registry. Older private
            // carriers without this identity are rejected; a bare 45 must never acquire its new owner.
            SessionSnapshotCheck.Require(identity != null && identity.Count == 2 &&
                identity["Revision"]?.Type == JTokenType.Integer && identity["Hash"]?.Type == JTokenType.String &&
                (long)identity["Revision"] == CharacterTestAuthoredContentIds.Revision &&
                (string)identity["Hash"] == CharacterTestAuthoredContentIds.CatalogHash,
                "actual-definition-identity-mismatch");
        }

        internal static bool IsInteractionDefinition(TacticalCardDefinition definition)
        {
            if (!(definition is TacticalUtilityCardDefinition card) || !(card.Payload is InteractionPreparationPayload payload)) return false;
            int index = (int)payload.Effect - 1;
            return index >= 0 && index < 5 && (card.HasPublishedBehavior ?
                card.PublishedCardId == card.Id && card.PublishedRevision > 0 && !string.IsNullOrWhiteSpace(card.PublishedContentHash) :
                card.Id == TacticalInteractionTrialCards.Ids[index]) &&
                card.BaseMomentumCost == TacticalInteractionTrialCards.Cost && card.DestinationAfterUse == CardDestination.Exhaust;
        }

        internal static void ValidateDeployment(TacticalDeployment deployment)
        {
            SessionSnapshotCheck.Require(deployment != null && deployment.Participants.Count == 2 && deployment.PlayerParticipant != null,
                "ordinary-two-actor-deployment-required");
            var player = deployment.PlayerParticipant;
            bool ordinary = deployment.CardTrial == null;
            SessionSnapshotCheck.Require(player.Cards.All(card => card is TacticalActionCardDefinition action ?
                action.PointMove == null && action.PositionRecall == null : !ordinary && IsInteractionDefinition(card)),
                "ordinary-two-actor-deployment-required");
            if (ordinary) return;
            // Explicit bounded extension: one temporary Lab deck with the five compiled preparation kinds.
            var effects = player.Cards.Where(InteractionPreparationPayload.Matches).ToArray();
            SessionSnapshotCheck.Require(effects.Length > 0 && effects.Length <= 5 && effects.Select(card => card.Id).Distinct().Count() == effects.Length &&
                player.CharacterItemInstanceId == 0 && player.InventoryOwnerId == 0 && player.CharacterItemRevision == 0 &&
                deployment.Participants.All(p => p.CharacterItemInstanceId == 0 && p.InventoryOwnerId == 0 && p.CharacterItemRevision == 0) &&
                deployment.CardTrial.StageRunId > 0 && deployment.CardTrial.InitialHandCount == deployment.CardTrial.HandCapacity &&
                deployment.CardTrial.HandCapacity >= effects.Length && deployment.CardTrial.HandCapacity <= 16 && player.Cards.Count <= 32 &&
                deployment.Participants.Where(p => p != player).All(p => p.Cards.All(card => card is TacticalActionCardDefinition action &&
                    action.PointMove == null && action.PositionRecall == null)), "bounded-interaction-lab-deployment-required");
        }

        internal static int[] CaptureResourceOwnerIds(GameSlice slice)
        {
            var rows = slice.CharacterRuntime.World.CapturePausedMomentumOwners();
            SessionSnapshotCheck.Require(rows.Length > 0 && rows.All(row => row.Value is MomentumLedger &&
                slice.CharacterEntities.Any(actor => actor.Id.Value == row.Key)) && rows.Select(row => row.Value).Distinct().Count() == rows.Length,
                "actual-resource-owner-coverage");
            var mixed = slice.Services.Modules.MixedPlayerTacticalStage;
            SessionSnapshotCheck.Require(rows.Any(row => row.Key == mixed.Player.Id.Value && ReferenceEquals(row.Value, mixed.Stage.Ledger)),
                "actual-player-ledger-binding");
            return rows.Select(row => row.Key).ToArray();
        }

        internal static SessionMomentumSnapshot[] CaptureMomentum(GameSlice slice, CharacterTestPausedRestoreContext c)
        {
            SessionSnapshotCheck.Require(CaptureResourceOwnerIds(slice).SequenceEqual(c.ResourceOwners), "actual-resource-owner-set-changed");
            return c.ResourceOwners.Select(runtime => {
                var actor = c.Actors.Single(map => map.Runtime == runtime);
                SessionSnapshotCheck.Require(slice.CharacterRuntime.World.TryGetTacticalMotion(runtime, out var ledger) && ledger is MomentumLedger,
                    "actual-momentum-ledger-required");
                return new SessionMomentumSnapshot {Actor = actor.Index, Generation = actor.Generation, Ledger = ((MomentumLedger)ledger).CaptureSessionState()};
            }).ToArray();
        }

        internal static CharacterTestPausedInteractionSnapshot[] CaptureInteractions(GameSlice slice, CharacterTestPausedRestoreContext c)
            => c.Actors.Select(map => new CharacterTestPausedInteractionSnapshot {Runtime = map.Runtime, Actor = map.Index,
                Generation = map.Generation, State = slice.CharacterRuntime.FindEntityById(map.Runtime).InteractionEffects.Capture()}).ToArray();

        internal static void ValidateIncomingResourceOwners(CharacterTestPausedSessionDocument d, CharacterTestPausedRestoreContext c)
        {
            SessionSnapshotCheck.Require(d.Motion != null && d.Motion.Length == c.ResourceOwners.Length &&
                d.Motion.Select((row, index) => row != null && row.Actor == c.Actors.Single(actor => actor.Runtime == c.ResourceOwners[index]).Index &&
                    row.Generation == c.Actors.Single(actor => actor.Runtime == c.ResourceOwners[index]).Generation).All(valid => valid),
                "actual-momentum-owner-set");
            foreach (var row in d.Motion) new MomentumLedger().RestoreSessionState(row.Ledger);
            SessionSnapshotCheck.Require(!double.IsNaN(d.ElapsedSeconds) && !double.IsInfinity(d.ElapsedSeconds) && d.ElapsedSeconds >= 0,
                "actual-elapsed-clock");
        }

        // Pure join validation is also run by the offline tests; it never grants, spends or mutates a component.
        internal static void ValidateInteractionJoins(CharacterTestPausedInteractionSnapshot[] rows,
            CharacterTestPausedActorMap[] actors, TacticalDeckManager deck, TacticalStageSnapshot stage,
            long frame, int playerRuntime, MomentumSnapshot playerLedger, CharacterTestPausedRestoreContext context = null)
        {
            SessionSnapshotCheck.Require(rows != null && actors != null && rows.Length == actors.Length &&
                stage != null && stage.Time != null && playerLedger != null, "actual-interaction-owner-set");
            for (int i = 0; i < rows.Length; i++) {
                var row = rows[i]; var actor = actors[i];
                SessionSnapshotCheck.Require(row != null && actor != null && row.Runtime == actor.Runtime && row.Actor == actor.Index &&
                    row.Generation == actor.Generation && row.State != null, "actual-interaction-owner-set");
                var state = row.State;
                SessionSnapshotCheck.Require(TacticalInteractionEffectAuthority.ValidateSessionJoin(state, deck, stage.Stage, stage.Time.Turn, frame, actor.Gameplay,
                    context?.FixedAssemblyWitness(row.Runtime)?.Admission),
                    "actual-interaction-card-stage-join");
                SessionSnapshotCheck.Require(row.Runtime == playerRuntime ? state.Stage == stage.Stage && state.Turn == stage.Time.Turn : state.Stage == 0,
                    "actual-interaction-player-deck-owner");
                foreach (var slot in state.Slots) if (slot.Activation != 0 && slot.Target.IsValid)
                    SessionSnapshotCheck.Require(slot.Target != actor.Gameplay && actors.Any(map => map.Gameplay == slot.Target),
                        "actual-interaction-target-generation");
                foreach (var receipt in state.Receipts)
                    SessionSnapshotCheck.Require(receipt.Attacker != receipt.Target && (receipt.Attacker == actor.Gameplay || receipt.Target == actor.Gameplay) &&
                        actors.Any(map => map.Gameplay == receipt.Attacker) && actors.Any(map => map.Gameplay == receipt.Target) &&
                        receipt.ActionInstance >= 0 && receipt.Trace >= 0, "actual-interaction-receipt-identity");
                if (row.Runtime == playerRuntime) {
                    long paid = checked((long)state.NextActivation * TacticalInteractionTrialCards.Cost);
                    SessionSnapshotCheck.Require(paid <= deck.PaidCardMomentum && state.NextActivation <= playerLedger.NextToken,
                        "actual-interaction-payment-join");
                }
                var returning = state.Slots[3];
                SessionSnapshotCheck.Require((state.ReturnResult == TacticalReturnResult.None || returning.Activation != 0) &&
                    (!state.ReturnPending || frame <= returning.ExpiresFrame) &&
                    (state.ReturnResult == TacticalReturnResult.None || state.ReturnResult == TacticalReturnResult.WaitingForRelease ||
                        !returning.Active && returning.X == 0 && returning.Y == 0 && returning.Z == 0) &&
                    (returning.Activation == 0 || returning.Active || state.ReturnPending || state.ReturnResult != TacticalReturnResult.None || frame > returning.ExpiresFrame),
                    "actual-interaction-return-state");
                for (int kind = 0; kind < state.Slots.Length; kind++)
                    SessionSnapshotCheck.Require(!state.Slots[kind].Active || frame <= state.Slots[kind].ExpiresFrame,
                        "actual-interaction-unprocessed-expiry");
            }
        }

        private static void ValidateReturnCoordinates(GameSlice slice, CharacterTestPausedInteractionSnapshot[] rows)
        {
            foreach (var row in rows) {
                var slot = row.State.Slots[3];
                if (slot.Activation == 0 || (!slot.Active && !row.State.ReturnPending)) continue;
                SessionSnapshotCheck.Require(slice.PhysicsRuntime?.IsReady == true, "actual-interaction-return-physics-required");
                var shapes = new List<CombatPhysicsAabbCollider>(); slice.PhysicsRuntime.World.CopyAabbCollidersTo(shapes);
                var floor = shapes.Single(shape => shape.BodyId.Value == CharacterTestCharacterPhysicsRuntime.StaticBodyIdStart);
                var point = new FixVector3(Fix64.FromRatio(slot.X, 1000), Fix64.FromRatio(slot.Y, 1000), Fix64.FromRatio(slot.Z, 1000));
                // The existing fixed floor has no slopes or steps. Occupancy may change after arming;
                // dynamic clearance is deliberately left to the one real motion attempt after release.
                SessionSnapshotCheck.Require(point.X >= floor.LocalMin.X && point.X <= floor.LocalMax.X &&
                    point.Z >= floor.LocalMin.Z && point.Z <= floor.LocalMax.Z &&
                    (point.Y - floor.LocalMax.Y).Abs() <= Fix64.FromRatio(1, 1000), "actual-interaction-return-coordinate");
            }
        }

        private static void ValidateCurrentInteractionsAndResources(GameSlice slice, CharacterTestPausedRestoreContext c)
        {
            ValidateDeployment(c.Deployment);
            var mixed = slice.Services.Modules.MixedPlayerTacticalStage;
            var motion = CaptureMomentum(slice, c);
            var interactions = CaptureInteractions(slice, c);
            ValidateFixedAssemblyWitnesses(interactions, motion, mixed.Stage.Deck.CaptureSessionState(mixed.Stage.SnapshotDefinitions()), c, c.Frame);
            ValidateInteractionJoins(interactions, c.Actors, mixed.Stage.Deck, mixed.Stage.CaptureSessionState(), c.Frame,
                mixed.Player.Id.Value, motion.Single(row => row.Actor == mixed.Player.GameplayEntityId.Index && row.Generation == mixed.Player.GameplayEntityId.Generation).Ledger, c);
            ValidateReturnCoordinates(slice, interactions);
        }

        private static void RestoreInteractionsAndResources(GameSlice candidate, CharacterTestPausedSessionDocument d,
            CharacterTestPausedRestoreContext c, GameSlice previous)
        {
            var world = candidate.CharacterRuntime.World;
            var mixed = candidate.Services.Modules.MixedPlayerTacticalStage;
            foreach (var row in d.Motion) {
                var map = d.Actors.Single(actor => actor.Index == row.Actor && actor.Generation == row.Generation);
                MomentumLedger ledger;
                if (map.Runtime == d.PlayerRuntime) ledger = (MomentumLedger)mixed.Stage.Ledger;
                else { ledger = new MomentumLedger(); ledger.RestoreSessionState(row.Ledger); world.BindTacticalMotion(map.Runtime, ledger); }
                SessionSnapshotCheck.Require(!previous.CharacterRuntime.World.TryGetTacticalMotion(map.Runtime, out var old) || !ReferenceEquals(ledger, old),
                    "actual-fresh-ledger-alias");
            }
            var playerLedger = d.Motion.Single(row => row.Actor == mixed.Player.GameplayEntityId.Index && row.Generation == mixed.Player.GameplayEntityId.Generation).Ledger;
            ValidateInteractionJoins(d.Interactions, d.Actors, mixed.Stage.Deck, d.Stage, d.CompletedFrame, d.PlayerRuntime, playerLedger, c);
            ValidateReturnCoordinates(candidate, d.Interactions);
            foreach (var row in d.Interactions) {
                var actor = candidate.CharacterRuntime.FindEntityById(row.Runtime);
                var old = previous.CharacterRuntime.FindEntityById(row.Runtime);
                SessionSnapshotCheck.Require(actor != null && actor.InteractionEffects != null && !ReferenceEquals(actor.InteractionEffects, old?.InteractionEffects),
                    "actual-fresh-interaction-alias");
                SessionSnapshotCheck.Require(actor.InteractionEffects.RestoreJoined(row.State, mixed.Stage.Deck, mixed.Stage.StageRunId,
                    mixed.Stage.TurnIndex, d.CompletedFrame, actor.GameplayEntityId, c.FixedAssemblyWitness(row.Runtime)?.Admission), "actual-interaction-restore-join");
                var witness = c.FixedAssemblyWitness(row.Runtime);
                SessionSnapshotCheck.Require(witness == null || actor.InteractionEffects.TryBindConfirmedFixedAssembly(witness.Admission),
                    "actual-fixed-assembly-journal-bind");
            }
            // Bind sees the restored same stage/turn/actor and therefore does not clear one-shot slots/receipts.
            mixed.Stage.ConfigureInteractionEffects(candidate.CharacterRuntime, () => mixed.Player, () => mixed.CurrentTarget);
        }

        internal static Dictionary<int, TacticalFixedAssemblyRestoreWitness> CaptureFixedAssemblyWitnesses(GameSlice slice, CharacterTestPausedRestoreContext c)
        {
            var result = new Dictionary<int, TacticalFixedAssemblyRestoreWitness>();
            var mixed = slice.Services.Modules.MixedPlayerTacticalStage;
            foreach (var map in c.Actors) {
                var effects = slice.CharacterRuntime.FindEntityById(map.Runtime).InteractionEffects;
                if (!effects.HasFixedAssemblyState) continue;
                SessionSnapshotCheck.Require(map.Runtime == mixed.Player.Id.Value, "actual-fixed-assembly-ai-owner-unavailable");
                SessionSnapshotCheck.Require(slice.CharacterRuntime.World.TryGetTacticalMotion(map.Runtime, out var bound) && bound is MomentumLedger,
                    "actual-fixed-assembly-resource-owner");
                SessionSnapshotCheck.Require(TacticalFixedAssemblyRestoreWitness.TryCapture(effects, (MomentumLedger)bound, c.Frame,
                    mixed.Stage.Deck, mixed.Stage.SnapshotDefinitions(), out var witness), "actual-fixed-assembly-trusted-journal");
                for (int i = 0; i < witness.Admission.CommitCount; i++) {
                    var assembly = witness.Admission.CommitAt(i).Assembly;
                    SessionSnapshotCheck.Require(c.Scope == "lab" || assembly.CharacterItemInstance == map.Item && assembly.InventoryOwner == map.Owner
                        && assembly.CharacterRevision == map.Revision, "actual-fixed-assembly-item-owner");
                }
                result.Add(map.Runtime, witness);
            }
            return result;
        }

        private static void ValidateFixedAssemblyWitnesses(CharacterTestPausedInteractionSnapshot[] interactions,
            SessionMomentumSnapshot[] motion, TacticalDeckSnapshot deck, CharacterTestPausedRestoreContext c, long frame)
        {
            SessionSnapshotCheck.Require(interactions != null && motion != null && interactions.Length == c.Actors.Length, "actual-fixed-assembly-owner-set");
            SessionSnapshotCheck.Require(c.MatchesFixedAssemblyResources(motion), "actual-fixed-assembly-resource-witness");
            for (int i = 0; i < c.Actors.Length; i++) {
                var map = c.Actors[i]; var row = interactions[i]; var witness = c.FixedAssemblyWitness(map.Runtime);
                SessionSnapshotCheck.Require(row != null && row.Runtime == map.Runtime && row.Actor == map.Index && row.Generation == map.Generation && row.State != null,
                    "actual-fixed-assembly-owner-set");
                if (witness == null) {
                    SessionSnapshotCheck.Require(row.State.Accessories == null || row.State.Accessories.Activations != null && row.State.Accessories.Activations.Length == 0,
                        "actual-fixed-assembly-unwitnessed-activation");
                    continue;
                }
                var owners = motion.Where(value => value != null && value.Actor == map.Index && value.Generation == map.Generation).ToArray();
                SessionSnapshotCheck.Require(owners.Length == 1 && witness.Matches(row.State, owners[0].Ledger, deck, frame), "actual-fixed-assembly-barrier-witness");
            }
        }

        private static bool ValidateFixedAssemblyProjection(CharacterTestNetworkFixedAssemblySnapshot[] rows, CharacterTestPausedRestoreContext c, long frame)
        {
            return c.MatchesFixedAssemblyProjection(rows, frame);
        }

        // Low-frequency content integrity for this SAME private envelope, not a runtime hash contributor or definition identity.
        // No new formal schema/contributor/field ID is allocated. All authority fragments participate exactly once.
        internal static string ComputeContentHash(CharacterTestPausedSessionDocument document)
        {
            var root = JObject.FromObject(document); root.Remove(nameof(document.StateHash));
            foreach (string field in new[] {nameof(document.World), nameof(document.Network), nameof(document.PhysicsStatic), nameof(document.PhysicsWorld)})
                if (root[field]?.Type == JTokenType.String) root[field] = StrictObject((string)root[field]);
            var canonical = CanonicalContent(root).ToString(Formatting.None);
            using (var digest = SHA256.Create())
                return string.Concat(digest.ComputeHash(Encoding.UTF8.GetBytes(canonical)).Select(value => value.ToString("x2", System.Globalization.CultureInfo.InvariantCulture)));
        }

        private static JToken CanonicalContent(JToken token)
        {
            if (token is JObject obj) return new JObject(obj.Properties().OrderBy(property => property.Name, StringComparer.Ordinal)
                .Select(property => new JProperty(property.Name, CanonicalContent(property.Value))));
            if (token is JArray array) return new JArray(array.Select(CanonicalContent));
            // Provider custom payloads are serialized JSON strings; canonicalize their contents too.
            if (token.Parent is JProperty property && property.Name.EndsWith("payloadJson", StringComparison.OrdinalIgnoreCase) && token.Type == JTokenType.String)
                return new JValue(CanonicalContent(StrictObject((string)token)).ToString(Formatting.None));
            return token.DeepClone();
        }
    }
}
