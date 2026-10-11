using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest.TacticalCard
{
    internal static class TacticalAutoBattleRuntimeInstaller
    {
        // This installer binds fixed logic programs. Point input is acquired per execution by
        // the Stage/Driver owner, and no automatic point policy is configured in production yet.
        internal static string AutomaticInputIssue(TacticalActionCardDefinition card)
            => card?.PositionRecall != null ? "execution:PositionRecallPlayerOnly" : card?.PointMove != null ? "execution-input:UnsupportedProvider" : string.Empty;

        internal static bool TryInstall(CharacterTestSliceServices services, TacticalDeployment deployment, out string issue,
            Func<TacticalDeploymentParticipant, ITacticalAutomaticCardExecutionPort> automaticOwner = null)
        {
            if (!TryBuildRequests(services, deployment, out var requests, out var actorChanges, out issue, automaticOwner)) return false;
            bool installed = false;
            try
            {
                foreach (var change in actorChanges) change.Apply();
                if (!services.Modules.RuntimeAiPlannerCommands.TryApplyBehaviorCompositionOverlayBatch(
                        requests, out CharacterTestRuntimeAiPlannerBehaviorHotUpdateResult result))
                {
                    issue = string.IsNullOrWhiteSpace(result.ReportText) ? "自动作战行为安装失败。" : result.ReportText;
                    return false;
                }
                installed = true;
                foreach (var entity in services.CharacterEntities)
                    if (!services.CharacterRuntime.World.TryGetTacticalMotion(entity.Id.Value, out _))
                        services.CharacterRuntime.World.BindTacticalMotion(entity.Id.Value, new MomentumLedger(deployment.InitialMotion));
                return true;
            }
            finally
            {
                if (!installed) foreach (var change in actorChanges) change.Restore();
            }
        }

        /// <summary>Checks the actual actors and frozen cards before replacing the preparation session.</summary>
        internal static bool TryValidatePreparation(CharacterTestSliceServices services, TacticalDeployment deployment, out string issue,
            Func<TacticalDeploymentParticipant, ITacticalAutomaticCardExecutionPort> automaticOwner = null)
            => TryBuildRequests(services, deployment, out _, out _, out issue, automaticOwner);

        private static bool TryBuildRequests(CharacterTestSliceServices services, TacticalDeployment deployment,
            out CharacterTestRuntimeAiPlannerBehaviorOverlayInstallRequest[] built, out ActorPlacement[] actorChanges, out string issue,
            Func<TacticalDeploymentParticipant, ITacticalAutomaticCardExecutionPort> automaticOwner)
        {
            built = null;
            actorChanges = null;
            issue = string.Empty;
            if (services?.CharacterRuntime == null || services.Modules.RuntimeAiPlannerCommands == null || deployment == null)
            { issue = "自动对战运行时尚未准备完成。"; return false; }
            // Reject before actor/control mutation, rather than compiling an unbound point contract.
            var automaticOwners = new Dictionary<string, ITacticalAutomaticCardExecutionPort>(StringComparer.Ordinal);
            foreach (var participant in deployment.Participants)
                if (!participant.IsPlayerControlled)
                    foreach (var card in participant.Cards)
                    {
                        string inputIssue = AutomaticInputIssue(card as TacticalActionCardDefinition);
                        if (inputIssue.Length == 0) continue;
                        if (!automaticOwners.TryGetValue(participant.EntityId, out var owner))
                        {
                            try { owner = automaticOwner?.Invoke(participant); }
                            catch (Exception exception)
                            { issue = "execution:AutomaticOwnerResolverException:" + exception.GetType().Name; return false; }
                            if (owner == null) { issue = inputIssue; return false; }
                            automaticOwners.Add(participant.EntityId, owner);
                        }
                    }
            var catalogState = CharacterTestRuntimeAiPlannerProjectCatalogLoader.Load(
                    new CharacterTestRuntimeAiLabBehaviorCardReferenceCatalog(null, services.CharacterRuntime.ActionRuntime.Registry));
            if (!catalogState.Success) { issue = catalogState.Diagnostic; return false; }
            var requests = new List<CharacterTestRuntimeAiPlannerBehaviorOverlayInstallRequest>();
            var assignedEntities = new HashSet<int>();
            var placements = new List<ActorPlacement>();
            for (int participantIndex = 0; participantIndex < deployment.Participants.Count; participantIndex++)
            {
                TacticalDeploymentParticipant participant = deployment.Participants[participantIndex];
                if (participant.IsPlayerControlled) continue;
                int profileSlot = participantIndex + 1;
                CharacterTestCharacterEntity entity = services.CharacterEntities.FirstOrDefault(value =>
                    value.Control.RuntimeAiPlannerProfileSlot == profileSlot);
                bool reassignControl = entity == null;
                if (entity == null)
                {
                    float best = float.PositiveInfinity;
                    foreach (CharacterTestCharacterEntity candidate in services.CharacterEntities)
                    {
                        if (assignedEntities.Contains(candidate.Id.Value)
                            || candidate.Identity.CharacterId != participant.CharacterId
                            || candidate.Team.TeamId != participant.TeamId) continue;
                        float dx = candidate.Transform.X - participant.Position.x;
                        float dz = candidate.Transform.Z - participant.Position.z;
                        float distance = dx * dx + dz * dz;
                        if (distance >= best) continue;
                        best = distance; entity = candidate;
                    }
                }
                if (entity == null) { issue = participant.DisplayName + " 尚未生成。"; return false; }
                placements.Add(new ActorPlacement(entity, profileSlot, participant, reassignControl));
                assignedEntities.Add(entity.Id.Value);
                var profile = new CharacterTestRuntimeAiPlannerBehaviorCompositionProfile(
                    "tactical-preparation." + participant.EntityId);
                var programs = new List<CharacterTestRuntimeAiLabCompiledLogicProgram>();
                var automaticBindings = new Dictionary<string, TacticalAutomaticExecutionBinding>(StringComparer.Ordinal);
                for (int cardIndex = 0; cardIndex < participant.Cards.Count; cardIndex++)
                {
                    if (!(participant.Cards[cardIndex] is TacticalActionCardDefinition definition)) continue;
                    CharacterTestRuntimeAiPlannerPublishedBehaviorCard published = catalogState.CardCatalog.Cards.FirstOrDefault(card =>
                        card.StableCardId == definition.PublishedCardId && card.Revision == definition.PublishedRevision
                        && card.ContentHash.ToString(CultureInfo.InvariantCulture) == definition.PublishedContentHash);
                    if (published?.TacticalSource == null)
                    { issue = participant.DisplayName + " 的行为卡发布源无效：" + definition.Name; return false; }
                    if (!participant.TryGetBindings(definition.DeckEntryInstanceId, out IReadOnlyDictionary<string, int> bindings))
                    { issue = participant.DisplayName + " 的动作装配快照缺失。"; return false; }
                    CharacterTestRuntimeAiLabCompiledLogicProgram program;
                    if (definition.PositionRecall != null) { issue = "execution:PositionRecallPlayerOnly"; return false; }
                    if (definition.PointMove != null)
                    {
                        if (!automaticOwners.TryGetValue(participant.EntityId, out var owner))
                        { issue = "execution-input:UnsupportedProvider"; return false; }
                        if (!owner.CanBind(entity.GameplayEntityId, definition, out issue)) return false;
                        if (!published.TacticalSource.TryCompileAutomaticContract(published.StableCardId, published.Metadata.DisplayName,
                            published.Revision, entity.Identity.CharacterId, out program, out issue)) return false;
                    }
                    else
                    {
                    var inputSource = new CharacterTacticalBoundCardInputs(services.CharacterRuntime, entity.Id.Value, bindings)
                        .WithRequirements(published.TacticalSource.InputRequirements);
                    if (!published.TacticalSource.HasPlayerActionSlot) inputSource.UseFunctionalResolvers();
                    if (!inputSource.TryPrepare(out CharacterTestTacticalCardInputs inputs, out issue))
                    {
                        issue = participant.DisplayName + " / " + definition.Name + " / 动作 "
                            + string.Join(",", bindings.Values.OrderBy(value => value)) + "：" + issue;
                        return false;
                    }
                    if (!published.TacticalSource.TryCompile(published.StableCardId, published.Metadata.DisplayName,
                            published.Revision, inputs, out program, out issue))
                        return false;
                    }
                    var entry = new CharacterTestRuntimeAiPlannerBehaviorModuleEntry(
                        CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationIdle, true, cardIndex)
                    { EntryId = published.StableCardId + "#" + definition.DeckEntryInstanceId.ToString(CultureInfo.InvariantCulture) };
                    entry.NumericId = 100000 + participantIndex * 1000 + cardIndex + 1;
                    entry.DefinitionNumericId = 4;
                    entry.SetResponsibilities(published.Metadata.Responsibilities);
                    CharacterTestRuntimeAiPlannerPlayerDeckRuntimeCompiler.ConfigureCanonicalArbitration(entry, published.Metadata);
                    if (definition.PositionRecall != null) { issue = "execution:PositionRecallPlayerOnly"; return false; }
                    if (definition.PointMove != null)
                    {
                        var binding = new TacticalAutomaticExecutionBinding(definition, automaticOwners[participant.EntityId]);
                        if (!binding.Matches(program.TacticalSource)) { issue = "execution:AutomaticOwnerBindingMismatch"; return false; }
                        automaticBindings.Add(entry.EntryId, binding);
                    }
                    profile.Entries.Add(entry);
                    programs.Add(program.WithSelectionDefinition(CharacterTestBehaviorCardSelectionDefinition.FromPublished(published)));
                }
                if (profile.Entries.Count == 0)
                {
                    var idle = new CharacterTestRuntimeAiPlannerBehaviorModuleEntry(
                        CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationIdle, true, 0)
                    { NumericId = 100000 + participantIndex * 1000 + 1, DefinitionNumericId = 4 };
                    profile.Entries.Add(idle);
                }
                var hash = new RuntimeContentHashAccumulator();
                hash.AddStringStable("tactical-auto-battle", participant.EntityId);
                for (int i = 0; i < participant.Cards.Count; i++) hash.AddStringStable("card", participant.Cards[i].Id);
                requests.Add(new CharacterTestRuntimeAiPlannerBehaviorOverlayInstallRequest(
                    profileSlot, profile, programs, hash.ToHash(), automaticBindings.Count == 0 ? null : automaticBindings));
            }
            built = requests.ToArray();
            actorChanges = placements.ToArray();
            return true;
        }
        // Capture before validation completes; apply only after every exact card/owner binding succeeds.
        internal sealed class ActorPlacement
        {
            private readonly CharacterTestCharacterEntity _entity;
            private readonly CharacterTestCharacterControlComponent _control;
            private readonly CharacterTestCharacterTransformComponent _transform;
            private readonly CharacterTestCharacterControlComponent _nextControl;
            private readonly CharacterTestCharacterTransformComponent _nextTransform;
            internal ActorPlacement(CharacterTestCharacterEntity entity, int slot,
                TacticalDeploymentParticipant participant, bool reassignControl)
            {
                _entity = entity; _control = entity.Control; _transform = entity.Transform;
                _nextControl = reassignControl ? new CharacterTestCharacterControlComponent(
                    CharacterTestCharacterControlOwner.AiControlled, slot) : _control;
                _nextTransform = new CharacterTestCharacterTransformComponent(
                    participant.Position.x, _transform.Y, participant.Position.z, participant.YawDegrees);
            }
            internal void Apply() { _entity.SetControl(_nextControl); _entity.SetTransform(_nextTransform); }
            internal void Restore() { _entity.SetControl(_control); _entity.SetTransform(_transform); }
        }
    }
}
