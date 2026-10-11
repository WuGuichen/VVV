using System.Collections.Generic;
using UnityEngine;

namespace MxFramework.Editor.Levels
{
    /// <summary>Result of an explicit save, including whether persistent data has committed.</summary>
    public enum MxLevelEditorSaveOutcome
    {
        NotCommitted,
        Committed,
        CommittedWithTailFailure
    }

    /// <summary>
    /// Declares serialized objects changed by SavePreparedChanges so a pre-commit failure
    /// restores in-memory caches together with files. Allocation behavior: LowFreqAlloc.
    /// </summary>
    public interface IMxLevelEditorSceneSaveSnapshotProvider
    {
        void CollectSceneSaveObjects(List<Object> output);
    }
}
