namespace Cthangover.Mods.Rpg
{
    using Cthangover.Mods.TileRender;

    /// <summary>
    /// Synchronous presentation hook, including the first frame of a replacement renderer. A subscriber may replace
    /// the borrowed frame with its own reusable storage; it must not mutate the world's cached poses or own the renderer.
    /// </summary>
    public delegate void RpgFrameFilter(RpgWorldView view, ITileWorldPresentation presentation, ref TileFrame frame);
}
