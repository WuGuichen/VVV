using System;
using System.IO;
using UnityEngine;

namespace MxFramework.Game.CharacterTest.Editor
{
    internal static partial class CharacterTestRuntimeAiPlannerProjectCatalogEditorBridge
    {
        private static Func<CharacterTestRuntimeAiPlannerProjectCatalogState> _combatDesignFixtureReload;

        /// <summary>Reuses the durable catalog owner for an explicitly isolated acceptance fixture.</summary>
        /// <remarks>LowFreqAlloc: synchronous Editor main-thread authoring operation; never scopes production assets.</remarks>
        internal static IDisposable UseCombatDesignFixtureCatalog(string fixtureRoot,
            Func<CharacterTestRuntimeAiPlannerProjectCatalogState> reload)
        {
            string allowed = Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/MxFramework/CombatDesign/fixtures"));
            string actual = Path.GetFullPath(fixtureRoot);
            if (!IsPathWithinRoot(actual, allowed) || Path.GetDirectoryName(actual) != allowed || reload == null)
                throw new ArgumentException("Combat design catalog scope requires a direct isolated fixture directory.");
            if (_combatDesignFixtureReload != null) throw new InvalidOperationException("A fixture catalog operation is already active.");
            return new CombatDesignFixtureCatalogScope(actual, reload);
        }

        internal static CharacterTestRuntimeAiPlannerProjectCatalogWriteResult WriteCombatDesignFixture(
            CharacterTestRuntimeAiPlannerProjectCatalogWriteRequest request)
        {
            if (_combatDesignFixtureReload == null) throw new InvalidOperationException("Isolated catalog scope is required.");
            // Mono/Windows can fail when Replace encounters an already existing legacy backup.
            // The durable transaction journal owns rollback; retain legacy snapshots in an archive before replacement.
            string recovery = GetEffectiveRecoveryRoot();
            foreach (string name in new[] { CharacterTestRuntimeAiPlannerProjectCatalogLoader.CardCatalogFileName,
                CharacterTestRuntimeAiPlannerProjectCatalogLoader.GroupCatalogFileName })
            {
                string previous = Path.Combine(recovery, name + ".previous");
                if (!File.Exists(previous)) continue;
                string archive = Path.Combine(recovery, "LegacyPrevious", Guid.NewGuid().ToString("N") + ".previous");
                Directory.CreateDirectory(Path.GetDirectoryName(archive));
                File.Move(previous, archive);
            }
            return Write(request);
        }

        internal static void RecoverCombatDesignFixture()
        {
            if (_combatDesignFixtureReload == null) throw new InvalidOperationException("Isolated catalog scope is required.");
            if (!TryRecoverPendingTransactions(out _, out string issue)) throw new InvalidOperationException(issue);
        }

        private sealed class CombatDesignFixtureCatalogScope : IDisposable
        {
            private readonly IDisposable _paths;
            internal CombatDesignFixtureCatalogScope(string root, Func<CharacterTestRuntimeAiPlannerProjectCatalogState> reload)
            {
                // The existing path scope already suppresses production AssetDatabase refreshes and validates journal paths.
                _paths = new ProjectCatalogRecoveryTestScope(Path.Combine(root, "StreamingAssets"), Path.Combine(root, "CatalogRecovery"), null);
                _combatDesignFixtureReload = reload;
            }
            public void Dispose() { _combatDesignFixtureReload = null; _paths.Dispose(); }
        }
    }
}
