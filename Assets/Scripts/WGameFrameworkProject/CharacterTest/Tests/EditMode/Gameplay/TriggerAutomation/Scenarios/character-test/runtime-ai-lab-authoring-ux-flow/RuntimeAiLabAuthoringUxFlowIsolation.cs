using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using MxFramework.Game.CharacterTest;
using UnityEngine;

namespace MxFramework.Tests.Gameplay.TriggerAutomation
{
    internal sealed class RuntimeAiLabAuthoringUxFlowSandbox : IDisposable
    {
        private readonly string _safeRootPrefix;

        internal RuntimeAiLabAuthoringUxFlowSandbox()
        {
            _safeRootPrefix = Path.GetFullPath(Path.Combine(
                Path.GetTempPath(),
                "MxFramework",
                "TriggerTests",
                "RuntimeAiLabAuthoringUxFlow"));
            RootPath = Path.Combine(_safeRootPrefix, Guid.NewGuid().ToString("N"));
            WorkspaceRoot = Path.Combine(RootPath, "Workspace");
            RecoveryRoot = Path.Combine(RootPath, "Recovery");
            RoleRegistryPath = Path.Combine(RootPath, "RoleRegistry", "workspace-roles.json");
            ActiveRolePath = Path.Combine(RootPath, "UserSettings", "active-workspace-role.txt");
            PublishedRoot = Path.Combine(RootPath, "Published");
        }

        internal string RootPath { get; }
        internal string WorkspaceRoot { get; }
        internal string RecoveryRoot { get; }
        internal string RoleRegistryPath { get; }
        internal string ActiveRolePath { get; }
        internal string PublishedRoot { get; }
        internal CharacterTestRuntimeAiLabBehaviorCardReferenceCatalog References { get; private set; }
        internal RuntimeAiLabAuthoringUxFlowPublishedOverride Published { get; private set; }
        internal CharacterTestArenaMatchConfig MatchConfig { get; private set; }
        internal CharacterTestRuntimeAiLabDraftPersistence Persistence { get; private set; }
        internal CharacterTestRuntimeAiLabAuthoringWorkspaceRoles Roles { get; private set; }

        internal bool AllWritableAuthoritiesUseTemp
        {
            get
            {
                string root = Path.GetFullPath(RootPath).TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
                return IsUnderRoot(WorkspaceRoot, root)
                    && IsUnderRoot(RecoveryRoot, root)
                    && IsUnderRoot(RoleRegistryPath, root)
                    && IsUnderRoot(ActiveRolePath, root)
                    && IsUnderRoot(PublishedRoot, root);
            }
        }

        internal bool TryInitialize(out string diagnostic)
        {
            try
            {
                Directory.CreateDirectory(RootPath);
                CharacterTestRuntimeAiPlannerProjectCatalogSeedResult seed =
                    CharacterTestRuntimeAiPlannerProjectCatalogSeed.Create();
                if (!seed.Success)
                {
                    diagnostic = seed.Diagnostic;
                    return false;
                }

                References = new CharacterTestRuntimeAiLabBehaviorCardReferenceCatalog(
                    CharacterTestAbilityCatalogSource.CreateEmbeddedFallback(),
                    CharacterTestCharacterActionConfigSource.Load(
                        CharacterTestCharacterActionConfigSourceOptions
                            .CreateLegacyFixtureFallback()).Registry);
                Published = new RuntimeAiLabAuthoringUxFlowPublishedOverride(
                    PublishedRoot,
                    References);
                if (!Published.TryInitialize(seed.Cards, seed.Groups, out diagnostic))
                    return false;

                MatchConfig = CharacterTestArenaMatchConfig.CreateProjectBacked(Published.State);
                Persistence = new CharacterTestRuntimeAiLabDraftPersistence(
                    WorkspaceRoot,
                    RecoveryRoot);
                Roles = CharacterTestRuntimeAiLabAuthoringWorkspaceRoles.CreateIsolated(
                    Persistence,
                    RoleRegistryPath,
                    ActiveRolePath);
                diagnostic = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                diagnostic = exception.GetType().Name + ": " + exception.Message;
                return false;
            }
        }

        internal bool WorkspaceFileExists(string workspaceId)
        {
            string directory = Path.Combine(WorkspaceRoot, workspaceId ?? string.Empty);
            return Directory.Exists(directory)
                && File.Exists(Path.Combine(directory, "workspace.json"));
        }

        internal string HashWorkspace(string workspaceId)
        {
            return RuntimeAiLabPathHash.Capture(
                new[] { Path.Combine(WorkspaceRoot, workspaceId ?? string.Empty) }).Hash;
        }

        internal bool TryReopenRoles(
            out CharacterTestRuntimeAiLabAuthoringWorkspaceRoleResult result)
        {
            var persistence = new CharacterTestRuntimeAiLabDraftPersistence(
                WorkspaceRoot,
                RecoveryRoot);
            Roles = CharacterTestRuntimeAiLabAuthoringWorkspaceRoles.CreateIsolated(
                persistence,
                RoleRegistryPath,
                ActiveRolePath);
            Persistence = persistence;
            MatchConfig = CharacterTestArenaMatchConfig.CreateProjectBacked(Published.State);
            result = Roles.OpenInitial(MatchConfig);
            return result.Success;
        }

        public void Dispose()
        {
            string full = Path.GetFullPath(RootPath);
            string prefix = _safeRootPrefix.TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                || full.Length <= prefix.Length)
            {
                return;
            }

            try
            {
                if (Directory.Exists(full))
                    Directory.Delete(full, recursive: true);
            }
            catch
            {
                // Temp cleanup is best effort; production-boundary assertions have already run.
            }
        }

        private static bool IsUnderRoot(string path, string root)
        {
            string full = Path.GetFullPath(path ?? string.Empty);
            return full.StartsWith(root, StringComparison.OrdinalIgnoreCase);
        }
    }

    internal sealed class RuntimeAiLabAuthoringUxFlowPublishedOverride
    {
        private readonly string _root;
        private readonly CharacterTestRuntimeAiLabBehaviorCardReferenceCatalog _references;

        internal RuntimeAiLabAuthoringUxFlowPublishedOverride(
            string root,
            CharacterTestRuntimeAiLabBehaviorCardReferenceCatalog references)
        {
            _root = Path.GetFullPath(root ?? string.Empty);
            _references = references ?? throw new ArgumentNullException(nameof(references));
            CardCatalogPath = Path.Combine(
                _root,
                CharacterTestRuntimeAiPlannerProjectCatalogLoader.CardCatalogFileName);
            GroupCatalogPath = Path.Combine(
                _root,
                CharacterTestRuntimeAiPlannerProjectCatalogLoader.GroupCatalogFileName);
        }

        internal string CardCatalogPath { get; }
        internal string GroupCatalogPath { get; }
        internal CharacterTestRuntimeAiPlannerProjectCatalogState State { get; private set; }
        internal int WriteCount { get; private set; }
        internal int ReloadCount { get; private set; }

        internal bool TryInitialize(
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog cards,
            CharacterTestRuntimeAiPlannerBehaviorGroupCatalog groups,
            out string diagnostic)
        {
            if (cards == null || groups == null)
            {
                diagnostic = "The Temp Published seed is unavailable.";
                return false;
            }

            try
            {
                Persist(cards, CharacterTestRuntimeAiPlannerBehaviorGroupCatalogJson.ToJson(groups));
                State = LoadFromDisk(incrementReload: false);
                diagnostic = State.Diagnostic;
                return State.Success;
            }
            catch (Exception exception)
            {
                diagnostic = exception.GetType().Name + ": " + exception.Message;
                return false;
            }
        }

        internal CharacterTestRuntimeAiPlannerProjectCatalogState Reload()
        {
            State = LoadFromDisk(incrementReload: true);
            return State;
        }

        internal CharacterTestRuntimeAiPlannerProjectCatalogWriteResult Write(
            CharacterTestRuntimeAiPlannerProjectCatalogWriteRequest request)
        {
            WriteCount++;
            if (!string.Equals(
                    Path.GetFullPath(request.CardCatalogPath),
                    CardCatalogPath,
                    StringComparison.OrdinalIgnoreCase)
                || !string.Equals(
                    Path.GetFullPath(request.GroupCatalogPath),
                    GroupCatalogPath,
                    StringComparison.OrdinalIgnoreCase))
            {
                return new CharacterTestRuntimeAiPlannerProjectCatalogWriteResult(
                    false,
                    true,
                    "The publish request escaped the Temp Published override.");
            }
            if (!string.Equals(
                    request.ExpectedCardFileHash,
                    CharacterTestRuntimeAiPlannerProjectCatalogLoader.ComputeFileHash(
                        CardCatalogPath),
                    StringComparison.Ordinal)
                || !string.Equals(
                    request.ExpectedGroupFileHash,
                    CharacterTestRuntimeAiPlannerProjectCatalogLoader.ComputeFileHash(
                        GroupCatalogPath),
                    StringComparison.Ordinal)
                || State == null
                || request.ExpectedCardCatalogHash != State.CardCatalog.CatalogHash)
            {
                return new CharacterTestRuntimeAiPlannerProjectCatalogWriteResult(
                    false,
                    true,
                    "The Temp Published expected hash changed before commit.");
            }

            try
            {
                Persist(request.CardCatalog, request.GroupCatalogJson);
                State = LoadFromDisk(incrementReload: false);
                return new CharacterTestRuntimeAiPlannerProjectCatalogWriteResult(
                    State.Success,
                    false,
                    State.Diagnostic);
            }
            catch (Exception exception)
            {
                return new CharacterTestRuntimeAiPlannerProjectCatalogWriteResult(
                    false,
                    false,
                    exception.GetType().Name + ": " + exception.Message);
            }
        }

        internal string CaptureContentHash()
        {
            return RuntimeAiLabPathHash.Capture(new[] { _root }).Hash;
        }

        private void Persist(
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog cards,
            string groupJson)
        {
            if (cards == null)
                throw new InvalidDataException("The Temp Published card catalog is null.");
            Directory.CreateDirectory(_root);
            CharacterTestRuntimeAiPlannerProjectCardStoragePayload payload =
                CharacterTestRuntimeAiPlannerProjectCardStorage.CreatePayload(cards);
            for (int i = 0; i < payload.Files.Count; i++)
            {
                CharacterTestRuntimeAiPlannerProjectCardFile file = payload.Files[i];
                string path = Path.GetFullPath(Path.Combine(
                    _root,
                    file.RelativePath.Replace('/', Path.DirectorySeparatorChar)));
                string prefix = _root.TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
                if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("A Temp Published card path escaped its root.");
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, file.Json, new UTF8Encoding(false));
            }
            File.WriteAllText(CardCatalogPath, payload.IndexJson, new UTF8Encoding(false));
            File.WriteAllText(GroupCatalogPath, groupJson ?? string.Empty, new UTF8Encoding(false));
        }

        private CharacterTestRuntimeAiPlannerProjectCatalogState LoadFromDisk(
            bool incrementReload)
        {
            if (incrementReload)
                ReloadCount++;
            try
            {
                string cardJson = File.ReadAllText(CardCatalogPath, Encoding.UTF8);
                string groupJson = File.ReadAllText(GroupCatalogPath, Encoding.UTF8);
                CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalogLoadResult cardLoad =
                    CharacterTestRuntimeAiPlannerProjectCardStorage.Load(
                        cardJson,
                        _root,
                        _references);
                if (!cardLoad.Success)
                {
                    return Failed(
                        cardLoad.Diagnostics.Count > 0
                            ? cardLoad.Diagnostics[0].Message
                            : "The Temp Published card catalog is invalid.");
                }
                CharacterTestRuntimeAiPlannerBehaviorGroupCatalogLoadResult groupLoad =
                    CharacterTestRuntimeAiPlannerBehaviorGroupCatalogJson.Load(
                        groupJson,
                        cardLoad.Catalog,
                        CharacterTestRuntimeAiPlannerBehaviorDefinitionCatalog.CreateBuiltIn());
                if (!groupLoad.Success)
                    return Failed(groupLoad.Diagnostic);
                return new CharacterTestRuntimeAiPlannerProjectCatalogState(
                    cardLoad.Catalog,
                    groupLoad.Catalog,
                    CardCatalogPath,
                    GroupCatalogPath,
                    CharacterTestRuntimeAiPlannerProjectCatalogLoader.ComputeFileHash(
                        CardCatalogPath),
                    CharacterTestRuntimeAiPlannerProjectCatalogLoader.ComputeFileHash(
                        GroupCatalogPath),
                    string.Empty);
            }
            catch (Exception exception)
            {
                return Failed(exception.GetType().Name + ": " + exception.Message);
            }
        }

        private CharacterTestRuntimeAiPlannerProjectCatalogState Failed(string diagnostic)
        {
            return new CharacterTestRuntimeAiPlannerProjectCatalogState(
                null,
                null,
                CardCatalogPath,
                GroupCatalogPath,
                string.Empty,
                string.Empty,
                diagnostic);
        }
    }

    internal sealed class RuntimeAiLabAuthoringUxFlowSelection
    {
        private RuntimeAiLabAuthoringUxFlowSelection(
            CharacterTestRuntimeAiLabBehaviorGroupDraft group,
            CharacterTestRuntimeAiLabBehaviorEntryDraft entry,
            CharacterTestRuntimeAiLabBehaviorCardDraft card,
            int entryIndex,
            string timelineId,
            string eventId)
        {
            Group = group;
            Entry = entry;
            Card = card;
            EntryIndex = entryIndex;
            GroupDraftId = group?.GroupDraftId ?? string.Empty;
            EntryId = entry?.EntryId ?? string.Empty;
            CardDraftId = card?.CardDraftId ?? string.Empty;
            PublishedStableCardId = card?.PublishedStableId ?? string.Empty;
            TimelineId = timelineId ?? string.Empty;
            EventId = eventId ?? string.Empty;
            SourcePublishedRevision = card?.SourcePublishedRevision ?? 0;
            SourcePublishedSemanticHash = card?.SourcePublishedSemanticHash ?? 0L;
            SemanticHash = CharacterTestRuntimeAiLabBehaviorCardLogicCompiler.ComputeSemanticHash(
                card?.LogicPayload);
        }

        internal CharacterTestRuntimeAiLabBehaviorGroupDraft Group { get; }
        internal CharacterTestRuntimeAiLabBehaviorEntryDraft Entry { get; }
        internal CharacterTestRuntimeAiLabBehaviorCardDraft Card { get; }
        internal int EntryIndex { get; }
        internal string GroupDraftId { get; }
        internal string EntryId { get; }
        internal string CardDraftId { get; }
        internal string PublishedStableCardId { get; }
        internal string TimelineId { get; }
        internal string EventId { get; }
        internal int SourcePublishedRevision { get; }
        internal long SourcePublishedSemanticHash { get; }
        internal long SemanticHash { get; }

        internal static bool TrySelect(
            CharacterTestRuntimeAiLabDraftWorkspace workspace,
            string preferredGroupDraftId,
            string preferredEntryId,
            out RuntimeAiLabAuthoringUxFlowSelection selection,
            out string diagnostic)
        {
            selection = null;
            if (workspace == null)
            {
                diagnostic = "The Temp authoring Workspace is unavailable.";
                return false;
            }

            CharacterTestRuntimeAiLabBehaviorGroupDraft group = null;
            if (!string.IsNullOrWhiteSpace(preferredGroupDraftId))
                workspace.Groups.TryGetValue(preferredGroupDraftId, out group);
            if (group == null)
            {
                workspace.TryGetGroupForSlot(
                    CharacterTestArenaMatchConfig.Team1Id,
                    0,
                    out group);
            }
            if (group == null)
            {
                diagnostic = "The Temp fixture has no behavior group in Team 1 / Slot 1.";
                return false;
            }

            for (int i = 0; i < group.Entries.Count; i++)
            {
                CharacterTestRuntimeAiLabBehaviorEntryDraft entry = group.Entries[i];
                if (entry == null
                    || !string.IsNullOrWhiteSpace(preferredEntryId)
                    && !string.Equals(entry.EntryId, preferredEntryId, StringComparison.Ordinal)
                    || !workspace.Cards.TryGetValue(
                        entry.CardDraftId,
                        out CharacterTestRuntimeAiLabBehaviorCardDraft card)
                    || card?.HasPublishedSource != true
                    || card.LogicPayload == null)
                {
                    continue;
                }

                for (int timelineIndex = 0;
                     timelineIndex < card.LogicPayload.Timelines.Count;
                     timelineIndex++)
                {
                    CharacterTestRuntimeAiLabBehaviorRecipeTimeline timeline =
                        card.LogicPayload.Timelines[timelineIndex];
                    if (timeline == null || string.IsNullOrWhiteSpace(timeline.TimelineId))
                        continue;
                    for (int eventIndex = 0; eventIndex < timeline.Events.Count; eventIndex++)
                    {
                        CharacterTestRuntimeAiLabBehaviorRecipeEvent evt =
                            timeline.Events[eventIndex];
                        if (evt == null || string.IsNullOrWhiteSpace(evt.EventId))
                            continue;
                        selection = new RuntimeAiLabAuthoringUxFlowSelection(
                            group,
                            entry,
                            card,
                            i,
                            timeline.TimelineId,
                            evt.EventId);
                        diagnostic = string.Empty;
                        return true;
                    }
                }

                for (int detectorIndex = 0;
                     detectorIndex < card.LogicPayload.Detectors.Count;
                     detectorIndex++)
                {
                    CharacterTestRuntimeAiLabLogicDetector detector =
                        card.LogicPayload.Detectors[detectorIndex];
                    if (detector == null
                        || string.IsNullOrWhiteSpace(detector.DetectorId)
                        || string.IsNullOrWhiteSpace(detector.StageId))
                    {
                        continue;
                    }
                    selection = new RuntimeAiLabAuthoringUxFlowSelection(
                        group,
                        entry,
                        card,
                        i,
                        detector.StageId,
                        detector.DetectorId);
                    diagnostic = string.Empty;
                    return true;
                }
            }

            diagnostic = "The Temp fixture has no published editable Card/Event.";
            return false;
        }

        internal bool TryToggleFixtureField(
            CharacterTestRuntimeAiLabDraftWorkspace workspace,
            out RuntimeAiLabAuthoringUxFlowSelection selection,
            out string diagnostic)
        {
            selection = null;
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload next =
                Card?.LogicPayload?.Clone();
            if (next == null)
            {
                diagnostic = "The selected fixture Card has no editable logic payload.";
                return false;
            }

            bool mutated = false;
            for (int timelineIndex = 0;
                 !mutated && timelineIndex < next.Timelines.Count;
                 timelineIndex++)
            {
                CharacterTestRuntimeAiLabBehaviorRecipeTimeline timeline =
                    next.Timelines[timelineIndex];
                if (timeline == null
                    || !string.Equals(timeline.TimelineId, TimelineId, StringComparison.Ordinal))
                {
                    continue;
                }
                for (int eventIndex = 0; eventIndex < timeline.Events.Count; eventIndex++)
                {
                    CharacterTestRuntimeAiLabBehaviorRecipeEvent evt = timeline.Events[eventIndex];
                    if (evt == null
                        || !string.Equals(evt.EventId, EventId, StringComparison.Ordinal))
                    {
                        continue;
                    }
                    evt.Priority = evt.Priority == 0 ? 1 : 0;
                    mutated = true;
                    break;
                }
            }

            CharacterTestRuntimeAiLabLogicDetector target = null;
            for (int i = 0; !mutated && i < next.Detectors.Count; i++)
            {
                if (string.Equals(
                        next.Detectors[i]?.DetectorId,
                        EventId,
                        StringComparison.Ordinal))
                {
                    target = next.Detectors[i];
                    break;
                }
            }
            if (!mutated && target == null)
            {
                diagnostic = "The selected fixture Event no longer exists.";
                return false;
            }

            if (!mutated)
                target.ExpectedResult = !target.ExpectedResult;
            if (!workspace.TryReplaceCardLogicPayload(
                    CharacterTestArenaMatchConfig.Team1Id,
                    0,
                    EntryIndex,
                    next,
                    out diagnostic))
            {
                return false;
            }
            if (!TrySelect(
                    workspace,
                    GroupDraftId,
                    EntryId,
                    out selection,
                    out diagnostic))
            {
                return false;
            }
            return true;
        }

        internal CharacterTestRuntimeAiLabTestCurrentChangesRequest CreateTestRequest(
            CharacterTestRuntimeAiLabDraftWorkspace workspace,
            string operationId)
        {
            var page = new CharacterTestRuntimeAiLabPageContext(
                CharacterTestRuntimeAiLabWorkspace.BehaviorCard,
                CharacterTestRuntimeAiLabWorkspace.RuntimeFrame,
                CharacterTestRuntimeAiLabWorkspace.BehaviorGroup,
                playerDeckPageIndex: 0,
                replacementSourceStableCardId: PublishedStableCardId,
                groupDraftId: GroupDraftId,
                entryId: EntryId,
                cardDraftId: CardDraftId,
                selectedElementKind: CharacterTestRuntimeAiLabSelectedElementKind.Detector,
                selectedElementStableId: EventId,
                CharacterTestRuntimeAiLabBehaviorCardViewport.Default,
                CharacterTestRuntimeAiLabBehaviorCardLibraryState.Default,
                default,
                CharacterTestRuntimeAiLabTimelineMode.BehaviorLogic,
                runtimeTraceExpanded: true,
                behaviorPlanExpanded: true);
            return new CharacterTestRuntimeAiLabTestCurrentChangesRequest(
                operationId,
                workspace.WorkspaceId,
                workspace.CurrentRevision,
                CharacterTestArenaMatchConfig.Team1Id,
                setupSlotIndex: 0,
                GroupDraftId,
                EntryId,
                CardDraftId,
                PublishedStableCardId,
                page);
        }
    }

    internal readonly struct RuntimeAiLabProductionFootprint :
        IEquatable<RuntimeAiLabProductionFootprint>
    {
        private RuntimeAiLabProductionFootprint(
            RuntimeAiLabPathHash draft,
            RuntimeAiLabPathHash published,
            RuntimeAiLabPathHash activeRole,
            string diagnostic)
        {
            Draft = draft;
            Published = published;
            ActiveRole = activeRole;
            Diagnostic = diagnostic ?? string.Empty;
        }

        internal RuntimeAiLabPathHash Draft { get; }
        internal RuntimeAiLabPathHash Published { get; }
        internal RuntimeAiLabPathHash ActiveRole { get; }
        internal string Diagnostic { get; }
        internal bool IsValid => Diagnostic.Length == 0
            && Draft.IsValid
            && Published.IsValid
            && ActiveRole.IsValid;

        internal static RuntimeAiLabProductionFootprint Capture()
        {
            try
            {
                string projectRoot = Path.GetFullPath(Directory.GetCurrentDirectory());
                string draftRoot = Path.Combine(
                    projectRoot,
                    "Authoring",
                    "RuntimeAiLab",
                    "Drafts");
                string publishedRoot = Path.Combine(
                    Application.streamingAssetsPath,
                    CharacterTestRuntimeAiPlannerProjectCatalogLoader.RelativeDirectory);
                string cardCatalog = Path.Combine(
                    publishedRoot,
                    CharacterTestRuntimeAiPlannerProjectCatalogLoader.CardCatalogFileName);
                string groupCatalog = Path.Combine(
                    publishedRoot,
                    CharacterTestRuntimeAiPlannerProjectCatalogLoader.GroupCatalogFileName);
                string cards = Path.Combine(
                    publishedRoot,
                    CharacterTestRuntimeAiPlannerProjectCardStorage.CardsDirectoryName);
                string activeRole = Path.Combine(
                    projectRoot,
                    "UserSettings",
                    "RuntimeAiLab",
                    "active-workspace-role.txt");
                RuntimeAiLabPathHash draft = RuntimeAiLabPathHash.Capture(
                    new[] { draftRoot });
                RuntimeAiLabPathHash published = RuntimeAiLabPathHash.Capture(
                    new[] { cardCatalog, groupCatalog, cards });
                RuntimeAiLabPathHash role = RuntimeAiLabPathHash.Capture(
                    new[] { activeRole });
                string diagnostic = !draft.IsValid
                    ? draft.Diagnostic
                    : !published.IsValid
                        ? published.Diagnostic
                        : role.Diagnostic;
                return new RuntimeAiLabProductionFootprint(
                    draft,
                    published,
                    role,
                    diagnostic);
            }
            catch (Exception exception)
            {
                return new RuntimeAiLabProductionFootprint(
                    default,
                    default,
                    default,
                    exception.GetType().Name + ": " + exception.Message);
            }
        }

        public bool Equals(RuntimeAiLabProductionFootprint other)
        {
            return IsValid
                && other.IsValid
                && Draft.Equals(other.Draft)
                && Published.Equals(other.Published)
                && ActiveRole.Equals(other.ActiveRole);
        }

        public override bool Equals(object obj)
        {
            return obj is RuntimeAiLabProductionFootprint other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Draft.GetHashCode();
                hash = hash * 397 ^ Published.GetHashCode();
                hash = hash * 397 ^ ActiveRole.GetHashCode();
                return hash;
            }
        }
    }

    internal readonly struct RuntimeAiLabPathHash : IEquatable<RuntimeAiLabPathHash>
    {
        private RuntimeAiLabPathHash(string hash, int fileCount, string diagnostic)
        {
            Hash = hash ?? string.Empty;
            FileCount = fileCount;
            Diagnostic = diagnostic ?? string.Empty;
        }

        internal string Hash { get; }
        internal int FileCount { get; }
        internal string Diagnostic { get; }
        internal bool IsValid => Hash.Length > 0 && Diagnostic.Length == 0;

        internal static RuntimeAiLabPathHash Capture(IReadOnlyList<string> roots)
        {
            try
            {
                var rows = new List<string>();
                int fileCount = 0;
                for (int rootIndex = 0; rootIndex < (roots?.Count ?? 0); rootIndex++)
                {
                    string root = Path.GetFullPath(roots[rootIndex] ?? string.Empty);
                    string label = "root[" + rootIndex.ToString(CultureInfo.InvariantCulture) + "]";
                    if (File.Exists(root))
                    {
                        rows.Add(label + "|file|" + ComputeFileHash(root));
                        fileCount++;
                        continue;
                    }
                    if (!Directory.Exists(root))
                    {
                        rows.Add(label + "|missing");
                        continue;
                    }

                    string[] files = Directory.GetFiles(root, "*", SearchOption.AllDirectories);
                    Array.Sort(files, StringComparer.OrdinalIgnoreCase);
                    if (files.Length == 0)
                        rows.Add(label + "|empty-directory");
                    for (int i = 0; i < files.Length; i++)
                    {
                        string relative = files[i].Substring(root.Length).TrimStart(
                            Path.DirectorySeparatorChar,
                            Path.AltDirectorySeparatorChar).Replace('\u005c', '/');
                        rows.Add(label + "|" + relative + "|" + ComputeFileHash(files[i]));
                        fileCount++;
                    }
                }
                rows.Sort(StringComparer.Ordinal);
                string aggregate = ComputeBytesHash(
                    Encoding.UTF8.GetBytes(string.Join("\n", rows)));
                return new RuntimeAiLabPathHash(aggregate, fileCount, string.Empty);
            }
            catch (Exception exception)
            {
                return new RuntimeAiLabPathHash(
                    string.Empty,
                    0,
                    exception.GetType().Name + ": " + exception.Message);
            }
        }

        public bool Equals(RuntimeAiLabPathHash other)
        {
            return IsValid
                && other.IsValid
                && FileCount == other.FileCount
                && string.Equals(Hash, other.Hash, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is RuntimeAiLabPathHash other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Hash == null ? 0 : StringComparer.Ordinal.GetHashCode(Hash)) * 397
                    ^ FileCount;
            }
        }

        private static string ComputeFileHash(string path)
        {
            using (SHA256 sha = SHA256.Create())
            using (FileStream stream = File.OpenRead(path))
                return ToHex(sha.ComputeHash(stream));
        }

        private static string ComputeBytesHash(byte[] bytes)
        {
            using (SHA256 sha = SHA256.Create())
                return ToHex(sha.ComputeHash(bytes ?? Array.Empty<byte>()));
        }

        private static string ToHex(byte[] bytes)
        {
            var builder = new StringBuilder((bytes?.Length ?? 0) * 2);
            for (int i = 0; i < (bytes?.Length ?? 0); i++)
                builder.Append(bytes[i].ToString("x2", CultureInfo.InvariantCulture));
            return builder.ToString();
        }
    }
}
