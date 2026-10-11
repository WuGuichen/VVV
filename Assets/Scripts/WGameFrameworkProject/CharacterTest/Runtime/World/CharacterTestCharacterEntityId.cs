namespace MxFramework.Game.CharacterTest
{
    public readonly struct CharacterTestCharacterEntityId
    {
        public CharacterTestCharacterEntityId(int value)
        {
            Value = value;
        }

        public int Value { get; }
        public bool IsValid => Value > 0;

        public override string ToString()
        {
            return Value.ToString();
        }
    }
}
