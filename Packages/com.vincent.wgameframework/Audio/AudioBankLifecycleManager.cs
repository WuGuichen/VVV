using System;

namespace MxFramework.Audio
{
    public readonly struct AudioBankLifecycleSnapshot
    {
        public AudioBankLifecycleSnapshot(int loadedBanks, int totalReferences, int loadFailures, int unloadFailures)
        {
            LoadedBanks = loadedBanks;
            TotalReferences = totalReferences;
            LoadFailures = loadFailures;
            UnloadFailures = unloadFailures;
        }

        public int LoadedBanks { get; }
        public int TotalReferences { get; }
        public int LoadFailures { get; }
        public int UnloadFailures { get; }
    }

    public readonly struct AudioBankRuntimeState
    {
        public AudioBankRuntimeState(string stableName, string group, AudioBankScope scope, int references, bool required)
        {
            StableName = stableName ?? string.Empty;
            Group = group ?? string.Empty;
            Scope = scope;
            References = references < 0 ? 0 : references;
            Required = required;
        }

        public string StableName { get; }
        public string Group { get; }
        public AudioBankScope Scope { get; }
        public int References { get; }
        public bool Required { get; }
        public bool Loaded => References > 0;
    }

    /// <summary>Owns manifest-driven Bank warmup, reference counts and scope unload.</summary>
    public sealed class AudioBankLifecycleManager : IDisposable
    {
        private readonly AudioBankManifest _manifest;
        private readonly IAudioBankBackend _backend;
        private readonly int[] _references;
        // Successful group ownership is retained even while an optional Bank is unavailable.
        private readonly int[] _groupReferences;
        private int _loadedBanks;
        private int _totalReferences;
        private int _loadFailures;
        private int _unloadFailures;

        /// <remarks>AllocByDesign during startup composition.</remarks>
        public AudioBankLifecycleManager(AudioBankManifest manifest, IAudioBankBackend backend)
        {
            _manifest = manifest ?? throw new ArgumentNullException(nameof(manifest));
            _backend = backend ?? throw new ArgumentNullException(nameof(backend));
            _references = new int[manifest.Banks.Count];
            _groupReferences = new int[manifest.Banks.Count];
        }

        public AudioBankLifecycleSnapshot Snapshot => new AudioBankLifecycleSnapshot(
            _loadedBanks,
            _totalReferences,
            _loadFailures,
            _unloadFailures);

        /// <remarks>AllocByDesign diagnostics path.</remarks>
        public AudioBankRuntimeState[] CaptureDebugStates()
        {
            var states = new AudioBankRuntimeState[_manifest.Banks.Count];
            for (int i = 0; i < states.Length; i++)
            {
                AudioBankDefinition bank = _manifest.Banks[i];
                states[i] = new AudioBankRuntimeState(
                    bank.StableName,
                    bank.Group,
                    bank.Scope,
                    _references[i],
                    bank.Required);
            }
            return states;
        }

        /// <summary>Acquires one group hold; recovered optional Banks serve every active hold.</summary>
        /// <remarks>LowFreqAlloc at the external backend boundary.</remarks>
        public AudioResult WarmupGroup(string group)
        {
            if (string.IsNullOrWhiteSpace(group))
                return AudioResult.Fail(AudioErrorCode.RequestRejected, "Audio Bank group is required.");

            bool matched = false;
            for (int i = 0; i < _manifest.Banks.Count; i++)
            {
                AudioBankDefinition bank = _manifest.Banks[i];
                if (!string.Equals(bank.Group, group, StringComparison.Ordinal))
                    continue;
                matched = true;
                AudioResult result = Acquire(i, bank);
                if (result.Failed && bank.Required)
                {
                    RollbackWarmup(group, i - 1);
                    return result;
                }
            }
            if (!matched)
                return AudioResult.Fail(AudioErrorCode.RequestRejected, "Unknown Audio Bank group: " + group + ".");

            // Commit ownership only after all required Banks have been acquired. A recovered
            // optional Bank also belongs to earlier holds whose warmup could not load it.
            for (int i = 0; i < _manifest.Banks.Count; i++)
            {
                if (!string.Equals(_manifest.Banks[i].Group, group, StringComparison.Ordinal))
                    continue;
                _groupReferences[i]++;
                if (_references[i] > 0)
                    SetReferences(i, _groupReferences[i]);
            }
            return AudioResult.Ok();
        }

        /// <summary>Releases one group hold without imposing an acquisition order.</summary>
        /// <remarks>LowFreqAlloc at the external backend boundary.</remarks>
        public AudioResult ReleaseGroup(string group)
        {
            if (string.IsNullOrWhiteSpace(group))
                return AudioResult.Fail(AudioErrorCode.RequestRejected, "Audio Bank group is required.");
            bool matched = false;
            AudioResult firstFailure = AudioResult.Ok();
            for (int i = _manifest.Banks.Count - 1; i >= 0; i--)
            {
                AudioBankDefinition bank = _manifest.Banks[i];
                if (!string.Equals(bank.Group, group, StringComparison.Ordinal))
                    continue;
                matched = true;
                if (_groupReferences[i] > 0)
                    _groupReferences[i]--;
                if (_references[i] == 0)
                    continue;
                // Keep a failed unload as one retryable reference, independent of group holds.
                SetReferences(i, Math.Max(1, _groupReferences[i]));
                if (_groupReferences[i] > 0)
                    continue;
                AudioResult result = Release(i, bank);
                if (result.Failed && firstFailure.Success)
                    firstFailure = result;
            }
            if (!matched)
                return AudioResult.Fail(AudioErrorCode.RequestRejected, "Unknown Audio Bank group: " + group + ".");
            return firstFailure;
        }

        /// <remarks>LowFreqAlloc at the external backend boundary.</remarks>
        public int UnloadScope(AudioBankScope scope)
        {
            int unloaded = 0;
            for (int i = _manifest.Banks.Count - 1; i >= 0; i--)
            {
                AudioBankDefinition bank = _manifest.Banks[i];
                if (bank.Scope != scope)
                    continue;
                _groupReferences[i] = 0;
                int references = _references[i];
                for (int reference = 0; reference < references; reference++)
                {
                    if (Release(i, bank).Success && _references[i] == 0)
                        unloaded++;
                }
            }
            return unloaded;
        }

        public void Dispose()
        {
            UnloadScope(AudioBankScope.Scene);
            UnloadScope(AudioBankScope.AppPersistent);
        }

        private AudioResult Acquire(int index, in AudioBankDefinition bank)
        {
            if (_references[index] == 0)
            {
                AudioResult load = _backend.LoadBank(bank.StableName, bank.PreloadSamples);
                if (load.Failed)
                {
                    _loadFailures++;
                    return load;
                }
                _loadedBanks++;
            }
            _references[index]++;
            _totalReferences++;
            return AudioResult.Ok();
        }

        private void RollbackWarmup(string group, int lastIndex)
        {
            for (int i = lastIndex; i >= 0; i--)
            {
                AudioBankDefinition bank = _manifest.Banks[i];
                if (string.Equals(bank.Group, group, StringComparison.Ordinal))
                {
                    Release(i, bank);
                    // If rollback cannot unload a newly recovered Bank, existing group holds
                    // can use that retained Bank; the failed warmup itself owns no hold.
                    if (_references[i] > 0)
                        SetReferences(i, Math.Max(1, _groupReferences[i]));
                }
            }
        }

        private void SetReferences(int index, int references)
        {
            _totalReferences += references - _references[index];
            _references[index] = references;
        }

        private AudioResult Release(int index, in AudioBankDefinition bank)
        {
            if (_references[index] == 0)
                return AudioResult.Ok();
            if (_references[index] == 1)
            {
                AudioResult unload = _backend.UnloadBank(bank.StableName);
                if (unload.Failed)
                {
                    _unloadFailures++;
                    return unload;
                }
                _loadedBanks--;
            }
            _references[index]--;
            _totalReferences--;
            return AudioResult.Ok();
        }
    }
}
