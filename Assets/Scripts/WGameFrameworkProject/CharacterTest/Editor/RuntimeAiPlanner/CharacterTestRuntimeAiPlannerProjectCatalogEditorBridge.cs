using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using MxFramework.Game.CharacterTest;
using MxFramework.Editor.CharacterAction;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Game.CharacterTest.Editor
{
    [InitializeOnLoad]
    internal static partial class CharacterTestRuntimeAiPlannerProjectCatalogEditorBridge
    {
        static CharacterTestRuntimeAiPlannerProjectCatalogEditorBridge()
        {
            if (!TryRecoverPendingTransactions(out bool recoveredAny, out string recoveryDiagnostic))
            {
                Debug.LogError(
                    "Runtime AI Planner project catalog recovery is incomplete; publishing is disabled until "
                    + "the retained transaction can be recovered: "
                    + recoveryDiagnostic);
            }
            else if (recoveredAny)
            {
                EditorApplication.delayCall += RefreshAssetsAfterRecovery;
            }

            CharacterTestRuntimeAiPlannerProjectCatalogWriteBridge.Handler = Write;
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardNumericIdentity.AuthoringAllocator =
                sourceKey => CharacterContentIdentityAuthoringCatalog.GetOrAllocate(
                    CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog.CardNumericDomainId,
                    sourceKey);
            if (CharacterContentIdentityAuthoringCatalog.GetExisting(
                    CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog.CatalogNumericDomainId,
                    "behavior-card/catalog")
                != CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog.DefaultCatalogNumericId)
            {
                throw new InvalidOperationException("Published behavior card catalog identity is not bound to its declared numeric ID.");
            }
        }

        [MenuItem("MxFramework/Character Test/Runtime AI Planner/Generate Or Restore Project Behavior Catalogs")]
        private static void GenerateOrRestoreProjectCatalogs()
        {
            CharacterTestRuntimeAiPlannerProjectCatalogSeedResult seed =
                CharacterTestRuntimeAiPlannerProjectCatalogSeed.Create();
            if (!seed.Success)
            {
                Debug.LogError("Runtime AI Planner project catalog recovery failed: " + seed.Diagnostic);
                return;
            }

            string directory = Path.Combine(
                Application.streamingAssetsPath,
                CharacterTestRuntimeAiPlannerProjectCatalogLoader.RelativeDirectory);
            Directory.CreateDirectory(directory);
            string cardPath = Path.Combine(
                directory,
                CharacterTestRuntimeAiPlannerProjectCatalogLoader.CardCatalogFileName);
            string groupPath = Path.Combine(
                directory,
                CharacterTestRuntimeAiPlannerProjectCatalogLoader.GroupCatalogFileName);
            CharacterTestRuntimeAiPlannerBehaviorGroupCatalog groups = seed.Groups;
            string existingGroupJson = ReadBestRecoveryGroupJson(groupPath);
            if (!string.IsNullOrWhiteSpace(existingGroupJson))
            {
                CharacterTestRuntimeAiPlannerBehaviorGroupCatalogLoadResult rebound =
                    CharacterTestRuntimeAiPlannerBehaviorGroupCatalogJson.RebindCardReferences(
                        existingGroupJson,
                        seed.Cards,
                        CharacterTestRuntimeAiPlannerBehaviorDefinitionCatalog.CreateBuiltIn());
                if (!rebound.Success)
                {
                    Debug.LogError("Runtime AI Planner project catalog recovery failed: " + rebound.Diagnostic);
                    return;
                }
                groups = rebound.Catalog;
            }
            CharacterTestRuntimeAiPlannerProjectCatalogWriteResult write = Write(
                new CharacterTestRuntimeAiPlannerProjectCatalogWriteRequest(
                    cardPath,
                    groupPath,
                    CharacterTestRuntimeAiPlannerProjectCatalogLoader.ComputeFileHash(cardPath),
                    CharacterTestRuntimeAiPlannerProjectCatalogLoader.ComputeFileHash(groupPath),
                    CharacterTestRuntimeAiPlannerProjectCatalogLoader.Load().CardCatalog?.CatalogHash
                        ?? seed.Cards.CatalogHash,
                    seed.Cards,
                    CharacterTestRuntimeAiPlannerBehaviorGroupCatalogJson.ToJson(groups)));
            if (!write.Success)
            {
                Debug.LogError("Runtime AI Planner project catalog recovery failed: " + write.Diagnostic);
                return;
            }

            Debug.Log("Runtime AI Planner project behavior catalogs generated and validated: " + directory);
        }

        [MenuItem("MxFramework/Character Test/Runtime AI Planner/Migrate Published Cards To Current Contract")]
        private static void MigratePublishedCardsToCurrentContract()
        {
            string directory = Path.Combine(
                Application.streamingAssetsPath,
                CharacterTestRuntimeAiPlannerProjectCatalogLoader.RelativeDirectory);
            string cardPath = Path.Combine(
                directory,
                CharacterTestRuntimeAiPlannerProjectCatalogLoader.CardCatalogFileName);
            string groupPath = Path.Combine(
                directory,
                CharacterTestRuntimeAiPlannerProjectCatalogLoader.GroupCatalogFileName);
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalogJson.ExplicitLegacyMigrationEnabled = true;
            try
            {
                CharacterTestCharacterActionRuntime actionRuntime = CharacterTestCharacterActionConfigSource.Load();
                var references = new CharacterTestRuntimeAiLabBehaviorCardReferenceCatalog(
                    CharacterTestAbilityCatalogSource.CreateRegistry(),
                    actionRuntime.Registry);
                CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalogLoadResult cards =
                    CharacterTestRuntimeAiPlannerProjectCardStorage.LoadForExplicitMigration(
                        File.ReadAllText(cardPath, Encoding.UTF8),
                        directory,
                        references);
                if (!cards.Success)
                {
                    Debug.LogError("Runtime AI Planner published-card migration failed: "
                        + FirstCardDiagnostic(cards));
                    return;
                }

                CharacterTestRuntimeAiPlannerBehaviorGroupCatalogLoadResult groups =
                    CharacterTestRuntimeAiPlannerBehaviorGroupCatalogJson.RebindCardReferences(
                        File.ReadAllText(groupPath, Encoding.UTF8),
                        cards.Catalog,
                        CharacterTestRuntimeAiPlannerBehaviorDefinitionCatalog.CreateBuiltIn());
                if (!groups.Success)
                {
                    Debug.LogError("Runtime AI Planner published-card migration failed: " + groups.Diagnostic);
                    return;
                }

                CharacterTestRuntimeAiPlannerProjectCatalogWriteResult write = Write(
                    new CharacterTestRuntimeAiPlannerProjectCatalogWriteRequest(
                        cardPath,
                        groupPath,
                        CharacterTestRuntimeAiPlannerProjectCatalogLoader.ComputeFileHash(cardPath),
                        CharacterTestRuntimeAiPlannerProjectCatalogLoader.ComputeFileHash(groupPath),
                        expectedCardCatalogHash: 0L,
                        cardCatalog: cards.Catalog,
                        groupCatalogJson: CharacterTestRuntimeAiPlannerBehaviorGroupCatalogJson.ToJson(groups.Catalog),
                        explicitLegacyMigration: true));
                if (!write.Success)
                {
                    Debug.LogError("Runtime AI Planner published-card migration failed: " + write.Diagnostic);
                    return;
                }

                Debug.Log("Runtime AI Planner published cards migrated through the formal catalog publisher.");
            }
            finally
            {
                CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalogJson.ExplicitLegacyMigrationEnabled = false;
            }
        }

        private static string ReadBestRecoveryGroupJson(string groupPath)
        {
            string current = ReadOptionalText(groupPath);
            string previous = ReadOptionalText(GetLocalRecoveryPath(
                CharacterTestRuntimeAiPlannerProjectCatalogLoader.GroupCatalogFileName));
            int currentCount = CountGroups(current);
            int previousCount = CountGroups(previous);
            return previousCount > currentCount ? previous : current;
        }

        private static string GetLocalRecoveryDirectory()
        {
            return GetEffectiveRecoveryRoot();
        }

        private static string GetLocalRecoveryPath(string fileName) =>
            Path.Combine(GetLocalRecoveryDirectory(), fileName + ".previous");

        private static string ReadOptionalText(string path)
        {
            try
            {
                return File.Exists(path) ? File.ReadAllText(path, Encoding.UTF8) : string.Empty;
            }
            catch (IOException)
            {
                return string.Empty;
            }
            catch (UnauthorizedAccessException)
            {
                return string.Empty;
            }
        }

        private static int CountGroups(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return 0;
            try
            {
                Newtonsoft.Json.Linq.JObject root = Newtonsoft.Json.Linq.JObject.Parse(json);
                return root["groups"] is Newtonsoft.Json.Linq.JArray groups ? groups.Count : 0;
            }
            catch (JsonException)
            {
                return 0;
            }
        }

        private static CharacterTestRuntimeAiPlannerProjectCatalogWriteResult Write(
            CharacterTestRuntimeAiPlannerProjectCatalogWriteRequest request)
        {
            if (!TryRecoverPendingTransactions(out _, out string pendingRecoveryDiagnostic))
            {
                return Failed(
                    "项目目录存在未完成且无法证明已恢复的事务，已禁止继续发布；恢复记录已保留："
                    + pendingRecoveryDiagnostic);
            }
            if (!TryValidateCatalogPaths(
                    request.CardCatalogPath,
                    request.GroupCatalogPath,
                    out string cardPath,
                    out string groupPath,
                    out string directory,
                    out string pathDiagnostic))
            {
                return Failed(pathDiagnostic);
            }
            if (request.CardCatalog == null)
                return Failed("待写入的项目行为卡目录不可用。");

            string currentCardHash = CharacterTestRuntimeAiPlannerProjectCatalogLoader.ComputeFileHash(cardPath);
            string currentGroupHash = CharacterTestRuntimeAiPlannerProjectCatalogLoader.ComputeFileHash(groupPath);
            if (!string.Equals(currentCardHash, request.ExpectedCardFileHash, StringComparison.Ordinal)
                || !string.Equals(currentGroupHash, request.ExpectedGroupFileHash, StringComparison.Ordinal))
            {
                return new CharacterTestRuntimeAiPlannerProjectCatalogWriteResult(
                    false,
                    true,
                    "项目目录已被其他编辑修改；本地草稿已保留，请重新加载或基于新版重新应用修改。");
            }
            CharacterTestRuntimeAiPlannerProjectCatalogState currentState =
                request.ExplicitLegacyMigration
                    ? null
                    : _combatDesignFixtureReload != null
                        ? _combatDesignFixtureReload()
                        : CharacterTestRuntimeAiPlannerProjectCatalogLoader.Load();
            if (!request.ExplicitLegacyMigration
                && (!currentState.Success
                    || currentState.CardCatalog.CatalogHash != request.ExpectedCardCatalogHash))
            {
                return new CharacterTestRuntimeAiPlannerProjectCatalogWriteResult(
                    false,
                    true,
                    "项目行为卡文件已被其他编辑修改；本地草稿已保留，请重新加载后再保存。");
            }

            string recoveryDirectory = GetLocalRecoveryDirectory();
            Directory.CreateDirectory(recoveryDirectory);
            string transactionId = Guid.NewGuid().ToString("N");
            ProjectCatalogTransactionContext transaction = CreateTransactionContext(transactionId);
            if (!IsSafeTransactionContext(transaction))
                return Failed("项目目录恢复事务路径未通过 recovery root 校验。");
            string previousCardPath = GetLocalRecoveryPath(
                CharacterTestRuntimeAiPlannerProjectCatalogLoader.CardCatalogFileName);
            string previousGroupPath = GetLocalRecoveryPath(
                CharacterTestRuntimeAiPlannerProjectCatalogLoader.GroupCatalogFileName);
            bool cardExisted = File.Exists(cardPath);
            bool groupExisted = File.Exists(groupPath);
            bool journalPersisted = false;
            bool committedMarkerPersisted = false;
            try
            {
                CharacterTestRuntimeAiPlannerProjectCardStoragePayload payload =
                    CharacterTestRuntimeAiPlannerProjectCardStorage.CreatePayload(request.CardCatalog);
                Directory.CreateDirectory(transaction.StagingRoot);
                for (int i = 0; i < payload.Files.Count; i++)
                {
                    CharacterTestRuntimeAiPlannerProjectCardFile file = payload.Files[i];
                    string stagingPath = Path.Combine(
                        transaction.StagingRoot,
                        file.RelativePath.Replace('/', Path.DirectorySeparatorChar));
                    if (!IsPathWithinRoot(stagingPath, transaction.StagingRoot))
                        throw new InvalidDataException("行为卡 staging 路径越界：" + file.RelativePath);
                    Directory.CreateDirectory(Path.GetDirectoryName(stagingPath));
                    WriteTextDurable(stagingPath, file.Json);
                }
                WriteTextDurable(transaction.TemporaryCardPath, payload.IndexJson);
                WriteTextDurable(transaction.TemporaryGroupPath, request.GroupCatalogJson);
                string validation = ValidateTemporaryPair(
                    transaction.StagingRoot,
                    transaction.TemporaryCardPath,
                    transaction.TemporaryGroupPath);
                if (validation.Length > 0)
                    return Failed(validation);

                ProjectCatalogTransactionJournal journal = CreateDurableBackups(
                    transaction,
                    directory,
                    cardPath,
                    groupPath);
                journalPersisted = true;

                Directory.CreateDirectory(directory);
                string cardFilesDirectory = Path.Combine(
                    directory,
                    CharacterTestRuntimeAiPlannerProjectCardStorage.CardsDirectoryName);
                WriteCardFiles(cardFilesDirectory, transaction.StagingRoot, payload.Files);
                PersistPhase(
                    transaction,
                    journal,
                    ProjectCatalogTransactionPhase.CardFilesInstalled);
                InjectFault(
                    CharacterTestRuntimeAiPlannerProjectCatalogTransactionFaultPoint
                        .AfterCardFilesInstalled);

                InstallCatalogFile(
                    transaction.TemporaryCardPath,
                    cardPath,
                    previousCardPath,
                    cardExisted,
                    recoveryDirectory);
                PersistPhase(
                    transaction,
                    journal,
                    ProjectCatalogTransactionPhase.CardIndexInstalled);
                InjectFault(
                    CharacterTestRuntimeAiPlannerProjectCatalogTransactionFaultPoint
                        .AfterCardIndexInstalled);

                InstallCatalogFile(
                    transaction.TemporaryGroupPath,
                    groupPath,
                    previousGroupPath,
                    groupExisted,
                    recoveryDirectory);
                PersistPhase(
                    transaction,
                    journal,
                    ProjectCatalogTransactionPhase.GroupCatalogInstalled);
                InjectFault(
                    CharacterTestRuntimeAiPlannerProjectCatalogTransactionFaultPoint
                        .AfterGroupCatalogInstalled);

                string installedValidation = ValidateTemporaryPair(directory, cardPath, groupPath);
                if (installedValidation.Length > 0)
                    throw new InvalidDataException("目录写入后复读失败：" + installedValidation);

                PersistPhase(transaction, journal, ProjectCatalogTransactionPhase.Committed);
                committedMarkerPersisted = true;
                InjectFault(
                    CharacterTestRuntimeAiPlannerProjectCatalogTransactionFaultPoint
                        .AfterCommittedMarkerPersisted);
                if (!TryDeleteTransactionDirectory(
                        transaction.TransactionRoot,
                        transaction.RecoveryRoot,
                        out string cleanupDiagnostic))
                {
                    Debug.LogWarning(
                        "Runtime AI Planner catalog commit succeeded, but its committed recovery record "
                        + "could not be cleaned and will be retried on the next load: "
                        + cleanupDiagnostic);
                }
                RefreshAssetsAfterCatalogMutation();
                return new CharacterTestRuntimeAiPlannerProjectCatalogWriteResult(true, false, string.Empty);
            }
            catch (CharacterTestRuntimeAiPlannerProjectCatalogSimulatedInterruptionException)
            {
                throw;
            }
            catch (Exception exception)
            {
                if (committedMarkerPersisted)
                {
                    bool cleaned = TryRecoverPendingTransactions(
                        out _,
                        out string committedCleanupDiagnostic);
                    if (!cleaned)
                    {
                        Debug.LogError(
                            "Runtime AI Planner catalog was committed, but its recovery record could not be "
                            + "cleaned: "
                            + committedCleanupDiagnostic);
                    }
                    return new CharacterTestRuntimeAiPlannerProjectCatalogWriteResult(
                        true,
                        false,
                        "游戏可用版本已经完整提交，但提交后的 Editor 收尾失败：" + exception.Message);
                }

                if (!journalPersisted)
                {
                    TryDeleteTransactionDirectory(
                        transaction.TransactionRoot,
                        transaction.RecoveryRoot,
                        out _);
                    return Failed("项目目录事务准备失败，正式目录未开始修改：" + exception.Message);
                }

                bool recovered = TryRecoverPendingTransactions(
                    out _,
                    out string recoveryDiagnostic);
                if (recovered)
                {
                    RefreshAssetsAfterCatalogMutation();
                    return Failed(
                        "项目目录事务写入失败，已通过持久恢复事务完整回滚；游戏可用版本没有改变："
                        + exception.Message);
                }

                return Failed(
                    "项目目录事务写入失败，且无法证明正式目录已完整恢复；恢复 journal 已保留，"
                    + "继续发布已被阻止："
                    + exception.Message
                    + "；"
                    + recoveryDiagnostic);
            }
            finally
            {
                if (!journalPersisted)
                {
                    TryDeleteTransactionDirectory(
                        transaction.TransactionRoot,
                        transaction.RecoveryRoot,
                        out _);
                }
            }
        }

        private static string ValidateTemporaryPair(
            string stagingRoot,
            string temporaryCardPath,
            string temporaryGroupPath)
        {
            CharacterTestCharacterActionRuntime actionRuntime = CharacterTestCharacterActionConfigSource.Load();
            var references = new CharacterTestRuntimeAiLabBehaviorCardReferenceCatalog(
                CharacterTestAbilityCatalogSource.CreateRegistry(),
                actionRuntime.Registry);
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalogLoadResult cards =
                CharacterTestRuntimeAiPlannerProjectCardStorage.Load(
                    File.ReadAllText(temporaryCardPath, Encoding.UTF8),
                    stagingRoot,
                    references);
            if (!cards.Success)
                return "临时行为卡目录复读校验失败：" + FirstCardDiagnostic(cards);

            CharacterTestRuntimeAiPlannerBehaviorGroupCatalogLoadResult groups =
                CharacterTestRuntimeAiPlannerBehaviorGroupCatalogJson.Load(
                    File.ReadAllText(temporaryGroupPath, Encoding.UTF8),
                    cards.Catalog,
                    CharacterTestRuntimeAiPlannerBehaviorDefinitionCatalog.CreateBuiltIn());
            return groups.Success ? string.Empty : "临时行为组目录复读校验失败：" + groups.Diagnostic;
        }

        private static void WriteCardFiles(
            string cardFilesDirectory,
            string stagingRoot,
            IReadOnlyList<CharacterTestRuntimeAiPlannerProjectCardFile> files)
        {
            Directory.CreateDirectory(cardFilesDirectory);
            var desired = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < files.Count; i++)
            {
                CharacterTestRuntimeAiPlannerProjectCardFile file = files[i];
                string targetPath = Path.GetFullPath(Path.Combine(
                    Path.GetDirectoryName(cardFilesDirectory),
                    file.RelativePath.Replace('/', Path.DirectorySeparatorChar)));
                desired.Add(targetPath);
                string sourcePath = Path.Combine(
                    stagingRoot,
                    file.RelativePath.Replace('/', Path.DirectorySeparatorChar));
                File.Copy(sourcePath, targetPath, true);
            }
            string[] existing = Directory.GetFiles(cardFilesDirectory, "*.json", SearchOption.TopDirectoryOnly);
            for (int i = 0; i < existing.Length; i++)
            {
                if (!desired.Contains(Path.GetFullPath(existing[i])))
                    File.Delete(existing[i]);
            }
        }

        private static string FirstCardDiagnostic(
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalogLoadResult result)
        {
            return result != null && result.Diagnostics.Count > 0
                ? result.Diagnostics[0].Message
                : "未知错误";
        }

        private static CharacterTestRuntimeAiPlannerProjectCatalogWriteResult Failed(string diagnostic)
        {
            return new CharacterTestRuntimeAiPlannerProjectCatalogWriteResult(false, false, diagnostic);
        }
    }
}
