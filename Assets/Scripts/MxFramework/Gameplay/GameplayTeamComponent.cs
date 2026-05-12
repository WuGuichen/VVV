using System;

namespace MxFramework.Gameplay
{
    public readonly struct GameplayTeamComponent : IGameplayComponent, IEquatable<GameplayTeamComponent>
    {
        public GameplayTeamComponent(int teamId)
        {
            TeamId = teamId;
        }

        public int TeamId { get; }
        public bool IsNeutral => TeamId <= 0;

        public GameplayTeamRelation RelationTo(GameplayTeamComponent other)
        {
            return GameplayTeamRelations.Resolve(TeamId, other.TeamId);
        }

        public bool Equals(GameplayTeamComponent other)
        {
            return TeamId == other.TeamId;
        }

        public override bool Equals(object obj)
        {
            return obj is GameplayTeamComponent other && Equals(other);
        }

        public override int GetHashCode()
        {
            return TeamId;
        }
    }
}
