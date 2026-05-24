using MxFramework.Story.Runtime;
using UnityEngine;

namespace MxFramework.Story.Unity
{
    [DisallowMultipleComponent]
    public sealed class StoryTriggerZoneAdapter : StoryUnityCommandAdapter
    {
        [SerializeField] private int _triggerId = 1;
        [SerializeField] private int _param0;
        [SerializeField] private int _param1;
        [SerializeField] private int _targetId;
        [SerializeField] private bool _raiseOnTriggerEnter = true;
        [SerializeField] private LayerMask _acceptedLayers = ~0;

        public int TriggerId
        {
            get => _triggerId;
            set => _triggerId = value;
        }

        public int Param0
        {
            get => _param0;
            set => _param0 = value;
        }

        public int Param1
        {
            get => _param1;
            set => _param1 = value;
        }

        public int TargetId
        {
            get => _targetId;
            set => _targetId = value;
        }

        public bool RaiseOnTriggerEnter
        {
            get => _raiseOnTriggerEnter;
            set => _raiseOnTriggerEnter = value;
        }

        protected override int DefaultSourceId => StoryRuntimeCommandSources.UnityAdapter;

        public StoryUnityCommandResult RaiseTrigger()
        {
            return RaiseTrigger(_triggerId, _param0, _param1, _targetId);
        }

        public StoryUnityCommandResult RaiseTrigger(int triggerId, int param0 = 0, int param1 = 0, int targetId = 0)
        {
            return Enqueue(StoryRuntimeCommandFactory.RaiseTrigger(
                ResolveFrame(),
                SourceId,
                triggerId,
                param0,
                param1,
                targetId,
                TraceId));
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!_raiseOnTriggerEnter || other == null || !IsAcceptedLayer(other.gameObject.layer))
            {
                return;
            }

            RaiseTrigger();
        }

        private bool IsAcceptedLayer(int layer)
        {
            return (_acceptedLayers.value & (1 << layer)) != 0;
        }
    }
}
