namespace Cthangover.Mods.Rpg.Battles
{
    using Cthangover.Core.Battle;
    using Cthangover.Core.Events;
    using Cthangover.Core.Scenes;
    using Cthangover.Core.Utils;
    using Godot;

    /// <summary>
    /// Starts a battle of the active battle core from the map and brings the
    /// player back to the same map cell afterwards. It is a thin layer over the
    /// platform (<see cref="BattleService.Init"/> + the <c>Battle</c> route):
    /// <list type="bullet">
    /// <item>the return scene is the current map's scene, and the position is
    /// already in <see cref="Cthangover.Mods.Rpg.RpgState"/>, so the world restores it;</item>
    /// <item>the backdrop is the map scene's default background (captured by
    /// the platform from the scene), so a map chooses its battle background in
    /// <c>scenes/&lt;map&gt;.json</c>;</item>
    /// <item>an optional win flag becomes the story flag "1" on victory — how a
    /// map monster disappears after it is beaten (its event's condition).</item>
    /// </list>
    /// The win flag of the running battle is transient: every <see cref="Start"/> overwrites it and
    /// the battle's end consumes it, so a new game needs no reset for it.
    /// </summary>
    public static class RpgBattleService
    {
        private const string BattleRoute = "Battle";
        private const string VictoryResult = "victory";
        private static string pendingWinFlag;

        /// <summary>
        /// Requests the encounter and switches to the battle route on the next
        /// frame (deferred like <c>switch_scene</c>, so a running dialog finishes
        /// its current command first). Returns false when there is no scene to come back to.
        /// </summary>
        public static bool Start(string enemies, string winFlag)
        {
            var sceneManager = FindSceneManager();
            var returnScene = sceneManager?.CurrentSceneName;
            if (string.IsNullOrEmpty(returnScene) || string.IsNullOrWhiteSpace(enemies))
            {
                GameLogger.Log("RPG", $"battle not started: scene='{returnScene}', enemies='{enemies}'", LogLevel.Error);
                return false;
            }

            pendingWinFlag = string.IsNullOrWhiteSpace(winFlag) ? null : winFlag.Trim();
            BattleService.Init(returnScene, enemies.Replace(" ", string.Empty));
            GameLogger.Log("RPG", $"battle '{enemies}' from '{returnScene}' (win flag: {pendingWinFlag ?? "none"})");
            Callable.From(() => sceneManager.SwitchScene(BattleRoute)).CallDeferred();
            return true;
        }

        /// <summary>Applies the pending win flag of the battle that just ended.</summary>
        public static void OnBattleEnded(BattleEndedEvent evt)
        {
            var flag = pendingWinFlag;
            pendingWinFlag = null;
            if (flag != null && evt?.Result == VictoryResult)
                ScenarioCondition.SetFlag(flag, "1");
        }

        private static SceneManager FindSceneManager()
        {
            var tree = Godot.Engine.GetMainLoop() as SceneTree;
            return tree?.Root?.GetNodeOrNull<SceneManager>("SceneManager");
        }
    }
}
