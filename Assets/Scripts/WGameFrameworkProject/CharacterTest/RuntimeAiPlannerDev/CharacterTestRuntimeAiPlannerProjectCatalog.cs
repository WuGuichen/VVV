using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    internal sealed class CharacterTestRuntimeAiPlannerExperimentGroupSnapshot
    {
        private readonly CharacterTestRuntimeAiPlannerBehaviorGroupCardReference[] _cards;

        public CharacterTestRuntimeAiPlannerExperimentGroupSnapshot(
            CharacterTestRuntimeAiPlannerBehaviorGroupRevision source)
        {
            StableGroupId = source?.StableGroupId ?? string.Empty;
            Revision = source?.Revision ?? 0;
            ContentHash = source?.ContentHash ?? 0L;
            _cards = CopyCards(source?.Cards);
            CharacterTestRuntimeAiPlannerBehaviorCompileResult compile = source != null
                ? CharacterTestRuntimeAiPlannerBehaviorCompositionCompiler.Compile(
                    source.Profile,
                    CharacterTestRuntimeAiPlannerBehaviorDefinitionCatalog.CreateBuiltIn())
                : null;
            RuntimeProfile = compile != null && compile.Success ? compile.Profile : null;
            Diagnostic = RuntimeProfile != null
                ? string.Empty
                : "行为组“" + StableGroupId + "”无法编译为冻结运行时 Profile。";
        }

        public string StableGroupId { get; }
        public int Revision { get; }
        public long ContentHash { get; }
        public CharacterTestRuntimeAiPlannerProfile RuntimeProfile { get; }
        public System.Collections.Generic.IReadOnlyList<CharacterTestRuntimeAiPlannerBehaviorGroupCardReference> Cards =>
            Array.AsReadOnly(_cards);
        public string Diagnostic { get; }
        public bool Success => RuntimeProfile != null && Diagnostic.Length == 0;

        private static CharacterTestRuntimeAiPlannerBehaviorGroupCardReference[] CopyCards(
            System.Collections.Generic.IReadOnlyList<CharacterTestRuntimeAiPlannerBehaviorGroupCardReference> source)
        {
            var result = new CharacterTestRuntimeAiPlannerBehaviorGroupCardReference[source?.Count ?? 0];
            for (int i = 0; i < result.Length; i++)
            {
                CharacterTestRuntimeAiPlannerBehaviorGroupCardReference card = source[i];
                result[i] = new CharacterTestRuntimeAiPlannerBehaviorGroupCardReference(
                    card.EntryId,
                    card.StableCardId,
                    card.CardRevision,
                    card.SemanticHash,
                    card.ContentHash,
                    card.Enabled,
                    card.Order);
            }
            return result;
        }
    }

    internal sealed class CharacterTestRuntimeAiPlannerExperimentCatalogSnapshot
    {
        private readonly CharacterTestRuntimeAiPlannerExperimentGroupSnapshot[] _groups;

        public CharacterTestRuntimeAiPlannerExperimentCatalogSnapshot(
            long cardCatalogHash,
            long groupCatalogHash,
            CharacterTestRuntimeAiPlannerBehaviorGroupRevision[] groups)
        {
            CardCatalogHash = cardCatalogHash;
            GroupCatalogHash = groupCatalogHash;
            CharacterTestRuntimeAiPlannerBehaviorGroupRevision[] source =
                groups ?? Array.Empty<CharacterTestRuntimeAiPlannerBehaviorGroupRevision>();
            _groups = new CharacterTestRuntimeAiPlannerExperimentGroupSnapshot[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                _groups[i] = new CharacterTestRuntimeAiPlannerExperimentGroupSnapshot(source[i]);
                if (Diagnostic.Length == 0 && !_groups[i].Success)
                    Diagnostic = _groups[i].Diagnostic;
            }
        }

        public long CardCatalogHash { get; }
        public long GroupCatalogHash { get; }
        public System.Collections.Generic.IReadOnlyList<CharacterTestRuntimeAiPlannerExperimentGroupSnapshot> Groups =>
            Array.AsReadOnly(_groups);
        public string Diagnostic { get; } = string.Empty;
        public bool Success => Diagnostic.Length == 0 && _groups.Length > 0;
    }

    internal sealed class CharacterTestRuntimeAiPlannerProjectCatalogState
    {
        public CharacterTestRuntimeAiPlannerProjectCatalogState(
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog cardCatalog,
            CharacterTestRuntimeAiPlannerBehaviorGroupCatalog groupCatalog,
            string cardCatalogPath,
            string groupCatalogPath,
            string cardFileHash,
            string groupFileHash,
            string diagnostic)
        {
            CardCatalog = cardCatalog;
            GroupCatalog = groupCatalog;
            CardCatalogPath = cardCatalogPath ?? string.Empty;
            GroupCatalogPath = groupCatalogPath ?? string.Empty;
            CardFileHash = cardFileHash ?? string.Empty;
            GroupFileHash = groupFileHash ?? string.Empty;
            Diagnostic = diagnostic ?? string.Empty;
        }

        public CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog CardCatalog { get; }
        public CharacterTestRuntimeAiPlannerBehaviorGroupCatalog GroupCatalog { get; }
        public string CardCatalogPath { get; }
        public string GroupCatalogPath { get; }
        public string CardFileHash { get; }
        public string GroupFileHash { get; }
        public string Diagnostic { get; }
        public bool Success => CardCatalog != null && GroupCatalog != null && Diagnostic.Length == 0;
        public bool CanWriteProjectCatalogs => CharacterTestRuntimeAiPlannerProjectCatalogWriteBridge.IsRegistered;

        public CharacterTestArenaBehaviorGroupDraftStore CreateArenaStore()
        {
            return Success
                ? CharacterTestArenaBehaviorGroupDraftStore.CreateFromProjectCatalog(GroupCatalog)
                : CharacterTestArenaBehaviorGroupDraftStore.CreateEmpty();
        }
    }

    internal static class CharacterTestRuntimeAiPlannerProjectCatalogLoader
    {
        public const string RelativeDirectory = "MxFramework/CharacterTest/runtime_ai_planner";
        public const string CardCatalogFileName = "behavior_card_catalog.json";
        public const string GroupCatalogFileName = "behavior_group_catalog.json";

        /// <remarks>LowFreqAlloc: settings/bootstrap disk read, JSON validation and compile only.</remarks>
        public static CharacterTestRuntimeAiPlannerProjectCatalogState Load(
            CharacterTestRuntimeAiLabBehaviorCardReferenceCatalog references = null)
        {
            string directory = Path.Combine(Application.streamingAssetsPath, RelativeDirectory);
            string cardPath = Path.Combine(directory, CardCatalogFileName);
            string groupPath = Path.Combine(directory, GroupCatalogFileName);
            if (!File.Exists(cardPath))
                return Failed(cardPath, groupPath, "缺少项目行为卡目录：" + cardPath);
            if (!File.Exists(groupPath))
                return Failed(cardPath, groupPath, "缺少项目行为组目录：" + groupPath);

            string cardJson;
            string groupJson;
            try
            {
                cardJson = File.ReadAllText(cardPath, Encoding.UTF8);
                groupJson = File.ReadAllText(groupPath, Encoding.UTF8);
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                return Failed(cardPath, groupPath, "读取项目行为目录失败：" + exception.Message);
            }

            CharacterTestRuntimeAiLabBehaviorCardReferenceCatalog safeReferences = references;
            if (safeReferences == null)
            {
                CharacterTestCharacterActionRuntime actionRuntime = CharacterTestCharacterActionConfigSource.Load();
                safeReferences = new CharacterTestRuntimeAiLabBehaviorCardReferenceCatalog(
                    CharacterTestAbilityCatalogSource.CreateRegistry(),
                    actionRuntime.Registry);
            }
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalogLoadResult cardLoad =
                CharacterTestRuntimeAiPlannerProjectCardStorage.IsIndexJson(cardJson)
                    ? CharacterTestRuntimeAiPlannerProjectCardStorage.Load(cardJson, directory, safeReferences)
                    : CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalogJson.Load(cardJson, safeReferences);
            if (!cardLoad.Success)

                return Failed(cardPath, groupPath, "项目行为卡目录无效：" + FirstCardDiagnostic(cardLoad));

            CharacterTestRuntimeAiPlannerBehaviorGroupCatalogLoadResult groupLoad =
                CharacterTestRuntimeAiPlannerBehaviorGroupCatalogJson.Load(
                    groupJson,
                    cardLoad.Catalog,
                    CharacterTestRuntimeAiPlannerBehaviorDefinitionCatalog.CreateBuiltIn());
            if (!groupLoad.Success)
                return Failed(cardPath, groupPath, "项目行为组目录无效：" + groupLoad.Diagnostic);

            return new CharacterTestRuntimeAiPlannerProjectCatalogState(
                cardLoad.Catalog,
                groupLoad.Catalog,
                cardPath,
                groupPath,
                ComputeFileHash(cardPath),
                ComputeFileHash(groupPath),
                string.Empty);
        }

        internal static string ComputeFileHash(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return string.Empty;
            using (SHA256 sha = SHA256.Create())
            using (FileStream stream = File.OpenRead(path))
            {
                byte[] bytes = sha.ComputeHash(stream);
                var builder = new StringBuilder(bytes.Length * 2);
                for (int i = 0; i < bytes.Length; i++)
                    builder.Append(bytes[i].ToString("x2"));
                return builder.ToString();
            }
        }

        private static string FirstCardDiagnostic(
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalogLoadResult result)
        {
            return result != null && result.Diagnostics.Count > 0
                ? result.Diagnostics[0].Message
                : "未知错误";
        }

        private static CharacterTestRuntimeAiPlannerProjectCatalogState Failed(
            string cardPath,
            string groupPath,
            string diagnostic)
        {
            return new CharacterTestRuntimeAiPlannerProjectCatalogState(
                null,
                null,
                cardPath,
                groupPath,
                string.Empty,
                string.Empty,
                diagnostic);
        }
    }

    internal readonly struct CharacterTestRuntimeAiPlannerProjectCatalogWriteRequest
    {
        public CharacterTestRuntimeAiPlannerProjectCatalogWriteRequest(
            string cardCatalogPath,
            string groupCatalogPath,
            string expectedCardFileHash,
            string expectedGroupFileHash,
            long expectedCardCatalogHash,
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog cardCatalog,
            string groupCatalogJson,
            bool explicitLegacyMigration = false)
        {
            CardCatalogPath = cardCatalogPath ?? string.Empty;
            GroupCatalogPath = groupCatalogPath ?? string.Empty;
            ExpectedCardFileHash = expectedCardFileHash ?? string.Empty;
            ExpectedGroupFileHash = expectedGroupFileHash ?? string.Empty;
            ExpectedCardCatalogHash = expectedCardCatalogHash;
            CardCatalog = cardCatalog;
            GroupCatalogJson = groupCatalogJson ?? string.Empty;
            ExplicitLegacyMigration = explicitLegacyMigration;
        }

        public string CardCatalogPath { get; }
        public string GroupCatalogPath { get; }
        public string ExpectedCardFileHash { get; }
        public string ExpectedGroupFileHash { get; }
        public long ExpectedCardCatalogHash { get; }
        public CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog CardCatalog { get; }
        public string GroupCatalogJson { get; }
        public bool ExplicitLegacyMigration { get; }
    }

    internal readonly struct CharacterTestRuntimeAiPlannerProjectCatalogWriteResult
    {
        public CharacterTestRuntimeAiPlannerProjectCatalogWriteResult(bool success, bool conflict, string diagnostic)
        {
            Success = success;
            Conflict = conflict;
            Diagnostic = diagnostic ?? string.Empty;
        }

        public bool Success { get; }
        public bool Conflict { get; }
        public string Diagnostic { get; }
    }

    internal static class CharacterTestRuntimeAiPlannerProjectCatalogWriteBridge
    {
        internal static Func<CharacterTestRuntimeAiPlannerProjectCatalogWriteRequest,
            CharacterTestRuntimeAiPlannerProjectCatalogWriteResult> Handler { get; set; }

        public static bool IsRegistered => Handler != null;

        public static CharacterTestRuntimeAiPlannerProjectCatalogWriteResult Write(
            CharacterTestRuntimeAiPlannerProjectCatalogWriteRequest request)
        {
            return Handler != null
                ? Handler(request)
                : new CharacterTestRuntimeAiPlannerProjectCatalogWriteResult(
                    false,
                    false,
                    "Player 构建只读；项目行为目录只能在 Unity Editor 中发布。");
        }
    }
}
