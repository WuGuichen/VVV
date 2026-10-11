using System.Collections.Generic;
using UnityEngine.UIElements;
namespace MxFramework.Editor.Levels
{
    /// <summary>Optional multi-selection inspector. Allocation behavior: AllocByDesign; Editor UI rebuilds only.</summary>
    public interface IMxLevelEditorBatchSelectionModule
    {
        void BuildBatchInspector(MxLevelEditorContext context, IReadOnlyList<MxLevelEditorListRow> rows, VisualElement inspector);
    }
}
