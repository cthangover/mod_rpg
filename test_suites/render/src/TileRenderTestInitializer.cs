namespace Cthangover.Mods.Rpg.Tests
{
    using Cthangover.Core.Mods;
    using Godot;
    /// <summary>Opt-in renderer contract/preview driver, never active during normal gameplay.</summary>
    public class TileRenderTestInitializer : IModInitializer
    {
        private static bool queued;
        public void OnModLoaded(string id) { }
        public void OnModResourcesReady()
        {
            if (queued) return;
            foreach (var arg in OS.GetCmdlineUserArgs())
                if (arg == "--itest=tile_render")
                { queued = true; ModRoot.AddDeferred(new TileRenderTestDriver()); return; }
        }
    }
}
