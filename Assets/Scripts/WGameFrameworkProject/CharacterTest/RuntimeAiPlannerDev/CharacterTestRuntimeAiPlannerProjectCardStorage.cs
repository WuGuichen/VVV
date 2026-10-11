using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MxFramework.Game.CharacterTest
{
    internal sealed class CharacterTestRuntimeAiPlannerProjectCardFile
    {
        public CharacterTestRuntimeAiPlannerProjectCardFile(
            string stableCardId,
            string relativePath,
            string json)
        {
            StableCardId = stableCardId ?? string.Empty;
            RelativePath = relativePath ?? string.Empty;
            Json = json ?? string.Empty;
        }

        public string StableCardId { get; }
        public string RelativePath { get; }
        public string Json { get; }
    }

    internal sealed class CharacterTestRuntimeAiPlannerProjectCardStoragePayload
    {
        public CharacterTestRuntimeAiPlannerProjectCardStoragePayload(
            string indexJson,
            CharacterTestRuntimeAiPlannerProjectCardFile[] files)
        {
            IndexJson = indexJson ?? string.Empty;
            Files = Array.AsReadOnly(files ?? Array.Empty<CharacterTestRuntimeAiPlannerProjectCardFile>());
        }

        public string IndexJson { get; }
        public IReadOnlyList<CharacterTestRuntimeAiPlannerProjectCardFile> Files { get; }
    }

    internal static class CharacterTestRuntimeAiPlannerProjectCardStorage
    {
        public const string Schema =
            "mx.character-test.runtime-ai-planner.project-behavior-card-index.v1";
        public const string CardsDirectoryName = "behavior_cards";

        public static bool IsIndexJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return false;
            try
            {
                return string.Equals((string)JObject.Parse(json)["schema"], Schema, StringComparison.Ordinal);
            }
            catch (JsonException)
            {
                return false;
            }
        }

        public static CharacterTestRuntimeAiPlannerProjectCardStoragePayload CreatePayload(
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog catalog)
        {
            if (catalog == null)
                throw new ArgumentNullException(nameof(catalog));

            var files = new List<CharacterTestRuntimeAiPlannerProjectCardFile>();
            var entries = new JArray();
            string previousStableCardId = null;
            for (int i = 0; i < catalog.Cards.Count; i++)
            {
                CharacterTestRuntimeAiPlannerPublishedBehaviorCard card = catalog.Cards[i];
                if (string.Equals(previousStableCardId, card.StableCardId, StringComparison.Ordinal))
                    continue;
                previousStableCardId = card.StableCardId;

                string fileName = ToFileName(card.StableCardId);
                string relativePath = CardsDirectoryName + "/" + fileName;
                CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog cardFileCatalog =
                    catalog.CreateStableCardFileCatalog(card.StableCardId);
                files.Add(new CharacterTestRuntimeAiPlannerProjectCardFile(
                    card.StableCardId,
                    relativePath,
                    CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalogJson.ToJson(cardFileCatalog)));
                entries.Add(new JObject
                {
                    ["stableCardId"] = card.StableCardId,
                    ["latestRevision"] = LatestRevision(cardFileCatalog),
                    ["file"] = relativePath
                });
            }

            var root = new JObject
            {
                ["schema"] = Schema,
                ["catalogId"] = catalog.CatalogId,
                ["revision"] = catalog.Revision,
                ["catalogNumericId"] = catalog.CatalogNumericId,
                ["numericIdentityHash"] = catalog.NumericIdentityHash,
                ["catalogHash"] = catalog.CatalogHash,
                ["cards"] = entries
            };
            return new CharacterTestRuntimeAiPlannerProjectCardStoragePayload(
                root.ToString(Formatting.Indented),
                files.ToArray());
        }

        public static CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalogLoadResult Load(
            string indexJson,
            string projectDirectory,
            CharacterTestRuntimeAiLabBehaviorCardReferenceCatalog references)
        {
            return LoadInternal(indexJson, projectDirectory, references, allowLegacyIndexHash: false);
        }

        internal static CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalogLoadResult LoadForExplicitMigration(
            string indexJson,
            string projectDirectory,
            CharacterTestRuntimeAiLabBehaviorCardReferenceCatalog references)
        {
            return LoadInternal(indexJson, projectDirectory, references, allowLegacyIndexHash: true);
        }

        private static CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalogLoadResult LoadInternal(
            string indexJson,
            string projectDirectory,
            CharacterTestRuntimeAiLabBehaviorCardReferenceCatalog references,
            bool allowLegacyIndexHash)
        {
            JObject root;
            try
            {
                root = JObject.Parse(indexJson ?? string.Empty);
            }
            catch (JsonException exception)
            {
                return Failed("PROJECT_CARD_INDEX_PARSE", "$", "行为卡索引解析失败：" + exception.Message);
            }

            if (!string.Equals((string)root["schema"], Schema, StringComparison.Ordinal))
                return Failed("PROJECT_CARD_INDEX_SCHEMA", "$.schema", "行为卡索引 schema 不受支持。");
            string catalogId = ((string)root["catalogId"] ?? string.Empty).Trim();
            int revision = (int?)root["revision"] ?? 0;
            int catalogNumericId = (int?)root["catalogNumericId"] ?? 0;
            long expectedNumericIdentityHash = (long?)root["numericIdentityHash"] ?? 0L;
            long expectedCatalogHash = (long?)root["catalogHash"] ?? 0L;
            if (catalogId.Length == 0 || revision <= 0 || catalogNumericId <= 0 || !(root["cards"] is JArray entries))
                return Failed("PROJECT_CARD_INDEX_INVALID", "$", "行为卡索引缺少目录身份、修订或 cards。");

            string rootDirectory;
            string cardsDirectory;
            try
            {
                rootDirectory = Path.GetFullPath(projectDirectory ?? string.Empty);
                cardsDirectory = Path.GetFullPath(Path.Combine(rootDirectory, CardsDirectoryName));
            }
            catch (Exception exception) when (
                exception is ArgumentException || exception is NotSupportedException || exception is PathTooLongException)
            {
                return Failed("PROJECT_CARD_DIRECTORY_INVALID", "$.cards", "行为卡目录路径无效：" + exception.Message);
            }

            var cards = new List<CharacterTestRuntimeAiPlannerPublishedBehaviorCard>();
            var stableIds = new HashSet<string>(StringComparer.Ordinal);
            bool migratedV9CardFiles = false;
            for (int i = 0; i < entries.Count; i++)
            {
                if (!(entries[i] is JObject entry))
                    return Failed("PROJECT_CARD_INDEX_ENTRY_INVALID", "$.cards[" + i + "]", "行为卡索引项无效。");
                string stableCardId = ((string)entry["stableCardId"] ?? string.Empty).Trim();
                string relativePath = ((string)entry["file"] ?? string.Empty).Replace('/', Path.DirectorySeparatorChar);
                int latestRevision = (int?)entry["latestRevision"] ?? 0;
                if (stableCardId.Length == 0 || latestRevision <= 0 || !stableIds.Add(stableCardId))
                    return Failed("PROJECT_CARD_INDEX_ID_INVALID", "$.cards[" + i + "]", "行为卡索引稳定 ID 或最新修订无效。");

                string fullPath;
                try
                {
                    fullPath = Path.GetFullPath(Path.Combine(rootDirectory, relativePath));
                }
                catch (Exception exception) when (
                    exception is ArgumentException || exception is NotSupportedException || exception is PathTooLongException)
                {
                    return Failed("PROJECT_CARD_PATH_INVALID", "$.cards[" + i + "].file", "行为卡文件路径无效：" + exception.Message);
                }
                string cardsPrefix = cardsDirectory.TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
                if (!fullPath.StartsWith(cardsPrefix, StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(Path.GetExtension(fullPath), ".json", StringComparison.OrdinalIgnoreCase))
                {
                    return Failed("PROJECT_CARD_PATH_OUTSIDE", "$.cards[" + i + "].file", "行为卡文件必须位于 behavior_cards 目录内。");
                }
                if (!File.Exists(fullPath))
                    return Failed("PROJECT_CARD_FILE_MISSING", "$.cards[" + i + "].file", "缺少行为卡文件：" + fullPath);

                CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalogLoadResult loaded;
                try
                {
                    string cardJson = File.ReadAllText(fullPath);
                    migratedV9CardFiles |= ContainsLegacyV9Logic(cardJson);
                    loaded = CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalogJson.Load(
                        cardJson,
                        references);
                }
                catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
                {
                    return Failed("PROJECT_CARD_FILE_READ", "$.cards[" + i + "].file", "读取行为卡文件失败：" + exception.Message);
                }
                if (!loaded.Success)
                    return loaded;
                int actualLatestRevision = 0;
                for (int cardIndex = 0; cardIndex < loaded.Catalog.Cards.Count; cardIndex++)
                {
                    CharacterTestRuntimeAiPlannerPublishedBehaviorCard card = loaded.Catalog.Cards[cardIndex];
                    if (!string.Equals(card.StableCardId, stableCardId, StringComparison.Ordinal))
                        return Failed("PROJECT_CARD_FILE_ID_MISMATCH", "$.cards[" + i + "]", "行为卡文件包含了其他稳定 ID。");
                    actualLatestRevision = Math.Max(actualLatestRevision, card.Revision);
                    cards.Add(card);
                }
                if (actualLatestRevision != latestRevision)
                    return Failed("PROJECT_CARD_FILE_REVISION_MISMATCH", "$.cards[" + i + "].latestRevision", "行为卡索引的最新修订与文件不一致。");
            }

            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalogCreateResult aggregate =
                CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog.ValidateAndCreate(
                    catalogId,
                    revision,
                    cards,
                    references,
                    catalogNumericId);
            if (!aggregate.Success)
                return new CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalogLoadResult(
                    null,
                    CopyDiagnostics(aggregate.Diagnostics));
            if (!allowLegacyIndexHash
                && !migratedV9CardFiles
                && aggregate.Catalog.CatalogHash != expectedCatalogHash)
            {
                return Failed(
                    "PROJECT_CARD_INDEX_HASH_MISMATCH",
                    "$.catalogHash",
                    "行为卡索引哈希与独立卡文件聚合结果不一致（索引 "
                    + expectedCatalogHash
                    + "，文件 "
                    + aggregate.Catalog.CatalogHash
                    + "）。");
            }
            // Only the explicit authoring migration may fill an absent derived fingerprint.
            // A supplied but incorrect fingerprint is rejected by every entry point.
            if (aggregate.Catalog.NumericIdentityHash != expectedNumericIdentityHash
                && (!allowLegacyIndexHash || root["numericIdentityHash"] != null))
                return Failed("PROJECT_CARD_INDEX_NUMERIC_IDENTITY_HASH_MISMATCH", "$.numericIdentityHash", "行为卡索引数字身份映射哈希不匹配。");
            return new CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalogLoadResult(
                aggregate.Catalog,
                Array.Empty<CharacterTestRuntimeAiLabCardDiagnostic>());
        }

        private static bool ContainsLegacyV9Logic(string json)
        {
            try
            {
                JArray cards = JObject.Parse(json ?? string.Empty)["cards"] as JArray;
                for (int i = 0; cards != null && i < cards.Count; i++)
                {
                    if (cards[i] is JObject card
                        && (string.Equals(
                                (string)card["logicSchema"],
                                CharacterTestRuntimeAiLabBehaviorCardLogicPayload.LegacyV9Schema,
                                StringComparison.Ordinal)
                            || string.Equals(
                                (string)card["logicSchema"],
                                CharacterTestRuntimeAiLabBehaviorCardLogicPayload.LegacyV10Schema,
                                StringComparison.Ordinal)))
                        return true;
                }
            }
            catch (JsonException)
            {
                return false;
            }
            return false;
        }

        private static string ToFileName(string stableCardId)
        {
            string value = stableCardId?.Trim() ?? string.Empty;
            if (value.Length == 0
                || value.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
                || value.IndexOf('/') >= 0
                || value.IndexOf('\\') >= 0
                || string.Equals(value, ".", StringComparison.Ordinal)
                || string.Equals(value, "..", StringComparison.Ordinal))
            {
                throw new InvalidDataException("行为卡稳定 ID 不能安全映射为独立文件名：" + value);
            }
            return value + ".json";
        }

        private static int LatestRevision(
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog catalog)
        {
            int latest = 0;
            for (int i = 0; i < catalog.Cards.Count; i++)
                latest = Math.Max(latest, catalog.Cards[i].Revision);
            return latest;
        }

        private static CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalogLoadResult Failed(
            string code,
            string path,
            string message)
        {
            return new CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalogLoadResult(
                null,
                new[]
                {
                    new CharacterTestRuntimeAiLabCardDiagnostic(
                        CharacterTestRuntimeAiLabCardDiagnosticSeverity.Error,
                        code,
                        path,
                        message)
                });
        }

        private static CharacterTestRuntimeAiLabCardDiagnostic[] CopyDiagnostics(
            IReadOnlyList<CharacterTestRuntimeAiLabCardDiagnostic> diagnostics)
        {
            var copy = new CharacterTestRuntimeAiLabCardDiagnostic[diagnostics?.Count ?? 0];
            for (int i = 0; i < copy.Length; i++)
                copy[i] = diagnostics[i];
            return copy;
        }
    }
}
