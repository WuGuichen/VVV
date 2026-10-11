using System;
using Newtonsoft.Json;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>Rebuilds the relevant identity from the currently loaded providers at a low-frequency boundary.</summary>
    internal interface ICharacterTestTacticalCardInputSource
    {
        bool TryPrepare(out CharacterTestTacticalCardInputs inputs, out string issue);
        bool TryPrepare(int actionId, out CharacterTestTacticalCardInputs inputs, out string issue);
    }

    internal readonly struct CharacterTestTacticalCardBindingState
    {
        internal const int CurrentVersion = 2;
        [JsonConstructor]
        public CharacterTestTacticalCardBindingState(int version, long programIdentity, RuntimeHashDigest sourceFingerprint,
            RuntimeHashDigest dependencyFingerprint, bool executing, int dependencyKind = 0, int dependencyCharacterId = 0)
        {
            Version = version;
            ProgramIdentity = programIdentity;
            SourceFingerprint = sourceFingerprint;
            DependencyFingerprint = dependencyFingerprint;
            Executing = executing;
            DependencyKind = dependencyKind;
            DependencyCharacterId = dependencyCharacterId;
        }

        public int Version { get; }
        public long ProgramIdentity { get; }
        public RuntimeHashDigest SourceFingerprint { get; }
        public RuntimeHashDigest DependencyFingerprint { get; }
        public int DependencyKind { get; }
        public int DependencyCharacterId { get; }
        public bool Executing { get; }
    }

    /// <summary>Owns one character's atomic card binding; retained immutable programs do not grant startup permission.</summary>
    /// <remarks>Preparation, compile, start validation and restore are LowFreqAlloc. This class is never polled by Tick.</remarks>
    internal sealed class CharacterTestTacticalCardBinding
    {
        private readonly ICharacterTestTacticalCardInputSource _inputs;
        private CharacterTestTacticalCardCompilation _compiled;
        private ICharacterTestTacticalCardInputSource _publishedInputs;

        internal bool TryCompilePublished(string name, string fingerprint, CharacterTestRuntimeAiLabCompiledLogicProgram program,
            CharacterTestTacticalCardInputs inputs, ICharacterTestTacticalCardInputSource currentInputs)
        {
            if (IsExecuting) return Reject("binding:ExecutingRecompileForbidden");
            if (program == null || inputs == null || currentInputs == null) return Reject("binding:PublishedInputsMissing");
            if (!inputs.TryValidatePreparedAction(out string issue)) return Reject(issue);
            _compiled = new CharacterTestTacticalCardCompilation(name, fingerprint, inputs, program, null);
            _publishedInputs = currentInputs;
            LastIssue = string.Empty; return true;
        }

        internal bool TryAcquirePublished(out CharacterTestTacticalCardCompilation compilation)
        {
            compilation = null;
            if (IsExecuting) return Reject("binding:AlreadyExecuting");
            if (_publishedInputs == null || !ValidateCurrentFingerprint(_compiled?.SourceFingerprint)) return false;
            IsExecuting = true; compilation = _compiled; return true;
        }

        internal CharacterTestTacticalCardBinding(ICharacterTestTacticalCardInputSource inputs)
        {
            _inputs = inputs ?? throw new ArgumentNullException(nameof(inputs));
        }

        internal CharacterTestTacticalCardCompilation RetainedCompilation => _compiled;
        internal bool IsExecuting { get; private set; }
        internal string LastIssue { get; private set; } = string.Empty;

        internal bool TryCompile(CharacterTestTacticalCardDefinition source, int actionId = 0)
        {
            if (IsExecuting) return Reject("binding:ExecutingRecompileForbidden");
            if (source == null) return Reject("source:Missing");
            bool prepared = actionId > 0
                ? _inputs.TryPrepare(actionId, out CharacterTestTacticalCardInputs inputs, out string issue)
                : _inputs.TryPrepare(out inputs, out issue);
            if (!prepared) return Reject(issue);
            if (!CharacterTestTacticalCardCompiler.TryCompile(source, inputs,
                    out CharacterTestTacticalCardCompilation candidate, out issue)) return Reject(issue);
            _compiled = candidate;
            _publishedInputs = null;
            LastIssue = string.Empty;
            return true;
        }

        /// <summary>Compiles editable authoring through the same per-character binding owner.</summary>
        internal bool TryCompileAuthoring(CharacterTestTacticalAuthoringSource source)
        {
            if (IsExecuting) return Reject("binding:ExecutingRecompileForbidden");
            var validation = CharacterTestTacticalAuthoringCompiler.Validate(source);
            if (validation.Count > 0) return Reject(validation[0].ToString());
            if (!_inputs.TryPrepare(out CharacterTestTacticalCardInputs inputs, out string issue)) return Reject(issue);
            if (!CharacterTestTacticalAuthoringCompiler.TryCompile(source, inputs, out var candidate, out var diagnostics))
                return Reject(diagnostics.Count > 0 ? diagnostics[0].ToString() : "binding:CompilationUnavailable");
            _compiled = new CharacterTestTacticalCardCompilation(source, inputs, candidate);
            _publishedInputs = null;
            LastIssue = string.Empty;
            return true;
        }

        internal bool TryAcquireAuthoring(CharacterTestTacticalAuthoringSource source, out CharacterTestTacticalCardCompilation compilation)
        {
            compilation = null;
            if (IsExecuting) return Reject("binding:AlreadyExecuting");
            if (!TryGetAuthoringFingerprint(source, out string fingerprint) || !ValidateCurrentFingerprint(fingerprint)) return false;
            IsExecuting = true;
            compilation = _compiled;
            return true;
        }

        internal bool TryValidateAuthoringRestore(CharacterTestTacticalAuthoringSource source, in CharacterTestTacticalCardBindingState state)
        {
            if (IsExecuting) return Reject("binding:StopBeforeRestore");
            if (state.Version != CharacterTestTacticalCardBindingState.CurrentVersion) return Reject("binding:SnapshotVersionMismatch");
            if (!TryGetAuthoringFingerprint(source, out string fingerprint) || !ValidateCurrentFingerprint(fingerprint)) return false;
            if (state.ProgramIdentity != _compiled.Program.Execution.Identity
                || !MatchesPreparedIdentity(state))
                return Reject("binding:SnapshotIdentityMismatch");
            return true;
        }

        private bool TryGetAuthoringFingerprint(CharacterTestTacticalAuthoringSource source, out string fingerprint)
        {
            fingerprint = null;
            if (source == null) return Reject("source:Missing");
            try { fingerprint = CharacterTestTacticalAuthoringJson.SemanticFingerprint(source); return true; }
            catch (JsonException error) { return Reject("source:Invalid:" + error.Message); }
        }

        /// <summary>Acquires a validated binding before handing it to the formal execution host.</summary>
        /// <remarks>The host must Release after startup rejection, terminal execution or explicit stop.</remarks>
        internal bool TryAcquire(CharacterTestTacticalCardDefinition currentSource,
            out CharacterTestTacticalCardCompilation compilation)
        {
            compilation = null;
            if (IsExecuting) return Reject("binding:AlreadyExecuting");
            if (!ValidateCurrent(currentSource)) return false;
            IsExecuting = true;
            compilation = _compiled;
            return true;
        }

        /// <remarks>NoAlloc. Called by the formal execution host at its lifecycle boundary.</remarks>
        internal void Release() => IsExecuting = false;

        internal CharacterTestTacticalCardBindingState CaptureState() => new CharacterTestTacticalCardBindingState(
            CharacterTestTacticalCardBindingState.CurrentVersion, _compiled?.Program.Execution.Identity ?? 0,
            _compiled?.SourceFingerprintDigest ?? default, _compiled?.DependencyFingerprintDigest ?? default, IsExecuting,
            _compiled?.DependencyKind ?? 0, _compiled?.DependencyCharacterId ?? 0);

        /// <summary>Checks a saved binding against current inputs; the host separately validates/restores the execution snapshot.</summary>
        internal bool TryValidateRestore(CharacterTestTacticalCardDefinition currentSource,
            in CharacterTestTacticalCardBindingState state)
        {
            if (IsExecuting) return Reject("binding:StopBeforeRestore");
            if (state.Version != CharacterTestTacticalCardBindingState.CurrentVersion)
                return Reject("binding:SnapshotVersionMismatch");
            if (!ValidateCurrent(currentSource)) return false;
            if (state.ProgramIdentity != _compiled.Program.Execution.Identity
                || !MatchesPreparedIdentity(state))
                return Reject("binding:SnapshotIdentityMismatch");
            return true;
        }

        private bool MatchesPreparedIdentity(in CharacterTestTacticalCardBindingState state) =>
            state.SourceFingerprint.Equals(_compiled.SourceFingerprintDigest)
            && state.DependencyFingerprint.Equals(_compiled.DependencyFingerprintDigest)
            && state.DependencyKind == _compiled.DependencyKind
            && state.DependencyCharacterId == _compiled.DependencyCharacterId;

        private bool ValidateCurrent(CharacterTestTacticalCardDefinition source)
            => ValidateCurrentFingerprint(source?.SemanticFingerprint);

        private bool ValidateCurrentFingerprint(string fingerprint)
        {
            if (_compiled == null) return Reject("binding:NotCompiled");
            if (fingerprint == null || !string.Equals(fingerprint, _compiled.SourceFingerprint, StringComparison.Ordinal))
                return Reject("binding:SourceChanged");
            int actionId = _compiled.ActionId;
            bool prepared = _publishedInputs != null
                ? _publishedInputs.TryPrepare(out CharacterTestTacticalCardInputs current, out string issue)
                : actionId > 0
                ? _inputs.TryPrepare(actionId, out current, out issue)
                : _inputs.TryPrepare(out current, out issue);
            if (!prepared) return Reject(issue);
            if (!string.Equals(current.DependencyFingerprint, _compiled.DependencyFingerprint, StringComparison.Ordinal))
                return Reject("binding:DependenciesChanged");
            LastIssue = string.Empty;
            return true;
        }

        private bool Reject(string issue)
        {
            LastIssue = string.IsNullOrEmpty(issue) ? "binding:InputsUnavailable" : issue;
            return false;
        }
    }
}
