using System;
using MxFramework.Runtime;

namespace MxFramework.Demo.GameplayTriggers
{
    /// <summary>Public Demo-only owner for a replaceable Gameplay Trigger playable composition.</summary>
    public sealed class GameplayTriggerPlayableSession : IDisposable
    {
        public const double FixedDeltaTime = 1d / 60d;

        private readonly GameplayTriggerPlayableInputLatch _inputLatch =
            new GameplayTriggerPlayableInputLatch();
        private GameplayTriggerPlayableComposition _composition;
        private RuntimeSaveState _checkpoint;
        private bool _candidateSwapInProgress;
        private bool _lastControlSucceeded = true;
        private bool _disposed;

        /// <remarks>AllocByDesign. Builds the default playable composition.</remarks>
        public GameplayTriggerPlayableSession()
        {
            _composition = new GameplayTriggerPlayableComposition(
                restoreProfile: null,
                isCandidateSwapInProgress: IsCandidateSwapInProgress);
        }

        /// <remarks>NoAlloc.</remarks>
        public GameplayTriggerPlayableSnapshot Snapshot
        {
            get
            {
                ThrowIfDisposed();
                return Decorate(_composition.Snapshot, 0);
            }
        }

        /// <summary>Submits one Unity/exogenous sample without advancing authority.</summary>
        /// <remarks>NoAlloc. A full edge ring rejects the whole sample atomically.</remarks>
        public bool SubmitInput(in GameplayTriggerPlayableInput input)
        {
            ThrowIfDisposed();
            return _inputLatch.TrySubmit(in input);
        }

        /// <summary>Advances one logical Tick using the current held movement and one queued edge.</summary>
        /// <remarks>NoAlloc after initialization.</remarks>
        public GameplayTriggerPlayableTickResult Tick()
        {
            ThrowIfDisposed();
            _inputLatch.Peek(
                out int moveX,
                out int moveZ,
                out int controlCommandId,
                out _);
            GameplayTriggerPlayableTickResult result =
                _composition.Tick(moveX, moveZ, controlCommandId);
            if (result.Accepted && controlCommandId != 0)
                _inputLatch.CommitPeekedEdge();
            if (!result.Accepted)
            {
                return new GameplayTriggerPlayableTickResult(
                    false,
                    Decorate(result.Snapshot, 0));
            }

            if (controlCommandId != 0)
                ProcessControl(controlCommandId);
            return new GameplayTriggerPlayableTickResult(
                true,
                Decorate(_composition.Snapshot, controlCommandId));
        }

        /// <summary>Convenience path for one input sample followed by one logical Tick.</summary>
        /// <remarks>NoAlloc after initialization.</remarks>
        public GameplayTriggerPlayableTickResult Tick(
            in GameplayTriggerPlayableInput input)
        {
            if (!SubmitInput(in input))
                return new GameplayTriggerPlayableTickResult(false, Snapshot);
            return Tick();
        }

        /// <summary>Samples the canonical Runtime/Gameplay/Trigger/Story/Demo authority hash.</summary>
        /// <remarks>LowFreqAlloc. Keep outside measured per-frame authority paths.</remarks>
        public long ComputeHash()
        {
            ThrowIfDisposed();
            return _composition.ComputeAuthorityHash();
        }

        /// <summary>Captures the latest successful, quiescent end-of-Tick into the in-memory slot.</summary>
        /// <remarks>AllocByDesign.</remarks>
        public GameplayTriggerPlayableControlResult CaptureCheckpoint()
        {
            ThrowIfDisposed();
            bool success = CaptureCheckpointCore();
            return new GameplayTriggerPlayableControlResult(
                success,
                Decorate(
                    _composition.Snapshot,
                    GameplayTriggerPlayableIds.CaptureCommand));
        }

        /// <summary>Restores the in-memory slot into a fresh candidate composition and publishes on success.</summary>
        /// <remarks>AllocByDesign.</remarks>
        public GameplayTriggerPlayableControlResult RestoreFresh()
        {
            ThrowIfDisposed();
            bool success = RestoreFreshCore();
            return new GameplayTriggerPlayableControlResult(
                success,
                Decorate(
                    _composition.Snapshot,
                    GameplayTriggerPlayableIds.RestoreCommand));
        }

        /// <summary>Restores one caller-owned immutable in-memory document into a fresh candidate.</summary>
        /// <remarks>
        /// AllocByDesign. Performs no IO and does not represent durable product persistence.
        /// Failure preserves the live composition, input latch, and current valid in-memory slot.
        /// </remarks>
        public GameplayTriggerPlayableControlResult RestoreFresh(
            RuntimeSaveState checkpoint)
        {
            ThrowIfDisposed();
            bool success = RestoreFreshCore(checkpoint);
            return new GameplayTriggerPlayableControlResult(
                success,
                Decorate(
                    _composition.Snapshot,
                    GameplayTriggerPlayableIds.RestoreCommand));
        }

        /// <summary>Publishes a brand-new default composition and clears the in-memory slot.</summary>
        /// <remarks>AllocByDesign.</remarks>
        public GameplayTriggerPlayableControlResult ResetFresh()
        {
            ThrowIfDisposed();
            bool success = ResetFreshCore();
            return new GameplayTriggerPlayableControlResult(
                success,
                Decorate(
                    _composition.Snapshot,
                    GameplayTriggerPlayableIds.ResetCommand));
        }

        /// <remarks>LowFreqAlloc only on lifecycle error paths.</remarks>
        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            _composition.Dispose();
        }

        private void ProcessControl(int controlCommandId)
        {
            switch (controlCommandId)
            {
                case GameplayTriggerPlayableIds.CaptureCommand:
                    CaptureCheckpointCore();
                    break;
                case GameplayTriggerPlayableIds.RestoreCommand:
                    RestoreFreshCore();
                    break;
                case GameplayTriggerPlayableIds.ResetCommand:
                    ResetFreshCore();
                    break;
            }
        }

        private bool CaptureCheckpointCore()
        {
            if (!_composition.CheckpointAccepted)
            {
                _lastControlSucceeded = false;
                return false;
            }
            RuntimeSaveStateCoordinatorResult<RuntimeSaveState> capture =
                _composition.CaptureCheckpoint();
            _lastControlSucceeded = capture.Success;
            if (capture.Success)
                _checkpoint = capture.Value;
            return capture.Success;
        }

        private bool RestoreFreshCore()
        {
            if (_checkpoint == null)
            {
                _lastControlSucceeded = false;
                return false;
            }
            return RestoreFreshCore(_checkpoint);
        }

        private bool RestoreFreshCore(RuntimeSaveState checkpoint)
        {
            _candidateSwapInProgress = true;
            GameplayTriggerPlayableComposition candidate = null;
            try
            {
                RuntimeSaveStateRestoreProfile candidateProfile =
                    _composition.RestoreProfile.CreateSibling();
                candidate = new GameplayTriggerPlayableComposition(
                    candidateProfile,
                    IsCandidateSwapInProgress);
                RuntimeSaveStateResult<RuntimeSaveStateRestorePlan> prepare =
                    candidateProfile.Prepare(checkpoint);
                if (!prepare.Success)
                {
                    _lastControlSucceeded = false;
                    return false;
                }

                RuntimeSaveStateCoordinatorResult<bool> restore =
                    candidate.RestorePrepared(prepare.Value);
                if (!restore.Success)
                {
                    _lastControlSucceeded = false;
                    return false;
                }

                candidate.ComputeAuthorityHash();

                GameplayTriggerPlayableComposition previous = _composition;
                _composition = candidate;
                candidate = null;
                previous.Dispose();
                _checkpoint = checkpoint;
                _inputLatch.Clear();
                _lastControlSucceeded = true;
                return true;
            }
            finally
            {
                candidate?.Dispose();
                _candidateSwapInProgress = false;
            }
        }

        private bool ResetFreshCore()
        {
            _candidateSwapInProgress = true;
            GameplayTriggerPlayableComposition candidate = null;
            try
            {
                candidate = new GameplayTriggerPlayableComposition(
                    restoreProfile: null,
                    isCandidateSwapInProgress: IsCandidateSwapInProgress);
                GameplayTriggerPlayableComposition previous = _composition;
                _composition = candidate;
                candidate = null;
                previous.Dispose();
                _checkpoint = null;
                _inputLatch.Clear();
                _lastControlSucceeded = true;
                return true;
            }
            finally
            {
                candidate?.Dispose();
                _candidateSwapInProgress = false;
            }
        }

        private GameplayTriggerPlayableSnapshot Decorate(
            GameplayTriggerPlayableSnapshot snapshot,
            int lastControlCommandId)
        {
            return snapshot.WithSessionState(
                lastControlCommandId,
                _checkpoint != null,
                _checkpoint != null ? _checkpoint.Frame : -1L,
                _lastControlSucceeded);
        }

        private bool IsCandidateSwapInProgress()
        {
            return _candidateSwapInProgress;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(GameplayTriggerPlayableSession));
        }
    }
}
