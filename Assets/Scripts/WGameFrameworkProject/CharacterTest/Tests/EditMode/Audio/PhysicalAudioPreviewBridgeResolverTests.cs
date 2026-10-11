using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using MxFramework.Audio;
using MxFramework.Audio.FMOD.Editor;
using MxFramework.Audio.Physical;
using MxFramework.Audio.Physical.Config;
using NUnit.Framework;
using UnityEngine;

namespace MxFramework.Tests.Audio
{
    public sealed class PhysicalAudioPreviewBridgeResolverTests
    {
        [Test]
        public void TryResolve_LoadsRequestCatalogAndVerifiesRuntimeIdentity()
        {
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            string relativePath = "Temp/MxFrameworkAuthoring/Audio/physical_preview_catalogs/test-"
                + Guid.NewGuid().ToString("N") + ".json";
            string path = Path.Combine(projectRoot, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            try
            {
                File.WriteAllText(path, CatalogJson);
                var request = CreateRequest(relativePath, ComputeHash(CatalogJson));

                bool success = PhysicalAudioPreviewBridgeResolver.TryResolve(
                    projectRoot,
                    request,
                    out PhysicalAudioPreviewResolution result,
                    out string error);

                Assert.That(success, Is.True, error);
                Assert.That(result.runtimeBasisId, Is.EqualTo(530001));
                Assert.That(result.runtimeCueId, Is.EqualTo(500201));
                Assert.That(result.basisId, Is.EqualTo("audio.basis.test"));
                Assert.That(result.audioCueId, Is.EqualTo("audio.cue.test"));
                Assert.That(result.volume, Is.EqualTo(0.6f).Within(0.0001f));
                Assert.That(result.pitch, Is.EqualTo(1.1f).Within(0.0001f));
                Assert.That(result.contactNormal.x, Is.EqualTo(0.2672612f).Within(0.0001f));
                Assert.That(result.contactNormal.y, Is.EqualTo(0.5345225f).Within(0.0001f));
                Assert.That(result.contactNormal.z, Is.EqualTo(-0.8017837f).Within(0.0001f));
                Assert.That(result.eventParameters, Is.Empty);
                Assert.That(result.parameters, Has.Count.EqualTo(2));
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        [Test]
        public void TryResolve_RejectsCatalogOutsideTempShelf()
        {
            var request = CreateRequest("Assets/Config/MxFramework/Audio/audio_physical_manifest.json", "sha256:ignored");

            bool success = PhysicalAudioPreviewBridgeResolver.TryResolve(
                Path.GetDirectoryName(Application.dataPath),
                request,
                out _,
                out string error);

            Assert.That(success, Is.False);
            Assert.That(error, Does.Contain("outside the Temp shelf"));
        }

        [Test]
        public void LatestFactCapture_PublishesResolvedPlaybackWithoutOwningRuntimeState()
        {
            Assert.That(
                PhysicalAudioRuntimeCatalogJson.TryLoad(
                    CatalogJson,
                    out PhysicalAudioDefinitionCatalog catalog,
                    out string loadError),
                Is.True,
                loadError);
            var impactor = new AcousticBodyProperties(
                new AcousticMaterialId(510001),
                0.5f,
                0.2f);
            var receiver = new AcousticBodyProperties(
                new AcousticMaterialId(510002),
                0.8f,
                0.2f);
            var fact = new PhysicalAudioInteractionFact(
                PhysicalAudioInteractionKind.ObjectImpact,
                PhysicalAudioInteractionMode.Impact,
                PhysicalAudioInteractionPhase.Started,
                impactor,
                receiver,
                6f,
                4f,
                2f,
                0f,
                0f,
                90f,
                0.6f,
                new PhysicalAudioDirection(1f, 2f, -3f),
                new AudioTransform(1f, 2f, 3f),
                77,
                123,
                456);
            var resolverParameters = new AudioParameterValue[catalog.MaxResponseParameterCount];
            var resolver = new PhysicalAudioResolver(catalog);
            Assert.That(
                resolver.TryResolve(fact, resolverParameters.AsSpan(), out PhysicalAudioResolvedPlayback result),
                Is.True);

            Assert.That(
                PhysicalAudioLatestFactCapture.Publish(
                    fact,
                    result,
                    resolverParameters.AsSpan(0, result.ParameterCount)),
                Is.True);
            var capturedParameters = new AudioParameterValue[PhysicalAudioLatestFactCapture.ParameterCapacity];
            Assert.That(
                PhysicalAudioLatestFactCapture.TryRead(
                    capturedParameters.AsSpan(),
                    out PhysicalAudioLatestFactSnapshot captured),
                Is.True);

            Assert.That(captured.Fact.EmitterId, Is.EqualTo(77));
            Assert.That(captured.Fact.Frame, Is.EqualTo(123));
            Assert.That(captured.Fact.Sequence, Is.EqualTo(456));
            Assert.That(captured.Fact.ContactNormal.X, Is.EqualTo(0.2672612f).Within(0.0001f));
            Assert.That(captured.Fact.ContactNormal.Y, Is.EqualTo(0.5345225f).Within(0.0001f));
            Assert.That(captured.Fact.ContactNormal.Z, Is.EqualTo(-0.8017837f).Within(0.0001f));
            Assert.That(captured.Result.BasisId.Value, Is.EqualTo(530001));
            Assert.That(captured.Result.CueId, Is.EqualTo(500201));
            Assert.That(captured.ParameterCount, Is.EqualTo(2));
            Assert.That(capturedParameters[0].ParameterId, Is.EqualTo(500251));
            Assert.That(capturedParameters[0].Value, Is.EqualTo(0.6f).Within(0.0001f));
            Assert.That(capturedParameters[1].ParameterId, Is.EqualTo(500252));
            Assert.That(capturedParameters[1].Value, Is.EqualTo(1.1f).Within(0.0001f));

            PhysicalAudioPreviewFact captureWireFact = FmodAudioPreviewRequestBridge.CreatePhysicalPreviewFact(fact);
            Assert.That(captureWireFact.contactNormal.x, Is.EqualTo(0.2672612f).Within(0.0001f));
            Assert.That(captureWireFact.contactNormal.y, Is.EqualTo(0.5345225f).Within(0.0001f));
            Assert.That(captureWireFact.contactNormal.z, Is.EqualTo(-0.8017837f).Within(0.0001f));

            Assert.That(
                PhysicalAudioLatestFactCapture.Publish(
                    fact,
                    result,
                    resolverParameters.AsSpan(0, result.ParameterCount)),
                Is.True);
            Assert.That(
                PhysicalAudioLatestFactCapture.TryRead(capturedParameters.AsSpan(), out _),
                Is.True);
            _ = GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1024; i++)
            {
                PhysicalAudioLatestFactCapture.Publish(
                    fact,
                    result,
                    resolverParameters.AsSpan(0, result.ParameterCount));
                PhysicalAudioLatestFactCapture.TryRead(capturedParameters.AsSpan(), out _);
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(allocated, Is.EqualTo(0L));
        }

        private static PhysicalAudioPreviewRequest CreateRequest(string relativePath, string hash)
        {
            return new PhysicalAudioPreviewRequest
            {
                runtimeCatalogPath = relativePath,
                runtimeCatalogHash = hash,
                physicalManifestHash = "physical-hash",
                audioCatalogHash = "audio-hash",
                expectedBasisId = "audio.basis.test",
                expectedRuntimeBasisId = 530001,
                expectedAudioCueId = "audio.cue.test",
                expectedRuntimeCueId = 500201,
                fact = new PhysicalAudioPreviewFact
                {
                    interactionKind = "ObjectImpact",
                    interactionMode = "Impact",
                    phase = "Started",
                    impactor = new PhysicalAudioPreviewBody
                    {
                        runtimeMaterialId = 510001,
                        mass = 0.5f,
                        contactArea = 0.2f,
                        surface = "Normal",
                        construction = "Solid"
                    },
                    receiver = new PhysicalAudioPreviewBody
                    {
                        runtimeMaterialId = 510002,
                        mass = 0.8f,
                        contactArea = 0.2f,
                        surface = "Normal",
                        construction = "Solid"
                    },
                    relativeSpeed = 6f,
                    normalSpeed = 4f,
                    tangentialSpeed = 2f,
                    intensity = 0.6f,
                    angleDegrees = 90f,
                    contactNormal = new PhysicalAudioPreviewDirection { x = 1f, y = 2f, z = -3f }
                },
                parameterBindings = new List<PhysicalAudioPreviewParameterBinding>
                {
                    new PhysicalAudioPreviewParameterBinding { parameterId = 500251, name = "MxPhysicalVolume" },
                    new PhysicalAudioPreviewParameterBinding { parameterId = 500252, name = "MxPhysicalPitch" }
                }
            };
        }

        private static string ComputeHash(string value)
        {
            using (SHA256 sha = SHA256.Create())
            {
                return "sha256:" + BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value)))
                    .Replace("-", string.Empty)
                    .ToLowerInvariant();
            }
        }

        private const string CatalogJson = @"{
  ""format"": ""mx.audioPhysicalRuntimeCatalog.v1"",
  ""schemaVersion"": 1,
  ""physicalManifestHash"": ""physical-hash"",
  ""audioCatalogHash"": ""audio-hash"",
  ""materials"": [
    { ""runtimeMaterialId"": 510001, ""hardness"": 0.8, ""density"": 0.7, ""roughness"": 0.1, ""damping"": 0.2, ""resonance"": 0.8 },
    { ""runtimeMaterialId"": 510002, ""hardness"": 0.6, ""density"": 0.9, ""roughness"": 0.7, ""damping"": 0.4, ""resonance"": 0.7 }
  ],
  ""families"": [{
    ""runtimeFamilyId"": 520001,
    ""interactionKind"": 3,
    ""interactionMode"": 3,
    ""fallbackFamilyRuntimeId"": 0,
    ""fallbackBasisRuntimeId"": 0,
    ""responseProfileRuntimeId"": 540001,
    ""priority"": 1,
    ""cooldownFrames"": 3,
    ""weights"": { ""material"": 4, ""mass"": 1, ""contactArea"": 1, ""surface"": 0.5, ""construction"": 0.5, ""speed"": 1, ""intensity"": 1, ""angle"": 0.5 }
  }],
  ""basisSounds"": [{
    ""runtimeBasisId"": 530001,
    ""familyRuntimeId"": 520001,
    ""runtimeCueId"": 500201,
    ""priority"": 1,
    ""impactor"": { ""materialRuntimeId"": 510001, ""mass"": { ""min"": 0, ""max"": 1, ""anchor"": 0.5 }, ""contactArea"": { ""min"": 0, ""max"": 1, ""anchor"": 0.2 }, ""surface"": 0, ""construction"": 0 },
    ""receiver"": { ""materialRuntimeId"": 510002, ""mass"": { ""min"": 0, ""max"": 1, ""anchor"": 0.8 }, ""contactArea"": { ""min"": 0, ""max"": 1, ""anchor"": 0.2 }, ""surface"": 0, ""construction"": 0 },
    ""relativeSpeed"": { ""min"": 0, ""max"": 10, ""anchor"": 5 },
    ""intensity"": { ""min"": 0, ""max"": 1, ""anchor"": 0.5 },
    ""angleDegrees"": { ""min"": 0, ""max"": 180, ""anchor"": 90 }
  }],
  ""responseProfiles"": [{
    ""runtimeResponseProfileId"": 540001,
    ""curves"": [
      { ""parameterId"": 500251, ""input"": 6, ""outputMin"": 0, ""outputMax"": 2, ""points"": [{ ""input"": 0, ""output"": 0 }, { ""input"": 1, ""output"": 1 }] },
      { ""parameterId"": 500252, ""input"": 1, ""outputMin"": 0.5, ""outputMax"": 2, ""points"": [{ ""input"": 0, ""output"": 0.5 }, { ""input"": 10, ""output"": 1.5 }] }
    ]
  }]
}";
    }
}
