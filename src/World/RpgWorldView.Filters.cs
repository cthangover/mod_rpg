namespace Cthangover.Mods.Rpg
{
    using System;
    using System.Collections.Generic;
    using Cthangover.Mods.Rpg.Model;
    using Cthangover.Mods.TileRender;

    public partial class RpgWorldView
    {
        /// <summary>
        /// Optional adapters replace presentation data after ordinary state/lighting evaluation. Subscriptions belong
        /// to their mod lifecycle; without subscribers the exact original frame reaches every renderer unchanged.
        /// </summary>
        public static event RpgFrameFilter FrameFiltering;

        /// <summary>
        /// Builds a resolved sprite's drawing descriptions with this world's existing texture cache. Returned looks and
        /// textures are borrowed until the view leaves; an adapter owns only its list/poses and must not dispose the images.
        /// </summary>
        public IReadOnlyList<TileVisual> SpriteLooks(RpgObject resolved)
        {
            if (builder == null)
            {
                throw new InvalidOperationException("Sprite drawing needs a built RPG view");
            }

            return builder.PartLooks(resolved, TileSize);
        }

        private TileFrame FilteredFrame(ITileWorldPresentation presentation)
        {
            var selected = frame;
            FrameFiltering?.Invoke(this, presentation, ref selected);
            return selected ?? throw new InvalidOperationException("An RPG presentation filter returned no frame");
        }
    }
}
