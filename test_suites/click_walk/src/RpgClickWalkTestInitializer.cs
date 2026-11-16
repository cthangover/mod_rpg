using System;
using Cthangover.Core.Mods;
using Godot;

namespace Cthangover.Mods.Rpg.ClickWalkTests
{
    /// <summary>Queues the terminating native driver only for this suite's exact activation argument.</summary>
    public sealed class RpgClickWalkTestInitializer : IModInitializer
    {
        private static bool queued;

        /// <summary>No per-mod mutation; all fixtures belong to the inactive suite.</summary>
        public void OnModLoaded(string modId)
        {
        }

        /// <summary>Attaches one tree-owned driver after resources become available; normal games never mount this suite.</summary>
        public void OnModResourcesReady()
        {
            var active = false;
            string shots = null;
            foreach (var arg in OS.GetCmdlineUserArgs())
            {
                if (arg == "--itest=rpg_click_walk")
                {
                    active = true;
                }

                if (arg.StartsWith("--shots=", StringComparison.Ordinal))
                {
                    shots = arg.Substring("--shots=".Length);
                }
            }

            if (!active || queued)
            {
                return;
            }

            queued = true;
            ModRoot.AddDeferred(new RpgClickWalkTestDriver(shots));
        }
    }
}
