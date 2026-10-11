using MxFramework.Game.CharacterTest.TacticalCard;
using MxFramework.Gameplay;
using MxFramework.CharacterAction;

int checkedCases = 0;
var actor = new GameplayEntityId(1, 1);
var binding = new TacticalExecutionInputBinding(1, 3, 10, 2, actor);
var rules = new TacticalPointInputRules(5000, TacticalPointRangeCenter.Both, true);
var geometry = new Geometry();
var origin = new TacticalWorldPoint(0, 0, 0);
var point = new TacticalWorldPoint(3000, 0, 4000);
var input = new TacticalExecutionInput(binding, TacticalExecutionInputSource.Manual, point, origin);
Check("range boundary accepted", TacticalInputStatus.Valid, Validate(input));
Check("missing point has no origin fallback", TacticalInputStatus.MissingInput, Validate(default));
Check("character parameter cannot be point", TacticalInputStatus.WrongInputKind,
    Validate(new TacticalExecutionInput(binding, TacticalExecutionInputSource.Manual, new GameplayEntityId(3, 1))));
Check("stale card version", TacticalInputStatus.StaleBinding, Validate(input,
    new TacticalExecutionInputBinding(1, 3, 10, 3, actor)));
Check("stale stage", TacticalInputStatus.StaleBinding, Validate(input,
    new TacticalExecutionInputBinding(2, 3, 10, 2, actor)));
Check("other actor", TacticalInputStatus.StaleBinding, Validate(input,
    new TacticalExecutionInputBinding(1, 3, 10, 2, new GameplayEntityId(2, 1))));
Check("same actor index with new generation is stale", TacticalInputStatus.StaleBinding, Validate(input,
    new TacticalExecutionInputBinding(1, 3, 10, 2, new GameplayEntityId(1, 2))));
Check("delayed actor disappearance", TacticalInputStatus.CharacterUnavailable,
    TacticalExecutionInputValidation.ValidatePoint(input, binding, rules, origin, origin, false, geometry));
Check("delayed origin change", TacticalInputStatus.OutOfRange,
    TacticalExecutionInputValidation.ValidatePoint(input, binding, rules, origin, new TacticalWorldPoint(-3000, 0, 0), true, geometry));
Check("client cannot move selection origin to fake legal range", TacticalInputStatus.StaleBinding,
    Validate(new TacticalExecutionInput(binding, TacticalExecutionInputSource.Manual, point, point)));
Check("point outside circle", TacticalInputStatus.OutOfRange,
    Validate(new TacticalExecutionInput(binding, TacticalExecutionInputSource.Manual, new TacticalWorldPoint(4000, 0, 4000), origin)));
Check("extreme integer coordinates do not overflow", TacticalInputStatus.OutOfRange,
    Validate(new TacticalExecutionInput(binding, TacticalExecutionInputSource.Manual, new TacticalWorldPoint(int.MaxValue, 0, int.MinValue), origin)));
geometry.Ground = TacticalInputStatus.InvalidGround;
Check("invalid ground", TacticalInputStatus.InvalidGround, Validate(input));
geometry.Ground = TacticalInputStatus.Valid; geometry.Path = TacticalInputStatus.Unreachable;
Check("unreachable point", TacticalInputStatus.Unreachable, Validate(input));
geometry.Path = TacticalInputStatus.PathUnavailable;
Check("query unsupported is not success", TacticalInputStatus.PathUnavailable, Validate(input));
geometry.Path = TacticalInputStatus.Valid;
Check("no geometry is not direct movement fallback", TacticalInputStatus.GroundUnavailable,
    TacticalExecutionInputValidation.ValidatePoint(input, binding, rules, origin, origin, true, null));
Check("automatic follows same validation", TacticalInputStatus.Valid,
    Validate(new TacticalExecutionInput(binding, TacticalExecutionInputSource.Automatic, point, origin)));
Check("world zero can be explicit selected point", TacticalInputStatus.Valid,
    Validate(new TacticalExecutionInput(binding, TacticalExecutionInputSource.Manual, origin, origin)));
Expect("nonfinite click rejected", !TacticalWorldPoint.TryFromMeters(double.NaN, 0, 0, out _));
Expect("overflow click rejected", !TacticalWorldPoint.TryFromMeters(double.MaxValue, 0, 0, out _));
Expect("deterministic millimeter conversion", TacticalWorldPoint.TryFromMeters(1.2505, 0, -1.2505, out var encoded)
    && encoded.Equals(new TacticalWorldPoint(1251, 0, -1251)));
var ledger = new MomentumLedger(50);
var deck = new TacticalDeckManager(3);
deck.InitializeDeck(new[] { new TacticalActionCardDefinition("fixture.action", "fixture", "", 10, new CharacterActionId(1)) }, 1);
var time = new TacticalTimeCoordinator(new TimeAdapter()); time.Initialize();
var card = deck.Hand[0];
Expect("existing selection reserves once", deck.SetPendingAction(card, ledger) == CardPlayFailure.None && ledger.Current == 50 && ledger.Reserved == 10);
var requestBinding = new TacticalExecutionInputBinding(1, 1, card.InstanceId, card.Version, actor);
var requestInput = new TacticalExecutionInput(requestBinding, TacticalExecutionInputSource.Manual, point, origin);
var port = new InputPort { Expected = requestBinding, Rules = rules, Geometry = geometry };
var driver = new TacticalSingleCardDriver(port, time, deck, ledger);
Expect("missing input does not admit or consume", !driver.TryCommit(0, out _) && port.AdmissionCount == 0 && ledger.Current == 50 && ledger.Reserved == 10);
Expect("wrong card cannot bind input", deck.SetPendingExecutionInput(requestInput,
    new TacticalExecutionInputBinding(1, 1, card.InstanceId + 1, card.Version, actor)) == TacticalInputStatus.StaleBinding);
Expect("pending parameter reuses original reservation", deck.SetPendingExecutionInput(requestInput, requestBinding) == TacticalInputStatus.Valid
    && ledger.Reserved == 10 && deck.PendingExecutionInput.Point.Equals(point));
port.Origin = new TacticalWorldPoint(-100000, 0, 0);
Expect("delayed check rejects before payment", !driver.TryCommit(0, out _) && port.AdmissionCount == 0 && ledger.Current == 50 && ledger.Reserved == 10);
port.Origin = origin; port.ChangeOriginOnPrepare = true;
Expect("admission rechecks changes after prepare", !driver.TryCommit(0, out _) && port.AdmissionCount == 0 && ledger.Current == 50 && ledger.Reserved == 10);
port.Origin = origin; port.ChangeOriginOnPrepare = false;
Expect("one confirmation admits and commits original reservation", driver.TryCommit(0, out _) && port.AdmissionCount == 1
    && ledger.Current == 40 && ledger.Reserved == 0 && driver.ExecutionId != 0);
Expect("commit transfers frozen input and clears pending", port.Admitted.Point.Equals(point) && deck.PendingExecutionInput.Kind == TacticalExecutionInputKind.None);
Expect("repeat confirmation cannot admit or charge twice", !driver.TryCommit(0, out _) && port.AdmissionCount == 1 && ledger.Current == 40);
var cancelLedger = new MomentumLedger(50);
var cancelDeck = new TacticalDeckManager(3);
cancelDeck.InitializeDeck(new[] { new TacticalActionCardDefinition("fixture.cancel", "cancel", "", 10, new CharacterActionId(1)) }, 1);
var cancelCard = cancelDeck.Hand[0]; cancelDeck.SetPendingAction(cancelCard, cancelLedger);
var cancelBinding = new TacticalExecutionInputBinding(1, 1, cancelCard.InstanceId, cancelCard.Version, actor);
var cancelInput = new TacticalExecutionInput(cancelBinding, TacticalExecutionInputSource.Manual, point, origin);
cancelDeck.SetPendingExecutionInput(cancelInput, cancelBinding);
Expect("cancel releases existing reservation and point", cancelDeck.CancelPendingAction(cancelLedger) == CardPlayFailure.None
    && cancelLedger.Current == 50 && cancelLedger.Reserved == 0 && cancelDeck.PendingExecutionInput.Kind == TacticalExecutionInputKind.None);
cancelDeck.SetPendingAction(cancelDeck.Hand[0], cancelLedger);
Expect("selection cannot inherit cancelled point", cancelDeck.PendingExecutionInput.Kind == TacticalExecutionInputKind.None);
var legacyLedger = new MomentumLedger(50);
var legacyDeck = new TacticalDeckManager(3);
legacyDeck.InitializeDeck(new[] { new TacticalActionCardDefinition("fixture.legacy", "legacy", "", 10, new CharacterActionId(1)) }, 1);
var legacyTime = new TacticalTimeCoordinator(new TimeAdapter()); legacyTime.Initialize();
legacyDeck.SetPendingAction(legacyDeck.Hand[0], legacyLedger);
var legacyDriver = new TacticalSingleCardDriver(new LegacyPort(), legacyTime, legacyDeck, legacyLedger);
Expect("input-free execution port keeps original admission", legacyDriver.TryCommit(0, out _) && legacyLedger.Current == 40);
foreach (bool executionStarted in new[] { false, true })
{
    var moveRules = new TacticalPointMoveRules(5000, 100, 180, 1000, true, TacticalPointMoveStartPolicy.OwnedMoveAccepted);
    var moveDefinition = TacticalActionCardDefinition.CreatePointMove("fixture.point." + executionStarted, "point", "", 10,
        moveRules, 2f, CardDestination.Discard);
    var moveLedger = new MomentumLedger(50);
    var moveDeck = new TacticalDeckManager(3);
    moveDeck.InitializeDeck(new[] { moveDefinition }, 1);
    var cooldown = new TacticalCooldownTracker(); moveDeck.BindCooldownTracker(cooldown, 1);
    var moveTime = new TacticalTimeCoordinator(new TimeAdapter()); moveTime.Initialize();
    moveDeck.SetPendingAction(moveDeck.Hand[0], moveLedger);
    var movePort = new SettlementPort { Started = executionStarted };
    var moveDriver = new TacticalSingleCardDriver(movePort, moveTime, moveDeck, moveLedger);
    Expect("pure movement confirmation retains original reservation", moveDriver.TryCommit(0, out _) && moveLedger.Current == 50 && moveLedger.Reserved == 10);
    bool finished = false;
    for (int tick = 1; tick <= 12 && !finished; tick++)
    {
        var sample = new SettleSample(tick, false, false, true, false, false, false, false, false, true, false, 0, 0);
        finished = moveDriver.OnCompletedSimulationTick(sample);
    }
    Expect("pure movement settles without faking a physical action", finished && moveDriver.ExecutionId == 0);
    Expect("refund follows execution start", moveLedger.Current == (executionStarted ? 40 : 50));
    Expect("original cooldown follows execution start", cooldown.IsOnCooldown(1, moveDefinition, 12) == executionStarted);
}
Expect("old observation maps physical start to execution start", new ExecutionObservation(1, true, false, false, false, false).ExecutionStarted);
var providerRequest = new TacticalPointProviderRequest(binding, origin, rules);
Check("missing provider explicitly unsupported", TacticalInputStatus.UnsupportedProvider,
    TacticalPointProviderInput.TryCreate(null, providerRequest, out var automatic));
Expect("unsupported provider creates no input", automatic.Kind == TacticalExecutionInputKind.None);
var provider = new PointProvider { Capability = TacticalPointProviderCapability.Unsupported, Point = point };
Check("unsupported capability is not called", TacticalInputStatus.UnsupportedProvider,
    TacticalPointProviderInput.TryCreate(provider, providerRequest, out automatic));
Expect("unsupported provider has no side effects", provider.Calls == 0);
provider.Capability = TacticalPointProviderCapability.WorldPoint; provider.Status = TacticalInputStatus.PointUnavailable;
Check("missing automatic point has no default fallback", TacticalInputStatus.PointUnavailable,
    TacticalPointProviderInput.TryCreate(provider, providerRequest, out automatic));
Expect("unavailable provider creates no input", automatic.Kind == TacticalExecutionInputKind.None);
provider.Status = TacticalInputStatus.Valid;
Check("automatic input is generated from owner capture", TacticalInputStatus.Valid,
    TacticalPointProviderInput.TryCreate(provider, providerRequest, out automatic));
Expect("provider cannot supply card binding or center", automatic.Binding.Equals(binding) && automatic.SelectionOrigin.Equals(origin)
    && automatic.Source == TacticalExecutionInputSource.Automatic);
Check("automatic point shares manual validation", TacticalInputStatus.Valid, Validate(automatic));
provider.Point = new TacticalWorldPoint(100000, 0, 0);
TacticalPointProviderInput.TryCreate(provider, providerRequest, out automatic);
Check("automatic provider cannot bypass card range", TacticalInputStatus.OutOfRange, Validate(automatic));
Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new { passed = checkedCases, failed = 0,
    scope = "production input, common validation, original Deck/Ledger/Driver transactions with controlled geometry/execution ports; no UI/Unity/physics evidence" }));

TacticalInputStatus Validate(TacticalExecutionInput value, TacticalExecutionInputBinding? expected = null)
    => TacticalExecutionInputValidation.ValidatePoint(value, expected ?? binding, rules, origin, origin, true, geometry);
void Check(string name, TacticalInputStatus expected, TacticalInputStatus actual) => Expect(name, expected == actual);
void Expect(string name, bool valid) { if (!valid) throw new Exception(name); checkedCases++; }

sealed class Geometry : ITacticalPointValidationPort
{
    public TacticalInputStatus Ground = TacticalInputStatus.Valid, Path = TacticalInputStatus.Valid;
    public TacticalInputStatus ValidateGround(GameplayEntityId actor, TacticalWorldPoint point) => Ground;
    public TacticalInputStatus ValidatePath(GameplayEntityId actor, TacticalWorldPoint origin, TacticalWorldPoint point) => Path;
}

sealed class TimeAdapter : ITacticalTimeAdapter
{
    public bool IsSimulationPaused { get; private set; }
    public void SetSimulationPaused(bool paused) => IsSimulationPaused = paused;
}
sealed class InputPort : ITacticalExecutionInputPort
{
    public TacticalExecutionInputBinding Expected;
    public TacticalPointInputRules Rules;
    public TacticalWorldPoint Origin;
    public ITacticalPointValidationPort Geometry;
    public bool ChangeOriginOnPrepare;
    public int AdmissionCount;
    public TacticalExecutionInput Admitted;
    public TacticalExecutionInputKind GetRequiredInputKind(TacticalCardInstance card) => TacticalExecutionInputKind.WorldPoint;
    public TacticalInputStatus ValidateInput(TacticalCardInstance card, in TacticalExecutionInput input)
        => TacticalExecutionInputValidation.ValidatePoint(input, Expected, Rules, default, Origin, true, Geometry);
    public bool TryPrepare(TacticalCardInstance card, in TacticalExecutionInput input, out string issue)
    {
        issue = "fixture";
        if (ValidateInput(card, input) != TacticalInputStatus.Valid) return false;
        if (ChangeOriginOnPrepare) Origin = new TacticalWorldPoint(-100000, 0, 0);
        return true;
    }
    public bool TryAdmit(TacticalCardInstance card, in TacticalExecutionInput input, out ulong executionId, out string issue)
    {
        executionId = 0; issue = "fixture";
        if (ValidateInput(card, input) != TacticalInputStatus.Valid) return false;
        AdmissionCount++; Admitted = input; executionId = 1; return true;
    }
    public bool TryPrepare(TacticalCardInstance card, out string issue) { issue = "input required"; return false; }
    public bool TryAdmit(TacticalCardInstance card, out ulong executionId, out string issue) { executionId = 0; issue = "input required"; return false; }
    public bool TryAbortPreAction(ulong executionId, string reason) => true;
    public ExecutionObservation Observe(ulong executionId) => default;
}
sealed class LegacyPort : ITacticalExecutionPort
{
    public bool TryPrepare(TacticalCardInstance card, out string issue) { issue = ""; return true; }
    public bool TryAdmit(TacticalCardInstance card, out ulong executionId, out string issue) { executionId = 1; issue = ""; return true; }
    public bool TryAbortPreAction(ulong executionId, string reason) => true;
    public ExecutionObservation Observe(ulong executionId) => default;
}
sealed class SettlementPort : ITacticalExecutionPort
{
    public bool Started;
    public bool TryPrepare(TacticalCardInstance card, out string issue) { issue = ""; return true; }
    public bool TryAdmit(TacticalCardInstance card, out ulong executionId, out string issue) { executionId = 1; issue = ""; return true; }
    public bool TryAbortPreAction(ulong executionId, string reason) => true;
    public ExecutionObservation Observe(ulong executionId) => new ExecutionObservation(executionId, false, false, true, true, true, Started);
}
sealed class PointProvider : ITacticalPointProvider
{
    public TacticalPointProviderCapability Capability { get; set; }
    public TacticalInputStatus Status; public TacticalWorldPoint Point; public int Calls;
    public TacticalInputStatus TryGetPoint(in TacticalPointProviderRequest request, out TacticalWorldPoint point)
    { Calls++; point = Point; return Status; }
}
