using System;
using System.Linq;
using System.Collections.Generic;
using MxFramework.Gameplay;
using MxFramework.Runtime;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MxFramework.Game.CharacterTest.TacticalCard
{
    /// <summary>Trusted frozen composition supplied by the session owner, never read from the incoming DTO.</summary>
    internal sealed class TacticalSessionRestoreContext
    {
        internal readonly string Scope,Profile,Battle,CatalogHash;
        internal readonly long Frame;
        internal readonly int TickRate,CommandCapacity;
        internal readonly TacticalCardDefinition[] Definitions;
        internal readonly SessionActorSnapshot[] Actors;
        internal readonly GameplayEntityId[] ResourceActors;
        internal readonly int DeckCharacter;
        internal readonly bool CardCooldownBound;
        internal readonly float CardCooldownTickRate;
        internal readonly AbilityDefinitionRegistry Abilities;
        internal readonly ulong StageRun;
        internal readonly TacticalStoryStepBinding Opening;
        internal readonly int HandCapacity;
        internal readonly bool TestPreparation;
        internal readonly Func<IReadOnlyDictionary<GameplayEntityId,MomentumLedger>,IAbilityCostAuthority> CreateDetachedCosts;
        internal readonly Func<IReadOnlyDictionary<GameplayEntityId,MomentumLedger>,AbilityRuntimeModule,ITacticalExecutionPort> CreateDetachedExecution;
        internal readonly Func<IReadOnlyDictionary<GameplayEntityId,MomentumLedger>,AbilityRuntimeModule,Func<long>,Func<TacticalCardDefinition,CardPlayEvaluation>> CreateDetachedEvaluation;
        internal readonly Func<RuntimeCommand,bool> ValidateCommand;
        internal TacticalSessionRestoreContext(string scope,string profile,string battle,string catalogHash,long frame,int tickRate,
            SessionActorSnapshot[] actors,GameplayEntityId[] resourceActors,TacticalCardDefinition[] definitions,AbilityDefinitionRegistry abilities,ulong stageRun,
            TacticalStoryStepBinding opening,int handCapacity,bool testPreparation,int commandCapacity,
            Func<IReadOnlyDictionary<GameplayEntityId,MomentumLedger>,IAbilityCostAuthority> createDetachedCosts,
            Func<IReadOnlyDictionary<GameplayEntityId,MomentumLedger>,AbilityRuntimeModule,ITacticalExecutionPort> createDetachedExecution,
            Func<IReadOnlyDictionary<GameplayEntityId,MomentumLedger>,AbilityRuntimeModule,Func<long>,Func<TacticalCardDefinition,CardPlayEvaluation>> createDetachedEvaluation,
            Func<RuntimeCommand,bool> validateCommand,int deckCharacter=0,bool cardCooldownBound=false,float cardCooldownTickRate=0f)
        {
            SessionSnapshotCheck.Require((scope=="profile"||scope=="lab") && !string.IsNullOrWhiteSpace(profile) && !string.IsNullOrWhiteSpace(battle) &&
                !string.IsNullOrWhiteSpace(catalogHash) && frame>=0 && tickRate>0 && actors!=null && actors.Length>=2 && definitions!=null && definitions.Length>0 &&
                resourceActors!=null && resourceActors.Length>0 && abilities!=null && stageRun>0 && handCapacity>0 && commandCapacity>=0 && createDetachedCosts!=null && createDetachedExecution!=null &&
                createDetachedEvaluation!=null && validateCommand!=null,"composition");
            Scope=scope;Profile=profile;Battle=battle;CatalogHash=catalogHash;Frame=frame;TickRate=tickRate;StageRun=stageRun;Opening=opening;
            HandCapacity=handCapacity;TestPreparation=testPreparation;CommandCapacity=commandCapacity;Abilities=abilities;
            Definitions=(TacticalCardDefinition[])definitions.Clone();
            Actors=actors.Select(a=>a==null?null:new SessionActorSnapshot {Entity=a.Entity,Generation=a.Generation,Item=a.Item,Owner=a.Owner,Revision=a.Revision}).ToArray();
            ResourceActors=(GameplayEntityId[])resourceActors.Clone();
            SessionSnapshotCheck.Require(deckCharacter>=0 && (cardCooldownBound?cardCooldownTickRate>0f && !float.IsNaN(cardCooldownTickRate) && !float.IsInfinity(cardCooldownTickRate):cardCooldownTickRate==0f),"composition-card-cooldown");
            DeckCharacter=deckCharacter;CardCooldownBound=cardCooldownBound;CardCooldownTickRate=cardCooldownTickRate;
            CreateDetachedCosts=createDetachedCosts;CreateDetachedExecution=createDetachedExecution;CreateDetachedEvaluation=createDetachedEvaluation;ValidateCommand=validateCommand;
            var ids=new HashSet<int>();var items=new HashSet<int>();
            foreach(var a in Actors)SessionSnapshotCheck.Require(a!=null && a.Id.IsValid && ids.Add(a.Entity) &&
                (scope=="lab"?a.Item==0&&a.Owner==0&&a.Revision==0:a.Item>0&&a.Owner>0&&a.Revision>0&&items.Add(a.Item)),"composition-actors");
            SessionSnapshotCheck.Require(ResourceActors.Distinct().Count()==ResourceActors.Length && ResourceActors.All(IsCurrent) &&
                ResourceActors.Contains(Actors[0].Id),"composition-resource-actors");
        }
        internal bool IsCurrent(GameplayEntityId id) => Actors.Any(a=>a.Entity==id.Index && a.Generation==id.Generation);
    }
    // This gate is private to an unpublished candidate. It cannot write Unity/global time.
    internal sealed class TacticalSnapshotTimeGate : ITacticalTimeAdapter
    {
        public bool IsSimulationPaused {get;private set;}
        public void SetSimulationPaused(bool paused) => IsSimulationPaused=paused;
    }
    internal sealed class TacticalRestoredSession
    {
        internal readonly IReadOnlyDictionary<GameplayEntityId,MomentumLedger> Ledgers;
        internal MomentumLedger Ledger => Ledgers[Context.Actors[0].Id];
        internal readonly TacticalDeckManager Deck;
        internal readonly TacticalStageFlowCoordinator Stage;
        internal readonly TacticalCommandPump Controls;
        internal readonly RuntimeCommandBuffer Commands;
        internal readonly AbilityRuntimeModule Abilities;
        internal readonly TacticalSessionRestoreContext Context;
        internal TacticalRestoredSession(TacticalSessionRestoreContext context,IReadOnlyDictionary<GameplayEntityId,MomentumLedger> ledgers,TacticalDeckManager deck,
            TacticalStageFlowCoordinator stage,TacticalCommandPump controls,RuntimeCommandBuffer commands,AbilityRuntimeModule abilities)
        {
            SessionSnapshotCheck.Require(context!=null && ledgers!=null && ledgers.Count==context.ResourceActors.Length &&
                context.ResourceActors.All(id=>ledgers.TryGetValue(id,out var ledger) && ledger!=null) && ledgers.Values.Distinct().Count()==ledgers.Count,"resource-map-composition");
            Context=context;Ledgers=ledgers;Deck=deck;Stage=stage;Controls=controls;Commands=commands;Abilities=abilities;
        }

        internal TacticalSessionSnapshot Capture()
        {
            Stage.ValidateCanonicalComposition(Context,Deck,Ledger);
            Deck.ValidateFrozenCooldownBinding(Context);
            var c=Commands.CaptureSnapshot();var a=new AbilityRuntimeSaveStateProvider(Abilities).CaptureSaveState();
            SessionSnapshotCheck.Require(a.Success,"ability-capture");
            var snapshot=new TacticalSessionSnapshot {Scope=Context.Scope,Profile=Context.Profile,Battle=Context.Battle,CatalogHash=Context.CatalogHash,
                Composition=TacticalCompositionSnapshot.Canonical(),
                Frame=Stage.LastCompletedFrame,TickRate=Context.TickRate,Actors=Context.Actors.Select(x=>new SessionActorSnapshot {Entity=x.Entity,Generation=x.Generation,Item=x.Item,Owner=x.Owner,Revision=x.Revision}).ToArray(),
                Motion=Context.ResourceActors.Select(id=>new SessionMomentumSnapshot {Actor=id.Index,Generation=id.Generation,Ledger=Ledgers[id].CaptureSessionState()}).ToArray(),
                Deck=Deck.CaptureSessionState(Context.Definitions),Stage=Stage.CaptureSessionState(),Controls=Controls.CaptureSessionState(),
                Commands=new RuntimeCommandsSnapshot {Frame=c.CurrentFrame.Value,NextSequence=c.NextSequence,Capacity=Commands.PendingCapacityLimit,
                    Pending=c.PendingCommands.Select(x=>RuntimeCommandSnapshot.Capture(x)).ToArray()},
                AbilityModule=Abilities.ModuleId,AbilityPayload=a.Value.ModuleStates.Single().CustomState.PayloadJson};
            SessionSnapshotCheck.Require(snapshot.Frame==c.CurrentFrame.Value,"capture-frame-barrier");
            TacticalSessionSnapshotOwner.ValidateAbility(snapshot.AbilityPayload,Context,snapshot.Frame);
            if(snapshot.Deck.Cooldown!=null)TacticalCooldownTracker.RestoreSessionState(snapshot.Deck.Cooldown,Context.Definitions,Context.DeckCharacter,snapshot.Frame);
            return snapshot;
        }
    }
    /// <summary>Atomic publication of the tactical partition. World/physics/session execution publication remains an explicit integration gate.</summary>
    internal sealed class TacticalSessionSnapshotOwner
    {
        internal TacticalRestoredSession Current {get;private set;}
        // Not restored from a save: previous UI/async callers must get a fresh handle after publication.
        internal ulong Lease {get;private set;}=1;
        internal TacticalSessionSnapshotOwner(TacticalRestoredSession current) {Current=current??throw new ArgumentNullException(nameof(current));}
        internal bool TryRestore(TacticalSessionSnapshot snapshot,TacticalSessionRestoreContext context,out string issue)
        {
            issue=string.Empty;
            try {
                SessionSnapshotCheck.Require(Lease<ulong.MaxValue,"ui-lease-exhausted");
                SessionSnapshotCheck.Require(context!=null,"composition-required");
                Current.Stage.ValidateCanonicalComposition(context,Current.Deck,Current.Ledger);
                Current.Deck.ValidateFrozenCooldownBinding(context);
                // Copy untrusted caller-owned data once; no later alias can mutate the candidate.
                var copy=TacticalSessionSnapshotJson.Read(TacticalSessionSnapshotJson.Write(snapshot));
                var candidate=Prepare(copy,context);
                Current=candidate;Lease++; // The only publication writes; all failure paths precede these.
                return true;
            } catch(Exception ex) {issue=ex.Message;return false;}
        }
        internal TacticalStageRejection Submit(ulong lease,in TacticalStageCommand command,int source,ulong sequence)
            => lease!=Lease?TacticalStageRejection.StaleSnapshot:Current.Controls.TrySubmit(in command,source,sequence);

        internal static TacticalRestoredSession Prepare(TacticalSessionSnapshot s,TacticalSessionRestoreContext c)
        {
            SessionSnapshotCheck.Require(s!=null && c!=null && s.Format=="character-test.tactical-session.candidate" && s.Version==2 && s.Composition!=null && s.Composition.IsCanonical() &&
                s.Scope==c.Scope && s.Profile==c.Profile && s.Battle==c.Battle && s.CatalogHash==c.CatalogHash && s.Frame==c.Frame && s.TickRate==c.TickRate &&
                s.Actors!=null && s.Actors.Length==c.Actors.Length && s.AbilityModule=="mxframework.ability.runtime","identity-version");
            for(int i=0;i<s.Actors.Length;i++){var a=s.Actors[i];var b=c.Actors[i];SessionSnapshotCheck.Require(a!=null &&
                a.Entity==b.Entity && a.Generation==b.Generation && a.Item==b.Item && a.Owner==b.Owner && a.Revision==b.Revision,"identity-map");}
            // Construct every collaborator fresh BEFORE importing ledger/cards or calling a provider.
            SessionSnapshotCheck.Require(s.Motion!=null && s.Motion.Length==c.ResourceActors.Length,"resource-map");
            var ledgers=new Dictionary<GameplayEntityId,MomentumLedger>();
            for(int i=0;i<s.Motion.Length;i++){
                var savedMotion=s.Motion[i];var id=c.ResourceActors[i];SessionSnapshotCheck.Require(savedMotion!=null && savedMotion.Actor==id.Index && savedMotion.Generation==id.Generation,"resource-actor-generation");
                ledgers.Add(id,new MomentumLedger());}
            var detachedMap=new System.Collections.ObjectModel.ReadOnlyDictionary<GameplayEntityId,MomentumLedger>(ledgers);
            var ledger=detachedMap[c.Actors[0].Id];var deck=new TacticalDeckManager(c.HandCapacity,c.TestPreparation);var time=new TacticalTimeCoordinator(new TacticalSnapshotTimeGate());
            var costs=c.CreateDetachedCosts(detachedMap);SessionSnapshotCheck.Require(costs!=null,"detached-cost-authority-required");
            var ability=new AbilityRuntimeModule(c.Abilities,costAuthority:costs);
            var execution=c.CreateDetachedExecution(detachedMap,ability);SessionSnapshotCheck.Require(execution!=null,"detached-execution-required");
            var driver=new TacticalSingleCardDriver(execution,time,deck,ledger);
            var stage=new TacticalStageFlowCoordinator(c.StageRun,time,deck,ledger,driver,c.Definitions,42,c.IsCurrent,c.Opening,
                allowConfigurableHandCapacity:c.HandCapacity!=3);
            stage.ValidateCanonicalComposition(c,deck,ledger);
            SessionSnapshotCheck.Require(s.Deck!=null && s.Deck.Character==c.DeckCharacter && (s.Deck.Cooldown!=null)==c.CardCooldownBound &&
                (s.Deck.Cooldown==null || s.Deck.Cooldown.TickRate==c.CardCooldownTickRate),"frozen-card-cooldown-binding");
            for(int i=0;i<s.Motion.Length;i++)ledgers[c.ResourceActors[i]].RestoreSessionState(s.Motion[i].Ledger);
            deck.RestoreSessionState(s.Deck,c.Definitions,ledger,()=>stage.LastCompletedFrame,c.Frame);
            deck.ValidateFrozenCooldownBinding(c);
            SessionSnapshotCheck.Require(s.Stage!=null && s.Stage.Player==c.Actors[0].Entity && s.Stage.PlayerGeneration==c.Actors[0].Generation,"resource-player-join");
            stage.RestorePlanningSessionState(s.Stage,s.Frame);
            SessionSnapshotCheck.Require(s.Controls!=null && s.Controls.Capacity==64 && s.Controls.SourceCapacity==8,"control-capacity");
            var controls=new TacticalCommandPump(stage,s.Controls.Capacity,s.Controls.SourceCapacity);controls.RestoreSessionState(s.Controls);
            var commands=RestoreCommands(s.Commands,c);
            // Reuse the existing Ability provider after stricter aggregate validation.
            ValidateAbility(s.AbilityPayload,c,s.Frame);
            var custom=new RuntimeCustomState(3310,AbilityRuntimeSaveStateProvider.CustomStateTypeId,AbilityRuntimeSaveStateProvider.CustomStateSchemaVersion,s.AbilityPayload);
            var saved=new RuntimeSaveState(RuntimeSaveState.CurrentSchemaVersion,new DateTime(1970,1,1,0,0,0,DateTimeKind.Utc),"","","",s.Frame,null,null,
                new[]{new RuntimeModuleSaveState(ability.ModuleId,AbilityRuntimeSaveStateProvider.CustomStateSchemaVersion,custom)});
            SessionSnapshotCheck.Require(new AbilityRuntimeSaveStateProvider(ability).RestoreSaveState(saved).Success,"ability-provider-restore");
            // Closures must belong to this unpublished aggregate, including its live frame source.
            // Never reuse delegates from the previous deck or a live composition root.
            deck.DefinitionEvaluation=c.CreateDetachedEvaluation(detachedMap,ability,()=>stage.LastCompletedFrame);
            SessionSnapshotCheck.Require(deck.DefinitionEvaluation!=null,"detached-admission-required");
            return new TacticalRestoredSession(c,detachedMap,deck,stage,controls,commands,ability);
        }
        internal static void ValidateAbility(string payload,TacticalSessionRestoreContext c,long frame)
        {
            SessionSnapshotCheck.Require(!string.IsNullOrEmpty(payload) && payload.Length<=1024*1024,"ability-payload");
            JObject a;
            using(var text=new System.IO.StringReader(payload))using(var reader=new JsonTextReader(text)) {
                a=JObject.Load(reader,new JsonLoadSettings {DuplicatePropertyNameHandling=DuplicatePropertyNameHandling.Error});SessionSnapshotCheck.Require(!reader.Read(),"ability-trailing-document");}
            SessionSnapshotCheck.Require(a.Properties().Select(p=>p.Name).OrderBy(x=>x).SequenceEqual(new[]{"cooldowns","definitionHash","schemaVersion"}) &&
                a["schemaVersion"].Type==JTokenType.Integer && a["definitionHash"].Type==JTokenType.Integer &&
                (int?)a["schemaVersion"]==AbilityRuntimeState.CurrentSchemaVersion && (long?)a["definitionHash"]==c.Abilities.ComputeStableHash() && a["cooldowns"] is JArray,"ability-definition-version");
            var ids=new HashSet<string>(StringComparer.Ordinal);
            foreach(var token in (JArray)a["cooldowns"]){var e=token as JObject;SessionSnapshotCheck.Require(e!=null &&
                e.Properties().Select(p=>p.Name).OrderBy(x=>x).SequenceEqual(new[]{"abilityId","casterEntityId","endFrame"}),"ability-row");
                SessionSnapshotCheck.Require(e.Properties().All(p=>p.Value.Type==JTokenType.Integer),"ability-integer-width");
                int actor=(int)e["casterEntityId"],id=(int)e["abilityId"];long end=(long)e["endFrame"];
                bool known=c.Abilities.TryGet(id,out var definition);
                SessionSnapshotCheck.Require(c.Actors.Any(x=>x.Entity==actor) && known && end>=0 && ids.Add(actor+":"+id),"ability-owner-duplicate");
                // Frame/end are nonnegative: subtraction cannot overflow when end is future.
                SessionSnapshotCheck.Require(end<=frame || end-frame<=definition.Cooldown.Frames,"ability-deadline-out-of-range");}
        }
        private static RuntimeCommandBuffer RestoreCommands(RuntimeCommandsSnapshot s,TacticalSessionRestoreContext c)
        {
            SessionSnapshotCheck.Require(s!=null && s.Frame==c.Frame && s.NextSequence>=0 && s.Capacity==c.CommandCapacity && s.Pending!=null && s.Pending.Length<=c.CommandCapacity,"runtime-commands");
            var ids=new HashSet<long>();var restored=new List<RuntimeCommand>(s.Pending.Length);
            foreach(var d in s.Pending){SessionSnapshotCheck.Require(d!=null && d.Frame>=c.Frame && d.Sequence>=0 && d.Sequence<s.NextSequence && ids.Add(d.Sequence) &&
                d.Command>=0 && d.Data!=null && d.Data.Length==8 && (d.EnvelopeType!=0 || d.EnvelopeVersion==0 && d.Provenance==0 && d.Data.All(x=>x==0)),"runtime-command-entry");
                var command=d.Restore();SessionSnapshotCheck.Require(c.ValidateCommand(command),"runtime-command-capability");restored.Add(command);}
            var buffer=RuntimeCommandBuffer.CreateBounded(null,new RuntimeFrame(c.Frame),c.CommandCapacity);
            buffer.RestoreSnapshot(new RuntimeCommandBufferSnapshot(new RuntimeFrame(c.Frame),s.NextSequence,restored.ToArray()));return buffer;
        }
    }
    internal static class TacticalSessionSnapshotJson
    {
        private static readonly JsonSerializerSettings Settings=new JsonSerializerSettings {MissingMemberHandling=MissingMemberHandling.Error,TypeNameHandling=TypeNameHandling.None,MaxDepth=64};
        internal static string Write(TacticalSessionSnapshot s) => JsonConvert.SerializeObject(s,Settings);
        internal static TacticalSessionSnapshot Read(string json)
        {
            SessionSnapshotCheck.Require(json!=null && json.Length<=4*1024*1024,"document-size");
            using(var text=new System.IO.StringReader(json))using(var reader=new JsonTextReader(text)){reader.MaxDepth=64;
                var token=JToken.Load(reader,new JsonLoadSettings {DuplicatePropertyNameHandling=DuplicatePropertyNameHandling.Error});
                SessionSnapshotCheck.Require(!reader.Read(),"trailing-document");
                var document=token as JObject;
                var fields=typeof(TacticalSessionSnapshot).GetFields(System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public).Select(x=>x.Name).OrderBy(x=>x,StringComparer.Ordinal);
                SessionSnapshotCheck.Require(document!=null && document.Properties().Select(x=>x.Name).OrderBy(x=>x,StringComparer.Ordinal).SequenceEqual(fields),"required-root-fields");
                ValidateShape(document,typeof(TacticalSessionSnapshot));
                foreach(var number in document.Descendants().OfType<JValue>().Where(x=>x.Type==JTokenType.Float))
                    SessionSnapshotCheck.Require(number.Path=="Deck.Cooldown.TickRate" || number.Path.StartsWith("Composition.",StringComparison.Ordinal) &&
                        (number.Path.EndsWith("LinearSpeed",StringComparison.Ordinal) || number.Path.EndsWith("AngularSpeed",StringComparison.Ordinal)),"integer-width");
                return token.ToObject<TacticalSessionSnapshot>(JsonSerializer.Create(Settings));}
        }
        private static void ValidateShape(JToken token,Type type)
        {
            if(token.Type==JTokenType.Null){SessionSnapshotCheck.Require(!type.IsValueType,"null-value");return;}
            if(type==typeof(string)){SessionSnapshotCheck.Require(token.Type==JTokenType.String,"string-type");return;}
            if(type==typeof(bool)){SessionSnapshotCheck.Require(token.Type==JTokenType.Boolean,"boolean-type");return;}
            if(type==typeof(float)){SessionSnapshotCheck.Require(token.Type==JTokenType.Float || token.Type==JTokenType.Integer,"float-type");return;}
            if(type.IsPrimitive || type.IsEnum){SessionSnapshotCheck.Require(token.Type==JTokenType.Integer,"integer-type");return;}
            if(type.IsArray){SessionSnapshotCheck.Require(token is JArray,"array-type");foreach(var child in (JArray)token)ValidateShape(child,type.GetElementType());return;}
            var value=token as JObject;var fields=type.GetFields(System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Instance);
            SessionSnapshotCheck.Require(value!=null && value.Properties().Select(x=>x.Name).OrderBy(x=>x,StringComparer.Ordinal)
                .SequenceEqual(fields.Select(x=>x.Name).OrderBy(x=>x,StringComparer.Ordinal)),"required-fragment-fields");
            foreach(var field in fields)ValidateShape(value[field.Name],field.FieldType);
        }
    }
}
