using System;
using System.Reflection;
using MxFramework.CharacterAction;
using MxFramework.Game.CharacterTest;
using MxFramework.Game.CharacterTest.TacticalCard;
using MxFramework.Gameplay;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.Game.CharacterTest
{
    // Native Editor tests. Reuses existing fixture IDs, actual action 301 tracks and staged gateway.
    // Delivery includes compilation; execution requires a separately granted Editor window.
    public sealed partial class CharacterTestActionUseAuthorityTests
    {
        private static AbilityRuntimeModule TransactionOwner(long cooldown=60)
        {
            var registry=new AbilityDefinitionRegistry();Assert.IsTrue(registry.TryRegister(new AbilityDefinitionConfig(ControlledAbilityId,OwnerId,"Transaction fixture",null,
                new AbilityCooldownConfig(cooldown,0),new[]{new AbilityCostConfig(CharacterTestCharacterAttributeIds.Motion,20),new AbilityCostConfig(CharacterTestCharacterAttributeIds.Hp,10)},
                0,new AbilityActionBindingConfig(new CharacterActionId(301),MigrationFixtureIds.Id0030),default,0,0),out _));
            return new AbilityRuntimeModule(registry);
        }
        private static MomentumLedger BindTransactionResources(CharacterTestTacticalCardRuntimeFixture fixture)
        {
            var motion=new MomentumLedger(50);fixture.Runtime.World.BindTacticalMotion(fixture.Source.Id.Value,motion);
            fixture.Runtime.World.SetCurrentAttribute(fixture.Source.GameplayEntityId,CharacterTestCharacterAttributeIds.Hp,30);
            return motion;
        }
        [Test]public void ActualActionOwnerCombinedCardQueryPaysNothingAndUsesCurrentBoundResources()
        {
            var ability=TransactionOwner();using(var fixture=new CharacterTestTacticalCardRuntimeFixture(actionId:301,actionRuntime:RestrictedRuntime(),abilityRuntime:ability))
            {
                var motion=BindTransactionResources(fixture);Assert.IsTrue(fixture.Runtime.ActionRuntime.Registry.TryGetAction(new CharacterActionId(301),out var action));
                for(int i=0;i<100;i++)Assert.IsTrue(fixture.Runtime.ActionUseAuthority.EvaluateCardCosts(fixture.Source.Id.Value,action,30,out int cost).Available&&cost==20);
                Assert.IsFalse(fixture.Runtime.ActionUseAuthority.EvaluateCardCosts(fixture.Source.Id.Value,action,31,out _).Available);
                Assert.AreEqual(50,motion.Current);Assert.AreEqual(30,fixture.Runtime.World.GetCurrentAttribute(fixture.Source,CharacterTestCharacterAttributeIds.Hp));Assert.AreEqual(0,ability.ActiveCooldownCount);
                Assert.IsTrue(motion.TryReserve(30,out var token));Assert.IsTrue(fixture.Runtime.ActionUseAuthority.EvaluateCardCosts(fixture.Source.Id.Value,action,0,out _).Available);
                Assert.IsTrue(fixture.Runtime.ActionUseAuthority.EvaluateCardCosts(fixture.Source.Id.Value,action,25,out _,30).Available);Assert.IsTrue(motion.ReleaseReservation(token));
            }
        }
        [Test]public void ActualStagedActionOwnerPaysMotionHpAndStartsCd60AtSuccessfulCommit()
        {
            var ability=TransactionOwner();using(var fixture=new CharacterTestTacticalCardRuntimeFixture(actionId:301,actionRuntime:RestrictedRuntime(),abilityRuntime:ability))
            {
                var motion=BindTransactionResources(fixture);var frame=new RuntimeFrame(fixture.NextFrame);Assert.IsTrue(fixture.Runtime.ActionRuntime.Registry.TryGetAction(new CharacterActionId(301),out var action));
                var result=fixture.Runtime.ActionUseAuthority.TryCommit(fixture.Source,action,frame,fixture.Facts);Assert.IsTrue(result.Committed,result.Reason);
                Assert.AreEqual(30,motion.Current);Assert.AreEqual(20,fixture.Runtime.World.GetCurrentAttribute(fixture.Source,CharacterTestCharacterAttributeIds.Hp));
                Assert.IsTrue(ability.Definitions.TryGet(ControlledAbilityId,out var owner));Assert.AreEqual(60,ability.GetRemainingCooldownFrames(fixture.Source.Id.Value,owner,frame));
                Assert.IsTrue(fixture.Runtime.ActionRuntime.TryGetActiveActionPresentationState(fixture.Source.Id,out long instance,out var id,out _,out _));Assert.AreEqual(301,id.Value);
                Assert.IsFalse(fixture.Runtime.ActionUseAuthority.TryCommit(fixture.Source,action,frame,fixture.Facts).Committed);
                Assert.AreEqual(30,motion.Current);Assert.AreEqual(20,fixture.Runtime.World.GetCurrentAttribute(fixture.Source,CharacterTestCharacterAttributeIds.Hp));
                Assert.IsTrue(fixture.Runtime.ActionRuntime.TryGetActiveActionPresentationState(fixture.Source.Id,out long after,out _,out _,out _));Assert.AreEqual(instance,after);
            }
        }
        private sealed class TransactionReceipts:ICharacterTestBehaviorReceiptObserver
        {internal int Count;public void OnBehaviorOrchestrationReceipt(in CharacterTestBehaviorOrchestrationReceipt receipt){Count++;}}
        [TestCase(0)] [TestCase(60)]public void ActualBehaviorCommandReceiptReplayKeepsInstancePaymentCooldownAndObserverOnce(long cooldown)
        {
            var ability=TransactionOwner(cooldown);using(var fixture=new CharacterTestTacticalCardRuntimeFixture(actionId:301,actionRuntime:RestrictedRuntime(),abilityRuntime:ability))
            {
                var motion=BindTransactionResources(fixture);var frame=new RuntimeFrame(fixture.NextFrame);var trace=CharacterTestAiTraceKinds.AtFrame(CharacterTestAiTraceKinds.ExecutionAction,fixture.Source.Id.Value,frame);
                var receipts=new TransactionReceipts();fixture.Runtime.RegisterBehaviorReceiptObserver(receipts);
                Assert.IsTrue(fixture.Runtime.TryApplyBehaviorRecipeOrchestration(frame,fixture.Source.Id.Value,CharacterTestBehaviorRecipeOrchestrationOperation.ReplaceCharacterAction,301,trace,1,1,out string message),message);
                Assert.IsTrue(fixture.Runtime.ActionRuntime.TryGetActiveActionPresentationState(fixture.Source.Id,out long instance,out _,out _,out _));
                long commits=fixture.Runtime.ActionUseAuthority.CommitCount;
                Assert.IsTrue(fixture.Runtime.TryApplyBehaviorRecipeOrchestration(frame,fixture.Source.Id.Value,CharacterTestBehaviorRecipeOrchestrationOperation.ReplaceCharacterAction,301,trace,1,1,out _));
                Assert.IsTrue(fixture.Runtime.ActionRuntime.TryGetActiveActionPresentationState(fixture.Source.Id,out long after,out _,out _,out _));Assert.AreEqual(instance,after);
                Assert.AreEqual(commits,fixture.Runtime.ActionUseAuthority.CommitCount);Assert.AreEqual(1,receipts.Count);Assert.AreEqual(30,motion.Current);
                Assert.AreEqual(20,fixture.Runtime.World.GetCurrentAttribute(fixture.Source,CharacterTestCharacterAttributeIds.Hp));
                Assert.IsTrue(ability.Definitions.TryGet(ControlledAbilityId,out var owner));Assert.AreEqual(cooldown,ability.GetRemainingCooldownFrames(fixture.Source.Id.Value,owner,frame));
            }
        }
        [Test]public void ActualDifferentSameFrameActionCommandsAreNotMistakenForOneTraceCause()
        {
            var ability=TransactionOwner(0);using(var fixture=new CharacterTestTacticalCardRuntimeFixture(actionId:301,actionRuntime:RestrictedRuntime(),abilityRuntime:ability))
            {
                var motion=BindTransactionResources(fixture);fixture.Runtime.World.SetCurrentAttribute(fixture.Source.GameplayEntityId,CharacterTestCharacterAttributeIds.Hp,50);
                var frame=new RuntimeFrame(fixture.NextFrame);var trace=CharacterTestAiTraceKinds.AtFrame(CharacterTestAiTraceKinds.ExecutionAction,fixture.Source.Id.Value,frame);
                Assert.IsTrue(fixture.Runtime.TryApplyBehaviorRecipeOrchestration(frame,fixture.Source.Id.Value,CharacterTestBehaviorRecipeOrchestrationOperation.ReplaceCharacterAction,301,trace,1,1,out string message),message);
                Assert.IsTrue(fixture.Runtime.TryApplyBehaviorRecipeOrchestration(frame,fixture.Source.Id.Value,CharacterTestBehaviorRecipeOrchestrationOperation.ReplaceCharacterAction,301,trace,2,1,out message),message);
                Assert.AreEqual(10,motion.Current);Assert.AreEqual(30,fixture.Runtime.World.GetCurrentAttribute(fixture.Source,CharacterTestCharacterAttributeIds.Hp));
            }
        }
        [Test]public void ActualFailedCommandReceiptCannotRetryImplicitlyAfterResourceGrant()
        {
            var ability=TransactionOwner(0);using(var fixture=new CharacterTestTacticalCardRuntimeFixture(actionId:301,actionRuntime:RestrictedRuntime(),abilityRuntime:ability))
            {
                var motion=BindTransactionResources(fixture);Assert.IsTrue(motion.TrySpend(40));var frame=new RuntimeFrame(fixture.NextFrame);var trace=default(RuntimeTraceIdentity);
                Assert.IsFalse(fixture.Runtime.TryApplyBehaviorRecipeOrchestration(frame,fixture.Source.Id.Value,CharacterTestBehaviorRecipeOrchestrationOperation.ReplaceCharacterAction,301,trace,1,1,out _));
                motion.Grant(40);long count=fixture.Runtime.ActionUseAuthority.CommitCount;
                Assert.IsFalse(fixture.Runtime.TryApplyBehaviorRecipeOrchestration(frame,fixture.Source.Id.Value,CharacterTestBehaviorRecipeOrchestrationOperation.ReplaceCharacterAction,301,trace,1,1,out _));
                Assert.AreEqual(50,motion.Current);Assert.AreEqual(count,fixture.Runtime.ActionUseAuthority.CommitCount);Assert.AreEqual(0,ability.ActiveCooldownCount);
            }
        }
        [Test]public void ActualConsumedCommandRoutePassesBufferIdentityToTheExistingReceiptOwner()
        {
            var ability=TransactionOwner(0);using(var fixture=new CharacterTestTacticalCardRuntimeFixture(actionId:301,actionRuntime:RestrictedRuntime(),abilityRuntime:ability))
            {
                var motion=BindTransactionResources(fixture);var frame=new RuntimeFrame(fixture.NextFrame);var receipts=new TransactionReceipts();fixture.Runtime.RegisterBehaviorReceiptObserver(receipts);
                var command=CharacterTestRuntimeCommandFactory.BehaviorRecipeOrchestration(frame,1,fixture.Source.Id.Value,CharacterTestBehaviorRecipeOrchestrationOperation.ReplaceCharacterAction,301).WithSequence(1);
                var apply=typeof(CharacterTestRuntimeCommandModule).GetMethod("ApplyBehaviorRecipeOrchestration",BindingFlags.Instance|BindingFlags.NonPublic);
                apply.Invoke(fixture.Commands,new object[]{command});long commits=fixture.Runtime.ActionUseAuthority.CommitCount;
                apply.Invoke(fixture.Commands,new object[]{command});Assert.AreEqual(30,motion.Current);Assert.AreEqual(commits,fixture.Runtime.ActionUseAuthority.CommitCount);Assert.AreEqual(1,receipts.Count);
            }
        }
        [Test]public void ActualRollbackCommandResetResumesItsInclusiveFirstReplayFrame()
        {
            var ability=TransactionOwner(0);using(var fixture=new CharacterTestTacticalCardRuntimeFixture(actionId:301,actionRuntime:RestrictedRuntime(),abilityRuntime:ability))
            {
                var motion=BindTransactionResources(fixture);fixture.Runtime.World.SetCurrentAttribute(fixture.Source.GameplayEntityId,CharacterTestCharacterAttributeIds.Hp,50);
                var frame=new RuntimeFrame(fixture.NextFrame);Assert.IsTrue(fixture.Runtime.TryApplyBehaviorRecipeOrchestration(frame,fixture.Source.Id.Value,CharacterTestBehaviorRecipeOrchestrationOperation.ReplaceCharacterAction,301,default,1,1,out _));
                // Only the receipt reset seam is tested here; the complete world restore belongs to the existing network owner.
                motion.Grant(20);fixture.Runtime.World.SetCurrentAttribute(fixture.Source.GameplayEntityId,CharacterTestCharacterAttributeIds.Hp,50);
                fixture.Commands.ResetForRollbackReplay(frame);
                Assert.IsTrue(fixture.Runtime.TryApplyBehaviorRecipeOrchestration(frame,fixture.Source.Id.Value,CharacterTestBehaviorRecipeOrchestrationOperation.ReplaceCharacterAction,301,default,1,1,out _));
                Assert.IsTrue(fixture.Runtime.TryApplyBehaviorRecipeOrchestration(frame,fixture.Source.Id.Value,CharacterTestBehaviorRecipeOrchestrationOperation.ReplaceCharacterAction,301,default,1,1,out _));
                Assert.AreEqual(30,motion.Current);
            }
        }
    }
}
