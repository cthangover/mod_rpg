namespace Cthangover.Mods.Rpg
{
    using Cthangover.Core.Bootstrap;
    using Cthangover.Core.Mods;

    /// <summary>
    /// Wires the rpg mod into the session: the new-game reset of the position.
    /// The world itself is not created here — <see cref="RpgWorldHost"/> sits in the shell scene (scene patch)
    /// and mounts a map whenever a map scene is entered. Save data goes through
    /// <see cref="RpgSaveable"/>, found by the kernel automatically. Battles are not wired here:
    /// the <c>rpg__battles</c> integration brings them in with <c>turn_battle_base</c>.
    /// </summary>
    public class RpgModInitializer : IModInitializer
    {
        /// <summary>Nothing per mod.</summary>
        public void OnModLoaded(string modId)
        {
        }

        /// <summary>Registers the reset (deduplicated by id).</summary>
        public void OnModResourcesReady()
        {
            AppServices.Current.RegisterResettable(new RpgStateReset());
        }
    }
}
