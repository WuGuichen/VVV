using MxFramework.Runtime;
using MxFramework.Story.Runtime;
using UnityEngine;

namespace MxFramework.Story.Unity
{
    public abstract class StoryUnityCommandAdapter : MonoBehaviour
    {
        [SerializeField] private int _sourceId;
        [SerializeField] private string _traceId = string.Empty;

        private RuntimeCommandBuffer _commandBuffer;
        private IStoryUnityFrameProvider _frameProvider;

        public int SourceId
        {
            get => _sourceId > 0 ? _sourceId : DefaultSourceId;
            set => _sourceId = value;
        }

        public string TraceId
        {
            get => _traceId ?? string.Empty;
            set => _traceId = value ?? string.Empty;
        }

        public RuntimeCommandBuffer CommandBuffer => _commandBuffer;
        public IStoryUnityFrameProvider FrameProvider => _frameProvider;
        public StoryUnityCommandResult LastResult { get; private set; }

        protected virtual int DefaultSourceId => StoryRuntimeCommandSources.UnityAdapter;

        public void Bind(RuntimeCommandBuffer commandBuffer, IStoryUnityFrameProvider frameProvider = null)
        {
            _commandBuffer = commandBuffer;
            _frameProvider = frameProvider;
        }

        public void Bind(StoryRuntimeModule module, IStoryUnityFrameProvider frameProvider = null)
        {
            Bind(module != null ? module.CommandBuffer : null, frameProvider);
        }

        public void Unbind()
        {
            _commandBuffer = null;
            _frameProvider = null;
        }

        protected RuntimeFrame ResolveFrame()
        {
            if (_frameProvider != null)
            {
                return _frameProvider.CurrentFrame;
            }

            return _commandBuffer != null ? _commandBuffer.CurrentFrame : RuntimeFrame.Zero;
        }

        protected StoryUnityCommandResult Enqueue(RuntimeCommand command)
        {
            if (_commandBuffer == null)
            {
                LastResult = StoryUnityCommandResult.NotConfigured(GetType().Name);
                return LastResult;
            }

            LastResult = StoryUnityCommandResult.FromValidation(_commandBuffer.Enqueue(command));
            return LastResult;
        }
    }
}
