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
            return matched
                ? AudioResult.Ok()
                : AudioResult.Fail(AudioErrorCode.RequestRejected, "Unknown Audio Bank group: " + group + ".");
        }

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
                if (bank.Scope != scope || _references[i] == 0)
                    continue;
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
                    Release(i, bank);
            }
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
