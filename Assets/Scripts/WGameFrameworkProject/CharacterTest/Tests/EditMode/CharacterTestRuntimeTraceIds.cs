using MxFramework.Runtime;

namespace MxFramework.Tests
{
    /// <summary>
    /// Explicit numeric trace identities used by EditMode fixtures after runtime trace labels
    /// were removed from the production protocol. The comments retain the old fixture label for
    /// review only; labels are never parsed or hashed at runtime.
    /// </summary>
    internal static class CharacterTestRuntimeTraceIds
    {
        public static readonly RuntimeTraceIdentity Attack = new RuntimeTraceIdentity(630001); // attack
        public static readonly RuntimeTraceIdentity Attack1 = new RuntimeTraceIdentity(630002); // attack-1
        public static readonly RuntimeTraceIdentity Attack2 = new RuntimeTraceIdentity(630003); // attack-2
        public static readonly RuntimeTraceIdentity AttackMissingRunner = new RuntimeTraceIdentity(630004); // attack-missing-runner
        public static readonly RuntimeTraceIdentity Adapter = new RuntimeTraceIdentity(630005); // adapter
        public static readonly RuntimeTraceIdentity ArmorBreak = new RuntimeTraceIdentity(630006); // armor-break
        public static readonly RuntimeTraceIdentity Armor = new RuntimeTraceIdentity(610006); // armor
        public static readonly RuntimeTraceIdentity AuraCast = new RuntimeTraceIdentity(610007); // aura-cast
        public static readonly RuntimeTraceIdentity Band = new RuntimeTraceIdentity(610008); // band
        public static readonly RuntimeTraceIdentity Block1 = new RuntimeTraceIdentity(610009); // block-1
        public static readonly RuntimeTraceIdentity Break = new RuntimeTraceIdentity(610010); // break
        public static readonly RuntimeTraceIdentity BrokenHit = new RuntimeTraceIdentity(610011); // broken-hit
        public static readonly RuntimeTraceIdentity CastComponent = new RuntimeTraceIdentity(610012); // cast-component
        public static readonly RuntimeTraceIdentity ComponentSystem = new RuntimeTraceIdentity(610013); // component-system
        public static readonly RuntimeTraceIdentity ComponentWorld = new RuntimeTraceIdentity(610014); // component-world
        public static readonly RuntimeTraceIdentity CohesionBreak = new RuntimeTraceIdentity(630007); // cohesion-break
        public static readonly RuntimeTraceIdentity Cooldown = new RuntimeTraceIdentity(630008); // cooldown
        public static readonly RuntimeTraceIdentity Create = new RuntimeTraceIdentity(610015); // create
        public static readonly RuntimeTraceIdentity CreateComponent = new RuntimeTraceIdentity(610016); // create-component
        public static readonly RuntimeTraceIdentity Custom = new RuntimeTraceIdentity(610017); // custom
        public static readonly RuntimeTraceIdentity Death = new RuntimeTraceIdentity(610018); // death
        public static readonly RuntimeTraceIdentity Debug = new RuntimeTraceIdentity(630009); // debug
        public static readonly RuntimeTraceIdentity DebugNoSink = new RuntimeTraceIdentity(630010); // debug-no-sink
        public static readonly RuntimeTraceIdentity Delayed = new RuntimeTraceIdentity(630011); // delayed
        public static readonly RuntimeTraceIdentity Despawn = new RuntimeTraceIdentity(610019); // despawn
        public static readonly RuntimeTraceIdentity DestroyComponent = new RuntimeTraceIdentity(610020); // destroy-component
        public static readonly RuntimeTraceIdentity Enable = new RuntimeTraceIdentity(610021); // enable
        public static readonly RuntimeTraceIdentity Fail = new RuntimeTraceIdentity(610022); // fail
        public static readonly RuntimeTraceIdentity GatewayCancelOnlyMovement = new RuntimeTraceIdentity(610023); // gateway.cancel-only.movement
        public static readonly RuntimeTraceIdentity GatewayDeathInvalid = new RuntimeTraceIdentity(610024); // gateway.death.invalid
        public static readonly RuntimeTraceIdentity GatewayOrchestrationInvalid = new RuntimeTraceIdentity(610025); // gateway.orchestration.invalid
        public static readonly RuntimeTraceIdentity GatewayHit = new RuntimeTraceIdentity(630020); // gateway.hit
        public static readonly RuntimeTraceIdentity GatewayHitReject = new RuntimeTraceIdentity(630021); // gateway.hit.reject
        public static readonly RuntimeTraceIdentity GatewayDeath = new RuntimeTraceIdentity(630022); // gateway.death
        public static readonly RuntimeTraceIdentity ReactionActive = new RuntimeTraceIdentity(630023); // reaction.active
        public static readonly RuntimeTraceIdentity ReactionDeath = new RuntimeTraceIdentity(630024); // reaction.death
        public static readonly RuntimeTraceIdentity GatewayTransition = new RuntimeTraceIdentity(630025); // gateway.transition
        public static readonly RuntimeTraceIdentity GatewayReject = new RuntimeTraceIdentity(630026); // gateway.reject
        public static readonly RuntimeTraceIdentity GatewayLifecycleOnce = new RuntimeTraceIdentity(630027); // gateway.lifecycle.once
        public static readonly RuntimeTraceIdentity GatewayLandedMismatch = new RuntimeTraceIdentity(630028); // gateway.landed.mismatch
        public static readonly RuntimeTraceIdentity GatewayLanded = new RuntimeTraceIdentity(630029); // gateway.landed
        public static readonly RuntimeTraceIdentity GatewayNoAllocReject = new RuntimeTraceIdentity(630030); // gateway.noalloc.reject
        public static readonly RuntimeTraceIdentity Guard = new RuntimeTraceIdentity(610026); // guard
        public static readonly RuntimeTraceIdentity GuardBreak = new RuntimeTraceIdentity(610027); // guard-break
        public static readonly RuntimeTraceIdentity Handled = new RuntimeTraceIdentity(610028); // handled
        public static readonly RuntimeTraceIdentity Hash = new RuntimeTraceIdentity(610029); // hash
        public static readonly RuntimeTraceIdentity Hit1 = new RuntimeTraceIdentity(610030); // hit-1
        public static readonly RuntimeTraceIdentity HitFull = new RuntimeTraceIdentity(610031); // hit-full
        public static readonly RuntimeTraceIdentity HitIncomplete = new RuntimeTraceIdentity(610032); // hit-incomplete
        public static readonly RuntimeTraceIdentity Hold = new RuntimeTraceIdentity(610033); // hold
        public static readonly RuntimeTraceIdentity Invalid = new RuntimeTraceIdentity(610034); // invalid
        public static readonly RuntimeTraceIdentity Locomotion = new RuntimeTraceIdentity(630012); // locomotion
        public static readonly RuntimeTraceIdentity Life = new RuntimeTraceIdentity(610035); // life
        public static readonly RuntimeTraceIdentity Missing = new RuntimeTraceIdentity(610036); // missing
        public static readonly RuntimeTraceIdentity MissingBinding = new RuntimeTraceIdentity(610037); // missing-binding
        public static readonly RuntimeTraceIdentity MissingCandidate = new RuntimeTraceIdentity(610038); // missing-candidate
        public static readonly RuntimeTraceIdentity MissingConfig = new RuntimeTraceIdentity(610039); // missing-config
        public static readonly RuntimeTraceIdentity MissingRegistry = new RuntimeTraceIdentity(610040); // missing-registry
        public static readonly RuntimeTraceIdentity MissingSet = new RuntimeTraceIdentity(610041); // missing-set
        public static readonly RuntimeTraceIdentity MissingWorld = new RuntimeTraceIdentity(610042); // missing-world
        public static readonly RuntimeTraceIdentity MustNotEnterEvent = new RuntimeTraceIdentity(610043); // must-not-enter-event
        public static readonly RuntimeTraceIdentity One = new RuntimeTraceIdentity(610044); // one
        public static readonly RuntimeTraceIdentity Pb1 = new RuntimeTraceIdentity(610045); // pb-1
        public static readonly RuntimeTraceIdentity Pending = new RuntimeTraceIdentity(610046); // pending
        public static readonly RuntimeTraceIdentity Repeat = new RuntimeTraceIdentity(610047); // repeat
        public static readonly RuntimeTraceIdentity Reset = new RuntimeTraceIdentity(610048); // reset
        public static readonly RuntimeTraceIdentity Save = new RuntimeTraceIdentity(610049); // save
        public static readonly RuntimeTraceIdentity SaveNoRegistry = new RuntimeTraceIdentity(610050); // save-no-registry
        public static readonly RuntimeTraceIdentity Sentinel = new RuntimeTraceIdentity(610051); // sentinel
        public static readonly RuntimeTraceIdentity SetHp = new RuntimeTraceIdentity(610052); // set-hp
        public static readonly RuntimeTraceIdentity SignalBatch = new RuntimeTraceIdentity(610053); // signal-batch
        public static readonly RuntimeTraceIdentity Snapshot = new RuntimeTraceIdentity(630018); // snapshot
        public static readonly RuntimeTraceIdentity Later = new RuntimeTraceIdentity(630019); // later
        public static readonly RuntimeTraceIdentity Spawn = new RuntimeTraceIdentity(610054); // spawn
        public static readonly RuntimeTraceIdentity SpawnEnemy = new RuntimeTraceIdentity(610055); // spawn-enemy
        public static readonly RuntimeTraceIdentity SpawnEvent = new RuntimeTraceIdentity(610056); // spawn-event
        public static readonly RuntimeTraceIdentity SpawnHero = new RuntimeTraceIdentity(610057); // spawn-hero
        public static readonly RuntimeTraceIdentity Stale = new RuntimeTraceIdentity(610058); // stale
        public static readonly RuntimeTraceIdentity StoryHit = new RuntimeTraceIdentity(610059); // story-hit
        public static readonly RuntimeTraceIdentity Strike = new RuntimeTraceIdentity(610060); // strike
        public static readonly RuntimeTraceIdentity Skill = new RuntimeTraceIdentity(630015); // skill
        public static readonly RuntimeTraceIdentity Motion = new RuntimeTraceIdentity(630013); // motion
        public static readonly RuntimeTraceIdentity RootMotion = new RuntimeTraceIdentity(630014); // root-motion
        public static readonly RuntimeTraceIdentity TraceDash = new RuntimeTraceIdentity(630016); // trace.dash
        public static readonly RuntimeTraceIdentity Trace2 = new RuntimeTraceIdentity(630017); // trace-2
        public static readonly RuntimeTraceIdentity TestBufferedJump = new RuntimeTraceIdentity(610061); // test-buffered-jump
        public static readonly RuntimeTraceIdentity TestDebugOverlayPrimary = new RuntimeTraceIdentity(610062); // test-debug-overlay-primary
        public static readonly RuntimeTraceIdentity TestDebugPrimary = new RuntimeTraceIdentity(610063); // test-debug-primary
        public static readonly RuntimeTraceIdentity TestFireballVfx = new RuntimeTraceIdentity(610064); // test-fireball-vfx
        public static readonly RuntimeTraceIdentity TestGameplayAbility = new RuntimeTraceIdentity(610065); // test-gameplay-ability
        public static readonly RuntimeTraceIdentity TestLight = new RuntimeTraceIdentity(610066); // test-light
        public static readonly RuntimeTraceIdentity TestUiDebugPrimary = new RuntimeTraceIdentity(610067); // test-ui-debug-primary
        public static readonly RuntimeTraceIdentity Three = new RuntimeTraceIdentity(610068); // three
        public static readonly RuntimeTraceIdentity TimelineComplete = new RuntimeTraceIdentity(610069); // timeline-complete
        public static readonly RuntimeTraceIdentity TraceCache = new RuntimeTraceIdentity(610071); // trace-cache
        public static readonly RuntimeTraceIdentity Trace403 = new RuntimeTraceIdentity(610070); // trace-403
        public static readonly RuntimeTraceIdentity TracePlan = new RuntimeTraceIdentity(610072); // trace-plan
        public static readonly RuntimeTraceIdentity TraceReject = new RuntimeTraceIdentity(610073); // trace-reject
        public static readonly RuntimeTraceIdentity TraceSource = new RuntimeTraceIdentity(610074); // trace-source
        public static readonly RuntimeTraceIdentity TraceSuccess = new RuntimeTraceIdentity(610075); // trace-success
        public static readonly RuntimeTraceIdentity TraceAbility = new RuntimeTraceIdentity(610077); // trace.ability
        public static readonly RuntimeTraceIdentity TraceCancel = new RuntimeTraceIdentity(610078); // trace.cancel
        public static readonly RuntimeTraceIdentity TraceCommand = new RuntimeTraceIdentity(610079); // trace.command
        public static readonly RuntimeTraceIdentity TraceDeath = new RuntimeTraceIdentity(610080); // trace.death
        public static readonly RuntimeTraceIdentity TraceFullHit = new RuntimeTraceIdentity(610081); // trace.full-hit
        public static readonly RuntimeTraceIdentity TraceHeavy = new RuntimeTraceIdentity(610082); // trace.heavy
        public static readonly RuntimeTraceIdentity TraceJump = new RuntimeTraceIdentity(610083); // trace.jump
        public static readonly RuntimeTraceIdentity TraceLightAttack = new RuntimeTraceIdentity(610084); // trace.light_attack
        public static readonly RuntimeTraceIdentity TracePressure = new RuntimeTraceIdentity(610085); // trace.pressure
        public static readonly RuntimeTraceIdentity TraceReaction = new RuntimeTraceIdentity(610086); // trace.reaction
        public static readonly RuntimeTraceIdentity TraceSourceOnly = new RuntimeTraceIdentity(610087); // trace.source-only
        public static readonly RuntimeTraceIdentity Two = new RuntimeTraceIdentity(610088); // two
        public static readonly RuntimeTraceIdentity Unknown = new RuntimeTraceIdentity(610089); // unknown
        public static readonly RuntimeTraceIdentity ZoneA = new RuntimeTraceIdentity(610090); // zone-a
        public static readonly RuntimeTraceIdentity Legacy = new RuntimeTraceIdentity(610091); // legacy
        public static readonly RuntimeTraceIdentity First = new RuntimeTraceIdentity(610092); // first
        public static readonly RuntimeTraceIdentity Second = new RuntimeTraceIdentity(610093); // second
        public static readonly RuntimeTraceIdentity Rule = new RuntimeTraceIdentity(610094); // rule
        public static readonly RuntimeTraceIdentity TestSpawn = new RuntimeTraceIdentity(610095); // test.spawn
        public static readonly RuntimeTraceIdentity TestStale = new RuntimeTraceIdentity(610096); // test.stale
    }
}
