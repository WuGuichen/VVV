using System.Diagnostics;
using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using MxFramework.Authoring.UiComposer;

namespace MxFramework.Authoring.Cli;

internal static class UiComposerPublishAllCommands
{
    public static int Dispatch(string[] args, JsonSerializerOptions jsonOptions)
    {
        if (args.Length < 2 || args[1] != "publish-all")
            throw new ArgumentException("ui-composer command is required (publish-all).", nameof(args));

        string projectRoot = Path.GetFullPath(Program.RequireOption(args, "--project-root"));
        string screen = Program.GetOption(args, "--screen", UiComposerService.DefaultScreenId);
        bool verifyUnity = Program.HasFlag(args, "--verify-unity");
        bool verifyInteraction = Program.HasFlag(args, "--verify-interaction");
        bool fast = Program.HasFlag(args, "--fast");
        bool json = Program.HasFlag(args, "--json");
        bool keepEditor = Program.HasFlag(args, "--keep-editor");
        string profile = Program.GetOption(args, "--interaction-profile", string.Empty);

        if (fast && (verifyUnity || verifyInteraction))
            throw new ArgumentException("--fast cannot be combined with Unity or interaction verification.");
        if (verifyInteraction && string.IsNullOrWhiteSpace(profile))
            throw new ArgumentException("--interaction-profile is required with --verify-interaction.");
        if (!verifyInteraction && !string.IsNullOrWhiteSpace(profile))
            throw new ArgumentException("--interaction-profile requires --verify-interaction.");
        if (verifyInteraction && !string.Equals(profile, "runtime-ai-lab", StringComparison.Ordinal))
            throw new ArgumentException("The first publish-all version only supports --interaction-profile runtime-ai-lab.");

        var request = new UiComposerPublishAllRequest
        {
            ProjectRoot = projectRoot,
            ScreenId = screen,
            VerificationLevel = verifyInteraction
                ? UiComposerPublishVerificationLevel.Interaction
                : verifyUnity
                    ? UiComposerPublishVerificationLevel.Unity
                    : UiComposerPublishVerificationLevel.Fast,
            InteractionProfile = profile,
            KeepEditor = keepEditor,
            JsonOutput = json
        };
        using var cancellation = new CancellationTokenSource();
        ConsoleCancelEventHandler cancelHandler = (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cancellation.Cancel();
        };
        Console.CancelKeyPress += cancelHandler;
        UiComposerPublishAllResult result;
        try
        {
            var orchestrator = new UiComposerPublishAllOrchestrator(
                jsonOptions,
                json ? null : message => Console.WriteLine(message));
            result = orchestrator.Run(request, cancellation.Token);
        }
        finally
        {
            Console.CancelKeyPress -= cancelHandler;
        }

        if (json)
            Console.WriteLine(JsonSerializer.Serialize(result, jsonOptions));
        else
            Console.WriteLine(result.Success
                ? "UI Composer publish-all completed: " + result.Message
                : "UI Composer publish-all failed at " + result.ResultStage + ": " + result.FailureCode + " - " + result.Message);

        return result.Success
            ? Program.ExitReady
            : result.FailureCode == UiComposerPublishFailureCode.PreflightBlocked
                ? Program.ExitValidationBlocked
                : Program.ExitToolError;
    }
}

internal enum UiComposerPublishVerificationLevel
{
    Fast,
    Unity,
    Interaction
}

internal enum UiComposerPublishFailureCode
{
    None,
    InvalidRequest,
    PreflightBlocked,
    PackagePublishAlreadyRunning,
    FairyGuiEditorMissing,
    FairyGuiProjectMissing,
    FairyGuiLaunchFailed,
    HelperProbeFailed,
    HelperProbeTimedOut,
    ApplyFailed,
    ApplyTimedOut,
    ApplyStateFileBusy,
    SourceVerificationFailed,
    PublishFailed,
    PublishTimedOut,
    ArtifactVerificationFailed,
    UnityMcpUnavailable,
    UnityValidationFailed,
    InteractionGatewayUnavailable,
    InteractionVerificationFailed,
    Cancelled,
    UnexpectedFailure
}

internal sealed class UiComposerPublishAllRequest
{
    public string ProjectRoot { get; set; } = string.Empty;
    public string ScreenId { get; set; } = string.Empty;
    public UiComposerPublishVerificationLevel VerificationLevel { get; set; }
    public string InteractionProfile { get; set; } = string.Empty;
    public bool KeepEditor { get; set; }
    public bool JsonOutput { get; set; }
}

internal sealed class UiComposerPublishAllResult
{
    public string OperationId { get; set; } = string.Empty;
    public string ScreenId { get; set; } = string.Empty;
    public string PackageId { get; set; } = string.Empty;
    public UiComposerPublishVerificationLevel VerificationLevel { get; set; }
    public Dictionary<string, string> AuthorityHashesBefore { get; set; } = new(StringComparer.Ordinal);
    public Dictionary<string, string> AuthorityHashesAfterApply { get; set; } = new(StringComparer.Ordinal);
    public string ProbeOperationId { get; set; } = string.Empty;
    public string ApplyOperationId { get; set; } = string.Empty;
    public string PublishOperationId { get; set; } = string.Empty;
    public Dictionary<string, string> PublishedArtifactHashes { get; set; } = new(StringComparer.Ordinal);
    public UiComposerEditorSession EditorOwnership { get; set; } = new();
    public List<UiComposerPublishStageResult> StageTimings { get; set; } = new();
    public UiComposerExternalVerificationResult UnityValidation { get; set; }
    public UiComposerExternalVerificationResult InteractionEvidence { get; set; }
    public bool Success { get; set; }
    public string ResultStage { get; set; } = string.Empty;
    public UiComposerPublishFailureCode FailureCode { get; set; }
    public string Message { get; set; } = string.Empty;
    public string LogPath { get; set; } = string.Empty;
}

internal sealed class UiComposerPublishStageResult
{
    public string Stage { get; set; } = string.Empty;
    public string StartedAtUtc { get; set; } = string.Empty;
    public string CompletedAtUtc { get; set; } = string.Empty;
    public long DurationMilliseconds { get; set; }
    public bool Success { get; set; }
    public string OperationId { get; set; } = string.Empty;
    public string Phase { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
}

internal sealed class UiComposerArtifactVerificationResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public Dictionary<string, string> Hashes { get; set; } = new(StringComparer.Ordinal);
}

internal sealed class UiComposerEditorSession
{
    public bool ReusedExistingEditor { get; set; }
    public bool LaunchedByCurrentOperation { get; set; }
    public bool CloseRequested { get; set; }
    public bool Closed { get; set; }
    public int ProcessId { get; set; }
    public string HelperVersion { get; set; } = string.Empty;
    public string ProjectPath { get; set; } = string.Empty;
    public string ActiveBranch { get; set; } = string.Empty;
    public string Warning { get; set; } = string.Empty;
}

internal sealed class UiComposerExternalVerificationResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string EvidenceJson { get; set; } = string.Empty;
    public string ScreenshotPath { get; set; } = string.Empty;
}

internal sealed class UiComposerPublishAllOrchestrator
{
    private const int LockWaitMilliseconds = 5000;
    private const int ExistingEditorProbeMilliseconds = 1500;
    private const int HelperReadyTimeoutMilliseconds = 60000;
    private const int ApplyTimeoutMilliseconds = 180000;
    private const int PublishTimeoutMilliseconds = 180000;
    private const int PollMilliseconds = 200;

    private readonly JsonSerializerOptions _jsonOptions;
    private readonly Action<string> _progress;
    private readonly List<string> _queuedRequestPaths = new();
    private CancellationToken _cancellationToken;
    private string _repositoryRoot = string.Empty;

    public UiComposerPublishAllOrchestrator(JsonSerializerOptions jsonOptions, Action<string> progress)
    {
        _jsonOptions = jsonOptions ?? throw new ArgumentNullException(nameof(jsonOptions));
        _progress = progress;
    }

    public UiComposerPublishAllResult Run(
        UiComposerPublishAllRequest request,
        CancellationToken cancellationToken = default)
    {
        _cancellationToken = cancellationToken;
        _queuedRequestPaths.Clear();
        var result = new UiComposerPublishAllResult
        {
            OperationId = Guid.NewGuid().ToString("N"),
            ScreenId = request?.ScreenId ?? string.Empty,
            VerificationLevel = request?.VerificationLevel ?? UiComposerPublishVerificationLevel.Fast,
            ResultStage = "Starting",
            FailureCode = UiComposerPublishFailureCode.None
        };
        Process launchedEditor = null;
        Mutex packageMutex = null;
        bool ownsMutex = false;

        try
        {
            if (request == null || string.IsNullOrWhiteSpace(request.ProjectRoot) || string.IsNullOrWhiteSpace(request.ScreenId))
                return Fail(result, "Starting", UiComposerPublishFailureCode.InvalidRequest, "Project root and screen id are required.");

            string root = Path.GetFullPath(request.ProjectRoot);
            _repositoryRoot = root;
            if (!Directory.Exists(root))
                return Fail(result, "Starting", UiComposerPublishFailureCode.InvalidRequest, "Project root does not exist: " + root);

            UiComposerAuthoringState before = Timed(result, "Preflight", "", () =>
            {
                Report(1, 7, "Preflight");
                UiComposerAuthoringState state = UiComposerService.ReadState(root, request.ScreenId);
                UiComposerWorkspacePlan plan = UiComposerService.PlanWorkspace(
                    root,
                    request.ScreenId,
                    Hashes(state),
                    state.ResourceCatalog,
                    state.ComponentRecipes,
                    state.Blueprint);
                if (plan.Blocked)
                {
                    string diagnostics = string.Join(" | ", plan.Diagnostics.Where(item => item.Blocking).Select(item => item.Code + ": " + item.Message));
                    throw new UiComposerPublishException(UiComposerPublishFailureCode.PreflightBlocked, diagnostics);
                }
                return state;
            });

            result.PackageId = before.BindingContract.Target.PackageId;
            result.AuthorityHashesBefore = HashDictionary(before);
            packageMutex = new Mutex(false, BuildMutexName(root, result.PackageId));
            try { ownsMutex = packageMutex.WaitOne(LockWaitMilliseconds); }
            catch (AbandonedMutexException) { ownsMutex = true; }
            if (!ownsMutex)
                return Fail(result, "Lock", UiComposerPublishFailureCode.PackagePublishAlreadyRunning, "Another publish-all operation owns package " + result.PackageId + ".");

            Report(2, 7, "FairyGUI helper ready");
            UiComposerOperationResult probe = Timed(result, "HelperReady", "", () =>
            {
                UiComposerQueuedOperation queued = Track(
                    UiComposerService.QueueProbe(root, result.PackageId));
                result.ProbeOperationId = queued.OperationId;
                UiComposerOperationStatus status = WaitOperation(root, queued.OperationId, ExistingEditorProbeMilliseconds, allowTimeout: true);
                if (status?.Result == null)
                {
                    launchedEditor = StartFairyGui(root);
                    result.EditorOwnership.LaunchedByCurrentOperation = true;
                    result.EditorOwnership.ProcessId = launchedEditor.Id;
                    status = WaitOperation(root, queued.OperationId, HelperReadyTimeoutMilliseconds, allowTimeout: false);
                }
                else
                {
                    result.EditorOwnership.ReusedExistingEditor = true;
                }

                if (status?.Result == null)
                    throw new UiComposerPublishException(UiComposerPublishFailureCode.HelperProbeTimedOut, "FairyGUI helper readiness probe timed out.");
                if (!status.Result.Ok || !status.Result.Ready)
                    throw new UiComposerPublishException(UiComposerPublishFailureCode.HelperProbeFailed, status.Result.Message);
                result.EditorOwnership.HelperVersion = status.Result.HelperVersion;
                result.EditorOwnership.ProjectPath = status.Result.ProjectPath;
                result.EditorOwnership.ActiveBranch = status.Result.ActiveBranch;
                if (result.EditorOwnership.ProcessId == 0)
                    result.EditorOwnership.ProcessId = status.Result.EditorProcessId;
                string expectedProject = Path.GetFullPath(Path.Combine(root, "FGUIProject"));
                if (!PathsEqual(expectedProject, status.Result.ProjectPath))
                    throw new UiComposerPublishException(UiComposerPublishFailureCode.HelperProbeFailed, "FairyGUI helper is attached to another project: " + status.Result.ProjectPath);
                return status.Result;
            });
            SetStageOperationId(result, "HelperReady", result.ProbeOperationId);

            Report(3, 7, "Apply");
            UiComposerOperationResult apply = Timed(result, "Apply", "", () => RunApply(root, request.ScreenId, before, result, probe));
            SetStageOperationId(result, "Apply", result.ApplyOperationId);

            Report(4, 7, "Verify source");
            UiComposerAuthoringState afterApply = Timed(result, "VerifySource", result.ApplyOperationId, () =>
            {
                UiComposerAuthoringState state = UiComposerService.ReadState(root, request.ScreenId);
                VerifyAppliedState(state, apply);
                return state;
            });
            result.AuthorityHashesAfterApply = HashDictionary(afterApply);

            Report(5, 7, "Publish");
            UiComposerOperationResult publish = Timed(result, "Publish", "", () =>
            {
                UiComposerQueuedOperation queued = Track(
                    UiComposerService.QueuePublish(root, request.ScreenId, Hashes(afterApply)));
                result.PublishOperationId = queued.OperationId;
                UiComposerOperationStatus status = WaitOperation(root, queued.OperationId, PublishTimeoutMilliseconds, allowTimeout: false);
                if (status?.Result == null)
                    throw new UiComposerPublishException(UiComposerPublishFailureCode.PublishTimedOut, "FairyGUI publish timed out.");
                if (!status.Result.Ok)
                    throw new UiComposerPublishException(UiComposerPublishFailureCode.PublishFailed, status.Result.Message);
                return status.Result;
            });
            SetStageOperationId(result, "Publish", result.PublishOperationId);

            Report(6, 7, "Verify artifacts");
            UiComposerArtifactVerificationResult artifacts = Timed(result, "VerifyArtifacts", result.PublishOperationId,
                () => VerifyArtifacts(root, request.ScreenId, apply, publish));
            result.PublishedArtifactHashes = artifacts.Hashes;

            if (request.VerificationLevel >= UiComposerPublishVerificationLevel.Unity)
            {
                Report(7, 7, "Unity validation");
                result.UnityValidation = Timed(result, "UnityValidation", "", () =>
                {
                    var client = new UnityMcpPublishVerificationClient(root, _jsonOptions);
                    UiComposerExternalVerificationResult validation = client.ValidateUnityScripts();
                    result.UnityValidation = validation;
                    if (!validation.Success)
                    {
                        UiComposerPublishFailureCode code = validation.Message.Contains("endpoint", StringComparison.OrdinalIgnoreCase)
                            ? UiComposerPublishFailureCode.UnityMcpUnavailable
                            : UiComposerPublishFailureCode.UnityValidationFailed;
                        throw new UiComposerPublishException(code, validation.Message);
                    }
                    return validation;
                });
            }

            if (request.VerificationLevel == UiComposerPublishVerificationLevel.Interaction)
            {
                result.InteractionEvidence = Timed(result, "InteractionVerification", "", () =>
                {
                    var client = new UnityMcpPublishVerificationClient(root, _jsonOptions);
                    UiComposerExternalVerificationResult verification = client.VerifyInteraction(request.InteractionProfile, result.OperationId);
                    result.InteractionEvidence = verification;
                    if (!verification.Success)
                    {
                        UiComposerPublishFailureCode code = verification.Message.Contains("not registered", StringComparison.OrdinalIgnoreCase)
                            ? UiComposerPublishFailureCode.InteractionGatewayUnavailable
                            : UiComposerPublishFailureCode.InteractionVerificationFailed;
                        throw new UiComposerPublishException(code, verification.Message);
                    }
                    return verification;
                });
            }

            result.Success = true;
            result.ResultStage = "Completed";
            result.Message = result.PackageId + " materialized, published and verified.";
            return result;
        }
        catch (UiComposerPublishException ex)
        {
            return Fail(result, result.ResultStage, ex.Code, ex.Message);
        }
        catch (OperationCanceledException)
        {
            return Fail(
                result,
                result.ResultStage,
                UiComposerPublishFailureCode.Cancelled,
                "UI Composer publish-all was cancelled.");
        }
        catch (Exception ex)
        {
            return Fail(result, result.ResultStage, UiComposerPublishFailureCode.UnexpectedFailure, ex.Message);
        }
        finally
        {
            if (launchedEditor != null && request?.KeepEditor != true)
                CloseOwnedEditor(launchedEditor, result.EditorOwnership);
            if (ownsMutex)
                packageMutex?.ReleaseMutex();
            packageMutex?.Dispose();
            DeleteUnclaimedRequestsBestEffort();
            WriteLogBestEffort(request?.ProjectRoot, result);
        }
    }

    private UiComposerOperationResult RunApply(string root, string screen, UiComposerAuthoringState before, UiComposerPublishAllResult parent, UiComposerOperationResult probe)
    {
        UiComposerOperationResult first = QueueAndWaitApply(root, screen, Hashes(before), parent);
        if (first.Ok)
            return first;
        if (!first.Message.Contains("Win32 IO returned 1224", StringComparison.OrdinalIgnoreCase))
            throw new UiComposerPublishException(UiComposerPublishFailureCode.ApplyFailed, first.Message);

        if (probe == null || !probe.Ok || !probe.Ready)
            throw new UiComposerPublishException(UiComposerPublishFailureCode.ApplyStateFileBusy, "Apply hit Win32 1224 and helper readiness could not be reconfirmed.");
        string packageRoot = Path.GetFullPath(Path.Combine(root, before.BindingContract.Target.PackageSourcePath));
        string statePath = Path.GetFullPath(Path.Combine(packageRoot, "ui-composer-state.json"));
        if (!IsWithin(statePath, packageRoot) || !WaitForExclusiveFile(statePath, 10000))
            throw new UiComposerPublishException(UiComposerPublishFailureCode.ApplyStateFileBusy, "ui-composer-state.json remained busy after Win32 1224.");

        UiComposerAuthoringState retryState = UiComposerService.ReadState(root, screen);
        UiComposerOperationResult retry = QueueAndWaitApply(root, screen, Hashes(retryState), parent);
        if (!retry.Ok)
            throw new UiComposerPublishException(UiComposerPublishFailureCode.ApplyStateFileBusy, retry.Message);
        return retry;
    }

    private UiComposerOperationResult QueueAndWaitApply(string root, string screen, UiComposerExpectedHashes expected, UiComposerPublishAllResult parent)
    {
        UiComposerQueuedOperation queued = Track(
            UiComposerService.QueueApply(root, screen, expected));
        parent.ApplyOperationId = queued.OperationId;
        UiComposerOperationStatus status = WaitOperation(root, queued.OperationId, ApplyTimeoutMilliseconds, allowTimeout: false);
        if (status?.Result == null)
            throw new UiComposerPublishException(UiComposerPublishFailureCode.ApplyTimedOut, "FairyGUI materialization timed out.");
        return status.Result;
    }

    private UiComposerOperationStatus WaitOperation(string root, string operationId, int timeoutMilliseconds, bool allowTimeout)
    {
        Stopwatch timer = Stopwatch.StartNew();
        string lastPhase = string.Empty;
        while (timer.ElapsedMilliseconds < timeoutMilliseconds)
        {
            _cancellationToken.ThrowIfCancellationRequested();
            UiComposerOperationStatus status = UiComposerService.ReadOperationStatus(root, operationId);
            if (status.Progress != null && !string.Equals(lastPhase, status.Progress.Phase, StringComparison.Ordinal))
            {
                lastPhase = status.Progress.Phase;
                _progress?.Invoke("  " + status.Progress.Phase + (string.IsNullOrWhiteSpace(status.Progress.Details) ? string.Empty : " - " + status.Progress.Details));
            }
            if (status.Result != null)
                return status;
            Thread.Sleep(PollMilliseconds);
        }
        if (allowTimeout)
            return UiComposerService.ReadOperationStatus(root, operationId);
        return null;
    }

    private UiComposerQueuedOperation Track(UiComposerQueuedOperation queued)
    {
        if (queued != null && !string.IsNullOrWhiteSpace(queued.RequestPath))
            _queuedRequestPaths.Add(queued.RequestPath);
        return queued;
    }

    private void DeleteUnclaimedRequestsBestEffort()
    {
        if (string.IsNullOrWhiteSpace(_repositoryRoot))
            return;
        for (int i = 0; i < _queuedRequestPaths.Count; i++)
        {
            try
            {
                string path = Path.GetFullPath(Path.Combine(
                    _repositoryRoot,
                    _queuedRequestPaths[i].Replace('/', Path.DirectorySeparatorChar)));
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch
            {
                // The helper may have claimed the request between the existence check and delete.
            }
        }
    }

    private static Process StartFairyGui(string root)
    {
        string editorPath = Path.GetFullPath(Path.Combine(root, "..", "FairyGUI-Editor", "FairyGUI-Editor.exe"));
        string projectPath = Path.Combine(root, "FGUIProject", "FGUIProject.fairy");
        if (!File.Exists(editorPath))
            throw new UiComposerPublishException(UiComposerPublishFailureCode.FairyGuiEditorMissing, "FairyGUI Editor was not found: " + editorPath);
        if (!File.Exists(projectPath))
            throw new UiComposerPublishException(UiComposerPublishFailureCode.FairyGuiProjectMissing, "FairyGUI project was not found: " + projectPath);
        try
        {
            return Process.Start(new ProcessStartInfo
            {
                FileName = editorPath,
                Arguments = "\"" + projectPath + "\"",
                WorkingDirectory = root,
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Minimized
            }) ?? throw new InvalidOperationException("Process.Start returned null.");
        }
        catch (Exception ex)
        {
            throw new UiComposerPublishException(UiComposerPublishFailureCode.FairyGuiLaunchFailed, ex.Message);
        }
    }

    private static void CloseOwnedEditor(Process process, UiComposerEditorSession session)
    {
        session.CloseRequested = true;
        try
        {
            if (process.HasExited) { session.Closed = true; return; }
            if (!process.CloseMainWindow())
            {
                session.Warning = "The FairyGUI process has no closable main window; it was left running.";
                return;
            }
            session.Closed = process.WaitForExit(5000);
            if (!session.Closed)
                session.Warning = "FairyGUI did not close within five seconds; it was left running.";
        }
        catch (Exception ex)
        {
            session.Warning = "FairyGUI close request failed and the process was left running: " + ex.Message;
        }
    }

    private static void VerifyAppliedState(UiComposerAuthoringState state, UiComposerOperationResult apply)
    {
        if (state.PackageState == null)
            throw new UiComposerPublishException(UiComposerPublishFailureCode.SourceVerificationFailed, "Apply completed without ui-composer-state.json.");
        if (!string.Equals(apply.SourceXmlHash, state.SourceXmlHash, StringComparison.OrdinalIgnoreCase))
            throw new UiComposerPublishException(UiComposerPublishFailureCode.SourceVerificationFailed, "Helper source XML hash does not match the materialized component XML.");
        UiComposerPackageState mapping = state.PackageState;
        if (mapping.Owner != "MxUiComposer" || mapping.ScreenId != state.Screen.ScreenId || mapping.PackageId != state.BindingContract.Target.PackageId)
            throw new UiComposerPublishException(UiComposerPublishFailureCode.SourceVerificationFailed, "Composer package ownership identity is inconsistent.");
        if (mapping.BlueprintHash != state.BlueprintHash || mapping.CatalogHash != state.CatalogHash || mapping.RecipeCatalogHash != state.RecipeCatalogHash || mapping.ContractHash != state.ContractHash || mapping.SourceXmlHash != state.SourceXmlHash)
            throw new UiComposerPublishException(UiComposerPublishFailureCode.SourceVerificationFailed, "Composer package state hashes do not match current authority.");
    }

    private static UiComposerArtifactVerificationResult VerifyArtifacts(string root, string screen, UiComposerOperationResult apply, UiComposerOperationResult publish)
    {
        UiComposerAuthoringState state = UiComposerService.ReadState(root, screen);
        VerifyAppliedState(state, apply);
        UiComposerBindingTarget target = state.BindingContract.Target;
        string packageRoot = Full(root, target.PackageSourcePath);
        string packageXml = Path.Combine(packageRoot, "package.xml");
        string componentXml = Path.Combine(packageRoot, target.ComponentName + ".xml");
        string statePath = Path.Combine(packageRoot, "ui-composer-state.json");
        string bytesPath = Full(root, target.PublishedPackagePath);
        string atlasPath = Full(root, target.PublishedAtlasPath);
        var paths = new[] { packageXml, componentXml, statePath, bytesPath, atlasPath };
        for (int i = 0; i < paths.Length; i++)
        {
            if (!File.Exists(paths[i]) || new FileInfo(paths[i]).Length == 0)
                throw new UiComposerPublishException(UiComposerPublishFailureCode.ArtifactVerificationFailed, "Published artifact is missing or empty: " + paths[i]);
        }

        XDocument package = XDocument.Load(packageXml, LoadOptions.None);
        bool componentDeclared = package.Descendants("component").Any(element =>
            string.Equals((string)element.Attribute("name"), target.ComponentName + ".xml", StringComparison.Ordinal));
        if (!componentDeclared)
            throw new UiComposerPublishException(UiComposerPublishFailureCode.ArtifactVerificationFailed, "package.xml does not declare " + target.ComponentName + ".xml.");

        foreach (UiComposerComponentMapping mapping in state.PackageState.ComponentMappings ?? Array.Empty<UiComposerComponentMapping>())
        {
            string mappedPath = Path.GetFullPath(Path.Combine(packageRoot, mapping.ComponentRelativePath.Replace('/', Path.DirectorySeparatorChar)));
            if (!IsWithin(mappedPath, packageRoot) || !File.Exists(mappedPath))
                throw new UiComposerPublishException(UiComposerPublishFailureCode.ArtifactVerificationFailed, "Mapped FairyGUI component is missing: " + mapping.ComponentRelativePath);
        }

        string expectedPublishDirectory = Path.GetDirectoryName(bytesPath) ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(publish.PublishedPath))
        {
            string reported = Path.GetFullPath(publish.PublishedPath);
            bool matches = PathsEqual(reported, bytesPath) || PathsEqual(reported, expectedPublishDirectory) || IsWithin(bytesPath, reported);
            if (!matches)
                throw new UiComposerPublishException(UiComposerPublishFailureCode.ArtifactVerificationFailed, "Helper published a different target: " + publish.PublishedPath);
        }

        string manifestPath = FindTextFile(
            Path.Combine(root, "Assets", "Scripts"),
            "*Manifest*.cs",
            target.PackageId,
            target.ComponentName,
            target.PublishedPackagePath.Replace('/', Path.DirectorySeparatorChar));
        if (string.IsNullOrEmpty(manifestPath))
            throw new UiComposerPublishException(UiComposerPublishFailureCode.ArtifactVerificationFailed, "Generated FairyGUI manifest was not found for " + target.PackageId + ".");

        string catalogPath = FindTextFile(
            Path.Combine(root, "Assets", "Config", "MxFramework", "ResourceProfiles"),
            "*.json",
            target.PackageBytesResourceId,
            target.AtlasResourceId);
        if (string.IsNullOrEmpty(catalogPath))
            throw new UiComposerPublishException(UiComposerPublishFailureCode.ArtifactVerificationFailed, "Resource mapping catalog does not contain the package and atlas resource ids.");

        var hashes = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [Relative(root, packageXml)] = HashFile(packageXml),
            [Relative(root, componentXml)] = HashFile(componentXml),
            [Relative(root, statePath)] = HashFile(statePath),
            [Relative(root, bytesPath)] = HashFile(bytesPath),
            [Relative(root, atlasPath)] = HashFile(atlasPath),
            [Relative(root, manifestPath)] = HashFile(manifestPath),
            [Relative(root, catalogPath)] = HashFile(catalogPath)
        };
        return new UiComposerArtifactVerificationResult { Success = true, Message = "All Composer and published artifacts are consistent.", Hashes = hashes };
    }

    private static string FindTextFile(string root, string pattern, params string[] required)
    {
        if (!Directory.Exists(root)) return string.Empty;
        foreach (string path in Directory.EnumerateFiles(root, pattern, SearchOption.AllDirectories))
        {
            string text;
            try { text = File.ReadAllText(path); }
            catch (IOException) { continue; }
            bool all = true;
            for (int i = 0; i < required.Length; i++)
            {
                string normalized = required[i] ?? string.Empty;
                if (normalized.Contains(Path.DirectorySeparatorChar))
                {
                    bool either = text.Contains(normalized, StringComparison.Ordinal)
                                  || text.Contains(normalized.Replace(Path.DirectorySeparatorChar, '/'), StringComparison.Ordinal)
                                  || text.Contains(normalized.Replace(Path.DirectorySeparatorChar, '\\'), StringComparison.Ordinal);
                    if (!either) { all = false; break; }
                }
                else if (!text.Contains(normalized, StringComparison.Ordinal)) { all = false; break; }
            }
            if (all) return path;
        }
        return string.Empty;
    }

    private T Timed<T>(UiComposerPublishAllResult result, string stage, string operationId, Func<T> action)
    {
        DateTime started = DateTime.UtcNow;
        Stopwatch watch = Stopwatch.StartNew();
        result.ResultStage = stage;
        var timing = new UiComposerPublishStageResult { Stage = stage, OperationId = operationId, StartedAtUtc = started.ToString("O") };
        result.StageTimings.Add(timing);
        try
        {
            T value = action();
            timing.Success = true;
            return value;
        }
        catch (Exception ex)
        {
            timing.Success = false;
            timing.Details = ex.Message;
            throw;
        }
        finally
        {
            timing.DurationMilliseconds = watch.ElapsedMilliseconds;
            timing.CompletedAtUtc = DateTime.UtcNow.ToString("O");
        }
    }

    private void Report(int step, int total, string message) => _progress?.Invoke("[" + step + "/" + total + "] " + message);

    private static void SetStageOperationId(
        UiComposerPublishAllResult result,
        string stage,
        string operationId)
    {
        UiComposerPublishStageResult timing = result.StageTimings.LastOrDefault(
            item => string.Equals(item.Stage, stage, StringComparison.Ordinal));
        if (timing != null)
            timing.OperationId = operationId ?? string.Empty;
    }

    private static UiComposerPublishAllResult Fail(UiComposerPublishAllResult result, string stage, UiComposerPublishFailureCode code, string message)
    {
        result.Success = false;
        result.ResultStage = stage;
        result.FailureCode = code;
        result.Message = string.IsNullOrWhiteSpace(message) ? code.ToString() : message;
        return result;
    }

    private void WriteLogBestEffort(string projectRoot, UiComposerPublishAllResult result)
    {
        if (string.IsNullOrWhiteSpace(projectRoot) || result == null) return;
        try
        {
            string directory = Path.Combine(Path.GetFullPath(projectRoot), "Logs", "UiComposerPublish");
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, result.OperationId + ".json");
            result.LogPath = path;
            File.WriteAllText(path, JsonSerializer.Serialize(result, _jsonOptions) + Environment.NewLine, new UTF8Encoding(false));
        }
        catch (Exception ex)
        {
            result.EditorOwnership.Warning = string.IsNullOrWhiteSpace(result.EditorOwnership.Warning)
                ? "Publish log could not be written: " + ex.Message
                : result.EditorOwnership.Warning + " Publish log could not be written: " + ex.Message;
        }
    }

    private static UiComposerExpectedHashes Hashes(UiComposerAuthoringState state) => new()
    {
        BlueprintHash = state.BlueprintHash,
        RecipeCatalogHash = state.RecipeCatalogHash,
        CatalogHash = state.CatalogHash,
        ContractHash = state.ContractHash,
        SourceXmlHash = state.SourceXmlHash,
        MappingHash = state.MappingHash
    };

    private static Dictionary<string, string> HashDictionary(UiComposerAuthoringState state) => new(StringComparer.Ordinal)
    {
        ["blueprint"] = state.BlueprintHash,
        ["recipes"] = state.RecipeCatalogHash,
        ["catalog"] = state.CatalogHash,
        ["contract"] = state.ContractHash,
        ["sourceXml"] = state.SourceXmlHash,
        ["mapping"] = state.MappingHash
    };

    private static string BuildMutexName(string root, string packageId)
    {
        string identity = Path.GetFullPath(root).ToUpperInvariant() + "\n" + packageId;
        string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity))).ToLowerInvariant();
        return "MxUiComposerPublishAll-" + hash[..24];
    }

    private static bool WaitForExclusiveFile(string path, int timeoutMilliseconds)
    {
        Stopwatch watch = Stopwatch.StartNew();
        while (watch.ElapsedMilliseconds < timeoutMilliseconds)
        {
            try
            {
                if (!File.Exists(path)) return true;
                using FileStream stream = new(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                return true;
            }
            catch (IOException) { Thread.Sleep(PollMilliseconds); }
            catch (UnauthorizedAccessException) { return false; }
        }
        return false;
    }

    private static string Full(string root, string relative) => Path.GetFullPath(Path.Combine(root, (relative ?? string.Empty).Replace('/', Path.DirectorySeparatorChar)));
    private static string Relative(string root, string path) => Path.GetRelativePath(root, path).Replace(Path.DirectorySeparatorChar, '/');
    private static bool PathsEqual(string left, string right) => string.Equals(Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar), Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase);
    private static bool IsWithin(string path, string root) => Path.GetFullPath(path).StartsWith(Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    private static string HashFile(string path)
    {
        using SHA256 hash = SHA256.Create();
        return Convert.ToHexString(hash.ComputeHash(File.ReadAllBytes(path))).ToLowerInvariant();
    }

    private sealed class UiComposerPublishException : Exception
    {
        public UiComposerPublishException(UiComposerPublishFailureCode code, string message) : base(message) => Code = code;
        public UiComposerPublishFailureCode Code { get; }
    }
}

internal sealed class UnityMcpPublishVerificationClient
{
    private readonly string _projectRoot;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly string _endpoint;
    private string _sessionId = string.Empty;
    private int _requestId;

    public UnityMcpPublishVerificationClient(string projectRoot, JsonSerializerOptions jsonOptions)
    {
        _projectRoot = Path.GetFullPath(projectRoot);
        _jsonOptions = jsonOptions;
        _endpoint = ResolveUnityMcpEndpoint();
    }

    public UiComposerExternalVerificationResult ValidateUnityScripts()
    {
        if (string.IsNullOrWhiteSpace(_endpoint))
            return Failed("Unity MCP endpoint was not found in the active Codex configuration.");
        try
        {
            Initialize();
            JsonNode response = ExpandEmbeddedJson(CallTool("execute_custom_tool", new JsonObject
            {
                ["tool_name"] = "validate_unity_scripts",
                ["parameters"] = new JsonObject()
            }));
            string evidence = response?.ToJsonString(_jsonOptions) ?? string.Empty;
            bool passed = ContainsJsonValue(response, "outcome", "passed")
                          && ContainsNestedJsonBoolean(response, "refresh", "completed", true)
                          && ContainsNestedJsonBoolean(response, "compilation", "started", true)
                          && ContainsNestedJsonBoolean(response, "compilation", "finished", true)
                          && ContainsJsonBoolean(response, "domain_reload_observed", true)
                          && ContainsJsonBoolean(response, "editor_ready", true)
                          && ContainsJsonNumber(response, "error_count", 0);
            return new UiComposerExternalVerificationResult
            {
                Success = passed,
                Message = passed ? "Unity script validation passed." : "Unity MCP returned incomplete or failing validation evidence.",
                EvidenceJson = evidence
            };
        }
        catch (Exception ex)
        {
            return Failed("Unity MCP validation failed: " + ex);
        }
    }

    public UiComposerExternalVerificationResult VerifyInteraction(string profile, string operationId)
    {
        if (!string.Equals(profile, "runtime-ai-lab", StringComparison.Ordinal))
            return Failed("Interaction profile is not registered: " + profile);
        if (string.IsNullOrWhiteSpace(_endpoint))
            return Failed("Unity MCP endpoint was not found in the active Codex configuration.");
        try
        {
            Initialize();
            JsonNode open = CallTool("execute_custom_tool", new JsonObject
            {
                ["tool_name"] = "runtime-ai-lab-open-scene",
                ["parameters"] = new JsonObject()
            });
            JsonNode play = CallTool("execute_custom_tool", new JsonObject
            {
                ["tool_name"] = "runtime-ai-lab-enter-play-mode",
                ["parameters"] = new JsonObject()
            });
            if (HasMcpError(open) || HasMcpError(play))
                return Failed("Runtime AI Lab interaction custom tools are not registered or failed.");

            string evidenceDirectory = Path.Combine(
                _projectRoot,
                "Logs",
                "UiComposerPublish",
                operationId);
            Directory.CreateDirectory(evidenceDirectory);
            JsonNode screenshot = CallTool("manage_camera", new JsonObject
            {
                ["action"] = "screenshot",
                ["capture_source"] = "game_view",
                ["include_image"] = true,
                ["max_resolution"] = 1920,
                ["output_folder"] = evidenceDirectory,
                ["screenshot_file_name"] = "runtime-ai-lab-game-view.png"
            });
            string screenshotPath = SaveScreenshot(screenshot, operationId);
            bool passed = !string.IsNullOrEmpty(screenshotPath) && !HasMcpError(screenshot);
            JsonNode screenshotEvidence = screenshot?.DeepClone();
            RemoveInlineImageData(screenshotEvidence);
            return new UiComposerExternalVerificationResult
            {
                Success = passed,
                Message = passed ? "Runtime AI Lab interaction profile completed." : "Runtime AI Lab Game View evidence was not produced.",
                EvidenceJson = new JsonObject
                {
                    ["openScene"] = open?.DeepClone(),
                    ["enterPlayMode"] = play?.DeepClone(),
                    ["screenshot"] = screenshotEvidence
                }.ToJsonString(_jsonOptions),
                ScreenshotPath = screenshotPath
            };
        }
        catch (Exception ex)
        {
            return Failed("Interaction verification failed: " + ex);
        }
    }

    private void Initialize()
    {
        if (!string.IsNullOrEmpty(_sessionId)) return;
        JsonNode initialized = Send("initialize", new JsonObject
        {
            ["protocolVersion"] = "2025-06-18",
            ["capabilities"] = new JsonObject(),
            ["clientInfo"] = new JsonObject { ["name"] = "mx-ui-composer-publish-all", ["version"] = "1.0.0" }
        }, notification: false);
        if (initialized == null)
            throw new InvalidOperationException("Unity MCP initialize returned no result.");
        Send("notifications/initialized", new JsonObject(), notification: true);
    }

    private JsonNode CallTool(string name, JsonObject arguments) => Send("tools/call", new JsonObject { ["name"] = name, ["arguments"] = arguments }, notification: false);

    private JsonNode Send(string method, JsonObject parameters, bool notification)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(4) };
        using var request = new HttpRequestMessage(HttpMethod.Post, _endpoint);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        if (!string.IsNullOrEmpty(_sessionId)) request.Headers.TryAddWithoutValidation("Mcp-Session-Id", _sessionId);
        var payload = new JsonObject { ["jsonrpc"] = "2.0", ["method"] = method, ["params"] = parameters };
        if (!notification) payload["id"] = ++_requestId;
        request.Content = new StringContent(payload.ToJsonString(_jsonOptions), Encoding.UTF8, "application/json");
        using HttpResponseMessage response = client.Send(request);
        if (response.Headers.TryGetValues("Mcp-Session-Id", out IEnumerable<string> sessions))
            _sessionId = sessions.FirstOrDefault() ?? _sessionId;
        string body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException("Unity MCP HTTP " + (int)response.StatusCode + ": " + body);
        if (notification || string.IsNullOrWhiteSpace(body)) return new JsonObject();
        JsonNode envelope = ParseMcpBody(body);
        if (envelope is JsonArray batch)
            envelope = batch.LastOrDefault(item => item is JsonObject) ?? envelope;
        if (envelope is JsonObject envelopeObject)
        {
            if (envelopeObject["error"] != null)
                throw new InvalidOperationException(envelopeObject["error"].ToJsonString());
            return envelopeObject["result"] ?? envelopeObject;
        }
        return envelope;
    }

    private static JsonNode ParseMcpBody(string body)
    {
        string trimmed = body.Trim();
        if (trimmed.StartsWith("{", StringComparison.Ordinal) || trimmed.StartsWith("[", StringComparison.Ordinal))
            return JsonNode.Parse(trimmed);
        JsonNode last = null;
        foreach (string line in trimmed.Split('\n'))
        {
            if (!line.StartsWith("data:", StringComparison.Ordinal)) continue;
            string data = line[5..].Trim();
            if (!string.IsNullOrEmpty(data)) last = JsonNode.Parse(data);
        }
        return last ?? throw new InvalidOperationException("Unity MCP returned an unsupported response body.");
    }

    private string ResolveUnityMcpEndpoint()
    {
        string overrideUrl = Environment.GetEnvironmentVariable("MX_UNITY_MCP_URL");
        if (!string.IsNullOrWhiteSpace(overrideUrl)) return overrideUrl.Trim();
        string config = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex", "config.toml");
        if (!File.Exists(config)) return string.Empty;
        string text = File.ReadAllText(config);
        Match section = Regex.Match(text, @"(?ms)^\[mcp_servers\.unityMCP\]\s*(.*?)(?=^\[|\z)");
        if (!section.Success) return string.Empty;
        Match url = Regex.Match(section.Groups[1].Value, "(?m)^url\\s*=\\s*\"([^\"]+)\"");
        return url.Success ? url.Groups[1].Value.Trim() : string.Empty;
    }

    private string SaveScreenshot(JsonNode response, string operationId)
    {
        JsonNode image = FindNode(response, node => node is JsonObject obj && string.Equals(obj["type"]?.GetValue<string>(), "image", StringComparison.Ordinal));
        string data = image is JsonObject imageObject ? imageObject["data"]?.GetValue<string>() ?? string.Empty : string.Empty;
        if (string.IsNullOrEmpty(data)) return string.Empty;
        string directory = Path.Combine(_projectRoot, "Logs", "UiComposerPublish", operationId);
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "runtime-ai-lab-game-view.png");
        File.WriteAllBytes(path, Convert.FromBase64String(data));
        return path;
    }

    private static bool ContainsJsonValue(JsonNode node, string property, string expected) => FindNode(node, candidate => candidate is JsonObject obj && string.Equals(obj[property]?.ToString(), expected, StringComparison.OrdinalIgnoreCase)) != null;
    private static bool ContainsJsonBoolean(JsonNode node, string property, bool expected) => FindNode(node, candidate => candidate is JsonObject obj && bool.TryParse(obj[property]?.ToString(), out bool value) && value == expected) != null;
    private static bool ContainsJsonNumber(JsonNode node, string property, int expected) => FindNode(node, candidate => candidate is JsonObject obj && int.TryParse(obj[property]?.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) && value == expected) != null;
    private static bool ContainsNestedJsonBoolean(JsonNode node, string parentProperty, string childProperty, bool expected) =>
        FindNode(node, candidate => candidate is JsonObject obj
                                    && obj[parentProperty] is JsonObject parent
                                    && bool.TryParse(parent[childProperty]?.ToString(), out bool value)
                                    && value == expected) != null;
    private static bool HasMcpError(JsonNode node) => node == null
                                                      || ContainsJsonBoolean(node, "isError", true)
                                                      || ContainsJsonBoolean(node, "success", false)
                                                      || ContainsJsonValue(node, "status", "error");

    private static JsonNode ExpandEmbeddedJson(JsonNode response)
    {
        JsonNode textNode = FindNode(response, candidate => candidate is JsonObject obj
                                                           && string.Equals(obj["type"]?.ToString(), "text", StringComparison.Ordinal)
                                                           && obj["text"] is JsonValue);
        if (textNode is not JsonObject textObject)
            return response;
        string text = textObject["text"]?.GetValue<string>()?.Trim() ?? string.Empty;
        if (!text.StartsWith("{", StringComparison.Ordinal) && !text.StartsWith("[", StringComparison.Ordinal))
            return response;
        try
        {
            return JsonNode.Parse(text) ?? response;
        }
        catch (JsonException)
        {
            return response;
        }
    }

    private static void RemoveInlineImageData(JsonNode node)
    {
        if (node is JsonObject obj)
        {
            if (string.Equals(obj["type"]?.ToString(), "image", StringComparison.Ordinal))
                obj.Remove("data");
            foreach (KeyValuePair<string, JsonNode> pair in obj.ToArray())
                RemoveInlineImageData(pair.Value);
            return;
        }
        if (node is JsonArray array)
        {
            foreach (JsonNode child in array)
                RemoveInlineImageData(child);
        }
    }

    private static JsonNode FindNode(JsonNode node, Func<JsonNode, bool> predicate)
    {
        if (node == null) return null;
        if (predicate(node)) return node;
        if (node is JsonObject obj)
        {
            foreach (KeyValuePair<string, JsonNode> pair in obj)
            {
                JsonNode found = FindNode(pair.Value, predicate);
                if (found != null) return found;
            }
        }
        else if (node is JsonArray array)
        {
            foreach (JsonNode child in array)
            {
                JsonNode found = FindNode(child, predicate);
                if (found != null) return found;
            }
        }
        return null;
    }

    private static UiComposerExternalVerificationResult Failed(string message) => new() { Success = false, Message = message };
}
