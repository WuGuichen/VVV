using System;
using MxFramework.ApplicationStartup;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.ApplicationStartup
{
    public class ApplicationStartupPlanTests
    {
        [Test]
        public void TryCreate_RegistrationPermutationUsesStableTopologicalOrder()
        {
            var a = new FakeStep("a");
            var b = new FakeStep("b");
            var c = new FakeStep("c");
            var descriptors = new[]
            {
                Descriptor("c", new[] { Id("a") }, 0),
                Descriptor("b", null, 10),
                Descriptor("a", null, 10)
            };

            bool success = ApplicationStartupPlan.TryCreate(
                new ApplicationStartupPlanId("boot"),
                descriptors,
                new IApplicationStartupStep[] { c, b, a },
                new FakeTransactionFactory(),
                out ApplicationStartupPlan plan,
                out ApplicationStartupError error);

            Assert.IsTrue(success, error.Message);
            Assert.AreEqual("a", plan.GetDescriptorInExecutionOrder(0).StepId.Value);
            Assert.AreEqual("c", plan.GetDescriptorInExecutionOrder(1).StepId.Value);
            Assert.AreEqual("b", plan.GetDescriptorInExecutionOrder(2).StepId.Value);
        }

        [Test]
        public void TryCreate_DuplicateMissingAndCycleFailStructurally()
        {
            AssertPlanError(
                new[] { Descriptor("a", null, 0), Descriptor("a", null, 1) },
                new IApplicationStartupStep[] { new FakeStep("a"), new FakeStep("a") },
                ApplicationStartupErrorCode.DuplicateStep);

            AssertPlanError(
                new[] { Descriptor("a", new[] { Id("missing") }, 0) },
                new IApplicationStartupStep[] { new FakeStep("a") },
                ApplicationStartupErrorCode.MissingDependency);

            AssertPlanError(
                new[] { Descriptor("a", new[] { Id("b") }, 0), Descriptor("b", new[] { Id("a") }, 0) },
                new IApplicationStartupStep[] { new FakeStep("a"), new FakeStep("b") },
                ApplicationStartupErrorCode.DependencyCycle);

            AssertPlanError(
                new[] { new ApplicationStartupStepDescriptor(Id("a"), null, 0, float.NaN) },
                new IApplicationStartupStep[] { new FakeStep("a") },
                ApplicationStartupErrorCode.InvalidWeight);
        }

        [Test]
        public void TryCreate_ExclusiveOwnerConflictFailsBeforeTransactionCreation()
        {
            var factory = new FakeTransactionFactory();
            var descriptors = new[]
            {
                new ApplicationStartupStepDescriptor(Id("a"), null, 0, 1f, 0d, "scene-pump"),
                new ApplicationStartupStepDescriptor(Id("b"), null, 0, 1f, 0d, "scene-pump")
            };

            bool success = ApplicationStartupPlan.TryCreate(
                new ApplicationStartupPlanId("boot"),
                descriptors,
                new IApplicationStartupStep[] { new FakeStep("a"), new FakeStep("b") },
                factory,
                out _,
                out ApplicationStartupError error);

            Assert.IsFalse(success);
            Assert.AreEqual(ApplicationStartupErrorCode.OwnerConflict, error.Code);
            Assert.AreEqual(0, factory.CreateCount);
        }

        private static void AssertPlanError(ApplicationStartupStepDescriptor[] descriptors, IApplicationStartupStep[] steps, ApplicationStartupErrorCode expected)
        {
            bool success = ApplicationStartupPlan.TryCreate(
                new ApplicationStartupPlanId("boot"), descriptors, steps, new FakeTransactionFactory(), out _, out ApplicationStartupError error);
            Assert.IsFalse(success);
            Assert.AreEqual(expected, error.Code);
        }

        internal static ApplicationStartupStepDescriptor Descriptor(string id, ApplicationStartupStepId[] dependencies, int order, double timeout = 0d)
        {
            return new ApplicationStartupStepDescriptor(Id(id), dependencies, order, 1f, timeout);
        }

        internal static ApplicationStartupStepId Id(string value) => new ApplicationStartupStepId(value);

        internal sealed class FakeStep : IApplicationStartupStep
        {
            public FakeStep(string id) => StepId = Id(id);
            public ApplicationStartupStepId StepId { get; }
            public IApplicationStartupStepOperation Begin(in ApplicationStartupStepContext context) => new FakeOperation(RuntimeOperationStatus.Succeeded, 1);
            public IApplicationStartupStepOperation BeginRollback(in ApplicationStartupRollbackContext context) => new FakeOperation(RuntimeOperationStatus.Succeeded, 1);
        }

        internal sealed class FakeTransactionFactory : IApplicationStartupTransactionFactory
        {
            public int CreateCount { get; private set; }
            public IApplicationStartupTransaction Create(ApplicationStartupPlanId planId)
            {
                CreateCount++;
                return new FakeTransaction();
            }
        }

        internal sealed class FakeTransaction : IApplicationStartupTransaction
        {
            public string TransactionId => "tx";
            public ApplicationStartupCommitObservation CommitObservation => ApplicationStartupCommitObservation.NotPublished;
            public ApplicationStartupCommitResult TryCommit(LiveApplicationState expected, ILiveApplicationStatePublisher publisher) => publisher.TryPublish(expected, LiveApplicationState.CreateCandidate(false, -1, new FakeLease()));
            public ApplicationStartupRollbackResult FinalizeRollback() => ApplicationStartupRollbackResult.Succeeded;
            public void Dispose() { }

            private sealed class FakeLease : IApplicationLiveSessionLease
            {
                public string SessionId => "plan";
                public void Dispose() { }
            }
        }

        internal sealed class FakeOperation : IApplicationStartupStepOperation
        {
            private readonly RuntimeOperationStatus _terminalStatus;
            private readonly int _ticksToTerminal;
            private readonly bool _throwOnCancel;
            private readonly bool _throwOnTickAfterTerminal;
            private readonly IApplicationStartupInnerErrorDetail _innerErrorDetail;
            private int _ticks;

            public FakeOperation(
                RuntimeOperationStatus terminalStatus,
                int ticksToTerminal,
                bool canCancel = true,
                bool cancelCompletes = true,
                bool throwOnCancel = false,
                bool throwOnTickAfterTerminal = false,
                IApplicationStartupInnerErrorDetail innerErrorDetail = null)
            {
                _terminalStatus = terminalStatus;
                _ticksToTerminal = ticksToTerminal;
                CanCancel = canCancel;
                CancelCompletes = cancelCompletes;
                _throwOnCancel = throwOnCancel;
                _throwOnTickAfterTerminal = throwOnTickAfterTerminal;
                _innerErrorDetail = innerErrorDetail;
                Status = RuntimeOperationStatus.Running;
            }

            public string OperationId => "fake";
            public RuntimeOperationStatus Status { get; private set; }
            public float Progress => _ticksToTerminal <= 0 ? 0f : System.Math.Min(1f, (float)_ticks / _ticksToTerminal);
            public RuntimeOperationError Error => Status == RuntimeOperationStatus.Failed ? new RuntimeOperationError("fake.failed", "Fake failure.") : default;
            public bool CanCancel { get; }
            public bool CancelCompletes { get; }
            public bool CancelWasRequested { get; private set; }
            public IApplicationStartupInnerErrorDetail InnerErrorDetail => _innerErrorDetail;

            public void CompleteExternally(RuntimeOperationStatus terminalStatus)
            {
                if (terminalStatus == RuntimeOperationStatus.Pending || terminalStatus == RuntimeOperationStatus.Running)
                {
                    throw new ArgumentOutOfRangeException(nameof(terminalStatus));
                }

                Status = terminalStatus;
            }

            public void Tick(in ApplicationStartupTickContext context)
            {
                if (_throwOnTickAfterTerminal && Status != RuntimeOperationStatus.Running)
                {
                    throw new InvalidOperationException("terminal operation was ticked");
                }

                if (Status != RuntimeOperationStatus.Running || _ticksToTerminal == int.MaxValue)
                {
                    return;
                }

                _ticks++;
                if (_ticks >= _ticksToTerminal)
                {
                    Status = _terminalStatus;
                }
            }

            public void RequestCancel()
            {
                CancelWasRequested = true;
                if (_throwOnCancel)
                {
                    throw new InvalidOperationException("cancel dispatch failed");
                }

                if (CanCancel && CancelCompletes)
                {
                    Status = RuntimeOperationStatus.Cancelled;
                }
            }
        }
    }
}
