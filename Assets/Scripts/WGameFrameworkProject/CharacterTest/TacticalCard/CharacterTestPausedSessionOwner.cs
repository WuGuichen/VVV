using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MxFramework.Gameplay;
using MxFramework.Runtime;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MxFramework.Game.CharacterTest.TacticalCard
{
    // Separate candidate envelope. It is not a registered save/network schema and has no disk entry point.
    internal sealed class CharacterTestPausedActorMap
    {
        public int Runtime, Index, Generation, Item, Owner, Participant;
        public long Revision;
        internal GameplayEntityId Gameplay => new GameplayEntityId(Index, Generation);
    }
    internal sealed class CharacterTestPausedSessionDocument
    {
        public string Format = "character-test.actual-paused-session.candidate";
        public int Version = 3;
        public string Capability = CharacterTestPausedPhysics.HeadlessCapability;
        public string Scope, Profile, Battle, ExternalEpoch, Config;
        public long CompletedFrame, NextFrame, NextCommandSequence;
        public double ElapsedSeconds;
        public int TickRate, NextRuntimeEntity, PlayerRuntime, TargetRuntime;
        public CharacterTestPausedActorMap[] Actors;
        public string World, Network, PhysicsStatic, PhysicsWorld;
        public SessionMomentumSnapshot[] Motion;
        public CharacterTestPausedInteractionSnapshot[] Interactions;
        public string StateHash;
        public TacticalDeckSnapshot Deck;
        public TacticalStageSnapshot Stage;
        public TacticalPumpSnapshot Controls;
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public TacticalIdleExecutionSnapshot ExecutionIdentity;
    }
    internal sealed class CharacterTestPausedRestoreContext
    {
        internal readonly string Scope, Profile, Battle, ExternalEpoch, Config;
        internal readonly long Frame;
        internal readonly int TickRate;
        internal readonly long AbilityHash;
        internal readonly string ActionDefinitions, EffectProviders, PhysicsStatic, PhysicsWorld;
        internal readonly int[] CostAttributes;
        internal readonly int[] ResourceOwners;
        internal readonly string[] AttributeLayouts;
        internal readonly TacticalDeployment Deployment;
        internal readonly CharacterTestPausedActorMap[] Actors;
        internal readonly Func<CharacterTestSliceOptions> CreateFreshOptions;
        internal readonly Func<bool> ExternalAuthorityIsCurrent;
        private readonly Dictionary<int, TacticalFixedAssemblyRestoreWitness> _fixedAssemblyWitnesses;
        private readonly string _fixedAssemblyResources, _fixedAssemblyProjection;
        private readonly string _idleExecutionIdentity;
        private readonly string _idleExecutionDeck;
        internal bool MatchesIdleExecutionIdentity(TacticalIdleExecutionSnapshot state)
            => _idleExecutionIdentity == JsonConvert.SerializeObject(state);
        internal bool MatchesIdleExecutionDeck(TacticalDeckSnapshot deck)
            => _idleExecutionDeck == null || _idleExecutionDeck == JsonConvert.SerializeObject(deck);
        internal TacticalFixedAssemblyRestoreWitness FixedAssemblyWitness(int runtime)
            => _fixedAssemblyWitnesses.TryGetValue(runtime, out var value) ? value : null;
        internal int FixedAssemblyWitnessCount => _fixedAssemblyWitnesses.Count;
        internal bool MatchesFixedAssemblyResources(SessionMomentumSnapshot[] rows)
            => _fixedAssemblyResources == null || _fixedAssemblyResources == JsonConvert.SerializeObject(rows);
        internal bool MatchesFixedAssemblyProjection(CharacterTestNetworkFixedAssemblySnapshot[] rows, long frame)
            => frame == Frame && _fixedAssemblyProjection == JsonConvert.SerializeObject(rows);
        internal CharacterTestPausedRestoreContext(GameSlice current, string scope, string profile, string battle,
            string externalEpoch, TacticalDeployment deployment, CharacterTestTacticalStageAuthoringDocument config,
            Func<CharacterTestSliceOptions> createFreshOptions, Func<bool> externalAuthorityIsCurrent)
        {
            SessionSnapshotCheck.Require(current != null && (scope == "profile" || scope == "lab") &&
                !string.IsNullOrWhiteSpace(profile) && !string.IsNullOrWhiteSpace(battle) && !string.IsNullOrWhiteSpace(externalEpoch) &&
                deployment != null && config != null && createFreshOptions != null && externalAuthorityIsCurrent != null,
                "actual-context");
            Scope = scope; Profile = profile; Battle = battle; ExternalEpoch = externalEpoch;
            Deployment = deployment; Config = CharacterTestPausedSessionCodec.CaptureConfiguration(config, deployment);
            Frame = current.Services.Modules.MixedPlayerTacticalStage?.Stage.LastCompletedFrame ?? -1;
            TickRate = current.CharacterRuntime.SimulationTickRate;
            AbilityHash = current.AbilityRuntime.Definitions.ComputeStableHash();
            ActionDefinitions = JsonConvert.SerializeObject(new { current.ActionRuntime.Registry.Actions, current.ActionRuntime.Registry.ActionSets });
            CreateFreshOptions = createFreshOptions; ExternalAuthorityIsCurrent = externalAuthorityIsCurrent;
            Actors = CharacterTestPausedSessionCodec.MapActors(current, this);
            ResourceOwners = CharacterTestPausedSessionCodec.CaptureResourceOwnerIds(current);
            _idleExecutionIdentity = JsonConvert.SerializeObject(current.Services.Modules.MixedPlayerTacticalStage.PausedAdapter.CaptureIdleExecutionIdentity());
            var mixed = current.Services.Modules.MixedPlayerTacticalStage;
            _idleExecutionDeck = _idleExecutionIdentity == "null" ? null :
                JsonConvert.SerializeObject(mixed.Stage.Deck.CaptureSessionState(mixed.Stage.SnapshotDefinitions()));
            _fixedAssemblyWitnesses = CharacterTestPausedSessionCodec.CaptureFixedAssemblyWitnesses(current, this);
            _fixedAssemblyResources = _fixedAssemblyWitnesses.Count == 0 ? null :
                JsonConvert.SerializeObject(CharacterTestPausedSessionCodec.CaptureMomentum(current, this));
            _fixedAssemblyProjection = JsonConvert.SerializeObject(current.Services.Network.CreateSnapshotParticipant().CaptureFixedAssemblyState(Frame));
            EffectProviders = CharacterTestPausedSessionCodec.EffectProviderShape(
                current.Services.Network.CreateSnapshotParticipant().CaptureRollbackDocument(new RuntimeFrame(Frame)));
            CostAttributes = current.AbilityRuntime.Definitions.CreateSnapshot().SelectMany(a => a.Costs)
                .Where(cost => cost != null && cost.Amount > 0 && cost.AttributeId != CharacterTestCharacterAttributeIds.Motion)
                .Select(cost => cost.AttributeId).Distinct().OrderBy(id => id).ToArray();
            AttributeLayouts = CharacterTestPausedSessionCodec.CaptureAttributeLayouts(current.CharacterRuntime.World.GameplayComponentWorld, Actors);
            PhysicsStatic = CharacterTestPausedPhysics.CaptureStatic(current.PhysicsRuntime);
            PhysicsWorld = CharacterTestPausedPhysics.CaptureWorld(current);
            CharacterTestPausedSessionCodec.ValidateCurrentFrame(current, this);
        }
    }

    internal static partial class CharacterTestPausedSessionCodec
    {
        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings {
            MissingMemberHandling = MissingMemberHandling.Error, TypeNameHandling = TypeNameHandling.None, MaxDepth = 64 };
        internal static JObject StrictObject(string json)
        {
            SessionSnapshotCheck.Require(json != null && json.Length <= 16 * 1024 * 1024, "actual-document-size");
            using (var text = new StringReader(json)) using (var reader = new JsonTextReader(text) { MaxDepth = 64 }) {
                var value = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                SessionSnapshotCheck.Require(!reader.Read(), "actual-trailing-document"); return value;
            }
        }
        internal static CharacterTestPausedSessionDocument Read(string json)
        {
            var root = StrictObject(json);
            var fields = typeof(CharacterTestPausedSessionDocument).GetFields(BindingFlags.Public | BindingFlags.Instance);
            if (root.Property(nameof(CharacterTestPausedSessionDocument.ExecutionIdentity)) == null)
                fields = fields.Where(f => f.Name != nameof(CharacterTestPausedSessionDocument.ExecutionIdentity)).ToArray();
            SessionSnapshotCheck.Require(root.Properties().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal)
                .SequenceEqual(fields.Select(f => f.Name).OrderBy(n => n, StringComparer.Ordinal)), "actual-root-capability-required");
            ValidateShape(root, typeof(CharacterTestPausedSessionDocument));
            var d = root.ToObject<CharacterTestPausedSessionDocument>(JsonSerializer.Create(Settings));
            SessionSnapshotCheck.Require(d.Format == "character-test.actual-paused-session.candidate" && d.Version == 3 &&
                (d.Capability == CharacterTestPausedPhysics.HeadlessCapability || d.Capability == CharacterTestPausedPhysics.FloorCapability), "actual-capability");
            SessionSnapshotCheck.Require(JToken.DeepEquals(root, JObject.FromObject(d)), "actual-document-roundtrip");
            return d;
        }
        private static void ValidateShape(JToken token, Type type)
        {
            if (token.Type == JTokenType.Null) { SessionSnapshotCheck.Require(!type.IsValueType, "actual-null-value"); return; }
            if (type == typeof(string)) { SessionSnapshotCheck.Require(token.Type == JTokenType.String, "actual-string-type"); return; }
            if (type == typeof(bool)) { SessionSnapshotCheck.Require(token.Type == JTokenType.Boolean, "actual-boolean-type"); return; }
            if (type == typeof(float) || type == typeof(double)) {
                SessionSnapshotCheck.Require(token.Type == JTokenType.Float || token.Type == JTokenType.Integer, "actual-float-type"); return;
            }
            if (type.IsPrimitive || type.IsEnum) { SessionSnapshotCheck.Require(token.Type == JTokenType.Integer, "actual-integer-type"); return; }
            if (type.IsArray) { SessionSnapshotCheck.Require(token is JArray, "actual-array-type");
                foreach (var child in (JArray)token) ValidateShape(child, type.GetElementType()); return; }
            var value = token as JObject;
            var members = type.GetFields(BindingFlags.Public | BindingFlags.Instance).Select(f => new { f.Name, Type = f.FieldType })
                .Concat(type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.CanRead && p.GetIndexParameters().Length == 0)
                    .Select(p => new { p.Name, Type = p.PropertyType })).ToArray();
            // Preserve the legacy envelope when the explicitly optional accessory fragment is absent.
            // All other fields remain required; present fragments still receive full recursive shape validation.
            if (type == typeof(TacticalInteractionSnapshot) && value != null
                && value.Property(nameof(TacticalInteractionSnapshot.Accessories)) == null)
                members = members.Where(m => m.Name != nameof(TacticalInteractionSnapshot.Accessories)).ToArray();
            if (type == typeof(CharacterTestPausedSessionDocument) && value != null
                && value.Property(nameof(CharacterTestPausedSessionDocument.ExecutionIdentity)) == null)
                members = members.Where(m => m.Name != nameof(CharacterTestPausedSessionDocument.ExecutionIdentity)).ToArray();
            SessionSnapshotCheck.Require(value != null && value.Properties().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal)
                .SequenceEqual(members.Select(m => m.Name).OrderBy(n => n, StringComparer.Ordinal)), "actual-fragment-fields");
            foreach (var member in members) ValidateShape(value[member.Name], member.Type);
        }
        internal static CharacterTestPausedActorMap[] MapActors(GameSlice slice, CharacterTestPausedRestoreContext c)
        {
            SessionSnapshotCheck.Require(c.Deployment.Participants.Count == 2 && slice.CharacterEntities.Count == 2, "actual-two-actors");
            var seen = new HashSet<int>();
            var map = new CharacterTestPausedActorMap[2];
            for (int i = 0; i < 2; i++) {
                var p = c.Deployment.Participants[i];
                var matches = slice.CharacterEntities.Where(e => e.Identity.CharacterId == p.CharacterId && e.Team.TeamId == p.TeamId &&
                    (e.Control.Owner == CharacterTestCharacterControlOwner.LocalPlayer) == p.IsPlayerControlled).ToArray();
                SessionSnapshotCheck.Require(matches.Length == 1 && seen.Add(matches[0].Id.Value), "runtime-deployment-identity");
                SessionSnapshotCheck.Require(c.Scope == "lab" ? p.CharacterItemInstanceId == 0 && p.InventoryOwnerId == 0 && p.CharacterItemRevision == 0 :
                    p.CharacterItemInstanceId > 0 && p.InventoryOwnerId > 0 && p.CharacterItemRevision > 0, "item-owner-revision-required");
                var entity = matches[0];
                map[i] = new CharacterTestPausedActorMap { Runtime = entity.Id.Value, Index = entity.GameplayEntityId.Index,
                    Generation = entity.GameplayEntityId.Generation, Item = p.CharacterItemInstanceId, Owner = p.InventoryOwnerId,
                    Revision = p.CharacterItemRevision, Participant = i };
            }
            SessionSnapshotCheck.Require(map.Select(m => m.Gameplay).Distinct().Count() == 2 &&
                (c.Scope == "lab" || map.Select(m => m.Item).Distinct().Count() == 2), "duplicate-actor-item");
            return map;
        }
        internal static void ValidateCurrentFrame(GameSlice slice, CharacterTestPausedRestoreContext c)
        {
            slice.ThrowIfAuthorityUnavailable();
            var mixed = slice.Services.Modules.MixedPlayerTacticalStage;
            SessionSnapshotCheck.Require(mixed != null && slice.Services.Modules.TacticalStage == null,
                "actual-mixed-provider-required");
            SessionSnapshotCheck.Require(!slice.Host.IsTickInProgress && slice.CharacterRuntime.TryGetLastCompletedRuntimeFrame(out var completed) &&
                completed.Value == c.Frame && mixed.Stage.LastCompletedFrame == c.Frame && slice.CurrentFrame.Value == checked(c.Frame + 1),
                "current-whole-frame-barrier"); // W1: checked before ANY restore factory, including after another complete turn.
            mixed.ValidatePausedRestoreBoundary();
            SessionSnapshotCheck.Require(c.MatchesIdleExecutionIdentity(mixed.PausedAdapter.CaptureIdleExecutionIdentity()),
                "current-idle-execution-history-changed");
            SessionSnapshotCheck.Require(c.MatchesIdleExecutionDeck(mixed.Stage.Deck.CaptureSessionState(mixed.Stage.SnapshotDefinitions())),
                "current-idle-execution-deck-changed");
            SessionSnapshotCheck.Require(CharacterTestPausedPhysics.CaptureStatic(slice.PhysicsRuntime) == c.PhysicsStatic &&
                CharacterTestPausedPhysics.CaptureWorld(slice) == c.PhysicsWorld, "current-physics-state-changed");
            SessionSnapshotCheck.Require(c.ExternalAuthorityIsCurrent(), "external-owner-revision-or-epoch-changed");
            SessionSnapshotCheck.Require(slice.AbilityRuntime.Definitions.ComputeStableHash() == c.AbilityHash &&
                JsonConvert.SerializeObject(new { slice.ActionRuntime.Registry.Actions, slice.ActionRuntime.Registry.ActionSets }) == c.ActionDefinitions,
                "current-action-or-ability-definitions-changed");
            var story = slice.StoryDirector.CreateSnapshot();
            SessionSnapshotCheck.Require(slice.PendingRuntimeCommandCount == 0 && slice.StoryModule.CommandBuffer.PendingCount == 0 &&
                story.ActiveBeatInstances.Count == 0 && story.Facts.Count == 0 && story.NextBeatInstanceId == 1 &&
                story.Graphs.All(g => g.Status == MxFramework.Story.StoryGraphRuntimeStatus.Loaded), "external-story-or-world-commands-active");
            SessionSnapshotCheck.Require(slice.CharacterRuntime.World.GameplayComponentWorld.PendingEventCount == 0,
                "gameplay-events-active");
            var map = MapActors(slice, c);
            SessionSnapshotCheck.Require(SameMap(map, c.Actors), "current-identity-map-changed");
            // Restore must prove the current world's idle boundary too, not just the incoming document.
            var network = slice.Services.Network.CreateSnapshotParticipant().CaptureRollbackDocument(new RuntimeFrame(c.Frame));
            SessionSnapshotCheck.Require(ValidateFixedAssemblyProjection(network.FixedAssemblyState, c, c.Frame), "current-fixed-assembly-projection-changed");
            SessionSnapshotCheck.Require(EffectProviderShape(network) == c.EffectProviders, "actual-effect-provider-shape");
            ValidateInactiveNetwork(network);
            ValidateWorldJoins(slice.CharacterRuntime.World.GameplayComponentWorld, network, map, c);
            mixed.Stage.CaptureSessionState();
            ValidateCurrentInteractionsAndResources(slice, c);
        }
        internal static bool SameMap(CharacterTestPausedActorMap[] a, CharacterTestPausedActorMap[] b) => a != null && b != null &&
            a.Length == b.Length && a.Zip(b, (x,y) => x != null && y != null && x.Runtime == y.Runtime && x.Index == y.Index &&
                x.Generation == y.Generation && x.Item == y.Item && x.Owner == y.Owner && x.Revision == y.Revision && x.Participant == y.Participant).All(v => v);

        // Existing serializers are registered on a facade over the registry, never into the live world's schema table.
        internal static GameplayComponentWorld ProviderWorld(GameplayComponentWorld actual)
        {
            var schemas = new GameplayComponentSchemaRegistry();
            GameplayCoreComponentSchemaDescriptors.RegisterSaveState(schemas);
            GameplayAttributeComponentSchemaDescriptors.RegisterSaveState(schemas);
            GameplayComponentBuffSchemaDescriptors.RegisterSaveState(schemas);
            GameplayComponentModifierSchemaDescriptors.RegisterSaveState(schemas);
            GameplayComponentAuraSourceSchemaDescriptors.RegisterSaveState(schemas);
            GameplayAbilityCooldownComponentSchemaDescriptors.RegisterSaveState(schemas);
            return new GameplayComponentWorld(actual.Registry, actual.Events, schemas);
        }
        internal static string CaptureWorld(GameplayComponentWorld world, long frame)
        {
            var result = new GameplayComponentWorldSaveStateProvider(ProviderWorld(world), () => frame).CaptureSaveState();
            SessionSnapshotCheck.Require(result.Success, "actual-world-capture");
            var payload = StrictObject(result.Value.ModuleStates.Single().CustomState.PayloadJson);
            SessionSnapshotCheck.Require(((JArray)payload["componentStores"]).Count == world.StoreCount, "unknown-world-store");
            // Buff/modifier/aura collections would introduce active-effect restore semantics outside this increment.
            foreach (var store in (JArray)payload["componentStores"]) {
                string id = (string)store["schemaId"];
                if (id != null && (id.Contains("buff") || id.Contains("modifier") || id.Contains("aura")))
                    SessionSnapshotCheck.Require(!((JContainer)store["entries"]).DescendantsAndSelf().OfType<JArray>().Any(a => a.Count > 0), "active-gameplay-effect");
            }
            // This private paused-session transport has no wall-clock capture identity.
            // Preserve every authority field; make the provider's diagnostic timestamp deterministic.
            var captured = result.Value;
            var stable = new RuntimeSaveState(captured.SchemaVersion, new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                captured.FrameworkVersion, captured.ConfigVersion, captured.ResourceCatalogVersion, captured.Frame,
                captured.Entities, captured.GlobalCounters, captured.ModuleStates, captured.Metadata);
            return RuntimeSaveStateJson.SaveToJson(stable);
        }
        internal static void ValidateInactiveNetwork(CharacterTestNetworkSnapshotDocument n)
        {
            SessionSnapshotCheck.Require(n != null && (n.SchemaVersion == CharacterTestNetworkSnapshotParticipant.SchemaVersionValue ||
                n.SchemaVersion == CharacterTestNetworkSnapshotParticipant.PositionRecallSchemaVersionValue && n.FixedAssemblyState == null) &&
                n.Characters != null && n.Characters.Length == 2 && n.MovableNaturalObjects != null && n.MovableNaturalObjects.Length == 0 &&
                n.Characters.All(r => r != null && r.Health > 0 && r.ActionState != CharacterTestActionStateId.Running &&
                    r.ActionRunnerActiveInstanceId == 0 && r.HitStunFramesRemaining == 0 &&
                    r.InputWorldMoveXMilli == 0 && r.InputWorldMoveYMilli == 0 &&
                    r.InputMoveXMilli == 0 && r.InputMoveYMilli == 0 && !r.JumpPressed && !r.AttackPrimaryPressed && !r.AttackSecondaryPressed &&
                    !r.DodgePressed && r.IsGrounded), "active-character-or-object");
            foreach (var value in new object[] { n.Projectiles, n.HookPulls, n.ArcMines, n.RuneTurrets,
                n.AreaConfinement, n.PoisonBottlePools, n.PhysicalShields }) {
                SessionSnapshotCheck.Require(value == null || !JObject.FromObject(value).Descendants().OfType<JArray>().Any(a => a.Count != 0),
                    "active-effect-provider-required");
            }
            SessionSnapshotCheck.Require(n.Ownerships != null && n.Ownerships.Length == 0 && string.IsNullOrEmpty(n.LocalPeerId),
                "network-peer-provider-required");
            SessionSnapshotCheck.Require(n.RuntimeAiPlannerState != null && !n.RuntimeAiPlannerState.HasActiveBehaviorBindings(),
                "active-ai-behavior-provider-required");
        }
        internal static void ValidateWorldJoins(GameplayComponentWorld world, CharacterTestNetworkSnapshotDocument n,
            CharacterTestPausedActorMap[] map, CharacterTestPausedRestoreContext c)
        {
            SessionSnapshotCheck.Require(n.AbilityRuntime != null && n.AbilityRuntime.DefinitionHash == c.AbilityHash,
                "actual-ability-definition-version");
            SessionSnapshotCheck.Require(world.TryGetStore<GameplayAttributeSetComponent>(out var attributes), "actual-hp-projection-join:attribute-store-required");
            foreach (var row in n.Characters) {
                var actor = map.Single(m => m.Runtime == row.EntityId);
                SessionSnapshotCheck.Require(attributes.TryGet(actor.Gameplay, out var set) &&
                    set.TryGet(CharacterTestCharacterAttributeIds.Hp, out var hp) && hp.CurrentValue == row.Health && hp.BaseValue == row.MaxHealth,
                    "actual-hp-projection-join");
                SessionSnapshotCheck.Require(set.TryGet(CharacterTestCombatEvadeRuntime.StaminaAttributeId, out var stamina) &&
                    stamina.CurrentValue == row.CombatEvadeStamina, "actual-stamina-projection-join");
                foreach (int attributeId in c.CostAttributes)
                    SessionSnapshotCheck.Require(set.TryGet(attributeId, out _), "actual-fee-resource-required:" + attributeId);
                SessionSnapshotCheck.Require(AttributeLayout(set) == c.AttributeLayouts[actor.Participant],
                    "actual-unknown-or-missing-attribute-layout");
            }
        }
        private static string AttributeLayout(GameplayAttributeSetComponent set) => string.Join(",", set.ToArray()
            .OrderBy(v => v.AttributeId).Select(v => v.AttributeId + ":" + v.BaseValue));
        internal static string[] CaptureAttributeLayouts(GameplayComponentWorld world, CharacterTestPausedActorMap[] map)
        {
            SessionSnapshotCheck.Require(world.TryGetStore<GameplayAttributeSetComponent>(out var attributes), "actual-attribute-store-required");
            return map.Select(actor => {
                SessionSnapshotCheck.Require(attributes.TryGet(actor.Gameplay, out var set), "actual-actor-attributes-required");
                return AttributeLayout(set);
            }).ToArray();
        }
        internal static string EffectProviderShape(CharacterTestNetworkSnapshotDocument n) => string.Join(",",
            new object[] { n.Projectiles, n.HookPulls, n.ArcMines, n.RuneTurrets, n.AreaConfinement, n.PoisonBottlePools, n.PhysicalShields }
                .Select(value => value == null ? "absent" : value.GetType().FullName));
        internal static CharacterTestPausedSessionDocument Capture(GameSlice slice, CharacterTestPausedRestoreContext c)
        {
            ValidateCurrentFrame(slice, c);
            var mixed = slice.Services.Modules.MixedPlayerTacticalStage;
            var network = slice.Services.Network.CreateSnapshotParticipant().CaptureRollbackDocument(new RuntimeFrame(c.Frame));
            SessionSnapshotCheck.Require(EffectProviderShape(network) == c.EffectProviders, "actual-effect-provider-shape");
            ValidateInactiveNetwork(network);
            ValidateWorldJoins(slice.CharacterRuntime.World.GameplayComponentWorld, network, c.Actors, c);
            var document = new CharacterTestPausedSessionDocument {
                Capability = c.PhysicsStatic == null ? CharacterTestPausedPhysics.HeadlessCapability : CharacterTestPausedPhysics.FloorCapability,
                PhysicsStatic = c.PhysicsStatic, PhysicsWorld = c.PhysicsWorld, Scope = c.Scope, Profile = c.Profile, Battle = c.Battle,
                ExternalEpoch = c.ExternalEpoch, Config = c.Config, CompletedFrame = c.Frame, NextFrame = checked(c.Frame + 1),
                TickRate = c.TickRate, ElapsedSeconds = slice.ElapsedSeconds, Actors = MapActors(slice, c),
                NextRuntimeEntity = slice.CharacterRuntime.World.PausedNextRuntimeEntityId,
                NextCommandSequence = slice.RuntimeCommands.CommandBuffer.CaptureSnapshot().NextSequence,
                PlayerRuntime = mixed.Player.Id.Value, TargetRuntime = mixed.CurrentTarget.Id.Value,
                Network = JsonConvert.SerializeObject(network), World = CaptureWorld(slice.CharacterRuntime.World.GameplayComponentWorld, c.Frame),
                Motion = CaptureMomentum(slice, c), Interactions = CaptureInteractions(slice, c), Deck = mixed.Stage.Deck.CaptureSessionState(mixed.Stage.SnapshotDefinitions()),
                Stage = mixed.Stage.CaptureSessionState(), Controls = mixed.Commands.CaptureSessionState(),
                ExecutionIdentity = mixed.PausedAdapter.CaptureIdleExecutionIdentity() };
            document.StateHash = ComputeContentHash(document);
            return document;
        }
        internal static GameSlice Prepare(CharacterTestPausedSessionDocument d, CharacterTestPausedRestoreContext c, GameSlice previous)
        {
            ValidateConfigurationIdentity(d.Config);
            ValidateConfigurationIdentity(c.Config);
            SessionSnapshotCheck.Require(d.Scope == c.Scope && d.Profile == c.Profile && d.Battle == c.Battle && d.ExternalEpoch == c.ExternalEpoch &&
                d.Config == c.Config && d.CompletedFrame == c.Frame && d.NextFrame == checked(c.Frame + 1) && d.TickRate == c.TickRate &&
                d.NextCommandSequence >= 0 && SameMap(d.Actors, c.Actors), "actual-context-mismatch");
            SessionSnapshotCheck.Require(d.Capability == (c.PhysicsStatic == null ? CharacterTestPausedPhysics.HeadlessCapability : CharacterTestPausedPhysics.FloorCapability) &&
                d.PhysicsStatic == c.PhysicsStatic && d.PhysicsWorld == c.PhysicsWorld, "actual-physics-document-mismatch");
            var networkRoot = StrictObject(d.Network);
            var network = networkRoot.ToObject<CharacterTestNetworkSnapshotDocument>(JsonSerializer.Create(Settings));
            // Require every serialized current-schema property. No NormalizeForSchema may fill omitted authority fields.
            SessionSnapshotCheck.Require(JToken.DeepEquals(networkRoot, JObject.FromObject(network)), "actual-network-complete-document");
            SessionSnapshotCheck.Require(EffectProviderShape(network) == c.EffectProviders, "actual-effect-provider-shape");
            ValidateInactiveNetwork(network);
            SessionSnapshotCheck.Require(network.Frame == c.Frame && network.Characters.All(row => d.Actors.Any(m => m.Runtime == row.EntityId)) &&
                d.Stage != null && d.Stage.LastFrame == c.Frame && d.Stage.Player == d.Actors.Single(m => m.Runtime == d.PlayerRuntime).Index &&
                d.Stage.PlayerGeneration == d.Actors.Single(m => m.Runtime == d.PlayerRuntime).Generation &&
                d.Stage.Opponent == d.Actors.Single(m => m.Runtime == d.TargetRuntime).Index &&
                d.Stage.OpponentGeneration == d.Actors.Single(m => m.Runtime == d.TargetRuntime).Generation, "actual-world-stage-join");
            StrictObject(d.World);
            var worldSave = RuntimeSaveStateJson.LoadFromJson(d.World);
            SessionSnapshotCheck.Require(worldSave.Success && worldSave.Value.Frame == c.Frame, "actual-world-payload");
            SessionSnapshotCheck.Require(worldSave.Value.ModuleStates.Count == 1 &&
                worldSave.Value.ModuleStates[0].ModuleId == GameplayComponentWorldSaveStateProvider.ModuleId, "actual-world-module");
            StrictObject(worldSave.Value.ModuleStates[0].CustomState.PayloadJson);
            ValidateIncomingResourceOwners(d, c);
            SessionSnapshotCheck.Require(c.MatchesIdleExecutionIdentity(d.ExecutionIdentity), "actual-idle-execution-history-witness");
            SessionSnapshotCheck.Require(c.MatchesIdleExecutionDeck(d.Deck), "actual-idle-execution-deck-witness");
            ValidateFixedAssemblyWitnesses(d.Interactions, d.Motion, d.Deck, c, d.CompletedFrame);
            SessionSnapshotCheck.Require(ValidateFixedAssemblyProjection(network.FixedAssemblyState, c, d.CompletedFrame), "actual-fixed-assembly-network-witness");
            var options = c.CreateFreshOptions();
            SessionSnapshotCheck.Require(options != null && options.Resources == null && options.AudioService == null && !options.CreateCharacterViews &&
                !options.EnableStoryBootstrap && !options.EnableLocalInputCommands && !options.NavigationBuildInput.HasValue &&
                options.TacticalStage == null && options.MixedPlayerTacticalStage != null &&
                ReferenceEquals(options.TacticalDeployment, c.Deployment) &&
                CaptureConfiguration(options.MixedPlayerTacticalStage, options.TacticalDeployment) == c.Config &&
                options.SimulationTickRate == c.TickRate && options.SubscribeBeforeStoryBootstrap == null && !options.EnableTraceFileWrite &&
                options.StartupCharacterSpawnSpecs == null, "fresh-no-views-options-required");
            CharacterTestPausedPhysics.ValidateFresh(options.PhysicsRuntime, previous.PhysicsRuntime, c.PhysicsStatic);
            options.DetachedTacticalRestore = true;
            // Invoke runtime factories once and check aliasing before their constructors can bind any cost authority.
            var ability = options.FreshAbilityRuntime?.Invoke() ?? CharacterTestAbilityCatalogSource.CreateRuntimeModule();
            var action = options.FreshActionRuntime?.Invoke();
            SessionSnapshotCheck.Require(ability != null && !ReferenceEquals(ability, previous.AbilityRuntime) &&
                ability.Definitions.ComputeStableHash() == c.AbilityHash &&
                (action == null || !ReferenceEquals(action, previous.ActionRuntime)), "fresh-runtime-factory-alias");
            options.FreshAbilityRuntime = () => ability;
            options.FreshActionRuntime = () => action;
            GameSlice candidate = null;
            try {
                candidate = new GameSlice(options);
                SessionSnapshotCheck.Require(JsonConvert.SerializeObject(new { candidate.ActionRuntime.Registry.Actions,
                    candidate.ActionRuntime.Registry.ActionSets }) == c.ActionDefinitions, "actual-action-definition-version");
                var coldNetwork = candidate.Services.Network.CreateSnapshotParticipant().CaptureRollbackDocument(RuntimeFrame.Zero);
                SessionSnapshotCheck.Require(EffectProviderShape(coldNetwork) == c.EffectProviders, "actual-fresh-effect-provider-shape");
                var world = candidate.CharacterRuntime.World.GameplayComponentWorld;
                var restored = new GameplayComponentWorldSaveStateProvider(ProviderWorld(world)).RestoreSaveState(worldSave.Value);
                SessionSnapshotCheck.Require(restored.Success && world.CountAlive == d.Actors.Length && d.Actors.All(m => world.IsAlive(m.Gameplay)),
                    "actual-allocation-identity");
                ValidateWorldJoins(world, network, d.Actors, c);
                candidate.CharacterRuntime.ImportPausedCharacterProjection(network, d.Actors, d.NextRuntimeEntity, c.Frame);
                var applied = candidate.Services.Network.CreateSnapshotParticipant().ApplyRollbackDocument(new RuntimeFrame(c.Frame), network,
                    preserveLocalPredictedEntity: false,
                    frozenPausedPreflight: (rows, frame) => ValidateFixedAssemblyProjection(rows, c, frame));
                SessionSnapshotCheck.Require(applied.Success, "actual-network-import:" + applied.Message);
                SessionSnapshotCheck.Require(CharacterTestPausedPhysics.CaptureWorld(candidate) == c.PhysicsWorld, "actual-restored-physics-projection");
                candidate.RestoreDetachedPausedClock(c.Frame, d.ElapsedSeconds);
                candidate.RuntimeCommands.CommandBuffer.RestoreSnapshot(new RuntimeCommandBufferSnapshot(new RuntimeFrame(d.NextFrame),
                    d.NextCommandSequence, Array.Empty<RuntimeCommand>()));
                candidate.Services.Modules.MixedPlayerTacticalStage.RestorePausedMixedState(d);
                RestoreInteractionsAndResources(candidate, d, c, previous);
                SessionSnapshotCheck.Require(SameMap(MapActors(candidate, c), c.Actors), "actual-restored-identity");
                CaptureWorld(world, c.Frame); // Unknown stores or missing provider coverage still reject before publication.
                // Check integrity after typed semantic checks, retaining their precise rejection reasons.
                SessionSnapshotCheck.Require(d.StateHash == ComputeContentHash(d), "actual-session-content-hash");
                SessionSnapshotCheck.Require(MatchesRestoredContent(candidate, c, d), "actual-restored-complete-state-hash");
                return candidate;
            } catch { candidate?.Dispose(); throw; }
        }

        // A schema-40 empty projection keeps its original content hash during validation. New captures use 41.
        // This compatibility projection cannot erase a witnessed or transported fixed assembly fragment.
        internal static bool MatchesRestoredContent(GameSlice candidate, CharacterTestPausedRestoreContext c, CharacterTestPausedSessionDocument source)
        {
            var actual = Capture(candidate, c);
            var previousNetwork = JsonConvert.DeserializeObject<CharacterTestNetworkSnapshotDocument>(source.Network);
            if (previousNetwork.SchemaVersion == CharacterTestNetworkSnapshotParticipant.PositionRecallSchemaVersionValue
                && previousNetwork.FixedAssemblyState == null && c.FixedAssemblyWitnessCount == 0) {
                var network = JsonConvert.DeserializeObject<CharacterTestNetworkSnapshotDocument>(actual.Network);
                if (network.FixedAssemblyState != null) return false;
                network.SchemaVersion = previousNetwork.SchemaVersion;
                actual.Network = JsonConvert.SerializeObject(network);
                actual.StateHash = ComputeContentHash(actual);
            }
            return actual.StateHash == source.StateHash;
        }
    }

    /// <summary>One aggregate reference for runtime world, actual Mixed adapter, Host/clock and UI lease.</summary>
    internal sealed class CharacterTestPausedSessionOwner : IDisposable
    {
        private readonly CharacterTestPausedRestoreContext _context;
        private readonly List<GameSlice> _retired = new List<GameSlice>();
        private bool _restoring, _disposed;
        private readonly Action<GameSlice> _beforeHostStart;
        internal event Action<GameSlice, ulong> Published;
        internal Exception LastPublicationError { get; private set; }
        internal GameSlice Current { get; private set; }
        internal ulong Lease { get; private set; } = 1;
        // Cold activation wiring runs while the candidate Host is still Created; publication observers cannot roll back a committed session.
        internal CharacterTestPausedSessionOwner(GameSlice current, CharacterTestPausedRestoreContext context, Action<GameSlice> beforeHostStart = null)
        { Current = current ?? throw new ArgumentNullException(nameof(current)); _context = context ?? throw new ArgumentNullException(nameof(context));
            _beforeHostStart = beforeHostStart;
            CharacterTestPausedSessionCodec.ValidateCurrentFrame(current, context); }
        internal string Capture() => JsonConvert.SerializeObject(CharacterTestPausedSessionCodec.Capture(Current, _context));
        internal bool TryRestore(string json, out string issue)
        {
            issue = string.Empty;
            if (_restoring || _disposed) { issue = "snapshot:actual-owner-unavailable"; return false; }
            _restoring = true; GameSlice candidate = null;
            try {
                try {
                    // W1 must run before parsing/calling a factory or starting any candidate Host.
                    CharacterTestPausedSessionCodec.ValidateCurrentFrame(Current, _context);
                    SessionSnapshotCheck.Require(Lease < ulong.MaxValue, "actual-lease-overflow");
                    var document = CharacterTestPausedSessionCodec.Read(json);
                    candidate = CharacterTestPausedSessionCodec.Prepare(document, _context, Current);
                    CharacterTestPausedSessionCodec.ValidateCurrentFrame(Current, _context);
                    _beforeHostStart?.Invoke(candidate);
                    candidate.StartDetachedPausedHost();
                    SessionSnapshotCheck.Require(candidate.Host.State == RuntimeLifecycleState.Started, "actual-host-start-failed");
                    candidate.Services.Modules.MixedPlayerTacticalStage.ValidatePausedRestoreBoundary();
                    SessionSnapshotCheck.Require(CharacterTestPausedSessionCodec.MatchesRestoredContent(candidate, _context, document),
                        "actual-started-complete-state-hash");
                    CharacterTestPausedSessionCodec.ValidateCurrentFrame(Current, _context);
                    _retired.Add(Current); // Any allocation failure occurs before publication.
                } catch (Exception e) {
                    issue = e.Message;
                    try { candidate?.Dispose(); } catch (Exception cleanup) { issue += "; candidate-cleanup:" + cleanup.Message; }
                    return false;
                }
                var previous = Current;
                previous.Services.Modules.MixedPlayerTacticalStage.PausedWorldTime.RelinquishGlobalTime();
                Current = candidate; candidate = null; Lease++; // Committed reference/lease boundary.
                var errors = new List<Exception>();
                Action<Action> attempt = action => { try { action(); } catch (Exception e) { errors.Add(e); } };
                attempt(() => Current.Services.Modules.MixedPlayerTacticalStage.PausedWorldTime.ClaimGlobalTime(
                    previous.Services.Modules.MixedPlayerTacticalStage.PausedWorldTime.ShutdownRestoreScale));
                attempt(() => previous.SuspendAuthority(previous.CurrentFrame, new InvalidOperationException("Retired paused-session lease.")));
                attempt(() => previous.Services.Modules.MixedPlayerTacticalStage.Stage.ReleaseControl());
                var observers = Published;
                if (observers != null) foreach (Action<GameSlice, ulong> observer in observers.GetInvocationList())
                    attempt(() => observer(Current, Lease));
                LastPublicationError = errors.Count == 0 ? null : new AggregateException("Paused session committed; post-publication operation failed.", errors);
                issue = LastPublicationError?.Message ?? string.Empty;
                return true; // A notification/retirement failure never reports that the old root was preserved.
            } finally { _restoring = false; }
        }
        internal TacticalStageRejection Apply(ulong lease, in TacticalStageCommand command)
        {
            if (_disposed || _restoring || lease != Lease) return TacticalStageRejection.StaleSnapshot;
            var mixed = Current.Services.Modules.MixedPlayerTacticalStage;
            mixed.PausedAdapter.SetContext(null, Current.CurrentFrame);
            return mixed.Stage.Apply(in command);
        }
        public void Dispose()
        {
            if (_disposed) return; _disposed = true;
            Current.Dispose(); foreach (var slice in _retired) slice.Dispose(); _retired.Clear();
        }
    }
}
