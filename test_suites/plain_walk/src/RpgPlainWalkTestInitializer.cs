namespace Cthangover.Mods.Rpg.PlainWalkTests
{
    using System.Linq;
    using Cthangover.Core.Mods;
    using Cthangover.Core.Utils;

    /// <summary>
    /// Entry point of the rpg_plain_walk test: queues the driver when the exact flag
    /// <c>--itest=rpg_plain_walk</c> is given. The driver quits the process, so it must never run in another test.
    /// </summary>
    public class RpgPlainWalkTestInitializer : IModInitializer
    {
        private static bool driverQueued;

        /// <summary>Nothing per mod.</summary>
        public void OnModLoaded(string modId)
        {
        }

        /// <summary>Queues the driver once.</summary>
        public void OnModResourcesReady()
        {
            var args = Godot.OS.GetCmdlineUserArgs();
            if (driverQueued || !args.Any(a => a == "--itest=rpg_plain_walk"))
                return;

            driverQueued = true;
            GameLogger.Log("TEST", "RpgPlainWalkTestInitializer: activating rpg_plain_walk");
            ModRoot.AddDeferred(new RpgPlainWalkTestDriver());
        }
    }
}
