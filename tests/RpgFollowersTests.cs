namespace Cthangover.Mods.Rpg.Tests
{
    using System.Collections.Generic;
    using System.Text.Json;
    using Cthangover.Core.Mods.Testing;
    using Cthangover.Mods.Rpg.Model;
    using Cthangover.Mods.Rpg.Play;
    using Godot;

    /// <summary>The follower trail (plan 14.H) without a scene tree: positions, gathering, blocking and the session wiring.</summary>
    public class RpgFollowersTests
    {
        private const float Speed = 4.5f;

        // Runs the followers until every step has arrived (10 s of game time is far more than a party needs).
        private static void Settle(RpgFollowers followers)
        {
            for (var i = 0; i < 200; i++)
                followers.Tick(0.05, Speed);
        }

        private static Vector2I CellOf(RpgFollowers followers, int index) => followers.Members[index].Actor.Cell;

        [ModTest("followers/walk_the_trail_one_step_behind_each_other")]
        static void WalkTheTrailOneStepBehindEachOther(ModTestContext ctx)
        {
            var followers = new RpgFollowers();
            followers.Resize(2, new Vector2I(2, 2), RpgDirection.Right);
            followers.Arrived(new Vector2I(3, 2));
            followers.Arrived(new Vector2I(4, 2));
            followers.Arrived(new Vector2I(5, 2));
            Settle(followers);
            ModAssert.True(CellOf(followers, 0) == new Vector2I(4, 2), "the first follower is one step behind the leader");
            ModAssert.True(CellOf(followers, 1) == new Vector2I(3, 2), "the second follower is two steps behind");
            ModAssert.True(followers.Members[0].Actor.Facing == RpgDirection.Right, "a follower faces its move direction");
        }

        [ModTest("followers/gather_on_teleport_and_spread_as_the_leader_walks")]
        static void GatherOnTeleportAndSpreadAsTheLeaderWalks(ModTestContext ctx)
        {
            var followers = new RpgFollowers();
            followers.Resize(2, new Vector2I(2, 2), RpgDirection.Down);
            followers.Arrived(new Vector2I(2, 3));
            followers.Arrived(new Vector2I(2, 4));
            Settle(followers);
            followers.Gather(new Vector2I(9, 9), RpgDirection.Left);
            ModAssert.True(CellOf(followers, 0) == new Vector2I(9, 9) && CellOf(followers, 1) == new Vector2I(9, 9), "a teleport gathers everyone on the leader");
            ModAssert.True(followers.TrailLength == 1, "the old trail is forgotten");
            followers.Arrived(new Vector2I(8, 9));
            Settle(followers);
            ModAssert.True(CellOf(followers, 0) == new Vector2I(9, 9), "the first follower waits on the leader's cell for the first step");
            ModAssert.True(CellOf(followers, 1) == new Vector2I(9, 9), "the second follower waits longer");
            followers.Arrived(new Vector2I(7, 9));
            Settle(followers);
            ModAssert.True(CellOf(followers, 0) == new Vector2I(8, 9), "the party spreads out as the leader walks");
            ModAssert.True(CellOf(followers, 1) == new Vector2I(9, 9), "the last follower is still waiting");
        }

        [ModTest("followers/occupied_cells_are_reported_for_npcs")]
        static void OccupiedCellsAreReportedForNpcs(ModTestContext ctx)
        {
            var followers = new RpgFollowers();
            followers.Resize(1, new Vector2I(0, 0), RpgDirection.Right);
            followers.Arrived(new Vector2I(1, 0));
            Settle(followers);
            ModAssert.True(followers.Occupies(new Vector2I(0, 0)), "the follower's cell is occupied");
            ModAssert.False(followers.Occupies(new Vector2I(1, 0)), "the cell ahead of the follower is free");
            ModAssert.False(followers.Occupies(new Vector2I(5, 5)), "other cells are free");
        }

        [ModTest("followers/leaving_cell_stays_occupied_until_the_step_ends")]
        static void LeavingCellStaysOccupiedUntilTheStepEnds(ModTestContext ctx)
        {
            var followers = new RpgFollowers();
            followers.Resize(1, new Vector2I(0, 0), RpgDirection.Right);
            followers.Arrived(new Vector2I(1, 0));
            followers.Arrived(new Vector2I(2, 0));
            followers.Tick(0.05, Speed);
            ModAssert.True(followers.Members[0].Actor.IsMoving, "the follower starts its step at once");
            ModAssert.True(followers.Occupies(new Vector2I(0, 0)), "the cell it leaves is still taken");
            Settle(followers);
            ModAssert.False(followers.Members[0].Actor.IsMoving, "the step finishes");
            ModAssert.True(CellOf(followers, 0) == new Vector2I(1, 0), "the follower stands one cell behind the head");
            ModAssert.False(followers.Occupies(new Vector2I(0, 0)), "the vacated cell is free again");
        }

        [ModTest("followers/trail_is_bounded_and_followers_stay_on_it")]
        static void TrailIsBoundedAndFollowersStayOnIt(ModTestContext ctx)
        {
            var followers = new RpgFollowers();
            followers.Resize(3, Vector2I.Zero, RpgDirection.Right);
            for (var x = 1; x <= 200; x++)
            {
                followers.Arrived(new Vector2I(x, 0));
                for (var t = 0; t < 5; t++)
                    followers.Tick(0.05, Speed);
            }
            Settle(followers);
            ModAssert.True(followers.TrailLength <= RpgFollowers.TrailCapacity, "the trail is capped");
            ModAssert.True(CellOf(followers, 0) == new Vector2I(199, 0), "the first follower is one cell behind the head");
            ModAssert.True(CellOf(followers, 2) == new Vector2I(197, 0), "the third follower is three cells behind the head");
        }

        [ModTest("followers/resize_keeps_positions_for_the_same_count_and_gathers_on_change")]
        static void ResizeKeepsPositionsForTheSameCountAndGathersOnChange(ModTestContext ctx)
        {
            var followers = new RpgFollowers();
            followers.Resize(1, new Vector2I(1, 1), RpgDirection.Down);
            followers.Arrived(new Vector2I(1, 2));
            Settle(followers);
            followers.Resize(1, new Vector2I(5, 5), RpgDirection.Down);
            ModAssert.True(CellOf(followers, 0) == new Vector2I(1, 1), "the same count keeps the follower where it is");
            followers.Resize(2, new Vector2I(5, 5), RpgDirection.Down);
            ModAssert.True(followers.Members.Count == 2, "two followers now");
            ModAssert.True(CellOf(followers, 1) == new Vector2I(5, 5), "a changed party gathers on the leader");
        }

        [ModTest("followers/session_follows_the_player_and_walks_through_it")]
        static void SessionFollowsThePlayerAndWalksThroughIt(ModTestContext ctx)
        {
            var map = new RpgMap { ID = "trail", Width = 8, Height = 3 };
            var session = new RpgWorldSession(map, _ => null, new List<RpgPlacement>(), (condition, scope) => true);
            session.SetFollowerCount(1);
            session.Relocate(new Vector2I(1, 1), RpgDirection.Right);
            ModAssert.True(session.Followers.Members[0].Actor.Cell == new Vector2I(1, 1), "the follower gathers on the arrival cell");

            var guard = 0;
            while ((session.Player.Cell.X < 3 || session.Player.IsMoving) && guard++ < 400)
                session.Tick(0.05, false, RpgDirection.Right);
            ModAssert.True(session.Player.Cell == new Vector2I(3, 1), "the player walked three cells");
            for (var i = 0; i < 100; i++)
                session.Tick(0.05, false, null);
            ModAssert.True(session.Followers.Members[0].Actor.Cell == new Vector2I(2, 1), "the follower walks one cell behind");

            for (var i = 0; i < 60; i++)
                session.Tick(0.05, false, RpgDirection.Left);
            for (var i = 0; i < 40; i++)
                session.Tick(0.05, false, null);
            ModAssert.True(session.Player.Cell == new Vector2I(0, 1), "the hero walks through the follower to the wall");
            ModAssert.True(session.Followers.Members[0].Actor.Cell == new Vector2I(1, 1), "the follower steps aside one cell behind the hero");
        }

        [ModTest("followers/dead_end_corridor_lets_the_hero_walk_back_out")]
        static void DeadEndCorridorLetsTheHeroWalkBackOut(ModTestContext ctx)
        {
            var map = new RpgMap { ID = "corridor", Width = 5, Height = 1 };
            var session = new RpgWorldSession(map, _ => null, new List<RpgPlacement>(), (condition, scope) => true);
            session.SetFollowerCount(2);
            session.Relocate(new Vector2I(0, 0), RpgDirection.Right);
            var guard = 0;
            while ((session.Player.Cell.X < 4 || session.Player.IsMoving) && guard++ < 400)
                session.Tick(0.05, false, RpgDirection.Right);
            for (var i = 0; i < 60; i++)
                session.Tick(0.05, false, null);
            ModAssert.True(session.Player.Cell == new Vector2I(4, 0), "the hero reached the dead end");
            ModAssert.True(session.Followers.Members[0].Actor.Cell == new Vector2I(3, 0), "the first follower is behind the hero");
            ModAssert.True(session.Followers.Members[1].Actor.Cell == new Vector2I(2, 0), "the second follower is behind it");

            guard = 0;
            while ((session.Player.Cell.X > 0 || session.Player.IsMoving) && guard++ < 400)
                session.Tick(0.05, false, RpgDirection.Left);
            for (var i = 0; i < 60; i++)
                session.Tick(0.05, false, null);
            ModAssert.True(session.Player.Cell == new Vector2I(0, 0), "the hero walked back through the party out of the dead end");
            ModAssert.True(session.Followers.Members[0].Actor.Cell == new Vector2I(1, 0), "the first follower follows on the trail, one cell behind");
            ModAssert.True(session.Followers.Members[1].Actor.Cell == new Vector2I(2, 0), "the second follower follows as well");
        }

        [ModTest("followers/direct_placement_gathers_at_the_next_tick")]
        static void DirectPlacementGathersAtTheNextTick(ModTestContext ctx)
        {
            var map = new RpgMap { ID = "trail2", Width = 8, Height = 3 };
            var session = new RpgWorldSession(map, _ => null, new List<RpgPlacement>(), (condition, scope) => true);
            session.SetFollowerCount(2);
            session.Player.Place(new Vector2I(6, 2), RpgDirection.Up);
            session.Tick(0.05, true);
            ModAssert.True(session.Followers.Members[0].Actor.Cell == new Vector2I(6, 2), "a teleport through the actor gathers the party");
            ModAssert.True(session.Followers.Members[1].Actor.Cell == new Vector2I(6, 2), "both followers are on the leader");
        }
    }
}
