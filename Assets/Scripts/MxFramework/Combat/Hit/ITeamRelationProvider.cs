using MxFramework.Combat.Core;

namespace MxFramework.Combat.Hit
{
    public interface ITeamRelationProvider
    {
        bool AreHostile(CombatEntityId a, CombatEntityId b);

        bool AreFriendly(CombatEntityId a, CombatEntityId b);

        bool IsSameTeam(CombatEntityId a, CombatEntityId b);
    }
}
