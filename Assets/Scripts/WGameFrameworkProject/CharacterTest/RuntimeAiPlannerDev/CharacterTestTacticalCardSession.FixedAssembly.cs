using System;
using MxFramework.Runtime;
using MxFramework.Game.CharacterTest.TacticalCard;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestTacticalCardBinding
    {
        internal CharacterTestTacticalCardBinding CreateDetached() => new CharacterTestTacticalCardBinding(_inputs);
        internal bool IsCurrentPublished() => IsExecuting && _publishedInputs != null
            && ValidateCurrentFingerprint(_compiled?.SourceFingerprint);
    }

    internal sealed partial class CharacterTestTacticalCardSession
    {
        /// <remarks>NoAlloc. Command continuations may finish cleanup after the session's pre-frame Tick.</remarks>
        internal void ReconcileFixedHostTerminal()
        { if (_host != null) ReleaseTerminal(); }

        internal bool TryPrepareFixedPublished(string name, string fingerprint,
            CharacterTestRuntimeAiLabCompiledLogicProgram program, CharacterTestTacticalCardInputs inputs,
            ICharacterTestTacticalCardInputSource source, CharacterTestRuntimeAiPlannerFactState facts,
            RuntimeFrame frame, out PreparedFixedStart prepared, out string issue)
        {
            prepared = null; issue = "fixed-host:ContextUnavailable";
            var entity = FindEntity();
            if (_binding.IsExecuting || entity == null || facts == null || !CanDrive(entity)) return false;
            var binding = _binding.CreateDetached();
            if (!binding.TryCompilePublished(name, fingerprint, program, inputs, source)
                || !binding.TryAcquirePublished(out var compiled)) { issue = binding.LastIssue; return false; }
            var host = CreateHost(compiled);
            if (!host.Start(entity, facts, frame)) { issue = host.LastFailureReason; return false; }
            prepared = new PreparedFixedStart(this, binding, host, entity);
            issue = string.Empty; return true;
        }

        internal sealed class PreparedFixedStart
        {
            private readonly CharacterTestTacticalCardSession _owner;
            private readonly CharacterTestTacticalCardBinding _oldBinding, _binding;
            private readonly CharacterTestBehaviorExecutionHost _oldHost, _host;
            private readonly CharacterTestCharacterEntity _actor;
            private bool _published;
            internal PreparedFixedStart(CharacterTestTacticalCardSession owner, CharacterTestTacticalCardBinding binding,
                CharacterTestBehaviorExecutionHost host, CharacterTestCharacterEntity actor)
            { _owner = owner; _oldBinding = owner._binding; _oldHost = owner._host; _binding = binding; _host = host; _actor = actor; }
            internal bool IsCurrent => !_published && ReferenceEquals(_owner._binding, _oldBinding)
                && ReferenceEquals(_owner._host, _oldHost) && !_oldBinding.IsExecuting
                && ReferenceEquals(_owner.FindEntity(), _actor) && _actor.CombatState.IsAlive && _binding.IsCurrentPublished();
            internal void Publish(TacticalFixedCardPaymentCommit payment)
            {
                // Fingerprint validation is cold and already checked by the joint owner before payment.
                if (_published || !ReferenceEquals(_owner._binding, _oldBinding) || !payment.IsPaid)
                    throw new InvalidOperationException("fixed-joint:HostParticipantChanged");
                _host.BindFixedPayment(payment); _owner._binding = _binding; _owner._host = _host;
                _owner.LastIssue = string.Empty; _published = true;
            }
        }
    }

    internal sealed partial class CharacterTestBehaviorExecutionHost
    {
        private TacticalFixedCardPaymentCommit _fixedPayment;
        internal void BindFixedPayment(TacticalFixedCardPaymentCommit payment)
        { _fixedPayment = payment; _runtime.FixedPayment = payment; }
    }
}
