using System;

namespace MxFramework.CharacterApplication
{
    public readonly struct CharacterSpawnPlan
    {
        public CharacterSpawnPlan(
            SpawnProfileId spawnProfileId,
            CharacterConfigId characterId,
            EquipmentLoadoutId loadoutId,
            string teamId,
            CharacterControllerKind controllerKind,
            CharacterPoseEntry pose,
            string debugName,
            CharacterDiagnostic[] diagnostics)
        {
            SpawnProfileId = spawnProfileId;
            CharacterId = characterId;
            LoadoutId = loadoutId;
            TeamId = teamId ?? string.Empty;
            ControllerKind = controllerKind;
            Pose = pose;
            DebugName = debugName ?? string.Empty;
            Diagnostics = diagnostics ?? Array.Empty<CharacterDiagnostic>();
        }

        public SpawnProfileId SpawnProfileId { get; }
        public CharacterConfigId CharacterId { get; }
        public EquipmentLoadoutId LoadoutId { get; }
        public string TeamId { get; }
        public CharacterControllerKind ControllerKind { get; }
        public CharacterPoseEntry Pose { get; }
        public string DebugName { get; }
        public CharacterDiagnostic[] Diagnostics { get; }
    }

    public static class SpawnPlanResolver
    {
        public static CharacterSpawnPlan Resolve(SpawnProfileConfig spawnProfile, CharacterSpawnRequest request)
        {
            var diagnostics = new CharacterDiagnosticBuilder();
            if (spawnProfile == null)
            {
                diagnostics.Add(
                    CharacterDiagnosticSeverity.Error,
                    CharacterDiagnosticCode.MissingSpawnProfile,
                    SpawnProfileConfig.TableName,
                    request.SpawnProfileId.Value,
                    string.Empty,
                    nameof(request.SpawnProfileId),
                    "Spawn profile is required.");
                return new CharacterSpawnPlan(request.SpawnProfileId, default, default, string.Empty, CharacterControllerKind.None, default, string.Empty, diagnostics.ToArray());
            }

            if (request.SpawnProfileId.IsValid && !request.SpawnProfileId.Equals(spawnProfile.SpawnProfileId))
            {
                diagnostics.Add(
                    CharacterDiagnosticSeverity.Error,
                    CharacterDiagnosticCode.InvalidSpawnRequest,
                    SpawnProfileConfig.TableName,
                    spawnProfile.Id,
                    spawnProfile.StableId,
                    nameof(request.SpawnProfileId),
                    "Spawn request references a different spawn profile.");
            }

            CharacterConfigId characterId = request.CharacterOverride.HasValue && request.CharacterOverride.Value.IsValid
                ? request.CharacterOverride.Value
                : spawnProfile.CharacterId;

            EquipmentLoadoutId loadoutId = request.LoadoutOverride.HasValue && request.LoadoutOverride.Value.IsValid
                ? request.LoadoutOverride.Value
                : spawnProfile.EquipmentLoadoutId;

            string teamId = !string.IsNullOrEmpty(request.TeamOverride)
                ? request.TeamOverride
                : spawnProfile.TeamId;

            CharacterControllerKind controllerKind = request.ControllerOverride.HasValue
                ? request.ControllerOverride.Value
                : spawnProfile.ControllerKind;

            CharacterPoseEntry pose = request.PoseOverride.HasValue
                ? request.PoseOverride.Value
                : spawnProfile.SpawnPose;

            string debugName = !string.IsNullOrEmpty(request.DebugNameOverride)
                ? request.DebugNameOverride
                : spawnProfile.DebugName;

            if (!characterId.IsValid)
            {
                diagnostics.Add(
                    CharacterDiagnosticSeverity.Error,
                    CharacterDiagnosticCode.MissingCharacterConfig,
                    SpawnProfileConfig.TableName,
                    spawnProfile.Id,
                    spawnProfile.StableId,
                    nameof(SpawnProfileConfig.CharacterId),
                    "Resolved spawn plan has no valid character config id.");
            }

            if (!loadoutId.IsValid)
            {
                diagnostics.Add(
                    CharacterDiagnosticSeverity.Warning,
                    CharacterDiagnosticCode.MissingDefaultLoadout,
                    SpawnProfileConfig.TableName,
                    spawnProfile.Id,
                    spawnProfile.StableId,
                    nameof(SpawnProfileConfig.EquipmentLoadoutId),
                    "Resolved spawn plan has no equipment loadout override.");
            }

            if (string.IsNullOrEmpty(teamId))
            {
                diagnostics.Add(
                    CharacterDiagnosticSeverity.Warning,
                    CharacterDiagnosticCode.InvalidSpawnRequest,
                    SpawnProfileConfig.TableName,
                    spawnProfile.Id,
                    spawnProfile.StableId,
                    nameof(SpawnProfileConfig.TeamId),
                    "Resolved spawn plan has no team id.");
            }

            return new CharacterSpawnPlan(spawnProfile.SpawnProfileId, characterId, loadoutId, teamId, controllerKind, pose, debugName, diagnostics.ToArray());
        }
    }

    public readonly struct CharacterSaveStateBinding
    {
        public CharacterSaveStateBinding(
            CharacterConfigId characterId,
            string characterStableId,
            int configVersion,
            EquipmentLoadoutId loadoutId,
            EquipmentStateId expectedActiveEquipmentStateId)
        {
            CharacterId = characterId;
            CharacterStableId = characterStableId ?? string.Empty;
            ConfigVersion = configVersion;
            LoadoutId = loadoutId;
            ExpectedActiveEquipmentStateId = expectedActiveEquipmentStateId;
        }

        public CharacterConfigId CharacterId { get; }
        public string CharacterStableId { get; }
        public int ConfigVersion { get; }
        public EquipmentLoadoutId LoadoutId { get; }
        public EquipmentStateId ExpectedActiveEquipmentStateId { get; }
    }

    public readonly struct SaveStateBindingResolveResult
    {
        public SaveStateBindingResolveResult(bool canReconstruct, CharacterDiagnostic[] diagnostics)
        {
            CanReconstruct = canReconstruct;
            Diagnostics = diagnostics ?? Array.Empty<CharacterDiagnostic>();
        }

        public bool CanReconstruct { get; }
        public CharacterDiagnostic[] Diagnostics { get; }
    }

    public static class SaveStateBindingResolver
    {
        public static SaveStateBindingResolveResult Resolve(
            CharacterSaveStateBinding binding,
            CharacterConfig character,
            EquipmentStateResolveResult equipmentStateResult)
        {
            var diagnostics = new CharacterDiagnosticBuilder();
            if (!binding.CharacterId.IsValid || string.IsNullOrEmpty(binding.CharacterStableId))
            {
                diagnostics.Add(
                    CharacterDiagnosticSeverity.Error,
                    CharacterDiagnosticCode.InvalidSaveStateBinding,
                    CharacterConfig.TableName,
                    binding.CharacterId.Value,
                    binding.CharacterStableId,
                    nameof(CharacterSaveStateBinding.CharacterId),
                    "SaveState binding must include config id and stable id.");
            }

            if (character == null)
            {
                diagnostics.Add(
                    CharacterDiagnosticSeverity.Error,
                    CharacterDiagnosticCode.MissingCharacterConfig,
                    CharacterConfig.TableName,
                    binding.CharacterId.Value,
                    binding.CharacterStableId,
                    nameof(character),
                    "Character config referenced by SaveState is missing.");
            }
            else
            {
                if (!character.CharacterId.Equals(binding.CharacterId)
                    || !CharacterResolverUtility.EqualsOrdinal(character.StableId, binding.CharacterStableId))
                {
                    diagnostics.Add(
                        CharacterDiagnosticSeverity.Error,
                        CharacterDiagnosticCode.SaveStateBindingMismatch,
                        CharacterConfig.TableName,
                        character.Id,
                        character.StableId,
                        nameof(CharacterSaveStateBinding.CharacterStableId),
                        "SaveState character config id or stable id does not match current config.");
                }
            }

            if (binding.ExpectedActiveEquipmentStateId.IsValid
                && equipmentStateResult.IsSuccess
                && !binding.ExpectedActiveEquipmentStateId.Equals(equipmentStateResult.ActiveStateId))
            {
                diagnostics.Add(
                    CharacterDiagnosticSeverity.Warning,
                    CharacterDiagnosticCode.SaveStateActiveStateMismatch,
                    EquipmentStateConfig.TableName,
                    binding.ExpectedActiveEquipmentStateId.Value,
                    string.Empty,
                    nameof(CharacterSaveStateBinding.ExpectedActiveEquipmentStateId),
                    "Current config resolves to a different active equipment state than the SaveState expectation.");
            }

            CharacterDiagnostic[] issues = diagnostics.ToArray();
            bool canReconstruct = true;
            for (int i = 0; i < issues.Length; i++)
            {
                if (issues[i].Severity == CharacterDiagnosticSeverity.Error)
                {
                    canReconstruct = false;
                    break;
                }
            }

            return new SaveStateBindingResolveResult(canReconstruct, issues);
        }
    }
}
