using System;
using System.Collections.Generic;
using MxFramework.Combat.Core;
using MxFramework.Gameplay;

namespace MxFramework.Combat.GameplayBridge
{
    public sealed class CombatEntityGameplayMap
    {
        private readonly Dictionary<int, GameplayEntityId> _combatToGameplay = new Dictionary<int, GameplayEntityId>();
        private readonly Dictionary<GameplayEntityId, int> _gameplayToCombat = new Dictionary<GameplayEntityId, int>();
        private readonly List<CombatEntityId> _combatOrder = new List<CombatEntityId>();

        public int Count => _combatToGameplay.Count;

        public void Register(CombatEntityId combatId, GameplayEntityId gameplayId)
        {
            if (combatId.IsNone)
            {
                throw new ArgumentException("Combat entity id cannot be none.", nameof(combatId));
            }

            if (!gameplayId.IsValid)
            {
                throw new ArgumentException("Gameplay entity id must be valid.", nameof(gameplayId));
            }

            int combatValue = combatId.Value;
            bool hasCombat = _combatToGameplay.TryGetValue(combatValue, out GameplayEntityId previousGameplayId);
            bool hasGameplay = _gameplayToCombat.TryGetValue(gameplayId, out int previousCombatValue);

            if (hasCombat && previousGameplayId.Equals(gameplayId))
            {
                return;
            }

            if (hasCombat)
            {
                _gameplayToCombat.Remove(previousGameplayId);
            }

            if (hasGameplay && previousCombatValue != combatValue)
            {
                _combatToGameplay.Remove(previousCombatValue);
                RemoveCombatOrder(new CombatEntityId(previousCombatValue));
            }

            _combatToGameplay[combatValue] = gameplayId;
            _gameplayToCombat[gameplayId] = combatValue;

            if (!hasCombat)
            {
                _combatOrder.Add(combatId);
            }
        }

        public bool TryGetCombatId(GameplayEntityId gameplayId, out CombatEntityId combatId)
        {
            if (_gameplayToCombat.TryGetValue(gameplayId, out int combatValue))
            {
                combatId = new CombatEntityId(combatValue);
                return true;
            }

            combatId = default;
            return false;
        }

        public bool TryGetGameplayId(CombatEntityId combatId, out GameplayEntityId gameplayId)
        {
            if (_combatToGameplay.TryGetValue(combatId.Value, out gameplayId))
            {
                return true;
            }

            gameplayId = default;
            return false;
        }

        public bool RemoveCombat(CombatEntityId combatId)
        {
            if (!_combatToGameplay.TryGetValue(combatId.Value, out GameplayEntityId gameplayId))
            {
                return false;
            }

            _combatToGameplay.Remove(combatId.Value);
            _gameplayToCombat.Remove(gameplayId);
            RemoveCombatOrder(combatId);
            return true;
        }

        public bool RemoveGameplay(GameplayEntityId gameplayId)
        {
            if (!_gameplayToCombat.TryGetValue(gameplayId, out int combatValue))
            {
                return false;
            }

            _gameplayToCombat.Remove(gameplayId);
            _combatToGameplay.Remove(combatValue);
            RemoveCombatOrder(new CombatEntityId(combatValue));
            return true;
        }

        public void Clear()
        {
            _combatToGameplay.Clear();
            _gameplayToCombat.Clear();
            _combatOrder.Clear();
        }

        public void CopyCombatIds(List<CombatEntityId> output)
        {
            if (output == null)
            {
                throw new ArgumentNullException(nameof(output));
            }

            for (int i = 0; i < _combatOrder.Count; i++)
            {
                CombatEntityId combatId = _combatOrder[i];
                if (_combatToGameplay.ContainsKey(combatId.Value))
                {
                    output.Add(combatId);
                }
            }
        }

        public CombatEntityId[] CreateCombatIdSnapshot()
        {
            var snapshot = new CombatEntityId[Count];
            int index = 0;
            for (int i = 0; i < _combatOrder.Count; i++)
            {
                CombatEntityId combatId = _combatOrder[i];
                if (_combatToGameplay.ContainsKey(combatId.Value))
                {
                    snapshot[index++] = combatId;
                }
            }

            return snapshot;
        }

        public void CopyGameplayIds(List<GameplayEntityId> output)
        {
            if (output == null)
            {
                throw new ArgumentNullException(nameof(output));
            }

            for (int i = 0; i < _combatOrder.Count; i++)
            {
                CombatEntityId combatId = _combatOrder[i];
                if (_combatToGameplay.TryGetValue(combatId.Value, out GameplayEntityId gameplayId))
                {
                    output.Add(gameplayId);
                }
            }
        }

        public GameplayEntityId[] CreateGameplayIdSnapshot()
        {
            var snapshot = new GameplayEntityId[Count];
            int index = 0;
            for (int i = 0; i < _combatOrder.Count; i++)
            {
                CombatEntityId combatId = _combatOrder[i];
                if (_combatToGameplay.TryGetValue(combatId.Value, out GameplayEntityId gameplayId))
                {
                    snapshot[index++] = gameplayId;
                }
            }

            return snapshot;
        }

        private void RemoveCombatOrder(CombatEntityId combatId)
        {
            for (int i = 0; i < _combatOrder.Count; i++)
            {
                if (_combatOrder[i].Equals(combatId))
                {
                    _combatOrder.RemoveAt(i);
                    return;
                }
            }
        }
    }
}
