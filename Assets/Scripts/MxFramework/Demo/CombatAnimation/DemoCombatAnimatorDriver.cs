using MxFramework.Combat.Animation;
using MxFramework.Combat.Core;
using MxFramework.Runtime.Unity;
using UnityEngine;

namespace MxFramework.Demo.CombatAnimation
{
    [DisallowMultipleComponent]
    [AddComponentMenu("MxFramework/Demo/Combat Animation Demo Animator Driver")]
    public sealed class DemoCombatAnimatorDriver : MonoBehaviour, ICombatAnimatorDriver
    {
        [SerializeField, Min(0)] private int _entityId;
        [SerializeField] private Animator _animator;
        [SerializeField] private CombatAnimatorMapping _mapping;

        public CombatEntityId EntityId
        {
            get => new CombatEntityId(_entityId);
            set => _entityId = value.Value;
        }

        public CombatAnimatorMapping Mapping
        {
            get => _mapping;
            set => _mapping = value;
        }

        public void OnActionStarted(ActionStartedEvent evt)
        {
            string stateName = ResolveStateName(evt.ActionId);
            if (_animator != null && !string.IsNullOrWhiteSpace(stateName))
            {
                _animator.CrossFade(stateName, 0.15f);
            }

            Debug.Log($"[CombatAnimatorDriver] Entity {evt.EntityId.Value}: ActionStarted actionId={evt.ActionId} -> state={stateName}", this);
        }

        public void OnActionPhaseChanged(ActionPhaseChangedEvent evt)
        {
        }

        public void OnActionFinished(ActionFinishedEvent evt)
        {
        }

        public void OnActionCanceled(ActionCanceledEvent evt)
        {
        }

        public void OnActionCancelRejected(ActionCancelRejectedEvent evt)
        {
        }

        private string ResolveStateName(int actionId)
        {
            if (_mapping != null && _mapping.TryGetMapping(actionId, out ActionAnimMapping mapping)
                && !string.IsNullOrWhiteSpace(mapping.AnimatorStateName))
            {
                return mapping.AnimatorStateName;
            }

            switch (actionId)
            {
                case CombatAnimationDemoIds.LightAttackActionId:
                    return "LightAttack";
                case CombatAnimationDemoIds.HeavyAttackActionId:
                    return "HeavyAttack";
                case CombatAnimationDemoIds.DodgeRollActionId:
                    return "DodgeRoll";
                default:
                    return "Unknown";
            }
        }

        private void Reset()
        {
            _animator = GetComponentInChildren<Animator>();
        }
    }
}
