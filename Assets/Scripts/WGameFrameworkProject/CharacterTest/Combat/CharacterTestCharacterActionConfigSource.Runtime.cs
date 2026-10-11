using System;
using MxFramework.CharacterAction;

namespace MxFramework.Game.CharacterTest
{
    public static partial class CharacterTestCharacterActionConfigSource
    {
        private static CharacterTestCharacterActionCatalog BuildCatalog(
            CharacterTestCharacterActionConfigSourcePayload payload)
        {
            CharacterTestAreaConfinementTuning areaConfinementTuning =
                CharacterTestAreaConfinementTuningSource.LoadDefault();
            string actionsJson = CharacterTestAreaConfinementActionTuningApplier.Apply(
                payload.ActionsJson,
                areaConfinementTuning);
            CharacterActionJsonImportResult import = CharacterActionJsonImporter.Import(
                payload.ActionSetsJson,
                actionsJson,
                payload.ReactionProfilesJson,
                payload.LandingProfilesJson,
                payload.ActionSetsSourcePath,
                payload.ActionsSourcePath,
                payload.ReactionProfilesSourcePath,
                payload.LandingProfilesSourcePath);
            var registry = new CharacterActionConfigRegistry(
                import.ActionSets,
                import.Actions,
                payload.ActionSetsSourcePath,
                payload.ActionsSourcePath,
                import.ReactionProfiles,
                payload.ReactionProfilesSourcePath,
                import.LandingProfiles,
                payload.LandingProfilesSourcePath);
            var validation = CharacterActionValidationReport.Validate(
                registry,
                new CharacterActionValidationOptions(
                    CharacterActionMissingAnimationActionKeyPolicy.Ignore));

            CharacterActionSetConfig actionSet = ResolveCompatibilityActionSet(registry);
            return new CharacterTestCharacterActionCatalog(
                import,
                registry,
                validation,
                actionSet,
                payload.SourceKind,
                payload.SourceDescription,
                payload.ActionSetsSourcePath,
                payload.ActionsSourcePath,
                payload.Diagnostics);
        }

        private static CharacterTestCharacterActionRuntime BuildRuntime(
            CharacterTestCharacterActionConfigSourcePayload payload)
        {
            return new CharacterTestCharacterActionRuntime(BuildCatalog(payload));
        }


        private static CharacterActionSetConfig ResolveCompatibilityActionSet(
            CharacterActionConfigRegistry registry)
        {
            if (registry == null)
                return null;

            if (registry.TryGetActionSet(ActionSetStableId, out CharacterActionSetConfig compatibility))
                return compatibility;

            return registry.ActionSets.Length == 1 ? registry.ActionSets[0] : null;
        }

    }
}
