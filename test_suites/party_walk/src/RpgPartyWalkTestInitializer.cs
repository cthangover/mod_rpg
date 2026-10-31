namespace Cthangover.Mods.Rpg.PartyWalkTests
{
    using System.Linq;
    using Cthangover.Core.Mods;
    using Cthangover.Core.Utils;
    using Godot;

    /// <summary>
    /// Entry point of the rpg_party_walk integration test: adds the driver to the tree when the exact
    /// flag <c>--itest=rpg_party_walk</c> is given (the driver quits the process, so it must never run in
    /// another test). Optional <c>--shots=&lt;dir&gt;</c> makes the driver save screenshots.
    /// </summary>
    public class RpgPartyWalkTestInitializer : IModInitializer
    {
        private static bool driverQueued;

        /// <summary>Nothing per mod.</summary>
        public void OnModLoaded(string modId)
        {
        }

        /// <summary>Queues the driver once.</summary>
        public void OnModResourcesReady()
        {
            var args = OS.GetCmdlineUserArgs();
            if (driverQueued || !args.Any(a => a == "--itest=rpg_party_walk"))
                return;

            driverQueued = true;
            var shots = args.Where(a => a.StartsWith("--shots=", System.StringComparison.Ordinal))
                .Select(a => a.Substring("--shots=".Length))
                .FirstOrDefault();
            GameLogger.Log("TEST", $"RpgPartyWalkTestInitializer: activating rpg_party_walk, shots={shots ?? "off"}");
            ModRoot.AddDeferred(new RpgPartyWalkTestDriver(shots));
        }
    }
}
