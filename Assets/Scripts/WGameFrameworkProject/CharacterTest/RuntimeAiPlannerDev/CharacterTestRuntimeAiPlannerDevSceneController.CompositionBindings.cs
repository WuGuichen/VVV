using System;
using System.Collections.Generic;
using System.Linq;

namespace MxFramework.Game.CharacterTest
{
    public sealed partial class CharacterTestRuntimeAiPlannerDevSceneController
    {
        private bool TryPrepareRevisionInstalls(IReadOnlyList<int> slots, CharacterTestRuntimeAiLabCandidateRevision revision,
            out CharacterTestRuntimeAiPlannerBehaviorOverlayInstallRequest[] requests,
            out CharacterTestRuntimeAiLabCandidateRevision[] revisions, out string diagnostic)
        {
            requests = null; revisions = new CharacterTestRuntimeAiLabCandidateRevision[slots.Count]; diagnostic = string.Empty;
            bool tactical = revision.LogicPrograms.Any(p => p?.TacticalSource != null);
            var prepared = new CharacterTestRuntimeAiPlannerBehaviorOverlayInstallRequest[slots.Count];
            var runtime = _context?.Slice?.CharacterRuntime;
            for (int i = 0; i < slots.Count; i++)
            {
                var current = revision;
                if (tactical)
                {
                    var actors = runtime?.Entities.Where(e => e.Control.Owner != CharacterTestCharacterControlOwner.None
                        && e.Control.RuntimeAiPlannerProfileSlot == slots[i]).ToArray();
                    if (actors == null || actors.Length == 0) { diagnostic = "战术绑定没有实际角色：profile " + slots[i]; return false; }
                    var programs = revision.LogicPrograms.ToArray(); bool changed = false;
                    for (int p = 0; p < programs.Length; p++)
                    {
                        var frozen = programs[p]?.TacticalSource;
                        if (frozen == null) continue;
                        bool preparedInputs = frozen.HasExplicitBindings
                            ? frozen.TryPrepareExplicit(runtime, actors[0], out var inputs, out diagnostic)
                            : PrepareTacticalInputs(runtime, actors[0], out inputs, out diagnostic, requirements: frozen.Requirements);
                        if (!preparedInputs) return false;
                        if (i == 0)
                        {
                            // The preferred slot is the original target captured by the candidate/request.
                            if (frozen.InputFingerprint != inputs.DependencyFingerprint)
                            { diagnostic = "测试动作装配在准备后已变化，请重新测试当前修改。"; return false; }
                            if (!frozen.ValidateBinding(runtime, actors[0], out diagnostic)) return false;
                        }
                        else if (frozen.InputFingerprint != inputs.DependencyFingerprint || !frozen.ValidateBinding(runtime, actors[0], out _))
                        {
                            // A peer target may have another opener even with the same character definition.
                            // Shared source, separate actual-input compilation; never read the live draft here.
                            if (frozen.IsLegacy)
                            {
                                if (!CharacterTestTacticalAuthoringCompiler.TryCompile(frozen.CloneLegacySource(), inputs, out var compiled, out var errors))
                                { diagnostic = string.Join("; ", errors); return false; }
                                programs[p] = compiled.Program.WithTacticalSource(frozen.Rebind(inputs, null)).WithSelectionDefinition(programs[p].SelectionDefinition);
                            }
                            else
                            {
                                if (!CharacterTestTacticalDslCompiler.TryCompile(frozen.CloneDocument(), inputs, out var compiled, out diagnostic)) return false;
                                programs[p] = compiled.Program.WithTacticalSource(frozen.Rebind(inputs, compiled.SourceLocations, compiled.SharedCompilations, compiled.Program)).WithSelectionDefinition(programs[p].SelectionDefinition);
                            }
                            changed = true;
                        }
                        foreach (var actor in actors)
                            if (!programs[p].TacticalSource.ValidateBinding(runtime, actor, out diagnostic)) return false;
                    }
                    if (changed)
                    {
                        var profile = revision.Profile;
                        long hash = ComputeCandidateCompositionHash(profile, _matchConfig.BehaviorGroups.DefinitionCatalog);
                        var identities = new CharacterTestRuntimeAiLabRevisionCardIdentity[programs.Length];
                        for (int p = 0; p < programs.Length; p++)
                        {
                            hash = MixCandidateHash(hash, profile.Entries[p].EntryId); hash = MixCandidateHash(hash, programs[p].SemanticHash);
                            if (programs[p].SelectionDefinition != null) hash = MixCandidateHash(hash, programs[p].SelectionDefinition.StableHash);
                            identities[p] = revision.CardIdentities[p].WithProgramHash(programs[p].SemanticHash);
                        }
                        var comparison = new CharacterTestRuntimeAiLabCandidateComparisonReport(programs.Length,
                            programs.Sum(p => p.Execution != null ? p.Execution.Events.Length : p.Stages.Count),
                            programs.Sum(p => p.Execution != null ? p.Execution.Conditions.Length : p.ActivationConditions.Count),
                            "实际角色装配", hash, repeatable: true);
                        if (!CharacterTestRuntimeAiLabCandidateRevision.TryCreate(revision.Revision, hash, profile, comparison,
                            programs, identities, revision.GroupDraftId, out current, out diagnostic)) return false;
                    }
                }
                revisions[i] = current;
                prepared[i] = new CharacterTestRuntimeAiPlannerBehaviorOverlayInstallRequest(slots[i], current.Profile, current.LogicPrograms, current.AuthorityHash);
            }
            requests = prepared; return true;
        }
    }
}
