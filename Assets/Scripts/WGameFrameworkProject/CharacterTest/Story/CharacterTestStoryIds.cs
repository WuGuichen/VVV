namespace MxFramework.Game.CharacterTest
{
    public static class CharacterTestStoryIds
    {
        public const string BootstrapSourcePath = "character-test/bootstrap";

        public const int BootstrapGraph = 450001;

        public static class Beats
        {
            public const int Entry = 450101;
            public const int LevelStartRegion = 450102;
            public const int TrainingComplete = 450103;
            public const int TacticalBattleVictory = 450104;
            public const int TacticalBattleDefeat = 450105;
        }

        public static class Steps
        {
            public const int OpenLoadingUi = 450201;
            public const int LoadBaseResources = 450202;
            public const int OpenStartupUi = 450203;
            public const int WelcomeLine = 450204;
            public const int LevelStartRegionLine = 450205;
            public const int TrainingCompleteLine = 450206;
            public const int TacticalBattleVictory = 450207;
            public const int TacticalBattleDefeat = 450208;
        }

        public static class Text
        {
            public const int WelcomeLine = 450301;
            public const int LevelStartRegionLine = 450302;
            public const int TrainingCompleteLine = 450303;
        }

        public static class Actions
        {
            public const int OpenLoadingUi = 451001;
            public const int LoadBaseResources = 451002;
            public const int OpenStartupUi = 451003;
            public const int TacticalBattleVictory = 451004;
            public const int TacticalBattleDefeat = 451005;
        }

        public static class Triggers
        {
            public const int LevelStartRegion = 450401;
            public const int TrainingComplete = 450402;
            public const int TacticalBattleVictory = 450403;
            public const int TacticalBattleDefeat = 450404;
        }
    }
}
