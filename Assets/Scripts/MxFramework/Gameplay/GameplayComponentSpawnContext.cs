using MxFramework.Runtime;

namespace MxFramework.Gameplay
{
    public readonly struct GameplayComponentSpawnContext
    {
        public GameplayComponentSpawnContext(
            RuntimeFrame frame,
            RuntimeCommand command,
            GameplayComponentSpawnDefinition definition,
            int variantId)
        {
            Frame = frame;
            Command = command;
            Definition = definition;
            VariantId = variantId;
        }

        public RuntimeFrame Frame { get; }
        public RuntimeCommand Command { get; }
        public GameplayComponentSpawnDefinition Definition { get; }
        public int VariantId { get; }
    }
}
