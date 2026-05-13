using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace MxFramework.Runtime.Unity
{
    [CreateAssetMenu(menuName = "MxFramework/Combat/Animator Mapping")]
    public sealed class CombatAnimatorMapping : ScriptableObject
    {
        [SerializeField] private AnimationClip _defaultIdle;
        [SerializeField] private AnimationClip _defaultWalk;
        [SerializeField] private List<ActionAnimMapping> _actionMappings = new List<ActionAnimMapping>();

        public AnimationClip DefaultIdle
        {
            get => _defaultIdle;
            set => _defaultIdle = value;
        }

        public AnimationClip DefaultWalk
        {
            get => _defaultWalk;
            set => _defaultWalk = value;
        }

        public List<ActionAnimMapping> ActionMappings => _actionMappings;

        public bool TryGetMapping(int actionId, out ActionAnimMapping mapping)
        {
            if (_actionMappings != null)
            {
                for (int i = 0; i < _actionMappings.Count; i++)
                {
                    ActionAnimMapping candidate = _actionMappings[i];
                    if (candidate != null && candidate.ActionId == actionId)
                    {
                        mapping = candidate;
                        return true;
                    }
                }
            }

            mapping = null;
            return false;
        }
    }

    [Serializable]
    public sealed class ActionAnimMapping
    {
        [SerializeField] private int _actionId;
        [SerializeField] private AnimationClip _clip;
        [SerializeField] private string _animatorStateName;
        [SerializeField] private float _crossFadeDuration = 0.15f;
        [SerializeField] private List<FrameEventBinding> _frameEvents = new List<FrameEventBinding>();

        public int ActionId
        {
            get => _actionId;
            set => _actionId = value;
        }

        public AnimationClip Clip
        {
            get => _clip;
            set => _clip = value;
        }

        public string AnimatorStateName
        {
            get => _animatorStateName;
            set => _animatorStateName = value;
        }

        public float CrossFadeDuration
        {
            get => _crossFadeDuration;
            set => _crossFadeDuration = value;
        }

        public List<FrameEventBinding> FrameEvents => _frameEvents;
    }

    [Serializable]
    public sealed class FrameEventBinding
    {
        [SerializeField] private string _eventName;
        [SerializeField] private int _atFrame;
        [SerializeField] private UnityEvent _unityEvent = new UnityEvent();

        public string EventName
        {
            get => _eventName;
            set => _eventName = value;
        }

        public int AtFrame
        {
            get => _atFrame;
            set => _atFrame = value;
        }

        public UnityEvent UnityEvent => _unityEvent;
    }
}
