namespace MxFramework.Runtime
{
    public sealed class RuntimeClock
    {
        public RuntimeClock()
            : this(RuntimeFrame.Zero)
        {
        }

        public RuntimeClock(RuntimeFrame startFrame)
        {
            CurrentFrame = startFrame;
        }

        public RuntimeFrame CurrentFrame { get; private set; }

        public RuntimeFrame Step()
        {
            CurrentFrame = CurrentFrame.Next();
            return CurrentFrame;
        }

        public void Reset(RuntimeFrame frame)
        {
            CurrentFrame = frame;
        }
    }
}
