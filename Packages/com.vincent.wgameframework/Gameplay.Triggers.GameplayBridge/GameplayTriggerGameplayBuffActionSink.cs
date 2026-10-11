using System;
using MxFramework.Gameplay;

namespace MxFramework.Gameplay.Triggers.GameplayBridge
{
    /// <summary>Reservation-aware same-frame Trigger action sink for one Gameplay component Buff request kind.</summary>
    public sealed class GameplayTriggerGameplayBuffActionSink : IGameplayTriggerActionSink
    {
        private readonly GameplayBuffRequestBuffer _requestBuffer;
        private readonly GameplayBuffApplicationRequestKind _requestKind;
        private readonly TokenMapping[] _tokenMappings;
        private readonly int[] _freeMappingSlots;
        private int _freeMappingCount;
        private int _nextToken = 1;
        private bool _tokenExhausted;

        /// <remarks>AllocByDesign. The supplied buffer must have positive fixed reservation capacity.</remarks>
        public GameplayTriggerGameplayBuffActionSink(
            GameplayTriggerActionKindId actionKindId,
            GameplayBuffRequestBuffer requestBuffer)
        {
            _requestBuffer = requestBuffer ?? throw new ArgumentNullException(nameof(requestBuffer));
            if (_requestBuffer.ReservationCapacity <= 0)
                throw new ArgumentException(
                    "Gameplay Trigger Buff action sinks require positive reservation capacity.",
                    nameof(requestBuffer));

            if (actionKindId == GameplayTriggerGameplayBuffActionKinds.ApplyComponentBuff)
                _requestKind = GameplayBuffApplicationRequestKind.Apply;
            else if (actionKindId == GameplayTriggerGameplayBuffActionKinds.RemoveComponentBuff)
                _requestKind = GameplayBuffApplicationRequestKind.Remove;
            else
                throw new ArgumentOutOfRangeException(nameof(actionKindId));

            ActionKindId = actionKindId;
            _tokenMappings = new TokenMapping[_requestBuffer.ReservationCapacity];
            _freeMappingSlots = new int[_requestBuffer.ReservationCapacity];
            _freeMappingCount = _freeMappingSlots.Length;
            for (int i = 0; i < _freeMappingSlots.Length; i++)
                _freeMappingSlots[i] = _freeMappingSlots.Length - i - 1;
        }

        public GameplayTriggerActionKindId ActionKindId { get; }
        public GameplayTriggerDeliveryContract DeliveryContract =>
            GameplayTriggerGameplayBuffActionKinds.DeliveryContract;
        public bool IsEnabled { get; private set; } = true;

        /// <remarks>NoAlloc. Disabling rejects new Prepare calls and does not revoke live tokens.</remarks>
        public void SetEnabled(bool enabled)
        {
            IsEnabled = enabled;
        }

        /// <remarks>NoAlloc. Structural mismatch, disabled state, local exhaustion or buffer full fail atomically.</remarks>
        public bool TryPrepare(
            in GameplayTriggerActionPreparation preparation,
            out int token)
        {
            token = 0;
            if (!IsEnabled ||
                _freeMappingCount == 0 ||
                _tokenExhausted ||
                !TryCreateRequest(in preparation, out GameplayBuffApplicationRequest request) ||
                !_requestBuffer.TryReserve(in request, out GameplayBuffRequestReservationToken bufferToken))
            {
                return false;
            }

            int mappingIndex = _freeMappingSlots[--_freeMappingCount];
            token = _nextToken;
            if (_nextToken == int.MaxValue)
                _tokenExhausted = true;
            else
                _nextToken++;
            _tokenMappings[mappingIndex] = new TokenMapping(token, bufferToken);
            return true;
        }

        /// <remarks>NoAlloc and idempotent. Unknown or duplicate tokens are no-ops.</remarks>
        public void Abort(int token)
        {
            int mappingIndex = FindMapping(token);
            if (mappingIndex < 0)
                return;

            GameplayBuffRequestReservationToken bufferToken =
                _tokenMappings[mappingIndex].BufferToken;
            _requestBuffer.AbortReservation(bufferToken);
            ReleaseMapping(mappingIndex);
        }

        /// <remarks>NoAlloc, no-fail and exactly-once for a token returned by TryPrepare.</remarks>
        public void Commit(int token, in GameplayTriggerActionIntent intent)
        {
            int mappingIndex = FindMapping(token);
            if (mappingIndex < 0)
                return;

            GameplayBuffRequestReservationToken bufferToken =
                _tokenMappings[mappingIndex].BufferToken;
            var correlation = new GameplayBuffRequestCorrelation(
                intent.ActivationId,
                intent.ActionIndex,
                ActionKindId.Value);
            _requestBuffer.CommitReservation(bufferToken, in correlation);
            ReleaseMapping(mappingIndex);
        }

        private bool TryCreateRequest(
            in GameplayTriggerActionPreparation preparation,
            out GameplayBuffApplicationRequest request)
        {
            GameplayTriggerActionInstruction instruction = preparation.Instruction;
            GameplayTriggerTargetRef target = preparation.Target;
            if (instruction.ActionKindId != ActionKindId ||
                target.DomainId != GameplayTriggerTargetRef.GameplayEntityDomainId ||
                target.Value <= 0L || target.Value > int.MaxValue ||
                target.Generation <= 0 ||
                instruction.Payload0 <= 0L || instruction.Payload0 > int.MaxValue ||
                instruction.Payload1 < 0L || instruction.Payload1 > int.MaxValue ||
                instruction.Payload2 != 0L)
            {
                request = default;
                return false;
            }

            request = new GameplayBuffApplicationRequest(
                _requestKind,
                new GameplayEntityId((int)target.Value, target.Generation),
                (int)instruction.Payload0,
                (int)instruction.Payload1,
                default,
                commandId: 0);
            return true;
        }

        private int FindMapping(int token)
        {
            if (token <= 0)
                return -1;
            for (int i = 0; i < _tokenMappings.Length; i++)
            {
                if (_tokenMappings[i].ActionToken == token)
                    return i;
            }
            return -1;
        }

        private void ReleaseMapping(int mappingIndex)
        {
            _tokenMappings[mappingIndex] = default;
            _freeMappingSlots[_freeMappingCount++] = mappingIndex;
        }

        private readonly struct TokenMapping
        {
            public TokenMapping(
                int actionToken,
                GameplayBuffRequestReservationToken bufferToken)
            {
                ActionToken = actionToken;
                BufferToken = bufferToken;
            }

            public int ActionToken { get; }
            public GameplayBuffRequestReservationToken BufferToken { get; }
        }
    }
}
