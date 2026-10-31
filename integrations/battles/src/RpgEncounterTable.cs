namespace Cthangover.Mods.Rpg.Battles
{
    using System.Collections.Generic;

    /// <summary>
    /// Random battles of a map, read from the map's <c>Encounters</c> field: after every step the player
    /// takes on it, a battle starts with probability <c>1 / Steps</c> against one of the
    /// <see cref="Groups"/>. Groups are enemy lists in the <c>battle.init</c> format (<c>"slime,slime"</c>),
    /// so they name character templates.
    /// </summary>
    public class RpgEncounterTable
    {
        /// <summary>Preserves mod-defined JSON fields when the editor saves this data.</summary>
        [System.Text.Json.Serialization.JsonExtensionData]
        public System.Collections.Generic.Dictionary<string, System.Text.Json.JsonElement> ExtraFields { get; set; }

        /// <summary>Average number of steps between battles; 0 or less turns encounters off.</summary>
        public int Steps { get; set; }

        /// <summary>Enemy groups, one of them picked uniformly.</summary>
        public List<string> Groups { get; set; } = new();
    }
}
