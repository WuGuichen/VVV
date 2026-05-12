namespace MxFramework.Audio
{
    public readonly struct AudioTransform
    {
        public AudioTransform(float x, float y, float z, float forwardX = 0f, float forwardY = 0f, float forwardZ = 1f)
        {
            X = x;
            Y = y;
            Z = z;
            ForwardX = forwardX;
            ForwardY = forwardY;
            ForwardZ = forwardZ;
        }

        public float X { get; }
        public float Y { get; }
        public float Z { get; }
        public float ForwardX { get; }
        public float ForwardY { get; }
        public float ForwardZ { get; }

        public static AudioTransform Origin => new AudioTransform(0f, 0f, 0f);
    }
}
