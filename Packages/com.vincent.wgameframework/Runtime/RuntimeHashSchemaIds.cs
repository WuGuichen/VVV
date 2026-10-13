namespace MxFramework.Runtime
{
    /// <summary>
    /// Global unique Schema IDs for all hash contributors and state entry points.
    /// </summary>
    public static class RuntimeHashSchemaIds
    {
        /// <summary>
        /// Default schema version for all runtime hash states.
        /// </summary>
        public const int Version1 = 1;

        // Framework Infrastructure
        public const int CombinedState = 1;  // existing, from RuntimeHashProtocolIds
        public const int TraceIdentity = 20;
        public const int PreparedContentDigest = 21;
        
        // Session/Network  
        public const int SessionHash = 1001;  // existing, from CharacterTestSessionHashContract
        public const int NetworkBoundedState = 1002;
        public const int NetworkSnapshot = 1003;
        public const int NetworkSnapshotBodyPart = 1004;
        public const int NetworkSnapshotAuthorityPose = 1005;
        public const int NetworkSnapshotProjectile = 1006;
        public const int NetworkSnapshotHookPull = 1007;
        public const int NetworkSnapshotArcMine = 1008;
        public const int NetworkSnapshotRuneTurret = 1009;
        public const int NetworkSnapshotAreaConfinement = 1010;
        public const int NetworkSnapshotCombatTarget = 1011;
        public const int NetworkSnapshotPoisonBottlePool = 1012;
        public const int NetworkSnapshotPhysicalShield = 1013;
        public const int ReplayValidation = 1014;
        public const int GenesisPotencyTriplet = 1015;
        public const int GenesisPotencyProfile = 1016;
        public const int GenesisPotencyApplication = 1017;
        public const int NetworkSnapshotDoubleEdgeStage = 1018;
        
        // Gameplay Core
        public const int GameplayWorld = 2001;
        public const int GameplayComponentWorld = 2002;
        public const int AbilityRuntime = 2003;
        public const int AbilityEffectTimeline = 2004;
        public const int AbilityGraphDefinition = 2005;
        public const int GameplayBuffDefinitions = 2006;
        public const int GameplayTriggers = 2007;
        public const int GameplayTriggerSceneCatalog = 2008;
        public const int CharacterAuthorityPose = 2009;
        
        // Gameplay Component Schemas (use sub-range)
        public const int GameplayComponentCooldown = 2101;
        public const int GameplayComponentAttribute = 2102;
        public const int GameplayComponentAuraSource = 2103;
        public const int GameplayComponentBuff = 2104;
        public const int GameplayComponentModifier = 2105;
        public const int GameplayComponentCore = 2106;
        public const int GameplayComponentTargetClassification = 2107;
        // Individual save-state identities for core component schemas. 2106 remains
        // reserved for the aggregate core component-world schema.
        public const int GameplayComponentIdentity = 2110;
        public const int GameplayComponentTeam = 2111;
        public const int GameplayComponentLifecycle = 2112;
        public const int GameplayComponentTags = 2113;
        public const int GameplayComponentStatuses = 2114;
        public const int GameplayComponentScatterPressure = 2115;
        public const int GameplayComponentGuardPressure = 2116;
        public const int GameplayComponentArmorIntegrity = 2117;
        
        // Combat
        public const int CombatProjectile = 3001;
        public const int CombatShield = 3002;
        public const int CombatHitResolutionPolicy = 3003;
        public const int CombatHitTraceWindow = 3004;
        public const int CombatTrace = 3005;
        
        // AI Planner
        public const int RuntimeAiPlannerState = 4001;
        public const int RuntimeAiPlannerSlotState = 4002;
        public const int RuntimeAiPlannerSessionAuthority = 4003;
        public const int RuntimeAiPlannerSessionParticipant = 4004;
        public const int RuntimeAiPlannerInstalledDeck = 4005;
        public const int RuntimeAiPlannerDeckPersistence = 4006;
        public const int RuntimeAiPlannerBehaviorGroupCatalog = 4007;
        public const int RuntimeAiPlannerBehaviorComposition = 4008;
        public const int RuntimeAiPlannerBehaviorExecution = 4009;
        public const int RuntimeAiPlannerBehaviorProgram = 4010;
        public const int RuntimeAiPlannerBehaviorExecutionState = 4011;
        public const int RuntimeAiPlannerBehaviorExecutionRuntime = 4012;
        public const int RuntimeAiPlannerBehaviorExecutionHost = 4013;
        public const int RuntimeAiPlannerStateArray = 4014;
        public const int RuntimeAiPlannerLabFrameState = 4015;
        public const int RuntimeAiPlannerTraceValue = 4016;
        public const int RuntimeAiPlannerTacticalSession = 4017;
        
        // CharacterTest Specific
        public const int CharacterTestNavigationGraph = 5001;
        public const int CharacterTestInteractionEvent = 5002;
        public const int CharacterTestBehaviorControlInput = 5003;
        public const int CharacterTestActionUseFailure = 5004;
        
        // Items
        public const int ItemAcquisition = 6001;
        public const int ItemInventory = 6002;
        public const int ItemWorld = 6003;
        
        // Story
        public const int StoryRuntime = 7001;
        
        // VFX
        public const int VfxRuntime = 8001;
        
        // Demos
        public const int DemoGameplayTriggerPlayable = 9001;
        public const int DemoMarbleMaze = 9002;
        public const int DemoBreakout = 9003;
        public const int DemoCharacterControl = 9004;
        public const int DemoTetris = 9005;
    }

    /// <summary>
    /// Permanent integer ContributorIds for each IRuntimeHashContributor. 
    /// Frozen: do not reorder, reassign, or reuse retired IDs.
    /// </summary>
    public static class RuntimeHashContributorIds
    {
        // Gameplay
        public const int GameplayWorld = 101;
        public const int GameplayComponentWorld = 102;
        public const int AbilityRuntime = 103;
        public const int AbilityEffectTimeline = 104;
        public const int AbilityGraphDefinition = 105;
        public const int GameplayBuffDefinitions = 106;
        
        // Combat
        public const int CombatProjectile = 201;
        public const int CombatShield = 202;
        
        // Triggers
        public const int GameplayTriggers = 301;
        public const int GameplayTriggerSceneCatalog = 302;
        
        // Items
        public const int ItemAcquisition = 401;
        public const int ItemInventory = 402;
        public const int ItemWorld = 403;
        
        // Story
        public const int StoryRuntime = 501;
        
        // VFX
        public const int VfxRuntime = 601;
        
        // Demos
        public const int DemoGameplayTriggerPlayable = 901;
        public const int DemoMarbleMaze = 902;
    }

    /// <summary>
    /// Root-level protocol field numbers shared by RuntimeHashCombiner.
    /// </summary>
    public static class RuntimeHashProtocolFields
    {
        public const int Frame = 1;
        public const int Contributors = 2;
        public const int ContributorId = 3;
        public const int ContributorState = 4;
    }
}
