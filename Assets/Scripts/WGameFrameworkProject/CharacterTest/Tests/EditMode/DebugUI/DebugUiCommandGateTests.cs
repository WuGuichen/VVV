using System;
using System.Collections.Generic;
using MxFramework.DebugUI;
using MxFramework.Diagnostics;
using NUnit.Framework;

namespace MxFramework.Tests.DebugUI
{
    public sealed class DebugUiCommandGateTests
    {
        [Test]
        public void Execute_DefaultGateRejectsCommands()
        {
            var gate = new DebugUiCommandGate(new TestCommandProvider());

            DebugUiCommandResult result = gate.Execute(new DebugUiCommandRequest("debug.low"));

            Assert.IsFalse(result.Success);
            Assert.AreEqual("disabled", result.ErrorCode);
            Assert.AreEqual(1, gate.Log.Count);
        }

        [Test]
        public void Execute_LowRiskCommandReturnsProviderResult()
        {
            var gate = new DebugUiCommandGate(new TestCommandProvider(), new DebugUiCommandGateOptions { Enabled = true });

            DebugUiCommandResult result = gate.Execute(new DebugUiCommandRequest("debug.low"));

            Assert.IsTrue(result.Success);
            Assert.AreEqual("ran", result.Message);
        }

        [Test]
        public void Execute_DestructiveCommandRequiresExplicitGateAndConfirmation()
        {
            var gate = new DebugUiCommandGate(new TestCommandProvider(), new DebugUiCommandGateOptions { Enabled = true });

            DebugUiCommandResult blocked = gate.Execute(new DebugUiCommandRequest("debug.destroy", confirmed: true));
            gate.Options.AllowDestructiveCommands = true;
            DebugUiCommandResult needsConfirm = gate.Execute(new DebugUiCommandRequest("debug.destroy"));
            DebugUiCommandResult accepted = gate.Execute(new DebugUiCommandRequest("debug.destroy", confirmed: true));

            Assert.AreEqual("destructive_disabled", blocked.ErrorCode);
            Assert.AreEqual("confirmation_required", needsConfirm.ErrorCode);
            Assert.IsTrue(accepted.Success);
        }

        [Test]
        public void DebugSource_ExportsCommandLogWithoutThrowing()
        {
            var gate = new DebugUiCommandGate(new TestCommandProvider(), new DebugUiCommandGateOptions { Enabled = true });
            gate.Execute(new DebugUiCommandRequest("debug.throw"));

            FrameworkDebugSnapshot snapshot = new DebugUiCommandGateDebugSource(gate).CreateSnapshot();

            Assert.That(snapshot.Sections[0].Body, Does.Contain("debug.low"));
            Assert.That(snapshot.Sections[1].Body, Does.Contain("InvalidOperationException"));
        }

        private sealed class TestCommandProvider : IDebugUiCommandProvider
        {
            private readonly List<DebugUiCommandDescriptor> _commands = new List<DebugUiCommandDescriptor>
            {
                new DebugUiCommandDescriptor("debug.low", "Low", "Low risk", DebugUiCommandRisk.Low, requiresConfirmation: false),
                new DebugUiCommandDescriptor("debug.destroy", "Destroy", "Destructive", DebugUiCommandRisk.Destructive, requiresConfirmation: true),
                new DebugUiCommandDescriptor("debug.throw", "Throw", "Throws", DebugUiCommandRisk.Low, requiresConfirmation: false)
            };

            public IReadOnlyList<DebugUiCommandDescriptor> Commands => _commands;

            public bool TryGetCommand(string commandId, out DebugUiCommandDescriptor descriptor)
            {
                for (int i = 0; i < _commands.Count; i++)
                {
                    if (_commands[i].CommandId == commandId)
                    {
                        descriptor = _commands[i];
                        return true;
                    }
                }

                descriptor = null;
                return false;
            }

            public DebugUiCommandResult Execute(DebugUiCommandRequest request)
            {
                if (request.CommandId == "debug.throw")
                    throw new InvalidOperationException("boom");

                return DebugUiCommandResult.Succeeded(request.CommandId, "ran", request.TraceId);
            }
        }
    }
}
