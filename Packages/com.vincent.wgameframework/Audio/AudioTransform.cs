namespace MxFramework.Audio
{
    public readonly struct AudioTransform
    {
        public AudioTransform(
            float x,
            float y,
            float z,
            float forwardX = 0f,
            float forwardY = 0f,
            float forwardZ = 1f,
            float upX = 0f,
            float upY = 1f,
            float upZ = 0f,
            float velocityX = 0f,
            float velocityY = 0f,
            float velocityZ = 0f)
        {
            X = x;
            Y = y;
            Z = z;
            ForwardX = forwardX;
            ForwardY = forwardY;
            ForwardZ = forwardZ;
            UpX = upX;
            UpY = upY;
            UpZ = upZ;
            VelocityX = velocityX;
            VelocityY = velocityY;
            VelocityZ = velocityZ;
        }

        public float X { get; }
        public float Y { get; }
        public float Z { get; }
        public float ForwardX { get; }
        public float ForwardY { get; }
        public float ForwardZ { get; }
        public float UpX { get; }
        public float UpY { get; }
        public float UpZ { get; }
        public float VelocityX { get; }
        public float VelocityY { get; }
        public float VelocityZ { get; }

        public static AudioTransform Origin => new AudioTransform(0f, 0f, 0f);
    }
}
