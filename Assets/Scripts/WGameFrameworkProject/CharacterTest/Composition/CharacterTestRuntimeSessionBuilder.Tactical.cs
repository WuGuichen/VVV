using System;
using System.Collections.Generic;
using MxFramework.Game.CharacterTest.TacticalCard;
using MxFramework.Runtime;
using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestRuntimeSessionBuilder
    {
        private CharacterTestCharacterSpawnSpec[] BuildTacticalPreparationSpawnSpecs()
        {
            TacticalBattleSetup setup = _tacticalPreparationOverride ?? CharacterTacticalServices.Preparation?.Working;
            if (setup?.Entities == null || setup.Entities.Count == 0)
                return Array.Empty<CharacterTestCharacterSpawnSpec>();
            var specs = new List<CharacterTestCharacterSpawnSpec>(setup.Entities.Count);
            for (int i = 0; i < setup.Entities.Count; i++)
            {
                TacticalBattleEntitySetup entity = setup.Entities[i];
                if (entity == null || entity.CharacterId <= 0 || entity.TeamId <= 0) continue;
                specs.Add(new CharacterTestCharacterSpawnSpec(entity.CharacterId, entity.TeamId,
                    entity.X, entity.Y, entity.Z, CharacterTestCharacterControlOwner.Scripted,
                    runtimeAiPlannerProfileSlot: i + 1));
            }
            return specs.ToArray();
        }

        private static CharacterTestCharacterSpawnSpec[] BuildTacticalAutoBattleSpawnSpecs(TacticalDeployment deployment)
        {
            if (deployment?.Participants == null) return Array.Empty<CharacterTestCharacterSpawnSpec>();
            var specs = new CharacterTestCharacterSpawnSpec[deployment.Participants.Count];
            for (int i = 0; i < specs.Length; i++)
            {
                TacticalDeploymentParticipant entity = deployment.Participants[i];
                specs[i] = new CharacterTestCharacterSpawnSpec(entity.CharacterId, entity.TeamId,
                    entity.Position.x, entity.Position.y, entity.Position.z,
                    entity.IsPlayerControlled
                        ? CharacterTestCharacterControlOwner.LocalPlayer
                        : CharacterTestCharacterControlOwner.AiControlled,
                    runtimeAiPlannerProfileSlot: i + 1);
            }
            return specs;
        }

        internal void RebuildTacticalPreparationSession(string message = "")
        {
            using var candidate = PrepareTacticalSession();
            PublishTacticalSession(candidate, string.IsNullOrEmpty(message) ? "Tactical preparation." : message);
            _context.MainMenuVisible = false;
            _context.MainMenuCoordinator.SetPaused(false);
            _gameManager.GetComponent<TacticalCardSliceCoordinator>()?.NotifyPreparationReady(message);
        }

        internal void RebuildTacticalBattleSession(TacticalDeployment deployment)
        {
            if (deployment == null) throw new ArgumentNullException(nameof(deployment));
            using var candidate = PrepareTacticalSession(deployment);
            PublishTacticalSession(candidate, "Confirmed tactical deployment.");
            _context.MainMenuVisible = false;
            _context.MainMenuCoordinator.SetPaused(false);
        }

        internal PreparedTacticalSession PrepareTacticalSession(TacticalDeployment deployment = null, TacticalBattleSetup setup = null)
        {
            float previousScale = Time.timeScale;
            var candidateContext = new CharacterTestRuntimeContext(_owner, _profile)
            {
                Logger = _context.Logger,
                NetworkCoordinator = _context.NetworkCoordinator,
                InputProvider = _context.InputProvider
            };
            var builder = new CharacterTestRuntimeSessionBuilder(_owner, candidateContext, _profile)
            {
                _preparingTacticalCandidate = true,
                _tacticalPreparationOverride = setup,
                _tacticalStageRunId = HighestKnownTacticalStageRunId()
            };
            try
            {
                builder.RebuildRuntimeSession(CharacterTestNetworkSessionKind.Offline, CharacterTestNetworkMode.Disabled,
                    null, "Prepared tactical candidate.", tacticalDeployment: deployment);
                if (candidateContext.Slice == null) throw new InvalidOperationException("Tactical candidate did not create a session.");
                return new PreparedTacticalSession(candidateContext.Slice, deployment != null, builder._tacticalStageRunId);
            }
            catch { candidateContext.Slice?.Dispose(); throw; }
            finally { Time.timeScale = previousScale; }
        }

        internal void PublishTacticalSession(PreparedTacticalSession candidate, string message,
            CharacterTestWorldObjectSceneCache selectedLevelWorld = null)
        {
            if (candidate == null || candidate.Slice == null) throw new InvalidOperationException("Tactical candidate is unavailable.");
            ValidatePreparedStageIdentity(candidate.Slice, candidate.StageRunId);
            GameSlice next = candidate.Take();
            GameSlice previous = _context.Slice;
            var errors = new List<Exception>();
            CharacterTestCleanupScope.Attempt(errors, "PreviousUi", () => _context.UiRuntime?.DetachSlice());
            CharacterTestCleanupScope.Attempt(errors, "PreviousCamera", ReleaseThirdPersonCamera);
            CharacterTestCleanupScope.Attempt(errors, "PreviousAudioSnapshots", () => _context.AudioMixSnapshotController?.Dispose());
            CharacterTestCleanupScope.Attempt(errors, "PreviousMusic", () => _context.AudioMusicStateController?.Dispose());
            CharacterTestCleanupScope.Attempt(errors, "PreviousAmbience", () => _context.AudioAmbienceController?.Dispose());
            // Publish synchronously; retirement failures are diagnostics, not permission to
            // destroy the fully prepared replacement or to claim the old world was restored.
            _context.Slice = next;
            _context.ActiveLevelWorldCache = selectedLevelWorld;
            if (selectedLevelWorld == null)
            {
                _context.ActiveLevelId = string.Empty;
                _context.ActiveLevelContentHash = string.Empty;
            }
            _context.NetworkRunner = null;
            _context.PlayerBehaviorDeckService = null;
            _context.MovableNaturalObjectViewBridge = null;
            _context.AudioMixController = null;
            _context.AudioMixSnapshotController = null;
            _context.AudioMusicStateController = null;
            _context.AudioAmbienceController = null;
            _context.FixedTickDriver.Reset();
            _context.ActiveNetworkSessionKind = CharacterTestNetworkSessionKind.Offline;
            _context.LastNetworkSessionMessage = message;
            _tacticalStageRunId = Math.Max(_tacticalStageRunId, candidate.StageRunId);
            CharacterTestCleanupScope.Attempt(errors, "PreviousSession", () => previous?.Dispose());
            Time.timeScale = 1f; // The replacement must be able to spawn before its own Ready pause is acquired.
            var runtime = next.Services.Modules.TacticalStage;
            var mixed = next.Services.Modules.MixedPlayerTacticalStage;
            try
            {
                _gameManager.BindTacticalControl(runtime?.Stage ?? mixed?.Stage, runtime?.Commands ?? mixed?.Commands);
                _gameManager.GetComponent<TacticalCardHud>()?.Bind(runtime?.Stage ?? mixed?.Stage,
                    runtime?.Commands ?? mixed?.Commands, next.Services.Modules.StoryFlowBridge,
                    EnsureCursorController(), _context.InputProvider);
                ConfigureMovableNaturalObjectViewBridge();
                next.Services.Presentation.SetHeadPadDisplayMode(_profile.HeadPadDisplayMode);
                InitializeThirdPersonCamera(CharacterTestCameraMode.MultiTargetAuto, candidate.IsBattle);
                var audio = next.Services.Modules.AudioService;
                _context.AudioMixController = CreateAudioMixController(audio);
                _context.SettingsRuntime?.AttachAudioService(audio, _context.AudioMixController);
                ConfigureUiRuntime(LoadStoryContent(), true);
            }
            catch (Exception error)
            {
                errors.Add(error);
                _context.MainMenuCoordinator.SetPaused(true);
                _context.LastNetworkSessionMessage = "会话已创建，但界面绑定失败：" + error.Message;
                _context.Logger?.Warning("GameManager", _context.LastNetworkSessionMessage);
                throw new TacticalSessionPresentationException(_context.LastNetworkSessionMessage, error);
            }
            if (errors.Count != 0) _context.Logger?.Warning("GameManager", "Tactical publication/retirement diagnostics: " + new AggregateException(errors).Message);
        }

        internal sealed class PreparedTacticalSession : IDisposable
        {
            internal PreparedTacticalSession(GameSlice slice, bool battle, ulong run) { Slice = slice; IsBattle = battle; StageRunId = run; }
            internal GameSlice Slice { get; private set; }
            internal bool IsBattle { get; }
            internal ulong StageRunId { get; }
            internal GameSlice Take() { var result = Slice; Slice = null; return result; }
            public void Dispose() { Slice?.Dispose(); Slice = null; }
        }
    }

    internal sealed class TacticalSessionPresentationException : InvalidOperationException
    {
        internal TacticalSessionPresentationException(string message, Exception inner) : base(message, inner) { }
    }
}
