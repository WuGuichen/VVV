using MCPForUnity.Editor.Services.Server;
using NUnit.Framework;

namespace MCPForUnityTests.EditMode
{
    public sealed class PidFileManagerTests
    {
        private PidFileManager _firstProject;
        private PidFileManager _secondProject;

        [SetUp]
        public void SetUp()
        {
            _firstProject = new PidFileManager("C:/McpForUnityTests/ProjectA");
            _secondProject = new PidFileManager("C:/McpForUnityTests/ProjectB");
            _firstProject.ClearTracking();
            _secondProject.ClearTracking();
        }

        [TearDown]
        public void TearDown()
        {
            _firstProject.ClearTracking();
            _secondProject.ClearTracking();
        }

        [Test]
        public void Handshake_IsVisibleOnlyToOwningProject()
        {
            _firstProject.StoreHandshake("C:/ProjectA/Library/MCPForUnity/RunState/mcp_http_8080.pid", "owner-a");

            Assert.That(_firstProject.TryGetHandshake(out var pidFile, out var token), Is.True);
            Assert.That(pidFile, Does.Contain("ProjectA"));
            Assert.That(token, Is.EqualTo("owner-a"));
            Assert.That(_secondProject.TryGetHandshake(out _, out _), Is.False);
        }

        [Test]
        public void Tracking_IsVisibleOnlyToOwningProject()
        {
            _firstProject.StoreTracking(12345, 8080, "hash-a");

            Assert.That(_firstProject.TryGetStoredPid(8080, out var pid), Is.True);
            Assert.That(pid, Is.EqualTo(12345));
            Assert.That(_firstProject.GetStoredArgsHash(), Is.EqualTo("hash-a"));
            Assert.That(_secondProject.TryGetStoredPid(8080, out _), Is.False);
            Assert.That(_secondProject.GetStoredArgsHash(), Is.Empty);
        }

        [Test]
        public void ClearingOneProject_DoesNotClearAnotherProject()
        {
            _firstProject.StoreHandshake("C:/ProjectA/mcp_http_8080.pid", "owner-a");
            _secondProject.StoreHandshake("C:/ProjectB/mcp_http_8080.pid", "owner-b");

            _firstProject.ClearTracking();

            Assert.That(_firstProject.TryGetHandshake(out _, out _), Is.False);
            Assert.That(_secondProject.TryGetHandshake(out _, out var token), Is.True);
            Assert.That(token, Is.EqualTo("owner-b"));
        }
    }
}
