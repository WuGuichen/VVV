namespace MxFramework.Gameplay
{
    public readonly struct GameplayComponentAbilityRuleResult
    {
        private GameplayComponentAbilityRuleResult(
            bool success,
            GameplayComponentAbilityFailureCode failureCode,
            string reason,
            int detailId,
            long remainingFrames)
        {
            Success = success;
            FailureCode = failureCode;
            Reason = reason ?? string.Empty;
            DetailId = detailId;
            RemainingFrames = remainingFrames;
        }

        public bool Success { get; }
        public GameplayComponentAbilityFailureCode FailureCode { get; }
        public string Reason { get; }
        public int DetailId { get; }
        public long RemainingFrames { get; }

        public static GameplayComponentAbilityRuleResult Succeeded()
        {
            return new GameplayComponentAbilityRuleResult(
                true,
                GameplayComponentAbilityFailureCode.None,
                string.Empty,
                0,
                0L);
        }

        public static GameplayComponentAbilityRuleResult Failed(
            GameplayComponentAbilityFailureCode failureCode,
            string reason,
            int detailId = 0,
            long remainingFrames = 0L)
        {
            return new GameplayComponentAbilityRuleResult(
                false,
                failureCode,
                reason,
                detailId,
                remainingFrames);
        }
    }
}
