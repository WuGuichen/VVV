using System;
using System.Collections.Generic;
using static MxFramework.CharacterAction.CharacterActionValidationHelpers;

namespace MxFramework.CharacterAction
{
    public enum CharacterActionMissingAnimationActionKeyPolicy
    {
        Error = 0,
        Warning = 1,
        Ignore = 2,
    }

    public readonly struct CharacterActionValidationDiagnostic : IEquatable<CharacterActionValidationDiagnostic>
    {
        public CharacterActionValidationDiagnostic(
            string sourcePath,
            string stablePath,
            string code,
            CharacterActionDiagnosticSeverity severity,
            string message)
        {
            if (string.IsNullOrEmpty(code))
                throw new ArgumentException("Diagnostic code cannot be empty.", nameof(code));

            SourcePath = sourcePath ?? string.Empty;
            StablePath = string.IsNullOrWhiteSpace(stablePath) ? "$" : stablePath;
            Code = code;
            Severity = severity;
            Message = message ?? string.Empty;
        }

        public string SourcePath { get; }
        public string StablePath { get; }
        public string Code { get; }
        public CharacterActionDiagnosticSeverity Severity { get; }
        public string Message { get; }

        public CharacterActionDiagnostic ToDiagnostic()
        {
            if (CharacterActionDiagnosticCodeText.TryParse(Code, out CharacterActionDiagnosticCode codeId))
                return new CharacterActionDiagnostic(codeId, Severity, Message);

            throw new InvalidOperationException(
                "Authoring-only validation code cannot be converted to a runtime diagnostic: " + Code);
        }

        public bool TryToDiagnostic(out CharacterActionDiagnostic diagnostic)
        {
            if (CharacterActionDiagnosticCodeText.TryParse(Code, out CharacterActionDiagnosticCode codeId))
            {
                diagnostic = new CharacterActionDiagnostic(codeId, Severity, Message);
                return true;
            }

            diagnostic = default(CharacterActionDiagnostic);
            return false;
        }

        public override string ToString()
        {
            return Severity + " " + Code + " " + SourcePath + " " + StablePath + " - " + Message;
        }

        public bool Equals(CharacterActionValidationDiagnostic other)
        {
            return string.Equals(SourcePath, other.SourcePath, StringComparison.Ordinal)
                && string.Equals(StablePath, other.StablePath, StringComparison.Ordinal)
                && string.Equals(Code, other.Code, StringComparison.Ordinal)
                && Severity == other.Severity
                && string.Equals(Message, other.Message, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is CharacterActionValidationDiagnostic other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = SourcePath != null ? SourcePath.GetHashCode() : 0;
                hash = (hash * 397) ^ (StablePath != null ? StablePath.GetHashCode() : 0);
                hash = (hash * 397) ^ (Code != null ? Code.GetHashCode() : 0);
                hash = (hash * 397) ^ (int)Severity;
                hash = (hash * 397) ^ (Message != null ? Message.GetHashCode() : 0);
                return hash;
            }
        }

        public static int Compare(CharacterActionValidationDiagnostic left, CharacterActionValidationDiagnostic right)
        {
            int compare = string.CompareOrdinal(left.SourcePath, right.SourcePath);
            if (compare != 0)
                return compare;
            compare = string.CompareOrdinal(left.StablePath, right.StablePath);
            if (compare != 0)
                return compare;
            compare = string.CompareOrdinal(left.Code, right.Code);
            if (compare != 0)
                return compare;
            return string.CompareOrdinal(left.Message, right.Message);
        }
    }

    internal static class CharacterActionValidationHelpers
    {
        internal static CharacterActionValidationDiagnostic Error(string sourcePath, string stablePath, string code, string message)
        {
            return new CharacterActionValidationDiagnostic(
                sourcePath,
                stablePath,
                code,
                CharacterActionDiagnosticSeverity.Error,
                message);
        }

        internal static CharacterActionValidationDiagnostic Warning(string sourcePath, string stablePath, string code, string message)
        {
            return new CharacterActionValidationDiagnostic(
                sourcePath,
                stablePath,
                code,
                CharacterActionDiagnosticSeverity.Warning,
                message);
        }

        internal static CharacterActionValidationDiagnostic[] Sort(List<CharacterActionValidationDiagnostic> diagnostics)
        {
            CharacterActionValidationDiagnostic[] result = diagnostics.ToArray();
            Array.Sort(result, CharacterActionValidationDiagnostic.Compare);
            return result;
        }
    }

    public sealed class CharacterActionValidationOptions
    {
        public static readonly CharacterActionValidationOptions Default = new CharacterActionValidationOptions();

        public CharacterActionValidationOptions(
            CharacterActionMissingAnimationActionKeyPolicy missingAnimationActionKeyPolicy = CharacterActionMissingAnimationActionKeyPolicy.Error,
            ICharacterActionCrossModuleValidator[] crossModuleValidators = null)
        {
            if (!Enum.IsDefined(typeof(CharacterActionMissingAnimationActionKeyPolicy), missingAnimationActionKeyPolicy))
                throw new ArgumentOutOfRangeException(nameof(missingAnimationActionKeyPolicy), "Missing animation action key policy is not defined.");

            MissingAnimationActionKeyPolicy = missingAnimationActionKeyPolicy;
            CrossModuleValidators = crossModuleValidators == null
                ? Array.Empty<ICharacterActionCrossModuleValidator>()
                : (ICharacterActionCrossModuleValidator[])crossModuleValidators.Clone();
        }

        public CharacterActionMissingAnimationActionKeyPolicy MissingAnimationActionKeyPolicy { get; }
        public ICharacterActionCrossModuleValidator[] CrossModuleValidators { get; }
    }

    public sealed class CharacterActionValidationContext
    {
        public CharacterActionValidationContext(
            CharacterActionConfigRegistry registry,
            CharacterActionValidationOptions options)
        {
            Registry = registry ?? throw new ArgumentNullException(nameof(registry));
            Options = options ?? CharacterActionValidationOptions.Default;
        }

        public CharacterActionConfigRegistry Registry { get; }
        public CharacterActionValidationOptions Options { get; }
    }

    public interface ICharacterActionCrossModuleValidator
    {
        CharacterActionValidationDiagnostic[] Validate(CharacterActionValidationContext context);
    }

    public interface ICharacterActionAnimationActionKeyProvider
    {
        bool ContainsAnimationActionKey(
            string actionSetStableId,
            CharacterActionId actionId,
            string animationActionKey);
    }

    public interface ICharacterActionCombatActionIdProvider
    {
        bool ContainsCombatActionId(
            string actionSetStableId,
            CharacterActionId actionId,
            int combatActionId);
    }

    public interface ICharacterActionPresentationResourceProvider
    {
        bool ContainsPresentationResource(
            string actionSetStableId,
            CharacterActionId actionId,
            CharacterActionResourceDependencyKind dependencyKind,
            string stableId);
    }

    public interface ICharacterActionNumericPresentationResourceProvider
    {
        bool ContainsNumericPresentationResource(CharacterActionId actionId,
            CharacterActionResourceDependencyKind dependencyKind, int id);
    }

    public interface ICharacterActionEquipmentTagProvider
    {
        bool ContainsEquipmentTag(string actionSetStableId, string equipmentTag);
    }

    public interface ICharacterActionCharacterEquipmentStateProvider
    {
        bool ContainsCharacterStableId(string characterStableId);
        bool ContainsEquipmentStateStableId(string characterStableId, string equipmentStateStableId);
    }

    public interface ICharacterActionAnimationValidator : ICharacterActionCrossModuleValidator
    {
    }

    public interface ICharacterActionCombatValidator : ICharacterActionCrossModuleValidator
    {
    }

    public interface ICharacterActionPresentationResourceValidator : ICharacterActionCrossModuleValidator
    {
    }

    public interface ICharacterActionEquipmentTagValidator : ICharacterActionCrossModuleValidator
    {
    }

    public interface ICharacterActionCharacterEquipmentStateValidator : ICharacterActionCrossModuleValidator
    {
    }

    public sealed class CharacterActionAnimationActionKeyValidator : ICharacterActionAnimationValidator
    {
        public CharacterActionAnimationActionKeyValidator(ICharacterActionAnimationActionKeyProvider provider)
        {
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        private readonly ICharacterActionAnimationActionKeyProvider _provider;

        public CharacterActionValidationDiagnostic[] Validate(CharacterActionValidationContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            var diagnostics = new List<CharacterActionValidationDiagnostic>();
            CharacterActionConfig[] actions = context.Registry.Actions;
            for (int i = 0; i < actions.Length; i++)
            {
                CharacterActionConfig action = actions[i];
                if (action == null)
                    continue;

                for (int j = 0; j < action.AnimationTrack.Events.Length; j++)
                {
                    AnimationTrackEvent trackEvent = action.AnimationTrack.Events[j];
                    string animationKey = trackEvent.EffectiveAnimationKey;
                    if (string.IsNullOrEmpty(animationKey))
                        continue;
                    if (_provider.ContainsAnimationActionKey(string.Empty, action.ActionId, animationKey))
                        continue;

                    diagnostics.Add(Error(
                        context.Registry.ActionsSourcePath,
                        CharacterActionValidationPath.ActionEventPath(action, CharacterActionTrackKind.Animation, j, trackEvent.StableEventId),
                        CharacterActionDiagnosticCodes.AnimationActionMissing,
                        "Animation key '" + animationKey + "' is not registered."));
                }
            }

            return Sort(diagnostics);
        }
    }

    public sealed class CharacterActionCombatActionIdValidator : ICharacterActionCombatValidator
    {
        public CharacterActionCombatActionIdValidator(ICharacterActionCombatActionIdProvider provider)
        {
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        private readonly ICharacterActionCombatActionIdProvider _provider;

        public CharacterActionValidationDiagnostic[] Validate(CharacterActionValidationContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            var diagnostics = new List<CharacterActionValidationDiagnostic>();
            CharacterActionConfig[] actions = context.Registry.Actions;
            for (int i = 0; i < actions.Length; i++)
            {
                CharacterActionConfig action = actions[i];
                if (action == null)
                    continue;

                CharacterActionResourceDependency[] dependencies = CharacterActionResourceDependencyCollector.Collect(action);
                for (int j = 0; j < dependencies.Length; j++)
                {
                    CharacterActionResourceDependency dependency = dependencies[j];
                    if (dependency.Kind != CharacterActionResourceDependencyKind.CombatAction
                        || dependency.IsMissing
                        || (dependency.NumericId <= 0 && string.IsNullOrEmpty(dependency.StableId)))
                    {
                        continue;
                    }

                    if (_provider.ContainsCombatActionId(string.Empty, action.ActionId, dependency.NumericId))
                        continue;

                    diagnostics.Add(Error(
                        context.Registry.ActionsSourcePath,
                        CharacterActionValidationPath.DependencyPath(dependency),
                        CharacterActionDiagnosticCodes.CombatActionMissing,
                        "Combat action id '" + dependency.StableId + "' is not registered."));
                }
            }

            return Sort(diagnostics);
        }
    }

    public sealed class CharacterActionPresentationResourceValidator : ICharacterActionPresentationResourceValidator
    {
        public CharacterActionPresentationResourceValidator(ICharacterActionPresentationResourceProvider provider)
        {
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        private readonly ICharacterActionPresentationResourceProvider _provider;

        public CharacterActionValidationDiagnostic[] Validate(CharacterActionValidationContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            var diagnostics = new List<CharacterActionValidationDiagnostic>();
            CharacterActionConfig[] actions = context.Registry.Actions;
            for (int i = 0; i < actions.Length; i++)
            {
                CharacterActionConfig action = actions[i];
                if (action == null)
                    continue;

                CharacterActionResourceDependency[] dependencies = CharacterActionResourceDependencyCollector.Collect(action);
                for (int j = 0; j < dependencies.Length; j++)
                {
                    CharacterActionResourceDependency dependency = dependencies[j];
                    if ((dependency.Kind != CharacterActionResourceDependencyKind.AudioCue
                            && dependency.Kind != CharacterActionResourceDependencyKind.VfxResource)
                        || dependency.IsMissing
                        || string.IsNullOrEmpty(dependency.StableId))
                    {
                        continue;
                    }

                    bool found = dependency.NumericId > 0
                        ? _provider is ICharacterActionNumericPresentationResourceProvider numeric
                            && numeric.ContainsNumericPresentationResource(action.ActionId, dependency.Kind, dependency.NumericId)
                        : _provider.ContainsPresentationResource(string.Empty, action.ActionId, dependency.Kind, dependency.StableId);
                    if (found)
                        continue;

                    string code = dependency.Kind == CharacterActionResourceDependencyKind.AudioCue
                        ? CharacterActionDiagnosticCodes.AudioCueMissing
                        : CharacterActionDiagnosticCodes.PresentationResourceMissing;
                    diagnostics.Add(Error(
                        context.Registry.ActionsSourcePath,
                        CharacterActionValidationPath.DependencyPath(dependency),
                        code,
                        "Presentation resource '" + dependency.StableId + "' is not registered."));
                }
            }

            return Sort(diagnostics);
        }
    }

    public sealed class CharacterActionEquipmentTagRequirementValidator : ICharacterActionEquipmentTagValidator
    {
        public CharacterActionEquipmentTagRequirementValidator(ICharacterActionEquipmentTagProvider provider)
        {
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        private readonly ICharacterActionEquipmentTagProvider _provider;

        public CharacterActionValidationDiagnostic[] Validate(CharacterActionValidationContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            var diagnostics = new List<CharacterActionValidationDiagnostic>();
            for (int i = 0; i < context.Registry.Actions.Length; i++)
            {
                CharacterActionConfig action = context.Registry.Actions[i];
                if (action == null)
                    continue;

                for (int j = 0; j < action.Requirements.Length; j++)
                {
                    CharacterActionRequirement requirement = action.Requirements[j];
                    if (requirement == null
                        || (requirement.Kind != CharacterActionRequirementKind.EquipmentTagRequired
                            && requirement.Kind != CharacterActionRequirementKind.EquipmentTagForbidden)
                        || string.IsNullOrEmpty(requirement.StableId))
                    {
                        continue;
                    }

                    if (_provider.ContainsEquipmentTag(string.Empty, requirement.StableId))
                        continue;

                    diagnostics.Add(Error(
                        context.Registry.ActionsSourcePath,
                        "actions[" + action.ActionId.Value + "].requirements[" + j + "].stableId",
                        CharacterActionDiagnosticCodes.EquipmentTagMissing,
                        "Equipment tag '" + requirement.StableId + "' is not registered."));
                }
            }

            for (int i = 0; i < context.Registry.ActionSets.Length; i++)
            {
                CharacterActionSetConfig actionSet = context.Registry.ActionSets[i];
                if (actionSet == null)
                    continue;

                ValidateAbilityTags(context, actionSet, actionSet.AbilityBindings, diagnostics);
            }

            return Sort(diagnostics);
        }

        private void ValidateAbilityTags(
            CharacterActionValidationContext context,
            CharacterActionSetConfig actionSet,
            CharacterAbilityActionBinding[] bindings,
            List<CharacterActionValidationDiagnostic> diagnostics)
        {
            for (int i = 0; i < bindings.Length; i++)
            {
                CharacterAbilityActionBinding binding = bindings[i];
                if (binding == null)
                    continue;

                ValidateTags(context, actionSet, binding.RequiredTags, "requiredTags", i, diagnostics);
                ValidateTags(context, actionSet, binding.ForbiddenTags, "forbiddenTags", i, diagnostics);
            }
        }

        private void ValidateTags(
            CharacterActionValidationContext context,
            CharacterActionSetConfig actionSet,
            string[] tags,
            string field,
            int bindingIndex,
            List<CharacterActionValidationDiagnostic> diagnostics)
        {
            for (int i = 0; i < tags.Length; i++)
            {
                string tag = tags[i];
                if (string.IsNullOrEmpty(tag) || _provider.ContainsEquipmentTag(actionSet.StableId, tag))
                    continue;

                diagnostics.Add(Error(
                    context.Registry.ActionSetsSourcePath,
                    "actionSets[" + actionSet.StableId + "].abilityBindings[" + bindingIndex + "]." + field + "[" + i + "]",
                    CharacterActionDiagnosticCodes.EquipmentTagMissing,
                    "Equipment tag '" + tag + "' is not registered."));
            }
        }
    }

    public sealed class CharacterActionCharacterEquipmentStateValidator : ICharacterActionCharacterEquipmentStateValidator
    {
        public CharacterActionCharacterEquipmentStateValidator(ICharacterActionCharacterEquipmentStateProvider provider)
        {
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        private readonly ICharacterActionCharacterEquipmentStateProvider _provider;

        public CharacterActionValidationDiagnostic[] Validate(CharacterActionValidationContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            var diagnostics = new List<CharacterActionValidationDiagnostic>();
            CharacterActionSetConfig[] actionSets = context.Registry.ActionSets;
            for (int i = 0; i < actionSets.Length; i++)
            {
                CharacterActionSetConfig actionSet = actionSets[i];
                if (actionSet == null)
                    continue;

                if (!string.IsNullOrEmpty(actionSet.CharacterStableId)
                    && !_provider.ContainsCharacterStableId(actionSet.CharacterStableId))
                {
                    diagnostics.Add(Error(
                        context.Registry.ActionSetsSourcePath,
                        "actionSets[" + actionSet.StableId + "].characterStableId",
                        CharacterActionDiagnosticCodes.CharacterEquipmentStateMissing,
                        "Character stable id '" + actionSet.CharacterStableId + "' is not registered."));
                }

                if (!string.IsNullOrEmpty(actionSet.EquipmentStateStableId)
                    && !_provider.ContainsEquipmentStateStableId(actionSet.CharacterStableId, actionSet.EquipmentStateStableId))
                {
                    diagnostics.Add(Error(
                        context.Registry.ActionSetsSourcePath,
                        "actionSets[" + actionSet.StableId + "].equipmentStateStableId",
                        CharacterActionDiagnosticCodes.CharacterEquipmentStateMissing,
                        "Equipment state stable id '" + actionSet.EquipmentStateStableId + "' is not registered."));
                }
            }

            return Sort(diagnostics);
        }
    }

    public sealed class CharacterActionValidationReport
    {
        public CharacterActionValidationReport(CharacterActionValidationDiagnostic[] diagnostics)
        {
            Diagnostics = diagnostics == null
                ? Array.Empty<CharacterActionValidationDiagnostic>()
                : (CharacterActionValidationDiagnostic[])diagnostics.Clone();
            Array.Sort(Diagnostics, CharacterActionValidationDiagnostic.Compare);
        }

        public CharacterActionValidationDiagnostic[] Diagnostics { get; }

        public bool HasErrors
        {
            get
            {
                for (int i = 0; i < Diagnostics.Length; i++)
                {
                    if (Diagnostics[i].Severity == CharacterActionDiagnosticSeverity.Error)
                        return true;
                }

                return false;
            }
        }

        public bool Success => !HasErrors;

        public static CharacterActionValidationReport Validate(
            CharacterActionConfigRegistry registry,
            CharacterActionValidationOptions options = null)
        {
            if (registry == null)
                throw new ArgumentNullException(nameof(registry));

            options = options ?? CharacterActionValidationOptions.Default;
            var diagnostics = new List<CharacterActionValidationDiagnostic>();
            diagnostics.AddRange(registry.Diagnostics);
            ValidateActionSets(registry, diagnostics);
            ValidateActions(registry, options, diagnostics);
            ValidateCrossModule(new CharacterActionValidationContext(registry, options), diagnostics);
            return new CharacterActionValidationReport(diagnostics.ToArray());
        }

        private static void ValidateActionSets(
            CharacterActionConfigRegistry registry,
            List<CharacterActionValidationDiagnostic> diagnostics)
        {
            CharacterActionSetConfig[] actionSets = registry.ActionSets;
            for (int i = 0; i < actionSets.Length; i++)
            {
                CharacterActionSetConfig actionSet = actionSets[i];
                if (actionSet == null)
                    continue;

                string setPath = "actionSets[" + actionSet.StableId + "]";
                ValidateActionReference(
                    registry,
                    diagnostics,
                    registry.ActionSetsSourcePath,
                    setPath + ".defaultActionId",
                    actionSet.DefaultActionId,
                    "Default action");

                if (!string.IsNullOrEmpty(actionSet.ReactionProfileId)
                    && !registry.TryGetReactionProfile(actionSet.ReactionProfileId, out _))
                {
                    diagnostics.Add(Error(
                        registry.ActionSetsSourcePath,
                        setPath + ".reactionProfileId",
                        CharacterActionDiagnosticCodes.MissingReactionProfile,
                        "Reaction profile '" + actionSet.ReactionProfileId + "' is missing."));
                }

                if (!string.IsNullOrEmpty(actionSet.LandingProfileId)
                    && !registry.TryGetLandingProfile(actionSet.LandingProfileId, out _))
                {
                    diagnostics.Add(Error(
                        registry.ActionSetsSourcePath,
                        setPath + ".landingProfileId",
                        CharacterActionDiagnosticCodes.MissingLandingProfile,
                        "Landing profile '" + actionSet.LandingProfileId + "' is missing."));
                }

                for (int j = 0; j < actionSet.CommandBindings.Length; j++)
                {
                    CharacterActionBinding binding = actionSet.CommandBindings[j];
                    if (binding == null || (binding.IntentId <= 0) || !binding.ActionId.IsValid)
                    {
                        diagnostics.Add(Error(
                            registry.ActionSetsSourcePath,
                            setPath + ".commandBindings[" + j + "]",
                            CharacterActionDiagnosticCodes.MissingActionBinding,
                            "Command binding is missing an intent id or action id."));
                        continue;
                    }

                    ValidateActionReference(
                        registry,
                        diagnostics,
                        registry.ActionSetsSourcePath,
                        setPath + ".commandBindings[" + j + "].actionId",
                        binding.ActionId,
                        "Command binding action");
                }

                for (int j = 0; j < actionSet.AbilityBindings.Length; j++)
                {
                    CharacterAbilityActionBinding binding = actionSet.AbilityBindings[j];
                    if (binding == null || binding.AbilityId <= 0 || !binding.ActionId.IsValid)
                    {
                        diagnostics.Add(Error(
                            registry.ActionSetsSourcePath,
                            setPath + ".abilityBindings[" + j + "]",
                            CharacterActionDiagnosticCodes.MissingAbilityBinding,
                            "Ability binding is missing an ability id or action id."));
                        continue;
                    }

                    ValidateActionReference(
                        registry,
                        diagnostics,
                        registry.ActionSetsSourcePath,
                        setPath + ".abilityBindings[" + j + "].actionId",
                        binding.ActionId,
                        "Ability binding action");
                }

                for (int j = 0; j < actionSet.ReactionBindings.Length; j++)
                {
                    CharacterReactionBinding binding = actionSet.ReactionBindings[j];
                    if (binding == null || !binding.DefaultActionId.IsValid)
                    {
                        diagnostics.Add(Error(
                            registry.ActionSetsSourcePath,
                            setPath + ".reactionBindings[" + j + "]",
                            CharacterActionDiagnosticCodes.ReactionRuleNoTarget,
                            "Reaction binding is missing a default action id."));
                        continue;
                    }

                    ValidateActionReference(
                        registry,
                        diagnostics,
                        registry.ActionSetsSourcePath,
                        setPath + ".reactionBindings[" + j + "].defaultActionId",
                        binding.DefaultActionId,
                        "Reaction binding action");
                }
            }

            ValidateReactionProfiles(registry, diagnostics);
            ValidateLandingProfiles(registry, diagnostics);
        }

        private static void ValidateLandingProfiles(
            CharacterActionConfigRegistry registry,
            List<CharacterActionValidationDiagnostic> diagnostics)
        {
            CharacterLandingProfile[] profiles = registry.LandingProfiles;
            for (int i = 0; i < profiles.Length; i++)
            {
                CharacterLandingProfile profile = profiles[i];
                if (profile == null)
                    continue;

                ValidateLandingActionReference(registry, diagnostics, profile, profile.LightLandingActionId, "lightLandingActionId");
                ValidateLandingActionReference(registry, diagnostics, profile, profile.HeavyLandingActionId, "heavyLandingActionId");
            }
        }

        private static void ValidateLandingActionReference(
            CharacterActionConfigRegistry registry,
            List<CharacterActionValidationDiagnostic> diagnostics,
            CharacterLandingProfile profile,
            CharacterActionId actionId,
            string fieldName)
        {
            if (!actionId.IsValid || registry.TryGetAction(actionId, out _))
                return;

            diagnostics.Add(Error(
                registry.LandingProfilesSourcePath,
                "landingProfiles[" + profile.StableId + "]." + fieldName,
                CharacterActionDiagnosticCodes.MissingActionConfig,
                "Landing action '" + actionId + "' is missing."));
        }

        private static void ValidateReactionProfiles(
            CharacterActionConfigRegistry registry,
            List<CharacterActionValidationDiagnostic> diagnostics)
        {
            CharacterReactionProfile[] profiles = registry.ReactionProfiles;
            for (int i = 0; i < profiles.Length; i++)
            {
                CharacterReactionProfile profile = profiles[i];
                if (profile == null)
                    continue;

                CharacterActionDiagnostic[] profileDiagnostics = CharacterActionValidation.ValidateReactionProfile(
                    profile,
                    registry.Actions,
                    CharacterReactionContextCompleteness.Full);
                for (int diagnosticIndex = 0; diagnosticIndex < profileDiagnostics.Length; diagnosticIndex++)
                {
                    CharacterActionDiagnostic diagnostic = profileDiagnostics[diagnosticIndex];
                    diagnostics.Add(new CharacterActionValidationDiagnostic(
                        registry.ReactionProfilesSourcePath,
                        "reactionProfiles[" + profile.StableId + "]",
                        diagnostic.Code,
                        diagnostic.Severity,
                        diagnostic.Message));
                }
            }
        }

        private static void ValidateActions(
            CharacterActionConfigRegistry registry,
            CharacterActionValidationOptions options,
            List<CharacterActionValidationDiagnostic> diagnostics)
        {
            CharacterActionConfig[] actions = registry.Actions;
            for (int i = 0; i < actions.Length; i++)
            {
                CharacterActionConfig action = actions[i];
                if (action == null)
                    continue;

                string actionPath = "actions[" + action.ActionId.Value + "]";
                if (action.TimelineAuthority == CharacterActionTimelineAuthority.CharacterAuthored
                    && !action.DurationFrames.HasValue)
                {
                    diagnostics.Add(Error(
                        registry.ActionsSourcePath,
                        actionPath + ".durationFrames",
                        CharacterActionDiagnosticCodes.ActionDurationMissing,
                        "CharacterAuthored action requires duration frames."));
                }

                ValidatePhases(registry, action, diagnostics);
                ValidateCancelWindows(registry, action, diagnostics);
                ValidateInterruptTargets(registry, action, diagnostics);
                ValidateMissingDependencies(registry, action, options, diagnostics);
                ValidateTrackEventFrames(registry, action, diagnostics);
                ValidateCombatAnchoredActionId(registry, action, diagnostics);
                ValidateLegacyTraceProfileIds(registry, action, diagnostics);
            }
        }

        private static void ValidatePhases(
            CharacterActionConfigRegistry registry,
            CharacterActionConfig action,
            List<CharacterActionValidationDiagnostic> diagnostics)
        {
            CharacterActionValidationIssue[] issues = CharacterActionPhaseValidator.Validate(
                action.TimelineAuthority,
                action.Phases,
                combatTimeline: null,
                durationFrames: action.DurationFrames);
            for (int i = 0; i < issues.Length; i++)
            {
                CharacterActionValidationIssue issue = issues[i];
                string path = issue.PhaseIndex >= 0
                    ? "actions[" + action.ActionId.Value + "].phases[" + issue.PhaseIndex + "]"
                    : "actions[" + action.ActionId.Value + "].phases";
                diagnostics.Add(Error(registry.ActionsSourcePath, path, CharacterActionDiagnosticCodeText.Get(issue.Code), issue.Message));
            }
        }

        private static void ValidateCancelWindows(
            CharacterActionConfigRegistry registry,
            CharacterActionConfig action,
            List<CharacterActionValidationDiagnostic> diagnostics)
        {
            int? durationFrames = ResolveValidationDuration(action);
            for (int i = 0; i < action.CancelRules.Length; i++)
            {
                CharacterCancelRule rule = action.CancelRules[i];
                string path = "actions[" + action.ActionId.Value + "].cancelRules[" + i + "]";
                if (durationFrames.HasValue && rule.EndFrame >= durationFrames.Value)
                {
                    diagnostics.Add(Error(
                        registry.ActionsSourcePath,
                        path,
                        CharacterActionDiagnosticCodes.InvalidCancelWindow,
                        "Cancel window " + rule.StartFrame + "-" + rule.EndFrame
                        + " extends beyond action duration " + durationFrames.Value + "."));
                }

                if (!CharacterActionValidation.IsCancelWindowCoveredByCancelablePhases(
                    action.Phases,
                    rule.StartFrame,
                    rule.EndFrame,
                    allowStartup: rule.CancelInputMask != 0))
                {
                    diagnostics.Add(Error(
                        registry.ActionsSourcePath,
                        path,
                        CharacterActionDiagnosticCodes.InvalidCancelWindow,
                        "Cancel window " + rule.StartFrame + "-" + rule.EndFrame
                        + " must be fully covered by cancelable phases."));
                }
            }
        }

        private static int? ResolveValidationDuration(CharacterActionConfig action)
        {
            return action.DurationFrames;
        }

        private static void ValidateInterruptTargets(
            CharacterActionConfigRegistry registry,
            CharacterActionConfig action,
            List<CharacterActionValidationDiagnostic> diagnostics)
        {
            for (int i = 0; i < action.InterruptRules.Length; i++)
            {
                CharacterInterruptRule rule = action.InterruptRules[i];
                if (rule.TargetActionId.IsValid
                    && !registry.TryGetAction(rule.TargetActionId, out _))
                {
                    diagnostics.Add(Error(
                        registry.ActionsSourcePath,
                        "actions[" + action.ActionId.Value + "].interruptRules[" + i + "].targetActionId",
                        CharacterActionDiagnosticCodes.InterruptTargetMissing,
                        "Interrupt target action id " + rule.TargetActionId + " is missing."));
                }
            }
        }

        private static void ValidateMissingDependencies(
            CharacterActionConfigRegistry registry,
            CharacterActionConfig action,
            CharacterActionValidationOptions options,
            List<CharacterActionValidationDiagnostic> diagnostics)
        {
            CharacterActionResourceDependency[] dependencies = CharacterActionResourceDependencyCollector.Collect(action);
            for (int i = 0; i < dependencies.Length; i++)
            {
                CharacterActionResourceDependency dependency = dependencies[i];
                if (!dependency.IsMissing)
                    continue;

                string code;
                CharacterActionDiagnosticSeverity severity = CharacterActionDiagnosticSeverity.Error;
                string message;
                switch (dependency.Kind)
                {
                    case CharacterActionResourceDependencyKind.CombatAction:
                        code = CharacterActionDiagnosticCodes.CombatActionMissing;
                        message = "Combat track start event requires a combat action id.";
                        break;
                    case CharacterActionResourceDependencyKind.GameplayRequest:
                        code = CharacterActionDiagnosticCodes.ResourceCostWithoutResourceId;
                        message = "Gameplay request track event requires a request id.";
                        break;
                    case CharacterActionResourceDependencyKind.AnimationAction:
                        if (options.MissingAnimationActionKeyPolicy == CharacterActionMissingAnimationActionKeyPolicy.Ignore)
                            continue;
                        severity = options.MissingAnimationActionKeyPolicy == CharacterActionMissingAnimationActionKeyPolicy.Warning
                            ? CharacterActionDiagnosticSeverity.Warning
                            : CharacterActionDiagnosticSeverity.Error;
                        code = CharacterActionDiagnosticCodes.AnimationActionMissing;
                        message = "Animation track event requires an animation action key.";
                        break;
                    case CharacterActionResourceDependencyKind.AudioCue:
                        code = CharacterActionDiagnosticCodes.AudioCueMissing;
                        message = "Audio presentation event requires a cue id.";
                        break;
                    case CharacterActionResourceDependencyKind.VfxResource:
                        code = CharacterActionDiagnosticCodes.PresentationResourceMissing;
                        message = "Visual presentation event requires a resource key.";
                        break;
                    case CharacterActionResourceDependencyKind.TraceProfile:
                        code = CharacterActionDiagnosticCodes.PresentationResourceMissing;
                        message = "Combat trace event requires a trace profile id.";
                        break;
                    default:
                        continue;
                }

                diagnostics.Add(new CharacterActionValidationDiagnostic(
                    registry.ActionsSourcePath,
                    CharacterActionValidationPath.DependencyPath(dependency),
                    code,
                    severity,
                    message));
            }
        }

        private static void ValidateTrackEventFrames(
            CharacterActionConfigRegistry registry,
            CharacterActionConfig action,
            List<CharacterActionValidationDiagnostic> diagnostics)
        {
            if (!action.DurationFrames.HasValue)
                return;

            int durationFrames = action.DurationFrames.Value;
            for (int i = 0; i < action.MotionTrack.Events.Length; i++)
                ValidateFrame(registry, action, CharacterActionTrackKind.Motion, i, action.MotionTrack.Events[i].Frame, action.MotionTrack.Events[i].StableEventId, durationFrames, diagnostics);
            for (int i = 0; i < action.CombatTrack.Events.Length; i++)
                ValidateFrame(registry, action, CharacterActionTrackKind.Combat, i, action.CombatTrack.Events[i].Frame, action.CombatTrack.Events[i].StableEventId, durationFrames, diagnostics);
            for (int i = 0; i < action.GameplayTrack.Events.Length; i++)
                ValidateFrame(registry, action, CharacterActionTrackKind.Gameplay, i, action.GameplayTrack.Events[i].Frame, action.GameplayTrack.Events[i].StableEventId, durationFrames, diagnostics);
            for (int i = 0; i < action.AnimationTrack.Events.Length; i++)
            {
                AnimationTrackEvent trackEvent = action.AnimationTrack.Events[i];
                ValidateFrame(registry, action, CharacterActionTrackKind.Animation, i, trackEvent.Frame, trackEvent.StableEventId, durationFrames, diagnostics);
                ValidateAnimationEndFrame(registry, action, i, trackEvent, durationFrames, diagnostics);
            }
            for (int i = 0; i < action.PresentationTrack.Events.Length; i++)
            {
                PresentationTrackEvent trackEvent = action.PresentationTrack.Events[i];
                ValidateFrame(registry, action, CharacterActionTrackKind.Presentation, i, trackEvent.Frame, trackEvent.StableEventId, durationFrames, diagnostics);
                ValidatePresentationEndFrame(registry, action, i, trackEvent, durationFrames, diagnostics);
            }
            for (int i = 0; i < action.DebugTrack.Events.Length; i++)
                ValidateFrame(registry, action, CharacterActionTrackKind.Debug, i, action.DebugTrack.Events[i].Frame, action.DebugTrack.Events[i].StableEventId, durationFrames, diagnostics);
        }

        private static void ValidateFrame(
            CharacterActionConfigRegistry registry,
            CharacterActionConfig action,
            CharacterActionTrackKind trackKind,
            int eventIndex,
            int frame,
            int stableEventId,
            int durationFrames,
            List<CharacterActionValidationDiagnostic> diagnostics)
        {
            if (frame < durationFrames)
                return;

            diagnostics.Add(Error(
                registry.ActionsSourcePath,
                CharacterActionValidationPath.ActionEventPath(action, trackKind, eventIndex, stableEventId),
                CharacterActionDiagnosticCodes.TrackEventOutsideDuration,
                "Track event frame must be within action duration."));
        }

        private static void ValidateAnimationEndFrame(
            CharacterActionConfigRegistry registry,
            CharacterActionConfig action,
            int eventIndex,
            AnimationTrackEvent trackEvent,
            int durationFrames,
            List<CharacterActionValidationDiagnostic> diagnostics)
        {
            if (trackEvent.EndFrame < trackEvent.Frame)
            {
                diagnostics.Add(Error(
                    registry.ActionsSourcePath,
                    CharacterActionValidationPath.ActionEventPath(action, CharacterActionTrackKind.Animation, eventIndex, trackEvent.StableEventId),
                    CharacterActionDiagnosticCodes.TrackEventOutsideDuration,
                    "Animation track event endFrame must be greater than or equal to frame."));
            }

            if (trackEvent.EndFrame >= durationFrames)
            {
                diagnostics.Add(Error(
                    registry.ActionsSourcePath,
                    CharacterActionValidationPath.ActionEventPath(action, CharacterActionTrackKind.Animation, eventIndex, trackEvent.StableEventId),
                    CharacterActionDiagnosticCodes.TrackEventOutsideDuration,
                    "Animation track event endFrame must be within action duration."));
            }
        }

        private static void ValidatePresentationEndFrame(
            CharacterActionConfigRegistry registry,
            CharacterActionConfig action,
            int eventIndex,
            PresentationTrackEvent trackEvent,
            int durationFrames,
            List<CharacterActionValidationDiagnostic> diagnostics)
        {
            if (trackEvent.EndFrame >= durationFrames
                && trackEvent.Kind != CharacterActionTrackEventKind.SpawnVisualCue)
            {
                diagnostics.Add(Error(
                    registry.ActionsSourcePath,
                    CharacterActionValidationPath.ActionEventPath(action, CharacterActionTrackKind.Presentation, eventIndex, trackEvent.StableEventId),
                    CharacterActionDiagnosticCodes.TrackEventOutsideDuration,
                    "Presentation track event endFrame must be within action duration."));
            }
        }

        private static void ValidateCombatAnchoredActionId(
            CharacterActionConfigRegistry registry,
            CharacterActionConfig action,
            List<CharacterActionValidationDiagnostic> diagnostics)
        {
            if (action.TimelineAuthority != CharacterActionTimelineAuthority.CombatAnchored)
                return;
            if (HasCombatActionId(action))
                return;

            diagnostics.Add(Error(
                registry.ActionsSourcePath,
                "actions[" + action.ActionId.Value + "].combatTrack.combatActionId",
                CharacterActionDiagnosticCodes.CombatActionMissing,
                "CombatAnchored action requires a combat action id."));
        }

        private static void ValidateLegacyTraceProfileIds(
            CharacterActionConfigRegistry registry,
            CharacterActionConfig action,
            List<CharacterActionValidationDiagnostic> diagnostics)
        {
            for (int i = 0; i < action.CombatTrack.Events.Length; i++)
            {
                CombatTrackEvent trackEvent = action.CombatTrack.Events[i];
                if (trackEvent.Kind == CharacterActionTrackEventKind.HitTraceWindow &&
                    (trackEvent.TraceProfileId > 0))
                {
                    diagnostics.Add(Warning(
                        registry.ActionsSourcePath,
                        CharacterActionValidationPath.ActionEventPath(action, CharacterActionTrackKind.Combat, i, trackEvent.StableEventId) + ".traceProfileId",
                        CharacterActionDiagnosticCodes.LegacyTraceProfileId,
                        "Combat hit trace event uses legacy traceProfileId '" + trackEvent.TraceProfileId + "'. Prefer equipmentSlotId so the current equipped weapon selects trace geometry."));
                }
            }
        }

        private static bool HasCombatActionId(CharacterActionConfig action)
        {
            if ((action.CombatTrack.CombatActionId > 0))
                return true;

            for (int i = 0; i < action.CombatTrack.Events.Length; i++)
            {
                CombatTrackEvent trackEvent = action.CombatTrack.Events[i];
                if (trackEvent.Kind == CharacterActionTrackEventKind.StartCombatAction
                    && (trackEvent.CombatActionId > 0))
                {
                    return true;
                }
            }

            return false;
        }

        private static void ValidateCrossModule(
            CharacterActionValidationContext context,
            List<CharacterActionValidationDiagnostic> diagnostics)
        {
            ICharacterActionCrossModuleValidator[] validators = context.Options.CrossModuleValidators;
            for (int i = 0; i < validators.Length; i++)
            {
                ICharacterActionCrossModuleValidator validator = validators[i];
                if (validator == null)
                    continue;

                CharacterActionValidationDiagnostic[] externalDiagnostics = validator.Validate(context) ?? Array.Empty<CharacterActionValidationDiagnostic>();
                diagnostics.AddRange(externalDiagnostics);
            }
        }

        private static void ValidateActionReference(
            CharacterActionConfigRegistry registry,
            List<CharacterActionValidationDiagnostic> diagnostics,
            string sourcePath,
            string stablePath,
            CharacterActionId actionId,
            string label)
        {
            if (!actionId.IsValid)
                return;
            if (registry.TryGetAction(actionId, out _))
                return;

            diagnostics.Add(Error(
                sourcePath,
                stablePath,
                CharacterActionDiagnosticCodes.MissingActionConfig,
                label + " '" + actionId + "' is missing."));
        }

    }

    internal static class CharacterActionValidationPath
    {
        public static string ActionEventPath(
            CharacterActionConfig action,
            CharacterActionTrackKind trackKind,
            int eventIndex,
            int stableEventId)
        {
            string eventKey = (stableEventId <= 0)
                ? eventIndex.ToString()
                : stableEventId.ToString(System.Globalization.CultureInfo.InvariantCulture);
            return "actions[" + action.ActionId.Value + "]." + TrackPath(trackKind) + ".events[" + eventKey + "]";
        }

        public static string DependencyPath(CharacterActionResourceDependency dependency)
        {
            if (dependency.TrackKind == CharacterActionTrackKind.Combat
                && dependency.EventKind == CharacterActionTrackEventKind.None)
            {
                return "actions[" + dependency.ActionId + "].combatTrack.combatActionId";
            }

            string eventKey = (dependency.StableEventId <= 0)
                ? dependency.Frame.ToString()
                : dependency.StableEventId.ToString(System.Globalization.CultureInfo.InvariantCulture);
            return "actions[" + dependency.ActionId + "]." + TrackPath(dependency.TrackKind) + ".events[" + eventKey + "]";
        }

        private static string TrackPath(CharacterActionTrackKind trackKind)
        {
            switch (trackKind)
            {
                case CharacterActionTrackKind.Motion:
                    return "motionTrack";
                case CharacterActionTrackKind.Combat:
                    return "combatTrack";
                case CharacterActionTrackKind.Gameplay:
                    return "gameplayTrack";
                case CharacterActionTrackKind.Animation:
                    return "animationTrack";
                case CharacterActionTrackKind.Presentation:
                    return "presentationTrack";
                case CharacterActionTrackKind.Debug:
                    return "debugTrack";
                default:
                    return "track";
            }
        }
    }
}
