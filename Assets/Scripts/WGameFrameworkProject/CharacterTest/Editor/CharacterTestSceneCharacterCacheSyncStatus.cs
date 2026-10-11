using System;
using MxFramework.Config.Levels;

namespace MxFramework.Game.CharacterTest.Editor
{
    internal enum CharacterTestSceneCharacterCacheSyncKind
    {
        MissingSceneTarget,
        EmptyDocument,
        NotApplied,
        OutOfSync,
        Applied
    }

    internal readonly struct CharacterTestSceneCharacterCacheSyncStatus
    {
        public CharacterTestSceneCharacterCacheSyncStatus(
            CharacterTestSceneCharacterCacheSyncKind kind,
            string message)
        {
            Kind = kind;
            Message = message ?? string.Empty;
        }

        public CharacterTestSceneCharacterCacheSyncKind Kind { get; }

        public string Message { get; }

        public bool IsApplied => Kind == CharacterTestSceneCharacterCacheSyncKind.Applied;

        public bool NeedsApply =>
            Kind == CharacterTestSceneCharacterCacheSyncKind.NotApplied
            || Kind == CharacterTestSceneCharacterCacheSyncKind.OutOfSync;

        public static CharacterTestSceneCharacterCacheSyncStatus Evaluate(
            CharacterTestSceneCharacterAuthoringDocument document,
            MxLevelAuthoringDocument envelope,
            CharacterTestSceneCharacterCache cache,
            string documentPath)
        {
            int documentCharacterCount = document?.Characters == null ? 0 : document.Characters.Count;
            if (documentCharacterCount == 0)
            {
                return new CharacterTestSceneCharacterCacheSyncStatus(
                    CharacterTestSceneCharacterCacheSyncKind.EmptyDocument,
                    "暂无角色配置；创建角色后点击保存。");
            }

            if (cache == null)
            {
                return new CharacterTestSceneCharacterCacheSyncStatus(
                    CharacterTestSceneCharacterCacheSyncKind.MissingSceneTarget,
                    "未找到 GameManager.CharacterSceneCache，无法完成保存同步。");
            }

            if (cache.CharacterInstanceCount == 0)
            {
                return new CharacterTestSceneCharacterCacheSyncStatus(
                    CharacterTestSceneCharacterCacheSyncKind.NotApplied,
                    "JSON 已有角色，但场景缓存为空；Runtime 不会使用这些角色，请点击保存重新同步。");
            }

            string expectedHash = envelope == null
                ? string.Empty
                : MxLevelAuthoringJson.ComputeContentHash(envelope);
            if (cache.CharacterInstanceCount != documentCharacterCount)
            {
                return new CharacterTestSceneCharacterCacheSyncStatus(
                    CharacterTestSceneCharacterCacheSyncKind.OutOfSync,
                    "场景缓存角色数量与 JSON 不一致；请点击保存重新同步。");
            }

            if (!string.Equals(cache.AuthoringContentHash, expectedHash, StringComparison.Ordinal))
            {
                return new CharacterTestSceneCharacterCacheSyncStatus(
                    CharacterTestSceneCharacterCacheSyncKind.OutOfSync,
                    "场景缓存内容不是当前 JSON；请点击保存重新同步。");
            }

            string normalizedExpectedPath = NormalizePath(documentPath);
            string normalizedAppliedPath = NormalizePath(cache.AuthoringJsonPath);
            if (!string.IsNullOrEmpty(normalizedExpectedPath)
                && !string.Equals(normalizedAppliedPath, normalizedExpectedPath, StringComparison.OrdinalIgnoreCase))
            {
                return new CharacterTestSceneCharacterCacheSyncStatus(
                    CharacterTestSceneCharacterCacheSyncKind.OutOfSync,
                    "场景缓存来自其他 JSON 路径；请保存并应用当前文件。");
            }

            return new CharacterTestSceneCharacterCacheSyncStatus(
                CharacterTestSceneCharacterCacheSyncKind.Applied,
                "场景缓存已同步；重建 CharacterTest 会话后生效。");
        }

        private static string NormalizePath(string path)
        {
            return string.IsNullOrWhiteSpace(path)
                ? string.Empty
                : path.Trim().Replace('\\', '/');
        }
    }
}
