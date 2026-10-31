namespace Cthangover.Mods.Rpg
{
    using System;
    using System.Collections.Generic;
    using Cthangover.Core.Games;
    using Cthangover.Core.Settings;
    using Cthangover.Core.Utils;
    using Godot;

    /// <summary>
    /// Who follows the leader on the map (plan 14.H): the battle set in roster order without the
    /// leader (<see cref="RpgPlayerLook.LeaderId"/>), each with the walking sheet of its own character.
    /// A follower without a <c>Charset</c> stays in the party (it still walks the trail) but is not
    /// drawn; that is an ERROR, logged once per character.
    /// </summary>
    public static class RpgPartyLook
    {
        private static readonly HashSet<string> reported = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>The followers as (character id, walking sheet or null), in party order.</summary>
        public static List<(string Id, string Charset)> Followers()
        {
            var result = new List<(string Id, string Charset)>();
            var roster = GameData.Instance?.Runtime?.CharacterData;
            if (roster == null)
                return result;
            var leader = RpgPlayerLook.LeaderId();
            foreach (var id in roster.Characters.Keys)
            {
                if (!roster.BattleSet.Contains(id) || string.Equals(id, leader, StringComparison.OrdinalIgnoreCase))
                    continue;
                var charset = RpgPlayerLook.CharsetOfId(id);
                if (charset == null && reported.Add(id))
                {
                    GameLogger.Log("RPG", $"follower '{id}' has no 'Charset': it walks in the party but is not drawn", LogLevel.Error);
                }
                result.Add((id, charset));
            }
            return result;
        }

        /// <summary>True when two follower lists name the same characters with the same walking sheets, in the same order.</summary>
        public static bool Same(IReadOnlyList<(string Id, string Charset)> a, IReadOnlyList<(string Id, string Charset)> b)
        {
            if (a.Count != b.Count)
                return false;
            for (var i = 0; i < a.Count; i++)
            {
                if (!string.Equals(a[i].Id, b[i].Id, StringComparison.OrdinalIgnoreCase) || a[i].Charset != b[i].Charset)
                    return false;
            }
            return true;
        }
    }
}
