using System;

namespace MxFramework.Game.CharacterTest
{
    internal enum CharacterTestRuntimeAiLabPublishCurrentChangesPostTestChoice
    {
        None = 0,
        RestartPublishedVersion = 1,
        ContinueCurrentTest = 2
    }

    internal readonly struct CharacterTestRuntimeAiLabPublishCurrentChangesPostTestResult
    {
        internal CharacterTestRuntimeAiLabPublishCurrentChangesPostTestResult(
            bool success,
            CharacterTestRuntimeAiLabPublishCurrentChangesPostTestChoice choice,
            string code,
            string message,
            bool runtimeIdentityPreserved,
            bool duplicate)
        {
            Success = success;
            Choice = choice;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
            RuntimeIdentityPreserved = runtimeIdentityPreserved;
            Duplicate = duplicate;
        }

        internal bool Success { get; }
        internal CharacterTestRuntimeAiLabPublishCurrentChangesPostTestChoice Choice { get; }
        internal string Code { get; }
        internal string Message { get; }
        internal bool RuntimeIdentityPreserved { get; }
        internal bool Duplicate { get; }

        internal CharacterTestRuntimeAiLabPublishCurrentChangesPostTestResult AsDuplicate()
        {
            return new CharacterTestRuntimeAiLabPublishCurrentChangesPostTestResult(
                Success,
                Choice,
                Code,
                Message,
                RuntimeIdentityPreserved,
                duplicate: true);
        }

        internal static CharacterTestRuntimeAiLabPublishCurrentChangesPostTestResult Succeeded(
            CharacterTestRuntimeAiLabPublishCurrentChangesPostTestChoice choice,
            string message,
            bool runtimeIdentityPreserved)
        {
            return new CharacterTestRuntimeAiLabPublishCurrentChangesPostTestResult(
                success: true,
                choice,
                "PUBLISH_CURRENT_CHANGES_POST_TEST_SUCCEEDED",
                message,
                runtimeIdentityPreserved,
                duplicate: false);
        }

        internal static CharacterTestRuntimeAiLabPublishCurrentChangesPostTestResult Failed(
            CharacterTestRuntimeAiLabPublishCurrentChangesPostTestChoice choice,
            string code,
            string message)
        {
            return new CharacterTestRuntimeAiLabPublishCurrentChangesPostTestResult(
                success: false,
                choice,
                code,
                message,
                runtimeIdentityPreserved: false,
                duplicate: false);
        }
    }

    /// <summary>
    /// Makes the post-publish test choice one-shot without owning either Catalog writes or the
    /// CharacterTest reset algorithm. Continue is proven to be a no-op against the supplied
    /// runtime identity; restart is delegated to the formal F0 session-start adapter.
    /// </summary>
    /// <remarks>LowFreqAlloc: explicit post-publish UI choice only.</remarks>
    internal sealed class CharacterTestRuntimeAiLabPublishCurrentChangesPostTestCoordinator
    {
        private string _completedToken = string.Empty;
        private CharacterTestRuntimeAiLabPublishCurrentChangesPostTestChoice _completedChoice;
        private CharacterTestRuntimeAiLabPublishCurrentChangesPostTestResult _lastResult;

        internal CharacterTestRuntimeAiLabPublishCurrentChangesPostTestResult Choose(
            CharacterTestRuntimeAiLabPublishCurrentChangesResult publish,
            string previewToken,
            CharacterTestRuntimeAiLabPublishCurrentChangesPostTestChoice choice,
            Func<string> captureRuntimeIdentity,
            Func<CharacterTestRuntimeAiLabPublishCurrentChangesPostTestResult> restartPublished)
        {
            string normalizedToken = previewToken?.Trim() ?? string.Empty;
            if (publish?.Success != true
                || publish.Preview == null
                || normalizedToken.Length == 0
                || !string.Equals(
                    normalizedToken,
                    publish.Preview.Token,
                    StringComparison.Ordinal))
            {
                return CharacterTestRuntimeAiLabPublishCurrentChangesPostTestResult.Failed(
                    choice,
                    "PUBLISH_CURRENT_CHANGES_POST_TEST_TOKEN_INVALID",
                    "发布结果已经失效；当前测试没有改变。");
            }

            if (string.Equals(normalizedToken, _completedToken, StringComparison.Ordinal))
            {
                if (choice == _completedChoice)
                    return _lastResult.AsDuplicate();
                return CharacterTestRuntimeAiLabPublishCurrentChangesPostTestResult.Failed(
                    choice,
                    "PUBLISH_CURRENT_CHANGES_POST_TEST_ALREADY_CHOSEN",
                    "本次发布已经选择了测试处理方式；当前测试没有再次改变。");
            }

            CharacterTestRuntimeAiLabPublishCurrentChangesPostTestResult result;
            switch (choice)
            {
                case CharacterTestRuntimeAiLabPublishCurrentChangesPostTestChoice.ContinueCurrentTest:
                    string before = captureRuntimeIdentity?.Invoke() ?? string.Empty;
                    string after = captureRuntimeIdentity?.Invoke() ?? string.Empty;
                    if (before.Length == 0
                        || after.Length == 0
                        || !string.Equals(before, after, StringComparison.Ordinal))
                    {
                        return CharacterTestRuntimeAiLabPublishCurrentChangesPostTestResult.Failed(
                            choice,
                            "PUBLISH_CURRENT_CHANGES_CONTINUE_IDENTITY_CHANGED",
                            "无法确认当前测试保持不变；请查看详情后重试。");
                    }
                    result = CharacterTestRuntimeAiLabPublishCurrentChangesPostTestResult.Succeeded(
                        choice,
                        "已继续当前测试；当前测试仍为发布前版本。",
                        runtimeIdentityPreserved: true);
                    break;
                case CharacterTestRuntimeAiLabPublishCurrentChangesPostTestChoice.RestartPublishedVersion:
                    result = restartPublished != null
                        ? restartPublished()
                        : CharacterTestRuntimeAiLabPublishCurrentChangesPostTestResult.Failed(
                            choice,
                            "PUBLISH_CURRENT_CHANGES_RESTART_UNAVAILABLE",
                            "新版本已发布，但当前测试无法重新开始。");
                    break;
                default:
                    return CharacterTestRuntimeAiLabPublishCurrentChangesPostTestResult.Failed(
                        choice,
                        "PUBLISH_CURRENT_CHANGES_POST_TEST_CHOICE_INVALID",
                        "请选择如何处理当前测试。");
            }

            if (!result.Success)
                return result;

            _completedToken = normalizedToken;
            _completedChoice = choice;
            _lastResult = result;
            return result;
        }
    }
}
