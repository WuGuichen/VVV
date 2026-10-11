using MxFramework.Game.CharacterTest.TacticalCard;

namespace MxFramework.Game.CharacterTest
{
    internal struct CharacterTestInteractionEffectsComponent
    {
        internal TacticalInteractionEffectAuthority Authority;
        internal CharacterTestInteractionEffectsComponent(bool initialize) { Authority = new TacticalInteractionEffectAuthority(); }
    }
    internal sealed partial class CharacterTestComponentWorld
    {
        private readonly CharacterTestComponentStore<CharacterTestInteractionEffectsComponent> _interactionEffects = new CharacterTestComponentStore<CharacterTestInteractionEffectsComponent>();
        internal TacticalInteractionEffectAuthority GetInteractionEffects(CharacterTestCharacterEntityId id)
        { return _interactionEffects.TryGet(id, out var component) ? component.Authority : null; }
    }
}
