using System.Text;
using System.Text.Json;
using MxFramework.Authoring.UiComposer;

namespace MxFramework.NoEngineTests.UI.Composer.Feedback;

internal static class Program
{
    private const string SettingsScreenId = "ui.screen.character_test.settings";
    private const string ContractSchemaV1 = "mx.uiComposerBindingContract.v1";
    private const string ContractSchemaV2 = "mx.uiComposerBindingContract.v2";

    public static int Main()
    {
        try
        {
            string repositoryRoot = FindRepositoryRoot();
            VerifyCurrentContracts(repositoryRoot);
            VerifyV2Preflight(repositoryRoot);
            Console.WriteLine("UI Composer feedback contract tests passed.");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    private static void VerifyCurrentContracts(string repositoryRoot)
    {
        UiComposerAuthoringState legacy = UiComposerService.ReadState(
            repositoryRoot,
            UiComposerService.DemoScreenId);
        Require(
            legacy.BindingContract.SchemaVersion == ContractSchemaV1,
            "Demo must remain a real v1 compatibility fixture.");
        Require(
            legacy.BindingSlots
                .Where(item => !string.IsNullOrWhiteSpace(item.CommandId))
                .All(IsSilent),
            "V1 command bindings must project as silent.");

        UiComposerAuthoringState settings = UiComposerService.ReadState(
            repositoryRoot,
            SettingsScreenId);
        Require(
            settings.BindingContract.SchemaVersion == ContractSchemaV2,
            "Settings must use the v2 feedback contract.");
        Require(
            HasPolicy(
                settings,
                "character_test.settings.close",
                "Back",
                "None",
                "None"),
            "Settings Close must expose Back feedback.");
        Require(
            HasPolicy(
                settings,
                "character_test.settings.apply",
                "None",
                "Confirm",
                "Error"),
            "Settings Apply must expose result feedback.");
    }

    private static void VerifyV2Preflight(string repositoryRoot)
    {
        string tempRoot = Path.Combine(
            Path.GetTempPath(),
            "mx-ui-feedback-contract-" + Guid.NewGuid().ToString("N"));
        try
        {
            CopyDirectory(
                Path.Combine(repositoryRoot, "ArtSource", "UIKits", "MxUiFoundation"),
                Path.Combine(tempRoot, "ArtSource", "UIKits", "MxUiFoundation"));
            Directory.CreateDirectory(Path.Combine(tempRoot, "FGUIProject", "assets"));

            UiComposerAuthoringState baseline = UiComposerService.ReadState(
                tempRoot,
                UiComposerService.DemoScreenId);
            string contractPath = Path.Combine(
                tempRoot,
                "ArtSource",
                "UIKits",
                "MxUiFoundation",
                "contracts",
                "ui_foundation_demo.ui-composer-binding-contract.json");
            UiComposerBindingContract contract =
                JsonSerializer.Deserialize<UiComposerBindingContract>(
                    File.ReadAllText(contractPath));
            contract.SchemaVersion = ContractSchemaV2;
            for (int i = 0; i < contract.Bindings.Length; i++)
                contract.Bindings[i].Feedback = null;
            WriteContract(contractPath, contract);

            UiComposerAuthoringState invalid = UiComposerService.ReadState(
                tempRoot,
                UiComposerService.DemoScreenId);
            Require(
                invalid.Diagnostics.Any(item => item.Code == "UICOMP015"),
                "V2 command bindings without explicit feedback must fail preflight.");

            for (int i = 0; i < contract.Bindings.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(contract.Bindings[i].CommandId))
                    continue;
                contract.Bindings[i].Feedback = new UiComposerFeedbackPolicy
                {
                    Activation = "Click",
                    Success = "None",
                    Failure = "None"
                };
            }
            WriteContract(contractPath, contract);

            UiComposerAuthoringState valid = UiComposerService.ReadState(
                tempRoot,
                UiComposerService.DemoScreenId);
            Require(
                valid.ErrorCount == 0 && valid.ContractHash != baseline.ContractHash,
                "Valid v2 feedback must enter the contract hash and pass preflight.");
        }
        finally
        {
            try
            {
                if (Directory.Exists(tempRoot))
                    Directory.Delete(tempRoot, true);
            }
            catch
            {
            }
        }
    }

    private static bool HasPolicy(
        UiComposerAuthoringState state,
        string commandId,
        string activation,
        string success,
        string failure)
    {
        UiComposerBindingSlot slot = state.BindingSlots.First(
            item => item.CommandId == commandId);
        return slot.Feedback != null &&
               slot.Feedback.Activation == activation &&
               slot.Feedback.Success == success &&
               slot.Feedback.Failure == failure;
    }

    private static bool IsSilent(UiComposerBindingSlot slot)
    {
        return slot.Feedback != null &&
               slot.Feedback.Activation == "None" &&
               slot.Feedback.Success == "None" &&
               slot.Feedback.Failure == "None";
    }

    private static void WriteContract(
        string path,
        UiComposerBindingContract contract)
    {
        File.WriteAllText(
            path,
            JsonSerializer.Serialize(
                contract,
                new JsonSerializerOptions { WriteIndented = true }) +
            Environment.NewLine,
            new UTF8Encoding(false));
    }

    private static void CopyDirectory(string source, string target)
    {
        foreach (string directory in Directory.GetDirectories(
                     source,
                     "*",
                     SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(
                Path.Combine(target, Path.GetRelativePath(source, directory)));
        }
        Directory.CreateDirectory(target);
        foreach (string file in Directory.GetFiles(
                     source,
                     "*",
                     SearchOption.AllDirectories))
        {
            string destination = Path.Combine(
                target,
                Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(file, destination, true);
        }
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null)
        {
            if (File.Exists(Path.Combine(current.FullName, "WGameFramework.sln")))
                return current.FullName;
            current = current.Parent;
        }
        throw new DirectoryNotFoundException("Could not locate repository root.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
