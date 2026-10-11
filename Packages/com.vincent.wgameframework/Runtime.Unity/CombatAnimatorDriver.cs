using MxFramework.Combat.Animation;
using MxFramework.Combat.Core;
using UnityEngine;

namespace MxFramework.Runtime.Unity
{
    [AddComponentMenu("MxFramework/Combat/Combat Animator Driver")]
    public sealed class CombatAnimatorDriver : MonoBehaviour, ICombatAnimatorDriver
    {
        [SerializeField, Min(0)] private int _entityId;
        [SerializeField] private Animator _animator;
        [SerializeField] private CombatAnimatorMapping _mapping;

        public CombatEntityId EntityId
        {
            get => new CombatEntityId(_entityId);
            set => _entityId = value.Value;
        }

        public Animator Animator
        {
            get => _animator;
            set => _animator = value;
        }

        public CombatAnimatorMapping Mapping
        {
            get => _mapping;
            set => _mapping = value;
        }

        public bool TryGetActionMapping(int actionId, out ActionAnimMapping mapping)
        {
            if (_mapping == null)
            {
                mapping = null;
                return false;
            }

            return _mapping.TryGetMapping(actionId, out mapping);
        }

        public void OnActionStarted(ActionStartedEvent evt)
        {
            if (_animator == null || !TryGetActionMapping(evt.ActionId, out ActionAnimMapping actionMapping))
            {
                return;
            }

            string stateName = actionMapping.AnimatorStateName;
            if (string.IsNullOrWhiteSpace(stateName))
            {
                return;
            }

            _animator.CrossFade(stateName, Mathf.Max(0f, actionMapping.CrossFadeDuration));
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

        private void Reset()
        {
            _animator = GetComponentInChildren<Animator>();
        }
    }
}
