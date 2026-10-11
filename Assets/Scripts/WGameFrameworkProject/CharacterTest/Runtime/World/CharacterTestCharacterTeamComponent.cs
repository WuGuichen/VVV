namespace MxFramework.Game.CharacterTest
{
    public readonly struct CharacterTestCharacterTeamComponent
    {
        public CharacterTestCharacterTeamComponent(int teamId, int defaultTeamId)
        {
            TeamId = teamId;
            DefaultTeamId = defaultTeamId;
        }

        public int TeamId { get; }
        public int DefaultTeamId { get; }
    }
}
