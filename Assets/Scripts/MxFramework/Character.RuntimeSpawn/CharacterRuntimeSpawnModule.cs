using System;
using System.Collections.Generic;
using MxFramework.CharacterApplication;
using MxFramework.Runtime;

namespace MxFramework.CharacterRuntimeSpawn
{
    public sealed class CharacterRuntimeSpawnModule : RuntimeModule
    {
        private readonly CharacterImportedPackage _package;
        private readonly Queue<CharacterSpawnRequest> _requests = new Queue<CharacterSpawnRequest>();
        private readonly List<CharacterRuntimeSpawnResult> _results = new List<CharacterRuntimeSpawnResult>();

        public CharacterRuntimeSpawnModule(CharacterImportedPackage package, string moduleId = "character.runtimeSpawn", int priority = 0)
            : base(moduleId, RuntimeTickStage.Simulation, priority)
        {
            _package = package ?? throw new ArgumentNullException(nameof(package));
        }

        public IReadOnlyList<CharacterRuntimeSpawnResult> Results => _results;
        public CharacterRuntimeSpawnResult LastResult { get; private set; }

        public void Enqueue(CharacterSpawnRequest request)
        {
            _requests.Enqueue(request);
        }

        public override void Tick(RuntimeTickContext context)
        {
            while (_requests.Count > 0)
            {
                CharacterSpawnRequest request = _requests.Dequeue();
                LastResult = CharacterRuntimeSpawnResolver.Resolve(_package, request);
                _results.Add(LastResult);
            }
        }
    }
}
