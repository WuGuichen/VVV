using MxFramework.Input;

namespace MxFramework.Tests
{
    internal static class CharacterTestInputTraceIds
    {
        public static readonly InputTraceIdentity CmdTrace = new InputTraceIdentity(640001); // cmd-trace
        public static readonly InputTraceIdentity StaleAttack = new InputTraceIdentity(640002); // stale-attack
        public static readonly InputTraceIdentity TestDebugPrimary = new InputTraceIdentity(640003); // test-debug-primary
        public static readonly InputTraceIdentity TestDebugOverlayPrimary = new InputTraceIdentity(640004); // test-debug-overlay-primary
        public static readonly InputTraceIdentity TestBufferedJump = new InputTraceIdentity(640005); // test-buffered-jump
        public static readonly InputTraceIdentity TestUiDebugPrimary = new InputTraceIdentity(640006); // test-ui-debug-primary
        public static readonly InputTraceIdentity TestGameplayAbility = new InputTraceIdentity(640007); // test-gameplay-ability
    }
}
