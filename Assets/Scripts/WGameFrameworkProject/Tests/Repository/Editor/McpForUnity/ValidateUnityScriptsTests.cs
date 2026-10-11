using System;
using System.Reflection;
using MCPForUnity.Editor.Tools;
using NUnit.Framework;

namespace MCPForUnityTests.EditMode
{
    public sealed class ValidateUnityScriptsTests
    {
        [TearDown]
        public void TearDown()
        {
            ValidateUnityScripts.ClearStateForTests();
        }

        [Test]
        public void Tool_UsesSingleCallPollingAcrossDomainReload()
        {
            McpForUnityToolAttribute attribute = typeof(ValidateUnityScripts)
                .GetCustomAttribute<McpForUnityToolAttribute>();

            Assert.That(attribute, Is.Not.Null);
            Assert.That(attribute.Name, Is.EqualTo("validate_unity_scripts"));
            Assert.That(attribute.AutoRegister, Is.True);
            Assert.That(attribute.RequiresPolling, Is.True);
            Assert.That(attribute.PollAction, Is.EqualTo("status"));
            Assert.That(attribute.MaxPollSeconds, Is.GreaterThanOrEqualTo(ValidateUnityScripts.TimeoutSeconds));
        }

        [Test]
        public void Policy_RequiresObservedRefreshCompileReloadAndReadyState()
        {
            UnityScriptValidationState state = CompleteState();

            state.RefreshCompleted = false;
            Assert.That(Evaluate(state, true, 0), Is.EqualTo(UnityScriptValidationDecision.Pending));
            state.RefreshCompleted = true;

            state.CompilationStarted = false;
            Assert.That(Evaluate(state, true, 0), Is.EqualTo(UnityScriptValidationDecision.Pending));
            state.CompilationStarted = true;

            state.CompilationFinished = false;
            Assert.That(Evaluate(state, true, 0), Is.EqualTo(UnityScriptValidationDecision.Pending));
            state.CompilationFinished = true;

            state.DomainReloadObserved = false;
            Assert.That(Evaluate(state, true, 0), Is.EqualTo(UnityScriptValidationDecision.Pending));
            state.DomainReloadObserved = true;

            Assert.That(Evaluate(state, false, 0), Is.EqualTo(UnityScriptValidationDecision.Pending));
            Assert.That(Evaluate(state, true, 0), Is.EqualTo(UnityScriptValidationDecision.Passed));
        }

        [Test]
        public void Policy_FailsForCompilerOrConsoleErrors()
        {
            UnityScriptValidationState state = CompleteState();
            state.CompilationErrors.Add("error CS1002");

            Assert.That(
                Evaluate(state, true, 0),
                Is.EqualTo(UnityScriptValidationDecision.CompilationFailed));

            state.CompilationErrors.Clear();
            Assert.That(
                Evaluate(state, true, 1),
                Is.EqualTo(UnityScriptValidationDecision.ConsoleErrors));
        }

        [Test]
        public void State_TimeoutUsesPersistedUtcStartTime()
        {
            DateTime started = new DateTime(2026, 8, 8, 8, 0, 0, DateTimeKind.Utc);
            UnityScriptValidationState state = UnityScriptValidationState.Create("job", started);

            Assert.That(state.IsTimedOut(started.AddSeconds(119), 120), Is.False);
            Assert.That(state.IsTimedOut(started.AddSeconds(121), 120), Is.True);
        }

        private static UnityScriptValidationDecision Evaluate(
            UnityScriptValidationState state,
            bool editorReady,
            int consoleErrors)
        {
            return UnityScriptValidationPolicy.Evaluate(state, editorReady, consoleErrors);
        }

        private static UnityScriptValidationState CompleteState()
        {
            UnityScriptValidationState state = UnityScriptValidationState.Create("job", DateTime.UtcNow);
            state.RefreshRequested = true;
            state.RefreshCompleted = true;
            state.CompileRequested = true;
            state.CompilationStarted = true;
            state.CompilationFinished = true;
            state.DomainReloadObserved = true;
            return state;
        }
    }
}
