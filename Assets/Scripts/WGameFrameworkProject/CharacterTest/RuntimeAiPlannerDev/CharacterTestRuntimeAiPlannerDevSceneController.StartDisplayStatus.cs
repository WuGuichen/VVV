using System.Text;
using System.Globalization;

namespace MxFramework.Game.CharacterTest
{
    public sealed partial class CharacterTestRuntimeAiPlannerDevSceneController
    {
        // Rendering consumes a receipt. Only explicit start/save/test commands execute preflight.
        private string _lastStartCheckKey, _lastStartCheckIssue;
        private bool _lastStartCheckValid;
        internal bool TryGetSessionStartStatusForDisplay(out string diagnostic, out bool pending)
        {
            pending = _lastStartCheckKey != StartCheckKey();
            diagnostic = pending ? string.Empty : _lastStartCheckIssue;
            return pending || _lastStartCheckValid;
        }
        private string StartCheckKey()
        {
            var key = new StringBuilder();
            key.Append(DraftWorkspace?.WorkspaceId).Append(':').Append(DraftWorkspace?.CurrentRevision ?? -1)
                .Append(':').Append(_selectedSetupTeamId).Append(':').Append(_selectedSetupSlotIndex)
                .Append(':').Append(CharacterTestTacticalPreparationCatalog.Revision).Append(':').Append(ProjectCatalogState?.GetHashCode() ?? 0);
            for (int team = 1; team <= 2; team++) for (int slot = 0; slot < CharacterTestArenaMatchConfig.SlotsPerTeam; slot++)
            {
                var configured = _matchConfig?.TryGetSlot(team, slot);
                if (configured == null) continue;
                key.Append('|').Append(configured.Enabled).Append(':').Append(configured.CharacterId).Append(':').Append(configured.LoadoutId)
                    .Append(':').Append(configured.VariantId).Append(':').Append(configured.BehaviorGroupDraftId)
                    .Append(':').Append(TacticalTestAction(team, slot, configured.CharacterId));
                if (_matchConfig.TryGetSpawnPosition(team, slot, out var position))
                    key.Append(':').Append(position.x.ToString("R", CultureInfo.InvariantCulture)).Append(':').Append(position.y.ToString("R", CultureInfo.InvariantCulture));
            }
            return key.ToString();
        }
    }
}
