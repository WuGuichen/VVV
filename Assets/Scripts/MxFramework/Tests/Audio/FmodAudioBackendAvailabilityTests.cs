using MxFramework.Audio;
using MxFramework.Audio.FMOD;
using NUnit.Framework;

namespace MxFramework.Tests.Audio
{
    public class FmodAudioBackendAvailabilityTests
    {
        [Test]
        public void Initialize_ReturnsExpectedAvailabilityForCompileSymbol()
        {
            var backend = new FmodAudioBackend();

            AudioResult result = backend.Initialize(new EmptyAudioDefinitions());

#if MXFRAMEWORK_FMOD
            Assert.IsTrue(result.Success, result.Message);
            Assert.IsTrue(backend.CaptureSnapshot().Initialized);
#else
            Assert.IsFalse(result.Success);
            Assert.AreEqual(AudioErrorCode.BackendUnavailable, result.ErrorCode);
            Assert.IsFalse(backend.CaptureSnapshot().Initialized);
#endif
        }

        private sealed class EmptyAudioDefinitions : IAudioDefinitionProvider
        {
            public bool TryGetEvent(int eventId, out AudioEventDefinition definition)
            {
                definition = default;
                return false;
            }

            public bool TryGetBus(int busId, out AudioBusDefinition definition)
            {
                definition = default;
                return false;
            }

            public bool TryGetParameter(int eventId, int parameterId, out AudioParameterDefinition definition)
            {
                definition = default;
                return false;
            }
        }
    }
}
