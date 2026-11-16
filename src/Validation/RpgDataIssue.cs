namespace Cthangover.Mods.Rpg.Validation
{
    /// <summary>A serializable diagnostic names a rule and an exact input field without retaining or changing its document.</summary>
    public sealed class RpgDataIssue
    {
        /// <summary>Stable rule identifier; blocking policy is separate from detection and the content inventory.</summary>
        public string Code { get; set; }

        /// <summary>Path within the effective object or map, including a part's authored index.</summary>
        public string Field { get; set; }

        /// <summary>Invariant detail such as the actual dimensions; presentation supplies the translated explanation.</summary>
        public string Detail { get; set; }

        /// <summary>Referenced object identity, or the map identity for a map-level issue.</summary>
        public string Resource { get; set; }

        /// <summary>Selectable placement identity when one exists; empty for painted layers.</summary>
        public string Placement { get; set; }

        /// <summary>Resolved source or current document path, without claiming an unavailable upstream edit history.</summary>
        public string Source { get; set; }

        /// <summary>Projection in which the input was checked, rather than the graphics backend.</summary>
        public string Projection { get; set; }

        /// <summary>Variant/state context of a separately checked effective input.</summary>
        public string Context { get; set; }

        /// <summary>Number of equivalent uses represented by this row in a map report.</summary>
        public int Uses { get; set; } = 1;
    }
}
