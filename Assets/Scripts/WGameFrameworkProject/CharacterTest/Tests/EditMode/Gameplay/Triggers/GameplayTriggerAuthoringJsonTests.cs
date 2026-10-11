using System;
using MxFramework.Gameplay.Triggers;
using MxFramework.Gameplay.Triggers.Config;
using NUnit.Framework;

namespace MxFramework.Tests.Gameplay.Triggers
{
    public sealed class GameplayTriggerAuthoringJsonTests
    {
        [Test]
        public void CanonicalRoundTrip_SortsSetRowsWithoutChangingSemanticOrder()
        {
            GameplayTriggerConditionNodeAuthoring[] semanticChildren =
            {
                new GameplayTriggerConditionNodeAuthoring(
                    GameplayTriggerConditionOperation.PushFalse),
                new GameplayTriggerConditionNodeAuthoring(
                    GameplayTriggerConditionOperation.PushTrue)
            };
            GameplayTriggerActionAuthoring[] semanticActions =
            {
                new GameplayTriggerActionAuthoring(
                    1002,
                    GameplayTriggerTargetSelector.Subject,
                    payload0: 2),
                new GameplayTriggerActionAuthoring(
                    1001,
                    GameplayTriggerTargetSelector.Owner,
                    payload0: 1)
            };
            GameplayTriggerSourceAuthoring source2 =
                new GameplayTriggerSourceAuthoring(
                    2,
                    GameplayTriggerSourceKind.Occupancy);
            GameplayTriggerSourceAuthoring source1 =
                new GameplayTriggerSourceAuthoring(
                    1,
                    GameplayTriggerSourceKind.Occupancy);
            GameplayTriggerRouteAuthoring route2 =
                new GameplayTriggerRouteAuthoring(
                    2,
                    GameplayTriggerOccurrenceKind.Exited,
                    1,
                    semanticActions);
            GameplayTriggerRouteAuthoring route1 =
                new GameplayTriggerRouteAuthoring(
                    1,
                    GameplayTriggerOccurrenceKind.Entered,
                    1,
                    semanticActions);
            var document = new GameplayTriggerAuthoringDocument(
                GameplayTriggerConfigSchemas.AuthoringId,
                GameplayTriggerConfigSchemas.AuthoringVersion,
                new[] { source2, source1 },
                new[]
                {
                    new GameplayTriggerConditionAuthoring(
                        1,
                        new GameplayTriggerConditionNodeAuthoring(
                            GameplayTriggerConditionOperation.All,
                            children: semanticChildren))
                },
                new[]
                {
                    new GameplayTriggerDefinitionAuthoring(
                        1,
                        1,
                        1,
                        new[] { route2, route1 })
                },
                new[]
                {
                    new GameplayTriggerBindingAuthoring(
                        1,
                        1,
                        1,
                        explicitTargets: new[]
                        {
                            new GameplayTriggerBindingTargetAuthoring(
                                2,
                                GameplayTriggerTargetRef.GameplayEntityDomainId,
                                2),
                            new GameplayTriggerBindingTargetAuthoring(
                                1,
                                GameplayTriggerTargetRef.GameplayEntityDomainId,
                                1)
                        })
                });

            string canonical =
                GameplayTriggerAuthoringJson.Serialize(document);
            Assert.IsTrue(
                GameplayTriggerAuthoringJson.TryDeserialize(
                    canonical,
                    out GameplayTriggerAuthoringDocument roundTrip,
                    out GameplayTriggerAuthoringJsonDiagnostic diagnostic),
                Describe(diagnostic));

            Assert.AreEqual(
                canonical,
                GameplayTriggerAuthoringJson.Serialize(roundTrip));
            Assert.AreEqual(1, roundTrip.Sources[0].SourceDefinitionId);
            Assert.AreEqual(2, roundTrip.Sources[1].SourceDefinitionId);
            Assert.AreEqual(1, roundTrip.Definitions[0].Routes[0].RouteId);
            Assert.AreEqual(2, roundTrip.Definitions[0].Routes[1].RouteId);
            Assert.AreEqual(
                GameplayTriggerConditionOperation.PushFalse,
                roundTrip.Conditions[0].Root.Children[0].Operation);
            Assert.AreEqual(
                GameplayTriggerConditionOperation.PushTrue,
                roundTrip.Conditions[0].Root.Children[1].Operation);
            Assert.AreEqual(
                1002,
                roundTrip.Definitions[0].Routes[0].Actions[0].ActionKindId);
            Assert.AreEqual(
                1001,
                roundTrip.Definitions[0].Routes[0].Actions[1].ActionKindId);
            Assert.AreEqual(
                1,
                roundTrip.Bindings[0].ExplicitTargets[0].SlotId);
            Assert.AreSame(source2, document.Sources[0]);
            Assert.AreSame(route2, document.Definitions[0].Routes[0]);
        }

        [Test]
        public void StrictReader_RejectsUnknownMissingDuplicateAndNullFields()
        {
            string json = GameplayTriggerAuthoringJson.Serialize(
                CreateMinimumDocument(),
                indented: false);

            AssertRejected(
                json.Insert(json.Length - 1, ",\"unknownP23Field\":true"),
                GameplayTriggerAuthoringJsonErrorCode.UnknownProperty);
            AssertRejected(
                json.Replace(
                    "\"schemaVersion\":1,",
                    string.Empty),
                GameplayTriggerAuthoringJsonErrorCode.MissingProperty);
            AssertRejected(
                json.Insert(
                    1,
                    "\"schemaId\":\"duplicate\","),
                GameplayTriggerAuthoringJsonErrorCode.DuplicateProperty);
            AssertRejected(
                json.Replace(
                    "\"sources\":[]",
                    "\"sources\":null"),
                GameplayTriggerAuthoringJsonErrorCode.NullProperty);
        }

        [Test]
        public void StrictReader_RejectsTrailingRootAndIntegerEnum()
        {
            string json = GameplayTriggerAuthoringJson.Serialize(
                CreateOneRowDocument(),
                indented: false);
            AssertRejected(
                json + "{}",
                GameplayTriggerAuthoringJsonErrorCode.InvalidJson);
            AssertRejected(
                json.Replace(
                    "\"sourceKind\":\"Occupancy\"",
                    "\"sourceKind\":1"),
                GameplayTriggerAuthoringJsonErrorCode.InvalidValue);
        }

        private static GameplayTriggerAuthoringDocument CreateMinimumDocument()
        {
            return new GameplayTriggerAuthoringDocument(
                GameplayTriggerConfigSchemas.AuthoringId,
                GameplayTriggerConfigSchemas.AuthoringVersion,
                Array.Empty<GameplayTriggerSourceAuthoring>(),
                Array.Empty<GameplayTriggerConditionAuthoring>(),
                Array.Empty<GameplayTriggerDefinitionAuthoring>(),
                Array.Empty<GameplayTriggerBindingAuthoring>());
        }

        private static GameplayTriggerAuthoringDocument CreateOneRowDocument()
        {
            return new GameplayTriggerAuthoringDocument(
                GameplayTriggerConfigSchemas.AuthoringId,
                GameplayTriggerConfigSchemas.AuthoringVersion,
                new[]
                {
                    new GameplayTriggerSourceAuthoring(
                        1,
                        GameplayTriggerSourceKind.Occupancy)
                },
                Array.Empty<GameplayTriggerConditionAuthoring>(),
                Array.Empty<GameplayTriggerDefinitionAuthoring>(),
                Array.Empty<GameplayTriggerBindingAuthoring>());
        }

        private static void AssertRejected(
            string json,
            GameplayTriggerAuthoringJsonErrorCode expectedCode)
        {
            Assert.IsFalse(
                GameplayTriggerAuthoringJson.TryDeserialize(
                    json,
                    out _,
                    out GameplayTriggerAuthoringJsonDiagnostic diagnostic));
            Assert.AreEqual(
                expectedCode,
                diagnostic.Code,
                Describe(diagnostic));
        }

        private static string Describe(
            GameplayTriggerAuthoringJsonDiagnostic diagnostic)
        {
            return diagnostic.Code
                + " "
                + diagnostic.Path
                + ": "
                + diagnostic.Message;
        }
    }
}
