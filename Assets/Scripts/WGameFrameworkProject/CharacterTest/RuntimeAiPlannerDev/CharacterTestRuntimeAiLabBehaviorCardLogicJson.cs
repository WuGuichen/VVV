using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace MxFramework.Game.CharacterTest
{
    internal static class CharacterTestRuntimeAiLabBehaviorCardLogicJson
    {
        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore,
            ObjectCreationHandling = ObjectCreationHandling.Auto
        };

        /// <remarks>LowFreqAlloc: draft persistence only.</remarks>
        public static string Serialize(CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload)
        {
            if (payload == null)
                throw new ArgumentNullException(nameof(payload));
            // Serialization is an authority boundary, not an in-place editor mutation. Work on a
            // clone so legacy authentication/migration and v8 canonicalization cannot silently replace
            // the caller's currently selected compatibility objects while an edit gesture is open.
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload serialized = payload.Clone();
            MigrateLegacyStages(serialized);
            if (!CharacterTestRuntimeAiLabBehaviorRecipeV6Migration.HasV6Authority(serialized))
            {
                CharacterTestRuntimeAiLabBehaviorRecipeV5Migration.SynchronizeForWrite(serialized);
                CharacterTestRuntimeAiLabBehaviorRecipeV6Migration.SynchronizeForWrite(serialized);
            }
            CharacterTestRuntimeAiLabBehaviorRecipeV11Migration.SynchronizeForWrite(serialized);
            return JsonConvert.SerializeObject(serialized, Settings);
        }

        /// <remarks>LowFreqAlloc: draft persistence only.</remarks>
        public static CharacterTestRuntimeAiLabBehaviorCardLogicPayload Deserialize(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new ArgumentException("Behavior card logic JSON is required.", nameof(json));
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload =
                JsonConvert.DeserializeObject<CharacterTestRuntimeAiLabBehaviorCardLogicPayload>(json, Settings);
            if (payload == null)
                throw new JsonSerializationException("Behavior card logic payload is null.");
            if (payload.BehaviorRecipeVersion < 0
                || payload.BehaviorRecipeVersion >
                    CharacterTestRuntimeAiLabBehaviorCardLogicPayload.CurrentBehaviorRecipeVersion)
            {
                throw new JsonSerializationException(
                    "Behavior card logic recipe version is unsupported; maximum supported version is v"
                    + CharacterTestRuntimeAiLabBehaviorCardLogicPayload.CurrentBehaviorRecipeVersion + ".");
            }
            MigrateLegacyStages(payload);
            if (!CharacterTestRuntimeAiLabBehaviorRecipeV6Migration.HasV6Authority(payload))
            {
                CharacterTestRuntimeAiLabBehaviorRecipeV5Migration.SynchronizeForRead(payload);
                CharacterTestRuntimeAiLabBehaviorRecipeV6Migration.SynchronizeForRead(payload);
            }
            CharacterTestRuntimeAiLabBehaviorRecipeV11Migration.SynchronizeForRead(payload);
            return payload;
        }

        internal static void MigrateLegacyStages(CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload)
        {
            for (int i = 0; payload != null && i < payload.Stages.Count; i++)
            {
                CharacterTestRuntimeAiLabLogicStage stage = payload.Stages[i];
                if (stage == null || stage.Kind != CharacterTestRuntimeAiLabCardStepKind.LegacyMigrationOnly)
                    continue;

                switch (stage.LegacyActionKind)
                {
                    case CharacterTestRuntimeAiPlannerActionKind.PrimaryAttack:
                        stage.Kind = CharacterTestRuntimeAiLabCardStepKind.CharacterAction;
                        stage.CharacterActionId = CharacterTestCharacterActionConfigSource.LightAttackActionId;
                        stage.LegacyActionKind = default;
                        break;
                    case CharacterTestRuntimeAiPlannerActionKind.Dodge:
                        stage.Kind = CharacterTestRuntimeAiLabCardStepKind.CharacterAction;
                        stage.CharacterActionId = CharacterTestCharacterActionConfigSource.DefaultEvadeActionId;
                        stage.PreviewDurationFrames = 1;
                        stage.PlannedDurationFrames = 1;
                        stage.DurationMode = CharacterTestRuntimeAiLabLogicDurationMode.Fixed;
                        stage.LegacyActionKind = default;
                        break;
                    case CharacterTestRuntimeAiPlannerActionKind.MoveToTarget:
                        stage.Kind = CharacterTestRuntimeAiLabCardStepKind.MoveToTarget;
                        stage.LegacyActionKind = default;
                        break;
                    case CharacterTestRuntimeAiPlannerActionKind.Strafe:
                    case CharacterTestRuntimeAiPlannerActionKind.Retreat:
                        stage.Kind = CharacterTestRuntimeAiLabCardStepKind.MoveToTarget;
                        break;
                    case CharacterTestRuntimeAiPlannerActionKind.Wait:
                        stage.Kind = CharacterTestRuntimeAiLabCardStepKind.Wait;
                        stage.WaitFrames = Math.Max(1, stage.WaitFrames > 0
                            ? stage.WaitFrames
                            : stage.PreviewDurationFrames);
                        stage.LegacyActionKind = default;
                        break;
                    case CharacterTestRuntimeAiPlannerActionKind.CastAbilitySlot:
                        stage.Kind = CharacterTestRuntimeAiLabCardStepKind.UseAbility;
                        stage.LegacyActionKind = default;
                        break;
                }
            }

            MigrateV3MovementStrategies(payload);

            // Logic v2 serialized the owning stage duration into StageEnd detectors even though
            // the evaluator treats those windows as open until the stage exits. Logic v3 makes
            // that dormant value canonical zero so equivalent cards retain one semantic identity.
            for (int i = 0; payload != null && i < payload.Detectors.Count; i++)
            {
                CharacterTestRuntimeAiLabLogicDetector detector = payload.Detectors[i];
                if (detector != null
                    && detector.WindowEndMode == CharacterTestRuntimeAiLabLogicDetectorWindowEndMode.StageEnd)
                {
                    detector.ActivationDurationFrames = 0;
                }
            }
        }

        internal static void MigrateV3MovementStrategies(
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload)
        {
            bool avoidHazard = payload?.Intent?.IndexOf(
                CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationApproachAndAttack,
                StringComparison.Ordinal) >= 0;
            for (int i = 0; payload != null && i < payload.Stages.Count; i++)
            {
                CharacterTestRuntimeAiLabLogicStage stage = payload.Stages[i];
                if (stage == null || stage.Kind != CharacterTestRuntimeAiLabCardStepKind.MoveToTarget)
                    continue;
                switch (stage.LegacyActionKind)
                {
                    case CharacterTestRuntimeAiPlannerActionKind.Strafe:
                        stage.MovementStrategy = CharacterTestRuntimeAiLabMovementStrategy.StrafeWhenBlocked;
                        break;
                    case CharacterTestRuntimeAiPlannerActionKind.Retreat:
                        stage.MovementStrategy = avoidHazard
                            ? CharacterTestRuntimeAiLabMovementStrategy.AvoidHazard
                            : CharacterTestRuntimeAiLabMovementStrategy.RetreatWhenClose;
                        break;
                    case CharacterTestRuntimeAiPlannerActionKind.MoveToTarget:
                        stage.MovementStrategy = CharacterTestRuntimeAiLabMovementStrategy.ApproachTarget;
                        break;
                }
                stage.LegacyActionKind = default;
            }
        }
    }
}
