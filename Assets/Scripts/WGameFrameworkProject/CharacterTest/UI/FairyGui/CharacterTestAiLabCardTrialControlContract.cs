namespace MxFramework.Game.CharacterTest
{
    // Shared by the runtime binder and reviewed noEngine Composer contract writer/check.
    internal static class CharacterTestAiLabCardTrialControlContract
    {
        internal const string Screen = "ui.screen.character_test.runtime_ai_lab";
        internal const string Recipe = "ui.component.runtime_ai_lab.card_trial";
        internal const string RowRecipe = "ui.component.runtime_ai_lab.card_trial_row";
        internal const string ChoiceRecipe = "ui.component.runtime_ai_lab.card_trial_choice";
        internal const string CardChoiceRecipe = "ui.component.runtime_ai_lab.card_trial_card_choice";
        internal const string HandCardRecipe = "ui.component.runtime_ai_lab.card_trial_hand_card";
        internal const string Component = "RuntimeAiLabCardTrial";
        internal const string Entry = "cardTrialEntry";
        internal const string Prototype = "cardTrialPrototype";
        internal const string OpenCommand = "runtime-ai-lab.open-card-trial";
        internal static readonly (string Name, string Kind)[] RootControls = {
            ("title", "text"), ("subtitle", "text"), ("returnObservation", "component"),
            ("configurationBackground", "graph"), ("headerBackground", "graph"), ("footerBackground", "graph"),
            ("configurationTitle", "text"), ("player", "component"), ("opponent", "component"),
            ("playerName", "text"), ("opponentName", "text"),
            ("playerRoleLabel", "text"), ("opponentRoleLabel", "text"), ("initialConditionsTitle", "text"),
            ("momentumLabel", "text"), ("initialMomentum", "input"), ("seedLabel", "text"), ("seed", "input"),
            ("costLabel", "text"), ("preparationCost", "input"), ("initialHand", "list"),
            ("momentumInputBackground", "graph"), ("seedInputBackground", "graph"), ("preparationCostInputBackground", "graph"),
            ("configurationHint", "text"), ("addHand", "component"), ("start", "component"), ("resumeTrial", "component"),
            ("reset", "component"), ("editConfiguration", "component"), ("showConditions", "component"), ("conditions", "text"),
            ("trialMenu", "component"), ("trialMenuBackground", "graph"), ("leaveTrial", "component"), ("closeConditions", "component"),
            ("feedback", "text"), ("observationFeedback", "text"), ("worldHint", "text"), ("confirm", "component"), ("cancel", "component"),
            ("observationBackground", "graph"), ("observation", "text"), ("hand", "list"), ("conditionsDetailBackground", "graph"), ("conditionsDetail", "text"), ("frozenHand", "list"),
            ("pickerBackground", "graph"), ("pickerTitle", "text"), ("pickerSearchBackground", "graph"), ("pickerSearch", "input"),
            ("pickerAll", "component"), ("pickerActions", "component"), ("pickerPreparations", "component"),
            ("pickerTools", "component"), ("pickerChoices", "list"), ("pickerCards", "list"), ("pickerCardPreview", "view"), ("pickerPortraitPreview", "component"), ("pickerDetailBackground", "graph"),
            ("pickerDetailTitle", "text"), ("pickerDetail", "text"), ("pickerCancel", "component"), ("pickerApply", "component"),
            ("restartShield", "graph"), ("restartBackground", "graph"), ("restartTitle", "text"), ("restartDetail", "text"),
            ("restartCancel", "component"), ("restartConfirm", "component")
        };
        internal static readonly (string Name, string Kind)[] RowControls = {
            ("face", "view"), ("slotLabel", "text"),
            ("chooseAction", "component"), ("replaceCard", "component"), ("copyCard", "component"), ("remove", "component")
        };
        internal static readonly (string Name, string Kind)[] ChoiceControls = {
            ("select", "component"), ("portrait", "component"), ("name", "text"), ("detail", "text")
        };
        internal static readonly (string Name, string Kind)[] CardChoiceControls = {
            ("face", "view"), ("select", "component")
        };
        internal static readonly (string Name, string Kind)[] HandCardControls = { ("face", "view") };
        internal static readonly string[] ConfigurationControls = {
            "configurationBackground", "configurationTitle", "playerRoleLabel", "player", "playerName", "opponentRoleLabel", "opponent", "opponentName",
            "initialConditionsTitle", "momentumLabel", "momentumInputBackground", "initialMomentum", "seedLabel",
            "seedInputBackground", "seed", "costLabel", "preparationCostInputBackground", "preparationCost",
            "initialHand", "addHand", "configurationHint", "conditions", "start", "resumeTrial", "feedback"
        };
        internal static readonly string[] ObservationControls = {
            "trialMenu", "worldHint", "confirm", "cancel", "observationBackground", "observation", "observationFeedback", "hand"
        };
        internal static readonly string[] TrialMenuControls = {
            "trialMenuBackground", "reset", "editConfiguration", "showConditions", "leaveTrial"
        };
        internal static readonly string[] PickerControls = {
            "pickerBackground", "pickerTitle", "pickerSearchBackground", "pickerSearch", "pickerAll", "pickerActions",
            "pickerPreparations", "pickerTools", "pickerChoices", "pickerCards", "pickerCardPreview", "pickerPortraitPreview", "pickerDetailBackground", "pickerDetailTitle", "pickerDetail",
            "pickerCancel", "pickerApply"
        };
        internal static readonly string[] RestartControls = {
            "restartShield", "restartBackground", "restartTitle", "restartDetail", "restartCancel", "restartConfirm"
        };
    }
}
