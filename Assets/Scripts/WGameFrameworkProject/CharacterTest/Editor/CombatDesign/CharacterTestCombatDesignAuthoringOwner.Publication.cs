using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using MxFramework.CharacterAction;
using MxFramework.Editor.CharacterAction;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MxFramework.Game.CharacterTest.Editor
{
    internal static partial class CharacterTestCombatDesignAuthoringOwner
    {
        // Failure injection is internal to focused isolated tests, never accepted from HTTP.
        internal static bool FailFixtureCheckpointForTests;

        private sealed partial class AuthoringContext
        {
            private string JournalPath(string cardId) => Path.Combine(Root, "journals", Hash(_scope + "/" + cardId) + ".json");
            private CharacterTestRuntimeAiPlannerProjectCatalogAuthoringSession Session()
            {
                var result = _fixture == null ? new CharacterTestRuntimeAiPlannerProjectCatalogAuthoringSession(_catalog)
                    : new CharacterTestRuntimeAiPlannerProjectCatalogAuthoringSession(_catalog, LoadCatalog, CharacterTestRuntimeAiPlannerProjectCatalogEditorBridge.WriteCombatDesignFixture);
                result.ResolveTacticalSource = draft => CharacterTestTacticalPublishedSource.CaptureDraft(_workspace, draft);
                return result;
            }

            private CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult Compile(CharacterTestRuntimeAiLabBehaviorCardDraft card, JObject config)
            {
                int character = (int?)config?["characterId"] ?? 0;
                Need(character > 0 && (_fixture != null || CharacterTestPlayableCharacterCatalog.Discover().Any(c => c.CharacterId == character)), "CHARACTER_REQUIRED: 请选择真实角色作为预检上下文。");
                var bindings = config?["actions"] as JObject ?? new JObject();
                string[] slots = Slots(card);
                Need(bindings.Properties().All(p => slots.Contains(p.Name)), "ACTION_BINDING_STALE: 槽位已变化，请重新选择预检配置。");
                var document = card.HasCompositionSource ? _workspace.ResolveComposition(card, referencedOnly: true) : null;
                var legacy = document == null ? card.CloneTacticalSource() : null;
                var requirements = document != null ? CharacterTestTacticalDependencies.Analyze(document) : CharacterTestTacticalDependencies.Analyze(legacy);
                var prepared = new Dictionary<string, CharacterTestTacticalCardInputs>(StringComparer.Ordinal);
                var inputs = CharacterTestTacticalCardInputs.Unbound(character);
                var measurement = CharacterTestTacticalSummaryPreparation.StandardMeasurement();
                var store = new CharacterActionTacticalMetricsStore();
                foreach (string slot in slots)
                {
                    int action = (int?)bindings[slot] ?? 0;
                    Need(action > 0, "ACTION_REQUIRED: 请配置 " + slot + " 使用的动作。");
                    using (var probe = CharacterActionReferenceProbeSession.ForTestAction(character, measurement.ReferenceDummy, action))
                    {
                        // Guard contributes real timing and content hashes, but has no attack range.
                        // The compiler still rejects range-dependent operations without a valid summary.
                        var actionRequirements = CharacterTestTacticalInputRequirements.PrimaryAction | CharacterTestTacticalInputRequirements.FunctionalActions;
                        if (probe.Action.Category != CharacterActionCategory.Guard)
                            actionRequirements |= CharacterTestTacticalInputRequirements.SpatialSummary;
                        Need(CharacterTestTacticalCardInputs.TryPrepare(probe.Runtime.ActionRuntime, probe.Source, probe.Runtime.PhysicsRuntime,
                            probe.Runtime.CombatTraceSystem.BakedTraceSource, probe.Runtime.CombatTraceSystem.WeaponTraceBindingResolver,
                            store, measurement, out var input, out string issue, action, actionRequirements), issue);
                        prepared.Add(slot, input);
                    }
                }
                if (slots.Length > 0) inputs = CharacterTestTacticalCardInputs.BindSlots(character, prepared);
                else if ((requirements & CharacterTestTacticalInputRequirements.FunctionalActions) != 0)
                {
                    using (var probe = CharacterActionReferenceProbeSession.ForCurrentOpener(character, measurement.ReferenceDummy))
                    {
                        Need(CharacterTestTacticalCardInputs.TryPrepare(probe.Runtime.ActionRuntime, probe.Source, probe.Runtime.PhysicsRuntime,
                            probe.Runtime.CombatTraceSystem.BakedTraceSource, probe.Runtime.CombatTraceSystem.WeaponTraceBindingResolver,
                            store, measurement, out var functional, out string issue, 0, CharacterTestTacticalInputRequirements.FunctionalActions), issue);
                        inputs = CharacterTestTacticalCardInputs.BindSlots(character, prepared, functional.StepBackAction, functional.StepBackContentHash,
                            functional.StepForwardAction, functional.StepForwardContentHash);
                    }
                }
                CharacterTestRuntimeAiLabCompiledLogicProgram program;
                if (document != null)
                {
                    Need(CharacterTestTacticalDslCompiler.TryCompile(document, inputs, out var compiled, out string issue), issue);
                    program = compiled.Program.WithTacticalSource(CharacterTestTacticalFrozenSource.Capture(_workspace, card, document, inputs, compiled));
                }
                else
                {
                    Need(CharacterTestTacticalAuthoringCompiler.TryCompile(legacy, inputs, out var compiled, out var errors), string.Join("; ", errors));
                    program = compiled.Program.WithTacticalSource(CharacterTestTacticalFrozenSource.CaptureLegacy(_workspace, card, inputs, compiled));
                }
                return new CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult(program.WithSelectionDefinition(CharacterTestBehaviorCardSelectionDefinition.FromTacticalCard(card, program)),
                    Array.Empty<CharacterTestRuntimeAiLabCardDiagnostic>());
            }

            internal JObject Preflight(JObject request)
            {
                Verify(request); var card = Card(request);
                Need(!File.Exists(JournalPath(card.CardDraftId)), "RECOVERY_REQUIRED: 请恢复此前发布操作。");
                var compiled = Compile(card, request["configuration"] as JObject);
                Verify(request);
                var session = Session(); Need(session.CanWrite, "CATALOG_READ_ONLY");
                Need(!session.HasPendingPublishRecovery, "RECOVERY_REQUIRED: 原发布流程有待恢复事务，请先在原作者入口恢复。");
                Need(session.TryHasUnpublishedCardChanges(card, _references, out bool changed, out string issue), issue);
                Need(string.IsNullOrEmpty(issue), issue);
                int latest = _catalog.CardCatalog.Cards.Where(c => c.StableCardId == card.PublishedStableId).Select(c => c.Revision).DefaultIfEmpty(0).Max();
                Need(latest == card.SourcePublishedRevision, "PUBLISHED_REVISION_CONFLICT: 正式库已有更新版本；请在原作者入口处理后重载。");
                var frozen = compiled.Program.TacticalSource;
                var plan = new JObject { ["scope"] = _scope, ["cardId"] = card.CardDraftId, ["stableId"] = card.PublishedStableId,
                    ["snapshotToken"] = SnapshotToken(), ["workspaceToken"] = WorkspaceToken(), ["configuration"] = request["configuration"]?.DeepClone(),
                    ["inputFingerprint"] = frozen.InputFingerprint, ["sourceHash"] = CharacterTestTacticalPublishedSource.Capture(frozen).SemanticHash.ToString(CultureInfo.InvariantCulture),
                    ["nextRevision"] = latest + 1, ["hasChanges"] = changed, ["canPublish"] = _canPublish && changed,
                    ["description"] = frozen.Description, ["descriptionDetails"] = frozen.DescriptionDetails,
                    ["expiresUtc"] = DateTime.UtcNow.AddMinutes(10).ToString("O"),
                    ["catalogPath"] = _catalog.CardCatalogPath.Replace('\\', '/'),
                    ["message"] = !changed ? "作者源与正式版本已同步。" : !_canPublish ? "预检通过；实验工作区不能正式发布，请先应用到 Main。" : "预检通过。确认后会重新编译并发布这张卡的一个新版本。" };
                string token = Hash(plan.ToString(Formatting.None));
                Atomic(Path.Combine(Root, "plans", token + ".json"), plan);
                plan["planToken"] = token; plan["detail"] = Detail(card); return plan;
            }

            internal JObject Publish(JObject request, string operationId)
            {
                Need(_canPublish, "MAIN_WORKSPACE_REQUIRED");
                Need((bool?)request["confirmed"] == true, "PUBLISH_CONFIRMATION_REQUIRED");
                string token = (string)request["planToken"]; Need(Token(token), "INVALID_PLAN_TOKEN");
                var plan = Read(Path.Combine(Root, "plans", token + ".json"));
                Need(Hash(plan.ToString(Formatting.None)) == token && (string)plan["scope"] == _scope && (bool?)plan["canPublish"] == true, "PLAN_INVALID");
                Need(DateTime.Parse((string)plan["expiresUtc"], null, DateTimeStyles.RoundtripKind) > DateTime.UtcNow, "PLAN_EXPIRED: 请重新预检。");
                Verify(plan); var card = Card(plan);
                string journalPath = JournalPath(card.CardDraftId);
                Need(!File.Exists(journalPath), "RECOVERY_REQUIRED: 此卡有未完成的发布检查点，请恢复原操作。");
                var compiled = Compile(card, plan["configuration"] as JObject);
                Need(compiled.Program.TacticalSource.InputFingerprint == (string)plan["inputFingerprint"], "DEPENDENCY_CHANGED: 动作或摘要依赖变化，请重新预检。");
                Verify(plan);
                var session = Session();
                Need(!session.HasPendingPublishRecovery, "RECOVERY_REQUIRED: 原发布流程有待恢复事务。");
                session.CompileTacticalForPublish = draft => compiled;
                var journal = new JObject { ["operationId"] = operationId, ["scope"] = _scope, ["cardId"] = card.CardDraftId,
                    ["stableId"] = card.PublishedStableId, ["workspaceToken"] = WorkspaceToken(), ["sourceHash"] = plan["sourceHash"],
                    ["revision"] = plan["nextRevision"], ["stage"] = "Prepared", ["name"] = card.DisplayName, ["cost"] = card.Cost, ["cooldown"] = card.CooldownSeconds };
                Atomic(journalPath, journal);
                var allocator = CharacterTestRuntimeAiPlannerPublishedBehaviorCardNumericIdentity.AuthoringAllocator;
                try
                {
                    CharacterTestRuntimeAiLabPublishResult result;
                    if (_fixture != null)
                    {
                        CharacterTestRuntimeAiPlannerPublishedBehaviorCardNumericIdentity.AuthoringAllocator = key => CharacterContentIdentityAuthoringCatalog.GetExisting(417, key);
                        using (CharacterTestRuntimeAiPlannerProjectCatalogEditorBridge.UseCombatDesignFixtureCatalog(_fixture, LoadCatalog))
                            result = session.TryPublishCard(card, _references);
                    }
                    else result = session.TryPublishCard(card, _references);
                    if (!result.Success) throw new InvalidOperationException(string.Join("; ", result.Diagnostics.Select(d => d.Message)));
                    journal["stage"] = "CatalogCommitted";
                    journal["contentHash"] = result.PublishedCard.ContentHash.ToString(CultureInfo.InvariantCulture);
                    Atomic(journalPath, journal);
                    if (_fixture != null && FailFixtureCheckpointForTests) throw new IOException("INJECTED_CHECKPOINT_FAILURE");
                    return Checkpoint(card, result.PublishedCard, journalPath);
                }
                catch (Exception error)
                {
                    // The existing durable catalog writer owns commit/rollback. Retain identity until a reload can determine its outcome.
                    return new JObject { ["status"] = "RecoveryRequired", ["message"] = "发布事务需要恢复：" + error.Message,
                        ["publicationStage"] = journal["stage"], ["cardId"] = card.CardDraftId, ["scope"] = _scope,
                        ["recoveryOperationId"] = operationId, ["diagnostics"] = new JArray(error.Message) };
                }
                finally { CharacterTestRuntimeAiPlannerPublishedBehaviorCardNumericIdentity.AuthoringAllocator = allocator; }
            }

            internal JObject Reconcile(JObject request)
            {
                var card = Card(request); string path = JournalPath(card.CardDraftId);
                Need(File.Exists(path), "NO_PENDING_PUBLICATION"); var journal = Read(path);
                Need((string)request["recoveryOperationId"] == (string)journal["operationId"], "RECOVERY_ID_MISMATCH");
                var state = LoadCatalog(); Need(state.Success, state.Diagnostic);
                if (!state.CardCatalog.TryGetExact((string)journal["stableId"], (int)journal["revision"], out var published))
                {
                    Need((string)journal["stage"] == "Prepared", "CATALOG_RECOVERY_REQUIRED: 已提交版本无法读取；请使用原目录恢复工具。");
                    File.Delete(path);
                    return new JObject { ["message"] = "未发现已提交版本。已解除重试锁，请重新加载并预检。", ["detail"] = Detail(card) };
                }
                Need((string)journal["stage"] == "CatalogCommitted" && journal["contentHash"] != null,
                    "CATALOG_RECOVERY_REQUIRED: 目录已有目标版本，但提交回执中断；请在原目录恢复工具核对，不会重发或自动认领该版本。");
                Need(published.ContentHash.ToString(CultureInfo.InvariantCulture) == (string)journal["contentHash"], "RECOVERY_CONTENT_HASH_CONFLICT");
                Need(published.SemanticHash.ToString(CultureInfo.InvariantCulture) == (string)journal["sourceHash"]
                    && published.Metadata.DisplayName == (string)journal["name"] && published.Metadata.Cost == (int)journal["cost"]
                    && published.Metadata.CooldownSeconds == (float)journal["cooldown"], "RECOVERY_CATALOG_CONFLICT: 目录版本与原请求不匹配。");
                if (card.SourcePublishedRevision == published.Revision && card.SourcePublishedContentHash == published.ContentHash)
                { File.Delete(path); return PublishedResult(published, Detail(card)); }
                Need((string)journal["workspaceToken"] == WorkspaceToken(), "RECOVERY_SOURCE_CONFLICT: 草稿已有新编辑，未覆盖；请在原作者入口恢复。");
                return Checkpoint(card, published, path);
            }
            private JObject Checkpoint(CharacterTestRuntimeAiLabBehaviorCardDraft card, CharacterTestRuntimeAiPlannerPublishedBehaviorCard published, string journalPath)
            {
                Need(_workspace.TryAdoptStandaloneTacticalPublishedRevision(card.CardDraftId, published, out string issue), issue);
                Persist(); var reload = LoadCatalog();
                Need(reload.Success && reload.CardCatalog.TryGetExact(published.StableCardId, published.Revision, out var actual)
                    && actual.ContentHash == published.ContentHash && actual.SemanticHash == published.SemanticHash, "RELOAD_HASH_MISMATCH");
                File.Delete(journalPath); return PublishedResult(published, Detail(card));
            }
            private JObject PublishedResult(CharacterTestRuntimeAiPlannerPublishedBehaviorCard published, JObject detail)
                => new JObject { ["message"] = _fixture == null ? "正式卡版本已发布并重新读取。行为组和玩家持有未改变。" : "隔离发布已提交并重新读取；正式目录未改动。",
                    ["scope"] = _scope, ["published"] = true, ["revision"] = published.Revision, ["stableId"] = published.StableCardId,
                    ["contentHash"] = published.ContentHash.ToString(CultureInfo.InvariantCulture), ["semanticHash"] = published.SemanticHash.ToString(CultureInfo.InvariantCulture),
                    ["detail"] = detail };
        }
    }
}
