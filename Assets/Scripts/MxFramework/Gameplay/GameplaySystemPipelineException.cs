using System;

namespace MxFramework.Gameplay
{
    public sealed class GameplaySystemPipelineException : Exception
    {
        public GameplaySystemPipelineException(string systemId, GameplaySystemPhase phase, Exception innerException)
            : base($"Gameplay system '{systemId}' failed in phase '{phase}'.", innerException)
        {
            SystemId = systemId ?? string.Empty;
            Phase = phase;
        }

        public string SystemId { get; }
        public GameplaySystemPhase Phase { get; }
    }
}
