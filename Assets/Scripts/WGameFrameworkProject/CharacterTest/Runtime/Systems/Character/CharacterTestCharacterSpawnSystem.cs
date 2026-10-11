using System.Collections.Generic;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    internal sealed class CharacterTestCharacterSpawnSystem
    {
        private readonly Queue<CharacterTestCharacterSpawnRequest> _spawnRequests =
            new Queue<CharacterTestCharacterSpawnRequest>();
        private readonly CharacterTestCharacterRuntimeWorld _world;
        private readonly CharacterTestCharacterDefinitionRegistry _definitions;
        private readonly CharacterTestCharacterUnityViewBridge _viewBridge;
        private readonly IRuntimeLogger _logger;
        private readonly bool _createViews;
        private int _lastProcessedRequestCount;
        private int _lastSpawnedCount;
        private int _lastFailedCount;
        private int _totalSpawnedCount;
        private int _totalFailedCount;

        public CharacterTestCharacterSpawnSystem(
            CharacterTestCharacterRuntimeWorld world,
            CharacterTestCharacterDefinitionRegistry definitions,
            CharacterTestCharacterUnityViewBridge viewBridge,
            IRuntimeLogger logger,
            bool createViews = true)
        {
            _world = world;
            _definitions = definitions;
            _viewBridge = viewBridge;
            _logger = logger ?? NullRuntimeLogger.Instance;
            _createViews = createViews;
        }

        public int PendingRequestCount => _spawnRequests.Count;

        public int LastProcessedRequestCount => _lastProcessedRequestCount;

        public int LastSpawnedCount => _lastSpawnedCount;

        public int LastFailedCount => _lastFailedCount;

        public int TotalSpawnedCount => _totalSpawnedCount;

        public int TotalFailedCount => _totalFailedCount;

        public bool Enqueue(CharacterTestCharacterSpawnRequest request)
        {
            if (!_definitions.TryGet(request.CharacterId, out _))
            {
                _logger.Warning("CharacterSpawn", "Character definition was not found. characterId=" + request.CharacterId);
                return false;
            }

            _spawnRequests.Enqueue(request);
            _logger.Info(
                "CharacterSpawn",
                "Character spawn request enqueued. characterId=" + request.CharacterId
                + " teamId=" + request.TeamId
                + " position=(" + request.X + ", " + request.Y + ", " + request.Z + ")"
                + " controlOwner=" + request.ControlOwner);
            return true;
        }

        /// <remarks>NoAlloc on the steady-state path after registration/initialization; invalid-call and exception paths may allocate diagnostics.</remarks>
        public void Tick()
        {
            _lastProcessedRequestCount = 0;
            _lastSpawnedCount = 0;
            _lastFailedCount = 0;
            while (_spawnRequests.Count > 0)
            {
                CharacterTestCharacterSpawnRequest request = _spawnRequests.Dequeue();
                _lastProcessedRequestCount++;
                if (!_definitions.TryGet(request.CharacterId, out CharacterTestCharacterDefinition definition))
                {
                    _logger.Warning("CharacterSpawn", "Character definition was not found. characterId=" + request.CharacterId);
                    _lastFailedCount++;
                    _totalFailedCount++;
                    continue;
                }

                CharacterTestCharacterEntity entity = _world.Spawn(request, definition);
                if (_createViews && !_viewBridge.TryCreateView(entity))
                {
                    _world.Remove(entity.Id);
                    _lastFailedCount++;
                    _totalFailedCount++;
                    continue;
                }

                _lastSpawnedCount++;
                _totalSpawnedCount++;
                _logger.Info(
                    "CharacterSpawn",
                    "Character entity spawned. entityId=" + entity.Id
                    + " characterId=" + entity.Identity.CharacterId
                    + " displayName=" + entity.Identity.DisplayName
                    + " teamId=" + entity.Team.TeamId
                    + " defaultTeamId=" + entity.Team.DefaultTeamId
                    + " key=" + entity.Identity.PrefabKey
                    + " position=(" + entity.Transform.X + ", " + entity.Transform.Y + ", " + entity.Transform.Z + ")"
                    + " controlOwner=" + entity.Control.Owner);
            }
        }

        public void Clear()
        {
            _spawnRequests.Clear();
            _lastProcessedRequestCount = 0;
            _lastSpawnedCount = 0;
            _lastFailedCount = 0;
            _totalSpawnedCount = 0;
            _totalFailedCount = 0;
        }
    }
}
