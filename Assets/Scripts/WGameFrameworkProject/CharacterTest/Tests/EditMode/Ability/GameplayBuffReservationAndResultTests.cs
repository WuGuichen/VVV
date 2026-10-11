using System;
using System.Collections.Generic;
using MxFramework.Gameplay;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.Ability
{
    public sealed class GameplayBuffReservationAndResultTests
    {
        private const int BuffId = 7001;

        [Test]
        public void LegacyRequest_DefaultCorrelationPreservesEqualityHashAndFifo()
        {
            var entity = new GameplayEntityId(4, 2);
            var request = new GameplayBuffApplicationRequest(
                GameplayBuffApplicationRequestKind.Apply,
                entity,
                BuffId,
                9,
                CharacterTestRuntimeTraceIds.Legacy,
                33);
            var explicitDefault = new GameplayBuffApplicationRequest(
                GameplayBuffApplicationRequestKind.Apply,
                entity,
                BuffId,
                9,
                CharacterTestRuntimeTraceIds.Legacy,
                33,
                default);

            Assert.AreEqual(request, explicitDefault);
            Assert.AreEqual(ComputeLegacyHash(request), request.GetHashCode());
            Assert.AreEqual(request.GetHashCode(), explicitDefault.GetHashCode());

            var buffer = new GameplayBuffRequestBuffer();
            buffer.EnqueueApply(entity, BuffId, 9, CharacterTestRuntimeTraceIds.First, 33);
            buffer.EnqueueRemove(entity, BuffId, 10, CharacterTestRuntimeTraceIds.Second, 34);

            GameplayBuffApplicationRequest[] snapshot = buffer.CreateSnapshot();
            Assert.AreEqual(0, buffer.ReservationCapacity);
            Assert.AreEqual(2, snapshot.Length);
            Assert.AreEqual(GameplayBuffApplicationRequestKind.Apply, snapshot[0].Kind);
            Assert.AreEqual(GameplayBuffApplicationRequestKind.Remove, snapshot[1].Kind);

            var drained = new List<GameplayBuffApplicationRequest>(2);
            Assert.AreEqual(2, buffer.DrainTo(drained));
            Assert.AreEqual(0, buffer.Count);
            buffer.Clear();
            Assert.AreEqual(0, buffer.Count);
        }

        [Test]
        public void Reservation_IsInvisibleUntilCommitAndPreservesVisibleCommitOrder()
        {
            var buffer = new GameplayBuffRequestBuffer(2, legacyPrewarmCapacity: 1);
            GameplayBuffApplicationRequest first = Request(1, GameplayBuffApplicationRequestKind.Apply);
            GameplayBuffApplicationRequest second = Request(2, GameplayBuffApplicationRequestKind.Remove);

            Assert.IsTrue(buffer.TryReserve(in first, out GameplayBuffRequestReservationToken token));
            Assert.IsTrue(token.IsValid);
            Assert.AreEqual(1, buffer.PreparedReservationCount);
            Assert.AreEqual(0, buffer.Count);
            Assert.AreEqual(0, buffer.CreateSnapshot().Length);

            buffer.EnqueueRemove(second.TargetEntityId, second.BuffId);
            var correlation = new GameplayBuffRequestCorrelation(10, 0, 1001010);
            buffer.CommitReservation(token, in correlation);

            GameplayBuffApplicationRequest[] snapshot = buffer.CreateSnapshot();
            Assert.AreEqual(2, snapshot.Length);
            Assert.AreEqual(second.TargetEntityId, snapshot[0].TargetEntityId);
            Assert.AreEqual(first.TargetEntityId, snapshot[1].TargetEntityId);
            Assert.AreEqual(correlation, snapshot[1].Correlation);
            Assert.AreEqual(0, buffer.PreparedReservationCount);
            Assert.AreEqual(1, buffer.CommittedReservationCount);

            var drained = new List<GameplayBuffApplicationRequest>(2);
            Assert.AreEqual(2, buffer.DrainTo(drained));
            Assert.AreEqual(0, buffer.CommittedReservationCount);
            Assert.AreEqual(0, buffer.Count);
        }

        [Test]
        public void Reservation_FullAbortCommitAndStaleTokensFailClosed()
        {
            var buffer = new GameplayBuffRequestBuffer(2);
            GameplayBuffApplicationRequest first = Request(1, GameplayBuffApplicationRequestKind.Apply);
            GameplayBuffApplicationRequest second = Request(2, GameplayBuffApplicationRequestKind.Remove);
            GameplayBuffApplicationRequest third = Request(3, GameplayBuffApplicationRequestKind.Apply);

            GameplayBuffApplicationRequest invalid = default;
            Assert.IsFalse(buffer.TryReserve(in invalid, out _));

            Assert.IsTrue(buffer.TryReserve(in first, out GameplayBuffRequestReservationToken firstToken));
            Assert.IsTrue(buffer.TryReserve(in second, out GameplayBuffRequestReservationToken secondToken));
            Assert.IsFalse(buffer.TryReserve(in third, out GameplayBuffRequestReservationToken fullToken));
            Assert.IsFalse(fullToken.IsValid);

            buffer.AbortReservation(secondToken);
            buffer.AbortReservation(secondToken);
            Assert.AreEqual(1, buffer.PreparedReservationCount);
            Assert.IsTrue(buffer.TryReserve(in third, out GameplayBuffRequestReservationToken replacementToken));
            Assert.AreNotEqual(secondToken, replacementToken);

            buffer.AbortReservation(secondToken);
            buffer.AbortReservation(new GameplayBuffRequestReservationToken(0, long.MaxValue));
            var firstCorrelation = new GameplayBuffRequestCorrelation(1, 0, 1001010);
            var thirdCorrelation = new GameplayBuffRequestCorrelation(2, 0, 1001010);
            buffer.CommitReservation(firstToken, in firstCorrelation);
            buffer.CommitReservation(firstToken, in firstCorrelation);
            buffer.CommitReservation(replacementToken, in thirdCorrelation);
            buffer.CommitReservation(replacementToken, in thirdCorrelation);

            Assert.AreEqual(0, buffer.PreparedReservationCount);
            Assert.AreEqual(2, buffer.CommittedReservationCount);
            Assert.AreEqual(2, buffer.CreateSnapshot().Length);
            buffer.AbortReservation(firstToken);
            Assert.AreEqual(2, buffer.CommittedReservationCount);
        }

        [Test]
        public void Reservation_OrdinalExhaustionIsReservedAndResetsOnlyWhenEmpty()
        {
            var buffer = new GameplayBuffRequestBuffer(
                reservationCapacity: 2,
                legacyPrewarmCapacity: 1,
                nextOrdinal: long.MaxValue,
                ordinalExhausted: false);
            GameplayBuffApplicationRequest request = Request(1, GameplayBuffApplicationRequestKind.Remove);

            Assert.IsTrue(buffer.TryReserve(in request, out GameplayBuffRequestReservationToken token));
            Assert.IsFalse(buffer.TryReserve(in request, out _));
            Assert.Throws<InvalidOperationException>(() =>
                buffer.EnqueueRemove(request.TargetEntityId, request.BuffId));

            var correlation = new GameplayBuffRequestCorrelation(1, 0, 1001011);
            Assert.DoesNotThrow(() => buffer.CommitReservation(token, in correlation));
            var drained = new List<GameplayBuffApplicationRequest>(1);
            Assert.AreEqual(1, buffer.DrainTo(drained));

            Assert.IsTrue(buffer.TryReserve(in request, out GameplayBuffRequestReservationToken resetToken));
            buffer.AbortReservation(resetToken);
            buffer.EnqueueRemove(request.TargetEntityId, request.BuffId);
            Assert.AreEqual(1, buffer.Count);
        }

        [Test]
        public void ApplicationSystem_PublishesOneFlatNumericResultPerCorrelatedTerminalBranch()
        {
            var world = new GameplayComponentWorld();
            GameplayEntityId entity = world.CreateEntity();
            var buffer = new GameplayBuffRequestBuffer(1);
            var sink = new RecordingResultSink();
            var system = new GameplayBuffApplicationSystem(
                Definitions(),
                buffer,
                resultSink: sink);

            Commit(buffer, entity, GameplayBuffApplicationRequestKind.Apply, requestId: 1);
            system.Tick(Context(world, 10));
            Commit(buffer, entity, GameplayBuffApplicationRequestKind.Apply, requestId: 2);
            system.Tick(Context(world, 11));
            Commit(buffer, entity, GameplayBuffApplicationRequestKind.Remove, requestId: 3);
            system.Tick(Context(world, 12));
            Commit(buffer, entity, GameplayBuffApplicationRequestKind.Remove, requestId: 4);
            system.Tick(Context(world, 13));

            Assert.AreEqual(4, sink.Results.Count);
            AssertResult(sink.Results[0], 1, GameplayBuffApplicationResultStatus.Succeeded,
                GameplayBuffApplicationResultReasonCode.Applied, 0, 1, 0L, 12L);
            AssertResult(sink.Results[1], 2, GameplayBuffApplicationResultStatus.Succeeded,
                GameplayBuffApplicationResultReasonCode.Refreshed, 1, 1, 12L, 13L);
            AssertResult(sink.Results[2], 3, GameplayBuffApplicationResultStatus.Succeeded,
                GameplayBuffApplicationResultReasonCode.Removed, 1, 0, 13L, 0L);
            AssertResult(sink.Results[3], 4, GameplayBuffApplicationResultStatus.Succeeded,
                GameplayBuffApplicationResultReasonCode.RemoveNoOp, 0, 0, 0L, 0L);

            buffer.EnqueueRemove(entity, BuffId);
            system.Tick(Context(world, 14));
            Assert.AreEqual(4, sink.Results.Count, "Uncorrelated legacy requests must not publish typed results.");
        }

        [Test]
        public void ApplicationSystem_PublishesNumericSemanticRejectionsWithoutRequestOrStringPayload()
        {
            var world = new GameplayComponentWorld();
            GameplayEntityId entity = world.CreateEntity();
            var sink = new RecordingResultSink();
            var buffer = new GameplayBuffRequestBuffer(1);
            var system = new GameplayBuffApplicationSystem(Definitions(), buffer, resultSink: sink);

            Commit(buffer, entity, GameplayBuffApplicationRequestKind.Apply, requestId: 1, buffId: 9999);
            system.Tick(Context(world, 10));

            Commit(buffer, entity, GameplayBuffApplicationRequestKind.Apply, requestId: 2);
            world.DestroyEntity(entity);
            system.Tick(Context(world, 11));

            Assert.AreEqual(2, sink.Results.Count);
            AssertResult(sink.Results[0], 1, GameplayBuffApplicationResultStatus.Rejected,
                GameplayBuffApplicationResultReasonCode.MissingBuffDefinition, 0, 0, 0L, 0L);
            AssertResult(sink.Results[1], 2, GameplayBuffApplicationResultStatus.Rejected,
                GameplayBuffApplicationResultReasonCode.MissingComponentEntity, 0, 0, 0L, 0L);
            Assert.AreEqual(BuffId, sink.Results[1].BuffId);
            Assert.AreEqual(17, sink.Results[1].SourceId);
        }

        private static GameplayBuffApplicationRequest Request(
            int entityIndex,
            GameplayBuffApplicationRequestKind kind)
        {
            return new GameplayBuffApplicationRequest(
                kind,
                new GameplayEntityId(entityIndex, 1),
                BuffId,
                0,
                RuntimeTraceIdentity.None,
                0);
        }

        private static void Commit(
            GameplayBuffRequestBuffer buffer,
            GameplayEntityId entity,
            GameplayBuffApplicationRequestKind kind,
            long requestId,
            int buffId = BuffId)
        {
            var request = new GameplayBuffApplicationRequest(
                kind,
                entity,
                buffId,
                17,
                RuntimeTraceIdentity.None,
                0);
            Assert.IsTrue(buffer.TryReserve(in request, out GameplayBuffRequestReservationToken token));
            var correlation = new GameplayBuffRequestCorrelation(
                requestId,
                operationIndex: 2,
                operationKindId: kind == GameplayBuffApplicationRequestKind.Apply ? 1001010 : 1001011);
            buffer.CommitReservation(token, in correlation);
        }

        private static GameplayBuffDefinitionRegistry Definitions()
        {
            return new GameplayBuffDefinitionRegistry(new GameplayBuffDefinition(
                BuffId,
                "buff.reservation.test",
                defaultDurationFrames: 2,
                maxStackCount: 1,
                stackPolicy: GameplayBuffStackPolicy.RefreshDuration,
                effects: Array.Empty<GameplayBuffEffectDefinition>()));
        }

        private static GameplaySystemContext Context(GameplayComponentWorld world, long frame)
        {
            return new GameplaySystemContext(
                new RuntimeFrame(frame),
                0d,
                0d,
                new GameplayWorld(),
                Array.Empty<RuntimeCommand>(),
                world.Events,
                componentWorld: world);
        }

        private static void AssertResult(
            GameplayBuffApplicationResult result,
            long requestId,
            GameplayBuffApplicationResultStatus status,
            GameplayBuffApplicationResultReasonCode reason,
            int oldStack,
            int newStack,
            long oldEnd,
            long newEnd)
        {
            Assert.AreEqual(requestId, result.Correlation.RequestId);
            Assert.AreEqual(status, result.Status);
            Assert.AreEqual(reason, result.ReasonCode);
            Assert.AreEqual(oldStack, result.OldStackCount);
            Assert.AreEqual(newStack, result.NewStackCount);
            Assert.AreEqual(oldEnd, result.OldEndFrame);
            Assert.AreEqual(newEnd, result.NewEndFrame);
        }

        private static int ComputeLegacyHash(GameplayBuffApplicationRequest request)
        {
            unchecked
            {
                int hash = (int)request.Kind;
                hash = (hash * 397) ^ request.TargetEntityId.GetHashCode();
                hash = (hash * 397) ^ request.BuffId;
                hash = (hash * 397) ^ request.SourceId;
                hash = (hash * 397) ^ request.TraceId.GetHashCode();
                hash = (hash * 397) ^ request.CommandId;
                return hash;
            }
        }

        private sealed class RecordingResultSink : IGameplayBuffApplicationResultSink
        {
            public readonly List<GameplayBuffApplicationResult> Results =
                new List<GameplayBuffApplicationResult>();

            public void Publish(in GameplayBuffApplicationResult result)
            {
                Results.Add(result);
            }
        }
    }
}
