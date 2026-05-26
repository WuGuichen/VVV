using MxFramework.Config.Runtime;
using MxFramework.Demo;
using MxFramework.Gameplay;
using MxFramework.Runtime;
using MxFramework.UI;
using NUnit.Framework;

namespace MxFramework.Tests.Ability
{
    public sealed class RuntimeAbilitySliceHudViewModelTests
    {
        [Test]
        public void Build_EmptyInput_ReturnsUiNeutralWaitingModel()
        {
            RuntimeAbilitySliceHudViewModel view = RuntimeAbilitySliceHudViewModelBuilder.Build(new RuntimeAbilitySliceHudBuilderInput());

            Assert.AreEqual("MxFramework RuntimeAbilitySlice", view.Title);
            Assert.AreEqual("Hardcoded Ability", view.ModeName);
            Assert.That(view.SnapshotSummary, Does.Contain("Waiting"));
            Assert.AreEqual("Player: waiting", view.Feedback.PlayerStatusText);
            Assert.AreEqual("Enemy: waiting", view.Feedback.EnemyStatusText);
            Assert.AreEqual(2, view.Commands.Count);
            Assert.AreEqual(RuntimeAbilitySliceHudCommandIds.Strike, view.Commands[0].CommandId);
            Assert.IsFalse(view.Commands[0].Enabled);
            Assert.AreEqual(RuntimeAbilitySliceHudCommandIds.Reset, view.Commands[1].CommandId);
            Assert.IsFalse(view.Commands[1].Enabled);
            Assert.That(view.Diagnostic.HeaderText, Does.Contain("waiting"));
        }

        [Test]
        public void Build_MapsSnapshotAndConfigSummaryWithoutToolkitViewModelDependency()
        {
            var snapshot = new GameplayDiagnosticSnapshot(
                "RuntimeAbilitySlice",
                "BasicAbilityConfig -> ConfigAbilityFactory",
                new[]
                {
                    new GameplayEntitySnapshot(
                        2,
                        2,
                        true,
                        new[] { new GameplayAttributeSnapshot(1, 565) },
                        new GameplayBuffSnapshot[0],
                        new GameplayModifierSnapshot[0])
                },
                new GameplayAbilityCastSnapshot(
                    "BasicAbilityConfig -> ConfigAbilityFactory",
                    false,
                    "NoValidTargets",
                    new int[0]),
                new[]
                {
                    new GameplayAbilityEventSnapshot("CastFailed", 300001, 1, null, "NoValidTargets")
                },
                new[]
                {
                    new GameplayAttributeEventSnapshot(2, 1, 600, 565, -35, "AbilityBurningBuff")
                });
            var config = new RuntimeConfigChangeSummary("Showcase Patch", "RuntimeAbilitySliceDemoData", "RebuildOnResolve");
            config.AddChangedAbility(300001);
            config.AddChangedBuff(100001);

            RuntimeAbilitySliceHudViewModel view = RuntimeAbilitySliceHudViewModelBuilder.Build(new RuntimeAbilitySliceHudBuilderInput
            {
                IsInitialized = true,
                UseConfigDriven = true,
                ConfigSummary = "source=Showcase Patch",
                ConfigModeStatus = "Config Driven",
                RuntimeConfigChangeSummaryText = config.ToSummaryText(),
                RuntimeConfigChangeSummary = config,
                Snapshot = snapshot
            });

            Assert.AreEqual("Config Driven Ability", view.ModeName);
            Assert.That(view.SnapshotSummary, Does.Contain("Entities 1"));
            Assert.That(view.Diagnostic.LastCastText, Does.Contain("NoValidTargets"));
            Assert.That(view.Diagnostic.ConfigSourceText, Does.Contain("Showcase Patch"));
            Assert.AreEqual(1, view.Diagnostic.EntitySummaryLines.Count);
            Assert.AreEqual(1, view.Diagnostic.AbilityEventSummaryLines.Count);
            Assert.AreEqual(1, view.Diagnostic.AttributeEventSummaryLines.Count);
            Assert.That(view.Diagnostic.ErrorSummaryText, Does.Contain("NoValidTargets"));
            Assert.IsFalse(view.Commands[0].Enabled);
            Assert.IsTrue(view.Commands[1].Enabled);
        }

        [Test]
        public void CommandSink_MapsRuntimeHudStrikeAndResetToManualCommands()
        {
            var target = new RecordingHudCommandTarget { IsInitializedValue = true, AutoSequenceEnabledValue = true };
            var sink = new RuntimeAbilitySliceUiCommandSink(target);
            var viewId = new MxUiViewId("runtimeAbilitySliceHud");

            sink.Enqueue(new MxUiCommand(viewId, RuntimeAbilitySliceHudCommandIds.Strike, null));
            sink.Enqueue(new MxUiCommand(viewId, RuntimeAbilitySliceHudCommandIds.Reset, null));

            Assert.AreEqual(2, target.Commands.Count);
            Assert.AreEqual(RuntimeAbilitySliceHudManualCommand.Strike, target.Commands[0]);
            Assert.AreEqual(RuntimeAbilitySliceHudManualCommand.Reset, target.Commands[1]);
            Assert.AreEqual(1, target.SetAutoSequenceEnabledCalls);
            Assert.IsFalse(target.AutoSequenceEnabledValue);
            Assert.AreEqual(2, sink.AcceptedCount);
            Assert.AreEqual(0, sink.RejectedCount);
            Assert.IsTrue(sink.LastResult.Success);
        }

        [Test]
        public void CommandSink_RejectsUnknownCommandWithoutCallingTarget()
        {
            var target = new RecordingHudCommandTarget { IsInitializedValue = true };
            var sink = new RuntimeAbilitySliceUiCommandSink(target);

            sink.Enqueue(new MxUiCommand(new MxUiViewId("runtimeAbilitySliceHud"), "runtimeHud.unknown", null));

            Assert.AreEqual(0, target.Commands.Count);
            Assert.AreEqual(0, sink.AcceptedCount);
            Assert.AreEqual(1, sink.RejectedCount);
            Assert.IsFalse(sink.LastResult.Success);
            Assert.That(sink.LastResult.Error.Message, Does.Contain("not mapped"));
        }

        private sealed class RecordingHudCommandTarget : IRuntimeAbilitySliceHudCommandTarget
        {
            public readonly System.Collections.Generic.List<RuntimeAbilitySliceHudManualCommand> Commands =
                new System.Collections.Generic.List<RuntimeAbilitySliceHudManualCommand>();

            public bool IsInitializedValue { get; set; }
            public bool AutoSequenceEnabledValue { get; set; }
            public int SetAutoSequenceEnabledCalls { get; private set; }

            public bool IsInitialized => IsInitializedValue;
            public bool AutoSequenceEnabled => AutoSequenceEnabledValue;

            public void SetAutoSequenceEnabled(bool enabled)
            {
                SetAutoSequenceEnabledCalls++;
                AutoSequenceEnabledValue = enabled;
            }

            public RuntimeCommandValidationResult EnqueueHudCommand(RuntimeAbilitySliceHudManualCommand command)
            {
                Commands.Add(command);
                return RuntimeCommandValidationResult.Accepted(new RuntimeCommand(
                    RuntimeFrame.Zero,
                    100,
                    (int)command,
                    0,
                    0,
                    0,
                    0,
                    "test"));
            }
        }
    }
}
