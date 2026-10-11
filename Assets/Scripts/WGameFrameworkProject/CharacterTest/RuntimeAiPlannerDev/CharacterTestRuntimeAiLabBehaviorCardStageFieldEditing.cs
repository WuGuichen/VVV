using System;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>
    /// Low-frequency identity/epoch gate for one expanded Stage field buffer.
    /// The text buffer remains in the FairyGUI binder; this object only proves
    /// that a delayed submit/blur still belongs to the same Draft authority.
    /// </summary>
    internal sealed class CharacterTestRuntimeAiLabStageFieldEditSession
    {
        private long _epoch;
        private CharacterTestRuntimeAiLabStageEditIdentity _identity;

        internal bool Active { get; private set; }
        internal bool Deactivating { get; private set; }
        internal long Epoch => _epoch;
        internal string StageId { get; private set; } = string.Empty;
        internal CharacterTestRuntimeAiLabStageFieldKind Field { get; private set; }

        internal bool Begin(
            in CharacterTestRuntimeAiLabStageEditIdentity identity,
            string stageId,
            CharacterTestRuntimeAiLabStageFieldKind field,
            out long epoch)
        {
            Cancel(deactivating: false);
            if (!identity.IsValid || string.IsNullOrWhiteSpace(stageId))
            {
                epoch = _epoch;
                return false;
            }
            _identity = identity;
            StageId = stageId.Trim();
            Field = field;
            Active = true;
            Deactivating = false;
            AdvanceEpoch();
            epoch = _epoch;
            return true;
        }

        internal bool CanCommit(
            in CharacterTestRuntimeAiLabStageEditIdentity identity,
            long expectedEpoch,
            string stageId,
            CharacterTestRuntimeAiLabStageFieldKind field)
        {
            return Active
                && !Deactivating
                && expectedEpoch == _epoch
                && _identity.Matches(identity)
                && string.Equals(StageId, stageId?.Trim(), StringComparison.Ordinal)
                && Field == field;
        }

        internal void Complete(long expectedEpoch)
        {
            if (!Active || expectedEpoch != _epoch)
                return;
            Active = false;
            Deactivating = false;
            _identity = default;
            StageId = string.Empty;
            AdvanceEpoch();
        }

        internal void Cancel(bool deactivating)
        {
            if (Active || Deactivating || StageId.Length > 0)
                AdvanceEpoch();
            Active = false;
            Deactivating = deactivating;
            _identity = default;
            StageId = string.Empty;
        }

        internal void Activate()
        {
            Deactivating = false;
        }

        private void AdvanceEpoch()
        {
            _epoch = _epoch == long.MaxValue ? 1L : _epoch + 1L;
        }
    }
}
