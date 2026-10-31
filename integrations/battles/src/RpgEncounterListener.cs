namespace Cthangover.Mods.Rpg.Battles
{
    using System;
    using System.Text.Json;
    using Cthangover.Core.Utils;
    using Cthangover.Mods.Rpg.Model;
    using Cthangover.Mods.Rpg.Play;

    /// <summary>
    /// Random encounters: after every plain step on a map that has an <c>Encounters</c> field
    /// (<see cref="RpgEncounterTable"/>), the roll may start a battle against one of its groups.
    /// The table is read from <see cref="RpgMap.ExtraFields"/> on each step (a small JSON element, so no cache
    /// that a change of the map would have to invalidate); a broken field is an ERROR and the map stays safe.
    /// </summary>
    public class RpgEncounterListener : IRpgPlainStepListener
    {
        private readonly Random random = new();

        /// <inheritdoc />
        public void OnPlainStep(RpgWorldSession session)
        {
            var enemies = RpgEncounterRoller.Roll(TableOf(session.Map), random);
            if (enemies != null)
                RpgBattleService.Start(enemies, null);
        }

        /// <summary>The map's encounter table; an empty table (no battles) when the map has none.</summary>
        public static RpgEncounterTable TableOf(RpgMap map)
        {
            var element = FieldOf(map, "Encounters");
            if (element == null || element.Value.ValueKind == JsonValueKind.Null)
                return new RpgEncounterTable();
            try
            {
                return element.Value.Deserialize<RpgEncounterTable>(JsonOptionsProvider.ReadOptions) ?? new RpgEncounterTable();
            }
            catch (JsonException ex)
            {
                GameLogger.Log("RPG", $"map '{map.ID}': Encounters is not {{Steps, Groups}}, no random battles: {ex.Message}", LogLevel.Error);
                return new RpgEncounterTable();
            }
        }

        private static JsonElement? FieldOf(RpgMap map, string key)
        {
            if (map.ExtraFields == null)
                return null;
            foreach (var pair in map.ExtraFields)
            {
                if (string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase))
                    return pair.Value;
            }
            return null;
        }
    }
}
