using System;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.Runtime
{
    public class RuntimeOperationTests
    {
        [Test]
        public void Constructor_CreatesPendingOperation()
        {
            var operation = new RuntimeOperation("resource-warmup");

            Assert.AreEqual("resource-warmup", operation.OperationId);
            Assert.AreEqual(RuntimeOperationStatus.Pending, operation.Status);
            Assert.AreEqual(0f, operation.Progress);
            Assert.IsTrue(operation.Error.IsNone);
            Assert.IsFalse(operation.IsTerminal);
        }

        [Test]
        public void Constructor_WithEmptyOperationIdThrows()
        {
            Assert.Throws<ArgumentException>(() => new RuntimeOperation(string.Empty));
            Assert.Throws<ArgumentException>(() => new RuntimeOperation(" "));
        }

        [Test]
        public void StartAndReportProgress_UpdatesRunningProgress()
        {
            var operation = new RuntimeOperation("scene-load");

            operation.Start();
            operation.ReportProgress(0.5f);

            Assert.AreEqual(RuntimeOperationStatus.Running, operation.Status);
            Assert.AreEqual(0.5f, operation.Progress);
            Assert.IsTrue(operation.Error.IsNone);
        }

        [Test]
        public void Succeed_CompletesOperation()
        {
            var operation = new RuntimeOperation("preview-request");
            operation.Start();
            operation.ReportProgress(0.25f);

            operation.Succeed();

            Assert.AreEqual(RuntimeOperationStatus.Succeeded, operation.Status);
            Assert.AreEqual(1f, operation.Progress);
            Assert.IsTrue(operation.Error.IsNone);
            Assert.IsTrue(operation.IsTerminal);
        }

        [Test]
        public void Fail_StoresError()
        {
            var operation = new RuntimeOperation("bundle-download");
            var error = new RuntimeOperationError("download.failed", "Bundle download failed.");
            operation.Start();
            operation.ReportProgress(0.75f);

            operation.Fail(error);

            Assert.AreEqual(RuntimeOperationStatus.Failed, operation.Status);
            Assert.AreEqual(0.75f, operation.Progress);
            Assert.AreEqual("download.failed", operation.Error.Code);
            Assert.AreEqual("Bundle download failed.", operation.Error.Message);
        }

        [Test]
        public void Cancel_CompletesWithoutError()
        {
            var operation = new RuntimeOperation("audio-bank-load");
            operation.Start();
            operation.ReportProgress(0.1f);

            operation.Cancel();

            Assert.AreEqual(RuntimeOperationStatus.Cancelled, operation.Status);
            Assert.AreEqual(0.1f, operation.Progress);
            Assert.IsTrue(operation.Error.IsNone);
        }

        [Test]
        public void Timeout_CompletesWithTimeoutStatus()
        {
            var operation = new RuntimeOperation("editor-server-request");
            var error = new RuntimeOperationError("request.timeout", "Request timed out.");
            operation.Start();
            operation.ReportProgress(0.4f);

            operation.Timeout(error);

            Assert.AreEqual(RuntimeOperationStatus.TimedOut, operation.Status);
            Assert.AreEqual(0.4f, operation.Progress);
            Assert.AreEqual("request.timeout", operation.Error.Code);
        }

        [Test]
        public void ReportProgress_OutsideZeroToOneThrows()
        {
            var operation = new RuntimeOperation("mod-diagnosis");
            operation.Start();

            Assert.Throws<ArgumentOutOfRangeException>(() => operation.ReportProgress(-0.01f));
            Assert.Throws<ArgumentOutOfRangeException>(() => operation.ReportProgress(1.01f));
            Assert.Throws<ArgumentOutOfRangeException>(() => operation.ReportProgress(float.NaN));
        }

        [Test]
        public void TerminalOperation_IgnoresFurtherMutations()
        {
            var operation = new RuntimeOperation("resource-warmup");
            operation.Start();
            operation.ReportProgress(0.3f);
            operation.Fail(new RuntimeOperationError("warmup.failed", "Warmup failed."));

            operation.ReportProgress(0.9f);
            operation.Succeed();
            operation.Cancel();
            operation.Timeout(new RuntimeOperationError("timeout", "Too late."));
            operation.Fail(new RuntimeOperationError("other", "Other failure."));
            operation.Start();

            Assert.AreEqual(RuntimeOperationStatus.Failed, operation.Status);
            Assert.AreEqual(0.3f, operation.Progress);
            Assert.AreEqual("warmup.failed", operation.Error.Code);
            Assert.AreEqual("Warmup failed.", operation.Error.Message);
        }

        [Test]
        public void TerminalSuccess_IgnoresProgressMutation()
        {
            var operation = new RuntimeOperation("scene-flow");
            operation.Start();
            operation.Succeed();

            operation.ReportProgress(0.1f);

            Assert.AreEqual(RuntimeOperationStatus.Succeeded, operation.Status);
            Assert.AreEqual(1f, operation.Progress);
        }
    }
}
