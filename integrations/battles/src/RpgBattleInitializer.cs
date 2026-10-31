namespace Cthangover.Mods.Rpg.Battles
{
    using Cthangover.Core.Battle;
    using Cthangover.Core.Events;
    using Cthangover.Core.Mods;

    /// <summary>
    /// Subscribes the win-flag handler to the end of every battle. The subscription is owned by this
    /// mod's id, so it goes away when the integration unloads.
    /// </summary>
    public class RpgBattleInitializer : IModInitializer
    {
        /// <inheritdoc />
        public void OnModLoaded(string modId)
        {
            ModEventBus.Subscribe<BattleEndedEvent>(RpgBattleService.OnBattleEnded, modId: modId);
        }

        /// <inheritdoc />
        public void OnModResourcesReady()
        {
        }
    }
}
