using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using MxFramework.Config.Levels;
using MxFramework.CharacterApplication;
using MxFramework.Game.CharacterTest.TacticalCard;
using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestRuntimeSessionBuilder
    {
        internal PreparedLevelSession PrepareLevelSession(MxLevelAuthoringDocument document, string sourcePath,
            bool publishPins = false, bool createViews = true)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));
            // A different Scene must first be opened by the scene-flow owner. This seam
            // never applies another scene's transform references to the current scene.
            if (document.ScenePath != _gameManager.gameObject.scene.path)
                throw new InvalidOperationException(document.LevelId + "/scene: open " + document.ScenePath + " before preparing this level");
            document = MxLevelAuthoringJson.LoadFromJson(MxLevelAuthoringJson.SaveToJson(document));
            var known = new HashSet<string>(new[] { "characterTest.characters", "characterTest.worldObjects", "characterTest.gameplayTriggers",
                "characterTest.tacticalStage", "characterTest.guidance", "characterTest.genesisValidation" }, StringComparer.Ordinal);
            foreach (var module in document.Modules)
                if (!known.Contains(module.ModuleId)) throw new InvalidOperationException(document.LevelId + "/unsupported-module: " + module.ModuleId);
            var root = _gameManager.WorldObjectSceneRoot;
            var world = new CharacterTestWorldObjectSceneCache();
            var characters = new CharacterTestSceneCharacterCache();
            var definitions = CharacterTestWorldObjectDefinitionCatalogLoader.LoadDefaultOrBuiltIn();
            if (!CharacterTestWorldObjectAuthoringAdapter.ApplyEnvelopeToCache(document, world, root, definitions.Catalog, sourcePath, out var worldReport))
                throw new InvalidOperationException(document.LevelId + "/worldObjects: " + worldReport);
            if (!CharacterTestSceneCharacterAuthoringAdapter.ApplyEnvelopeToCache(document, characters, root, sourcePath, out var characterReport))
                throw new InvalidOperationException(document.LevelId + "/characters: " + characterReport);
            CharacterTestSceneCharacterAuthoringAdapter.TryExtractCharactersDocument(document, out var actorDocument, out _);
            var actors = actorDocument.Characters.Where(a => a.Enabled).OrderBy(a => a.StableId, StringComparer.Ordinal).ToArray();
            var collected = characters.CollectCharacterSpawnSpecs(out var diagnostics);
            if (collected.Length != actors.Length || diagnostics.Length != 0) throw new InvalidOperationException(document.LevelId + "/spawn cache: " + string.Join(";", diagnostics));
            CharacterTestWorldObjectAuthoringAdapter.TryExtractWorldObjectsDocument(document, out var worldDocument, out _);
            if (!CharacterTestGameplayTriggerSceneCacheCompiler.TryCompile(document, worldDocument, root, definitions.Catalog, out var triggerData, out var triggerReport))
                throw new InvalidOperationException(document.LevelId + "/gameplayTriggers: " + triggerReport);
            var triggers = new CharacterTestGameplayTriggerSceneCache(); triggers.Configure(triggerData);
            var guidance = new CharacterTestGuidanceSceneCache();
            if (document.TryGetModule(CharacterTestGuidanceAuthoringAdapter.ModuleId, out _))
            {
                if (!CharacterTestGuidanceAuthoringAdapter.TryExtract(document, out var guidanceDocument, out var guidanceDiagnostic)
                    || !CharacterTestGuidanceCompiler.TryCompile(guidanceDocument, out var plan, out var guidanceReport))
                    throw new InvalidOperationException(document.LevelId + "/guidance: " + guidanceDiagnostic.Message);
                guidance.Configure(plan);
            }
            // genesisValidation is an authoring validation workspace. Its document is
            // preserved in the same envelope; it does not own runtime simulation state.
            var catalog = CharacterTestRuntimeAiPlannerProjectCatalogLoader.Load();
            if (!catalog.Success) throw new InvalidOperationException(catalog.Diagnostic);
            var defaultProfile = CharacterTestRuntimeAiPlannerProfileJson.LoadFromStreamingAssetsOrDefault(_profile.RuntimeAiPlannerProfilePath);
            var profiles = new List<CharacterTestRuntimeAiPlannerProfileBinding>();
            var spawns = new List<CharacterTestCharacterSpawnSpec>();
            for (int i = 0; i < actors.Length; i++)
            {
                var actor = actors[i];
                var spec = collected.Single(s => s.InstanceStableId == actor.StableId).WithRuntimeAiPlannerProfileSlot(i + 1);
                var group = CharacterTestLevelBehaviorCompiler.Resolve(document.LevelId, actor, catalog);
                if (actor.ControlOwner == CharacterTestCharacterControlOwner.AiControlled && group == null
                    && string.IsNullOrWhiteSpace(actor.RuntimeAiPlannerProfilePath))
                    throw new InvalidOperationException(document.LevelId + "/" + actor.StableId + "/behavior group required");
                var compiled = group == null ? null : CharacterTestRuntimeAiPlannerBehaviorCompositionCompiler.Compile(group.Profile);
                var legacy = string.IsNullOrWhiteSpace(actor.RuntimeAiPlannerProfilePath) ? defaultProfile
                    : CharacterTestRuntimeAiPlannerProfileJson.LoadFromStreamingAssetsOrDefault(actor.RuntimeAiPlannerProfilePath);
                profiles.Add(new CharacterTestRuntimeAiPlannerProfileBinding(i + 1, "level:" + document.LevelId + "/" + actor.StableId,
                    compiled?.Profile ?? legacy.Profile, compiled?.Report ?? legacy.Report));
                spawns.Add(spec);
            }
            CharacterTestTacticalStageAuthoringDocument tactical = null;
            TacticalDeployment deployment = null;
            if (document.TryGetModule(CharacterTestTacticalStageAuthoringDocument.ModuleId, out var stageModule))
            {
                if (stageModule.SchemaVersion != CharacterTestTacticalStageAuthoringDocument.CurrentSchemaVersion)
                    throw new InvalidOperationException(document.LevelId + "/tacticalStage schema");
                tactical = stageModule.ReadPayload<CharacterTestTacticalStageAuthoringDocument>();
                deployment = CreateLevelDeployment(document.LevelId, actors, spawns, catalog, tactical);
            }
            ulong runId = NextStageRunId(HighestKnownTacticalStageRunId());
            var physics = CharacterTestCombatPhysicsSceneBridge.BuildRuntime(root, world, _gameManager.PhysicsSettings);
            if (physics == null) throw new InvalidOperationException(document.LevelId + "/physics runtime missing");
            CharacterTestNavigationSessionBuildInput? navigation = CompileRequiredLevelNavigation(
                document.LevelId, _gameManager.NavigationSettings, world, out var navigationResult);
            physics.SetNavigationBuildInput(navigation, navigationResult);
            GameSlice slice = null;
            var resources = CreateResourceRuntimeServices();
            bool accepted = false;
            float scale = Time.timeScale;
            try
            {
                slice = new GameSlice(new CharacterTestSliceOptions {
                    DetachedTacticalRestore = true, EnableStoryBootstrap = false,
                    Logger = _context.Logger, Resources = resources, IncomingOwnershipAccepted = () => accepted = true,
                    StoryContent = LoadStoryContent(), BaseResourcePreloadGroupId = _profile.BaseResourcePreloadGroupId,
                    ActionCatalog = CharacterTestCharacterActionConfigSource.LoadCatalog(), PhysicsRuntime = physics,
                    NavigationBuildInput = navigation, NavigationInputResult = navigationResult,
                    RuntimeAiPlannerProfile = defaultProfile.Profile, RuntimeAiPlannerProfileReport = defaultProfile.Report,
                    RuntimeAiPlannerProfileBindings = profiles, CreateCharacterViews = createViews,
                    EnableLocalInputCommands = tactical == null, InputProvider = _context.InputProvider,
                    EnableTraceFileWrite = false,
                    MovableNaturalObjectSpawnSpecs = world.CollectMovableSpawnSpecs(out _), StartupCharacterSpawnSpecs = Array.Empty<CharacterTestCharacterSpawnSpec>(),
                    GameplayTriggerSceneCache = triggers, MixedPlayerTacticalStage = tactical, TacticalDeployment = deployment,
                    TacticalStageRunId = runId, InitialCameraMode = CharacterTestCameraMode.MultiTargetAuto
                });
                ValidateRequiredLevelNavigation(document.LevelId, navigationResult,
                    slice.Services.Modules.NavigationSession?.BuildResult ?? default);
                slice.StartPreparedLevelHost();
                var runtime = slice.Services.CharacterRuntime;
                runtime.PrepareLevelActors(spawns);
                var requests = new List<CharacterTestRuntimeAiPlannerBehaviorOverlayInstallRequest>();
                foreach (var actor in actors)
                {
                    var entity = runtime.FindLevelActor(actor.StableId);
                    var pose = actor.SpawnPose;
                    if (pose != null) entity.SetTransform(new CharacterTestCharacterTransformComponent(entity.Transform.X, entity.Transform.Y, entity.Transform.Z, pose.YawDegrees));
                    if (actor.BehaviorBinding != null)
                        requests.Add(CharacterTestLevelBehaviorCompiler.Compile(document.LevelId, actor,
                            entity.Control.RuntimeAiPlannerProfileSlot, catalog, runtime.ActionRuntime, entity, physics, publishPins, runtime));
                }
                if (requests.Count > 0 && !slice.Services.Modules.RuntimeAiPlannerCommands.TryApplyBehaviorCompositionOverlayBatch(requests, out var result))
                    throw new InvalidOperationException(document.LevelId + "/AI install: " + result.ReportText);
                if (publishPins) CharacterTestSceneCharacterAuthoringAdapter.UpsertCharactersModule(document, actorDocument);
                return new PreparedLevelSession(slice, document, world, characters, triggers, guidance, runId, deployment);
            }
            catch { slice?.Dispose(); if (!accepted) resources.Dispose(); throw; }
            finally { Time.timeScale = scale; }
        }

        private static TacticalDeployment CreateLevelDeployment(string levelId, CharacterTestSceneCharacterDefinition[] actors,
            List<CharacterTestCharacterSpawnSpec> spawns, CharacterTestRuntimeAiPlannerProjectCatalogState catalog, CharacterTestTacticalStageAuthoringDocument config)
        {
            if (actors.Count(a => a.ControlOwner == CharacterTestCharacterControlOwner.LocalPlayer) != 1)
                throw new InvalidOperationException(levelId + "/tacticalStage requires one LocalPlayer");
            var participants = new List<TacticalDeploymentParticipant>();
            foreach (var actor in actors)
            {
                var cards = new List<TacticalCardDefinition>();
                var bindings = new Dictionary<ulong, ReadOnlyDictionary<string, int>>();
                var group = CharacterTestLevelBehaviorCompiler.Resolve(levelId, actor, catalog);
                if (group != null) foreach (var item in group.Cards.Where(c => c.Enabled).OrderBy(c => c.Order))
                {
                    catalog.CardCatalog.TryGetExact(item.StableCardId, item.CardRevision, out var published);
                    // Action-free legacy fallback participates in the AI group, not in the human action deck.
                    if (published.TacticalSource == null) continue;
                    ulong instance = checked((ulong)cards.Count + 1);
                    var metadata = new CharacterTacticalCardDefinition { CardId = published.StableCardId, Revision = published.Revision,
                        ContentHash = published.ContentHash.ToString(System.Globalization.CultureInfo.InvariantCulture), DisplayName = published.Metadata.DisplayName,
                        Description = published.Metadata.ShortDescription, MotionCost = published.Metadata.Cost,
                        CooldownSeconds = published.Metadata.CooldownSeconds, IconKey = published.Metadata.IconKey,
                        PresentationSource = published.TacticalSource, Slots = CharacterTestLevelBehaviorCompiler.Slots(published.TacticalSource) };
                    cards.Add(TacticalDeployment.CreateDefinition(new CharacterTacticalCardReference {
                        CardId = metadata.CardId, Revision = metadata.Revision, ContentHash = metadata.ContentHash, InstanceId = instance }, metadata));
                    bindings.Add(instance, new ReadOnlyDictionary<string, int>(actor.BehaviorBinding.Actions.Where(a => a.EntryId == item.EntryId)
                        .ToDictionary(a => a.SlotPath, a => a.ActionId, StringComparer.Ordinal)));
                }
                var spec = spawns.Single(s => s.InstanceStableId == actor.StableId);
                participants.Add(new TacticalDeploymentParticipant(actor.StableId, actor.CharacterId, actor.StableId, actor.DisplayName,
                    actor.TeamId, actor.ControlOwner == CharacterTestCharacterControlOwner.LocalPlayer,
                    new Vector3(spec.X, spec.Y, spec.Z), actor.SpawnPose?.YawDegrees ?? 0, cards.ToArray(), bindings));
            }
            var player = participants.Single(p => p.IsPlayerControlled);
            if (player.Cards.Count < config.HandCapacity || !participants.Any(p => p.TeamId != player.TeamId))
                throw new InvalidOperationException(levelId + "/tacticalStage requires a complete formal player deck and hostile actor");
            return TacticalDeployment.CreateLevel(participants.ToArray(), config.InitialMomentum);
        }

        internal void LoadLevel(string levelId)
        {
            string root = Path.Combine(Application.streamingAssetsPath, "MxFramework/CharacterTest/Levels");
            var catalog = CharacterTestLevelCatalog.Read(Path.Combine(root, "directory.json"));
            var document = catalog.Load(levelId, root, true, out var sourcePath);
            using var candidate = PrepareLevelSession(document, sourcePath);
            PublishLevelSession(candidate);
        }

        internal void PublishLevelSession(PreparedLevelSession candidate)
        {
            if (candidate?.Slice == null) throw new InvalidOperationException("Prepared level required.");
            if (_context.NetworkRunner != null) throw new InvalidOperationException("Switch level from an offline session.");
            ValidatePreparedStageIdentity(candidate.Slice, candidate.RunId);
            // Candidate compilation has completed; only this synchronous commit owns
            // the new identity/time. The old slice owns and releases its subscriptions.
            using var publication = new PreparedTacticalSession(candidate.Take(), candidate.Document.TryGetModule("characterTest.tacticalStage", out _), candidate.RunId);
            _context.ActiveLevelId = candidate.Document.LevelId;
            _context.ActiveLevelContentHash = MxLevelAuthoringJson.ComputeContentHash(candidate.Document);
            _gameManager.SetGuidanceSceneCache(candidate.Guidance);
            PublishTacticalSession(publication, "Level " + candidate.Document.LevelId, candidate.World);
            _context.Slice.Services.Modules.MixedPlayerTacticalStage?.PausedWorldTime.ClaimGlobalTime(1f);
            _context.MainMenuVisible = false;
            _context.NetworkSyncProbe.Reset();
            _context.ManualPaused = false; _context.Paused = false;
            _context.MainMenuCoordinator?.SetPaused(false);
            _gameManager.GetComponent<TacticalCardSliceCoordinator>()?.NotifyLevelSessionPublished(
                _context.Slice, candidate.Deployment, candidate.RunId);
        }

        internal static CharacterTestMovableNaturalObjectUnityViewBridge CreateLevelWorldViewBridge(
            CharacterTestWorldObjectSceneCache selected, CharacterTestWorldObjectSceneCache serialized,
            Func<CharacterTestMovableNaturalObjectSceneSource> legacySource)
        {
            // A selected empty level clears the map, including any legacy Scene source.
            if (selected != null) return CharacterTestMovableNaturalObjectUnityViewBridge.FromSceneCache(selected);
            if (serialized != null && serialized.MovableInstanceCount > 0)
                return CharacterTestMovableNaturalObjectUnityViewBridge.FromSceneCache(serialized);
            return CharacterTestMovableNaturalObjectUnityViewBridge.FromSceneSource(legacySource?.Invoke());
        }

        private ulong HighestKnownTacticalStageRunId()
        {
            return Math.Max(_tacticalStageRunId, StageRunIdOf(_context.Slice));
        }

        private static ulong StageRunIdOf(GameSlice slice)
        {
            return slice?.Services.Modules.TacticalStage?.Stage.StageRunId
                ?? slice?.Services.Modules.MixedPlayerTacticalStage?.Stage.StageRunId ?? 0;
        }

        internal static ulong NextStageRunId(ulong lastAllocated, ulong observedRunId = 0)
        {
            ulong high = Math.Max(lastAllocated, observedRunId);
            if (high == ulong.MaxValue) throw new InvalidOperationException("Session run identity exhausted.");
            return high + 1;
        }

        private void ValidatePreparedStageIdentity(GameSlice candidate, ulong proposedRunId)
        {
            ulong actual = StageRunIdOf(candidate);
            if (actual != 0 && (actual != proposedRunId || actual <= StageRunIdOf(_context.Slice)))
                throw new InvalidOperationException("Prepared session has a stale or mismatched StageRunId.");
        }

        internal static CharacterTestNavigationSessionBuildInput? CompileRequiredLevelNavigation(string levelId,
            CharacterTestNavigationSceneSettings settings, CharacterTestWorldObjectSceneCache world,
            out CharacterTestNavigationSceneInputResult result)
        {
            if (CharacterTestNavigationSceneInputCompiler.TryCompile(settings, world, out var input, out result)) return input;
            if (result.Status == CharacterTestNavigationSceneInputStatus.Disabled) return null;
            throw new InvalidOperationException(levelId + "/navigation/input: " + result.Status);
        }

        internal static void ValidateRequiredLevelNavigation(string levelId, CharacterTestNavigationSceneInputResult input,
            CharacterTestNavigationSessionBuildResult build)
        {
            if (input.Status != CharacterTestNavigationSceneInputStatus.Disabled && !build.Success)
                throw new InvalidOperationException(levelId + "/navigation/build: " + build.Status);
        }

        internal sealed class PreparedLevelSession : IDisposable
        {
            internal PreparedLevelSession(GameSlice slice, MxLevelAuthoringDocument document, CharacterTestWorldObjectSceneCache world,
                CharacterTestSceneCharacterCache characters, CharacterTestGameplayTriggerSceneCache triggers, CharacterTestGuidanceSceneCache guidance, ulong runId,
                TacticalDeployment deployment)
            { Slice = slice; Document = document; World = world; Characters = characters; Triggers = triggers; Guidance = guidance; RunId = runId; Deployment = deployment; }
            internal GameSlice Slice { get; private set; }
            internal MxLevelAuthoringDocument Document { get; }
            internal CharacterTestWorldObjectSceneCache World { get; }
            internal CharacterTestSceneCharacterCache Characters { get; }
            internal CharacterTestGameplayTriggerSceneCache Triggers { get; }
            internal CharacterTestGuidanceSceneCache Guidance { get; }
            internal ulong RunId { get; }
            internal TacticalDeployment Deployment { get; }
            internal GameSlice Take() { var slice = Slice; Slice = null; return slice; }
            public void Dispose() { Slice?.Dispose(); Slice = null; }
        }
    }
}
