using MxFramework.Audio;
using MxFramework.Audio.Config;
using MxFramework.Audio.Physical;
using MxFramework.Game.CharacterTest;
using NUnit.Framework;

namespace MxFramework.Tests.Audio
{
    public sealed class AudioRuntimeCatalogJsonTests
    {
        [Test]
        public void TryLoad_ResolvesCueByStringAndInteger_WithSharedEvent()
        {
            const string json = @"{
  'format':'mx.audioRuntimeCatalog.v1',
  'manifestHash':'manifest-a',
  'snapshotHash':'snapshot-a',
  'buses':[{'id':500001,'name':'Master','fmodBusPath':'bus:/'}],
  'events':[{'id':500101,'stableId':'event.shared','name':'Shared','fmodEventPath':'event:/Shared','busId':500001,'is3D':true,'parameters':[{'id':500251,'name':'Impact','kind':'Continuous','maxValue':1.0}]}],
  'cues':[
    {'id':500101,'audioCueId':'sfx.a','eventId':500101,'playMode':'OneShot'},
    {'id':500102,'audioCueId':'sfx.b','eventId':500101,'playMode':'OneShot'}
  ]
}";

            Assert.IsTrue(AudioRuntimeCatalogJson.TryLoad(json, out AudioDefinitionCatalog catalog, out string error), error);
            Assert.AreEqual("manifest-a", catalog.ManifestHash);
            Assert.IsTrue(catalog.TryGetCue("sfx.a", out AudioCueDefinition first));
            Assert.IsTrue(catalog.TryGetCue(500102, out AudioCueDefinition second));
            Assert.AreEqual(first.EventId, second.EventId);
            Assert.IsTrue(catalog.TryGetParameter(first.EventId, "Impact", out AudioParameterDefinition parameter));
            Assert.AreEqual(500251, parameter.Id);

            var physicalCatalog = new PhysicalAudioDefinitionCatalog(
                System.Array.Empty<AcousticMaterialProfileDefinition>(),
                System.Array.Empty<PhysicalAudioFamilyDefinition>(),
                System.Array.Empty<PhysicalAudioBasisDefinition>(),
                System.Array.Empty<PhysicalAudioResponseProfileDefinition>(),
                "physical-a",
                "audio-a");
            CharacterTestAudioCueCatalog adapted = CharacterTestAudioCueCatalog.FromRuntimeCatalog(
                catalog,
                physicalCatalog);
            Assert.IsTrue(adapted.TryGetCue("sfx.b", out AudioCueDefinition adaptedCue));
            Assert.AreEqual(500101, adaptedCue.EventId);
        }

        [Test]
        public void TryLoad_InvalidCatalogFailsAsAWhole()
        {
            const string json = "{'format':'mx.audioRuntimeCatalog.v1','buses':[],'events':[],'cues':[{'id':500101,'audioCueId':'sfx.missing','eventId':599999}]}";
            Assert.IsFalse(AudioRuntimeCatalogJson.TryLoad(json, out AudioDefinitionCatalog catalog, out string error));
            Assert.IsNull(catalog);
            StringAssert.Contains("missing event", error.ToLowerInvariant());
        }

        [Test]
        public void PhysicalLinker_PrecompilesNumericBindingAndAllowsBackendNativeParameters()
        {
            AudioDefinitionCatalog audioCatalog = CreateLinkAudioCatalog();
            PhysicalAudioDefinitionCatalog physicalCatalog = CreateLinkPhysicalCatalog(
                cueId: 500101,
                parameterId: CharacterTestAudioCueCatalog.PhysicalAudioVolumeParameterId);

            bool success = CharacterTestAudioCueCatalog.TryFromRuntimeCatalog(
                audioCatalog,
                physicalCatalog,
                out CharacterTestAudioCueCatalog catalog,
                out string error);

            Assert.IsTrue(success, error);
            Assert.IsTrue(catalog.TryGetPhysicalAudioBinding(
                500101,
                out CharacterTestPhysicalAudioPlaybackBinding binding));
            Assert.AreEqual(500201, binding.EventId);
            Assert.IsTrue(binding.Is3D);
            Assert.AreEqual("sfx.physical.link", binding.CueName);
        }

        [Test]
        public void PhysicalLinker_RejectsMissingCueAsCompletePairFailure()
        {
            bool success = CharacterTestAudioCueCatalog.TryFromRuntimeCatalog(
                CreateLinkAudioCatalog(),
                CreateLinkPhysicalCatalog(cueId: 599999, parameterId: 0),
                out CharacterTestAudioCueCatalog catalog,
                out string error);

            Assert.IsFalse(success);
            Assert.IsNull(catalog);
            StringAssert.Contains("missing runtime Cue", error);
        }

        [Test]
        public void PhysicalLinker_RejectsMissingNonNativeFmodParameter()
        {
            bool success = CharacterTestAudioCueCatalog.TryFromRuntimeCatalog(
                CreateLinkAudioCatalog(),
                CreateLinkPhysicalCatalog(
                    cueId: 500101,
                    parameterId: CharacterTestAudioCueCatalog.PhysicalAudioLowPassParameterId),
                out CharacterTestAudioCueCatalog catalog,
                out string error);

            Assert.IsFalse(success);
            Assert.IsNull(catalog);
            StringAssert.Contains("missing required FMOD parameter", error);
        }

        [Test]
        public void PhysicalLinker_RejectsResponseBeyondIntentCapacityAsCompletePairFailure()
        {
            bool success = CharacterTestAudioCueCatalog.TryFromRuntimeCatalog(
                CreateLinkAudioCatalog(),
                CreateLinkPhysicalCatalog(
                    cueId: 500101,
                    parameterId: CharacterTestAudioCueCatalog.PhysicalAudioVolumeParameterId,
                    responseCurveCount: CharacterTestAudioIntent.MaxParameterCount + 1),
                out CharacterTestAudioCueCatalog catalog,
                out string error);

            Assert.IsFalse(success);
            Assert.IsNull(catalog);
            StringAssert.Contains("requires 6 response parameters", error);
            StringAssert.Contains("intent capacity 5", error);
            StringAssert.Contains("fall back as a whole", error);
        }

        [Test]
        public void CatalogStatus_DistinguishesGeneratedLinkedFromLegacyFallback()
        {
            CharacterTestAudioCatalogStatus legacy = CharacterTestAudioCatalogStatus.From(
                CharacterTestAudioCueCatalog.CreateLegacyFallback());
            Assert.AreEqual(CharacterTestAudioCatalogMode.LegacyFallback, legacy.Mode);

            CharacterTestAudioCueCatalog generatedCatalog = CharacterTestAudioCueCatalog.FromRuntimeCatalog(
                CreateLinkAudioCatalog(),
                CreateLinkPhysicalCatalog(
                    cueId: 500101,
                    parameterId: CharacterTestAudioCueCatalog.PhysicalAudioVolumeParameterId));
            CharacterTestAudioCatalogStatus generated = CharacterTestAudioCatalogStatus.From(generatedCatalog);

            Assert.AreEqual(CharacterTestAudioCatalogMode.GeneratedLinked, generated.Mode);
            Assert.AreEqual("audio-hash", generated.AudioManifestHash);
            Assert.AreEqual("physical-hash", generated.PhysicalManifestHash);
            Assert.AreEqual("audio-hash", generated.LinkedAudioCatalogHash);
        }

        private static AudioDefinitionCatalog CreateLinkAudioCatalog()
        {
            return new AudioDefinitionCatalog(
                new[] { new AudioBusDefinition(500001, "physical", "bus:/") },
                new[]
                {
                    new AudioEventDefinition(
                        500201,
                        "physical.link",
                        "event:/MxFramework/Generated/Physical/ObjectImpact/530001",
                        string.Empty,
                        AudioEventKind.Event,
                        500001,
                        is3D: true,
                        isLoop: false,
                        maxDistance: 24f)
                },
                new[]
                {
                    new AudioCueDefinition(
                        500101,
                        "sfx.physical.link",
                        500201,
                        AudioCuePlayMode.OneShot)
                },
                "audio-hash",
                "snapshot-hash");
        }

        private static PhysicalAudioDefinitionCatalog CreateLinkPhysicalCatalog(
            int cueId,
            int parameterId,
            int responseCurveCount = 1)
        {
            PhysicalAudioResponseProfileId responseId = parameterId > 0
                ? new PhysicalAudioResponseProfileId(540001)
                : PhysicalAudioResponseProfileId.None;
            PhysicalAudioResponseProfileDefinition[] responses;
            if (parameterId > 0)
            {
                if (responseCurveCount < 1)
                    responseCurveCount = 1;
                var curves = new PhysicalAudioResponseCurveDefinition[responseCurveCount];
                for (int i = 0; i < curves.Length; i++)
                {
                    curves[i] = new PhysicalAudioResponseCurveDefinition(
                        parameterId + i,
                        PhysicalAudioResponseInput.Intensity,
                        0f,
                        1f,
                        new[]
                        {
                            new PhysicalAudioResponsePoint(0f, 0f),
                            new PhysicalAudioResponsePoint(1f, 1f)
                        });
                }
                responses = new[]
                {
                    new PhysicalAudioResponseProfileDefinition(responseId, curves)
                };
            }
            else
            {
                responses = System.Array.Empty<PhysicalAudioResponseProfileDefinition>();
            }
            var range = new PhysicalAudioRange(0f, 1f, 0.5f);
            return new PhysicalAudioDefinitionCatalog(
                System.Array.Empty<AcousticMaterialProfileDefinition>(),
                new[]
                {
                    new PhysicalAudioFamilyDefinition(
                        new PhysicalAudioFamilyId(520001),
                        PhysicalAudioInteractionKind.ObjectImpact,
                        PhysicalAudioInteractionMode.Impact,
                        PhysicalAudioFamilyId.None,
                        AudioBasisId.None,
                        responseId,
                        priority: 10,
                        cooldownFrames: 0,
                        new PhysicalAudioSelectionWeights(4f, 1f, 1f, 0.5f, 0.5f, 1f, 1f, 0.5f))
                },
                new[]
                {
                    new PhysicalAudioBasisDefinition(
                        new AudioBasisId(530001),
                        new PhysicalAudioFamilyId(520001),
                        cueId,
                        priority: 10,
                        new PhysicalAudioBodyAnchorDefinition(
                            AcousticMaterialId.None,
                            range,
                            range,
                            AcousticSurfaceKind.Any,
                            AcousticConstructionKind.Any),
                        new PhysicalAudioBodyAnchorDefinition(
                            AcousticMaterialId.None,
                            range,
                            range,
                            AcousticSurfaceKind.Any,
                            AcousticConstructionKind.Any),
                        range,
                        range,
                        new PhysicalAudioRange(0f, 180f, 90f))
                },
                responses,
                "physical-hash",
                "audio-hash");
        }
    }
}
