namespace Cthangover.Mods.Rpg.Tests
{
    using System.Collections.Generic;
    using Cthangover.Core.Actions;
    using Cthangover.Core.Items;
    using Cthangover.Core.Mods;

    /// <summary>Action context for RPG action tests: parameters only, no services.</summary>
    public class RpgFakeActionContext : IActionContext
    {
        /// <summary>Values returned by <see cref="GetParam"/>.</summary>
        public Dictionary<string, string> Params { get; } = new();

        public IQuestService Quests => null;
        public ICharacterService Character => null;
        public ILightingService Lighting => null;
        public ISceneNodeService Scene => null;
        public IModRegistry ModRegistry => null;
        public IInventory Inventory => null;
        public IMapZoneService Map => null;

        public string GetParam(string name) => Params.TryGetValue(name, out var value) ? value : null;

        public void Log(string category, string message)
        {
        }

        public void LogWarning(string category, string message)
        {
        }
    }
}
