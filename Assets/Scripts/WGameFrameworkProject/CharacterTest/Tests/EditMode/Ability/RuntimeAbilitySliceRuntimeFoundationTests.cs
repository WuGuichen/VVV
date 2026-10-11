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
        public void SaveResetLoadContinue_RestoresGameplayAttributeState()
        {
            RuntimeAbilitySliceRunner runner = CreateRunner();
            runner.SetAutoSequenceEnabled(false);
            runner.SetLiveTickEnabled(false);
            int initialEnemyHp = runner.Enemy.Store.GetAttribute(AttrHp);

            Assert.IsTrue(runner.EnqueueManualCommand(MxRuntimeHudManualCommand.Strike).Success);
            runner.RunRuntimeFrame(0f);
            int damagedEnemyHp = runner.Enemy.Store.GetAttribute(AttrHp);
            Assert.AreEqual(initialEnemyHp - 110, damagedEnemyHp);

            RuntimeSaveStateResult<RuntimeSaveState> saveResult = runner.SaveRuntimeState();
            Assert.IsTrue(saveResult.Success);

            runner.ResetDemo();
            Assert.AreEqual(initialEnemyHp, runner.Enemy.Store.GetAttribute(AttrHp));

            RuntimeSaveStateResult<bool> restoreResult = runner.RestoreSaveState(saveResult.Value);
            Assert.IsTrue(restoreResult.Success);
            Assert.AreEqual(damagedEnemyHp, runner.Enemy.Store.GetAttribute(AttrHp));

            Assert.IsTrue(runner.EnqueueManualCommand(MxRuntimeHudManualCommand.Strike).Success);
            runner.RunRuntimeFrame(0f);

            Assert.AreEqual(damagedEnemyHp - 110, runner.Enemy.Store.GetAttribute(AttrHp));
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
