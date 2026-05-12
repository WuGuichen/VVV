using MxFramework.Demo;
using MxFramework.Runtime;
using MxFramework.UI.Toolkit;
using NUnit.Framework;
using UnityEngine;

namespace MxFramework.Tests.Ability
{
    public sealed class RuntimeAbilitySliceRuntimeFoundationTests
    {
        private const int AttrHp = 1;
        private const int BuffBurning = 100001;
        private GameObject _runnerObject;

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_runnerObject);
        }

        [Test]
        public void ManualHudCommand_EnqueuesAndExecutesThroughRuntimeHostFrame()
        {
            RuntimeAbilitySliceRunner runner = CreateRunner();
            runner.SetAutoSequenceEnabled(false);
            runner.SetLiveTickEnabled(false);
            int enemyHpBefore = runner.Enemy.Store.GetAttribute(AttrHp);

            RuntimeCommandValidationResult result = runner.EnqueueManualCommand(MxRuntimeHudManualCommand.Strike);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(1, runner.PendingRuntimeCommandCount);

            runner.RunRuntimeFrame(0f);

            Assert.AreEqual(0, runner.PendingRuntimeCommandCount);
            Assert.AreEqual(enemyHpBefore - 110, runner.Enemy.Store.GetAttribute(AttrHp));
            Assert.AreEqual(1, runner.RuntimeReplaySnapshot.Count);
            Assert.AreNotEqual(0L, runner.LastRuntimeResultHash);
            Assert.That(runner.RuntimeFoundationSummary, Does.Contain("commands=1"));
        }

        [Test]
        public void SaveResetLoadContinue_RestoresGameplayBuffState()
        {
            RuntimeAbilitySliceRunner runner = CreateRunner();
            runner.SetAutoSequenceEnabled(false);
            runner.SetLiveTickEnabled(false);

            Assert.IsTrue(runner.EnqueueManualCommand(MxRuntimeHudManualCommand.ApplyBuff).Success);
            runner.RunRuntimeFrame(0f);
            Assert.AreEqual(1, runner.Enemy.Buffs.GetBuffLayer(BuffBurning));

            RuntimeSaveStateResult<RuntimeSaveState> saveResult = runner.SaveRuntimeState();
            Assert.IsTrue(saveResult.Success);

            runner.ResetDemo();
            Assert.AreEqual(0, runner.Enemy.Buffs.GetBuffLayer(BuffBurning));

            RuntimeSaveStateResult<bool> restoreResult = runner.RestoreSaveState(saveResult.Value);
            Assert.IsTrue(restoreResult.Success);
            Assert.AreEqual(1, runner.Enemy.Buffs.GetBuffLayer(BuffBurning));

            int enemyHpBeforeTick = runner.Enemy.Store.GetAttribute(AttrHp);
            runner.SetLiveTickEnabled(true);
            runner.RunRuntimeFrame(1f);

            Assert.AreEqual(enemyHpBeforeTick - 35, runner.Enemy.Store.GetAttribute(AttrHp));
        }

        private RuntimeAbilitySliceRunner CreateRunner()
        {
            _runnerObject = new GameObject("RuntimeAbilitySliceRuntimeFoundationTests");
            RuntimeAbilitySliceRunner runner = _runnerObject.AddComponent<RuntimeAbilitySliceRunner>();
            runner.ResetDemo();
            return runner;
        }
    }
}
