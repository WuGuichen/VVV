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
using Newtonsoft.Json.Linq;

// CPU regression: reads captured real catalogs and mutates only private fixture state.
internal static class CooldownCoverageProbe
{
    private sealed class FixtureCosts : IAbilityCostAuthority
    {
        internal int Queries, Commits;
        public bool CanAfford(int actor, IReadOnlyList<AbilityCostConfig> costs, out int missing)
        { Queries++; missing=0; return true; }
        public bool TryCommitCosts(int actor, IReadOnlyList<AbilityCostConfig> costs, out int missing)
        { Commits++; missing=0; return true; }
    }
    private static int passed, failed;
    private static readonly List<object> assertions=new List<object>();
    private static void Check(bool condition,string name)
    {
        if(condition) passed++; else failed++;
        assertions.Add(new { Name=name, Passed=condition });
    }
    private static int Main(string[] args)
    {
        try
        {
            string root=args[0], output=args[1];
            var rows=new List<object>();
            foreach(string distribution in new[]{"generated","streaming"})
            {
                string source=Path.Combine(root,distribution);
                string abilityPath=Path.Combine(source,"ability_catalog.json");
                var jsonRegistry=AbilityDefinitionJsonImporter.ImportFile(abilityPath);
                var binary=AbilityDefinitionBinaryImporter.ImportFile(Path.Combine(source,"ability_catalog.bytes"));
                Check(binary.Success,distribution+": ability binary import");
                if(!binary.Success) throw new InvalidOperationException("Real ability binary could not be imported");
                var abilitiesRegistry=binary.Catalog;
                Check(jsonRegistry.ComputeStableHash()==abilitiesRegistry.ComputeStableHash(),distribution+": ability binary/json definitions agree");
                var actions=new List<CharacterActionConfig>();
                foreach(string file in Directory.GetFiles(Path.Combine(source,"libraries"),"*.json",SearchOption.AllDirectories).OrderBy(p=>p,StringComparer.Ordinal))
                {
                    var imported=CharacterActionJsonImporter.Import("{\"schemaVersion\":\"0.6.0\",\"actionSets\":[]}",File.ReadAllText(file),"probe.empty-action-sets",file);
                    actions.AddRange(imported.Actions);
                }
                var actionRegistry=new CharacterActionConfigRegistry(Array.Empty<CharacterActionSetConfig>(),actions.ToArray());
                Console.WriteLine(distribution+": actions="+actions.Count+" cooldownRequirementActions="+actions.Count(a=>a.Requirements.Any(r=>r.Kind==CharacterActionRequirementKind.CooldownReady)));
                var costs=new FixtureCosts();
                var abilities=new AbilityRuntimeModule(abilitiesRegistry,costAuthority:costs);
                var authority=new CharacterTestActionUseAuthority(null,abilities);
                int checkedActions=0, missedActions=0;
                foreach(var token in (JArray)JObject.Parse(File.ReadAllText(abilityPath))["abilities"])
                {
                    int abilityId=token.Value<int>("abilityId");
                    if(!abilitiesRegistry.TryGet(abilityId,out var ability)) throw new InvalidOperationException("Missing real ability");
                    if(ability.Cooldown.Frames<=0) continue;
                    int id=ability.ActionBinding.ActionId.Value;
                    if(!actionRegistry.TryGetAction(new CharacterActionId(id),out var action)) throw new InvalidOperationException("Missing actual bound action "+id);
                    if(!abilities.TryCommitActivation(1,ability,RuntimeFrame.Zero,out var failure)) throw new InvalidOperationException("Fixture cooldown seed failed: "+failure.ReasonCode);
                    long actualRemaining=abilities.GetRemainingCooldownFrames(1,ability,RuntimeFrame.Zero);
                    Check(actualRemaining>0,distribution+":"+id+": actual Ability cooldown active");
                    int costQueries=costs.Queries, costCommits=costs.Commits;
                    string before=JsonConvert.SerializeObject(abilities.CreateStateSnapshot());
                    var first=new Dictionary<string,int>{{"attack",id}};
                    var second=new Dictionary<string,int>{{"different-template-slot",id}};
                    var combined=new Dictionary<string,int>{{"opener",301},{"second-action",id}};
                    bool single=CharacterTestTacticalExecutionAdapter.ValidateBoundActionCooldowns(first,actionRegistry,authority,1,RuntimeFrame.Zero,out var singleIssue);
                    bool otherTemplate=CharacterTestTacticalExecutionAdapter.ValidateBoundActionCooldowns(second,actionRegistry,authority,1,RuntimeFrame.Zero,out var secondIssue);
                    bool multi=CharacterTestTacticalExecutionAdapter.ValidateBoundActionCooldowns(combined,actionRegistry,authority,1,RuntimeFrame.Zero,out var multiIssue);
                    bool automaticSingle=CharacterTestRuntimeAiLabBehaviorCardRuntimeOverlay.RuntimeBridge.EvaluateConfiguredActionCooldowns(
                        first.Values.ToArray(),authority,actionRegistry,1,RuntimeFrame.Zero).Available;
                    bool automaticOther=CharacterTestRuntimeAiLabBehaviorCardRuntimeOverlay.RuntimeBridge.EvaluateConfiguredActionCooldowns(
                        second.Values.ToArray(),authority,actionRegistry,1,RuntimeFrame.Zero).Available;
                    bool automaticMulti=CharacterTestRuntimeAiLabBehaviorCardRuntimeOverlay.RuntimeBridge.EvaluateConfiguredActionCooldowns(
                        combined.Values.ToArray(),authority,actionRegistry,1,RuntimeFrame.Zero).Available;
                    Check(!single,distribution+":"+id+": cooling single action must reject");
                    Check(!otherTemplate,distribution+":"+id+": same action in different template must reject");
                    Check(!multi,distribution+":"+id+": ready first action plus cooling second action must reject");
                    Check(!automaticSingle,distribution+":"+id+": automatic single action must reject");
                    Check(!automaticOther,distribution+":"+id+": automatic different template must reject");
                    Check(!automaticMulti,distribution+":"+id+": automatic complete combination must reject");
                    Check(CharacterTestTacticalExecutionAdapter.ValidateBoundActionCooldowns(first,actionRegistry,authority,2,RuntimeFrame.Zero,out _),distribution+":"+id+": separate actor unaffected");
                    Check(CharacterTestTacticalExecutionAdapter.ValidateBoundActionCooldowns(first,actionRegistry,authority,1,new RuntimeFrame(ability.Cooldown.Frames),out _),distribution+":"+id+": exact cooldown end allows");
                    Check(CharacterTestRuntimeAiLabBehaviorCardRuntimeOverlay.RuntimeBridge.EvaluateConfiguredActionCooldowns(
                        combined.Values.ToArray(),authority,actionRegistry,2,RuntimeFrame.Zero).Available,distribution+":"+id+": automatic separate actor allowed");
                    Check(CharacterTestRuntimeAiLabBehaviorCardRuntimeOverlay.RuntimeBridge.EvaluateConfiguredActionCooldowns(
                        combined.Values.ToArray(),authority,actionRegistry,1,new RuntimeFrame(ability.Cooldown.Frames)).Available,distribution+":"+id+": automatic exact deadline allows");
                    Check(before==JsonConvert.SerializeObject(abilities.CreateStateSnapshot())&&costs.Queries==costQueries&&costs.Commits==costCommits&&authority.CommitCount==0,
                        distribution+":"+id+": query is read-only and does not query/pay resources");
                    var direct=authority.EvaluateCooldown(1,action,RuntimeFrame.Zero,out long gateRemaining);
                    bool selectable=CharacterTestTacticalCardInputs.TryValidateAction(action,out string selectionIssue);
                    rows.Add(new { Distribution=distribution,ActionId=id,ActionName=action.DisplayName,Category=action.Category.ToString(),AbilityId=abilityId,
                        ActualCooldownFrames=actualRemaining,DeclaredCooldownOwners=action.Requirements.Where(r=>r.Kind==CharacterActionRequirementKind.CooldownReady).Select(r=>r.ReferenceId).ToArray(),
                        TacticalActionValidation=selectable,ActionValidationIssue=selectionIssue,GateRemainingFrames=gateRemaining,GateOwner=direct.AbilityId,
                        SingleAccepted=single,SingleIssue=singleIssue,DifferentTemplateAccepted=otherTemplate,CombinationAccepted=multi,
                        AutomaticSingleAccepted=automaticSingle,AutomaticDifferentTemplateAccepted=automaticOther,AutomaticCombinationAccepted=automaticMulti });
                    checkedActions++; if(single||otherTemplate||multi) missedActions++;
                }
                Check(CharacterTestTacticalExecutionAdapter.ValidateBoundActionCooldowns(new Dictionary<string,int>(),actionRegistry,authority,1,RuntimeFrame.Zero,out _),distribution+": no configured action is allowed");
                Check(CharacterTestTacticalExecutionAdapter.ValidateBoundActionCooldowns(new Dictionary<string,int>{{"plain",301}},actionRegistry,authority,1,RuntimeFrame.Zero,out _),distribution+": no-owner plain action does not inherit other skill cooldowns");
                Check(CharacterTestRuntimeAiLabBehaviorCardRuntimeOverlay.RuntimeBridge.EvaluateConfiguredActionCooldowns(Array.Empty<int>(),null,null,1,RuntimeFrame.Zero).Available,
                    distribution+": automatic card with no configured action requires no authority");
                Console.WriteLine(distribution+": real cooling actions="+checkedActions+"; incorrectly admitted="+missedActions);
            }
            File.WriteAllText(Path.Combine(output,"actual-action-cooldown-results.json"),JsonConvert.SerializeObject(rows,Formatting.Indented));
            File.WriteAllText(Path.Combine(output,"assertions.json"),JsonConvert.SerializeObject(assertions,Formatting.Indented));
            Console.WriteLine("TOTAL passed="+passed+" failed="+failed);
            return failed==0?0:2;
        }
        catch(Exception ex){Console.WriteLine(ex);return 1;}
    }
}
