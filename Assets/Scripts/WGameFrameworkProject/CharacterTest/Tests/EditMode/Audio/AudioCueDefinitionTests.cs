using MxFramework.Audio;
using NUnit.Framework;

namespace MxFramework.Tests.Audio
{
    public class AudioCueDefinitionTests
    {
        [Test]
        public void AudioCueDefinition_Defaults_DoNotExposeNullArrays()
        {
            var cue = new AudioCueDefinition(500101, "Sword Slash", 510101);

            Assert.AreEqual(500101, cue.Id);
            Assert.AreEqual(510101, cue.EventId);
            Assert.AreEqual(AudioCuePlayMode.OneShot, cue.PlayMode);
            Assert.AreEqual(AudioCueMissingPolicy.Mute, cue.MissingPolicy);
            Assert.IsNotNull(cue.DefaultParameters);
            Assert.IsNotNull(cue.Labels);
            Assert.AreEqual(0, cue.DefaultParameters.Count);
            Assert.AreEqual(0, cue.Labels.Count);
        }

        [Test]
        public void AudioCueDefinition_WithDefaults_PreservesEventAndParameterBridge()
        {
            var cue = new AudioCueDefinition(
                500102,
                "Heavy Hit",
                510101,
                AudioCuePlayMode.StartEvent,
                new[] { new AudioParameterValue(1, 0.75f) },
                AudioCueMissingPolicy.FailRequest,
                new[] { "combat", "hit" });

            Assert.AreEqual(510101, cue.EventId);
            Assert.AreEqual(AudioCuePlayMode.StartEvent, cue.PlayMode);
            Assert.AreEqual(AudioPlayMode.StartEvent, cue.ToAudioPlayMode());
            Assert.AreEqual(AudioCueMissingPolicy.FailRequest, cue.MissingPolicy);
            Assert.AreEqual(1, cue.DefaultParameters.Count);
            Assert.AreEqual(1, cue.DefaultParameters[0].ParameterId);
            Assert.AreEqual(0.75f, cue.DefaultParameters[0].Value);
            Assert.AreEqual("combat", cue.Labels[0]);
        }

        [Test]
        public void AudioCueDefinition_Constructor_DefensivelyCopiesCollections()
        {
            var parameters = new[] { new AudioParameterValue(1, 0.75f) };
            var labels = new[] { "combat" };
            var cue = new AudioCueDefinition(
                500102,
                "Heavy Hit",
                510101,
                defaultParameters: parameters,
                labels: labels);

            parameters[0] = new AudioParameterValue(2, 0.25f);
            labels[0] = "mutated";

            Assert.AreEqual(1, cue.DefaultParameters[0].ParameterId);
            Assert.AreEqual("combat", cue.Labels[0]);
        }
    }
}
