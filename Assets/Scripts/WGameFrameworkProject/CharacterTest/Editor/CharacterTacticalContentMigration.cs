using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using MxFramework.CharacterAction;
using MxFramework.CharacterApplication;
using MxFramework.Game.CharacterTest.TacticalCard;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Game.CharacterTest.Editor
{
    /// <summary>Explicit one-time content migration. Never runs during discovery, spawn or gameplay.</summary>
    internal static class CharacterTacticalContentMigration
    {
        private const string CardId = "tactical.bound-action";
        private static readonly JsonSerializerSettings Json = new JsonSerializerSettings {
            ContractResolver = new CamelCasePropertyNamesContractResolver(), Formatting = Formatting.Indented };

        [MenuItem("MxFramework/CharacterTest/Tactical Content/Initialize Missing Character Defaults")]
        internal static void InitializeMissingDefaults()
        {
            string root = Path.GetDirectoryName(Application.dataPath);
            string catalogPath = Path.Combine(Application.streamingAssetsPath, "MxFramework/CharacterTest/tactical_content.json");
            var content = File.Exists(catalogPath) ? JsonConvert.DeserializeObject<CharacterTacticalContentCatalog>(File.ReadAllText(catalogPath))
                : new CharacterTacticalContentCatalog();
            string acquisitionPath = Path.Combine(Path.GetDirectoryName(catalogPath), "tactical_acquisition.json");
            if (File.Exists(acquisitionPath)) content.Acquisition = JsonConvert.DeserializeObject<CharacterTacticalAcquisitionConfig>(File.ReadAllText(acquisitionPath)).Characters;
            var actions = content.Actions.ToList();
            var pools = content.Acquisition.ToList();
            var registry = CharacterTestCharacterActionConfigSource.Load().Registry;
            var references = new CharacterTestRuntimeAiLabBehaviorCardReferenceCatalog(null, registry);
            var publishedState = CharacterTestRuntimeAiPlannerProjectCatalogLoader.Load(references);
            if (!publishedState.Success) throw new InvalidOperationException(publishedState.Diagnostic);
            content.SetPublishedCards(publishedState.CardCatalog.Cards);
            content.Validate();
            var published = publishedState.CardCatalog.Cards.FirstOrDefault(c => c.StableCardId == CardId && c.Revision == 1);
            var measurement = CharacterTestTacticalSummaryPreparation.StandardMeasurement();
            var report = new List<string>();
            var defaultWrites = new Dictionary<string, CharacterTacticalDefaults>(StringComparer.Ordinal);
            foreach (var definition in CharacterTestPlayableCharacterCatalog.Discover())
            {
                if (!TacticalDeckCatalog.TryGetPackageId(definition, out string packageId)) continue;
                string sourceRoot = Path.Combine(root, "Authoring/CharacterPackages/character-" + packageId);
                string sourcePath = Path.Combine(sourceRoot, CharacterTacticalDefaults.RelativePath);
                if (!Directory.Exists(sourceRoot)) { report.Add(packageId + ": missing authoring source"); continue; }
                CharacterTacticalDefaults defaults;
                if (File.Exists(sourcePath))
                {
                    defaults = JsonConvert.DeserializeObject<CharacterTacticalDefaults>(File.ReadAllText(sourcePath));
                    if (!defaults.TryValidate(out string issue)) throw new InvalidOperationException(sourcePath + ": " + issue);
                    foreach (string id in defaults.ActionIds)
                    {
                        if (actions.Any(a => a.Id == id)) continue;
                        var original = registry.Actions.FirstOrDefault(a => id == "ta." + packageId + "." + a.ActionId.Value);
                        string library = original == null ? null : FindLibrary(root, packageId, original.ActionId.Value);
                        if (library == null) throw new InvalidOperationException("无法恢复明确的动作库引用：" + id);
                        using (var probe = CharacterActionReferenceProbeSession.ForTestAction(definition.CharacterId, measurement.ReferenceDummy, original.ActionId.Value))
                        {
                            var store = new CharacterActionTacticalMetricsStore();
                            if (!CharacterTestTacticalCardInputs.TryPrepare(probe.Runtime.ActionRuntime, probe.Source, probe.Runtime.PhysicsRuntime,
                                probe.Runtime.CombatTraceSystem.BakedTraceSource, probe.Runtime.CombatTraceSystem.WeaponTraceBindingResolver,
                                store, measurement, out var single, out issue, original.ActionId.Value,
                                CharacterTestTacticalInputRequirements.PrimaryAction | CharacterTestTacticalInputRequirements.SpatialSummary))
                                throw new InvalidOperationException(issue);
                            var inputs = CharacterTestTacticalCardInputs.BindSlots(definition.CharacterId,
                                new Dictionary<string, CharacterTestTacticalCardInputs> { ["action"] = single });
                            var source = CreateSource();
                            if (!CharacterTestTacticalAuthoringCompiler.TryCompile(source, inputs, out var compilation, out var diagnostics))
                                throw new InvalidOperationException(string.Join("; ", diagnostics));
                            if (published == null) published = Publish(source, inputs, compilation, references);
                            actions.Add(new CharacterTacticalActionDefinition { Id = id, DisplayName = original.DisplayName,
                                ActionLibraryId = library, ActionId = original.ActionId.Value, CharacterStableIds = new[] { defaults.CharacterStableId },
                                Source = "Assets/MxFrameworkGenerated/CharacterActions/Libraries/" + library + "/character_actions.json" });
                        }
                    }
                }
                else
                {
                    var application = JObject.Parse(File.ReadAllText(Path.Combine(sourceRoot, "config/character_application.json")));
                    defaults = new CharacterTacticalDefaults { CharacterStableId = (string)application["characterStableId"] };
                    defaults.Diagnostics.Add("尚未配置初始拥有动作。请在默认战术内容中显式配置；基础行为方案不授予战术动作。");
                    defaultWrites[sourcePath] = defaults;
                    string manifestPath = Path.Combine(sourceRoot, "manifest.json");
                    var manifest = JObject.Parse(File.ReadAllText(manifestPath));
                    var files = manifest["configFiles"] as JArray ?? new JArray();
                    if (!files.Values<string>().Contains(CharacterTacticalDefaults.RelativePath)) files.Add(CharacterTacticalDefaults.RelativePath);
                    manifest["configFiles"] = files;
                    File.WriteAllText(manifestPath, manifest.ToString(Formatting.Indented) + "\n");
                }
                // This repository generator projects the same validated reference DTO as the Authoring compiler.
                if (!File.Exists(catalogPath) && published != null)
                {
                    foreach (var reference in defaults.Cards.Where(c => c.CardId == CardId && c.Revision == 1))
                        reference.ContentHash = published.ContentHash.ToString(CultureInfo.InvariantCulture);
                    defaultWrites[sourcePath] = defaults;
                }
                defaultWrites[Path.Combine(definition.GeneratedPackageRoot, CharacterTacticalDefaults.RelativePath)] = defaults;
                if (pools.All(p => p.CharacterStableId != defaults.CharacterStableId)) pools.Add(new CharacterTacticalAcquisitionPool {
                    CharacterStableId = defaults.CharacterStableId, ActionIds = defaults.ActionIds.ToArray(), Cards = defaults.Cards.ToArray() });
                report.Add(packageId + ": " + defaults.ActionIds.Count + " actions, " + defaults.Cards.Count + " cards; " + string.Join("; ", defaults.Diagnostics));
            }
            if (published != null)
            {
                var cards = publishedState.CardCatalog.Cards.ToList();
                if (!cards.Any(c => c.StableCardId == CardId && c.Revision == 1))
                {
                    cards.Add(published);
                    var rebuilt = CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog.ValidateAndCreate(publishedState.CardCatalog.CatalogId,
                        publishedState.CardCatalog.Revision + 1, cards, references);
                    if (!rebuilt.Success) throw new InvalidOperationException(JsonConvert.SerializeObject(rebuilt.Diagnostics));
                    var write = CharacterTestRuntimeAiPlannerProjectCatalogWriteBridge.Write(new CharacterTestRuntimeAiPlannerProjectCatalogWriteRequest(
                        publishedState.CardCatalogPath, publishedState.GroupCatalogPath, publishedState.CardFileHash, publishedState.GroupFileHash,
                        publishedState.CardCatalog.CatalogHash, rebuilt.Catalog, File.ReadAllText(publishedState.GroupCatalogPath)));
                    if (!write.Success) throw new InvalidOperationException(write.Diagnostic);
                }
                if (!content.Cards.Any(c => c.CardId == CardId && c.Revision == 1))
                    content.SetPublishedCards(publishedState.CardCatalog.Cards.Concat(new[] { published }));
            }
            content.Actions = actions.ToArray(); content.Acquisition = pools.ToArray(); content.Validate(); Write(catalogPath, content);
            Write(acquisitionPath, new CharacterTacticalAcquisitionConfig { Characters = content.Acquisition });
            foreach (var write in defaultWrites) Write(write.Key, write.Value);
            Directory.CreateDirectory(Path.Combine(root, "Library/TacticalContent"));
            File.WriteAllLines(Path.Combine(root, "Library/TacticalContent/initialization.txt"), report);
            CharacterTestPlayableCharacterCatalog.Refresh();
            AssetDatabase.Refresh();
            Debug.Log("Tactical content initialization completed:\n" + string.Join("\n", report));
        }

        internal static CharacterTestTacticalAuthoringSource CreateSource()
        {
            var source = CharacterTestTacticalAuthoringTemplates.CreateSingleAttack("常规移动接敌出招");
            source.Slots[0].SlotId = "action"; source.Slots[0].Resolver = "binding";
            source.Slots[0].Description = "接近目标后执行的战术动作";
            foreach (var evt in source.Timelines[0].Events)
                if (evt.Operation.Kind == "CharacterAction") evt.Operation.SlotId = "action";
            return source;
        }
        private static CharacterTestRuntimeAiPlannerPublishedBehaviorCard Publish(CharacterTestTacticalAuthoringSource source,
            CharacterTestTacticalCardInputs inputs, CharacterTestTacticalAuthoringCompilation compilation,
            CharacterTestRuntimeAiLabBehaviorCardReferenceCatalog references)
        {
            var draft = new CharacterTestRuntimeAiLabBehaviorCardDraft(CardId, CardId, source.Name, "Tactical", null);
            draft.SetTacticalSource(source);
            var frozen = CharacterTestTacticalFrozenSource.CaptureLegacy(null, draft, inputs, compilation);
            var program = compilation.Program.WithTacticalSource(frozen);
            var metadata = new CharacterTestRuntimeAiPlannerPublishedBehaviorCardMetadata(source.Name, "ui.behavior-card.custom", 0,
                "接近目标并执行卡槽中装配的动作。", string.Empty, string.Empty);
            int version = CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.CurrentRuntimeContractVersion;
            int numeric = CharacterTestRuntimeAiPlannerPublishedBehaviorCardNumericIdentity.AllocateForAuthoring(CardId);
            var result = CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.ValidateAndCreate(draft,
                new CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublishRequest(CardId, 1, version, version, metadata, numeric), references,
                new CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult(program, null));
            if (!result.Success) throw new InvalidOperationException(JsonConvert.SerializeObject(result.Diagnostics));
            return result.Card;
        }
        private static CharacterTacticalCardReference Reference(CharacterTestRuntimeAiPlannerPublishedBehaviorCard card) => new CharacterTacticalCardReference {
            CardId = card.StableCardId, Revision = card.Revision, ContentHash = card.ContentHash.ToString(CultureInfo.InvariantCulture) };
        private static string FindLibrary(string root, string package, int action)
        {
            var profile = JObject.Parse(File.ReadAllText(Path.Combine(root, "Assets/MxFrameworkGenerated/CharacterActions/Profiles", package, "character_action_profile.json")));
            foreach (string library in profile["profiles"].SelectMany(p => p["actionLibraryIds"]).Values<string>().Distinct())
            {
                string path = Path.Combine(root, "Assets/MxFrameworkGenerated/CharacterActions/Libraries", library, "character_actions.json");
                if (File.Exists(path) && JObject.Parse(File.ReadAllText(path))["actions"].Any(a => (int?)a["actionId"] == action)) return library;
            }
            return null;
        }
        private static void Write(string path, object value)
        { Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllText(path, JsonConvert.SerializeObject(value, Json) + "\n"); }
    }
}
