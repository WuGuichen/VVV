using System;
using System.Collections.Generic;

namespace MxFramework.Gameplay
{
    public sealed class GameplayComponentWorldDiagnostics
    {
        private readonly List<GameplayComponentStoreDiagnosticSnapshot> _stores =
            new List<GameplayComponentStoreDiagnosticSnapshot>();

        public GameplayComponentWorldDiagnosticSnapshot BuildSnapshot(GameplayComponentWorld world)
        {
            if (world == null)
                throw new ArgumentNullException(nameof(world));

            _stores.Clear();
            world.Registry.CopyStoreDiagnostics(_stores);

            return new GameplayComponentWorldDiagnosticSnapshot(
                world.CreateEntitySnapshot(),
                _stores,
                world.Events.CreateSnapshot());
        }
    }
}
