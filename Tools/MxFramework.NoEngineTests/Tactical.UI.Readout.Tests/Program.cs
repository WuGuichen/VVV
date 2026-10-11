using System;

namespace MxFramework.NoEngineTests.TacticalUiReadout
{
    internal static class Program
    {
        private static int Main()
        {
            int passed = 0;
            TacticalUiReadoutTests.Run((name, condition) =>
            {
                if (!condition) throw new InvalidOperationException(name);
                passed++;
                Console.WriteLine("PASS " + name);
            });
            Console.WriteLine("Tactical UI readout: " + passed + " production-source checks passed.");
            return 0;
        }
    }
}
