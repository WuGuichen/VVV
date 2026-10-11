using System;
using System.Collections.Generic;
using System.Linq;
using MxFramework.CharacterAction;
using MxFramework.Core.Math;

namespace MxFramework.Game.CharacterTest
{
    public sealed partial class CharacterTestRuntimeAiPlannerDevSceneController
    {
        private sealed class TacticalPreparedEntry
        {
            internal CharacterTestTacticalCardInputs Inputs;
            internal string Issue;
            internal bool Success;
            internal CharacterTestTacticalSummaryRequest Request;
        }
        private readonly Dictionary<(object Entity, object Registry, object Rig, FixVector3 Shape, long Center, CharacterTestCharacterEquipmentComponent Equipment,
            int Character, int Action, CharacterTestTacticalInputRequirements Requirements, long Epoch), TacticalPreparedEntry> _tacticalPrepared = new();
        private CharacterTestTacticalSummaryRequest _preparedSummaryRequest;
        internal long TacticalInputBuildCount { get; private set; }
        internal long TacticalCompileBuildCount { get; private set; }

        private bool TryPrepareTacticalSlotInputsCore(int team, int slot, int character,
            CharacterTestTacticalInputRequirements requirements, bool fresh, out CharacterTestTacticalCardInputs inputs, out string issue)
        {
            using var startupProfile = CharacterTestStartupProfiler.PrepareInputs.Auto();
            inputs = null; issue = string.Empty; _preparedSummaryRequest = null;
            if (character <= 0) { issue = "当前角色配置缺失。"; return false; }
            if (requirements == CharacterTestTacticalInputRequirements.None) { inputs = CharacterTestTacticalCardInputs.Unbound(character); return true; }
            var runtime = _context?.Slice?.CharacterRuntime;
            CharacterTestCharacterEntity entity = null;
            if (runtime != null && _sessionBuilder != null && _sessionBuilder.TryResolveArenaRuntimeAiPlannerProfileSlot(team, slot, out int profile))
                entity = runtime.Entities.FirstOrDefault(e => e.Control.RuntimeAiPlannerProfileSlot == profile && e.Identity.CharacterId == character);
            var physics = runtime?.PhysicsRuntime;
            var registry = runtime?.ActionRuntime.Registry;
            int action = (requirements & (CharacterTestTacticalInputRequirements.PrimaryAction | CharacterTestTacticalInputRequirements.SpatialSummary)) != 0 ? TacticalTestAction(team, slot, character) : 0;
            FixVector3 shape = entity == null ? default : physics.GetCharacterHalfExtents(entity);
            long center = entity == null ? 0 : physics.GetCharacterCenterOffsetY(entity).RawValue;
            var key = ((object)entity, (object)registry, (object)entity?.AuthorityPose?.Definition, shape, center,
                entity?.Equipment ?? default, character, action, requirements, CharacterTestTacticalPreparationCatalog.Revision);
            if (!fresh && _tacticalPrepared.TryGetValue(key, out var retained))
            { inputs = retained.Inputs; issue = retained.Issue; _preparedSummaryRequest = retained.Request; return retained.Success; }
            var value = new TacticalPreparedEntry();
            TacticalInputBuildCount++;
            try
            {
                if (entity != null)
                {
                    value.Request = SummaryDescriptor(runtime.ActionRuntime, entity, action, requirements);
                    value.Success = PrepareTacticalInputs(runtime, entity, out value.Inputs, out value.Issue, action, requirements);
                }
                else
                {
                    var role = CharacterTestTacticalPreparationCatalog.Role(character, runtime?.ActionRuntime);
                    value.Request = SummaryDescriptor(role.Actions, role.Source, action, requirements);
                    value.Success = CharacterTestTacticalCardInputs.TryPrepare(role.Actions, role.Source, role.Physics,
                        CharacterTestDefaultBakedWeaponTraceSource.Instance, CharacterTestDefaultWeaponTraceBindingResolver.Instance,
                        new CharacterActionTacticalMetricsStore(), CharacterTestTacticalSummaryPreparation.StandardMeasurement(),
                        out value.Inputs, out value.Issue, action, requirements);
                }
            }
            catch (Exception error) when (error is ArgumentException || error is InvalidOperationException || error is System.IO.IOException)
            { value.Success = false; value.Issue = error.Message; }
            if (!value.Success) value.Issue = TargetIssue(team, slot, character, value.Request?.ActionId ?? action, value.Issue);
            if (_tacticalPrepared.Count >= 64) _tacticalPrepared.Clear();
            _tacticalPrepared[key] = value; _preparedSummaryRequest = value.Request; inputs = value.Inputs; issue = value.Issue; return value.Success;
        }

        private static CharacterTestTacticalSummaryRequest SummaryDescriptor(CharacterTestCharacterActionRuntime actions, CharacterTestCharacterEntity entity,
            int selectedAction, CharacterTestTacticalInputRequirements requirements)
        {
            if ((requirements & CharacterTestTacticalInputRequirements.PrimaryAction) == 0 || !actions.TryGetEntityActionSet(entity.Id, out var set)
                || !CharacterTestTacticalCardInputs.TryResolveSelectedAction(set, actions.Registry.Actions, selectedAction, out var action, out _)) return null;
            return new CharacterTestTacticalSummaryRequest(entity.Identity.CharacterId, action.ActionId.Value, set.StableId, entity.Equipment,
                string.Empty, CharacterTestTacticalSummaryPreparation.StandardMeasurement(), selectedAction > 0);
        }

        private long _tacticalActorRevision;
        private (object Entity, object Registry, object Rig, CharacterTestCharacterEquipmentComponent Equipment, FixVector3 Shape, long Center) _tacticalActorKey;
        private void InvalidateTacticalActorChanges()
        {
            var runtime = _context?.Slice?.CharacterRuntime;
            CharacterTestCharacterEntity entity = null;
            if (runtime != null && _sessionBuilder != null && _sessionBuilder.TryResolveArenaRuntimeAiPlannerProfileSlot(_selectedSetupTeamId, _selectedSetupSlotIndex, out int profile))
                entity = runtime.Entities.FirstOrDefault(e => e.Control.RuntimeAiPlannerProfileSlot == profile);
            var key = ((object)entity, (object)runtime?.ActionRuntime.Registry, (object)entity?.AuthorityPose?.Definition, entity?.Equipment ?? default,
                entity == null ? default : runtime.PhysicsRuntime.GetCharacterHalfExtents(entity), entity == null ? 0 : runtime.PhysicsRuntime.GetCharacterCenterOffsetY(entity).RawValue);
            if (!_tacticalActorKey.Equals(key)) { _tacticalCompiled.Clear(); _tacticalActorKey = key; _tacticalActorRevision++; }
        }

        private sealed class TacticalCompiledEntry
        {
            internal int CardRevision, ActionChoice;
            internal CharacterTestTacticalInputRequirements Requirements;
            internal CharacterTestTacticalSummaryRequest Request;
            internal string SemanticSource;
            internal CharacterTestTacticalCardInputs Inputs;
            internal CharacterTestTacticalDslCompilation Dsl;
            internal CharacterTestTacticalAuthoringCompilation Legacy;
            internal CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult Result;
            internal CharacterTestTacticalDslDescription Description;
        }
        private readonly Dictionary<string, TacticalCompiledEntry> _tacticalCompiled = new(StringComparer.Ordinal);
        private string _tacticalCompileOwner;
        private TacticalCompiledEntry CurrentTacticalCompilation(CharacterTestRuntimeAiLabBehaviorCardDraft card)
            => card != null && _tacticalCompiled.TryGetValue(card.CardDraftId, out var entry) ? entry : null;
    }
}
