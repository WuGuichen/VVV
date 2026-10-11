namespace MxFramework.Audio
{
    public enum AudioCommandKind
    {
        PlayOneShot = 0,
        StartEvent = 1
    }

    public enum AudioScope
    {
        TransientFrame = 0,
        Entity = 1,
        Scene = 2,
        AppPersistent = 3
    }

    public enum AudioPredictionPolicy
    {
        Immediate = 0,
        ConfirmedOnly = 1,
        SuppressDuringReplay = 2
    }

    /// <summary>Stable presentation command consumed by <see cref="AudioPresentationDrain"/>.</summary>
    /// <remarks>NoAlloc. Parameter storage remains caller-owned through <see cref="AudioPlayRequest"/> until the command is drained.</remarks>
    public readonly struct AudioCommand
    {
        public AudioCommand(
            AudioCommandKind kind,
            in AudioPlayRequest request,
            AudioScope scope = AudioScope.TransientFrame,
            int ownerId = 0,
            int categoryId = 0,
            int dedupeOrdinal = 0,
            AudioPredictionPolicy predictionPolicy = AudioPredictionPolicy.SuppressDuringReplay)
        {
            Kind = kind;
            Request = request;
            Scope = scope;
            OwnerId = ownerId;
            CategoryId = categoryId;
            DedupeOrdinal = dedupeOrdinal;
            PredictionPolicy = predictionPolicy;
        }

        public AudioCommandKind Kind { get; }
        public AudioPlayRequest Request { get; }
        public AudioScope Scope { get; }
        public int OwnerId { get; }
        public int CategoryId { get; }
        public int DedupeOrdinal { get; }
        public AudioPredictionPolicy PredictionPolicy { get; }
    }
}
