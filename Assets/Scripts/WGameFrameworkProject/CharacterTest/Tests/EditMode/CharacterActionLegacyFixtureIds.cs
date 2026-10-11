using System;
using MxFramework.CharacterAction;

namespace MxFramework.Tests
{
    /// <summary>
    /// Test-fixture-only adapter for scenarios that deliberately exercise labels unrelated to
    /// checked-in CharacterAction content. Runtime/importer code never consults this adapter.
    /// </summary>
    internal static class CharacterActionLegacyFixtureIds
    {
        internal static CharacterActionId From(string fixtureLabel)
        {
            string value = fixtureLabel ?? string.Empty;
            unchecked
            {
                int hash = 17;
                for (int i = 0; i < value.Length; i++)
                    hash = hash * 31 + value[i];
                hash &= 0x3fffffff;
                return new CharacterActionId(1000000000 + (hash == 0 ? 1 : hash));
            }
        }
    }
}
