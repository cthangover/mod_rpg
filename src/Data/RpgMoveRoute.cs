using System.Collections.Generic;

namespace Cthangover.Mods.Rpg
{
    /// <summary>
    /// Autonomous movement of an event (RPG Maker's "move route" / "autonomous movement"):
    /// <c>"Route": { "Steps": ["left", "left", "wait", "right", "right"], "Every": 0.8 }</c>.
    /// Steps: <c>up</c>, <c>down</c>, <c>left</c>, <c>right</c>, <c>random</c> (a random
    /// direction), <c>toward</c> / <c>away</c> (relative to the player), <c>wait</c>, and
    /// <c>face_up</c> … <c>face_right</c> (turn without moving). A step into a blocked cell is
    /// skipped (the event turns that way). The route pauses while a dialog runs or the player
    /// cannot walk; positions are not saved, so an event starts from its cell on every visit.
    /// </summary>
    public class RpgMoveRoute
    {
        /// <summary>Preserves mod-defined JSON fields when the editor saves this data.</summary>
        [System.Text.Json.Serialization.JsonExtensionData]
        public System.Collections.Generic.Dictionary<string, System.Text.Json.JsonElement> ExtraFields { get; set; }

        /// <summary>The steps, repeated from the start when <see cref="Repeat"/> is set.</summary>
        public List<string> Steps { get; set; } = new();
        /// <summary>Alternative complete cycles, shuffled without repeats; empty keeps the legacy Steps route.</summary>
        public List<List<string>> Variants { get; set; } = new();

        /// <summary>Seconds between steps.</summary>
        public double Every { get; set; } = 1.0;

        /// <summary>Walking speed in cells per second, independent of instruction pauses.</summary>
        public float Speed { get; set; } = 4.5f;

        /// <summary>Retry obstructed steps instead of losing the intended path.</summary>
        public bool RetryBlocked { get; set; }

        /// <summary>Whether the route starts over after the last step.</summary>
        public bool Repeat { get; set; } = true;
    }
}
