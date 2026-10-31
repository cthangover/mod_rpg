namespace Cthangover.Mods.Rpg.Tests
{
    using Cthangover.Core.Mods;
    using Godot;

    /// <summary>Opt-in map snapshot driver (--itest=rpg_snapshots), never active during normal gameplay.</summary>
    public class RpgSnapshotsInitializer : IModInitializer
    {
        private static bool queued;

        public void OnModLoaded(string id) { }

        public void OnModResourcesReady()
        {
            if (queued) return;
            foreach (var arg in OS.GetCmdlineUserArgs())
                if (arg == "--itest=rpg_snapshots")
                { queued = true; ModRoot.AddDeferred(new RpgSnapshotsDriver()); return; }
        }
    }
}
