using System;
using Cthangover.Core.Factories;
using Cthangover.Core.Mods.Resolvers;

namespace Cthangover.Mods.Rpg
{
    /// <summary>
    /// Tilesets collected by the shared entity resolver (<c>"Kind": "rpg_tileset"</c>).
    /// <see cref="Publish"/> lets the tileset editor put a just-saved tileset into the resolver's cache,
    /// like <see cref="RpgMapFactory.Publish"/> does for maps.
    /// </summary>
    public class RpgTilesetFactory : FileFactory<RpgTileset>
    {
        private static readonly Lazy<RpgTilesetFactory> instance = new(() => new RpgTilesetFactory());

        /// <summary>Shared instance.</summary>
        public static RpgTilesetFactory Instance => instance.Value;

        /// <summary>The map's tileset with the objects of its extra tilesets (<see cref="RpgTilesetComposer"/>).</summary>
        public RpgTileset ForMap(RpgMap map)
        {
            if (map == null)
                return null;
            var extras = new System.Collections.Generic.List<RpgTileset>();
            foreach (var id in map.Tilesets ?? new System.Collections.Generic.List<string>())
                extras.Add(Get(id));
            return RpgTilesetComposer.Compose(Get(map.Tileset), extras);
        }

        /// <summary>Replaces the cached definition of <paramref name="tileset"/>'s id.</summary>
        public void Publish(RpgTileset tileset)
        {
            if (tileset != null && !string.IsNullOrEmpty(tileset.ID))
                Entities.Collect<RpgTileset>()[tileset.ID] = tileset;
        }
    }
}
