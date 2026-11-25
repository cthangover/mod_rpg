namespace Cthangover.Mods.Rpg.Tests
{
    using Cthangover.Core.Mods.Testing;

    public sealed class RpgGroundRewardTests
    {
        [ModTest("rpg/ground_reward_preserves_battle_return_cell")]
        public static void BattleReturnCell(ModTestContext context)
        {
            var state = RpgState.Instance;
            var previous = state.Data;
            try
            {
                state.Place("reward_return_map", 8, 13, default);
                var world = new RpgGroundRewards();
                ModAssert.False(world.TryCapture("other_map", out _));
                ModAssert.True(world.TryCapture("reward_return_map", out var point));
                ModAssert.Equal("rpg", point.Domain);
                ModAssert.Equal("reward_return_map", point.Location);
                ModAssert.Equal(8f, point.X);
                ModAssert.Equal(13f, point.Y);
                ModAssert.False(world.TryProject(point, out _, out _), "a parked map cannot offer pickup");
            }
            finally
            {
                state.Data = previous;
            }
        }
    }
}
