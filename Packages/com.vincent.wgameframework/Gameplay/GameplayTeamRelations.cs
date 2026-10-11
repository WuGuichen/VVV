namespace MxFramework.Gameplay
{
    public static class GameplayTeamRelations
    {
        public static GameplayTeamRelation Resolve(int sourceTeamId, int targetTeamId)
        {
            if (sourceTeamId <= 0 || targetTeamId <= 0)
                return GameplayTeamRelation.Neutral;

            return sourceTeamId == targetTeamId
                ? GameplayTeamRelation.SameTeam
                : GameplayTeamRelation.Enemy;
        }

        public static GameplayTeamRelation Resolve(IRuntimeEntity source, IRuntimeEntity target)
        {
            if (source == null || target == null)
                return GameplayTeamRelation.Neutral;

            return Resolve(source.TeamId, target.TeamId);
        }

        public static bool IsSameTeam(int sourceTeamId, int targetTeamId)
        {
            return Resolve(sourceTeamId, targetTeamId) == GameplayTeamRelation.SameTeam;
        }

        public static bool IsEnemy(int sourceTeamId, int targetTeamId)
        {
            return Resolve(sourceTeamId, targetTeamId) == GameplayTeamRelation.Enemy;
        }

        public static bool IsNeutral(int sourceTeamId, int targetTeamId)
        {
            return Resolve(sourceTeamId, targetTeamId) == GameplayTeamRelation.Neutral;
        }
    }
}
