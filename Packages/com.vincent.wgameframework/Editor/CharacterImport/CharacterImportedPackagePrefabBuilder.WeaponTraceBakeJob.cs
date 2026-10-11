using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using MxFramework.CharacterAction;
using MxFramework.CharacterRuntimeSpawn;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Editor.CharacterImport
{
    public static partial class CharacterImportedPackagePrefabBuilder
    {
        /// <summary>LowFreqAlloc: creates a main-thread sampling job; output is published only through Publish after successful completion.</summary>
        public static WeaponTraceBakeJob CreateWeaponTraceBakeJob(string importedPackageRoot, bool prepareWeaponPrefabs = true)
        {
            string root = NormalizeProjectPath(importedPackageRoot);
            CharacterImportedPackage package = LoadImportedPackage(root, "incremental weapon/contact sampling");
            if (package == null) throw new InvalidDataException("Imported character package could not be loaded: " + root);
            if (!TryLoadCharacterActionRegistryForPackage(package, out CharacterActionConfigRegistry registry, out string issue))
                throw new InvalidDataException(issue);
            return new WeaponTraceBakeJob(new WeaponTraceSamplingState(package,
                CreateWeaponTraceBakeRequests(package, registry), prepareWeaponPrefabs));
        }

        internal sealed class WeaponTraceSamplingState
        {
            internal WeaponTraceSamplingState(CharacterImportedPackage package, CharacterWeaponTraceBakeRequest[] requests,
                bool prepareWeaponPrefabs, bool includeContacts = true)
            {
                Package = package;
                Requests = requests ?? Array.Empty<CharacterWeaponTraceBakeRequest>();
                ContactRequests = includeContacts ? CreateBodyPartContactMotionBakeRequests(package) : Array.Empty<BodyPartContactMotionBakeRequest>();
                PrepareWeaponPrefabs = prepareWeaponPrefabs;
            }
            internal readonly CharacterImportedPackage Package;
            internal readonly CharacterWeaponTraceBakeRequest[] Requests;
            internal readonly BodyPartContactMotionBakeRequest[] ContactRequests;
            internal readonly bool PrepareWeaponPrefabs;
            internal readonly CharacterWeaponTraceBakeCatalog Weapons = new();
            internal readonly CharacterWeaponTraceBakeCatalog Contacts = new();
            internal readonly List<CharacterWeaponTraceBakeBuildIssue> Issues = new();
            internal readonly Dictionary<string, AnimationClip> Clips = new(StringComparer.Ordinal);
            internal CharacterWeaponTraceBakeBuildReport Report;
        }

        /// <summary>Editor-only bounded sampling. Dispose cancels and releases the preview scene, equipment and PlayableGraph.</summary>
        public sealed class WeaponTraceBakeJob : IDisposable
        {
            private readonly WeaponTraceSamplingState _state;
            private IEnumerator<object> _routine;
            private bool _disposed;
            private bool _published;

            internal WeaponTraceBakeJob(WeaponTraceSamplingState state)
            {
                _state = state;
                _routine = SampleWeaponTraceBakeRoutine(state).GetEnumerator();
            }

            public bool IsComplete { get; private set; }
            public int ProcessedWorkUnits { get; private set; }
            public CharacterWeaponTraceBakeBuildReport Report => _state.Report;
            public CharacterWeaponTraceBakeCatalog WeaponCatalog => _state.Weapons;
            public CharacterWeaponTraceBakeCatalog ContactCatalog => _state.Contacts;

            /// <summary>LowFreqAlloc: processes at most maxWorkUnits frame/preflight units, stopping at the elapsed budget between units.</summary>
            /// <remarks>Unity asset loading, preview initialization and publication are indivisible; the budget bounds sampling between those operations.</remarks>
            public bool Step(int maxWorkUnits = 64, double budgetMilliseconds = 8)
            {
                if (_disposed) throw new ObjectDisposedException(nameof(WeaponTraceBakeJob));
                if (maxWorkUnits <= 0) throw new ArgumentOutOfRangeException(nameof(maxWorkUnits));
                if (budgetMilliseconds <= 0) throw new ArgumentOutOfRangeException(nameof(budgetMilliseconds));
                if (IsComplete) return false;
                var watch = Stopwatch.StartNew();
                try
                {
                    for (int count = 0; count < maxWorkUnits; count++)
                    {
                        if (!_routine.MoveNext())
                        {
                            IsComplete = true;
                            _routine.Dispose();
                            _routine = null;
                            return false;
                        }
                        ProcessedWorkUnits++;
                        if (watch.Elapsed.TotalMilliseconds >= budgetMilliseconds) break;
                    }
                    return true;
                }
                catch
                {
                    Dispose();
                    throw;
                }
            }

            /// <summary>LowFreqAlloc: publishes a complete successful result in the existing v3 format; callers must verify source contents first.</summary>
            public void Publish()
            {
                if (_disposed) throw new ObjectDisposedException(nameof(WeaponTraceBakeJob));
                if (!IsComplete || Report == null || !Report.Success)
                    throw new InvalidOperationException("Sampling must complete successfully before publishing.");
                if (_published) return;
                WriteWeaponTraceBakeCatalog(_state.Package, _state.Requests, _state.Weapons, _state.Contacts);
                AssetDatabase.ImportAsset(NormalizeProjectPath(_state.Package.RootPath) + "/"
                    + CharacterWeaponTraceBakeCatalogJson.DefaultRelativePath, ImportAssetOptions.ForceUpdate);
                _published = true;
            }

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                _routine?.Dispose();
                _routine = null;
            }
        }
    }
}
