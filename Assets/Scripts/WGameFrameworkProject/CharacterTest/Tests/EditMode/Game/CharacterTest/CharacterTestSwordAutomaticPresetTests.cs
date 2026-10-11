using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MxFramework.CharacterAction;
using MxFramework.Game.CharacterTest;
using MxFramework.Game.CharacterTest.TacticalCard;
using MxFramework.Gameplay;
using MxFramework.Runtime;
using Newtonsoft.Json;
using NUnit.Framework;

namespace MxFramework.Tests.Game.CharacterTest
{
    /// <summary>Formal published programs on the existing native command/action/CombatPhysics fixture.</summary>
    public sealed class CharacterTestSwordAutomaticPresetTests
    {
        [Test]
        public void FarEngagementMovesAttacksWithdrawsAndReturnsToSelection()
        {
            using var f = new Fixture();
            f.World.PlaceTarget(6000); f.World.AdvanceWorldOnly();
            int initialHealth=f.World.Target.CombatState.Health;
            bool hit=false, withdrew=false; float previousDistance=f.World.RootDistance;
            for(int i=0;i<700;i++) {
                f.Step();
                hit |= f.World.Target.CombatState.Health<initialHealth;
                withdrew |= hit && f.World.Source.Action.State!=CharacterTestActionStateId.Running
                    && f.World.Source.Input.HasActiveMove && f.World.RootDistance>previousDistance+0.001f;
                previousDistance=f.World.RootDistance;
                if(hit && withdrew && f.Overlay.LastExecutionEndReason==CharacterTestBehaviorExecutionEndReason.Completed
                    && f.Overlay.SelectionPassCount>2) break;
            }
            f.WriteEvidence("engagement");
            CollectionAssert.Contains(f.Selected,CharacterTestRuntimeAiPlannerSwordPreset.CardIds[0]);
            CollectionAssert.Contains(f.Selected,CharacterTestRuntimeAiPlannerSwordPreset.CardIds[1]);
            Assert.IsTrue(f.World.Samples.Any(s=>s.ActionId==455 && s.ActionInstance>0),"Must submit the actual sword action.");
            Assert.IsTrue(hit,"Must damage the actual reference hurtbox.");
            Assert.IsTrue(withdrew,"Must observe movement away after the attack.");
            Assert.Greater(f.Overlay.SelectionPassCount,2,"The attack/withdraw sequence must also return to arbitration.");
        }

        [TestCase(12,true,2)]
        [TestCase(-1,true,1)]
        [TestCase(25,true,1)]
        [TestCase(12,false,1)]
        public void ExistingThreatFactsChooseRetreatOnlyWithConfirmedBoundedImpact(int impact,bool willHit,int selected)
        {
            using var f=new Fixture(); f.World.PlaceTarget(2000); f.World.AdvanceWorldOnly();
            f.Step(impact,willHit);
            Assert.AreEqual(CharacterTestRuntimeAiPlannerSwordPreset.CardIds[selected],f.Selected.Last());
            if(selected==2) {
                // The reused retreat has an authored start delay; verify its bounded actual motion.
                for(int i=0;i<40 && f.World.Source.Transform.ZMillimeters>=0;i++) f.Step(impact,willHit);
                Assert.Less(f.World.Source.Transform.ZMillimeters,0,"Ordinary retreat must move away within its start window.");
                Assert.IsFalse(f.World.Samples.Any(s=>s.ActionId==455 && s.ActionInstance>0));
            }
        }

        [Test]
        public void NewThreatWhileApproachingYieldsAndSelectsRetreat()
        {
            using var f=new Fixture(); f.World.PlaceTarget(6000); f.World.AdvanceWorldOnly();
            for(int i=0;i<40 && !f.World.Source.Input.HasActiveMove;i++) f.Step();
            Assert.IsTrue(f.World.Source.Input.HasActiveMove,"The approach must already be executing.");
            Assert.AreEqual(CharacterTestRuntimeAiPlannerSwordPreset.CardIds[0],f.Selected.Last());
            for(int i=0;i<8 && !f.Selected.Contains(CharacterTestRuntimeAiPlannerSwordPreset.CardIds[2]);i++) f.Step(12,true);
            f.WriteEvidence("approach-threat-yield");
            CollectionAssert.Contains(f.Selected,CharacterTestRuntimeAiPlannerSwordPreset.CardIds[2]);
            Assert.IsFalse(f.World.Samples.Any(s=>s.ActionId==455 && s.ActionInstance>0));
        }

        [Test]
        public void NewThreatWhileWithdrawingYieldsAndSelectsRetreat()
        {
            using var f=new Fixture(); f.World.PlaceTarget(2000); f.World.AdvanceWorldOnly();
            int health=f.World.Target.CombatState.Health; bool withdrawing=false;
            for(int i=0;i<400;i++) {
                float before=f.World.RootDistance; f.Step();
                withdrawing=f.World.Target.CombatState.Health<health
                    && f.World.Source.Action.State!=CharacterTestActionStateId.Running
                    && f.World.Source.Input.HasActiveMove && f.World.RootDistance>before+0.001f;
                if(withdrawing) break;
            }
            Assert.IsTrue(withdrawing,"Observe real ordinary withdrawal after the committed action completes.");
            Assert.AreEqual(CharacterTestRuntimeAiPlannerSwordPreset.CardIds[1],f.Selected.Last());
            for(int i=0;i<8 && !f.Selected.Contains(CharacterTestRuntimeAiPlannerSwordPreset.CardIds[2]);i++) f.Step(12,true);
            f.WriteEvidence("withdrawal-threat-yield");
            CollectionAssert.Contains(f.Selected,CharacterTestRuntimeAiPlannerSwordPreset.CardIds[2]);
        }

        [Test]
        public void NewThreatDoesNotInterruptSubmittedSwordAction()
        {
            using var f=new Fixture(); f.World.PlaceTarget(2000); f.World.AdvanceWorldOnly();
            for(int i=0;i<180 && !f.World.Samples.Any(s=>s.ActionId==455 && s.ActionInstance>0);i++) f.Step();
            var committed=f.World.Samples.Last(s=>s.ActionId==455 && s.ActionInstance>0);
            for(int i=0;i<5;i++) f.Step(12,true);
            f.WriteEvidence("committed-action-threat");
            Assert.AreEqual(CharacterTestRuntimeAiPlannerSwordPreset.CardIds[1],f.Selected.Last(),"The current committed action cannot be forcibly replaced.");
            Assert.IsTrue(f.World.Samples.SkipWhile(s=>s.WorldFrame<committed.WorldFrame).Any(s=>s.ActionInstance==committed.ActionInstance));
        }

        [Test]
        public void NoTargetUsesExistingFallbackWithoutMovementOrAttack()
        {
            using var f=new Fixture(); f.Step(noTarget:true);
            CollectionAssert.Contains(f.Selected,CharacterTestRuntimeAiPlannerSwordPreset.CardIds[3]);
            Assert.IsFalse(f.World.Source.Input.HasActiveMove);
            Assert.IsFalse(f.World.Samples.Any(s=>s.ActionInstance>0));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ExistingAbilityOwnerResourceOrCooldownGateLeavesFallbackAndSpendsNothing(bool cooldown)
        {
            // Explicit in-memory adverse configuration, not a new production action cost.
            var owner=new AbilityDefinitionConfig(93001,"sword-adverse-owner","Sword adverse owner",null,
                new AbilityCooldownConfig(60,0),new[]{new AbilityCostConfig(CharacterTestCharacterAttributeIds.Motion,20)},
                0,new AbilityActionBindingConfig(new CharacterActionId(455),MigrationFixtureIds.Id0030),default,0,0);
            var definitions=new AbilityDefinitionRegistry(); Assert.IsTrue(definitions.TryRegister(owner,out _));
            var abilities=new AbilityRuntimeModule(definitions);
            using var f=new Fixture(abilities); f.World.PlaceTarget(2000); f.World.AdvanceWorldOnly();
            var motion=new MomentumLedger(cooldown?50:19); f.World.Runtime.World.BindTacticalMotion(f.World.Source.Id.Value,motion);
            if(cooldown) abilities.CommitActivation(f.World.Source.Id.Value,owner,new RuntimeFrame(f.World.NextFrame));
            int before=motion.Current; f.Step();
            CollectionAssert.Contains(f.Selected,CharacterTestRuntimeAiPlannerSwordPreset.CardIds[3]);
            Assert.AreEqual(before,motion.Current); Assert.AreEqual(0,f.World.Runtime.ActionUseAuthority.CommitCount);
            Assert.IsFalse(f.World.Samples.Any(s=>s.ActionInstance>0));
            f.WriteEvidence(cooldown?"cooldown-gate":"motion-gate");
        }

        private sealed class Fixture : IDisposable
        {
            internal readonly CharacterTestTacticalCardRuntimeFixture World;
            internal readonly CharacterTestRuntimeAiLabBehaviorCardRuntimeOverlay Overlay;
            internal readonly List<string> Selected=new List<string>();
            private readonly List<object> _events=new List<object>();
            internal Fixture(AbilityRuntimeModule abilities=null)
            {
                string role="character.definition."+CharacterTestRuntimeAiPlannerSwordPreset.CharacterId;
                var record=new CharacterTestConfiguredCardLibrary().List(role).Single(c=>c.Name==CharacterTestRuntimeAiPlannerSwordPreset.Name
                    && c.BaseKey=="published:"+CharacterTestRuntimeAiPlannerSwordPreset.CardIds[1]);
                Assert.AreEqual(role,record.OwnerCharacter); Assert.AreEqual(455,record.Actions["primary"]);
                Assert.AreEqual(1,record.Actions.Count);
                var savedSource=CharacterTestTacticalPublishedSource.Read(record.Source);
                World=new CharacterTestTacticalCardRuntimeFixture(actionId:record.Actions["primary"],
                    sourceCharacterId:CharacterTestRuntimeAiPlannerSwordPreset.CharacterId,abilityRuntime:abilities);
                var catalog=CharacterTestRuntimeAiPlannerProjectCatalogLoader.Load(); Assert.IsTrue(catalog.Success,catalog.Diagnostic);
                var profile=new CharacterTestRuntimeAiPlannerBehaviorCompositionProfile("sword-native-preset");
                var programs=new List<CharacterTestRuntimeAiLabCompiledLogicProgram>();
                for(int kind=0;kind<4;kind++) {
                    Assert.IsTrue(catalog.CardCatalog.TryResolveLatestCompatible(CharacterTestRuntimeAiPlannerSwordPreset.CardIds[kind],8,out var card));
                    CharacterTestRuntimeAiLabCompiledLogicProgram program;
                    if(card.TacticalSource!=null) {
                        var inputs=kind==1?CharacterTestTacticalCardInputs.BindSlots(CharacterTestRuntimeAiPlannerSwordPreset.CharacterId,
                            new Dictionary<string,CharacterTestTacticalCardInputs>{{"primary",World.Inputs}})
                            :CharacterTestTacticalCardInputs.Unbound(CharacterTestRuntimeAiPlannerSwordPreset.CharacterId);
                        if(kind==1) Assert.IsTrue(Newtonsoft.Json.Linq.JToken.DeepEquals(record.Source,card.TacticalSource.ToJson()),"Saved source must match the current exact published sword card.");
                        var source=kind==1?savedSource:card.TacticalSource;
                        Assert.IsTrue(source.TryCompile(card.StableCardId,card.Metadata.DisplayName,card.Revision,inputs,out program,out string issue),issue);
                    } else {
                        var compiled=CharacterTestRuntimeAiLabBehaviorCardLogicCompiler.Compile(card.CloneLogic(),
                            new CharacterTestRuntimeAiLabBehaviorCardReferenceCatalog(null,World.Runtime.ActionRuntime.Registry));
                        Assert.IsTrue(compiled.Success,JsonConvert.SerializeObject(compiled.Diagnostics)); program=compiled.Program;
                    }
                    programs.Add(program.WithSelectionDefinition(CharacterTestBehaviorCardSelectionDefinition.FromPublished(card)));
                    var entry=new CharacterTestRuntimeAiPlannerBehaviorModuleEntry(CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationIdle,true,kind*10)
                        {EntryId=card.StableCardId,NumericId=card.NumericId,DefinitionNumericId=card.NumericId};
                    entry.SetResponsibilities(card.Metadata.Responsibilities);
                    CharacterTestRuntimeAiPlannerPlayerDeckRuntimeCompiler.ConfigureCanonicalArbitration(entry,card.Metadata);
                    profile.Entries.Add(entry);
                }
                Overlay=new CharacterTestRuntimeAiLabBehaviorCardRuntimeOverlay(profile,programs,World.Runtime.ActionRuntime,abilities,73,
                    entities:World.Runtime.Entities,navigationService:CharacterTestDirectAiNavigationService.Instance,
                    perceptionFrameCache:World.Runtime.PerceptionFrameCache,characterRuntime:World.Runtime);
            }
            internal void Step(int impact=-1,bool willHit=false,bool noTarget=false)
            {
                World.RefreshFacts();
                if(noTarget) World.Facts.SetNoTarget(false);
                else World.Facts.SetTargetAttackThreat(new CharacterActionId(455),willHit?1:0,impact,willHit,false,true);
                var frame=new RuntimeFrame(World.NextFrame);
                Assert.IsTrue(Overlay.TryTick(World.Source,World.Facts,frame,out var intent));
                if(intent.Kind!=CharacterTestRuntimeAiLabRuntimeIntentKind.None) {
                    var result=CharacterTestBehaviorExecutionCommandWriter.Submit(frame,World.Source.Id.Value,intent,World.Commands.Enqueue);
                    Assert.IsTrue(result.Success,result.Success?"":result.Error.Message);
                    Overlay.RecordCommandResult(World.Source.Id.Value,result);
                }
                string selected="";
                if(Overlay.TryGetRuntimePresentation(World.Source.Id.Value,out var presentation)) {
                    selected=presentation.StableCardId;
                    if(Selected.Count==0||Selected.Last()!=selected) Selected.Add(selected);
                }
                _events.Add(new {frame=frame.Value,card=selected,distance=World.RootDistance,impact,willHit,
                    intent=intent.Kind.ToString(),passes=Overlay.SelectionPassCount,end=Overlay.LastExecutionEndReason.ToString()});
                World.AdvanceWorldOnly();
            }
            internal void WriteEvidence(string name)
            {
                string directory=Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath,"../Library/SwordPresetAcceptance"));
                Directory.CreateDirectory(directory);
                File.WriteAllText(Path.Combine(directory,name+".json"),JsonConvert.SerializeObject(new {selected=Selected,events=_events,
                    samples=World.Samples,commits=World.Runtime.ActionUseAuthority.CommitCount},Formatting.Indented));
            }
            public void Dispose()=>World.Dispose();
        }
    }
}
