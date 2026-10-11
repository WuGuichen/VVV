using MxFramework.Runtime;

namespace MxFramework.Story.Unity
{
    public interface IStoryUnityFrameProvider
    {
        RuntimeFrame CurrentFrame { get; }
    }

    public sealed class StoryUnityManualFrameProvider : IStoryUnityFrameProvider
    {
        public StoryUnityManualFrameProvider()
            : this(RuntimeFrame.Zero)
        {
        }

        public StoryUnityManualFrameProvider(RuntimeFrame currentFrame)
        {
            CurrentFrame = currentFrame;
        }

        public RuntimeFrame CurrentFrame { get; private set; }

        public void SetFrame(RuntimeFrame frame)
        {
            CurrentFrame = frame;
        }

        public void SetFrame(long frameIndex)
        {
            CurrentFrame = new RuntimeFrame(frameIndex);
        }
    }
}
