namespace Cthangover.Mods.Rpg.Model
{
    using System.Collections.Generic;

    /// <summary>
    /// What happens when the player uses or steps on a placed object. <see cref="Scenario"/> and
    /// <see cref="Commands"/> may be combined (the scenario plays first, then the lines, e.g. a
    /// shared pickup scenario plus <c>flag self.picked 1</c>); <see cref="Teleport"/> stands alone.
    /// <c>self.&lt;name&gt;</c> in the lines means the flag <c>&lt;placement id&gt;:&lt;name&gt;</c>.
    /// A state replaces this component wholesale, which is how a locked door loses its teleport.
    /// </summary>
    public class RpgInteract
    {
        /// <summary>Use (action key) or Step (entering any footprint cell).</summary>
        public RpgInteractTrigger Trigger { get; set; } = RpgInteractTrigger.Use;

        /// <summary>Scenario path in the <c>scenarios</c> group.</summary>
        public string Scenario { get; set; }

        /// <summary>Inline scenario lines.</summary>
        public List<string> Commands { get; set; }

        /// <summary>Map change; excludes <see cref="Scenario"/> and <see cref="Commands"/>.</summary>
        public RpgTeleport Teleport { get; set; }
    }
}
