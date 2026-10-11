namespace MxFramework.Game.CharacterTest.TacticalCard
{
    /// <summary>Level-owned composition settings; character and combat definitions remain in their own modules.</summary>
    public sealed class CharacterTestTacticalStageAuthoringDocument
    {
        public const string ModuleId = "characterTest.tacticalStage";
        public const int CurrentSchemaVersion = 1;

        public int InitialMomentum { get; set; } = 50;
        public int HandCapacity { get; set; } = 3;
        public int RequiredStableTicks { get; set; } = 3;
        public float NormalScale { get; set; } = 1f;
        public float DilatedScale { get; set; } = 0.2f;
        public int PreActionBudgetTicks { get; set; } = 240;
        public int ExecutionBudgetTicks { get; set; } = 300;
        public int IngressBudgetTicks { get; set; } = 300;
        public int StoryTransferBudgetTicks { get; set; } = 60;
        public string SimultaneousOutcomePolicy { get; set; } = "DefeatWinsSameFrame";
        public string StageResetPolicy { get; set; } = "ResetSessionAndDeck";
        public string PlayerSpawnStableId { get; set; } = "tactical.player.spawn";
        public string OpponentSpawnStableId { get; set; } = "tactical.enemy.spawn";
    }
}
