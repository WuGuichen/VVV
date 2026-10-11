using System;
using System.Collections.Generic;

namespace MxFramework.Game.CharacterTest.Editor
{
    internal readonly struct CharacterTestRuntimeAiPlannerTemplateDescriptor
    {
        public CharacterTestRuntimeAiPlannerTemplateDescriptor(
            string templateId,
            string displayName,
            string projectAssetPath)
        {
            TemplateId = templateId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            ProjectAssetPath = projectAssetPath ?? string.Empty;
        }

        public string TemplateId { get; }
        public string DisplayName { get; }
        public string ProjectAssetPath { get; }
    }

    internal static class CharacterTestRuntimeAiPlannerTemplateLibrary
    {
        public const string ProfileFolderProjectPath =
            "Assets/StreamingAssets/MxFramework/CharacterTest/runtime_ai_planner";

        public const string DefaultMeleeTemplateId = "default-melee";
        public const string PerceptionAwareMeleeTemplateId = "perception-melee";
        public const string RangedKiterTemplateId = "ranged-kiter";
        public const string HazardSkirmisherTemplateId = "hazard-skirmisher";
        public const string SkillCasterTemplateId = "skill-caster";
        public const string AggressiveBerserkerTemplateId = "aggressive-berserker";
        public const string DefensiveGuardianTemplateId = "defensive-guardian";
        public const string AmbusherTemplateId = "ambusher";
        public const string ControllerZonerTemplateId = "controller-zoner";

        public const string PerceptionAwareMeleeProfileId =
            "character-test.runtime-ai-planner.perception-melee";
        public const string RangedKiterProfileId =
            "character-test.runtime-ai-planner.ranged-kiter";
        public const string HazardSkirmisherProfileId =
            "character-test.runtime-ai-planner.hazard-skirmisher";
        public const string SkillCasterProfileId =
            "character-test.runtime-ai-planner.skill-caster";
        public const string AggressiveBerserkerProfileId =
            "character-test.runtime-ai-planner.aggressive-berserker";
        public const string DefensiveGuardianProfileId =
            "character-test.runtime-ai-planner.defensive-guardian";
        public const string AmbusherProfileId =
            "character-test.runtime-ai-planner.ambusher";
        public const string ControllerZonerProfileId =
            "character-test.runtime-ai-planner.controller-zoner";

        public const string PerceptionAwareMeleeProjectAssetPath =
            ProfileFolderProjectPath + "/perception_melee.json";
        public const string RangedKiterProjectAssetPath =
            ProfileFolderProjectPath + "/ranged_kiter.json";
        public const string HazardSkirmisherProjectAssetPath =
            ProfileFolderProjectPath + "/hazard_skirmisher.json";
        public const string SkillCasterProjectAssetPath =
            ProfileFolderProjectPath + "/skill_caster.json";
        public const string AggressiveBerserkerProjectAssetPath =
            ProfileFolderProjectPath + "/aggressive_berserker.json";
        public const string DefensiveGuardianProjectAssetPath =
            ProfileFolderProjectPath + "/defensive_guardian.json";
        public const string AmbusherProjectAssetPath =
            ProfileFolderProjectPath + "/ambusher.json";
        public const string ControllerZonerProjectAssetPath =
            ProfileFolderProjectPath + "/controller_zoner.json";

        private static readonly CharacterTestRuntimeAiPlannerTemplateDescriptor[] TemplateDescriptors =
        {
            new CharacterTestRuntimeAiPlannerTemplateDescriptor(
                DefaultMeleeTemplateId,
                "默认近战",
                CharacterTestRuntimeAiPlannerProfileJson.DefaultProjectAssetPath),
            new CharacterTestRuntimeAiPlannerTemplateDescriptor(
                PerceptionAwareMeleeTemplateId,
                "感知近战",
                PerceptionAwareMeleeProjectAssetPath),
            new CharacterTestRuntimeAiPlannerTemplateDescriptor(
                RangedKiterTemplateId,
                "远程风筝",
                RangedKiterProjectAssetPath),
            new CharacterTestRuntimeAiPlannerTemplateDescriptor(
                HazardSkirmisherTemplateId,
                "危险游斗",
                HazardSkirmisherProjectAssetPath),
            new CharacterTestRuntimeAiPlannerTemplateDescriptor(
                SkillCasterTemplateId,
                "技能施法",
                SkillCasterProjectAssetPath),
            new CharacterTestRuntimeAiPlannerTemplateDescriptor(
                AggressiveBerserkerTemplateId,
                "激进冲锋",
                AggressiveBerserkerProjectAssetPath),
            new CharacterTestRuntimeAiPlannerTemplateDescriptor(
                DefensiveGuardianTemplateId,
                "守卫防御",
                DefensiveGuardianProjectAssetPath),
            new CharacterTestRuntimeAiPlannerTemplateDescriptor(
                AmbusherTemplateId,
                "伏击者",
                AmbusherProjectAssetPath),
            new CharacterTestRuntimeAiPlannerTemplateDescriptor(
                ControllerZonerTemplateId,
                "控制控场",
                ControllerZonerProjectAssetPath)
        };

        public static IReadOnlyList<CharacterTestRuntimeAiPlannerTemplateDescriptor> Templates =>
            TemplateDescriptors;

        public static bool TryGetTemplate(
            string templateId,
            out CharacterTestRuntimeAiPlannerTemplateDescriptor descriptor)
        {
            for (int i = 0; i < TemplateDescriptors.Length; i++)
            {
                if (string.Equals(TemplateDescriptors[i].TemplateId, templateId, StringComparison.Ordinal))
                {
                    descriptor = TemplateDescriptors[i];
                    return true;
                }
            }

            descriptor = default;
            return false;
        }

        public static CharacterTestRuntimeAiPlannerProfile CreateTemplate(string templateId)
        {
            switch (templateId)
            {
                case DefaultMeleeTemplateId:
                    return CharacterTestRuntimeAiPlannerProfileSource.CreateDefault();
                case PerceptionAwareMeleeTemplateId:
                    return CreatePerceptionAwareMeleeTemplate();
                case RangedKiterTemplateId:
                    return CreateRangedKiterTemplate();
                case HazardSkirmisherTemplateId:
                    return CreateHazardSkirmisherTemplate();
                case SkillCasterTemplateId:
                    return CreateSkillCasterTemplate();
                case AggressiveBerserkerTemplateId:
                    return CreateAggressiveBerserkerTemplate();
                case DefensiveGuardianTemplateId:
                    return CreateDefensiveGuardianTemplate();
                case AmbusherTemplateId:
                    return CreateAmbusherTemplate();
                case ControllerZonerTemplateId:
                    return CreateControllerZonerTemplate();
                default:
                    throw new ArgumentException(
                        "Unknown CharacterTest Runtime AI Planner template: " + templateId,
                        nameof(templateId));
            }
        }

        public static CharacterTestRuntimeAiPlannerProfile CreatePerceptionAwareMeleeTemplate()
        {
            return CharacterTestRuntimeAiPlannerEditorModel.CreatePerceptionAwareMeleeTemplate();
        }

        public static CharacterTestRuntimeAiPlannerProfile CreateRangedKiterTemplate()
        {
            const string observeStateId = "observe";
            const string keepRangeStateId = "keep_range";
            const string castStateId = "cast";
            const string retreatStateId = "retreat";
            const float rangedAttackRange = 4.5f;

            return new CharacterTestRuntimeAiPlannerProfile(
                RangedKiterProfileId,
                ResolveStateNumericId(observeStateId),
                CharacterTestAiBehaviorProfileSource.DefaultTargetSelectionMode,
                rangedAttackRange,
                CreateRootStates(
                    State(observeStateId, "观察"),
                    State(keepRangeStateId, "保持距离"),
                    State(castStateId, "远程输出"),
                    State(retreatStateId, "拉开距离")),
                new[]
                {
                    Transition(string.Empty, observeStateId, 10, Bool(
                        CharacterTestRuntimeAiPlannerFactKind.TargetExists,
                        false), "无目标时观察"),
                    Transition(string.Empty, retreatStateId, 90, Bool(
                        CharacterTestRuntimeAiPlannerFactKind.EnvironmentNearHazard,
                        true), "靠近危险时撤退"),
                    Transition(string.Empty, retreatStateId, 80, LessOrEqual(
                        CharacterTestRuntimeAiPlannerFactKind.TargetDistance,
                        1.7f), "目标近身时拉开"),
                    Transition(string.Empty, castStateId, 70, Bool(
                        CharacterTestRuntimeAiPlannerFactKind.TargetInAttackRange,
                        true), "进入射程后施法"),
                    Transition(string.Empty, keepRangeStateId, 20, Bool(
                        CharacterTestRuntimeAiPlannerFactKind.TargetExists,
                        true), "发现目标后保持距离")
                },
                new[]
                {
                    Action(930001, observeStateId, CharacterTestRuntimeAiPlannerActionKind.Wait,
                        5f, 0f, "观察等待", 0, 0, Bool(
                            CharacterTestRuntimeAiPlannerFactKind.TargetExists,
                            false)),
                    Action(930010, keepRangeStateId, CharacterTestRuntimeAiPlannerActionKind.MoveToTarget,
                        55f, 1f, "可见目标时接近射程", 0, 0,
                        Bool(CharacterTestRuntimeAiPlannerFactKind.TargetVisible, true),
                        Bool(CharacterTestRuntimeAiPlannerFactKind.TargetInAttackRange, false),
                        Bool(CharacterTestRuntimeAiPlannerFactKind.MovePathAvailable, true)),
                    Action(930011, keepRangeStateId, CharacterTestRuntimeAiPlannerActionKind.Strafe,
                        45f, 1f, "视线受阻时横移找角度", 0, 0,
                        Bool(CharacterTestRuntimeAiPlannerFactKind.TargetLineOfSightBlocked, true),
                        LessOrEqual(CharacterTestRuntimeAiPlannerFactKind.TargetLastSeenAgeFrames, 30f)),
                    Action(930020, castStateId, CharacterTestRuntimeAiPlannerActionKind.CastAbilitySlot,
                        120f, 1f, "射程内技能输出", 1, 0,
                        Bool(CharacterTestRuntimeAiPlannerFactKind.TargetVisible, true),
                        Bool(CharacterTestRuntimeAiPlannerFactKind.TargetInAttackRange, true),
                        Bool(CharacterTestRuntimeAiPlannerFactKind.SelfActionRunning, false)),
                    Action(930021, castStateId, CharacterTestRuntimeAiPlannerActionKind.Strafe,
                        40f, 1.4f, "输出间隙横移", 0, 0,
                        Bool(CharacterTestRuntimeAiPlannerFactKind.TargetVisible, true),
                        Bool(CharacterTestRuntimeAiPlannerFactKind.SelfActionRunning, true)),
                    Action(930030, retreatStateId, CharacterTestRuntimeAiPlannerActionKind.Retreat,
                        100f, 1f, "危险或近身时后撤", 0, 0,
                        Bool(CharacterTestRuntimeAiPlannerFactKind.TargetExists, true)),
                    Action(930031, retreatStateId, CharacterTestRuntimeAiPlannerActionKind.Strafe,
                        65f, 1.2f, "高威胁时侧移", 0, 0,
                        GreaterOrEqual(CharacterTestRuntimeAiPlannerFactKind.TargetThreatScore, 0.7f))
                },
                CharacterTestRuntimeAiPlannerPerceptionSettings.CreateDefault(),
                RangedKiterMovement());
        }

        public static CharacterTestRuntimeAiPlannerProfile CreateHazardSkirmisherTemplate()
        {
            const string observeStateId = "observe";
            const string chaseStateId = "chase";
            const string attackStateId = "attack";
            const string repositionStateId = "reposition";
            const string recoverStateId = "recover";

            return new CharacterTestRuntimeAiPlannerProfile(
                HazardSkirmisherProfileId,
                ResolveStateNumericId(observeStateId),
                CharacterTestAiBehaviorProfileSource.DefaultTargetSelectionMode,
                CharacterTestAiBehaviorProfileSource.DefaultAttackRange,
                CreateRootStates(
                    State(observeStateId, "观察"),
                    State(chaseStateId, "追击"),
                    State(attackStateId, "攻击"),
                    State(repositionStateId, "绕行"),
                    State(recoverStateId, "脱险")),
                new[]
                {
                    Transition(string.Empty, observeStateId, 10, Bool(
                        CharacterTestRuntimeAiPlannerFactKind.TargetExists,
                        false), "无目标时观察"),
                    Transition(string.Empty, recoverStateId, 90, Bool(
                        CharacterTestRuntimeAiPlannerFactKind.EnvironmentNearHazard,
                        true), "危险优先脱离"),
                    Transition(string.Empty, attackStateId, 80, Bool(
                        CharacterTestRuntimeAiPlannerFactKind.TargetInAttackRange,
                        true), "进入攻击范围"),
                    Transition(string.Empty, repositionStateId, 70, Bool(
                        CharacterTestRuntimeAiPlannerFactKind.TargetLineOfSightBlocked,
                        true), "遮挡时绕行"),
                    Transition(string.Empty, chaseStateId, 20, Bool(
                        CharacterTestRuntimeAiPlannerFactKind.TargetExists,
                        true), "发现目标后追击")
                },
                new[]
                {
                    Action(940001, observeStateId, CharacterTestRuntimeAiPlannerActionKind.Wait,
                        5f, 0f, "观察等待", 0, 0, Bool(
                            CharacterTestRuntimeAiPlannerFactKind.TargetExists,
                            false)),
                    Action(940010, chaseStateId, CharacterTestRuntimeAiPlannerActionKind.MoveToTarget,
                        60f, 1f, "直线路径清晰时追击", 0, 0,
                        Bool(CharacterTestRuntimeAiPlannerFactKind.TargetVisible, true),
                        Bool(CharacterTestRuntimeAiPlannerFactKind.MoveDirectPathClear, true),
                        Bool(CharacterTestRuntimeAiPlannerFactKind.TargetInAttackRange, false)),
                    Action(940011, chaseStateId, CharacterTestRuntimeAiPlannerActionKind.Strafe,
                        38f, 1.1f, "靠近障碍时游走", 0, 0,
                        Bool(CharacterTestRuntimeAiPlannerFactKind.EnvironmentNearObstacle, true),
                        Bool(CharacterTestRuntimeAiPlannerFactKind.TargetVisible, true)),
                    Action(940020, attackStateId, CharacterTestRuntimeAiPlannerActionKind.PrimaryAttack,
                        100f, 1f, "安全轻攻击", 0, 0,
                        Bool(CharacterTestRuntimeAiPlannerFactKind.TargetVisible, true),
                        Bool(CharacterTestRuntimeAiPlannerFactKind.TargetInAttackRange, true),
                        Bool(CharacterTestRuntimeAiPlannerFactKind.SelfActionRunning, false)),
                    Action(940021, attackStateId, CharacterTestRuntimeAiPlannerActionKind.Wait,
                        10f, 0f, "动作中等待", 0, 0,
                        Bool(CharacterTestRuntimeAiPlannerFactKind.SelfActionRunning, true)),
                    Action(940030, repositionStateId, CharacterTestRuntimeAiPlannerActionKind.Strafe,
                        70f, 1f, "遮挡时横移找线", 0, 0,
                        Bool(CharacterTestRuntimeAiPlannerFactKind.TargetLineOfSightBlocked, true),
                        LessOrEqual(CharacterTestRuntimeAiPlannerFactKind.TargetLastSeenAgeFrames, 30f)),
                    Action(940031, repositionStateId, CharacterTestRuntimeAiPlannerActionKind.Wait,
                        15f, 0.1f, "记忆目标等待", 0, 0,
                        Bool(CharacterTestRuntimeAiPlannerFactKind.TargetVisible, false),
                        LessOrEqual(CharacterTestRuntimeAiPlannerFactKind.TargetLastSeenAgeFrames, 30f)),
                    Action(940040, recoverStateId, CharacterTestRuntimeAiPlannerActionKind.Retreat,
                        120f, 1f, "危险脱离", 0, 0,
                        Bool(CharacterTestRuntimeAiPlannerFactKind.EnvironmentNearHazard, true)),
                    Action(940041, recoverStateId, CharacterTestRuntimeAiPlannerActionKind.Strafe,
                        45f, 1.2f, "利用物体绕行", 0, 0,
                        Bool(CharacterTestRuntimeAiPlannerFactKind.EnvironmentNearUsefulObject, true))
                },
                CharacterTestRuntimeAiPlannerPerceptionSettings.CreateDefault(),
                HazardSkirmisherMovement());
        }

        public static CharacterTestRuntimeAiPlannerProfile CreateSkillCasterTemplate()
        {
            const string observeStateId = "observe";
            const string approachStateId = "approach";
            const string castStateId = "cast";
            const string recoverStateId = "recover";
            const float casterAttackRange = 4.2f;

            return new CharacterTestRuntimeAiPlannerProfile(
                SkillCasterProfileId,
                ResolveStateNumericId(observeStateId),
                CharacterTestAiBehaviorProfileSource.DefaultTargetSelectionMode,
                casterAttackRange,
                CreateRootStates(
                    State(observeStateId, "观察"),
                    State(approachStateId, "接近"),
                    State(castStateId, "施法"),
                    State(recoverStateId, "脱离")),
                new[]
                {
                    Transition(string.Empty, observeStateId, 10, Bool(
                        CharacterTestRuntimeAiPlannerFactKind.TargetExists,
                        false), "无目标时观察"),
                    Transition(string.Empty, recoverStateId, 90, Bool(
                        CharacterTestRuntimeAiPlannerFactKind.EnvironmentNearHazard,
                        true), "危险时脱离"),
                    Transition(string.Empty, recoverStateId, 80, LessOrEqual(
                        CharacterTestRuntimeAiPlannerFactKind.TargetDistance,
                        1.5f), "被近身时后撤"),
                    Transition(string.Empty, castStateId, 70, Bool(
                        CharacterTestRuntimeAiPlannerFactKind.TargetInAttackRange,
                        true), "进入施法范围"),
                    Transition(string.Empty, approachStateId, 20, Bool(
                        CharacterTestRuntimeAiPlannerFactKind.TargetExists,
                        true), "发现目标后接近")
                },
                new[]
                {
                    Action(950001, observeStateId, CharacterTestRuntimeAiPlannerActionKind.Wait,
                        5f, 0f, "观察等待", 0, 0, Bool(
                            CharacterTestRuntimeAiPlannerFactKind.TargetExists,
                            false)),
                    Action(950010, approachStateId, CharacterTestRuntimeAiPlannerActionKind.MoveToTarget,
                        55f, 1f, "可见目标时进入施法距离", 0, 0,
                        Bool(CharacterTestRuntimeAiPlannerFactKind.TargetVisible, true),
                        Bool(CharacterTestRuntimeAiPlannerFactKind.TargetInAttackRange, false),
                        Bool(CharacterTestRuntimeAiPlannerFactKind.MovePathAvailable, true)),
                    Action(950011, approachStateId, CharacterTestRuntimeAiPlannerActionKind.Wait,
                        12f, 0.1f, "遮挡时等待记忆目标", 0, 0,
                        Bool(CharacterTestRuntimeAiPlannerFactKind.TargetLineOfSightBlocked, true),
                        LessOrEqual(CharacterTestRuntimeAiPlannerFactKind.TargetLastSeenAgeFrames, 30f)),
                    Action(950020, castStateId, CharacterTestRuntimeAiPlannerActionKind.CastAbilitySlot,
                        130f, 1f, "优先施放一号技能", 1, 0,
                        Bool(CharacterTestRuntimeAiPlannerFactKind.TargetVisible, true),
                        Bool(CharacterTestRuntimeAiPlannerFactKind.TargetInAttackRange, true),
                        Bool(CharacterTestRuntimeAiPlannerFactKind.SelfActionRunning, false)),
                    Action(950021, castStateId, CharacterTestRuntimeAiPlannerActionKind.Wait,
                        20f, 0f, "施法动作中等待", 0, 0,
                        Bool(CharacterTestRuntimeAiPlannerFactKind.SelfActionRunning, true)),
                    Action(950030, recoverStateId, CharacterTestRuntimeAiPlannerActionKind.Retreat,
                        110f, 1f, "危险或近身时后撤", 0, 0,
                        Bool(CharacterTestRuntimeAiPlannerFactKind.TargetExists, true)),
                    Action(950031, recoverStateId, CharacterTestRuntimeAiPlannerActionKind.Strafe,
                        55f, 1.2f, "可利用物体附近侧移", 0, 0,
                        Bool(CharacterTestRuntimeAiPlannerFactKind.EnvironmentNearUsefulObject, true))
                },
                CharacterTestRuntimeAiPlannerPerceptionSettings.CreateDefault(),
                SkillCasterMovement());
        }

        public static CharacterTestRuntimeAiPlannerProfile CreateAggressiveBerserkerTemplate()
        {
            const string observeStateId = "observe";
            const string chaseStateId = "chase";
            const string attackStateId = "attack";
            const string repositionStateId = "reposition";
            const float berserkerAttackRange = 1.6f;

            return new CharacterTestRuntimeAiPlannerProfile(
                AggressiveBerserkerProfileId,
                ResolveStateNumericId(observeStateId),
                CharacterTestAiBehaviorProfileSource.DefaultTargetSelectionMode,
                berserkerAttackRange,
                CreateRootStates(
                    State(observeStateId, "观察"),
                    State(chaseStateId, "冲锋"),
                    State(attackStateId, "猛攻"),
                    State(repositionStateId, "破障")),
                new[]
                {
                    Transition(string.Empty, observeStateId, 10, Bool(
                        CharacterTestRuntimeAiPlannerFactKind.TargetExists,
                        false), "无目标时观察"),
                    Transition(string.Empty, attackStateId, 90, Bool(
                        CharacterTestRuntimeAiPlannerFactKind.TargetInAttackRange,
                        true), "贴近后猛攻"),
                    Transition(string.Empty, repositionStateId, 70, Bool(
                        CharacterTestRuntimeAiPlannerFactKind.TargetLineOfSightBlocked,
                        true), "遮挡时破障绕行"),
                    Transition(string.Empty, chaseStateId, 20, Bool(
                        CharacterTestRuntimeAiPlannerFactKind.TargetExists,
                        true), "发现目标后冲锋")
                },
                new[]
                {
                    Action(960001, observeStateId, CharacterTestRuntimeAiPlannerActionKind.Wait,
                        5f, 0f, "观察等待", 0, 0, Bool(
                            CharacterTestRuntimeAiPlannerFactKind.TargetExists,
                            false)),
                    Action(960010, chaseStateId, CharacterTestRuntimeAiPlannerActionKind.MoveToTarget,
                        95f, 1f, "可见目标时直冲", 0, 0,
                        Bool(CharacterTestRuntimeAiPlannerFactKind.TargetVisible, true),
                        Bool(CharacterTestRuntimeAiPlannerFactKind.TargetInAttackRange, false),
                        Bool(CharacterTestRuntimeAiPlannerFactKind.MovePathAvailable, true)),
                    Action(960011, chaseStateId, CharacterTestRuntimeAiPlannerActionKind.Strafe,
                        35f, 1f, "靠近障碍时压迫走位", 0, 0,
                        Bool(CharacterTestRuntimeAiPlannerFactKind.EnvironmentNearObstacle, true),
                        Bool(CharacterTestRuntimeAiPlannerFactKind.TargetVisible, true)),
                    Action(960020, attackStateId, CharacterTestRuntimeAiPlannerActionKind.PrimaryAttack,
                        140f, 1f, "近身连续轻攻击", 0, 0,
                        Bool(CharacterTestRuntimeAiPlannerFactKind.TargetVisible, true),
                        Bool(CharacterTestRuntimeAiPlannerFactKind.TargetInAttackRange, true),
                        Bool(CharacterTestRuntimeAiPlannerFactKind.SelfActionRunning, false)),
                    Action(960021, attackStateId, CharacterTestRuntimeAiPlannerActionKind.Wait,
                        15f, 0f, "攻击动作中等待", 0, 0,
                        Bool(CharacterTestRuntimeAiPlannerFactKind.SelfActionRunning, true)),
                    Action(960030, repositionStateId, CharacterTestRuntimeAiPlannerActionKind.Strafe,
                        75f, 1f, "遮挡时横移压近", 0, 0,
                        Bool(CharacterTestRuntimeAiPlannerFactKind.TargetLineOfSightBlocked, true),
                        LessOrEqual(CharacterTestRuntimeAiPlannerFactKind.TargetLastSeenAgeFrames, 30f)),
                    Action(960031, repositionStateId, CharacterTestRuntimeAiPlannerActionKind.MoveToTarget,
                        55f, 1.1f, "仍有路径时继续追压", 0, 0,
                        Bool(CharacterTestRuntimeAiPlannerFactKind.TargetExists, true),
                        Bool(CharacterTestRuntimeAiPlannerFactKind.MovePathAvailable, true))
                },
                CharacterTestRuntimeAiPlannerPerceptionSettings.CreateDefault(),
                AggressiveMeleeMovement());
        }

        public static CharacterTestRuntimeAiPlannerProfile CreateDefensiveGuardianTemplate()
        {
            const string observeStateId = "observe";
            const string holdStateId = "hold";
            const string interceptStateId = "intercept";
            const string attackStateId = "attack";
            const string recoverStateId = "recover";
            const float guardianAttackRange = 1.8f;

            return new CharacterTestRuntimeAiPlannerProfile(
                DefensiveGuardianProfileId,
                ResolveStateNumericId(observeStateId),
                CharacterTestAiBehaviorProfileSource.DefaultTargetSelectionMode,
                guardianAttackRange,
                CreateRootStates(
                    State(observeStateId, "观察"),
                    State(holdStateId, "守位"),
                    State(interceptStateId, "拦截"),
                    State(attackStateId, "反击"),
                    State(recoverStateId, "脱险")),
                new[]
                {
                    Transition(string.Empty, observeStateId, 10, Bool(
                        CharacterTestRuntimeAiPlannerFactKind.TargetExists,
                        false), "无目标时守望"),
                    Transition(string.Empty, recoverStateId, 95, Bool(
                        CharacterTestRuntimeAiPlannerFactKind.EnvironmentNearHazard,
                        true), "危险优先脱离"),
                    Transition(string.Empty, attackStateId, 80, Bool(
                        CharacterTestRuntimeAiPlannerFactKind.TargetInAttackRange,
                        true), "进入反击范围"),
                    Transition(string.Empty, holdStateId, 70, Bool(
                        CharacterTestRuntimeAiPlannerFactKind.EnvironmentNearUsefulObject,
                        true), "靠近可利用物时守位"),
                    Transition(string.Empty, holdStateId, 60, Bool(
                        CharacterTestRuntimeAiPlannerFactKind.EnvironmentNearObstacle,
                        true), "靠近障碍时守位"),
                    Transition(string.Empty, interceptStateId, 20, Bool(
                        CharacterTestRuntimeAiPlannerFactKind.TargetExists,
                        true), "发现目标后拦截")
                },
                new[]
                {
                    Action(970001, observeStateId, CharacterTestRuntimeAiPlannerActionKind.Wait,
                        8f, 0f, "守望等待", 0, 0, Bool(
                            CharacterTestRuntimeAiPlannerFactKind.TargetExists,
                            false)),
                    Action(970010, holdStateId, CharacterTestRuntimeAiPlannerActionKind.Strafe,
                        65f, 1f, "围绕可利用物守位", 0, 0,
                        Bool(CharacterTestRuntimeAiPlannerFactKind.EnvironmentNearUsefulObject, true)),
                    Action(970011, holdStateId, CharacterTestRuntimeAiPlannerActionKind.Wait,
                        35f, 0.1f, "遮挡时稳住位置", 0, 0,
                        Bool(CharacterTestRuntimeAiPlannerFactKind.TargetVisible, false),
                        LessOrEqual(CharacterTestRuntimeAiPlannerFactKind.TargetLastSeenAgeFrames, 30f)),
                    Action(970020, interceptStateId, CharacterTestRuntimeAiPlannerActionKind.MoveToTarget,
                        58f, 1f, "路径清晰时拦截", 0, 0,
                        Bool(CharacterTestRuntimeAiPlannerFactKind.TargetVisible, true),
                        Bool(CharacterTestRuntimeAiPlannerFactKind.TargetInAttackRange, false),
                        Bool(CharacterTestRuntimeAiPlannerFactKind.MovePathAvailable, true)),
                    Action(970021, interceptStateId, CharacterTestRuntimeAiPlannerActionKind.Strafe,
                        48f, 1f, "目标被遮挡时侧移卡位", 0, 0,
                        Bool(CharacterTestRuntimeAiPlannerFactKind.TargetLineOfSightBlocked, true),
                        LessOrEqual(CharacterTestRuntimeAiPlannerFactKind.TargetLastSeenAgeFrames, 30f)),
                    Action(970030, attackStateId, CharacterTestRuntimeAiPlannerActionKind.PrimaryAttack,
                        105f, 1f, "进入范围后反击", 0, 0,
                        Bool(CharacterTestRuntimeAiPlannerFactKind.TargetVisible, true),
                        Bool(CharacterTestRuntimeAiPlannerFactKind.TargetInAttackRange, true),
                        Bool(CharacterTestRuntimeAiPlannerFactKind.SelfActionRunning, false)),
                    Action(970031, attackStateId, CharacterTestRuntimeAiPlannerActionKind.Wait,
                        15f, 0f, "反击动作中等待", 0, 0,
                        Bool(CharacterTestRuntimeAiPlannerFactKind.SelfActionRunning, true)),
                    Action(970040, recoverStateId, CharacterTestRuntimeAiPlannerActionKind.Retreat,
                        115f, 1f, "危险脱离", 0, 0,
                        Bool(CharacterTestRuntimeAiPlannerFactKind.EnvironmentNearHazard, true)),
                    Action(970041, recoverStateId, CharacterTestRuntimeAiPlannerActionKind.Strafe,
                        50f, 1.2f, "利用掩体重整", 0, 0,
                        Bool(CharacterTestRuntimeAiPlannerFactKind.EnvironmentNearUsefulObject, true))
                },
                CharacterTestRuntimeAiPlannerPerceptionSettings.CreateDefault(),
                DefensiveGuardianMovement());
        }

        public static CharacterTestRuntimeAiPlannerProfile CreateAmbusherTemplate()
        {
            const string hideStateId = "hide";
            const string stalkStateId = "stalk";
            const string strikeStateId = "strike";
            const string relocateStateId = "relocate";
            const float ambusherAttackRange = 2.2f;

            return new CharacterTestRuntimeAiPlannerProfile(
                AmbusherProfileId,
                ResolveStateNumericId(hideStateId),
                CharacterTestAiBehaviorProfileSource.DefaultTargetSelectionMode,
                ambusherAttackRange,
                CreateRootStates(
                    State(hideStateId, "潜伏"),
                    State(stalkStateId, "跟踪"),
                    State(strikeStateId, "突袭"),
                    State(relocateStateId, "换位")),
                new[]
                {
                    Transition(string.Empty, hideStateId, 10, Bool(
                        CharacterTestRuntimeAiPlannerFactKind.TargetExists,
                        false), "无目标时潜伏"),
                    Transition(string.Empty, strikeStateId, 90, Bool(
                        CharacterTestRuntimeAiPlannerFactKind.TargetInAttackRange,
                        true), "目标进入突袭范围"),
                    Transition(string.Empty, relocateStateId, 80, Bool(
                        CharacterTestRuntimeAiPlannerFactKind.TargetLineOfSightBlocked,
                        true), "遮挡时换位"),
                    Transition(string.Empty, stalkStateId, 20, Bool(
                        CharacterTestRuntimeAiPlannerFactKind.TargetExists,
                        true), "发现目标后跟踪")
                },
                new[]
                {
                    Action(980001, hideStateId, CharacterTestRuntimeAiPlannerActionKind.Wait,
                        20f, 0f, "潜伏等待", 0, 0, Bool(
                            CharacterTestRuntimeAiPlannerFactKind.TargetExists,
                            false)),
                    Action(980002, hideStateId, CharacterTestRuntimeAiPlannerActionKind.Strafe,
                        45f, 1f, "靠近掩体时调整伏击位", 0, 0,
                        Bool(CharacterTestRuntimeAiPlannerFactKind.EnvironmentNearUsefulObject, true)),
                    Action(980010, stalkStateId, CharacterTestRuntimeAiPlannerActionKind.MoveToTarget,
                        50f, 1f, "可见目标时贴近", 0, 0,
                        Bool(CharacterTestRuntimeAiPlannerFactKind.TargetVisible, true),
                        Bool(CharacterTestRuntimeAiPlannerFactKind.TargetInAttackRange, false),
                        Bool(CharacterTestRuntimeAiPlannerFactKind.MovePathAvailable, true)),
                    Action(980011, stalkStateId, CharacterTestRuntimeAiPlannerActionKind.Strafe,
                        55f, 1f, "利用障碍跟踪", 0, 0,
                        Bool(CharacterTestRuntimeAiPlannerFactKind.EnvironmentNearObstacle, true),
                        Bool(CharacterTestRuntimeAiPlannerFactKind.TargetVisible, true)),
                    Action(980020, strikeStateId, CharacterTestRuntimeAiPlannerActionKind.CastAbilitySlot,
                        125f, 1f, "突袭技能", 2, 0,
                        Bool(CharacterTestRuntimeAiPlannerFactKind.TargetVisible, true),
                        Bool(CharacterTestRuntimeAiPlannerFactKind.TargetInAttackRange, true),
                        Bool(CharacterTestRuntimeAiPlannerFactKind.SelfActionRunning, false)),
                    Action(980021, strikeStateId, CharacterTestRuntimeAiPlannerActionKind.PrimaryAttack,
                        85f, 1f, "突袭后补刀", 0, 0,
                        Bool(CharacterTestRuntimeAiPlannerFactKind.TargetVisible, true),
                        Bool(CharacterTestRuntimeAiPlannerFactKind.TargetInAttackRange, true),
                        Bool(CharacterTestRuntimeAiPlannerFactKind.SelfActionRunning, false)),
                    Action(980022, strikeStateId, CharacterTestRuntimeAiPlannerActionKind.Wait,
                        15f, 0f, "突袭动作中等待", 0, 0,
                        Bool(CharacterTestRuntimeAiPlannerFactKind.SelfActionRunning, true)),
                    Action(980030, relocateStateId, CharacterTestRuntimeAiPlannerActionKind.Strafe,
                        70f, 1f, "遮挡时重新找角度", 0, 0,
                        Bool(CharacterTestRuntimeAiPlannerFactKind.TargetLineOfSightBlocked, true),
                        LessOrEqual(CharacterTestRuntimeAiPlannerFactKind.TargetLastSeenAgeFrames, 30f)),
                    Action(980031, relocateStateId, CharacterTestRuntimeAiPlannerActionKind.Wait,
                        18f, 0.1f, "记忆目标短暂停顿", 0, 0,
                        Bool(CharacterTestRuntimeAiPlannerFactKind.TargetVisible, false),
                        LessOrEqual(CharacterTestRuntimeAiPlannerFactKind.TargetLastSeenAgeFrames, 30f))
                },
                CharacterTestRuntimeAiPlannerPerceptionSettings.CreateDefault(),
                AmbusherMovement());
        }

        public static CharacterTestRuntimeAiPlannerProfile CreateControllerZonerTemplate()
        {
            const string observeStateId = "observe";
            const string zoneStateId = "zone";
            const string castStateId = "cast";
            const string evadeStateId = "evade";
            const float controllerAttackRange = 4.8f;

            return new CharacterTestRuntimeAiPlannerProfile(
                ControllerZonerProfileId,
                ResolveStateNumericId(observeStateId),
                CharacterTestAiBehaviorProfileSource.DefaultTargetSelectionMode,
                controllerAttackRange,
                CreateRootStates(
                    State(observeStateId, "观察"),
                    State(zoneStateId, "控场"),
                    State(castStateId, "释放"),
                    State(evadeStateId, "规避")),
                new[]
                {
                    Transition(string.Empty, observeStateId, 10, Bool(
                        CharacterTestRuntimeAiPlannerFactKind.TargetExists,
                        false), "无目标时观察"),
                    Transition(string.Empty, evadeStateId, 95, Bool(
                        CharacterTestRuntimeAiPlannerFactKind.EnvironmentNearHazard,
                        true), "危险时规避"),
                    Transition(string.Empty, evadeStateId, 85, LessOrEqual(
                        CharacterTestRuntimeAiPlannerFactKind.TargetDistance,
                        1.6f), "被近身时规避"),
                    Transition(string.Empty, castStateId, 70, Bool(
                        CharacterTestRuntimeAiPlannerFactKind.TargetInAttackRange,
                        true), "进入控场范围"),
                    Transition(string.Empty, zoneStateId, 20, Bool(
                        CharacterTestRuntimeAiPlannerFactKind.TargetExists,
                        true), "发现目标后控场")
                },
                new[]
                {
                    Action(990001, observeStateId, CharacterTestRuntimeAiPlannerActionKind.Wait,
                        5f, 0f, "观察等待", 0, 0, Bool(
                            CharacterTestRuntimeAiPlannerFactKind.TargetExists,
                            false)),
                    Action(990010, zoneStateId, CharacterTestRuntimeAiPlannerActionKind.MoveToTarget,
                        50f, 1f, "进入控场距离", 0, 0,
                        Bool(CharacterTestRuntimeAiPlannerFactKind.TargetVisible, true),
                        Bool(CharacterTestRuntimeAiPlannerFactKind.TargetInAttackRange, false),
                        Bool(CharacterTestRuntimeAiPlannerFactKind.MovePathAvailable, true)),
                    Action(990011, zoneStateId, CharacterTestRuntimeAiPlannerActionKind.Strafe,
                        55f, 1f, "遮挡时横移布控", 0, 0,
                        Bool(CharacterTestRuntimeAiPlannerFactKind.TargetLineOfSightBlocked, true),
                        LessOrEqual(CharacterTestRuntimeAiPlannerFactKind.TargetLastSeenAgeFrames, 30f)),
                    Action(990020, castStateId, CharacterTestRuntimeAiPlannerActionKind.CastAbilitySlot,
                        135f, 1f, "控场技能", 3, 0,
                        Bool(CharacterTestRuntimeAiPlannerFactKind.TargetVisible, true),
                        Bool(CharacterTestRuntimeAiPlannerFactKind.TargetInAttackRange, true),
                        Bool(CharacterTestRuntimeAiPlannerFactKind.SelfActionRunning, false)),
                    Action(990021, castStateId, CharacterTestRuntimeAiPlannerActionKind.Strafe,
                        45f, 1.2f, "释放间隙横移", 0, 0,
                        Bool(CharacterTestRuntimeAiPlannerFactKind.TargetVisible, true),
                        Bool(CharacterTestRuntimeAiPlannerFactKind.SelfActionRunning, true)),
                    Action(990030, evadeStateId, CharacterTestRuntimeAiPlannerActionKind.Retreat,
                        110f, 1f, "拉开控场距离", 0, 0,
                        Bool(CharacterTestRuntimeAiPlannerFactKind.TargetExists, true)),
                    Action(990031, evadeStateId, CharacterTestRuntimeAiPlannerActionKind.Strafe,
                        70f, 1f, "高威胁时侧移规避", 0, 0,
                        GreaterOrEqual(CharacterTestRuntimeAiPlannerFactKind.TargetThreatScore, 0.75f))
                },
                CharacterTestRuntimeAiPlannerPerceptionSettings.CreateDefault(),
                ControllerZonerMovement());
        }

        private static CharacterTestRuntimeAiPlannerStateDefinition[] CreateRootStates(
            params CharacterTestRuntimeAiPlannerStateDefinition[] childStates)
        {
            var states = new CharacterTestRuntimeAiPlannerStateDefinition[childStates.Length + 1];
            states[0] = new CharacterTestRuntimeAiPlannerStateDefinition(
                CharacterTestRuntimeAiPlannerProfileSource.RootStateId,
                displayName: "根");
            for (int i = 0; i < childStates.Length; i++)
                states[i + 1] = childStates[i];
            return states;
        }

        private static CharacterTestRuntimeAiPlannerStateDefinition State(
            string stateId,
            string displayName)
        {
            return new CharacterTestRuntimeAiPlannerStateDefinition(
                ResolveStateNumericId(stateId),
                CharacterTestRuntimeAiPlannerProfileSource.RootStateId,
                displayName);
        }

        private static CharacterTestRuntimeAiPlannerTransitionDefinition Transition(
            string sourceStateId,
            string targetStateId,
            int priority,
            CharacterTestRuntimeAiPlannerConditionDefinition condition,
            string displayName)
        {
            return new CharacterTestRuntimeAiPlannerTransitionDefinition(
                ResolveStateNumericId(sourceStateId),
                ResolveStateNumericId(targetStateId),
                priority,
                condition,
                displayName);
        }

        private static CharacterTestRuntimeAiPlannerActionDefinition Action(
            int actionId,
            string stateId,
            CharacterTestRuntimeAiPlannerActionKind kind,
            float score,
            float cost,
            string displayName,
            int abilitySlot,
            int abilityId,
            params CharacterTestRuntimeAiPlannerConditionDefinition[] conditions)
        {
            return new CharacterTestRuntimeAiPlannerActionDefinition(
                actionId,
                ResolveStateNumericId(stateId),
                kind,
                score,
                cost,
                displayName,
                abilitySlot,
                abilityId,
                conditions);
        }

        private static int ResolveStateNumericId(string value)
        {
            return CharacterTestRuntimeAiPlannerEditorStateIdentity.ResolveOrAllocate(value);
        }

        private static CharacterTestRuntimeAiPlannerMovementSettings RangedKiterMovement()
        {
            return new CharacterTestRuntimeAiPlannerMovementSettings(
                speedScale: 0.9f,
                steeringResponsiveness: 0.32f,
                approachLateralBlend: 0.2f,
                preferredDistanceMin: 2.8f,
                preferredDistanceMax: 4.3f,
                arrivalSlowdownDistance: 1.6f,
                strafeSwitchMinFrames: 18,
                strafeSwitchMaxFrames: 48,
                directionHoldFrames: 7,
                reactionDelayFrames: 3);
        }

        private static CharacterTestRuntimeAiPlannerMovementSettings HazardSkirmisherMovement()
        {
            return new CharacterTestRuntimeAiPlannerMovementSettings(
                speedScale: 1f,
                steeringResponsiveness: 0.42f,
                approachLateralBlend: 0.18f,
                preferredDistanceMin: 0f,
                preferredDistanceMax: 0f,
                arrivalSlowdownDistance: 1f,
                strafeSwitchMinFrames: 14,
                strafeSwitchMaxFrames: 38,
                directionHoldFrames: 5,
                reactionDelayFrames: 1);
        }

        private static CharacterTestRuntimeAiPlannerMovementSettings SkillCasterMovement()
        {
            return new CharacterTestRuntimeAiPlannerMovementSettings(
                speedScale: 0.82f,
                steeringResponsiveness: 0.3f,
                approachLateralBlend: 0.14f,
                preferredDistanceMin: 2.4f,
                preferredDistanceMax: 4f,
                arrivalSlowdownDistance: 1.8f,
                strafeSwitchMinFrames: 24,
                strafeSwitchMaxFrames: 64,
                directionHoldFrames: 8,
                reactionDelayFrames: 4);
        }

        private static CharacterTestRuntimeAiPlannerMovementSettings AggressiveMeleeMovement()
        {
            return new CharacterTestRuntimeAiPlannerMovementSettings(
                speedScale: 1f,
                steeringResponsiveness: 0.5f,
                approachLateralBlend: 0.08f,
                preferredDistanceMin: 0f,
                preferredDistanceMax: 0f,
                arrivalSlowdownDistance: 0.8f,
                strafeSwitchMinFrames: 12,
                strafeSwitchMaxFrames: 32,
                directionHoldFrames: 4,
                reactionDelayFrames: 0);
        }

        private static CharacterTestRuntimeAiPlannerMovementSettings DefensiveGuardianMovement()
        {
            return new CharacterTestRuntimeAiPlannerMovementSettings(
                speedScale: 0.75f,
                steeringResponsiveness: 0.28f,
                approachLateralBlend: 0.12f,
                preferredDistanceMin: 1.2f,
                preferredDistanceMax: 1.8f,
                arrivalSlowdownDistance: 1.2f,
                strafeSwitchMinFrames: 28,
                strafeSwitchMaxFrames: 72,
                directionHoldFrames: 10,
                reactionDelayFrames: 3);
        }

        private static CharacterTestRuntimeAiPlannerMovementSettings AmbusherMovement()
        {
            return new CharacterTestRuntimeAiPlannerMovementSettings(
                speedScale: 0.92f,
                steeringResponsiveness: 0.34f,
                approachLateralBlend: 0.24f,
                preferredDistanceMin: 0.6f,
                preferredDistanceMax: 1.8f,
                arrivalSlowdownDistance: 1.4f,
                strafeSwitchMinFrames: 20,
                strafeSwitchMaxFrames: 54,
                directionHoldFrames: 7,
                reactionDelayFrames: 2);
        }

        private static CharacterTestRuntimeAiPlannerMovementSettings ControllerZonerMovement()
        {
            return new CharacterTestRuntimeAiPlannerMovementSettings(
                speedScale: 0.86f,
                steeringResponsiveness: 0.31f,
                approachLateralBlend: 0.18f,
                preferredDistanceMin: 3f,
                preferredDistanceMax: 4.6f,
                arrivalSlowdownDistance: 1.8f,
                strafeSwitchMinFrames: 22,
                strafeSwitchMaxFrames: 58,
                directionHoldFrames: 8,
                reactionDelayFrames: 3);
        }

        private static CharacterTestRuntimeAiPlannerConditionDefinition Bool(
            CharacterTestRuntimeAiPlannerFactKind fact,
            bool value)
        {
            return CharacterTestRuntimeAiPlannerConditionDefinition.Bool(fact, value);
        }

        private static CharacterTestRuntimeAiPlannerConditionDefinition LessOrEqual(
            CharacterTestRuntimeAiPlannerFactKind fact,
            float value)
        {
            return new CharacterTestRuntimeAiPlannerConditionDefinition(
                fact,
                CharacterTestRuntimeAiPlannerConditionOperator.LessOrEqual,
                true,
                value);
        }

        private static CharacterTestRuntimeAiPlannerConditionDefinition GreaterOrEqual(
            CharacterTestRuntimeAiPlannerFactKind fact,
            float value)
        {
            return new CharacterTestRuntimeAiPlannerConditionDefinition(
                fact,
                CharacterTestRuntimeAiPlannerConditionOperator.GreaterOrEqual,
                true,
                value);
        }
    }
}
