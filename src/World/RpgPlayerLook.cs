namespace Cthangover.Mods.Rpg
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;
    using Cthangover.Core.Characters;
    using Cthangover.Core.Games;
    using Cthangover.Core.Mods.Resolvers;
    using Cthangover.Core.Settings;
    using Cthangover.Core.Utils;

    /// <summary>
    /// Who the player looks like on a map. Model v2 (plan section 4): the walking sheet is the
    /// <c>Charset</c> of the party leader's character — the game setting <c>stats.player</c>, else the
    /// first member of the battle set (the same "player" the stats mod resolves) — so a class chosen
    /// in a prologue can change the look through character data, and two games sharing maps keep
    /// their own heroes. A leader without a charset is an ERROR; the world then shows the missing-sheet
    /// placeholder rather than somebody else.
    /// </summary>
    public static class RpgPlayerLook
    {
        /// <summary>Game setting naming the player character (shared with the stats mod).</summary>
        public const string PlayerSettingKey = "stats.player";

        private static string reported;

        /// <summary>The party leader's character id: <see cref="PlayerSettingKey"/>, else the first battle-set member; null without either.</summary>
        public static string LeaderId()
        {
            var setting = ActiveGame.GetSetting(PlayerSettingKey);
            if (!string.IsNullOrWhiteSpace(setting))
                return setting.Trim();
            var roster = GameData.Instance?.Runtime?.CharacterData;
            if (roster == null)
                return null;
            foreach (var id in roster.Characters.Keys)
            {
                if (roster.BattleSet.Contains(id))
                    return id;
            }
            return null;
        }

        /// <summary>The <c>Charset</c> field of a character definition, or null.</summary>
        public static string CharsetOf(CharacterInfo character)
        {
            if (character?.ExtraProperties == null)
                return null;
            foreach (var pair in character.ExtraProperties)
            {
                if (!string.Equals(pair.Key, "Charset", StringComparison.OrdinalIgnoreCase))
                    continue;
                var text = pair.Value switch
                {
                    JsonElement { ValueKind: JsonValueKind.String } element => element.GetString(),
                    string value => value,
                    _ => null,
                };
                return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
            }
            return null;
        }

        /// <summary>
        /// The walking sheet of the leader (<see cref="LeaderId"/>, <see cref="CharsetOf"/>); null with an
        /// ERROR (once per leader) when there is no leader or it has no <c>Charset</c>.
        /// </summary>
        public static string LeaderCharset()
        {
            var leader = LeaderId();
            var charset = CharsetOfId(leader);
            if (charset == null && reported != leader)
            {
                reported = leader;
                GameLogger.Log("RPG", leader == null
                    ? "player look: no party leader (setting 'stats.player' or a party member) to take the walking sheet from"
                    : $"player look: character '{leader}' has no 'Charset'", LogLevel.Error);
            }
            return charset;
        }

        /// <summary>The <c>Charset</c> of the character template <paramref name="id"/> (case-insensitive), or null.</summary>
        public static string CharsetOfId(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;
            var all = Entities.Collect<CharacterInfo>();
            if (!all.TryGetValue(id, out var character))
                character = Find(all, id);
            return CharsetOf(character);
        }

        private static CharacterInfo Find(Dictionary<string, CharacterInfo> all, string id)
        {
            foreach (var pair in all)
            {
                if (string.Equals(pair.Key, id, StringComparison.OrdinalIgnoreCase))
                    return pair.Value;
            }
            return null;
        }
    }
}
