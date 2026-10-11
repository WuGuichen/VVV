using System;
using MxFramework.Audio;
using MxFramework.Audio.Physical;
using NUnit.Framework;

namespace MxFramework.Tests.Audio
{
    public sealed class PhysicalAudioResolverTests
    {
        [Test]
        public void TryResolve_SelectsExactMaterialPairAndEvaluatesResponse()
        {
            PhysicalAudioDefinitionCatalog catalog = CreateCatalog(
                new[]
                {
                    CreateBasis(530002, 500202, AcousticMaterialId.None, AcousticMaterialId.None, priority: 0),
                    CreateBasis(530001, 500201, new AcousticMaterialId(510001), new AcousticMaterialId(510002), priority: 0)
                });
            var resolver = new PhysicalAudioResolver(catalog);
            var parameters = new AudioParameterValue[2];

            bool success = resolver.TryResolve(CreateFact(), parameters, out PhysicalAudioResolvedPlayback result);

            Assert.That(success, Is.True);
            Assert.That(result.BasisId, Is.EqualTo(new AudioBasisId(530001)));
            Assert.That(result.CueId, Is.EqualTo(500201));
            Assert.That(result.ParameterCount, Is.EqualTo(2));
            Assert.That(parameters[0].ParameterId, Is.EqualTo(500251));
            Assert.That(parameters[0].Value, Is.EqualTo(0.6f).Within(0.0001f));
            Assert.That(parameters[1].Value, Is.EqualTo(1.1f).Within(0.0001f));
        }

        [Test]
        public void TryResolve_UsesExplicitFallbackFamilyWhenPrimaryHasNoMatch()
        {
            var materials = CreateMaterials();
            var response = CreateResponseProfile();
            var primary = new PhysicalAudioFamilyDefinition(
                new PhysicalAudioFamilyId(520001),
                PhysicalAudioInteractionKind.ObjectImpact,
                PhysicalAudioInteractionMode.Impact,
                new PhysicalAudioFamilyId(520002),
                AudioBasisId.None,
                response.Id,
                10,
                2,
                CreateWeights());
            var fallback = new PhysicalAudioFamilyDefinition(
                new PhysicalAudioFamilyId(520002),
                PhysicalAudioInteractionKind.ObjectImpact,
                PhysicalAudioInteractionMode.Any,
                PhysicalAudioFamilyId.None,
                new AudioBasisId(530002),
                response.Id,
                0,
                5,
                CreateWeights());
            var impossible = CreateBasis(530001, 500201, new AcousticMaterialId(510003), new AcousticMaterialId(510003), familyId: 520001);
            var fallbackBasis = CreateBasis(530002, 500202, AcousticMaterialId.None, AcousticMaterialId.None, familyId: 520002);
            var catalog = new PhysicalAudioDefinitionCatalog(
                materials,
                new[] { primary, fallback },
                new[] { impossible, fallbackBasis },
                new[] { response });
            var resolver = new PhysicalAudioResolver(catalog);

            bool success = resolver.TryResolve(CreateFact(), new AudioParameterValue[2], out PhysicalAudioResolvedPlayback result);

            Assert.That(success, Is.True);
            Assert.That(result.FamilyId, Is.EqualTo(new PhysicalAudioFamilyId(520002)));
            Assert.That(result.BasisId, Is.EqualTo(new AudioBasisId(530002)));
            Assert.That(result.UsedFallback, Is.True);
            Assert.That(result.CooldownFrames, Is.EqualTo(5));
        }

        [Test]
        public void TryResolve_ReportsRequiredResponseBufferWithoutPartialSuccess()
        {
            var resolver = new PhysicalAudioResolver(CreateCatalog(new[]
            {
                CreateBasis(530001, 500201, new AcousticMaterialId(510001), new AcousticMaterialId(510002))
            }));

            bool success = resolver.TryResolve(CreateFact(), new AudioParameterValue[1], out PhysicalAudioResolvedPlayback result);

            Assert.That(success, Is.False);
            Assert.That(result.FailureReason, Is.EqualTo(PhysicalAudioResolveFailureReason.BufferTooSmall));
            Assert.That(result.ParameterCount, Is.EqualTo(2));
        }

        [Test]
        public void TryResolve_UsesStableBasisIdAsFinalTieBreaker()
        {
            var resolver = new PhysicalAudioResolver(CreateCatalog(new[]
            {
                CreateBasis(530002, 500202, new AcousticMaterialId(510001), new AcousticMaterialId(510002)),
                CreateBasis(530001, 500201, new AcousticMaterialId(510001), new AcousticMaterialId(510002))
            }));

            bool success = resolver.TryResolve(CreateFact(), new AudioParameterValue[2], out PhysicalAudioResolvedPlayback result);

            Assert.That(success, Is.True);
            Assert.That(result.BasisId, Is.EqualTo(new AudioBasisId(530001)));
        }

        [Test]
        public void TryResolve_PreservesSignedPriorityAndSelectsHigherValue()
        {
            PhysicalAudioBasisDefinition lower = CreateBasis(
                530001,
                500201,
                new AcousticMaterialId(510001),
                new AcousticMaterialId(510002),
                priority: -20);
            PhysicalAudioBasisDefinition higher = CreateBasis(
                530002,
                500202,
                new AcousticMaterialId(510001),
                new AcousticMaterialId(510002),
                priority: -10);
            var resolver = new PhysicalAudioResolver(CreateCatalog(new[] { lower, higher }));

            bool success = resolver.TryResolve(CreateFact(), new AudioParameterValue[2], out PhysicalAudioResolvedPlayback result);

            Assert.That(success, Is.True);
            Assert.That(lower.Priority, Is.EqualTo(-20));
            Assert.That(higher.Priority, Is.EqualTo(-10));
            Assert.That(result.BasisId, Is.EqualTo(new AudioBasisId(530002)));
        }

        [Test]
        public void CaptureTrace_IncludesRejectedAndSelectedCandidates()
        {
            var resolver = new PhysicalAudioResolver(CreateCatalog(new[]
            {
                CreateBasis(530001, 500201, new AcousticMaterialId(510003), new AcousticMaterialId(510002)),
                CreateBasis(530002, 500202, new AcousticMaterialId(510001), new AcousticMaterialId(510002))
            }));

            PhysicalAudioResolveTraceSnapshot trace = resolver.CaptureTrace(CreateFact());

            Assert.That(trace.Result.Success, Is.True);
            Assert.That(trace.Entries, Has.Length.EqualTo(2));
            Assert.That(trace.Entries[0].RejectionReason, Is.EqualTo(PhysicalAudioBasisRejectionReason.ImpactorMaterial));
            Assert.That(trace.Entries[1].Matched, Is.True);
            Assert.That(trace.Parameters, Has.Length.EqualTo(2));
        }

        [Test]
        public void TryResolve_AfterInitialization_DoesNotAllocate()
        {
            var resolver = new PhysicalAudioResolver(CreateCatalog(new[]
            {
                CreateBasis(530001, 500201, new AcousticMaterialId(510001), new AcousticMaterialId(510002))
            }));
            PhysicalAudioInteractionFact fact = CreateFact();
            var parameters = new AudioParameterValue[2];
            Assert.That(resolver.TryResolve(fact, parameters, out _), Is.True);
            _ = GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();

            for (int i = 0; i < 1000; i++)
                resolver.TryResolve(fact, parameters, out _);

            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(allocated, Is.EqualTo(0L));
        }

        [Test]
        public void TryResolve_PairV2_TreatsMaterialOrderAsUnorderedButPreservesDirectedBodies()
        {
            PhysicalAudioDefinitionCatalog catalog = CreatePairCatalog(
                CreatePairMaterials(),
                new[] { new AcousticMaterialPairDefinition(new AcousticMaterialPairId(550001), new AcousticMaterialId(510001), new AcousticMaterialId(510002)) },
                new[] { CreatePairBasis(530001, 550001, 500201, 0.2f, 0.8f) });
            var resolver = new PhysicalAudioResolver(catalog);
            var parameters = new AudioParameterValue[2];

            PhysicalAudioInteractionFact forward = CreatePairFact(510001, 0.2f, 510002, 0.8f);
            PhysicalAudioInteractionFact reverse = CreatePairFact(510002, 0.8f, 510001, 0.2f);

            Assert.That(resolver.TryResolve(forward, parameters, out PhysicalAudioResolvedPlayback forwardResult), Is.True);
            Assert.That(resolver.TryResolve(reverse, parameters, out PhysicalAudioResolvedPlayback reverseResult), Is.True);
            Assert.That(forwardResult.MaterialPairId, Is.EqualTo(new AcousticMaterialPairId(550001)));
            Assert.That(reverseResult.MaterialPairId, Is.EqualTo(forwardResult.MaterialPairId));
            Assert.That(reverseResult.BasisId, Is.EqualTo(forwardResult.BasisId));
            Assert.That(reverseResult.VariantKind, Is.EqualTo(PhysicalAudioVariantKind.Impact));
        }

        [Test]
        public void TryResolve_PairV2_UsesExplicitMaterialFallback()
        {
            PhysicalAudioDefinitionCatalog catalog = CreatePairCatalog(
                CreatePairMaterials(),
                new[] { new AcousticMaterialPairDefinition(new AcousticMaterialPairId(550001), new AcousticMaterialId(510001), new AcousticMaterialId(510002)) },
                new[] { CreatePairBasis(530001, 550001, 500201, 0.5f, 0.8f) });
            var resolver = new PhysicalAudioResolver(catalog);

            bool success = resolver.TryResolve(
                CreatePairFact(510003, 0.5f, 510002, 0.8f),
                new AudioParameterValue[2],
                out PhysicalAudioResolvedPlayback result);

            Assert.That(success, Is.True);
            Assert.That(result.MaterialPairId, Is.EqualTo(new AcousticMaterialPairId(550001)));
            Assert.That(result.UsedFallback, Is.True);

            PhysicalAudioResolveTraceSnapshot trace = resolver.CaptureTrace(CreatePairFact(510003, 0.5f, 510002, 0.8f));
            Assert.That(trace.ImpactorMaterialFallbackPath, Is.EqualTo(new[]
            {
                new AcousticMaterialId(510003),
                new AcousticMaterialId(510001)
            }));
            Assert.That(trace.ReceiverMaterialFallbackPath, Is.EqualTo(new[] { new AcousticMaterialId(510002) }));
            Assert.That(trace.Result.MaterialPairId, Is.EqualTo(new AcousticMaterialPairId(550001)));
        }

        [Test]
        public void TryResolve_PairV2_UsesStablePairIdAfterFallbackDepthTie()
        {
            var pairs = new[]
            {
                new AcousticMaterialPairDefinition(new AcousticMaterialPairId(550002), new AcousticMaterialId(510001), new AcousticMaterialId(510004)),
                new AcousticMaterialPairDefinition(new AcousticMaterialPairId(550001), new AcousticMaterialId(510002), new AcousticMaterialId(510003))
            };
            var bases = new[]
            {
                CreatePairBasis(530002, 550002, 500202, 0.5f, 0.5f),
                CreatePairBasis(530001, 550001, 500201, 0.5f, 0.5f)
            };
            var resolver = new PhysicalAudioResolver(CreatePairCatalog(CreatePairMaterials(), pairs, bases));

            bool success = resolver.TryResolve(
                CreatePairFact(510003, 0.5f, 510004, 0.5f),
                new AudioParameterValue[2],
                out PhysicalAudioResolvedPlayback result);

            Assert.That(success, Is.True);
            Assert.That(result.MaterialPairId, Is.EqualTo(new AcousticMaterialPairId(550001)));
            Assert.That(result.BasisId, Is.EqualTo(new AudioBasisId(530001)));
        }

        [Test]
        public void TryResolve_PairV2_AfterInitialization_DoesNotAllocate()
        {
            PhysicalAudioDefinitionCatalog catalog = CreatePairCatalog(
                CreatePairMaterials(),
                new[] { new AcousticMaterialPairDefinition(new AcousticMaterialPairId(550001), new AcousticMaterialId(510001), new AcousticMaterialId(510002)) },
                new[] { CreatePairBasis(530001, 550001, 500201, 0.5f, 0.8f) });
            var resolver = new PhysicalAudioResolver(catalog);
            PhysicalAudioInteractionFact fact = CreatePairFact(510003, 0.5f, 510002, 0.8f);
            var parameters = new AudioParameterValue[2];
            Assert.That(resolver.TryResolve(fact, parameters, out _), Is.True);
            _ = GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();

            for (int i = 0; i < 1000; i++)
                resolver.TryResolve(fact, parameters, out _);

            Assert.That(GC.GetAllocatedBytesForCurrentThread() - before, Is.EqualTo(0L));
        }

        [Test]
        public void Catalog_PairV2_RejectsDuplicateCanonicalMaterialCombination()
        {
            var pairs = new[]
            {
                new AcousticMaterialPairDefinition(new AcousticMaterialPairId(550001), new AcousticMaterialId(510001), new AcousticMaterialId(510002)),
                new AcousticMaterialPairDefinition(new AcousticMaterialPairId(550002), new AcousticMaterialId(510001), new AcousticMaterialId(510002))
            };

            Assert.Throws<ArgumentException>(() => CreatePairCatalog(CreatePairMaterials(), pairs, Array.Empty<PhysicalAudioBasisDefinition>()));
        }

        [Test]
        public void Catalog_ClonesSourceArraysAndExposesReadOnlyIndexedValues()
        {
            AcousticMaterialProfileDefinition[] materials = CreateMaterials();
            PhysicalAudioResponseProfileDefinition response = CreateResponseProfile();
            var family = new PhysicalAudioFamilyDefinition(
                new PhysicalAudioFamilyId(520001),
                PhysicalAudioInteractionKind.ObjectImpact,
                PhysicalAudioInteractionMode.Impact,
                PhysicalAudioFamilyId.None,
                AudioBasisId.None,
                response.Id,
                priority: 1,
                cooldownFrames: 0,
                CreateWeights());
            PhysicalAudioBasisDefinition basis = CreateBasis(
                530001,
                500201,
                new AcousticMaterialId(510001),
                new AcousticMaterialId(510002));
            var catalog = new PhysicalAudioDefinitionCatalog(
                materials,
                new[] { family },
                new[] { basis },
                new[] { response });

            materials[0] = new AcousticMaterialProfileDefinition(
                new AcousticMaterialId(519999),
                0f,
                0f,
                0f,
                0f,
                0f);

            Assert.That(catalog.MaterialCount, Is.EqualTo(3));
            Assert.That(catalog.FamilyCount, Is.EqualTo(1));
            Assert.That(catalog.BasisCount, Is.EqualTo(1));
            Assert.That(catalog.ResponseProfileCount, Is.EqualTo(1));
            Assert.That(catalog.GetMaterialAt(0).Id, Is.EqualTo(new AcousticMaterialId(510001)));
            Assert.That(catalog.TryGetMaterial(
                new AcousticMaterialId(510001),
                out AcousticMaterialProfileDefinition stored), Is.True);
            Assert.That(stored.Hardness, Is.EqualTo(0.8f));
        }

        private static PhysicalAudioDefinitionCatalog CreateCatalog(PhysicalAudioBasisDefinition[] bases)
        {
            PhysicalAudioResponseProfileDefinition response = CreateResponseProfile();
            var family = new PhysicalAudioFamilyDefinition(
                new PhysicalAudioFamilyId(520001),
                PhysicalAudioInteractionKind.ObjectImpact,
                PhysicalAudioInteractionMode.Impact,
                PhysicalAudioFamilyId.None,
                AudioBasisId.None,
                response.Id,
                10,
                3,
                CreateWeights());
            return new PhysicalAudioDefinitionCatalog(
                CreateMaterials(),
                new[] { family },
                bases,
                new[] { response },
                "physical-hash",
                "audio-hash");
        }

        private static PhysicalAudioDefinitionCatalog CreatePairCatalog(
            AcousticMaterialProfileDefinition[] materials,
            AcousticMaterialPairDefinition[] pairs,
            PhysicalAudioBasisDefinition[] bases)
        {
            PhysicalAudioResponseProfileDefinition response = CreateResponseProfile();
            var family = new PhysicalAudioFamilyDefinition(
                new PhysicalAudioFamilyId(520001),
                PhysicalAudioInteractionKind.ObjectImpact,
                PhysicalAudioInteractionMode.Impact,
                PhysicalAudioVariantKind.Impact,
                PhysicalAudioFamilyId.None,
                AudioBasisId.None,
                response.Id,
                10,
                3,
                CreateWeights());
            return new PhysicalAudioDefinitionCatalog(
                materials,
                pairs,
                new[] { family },
                bases,
                new[] { response },
                "physical-v2-hash",
                "audio-hash");
        }

        private static AcousticMaterialProfileDefinition[] CreateMaterials()
        {
            return new[]
            {
                new AcousticMaterialProfileDefinition(new AcousticMaterialId(510001), 0.8f, 0.7f, 0.1f, 0.2f, 0.8f),
                new AcousticMaterialProfileDefinition(new AcousticMaterialId(510002), 0.6f, 0.9f, 0.7f, 0.4f, 0.7f),
                new AcousticMaterialProfileDefinition(new AcousticMaterialId(510003), 0.2f, 0.2f, 0.5f, 0.9f, 0.2f)
            };
        }

        private static AcousticMaterialProfileDefinition[] CreatePairMaterials()
        {
            return new[]
            {
                new AcousticMaterialProfileDefinition(new AcousticMaterialId(510001), 0.8f, 0.7f, 0.1f, 0.2f, 0.8f),
                new AcousticMaterialProfileDefinition(new AcousticMaterialId(510002), 0.6f, 0.9f, 0.7f, 0.4f, 0.7f),
                new AcousticMaterialProfileDefinition(new AcousticMaterialId(510003), 0.2f, 0.2f, 0.5f, 0.9f, 0.2f, new AcousticMaterialId(510001)),
                new AcousticMaterialProfileDefinition(new AcousticMaterialId(510004), 0.9f, 0.8f, 0.2f, 0.1f, 0.9f, new AcousticMaterialId(510002))
            };
        }

        private static PhysicalAudioBasisDefinition CreatePairBasis(
            int basisId,
            int pairId,
            int cueId,
            float sideAMass,
            float sideBMass)
        {
            var area = new PhysicalAudioRange(0f, 1f, 0.2f);
            return new PhysicalAudioBasisDefinition(
                new AudioBasisId(basisId),
                new AcousticMaterialPairId(pairId),
                PhysicalAudioVariantKind.Impact,
                cueId,
                priority: 0,
                new PhysicalAudioBodyAnchorDefinition(
                    AcousticMaterialId.None,
                    new PhysicalAudioRange(sideAMass, sideAMass, sideAMass),
                    area,
                    AcousticSurfaceKind.Any,
                    AcousticConstructionKind.Any),
                new PhysicalAudioBodyAnchorDefinition(
                    AcousticMaterialId.None,
                    new PhysicalAudioRange(sideBMass, sideBMass, sideBMass),
                    area,
                    AcousticSurfaceKind.Any,
                    AcousticConstructionKind.Any),
                new PhysicalAudioRange(0f, 10f, 5f),
                new PhysicalAudioRange(0f, 1f, 0.5f),
                new PhysicalAudioRange(0f, 180f, 90f));
        }

        private static PhysicalAudioInteractionFact CreatePairFact(
            int impactorMaterialId,
            float impactorMass,
            int receiverMaterialId,
            float receiverMass)
        {
            var impactor = new AcousticBodyProperties(
                new AcousticMaterialId(impactorMaterialId),
                impactorMass,
                0.2f,
                AcousticSurfaceKind.Smooth,
                AcousticConstructionKind.Solid);
            var receiver = new AcousticBodyProperties(
                new AcousticMaterialId(receiverMaterialId),
                receiverMass,
                0.2f,
                AcousticSurfaceKind.Rough,
                AcousticConstructionKind.Solid);
            return new PhysicalAudioInteractionFact(
                PhysicalAudioInteractionKind.ObjectImpact,
                PhysicalAudioInteractionMode.Impact,
                PhysicalAudioInteractionPhase.Started,
                impactor,
                receiver,
                relativeSpeed: 6f,
                normalSpeed: 5f,
                tangentialSpeed: 1f,
                pressure: 0.8f,
                impulse: 4f,
                angleDegrees: 90f,
                intensity: 0.6f,
                AudioTransform.Origin,
                emitterId: 10,
                frame: 100,
                sequence: 2);
        }

        private static PhysicalAudioResponseProfileDefinition CreateResponseProfile()
        {
            return new PhysicalAudioResponseProfileDefinition(
                new PhysicalAudioResponseProfileId(540001),
                new[]
                {
                    new PhysicalAudioResponseCurveDefinition(
                        500251,
                        PhysicalAudioResponseInput.Intensity,
                        0f,
                        1f,
                        new[] { new PhysicalAudioResponsePoint(0f, 0f), new PhysicalAudioResponsePoint(1f, 1f) }),
                    new PhysicalAudioResponseCurveDefinition(
                        500252,
                        PhysicalAudioResponseInput.RelativeSpeed,
                        0.5f,
                        1.5f,
                        new[] { new PhysicalAudioResponsePoint(0f, 0.5f), new PhysicalAudioResponsePoint(10f, 1.5f) })
                });
        }

        private static PhysicalAudioBasisDefinition CreateBasis(
            int basisId,
            int cueId,
            AcousticMaterialId impactorMaterial,
            AcousticMaterialId receiverMaterial,
            int priority = 0,
            int familyId = 520001)
        {
            var mass = new PhysicalAudioRange(0f, 1f, 0.5f);
            var area = new PhysicalAudioRange(0f, 1f, 0.2f);
            return new PhysicalAudioBasisDefinition(
                new AudioBasisId(basisId),
                new PhysicalAudioFamilyId(familyId),
                cueId,
                priority,
                new PhysicalAudioBodyAnchorDefinition(impactorMaterial, mass, area, AcousticSurfaceKind.Any, AcousticConstructionKind.Any),
                new PhysicalAudioBodyAnchorDefinition(receiverMaterial, mass, area, AcousticSurfaceKind.Any, AcousticConstructionKind.Any),
                new PhysicalAudioRange(0f, 10f, 5f),
                new PhysicalAudioRange(0f, 1f, 0.5f),
                new PhysicalAudioRange(0f, 180f, 90f));
        }

        private static PhysicalAudioSelectionWeights CreateWeights()
        {
            return new PhysicalAudioSelectionWeights(4f, 1f, 1f, 0.5f, 0.5f, 1f, 1f, 0.5f);
        }

        private static PhysicalAudioInteractionFact CreateFact()
        {
            var impactor = new AcousticBodyProperties(
                new AcousticMaterialId(510001),
                0.5f,
                0.2f,
                AcousticSurfaceKind.Smooth,
                AcousticConstructionKind.Solid);
            var receiver = new AcousticBodyProperties(
                new AcousticMaterialId(510002),
                0.8f,
                0.2f,
                AcousticSurfaceKind.Rough,
                AcousticConstructionKind.Solid);
            return new PhysicalAudioInteractionFact(
                PhysicalAudioInteractionKind.ObjectImpact,
                PhysicalAudioInteractionMode.Impact,
                PhysicalAudioInteractionPhase.Started,
                impactor,
                receiver,
                relativeSpeed: 6f,
                normalSpeed: 5f,
                tangentialSpeed: 1f,
                pressure: 0.8f,
                impulse: 4f,
                angleDegrees: 90f,
                intensity: 0.6f,
                AudioTransform.Origin,
                emitterId: 10,
                frame: 100,
                sequence: 2);
        }
    }
}
