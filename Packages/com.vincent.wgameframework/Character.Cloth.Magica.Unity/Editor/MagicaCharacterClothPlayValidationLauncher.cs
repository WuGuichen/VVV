using System;
using System.Collections.Generic;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace MxFramework.CharacterCloth.Magica.Unity.Editor
{
    internal sealed class MagicaCharacterClothPlayValidationLauncher : ICallbacks
    {
        internal const string TestName = "MxFramework.Tests.PlayMode.CharacterCloth.Magica.NumericCharacterMagicaClothPlayModeTests.GeneratedNumericCharacter_BoneClothBuildsAndChangesRuntimeState";
        private static MagicaCharacterClothPlayValidationLauncher s_current;
        private readonly TestRunnerApi _api;
        private readonly Action<bool, string> _completed;
        private readonly List<ITestResultAdaptor> _leaves = new List<ITestResultAdaptor>();

        private MagicaCharacterClothPlayValidationLauncher(Action<bool, string> completed)
        {
            _completed = completed;
            _api = ScriptableObject.CreateInstance<TestRunnerApi>();
            _api.hideFlags = HideFlags.HideAndDontSave;
            _api.RegisterCallbacks(this);
        }

        internal static bool IsRunning => s_current != null;

        internal static void Start(Action<bool, string> completed)
        {
            if (s_current != null) return;
            s_current = new MagicaCharacterClothPlayValidationLauncher(completed);
            try
            {
                var filter = new Filter { testMode = TestMode.PlayMode, testNames = new[] { TestName } };
                s_current._api.Execute(new ExecutionSettings(filter));
            }
            catch
            {
                s_current.Release();
                throw;
            }
        }

        public void RunStarted(ITestAdaptor testsToRun) => _leaves.Clear();
        public void TestStarted(ITestAdaptor test) { }
        public void TestFinished(ITestResultAdaptor result) { if (result != null && !result.HasChildren) _leaves.Add(result); }
        public void RunFinished(ITestResultAdaptor result)
        {
            bool passed = result != null && result.FailCount == 0 && result.PassCount > 0;
            string message = passed
                ? "Cloth PlayMode 验收通过。"
                : "Cloth PlayMode 验收失败：" + FirstFailure(result);
            Action<bool, string> callback = _completed;
            Release();
            callback?.Invoke(passed, message);
        }

        private string FirstFailure(ITestResultAdaptor root)
        {
            for (int i = 0; i < _leaves.Count; i++)
                if (!string.Equals(_leaves[i].ResultState, "Passed", StringComparison.OrdinalIgnoreCase))
                    return string.IsNullOrWhiteSpace(_leaves[i].Message) ? _leaves[i].FullName : _leaves[i].Message;
            return root?.Message ?? "Unknown";
        }

        private void Release()
        {
            _api.UnregisterCallbacks(this);
            UnityEngine.Object.DestroyImmediate(_api);
            if (ReferenceEquals(s_current, this)) s_current = null;
        }
    }
}
