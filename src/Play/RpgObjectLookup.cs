namespace Cthangover.Mods.Rpg.Play
{
    using System;
    using System.Collections.Generic;
    using Cthangover.Core.Mods.Resolvers;
    using Cthangover.Mods.Rpg.Model;

    /// <summary>
    /// The <c>rpg_object</c> entities of the loaded mods (id override, patches and <c>Extends</c>
    /// already applied by the core resolver), looked up by id without regard to case. One lookup is
    /// taken per built map, so a world never sees half of an editor's republish.
    /// </summary>
    public sealed class RpgObjectLookup
    {
        private readonly Dictionary<string, RpgObject> objects;

        /// <summary>Snapshots the current entities.</summary>
        public RpgObjectLookup()
            : this(Entities.Collect<RpgObject>())
        {
        }

        /// <summary>Looks up <paramref name="objects"/> (tests, tools).</summary>
        public RpgObjectLookup(Dictionary<string, RpgObject> objects)
        {
            this.objects = objects ?? new Dictionary<string, RpgObject>();
        }

        /// <summary>The object <paramref name="id"/>, or null.</summary>
        public RpgObject Find(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;
            if (objects.TryGetValue(id, out var value))
                return value;
            foreach (var pair in objects)
            {
                if (string.Equals(pair.Key, id, StringComparison.OrdinalIgnoreCase))
                    return pair.Value;
            }
            return null;
        }
    }
}
