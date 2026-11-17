namespace Cthangover.Mods.Rpg.Validation
{
    /// <summary>Immutable catalogue metadata; severity is fixed by the accepted audit rather than current user input.</summary>
    public sealed class RpgDataRule
    {
        /// <summary>Stable value written into finding reports and compared with the frozen audit.</summary>
        public string Code { get; }

        /// <summary>Shared ru/en explanation key, independent of field paths and resource identity.</summary>
        public string MessageKey { get; }

        /// <summary>Whether this audited rule refuses Save or Apply; detection itself never depends on this flag.</summary>
        public bool BlocksSave { get; }

        internal RpgDataRule(string code, string messageKey, bool blocksSave)
        {
            Code = code;
            MessageKey = messageKey;
            BlocksSave = blocksSave;
        }
    }
}
