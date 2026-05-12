using System.Collections.Generic;

namespace MxFramework.Gameplay
{
    public sealed class GameplayComponentAbilityRequestStore
    {
        private readonly Dictionary<int, GameplayComponentAbilityRequest> _requests =
            new Dictionary<int, GameplayComponentAbilityRequest>();
        private readonly Dictionary<int, int> _generations = new Dictionary<int, int>();
        private readonly Queue<int> _freeIndices = new Queue<int>();
        private int _nextIndex = 1;

        public int Count => _requests.Count;

        public GameplayComponentAbilityRequestHandle Add(GameplayComponentAbilityRequest request)
        {
            if (request == null)
                throw new System.ArgumentNullException(nameof(request));

            int index = _freeIndices.Count > 0 ? _freeIndices.Dequeue() : _nextIndex++;
            int generation = _generations.TryGetValue(index, out int currentGeneration) ? currentGeneration + 1 : 1;

            _generations[index] = generation;
            _requests[index] = request;
            return new GameplayComponentAbilityRequestHandle(index, generation);
        }

        public bool TryGet(GameplayComponentAbilityRequestHandle handle, out GameplayComponentAbilityRequest request)
        {
            if (!handle.IsValid ||
                !_generations.TryGetValue(handle.Index, out int generation) ||
                generation != handle.Generation ||
                !_requests.TryGetValue(handle.Index, out request))
            {
                request = null;
                return false;
            }

            return true;
        }

        public bool Remove(GameplayComponentAbilityRequestHandle handle)
        {
            if (!handle.IsValid ||
                !_generations.TryGetValue(handle.Index, out int generation) ||
                generation != handle.Generation ||
                !_requests.Remove(handle.Index))
            {
                return false;
            }

            _freeIndices.Enqueue(handle.Index);
            return true;
        }

        public void Clear()
        {
            _requests.Clear();
            _freeIndices.Clear();
        }
    }
}
