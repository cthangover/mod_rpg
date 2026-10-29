namespace Cthangover.Mods.Rpg.Tests
{
    using Cthangover.Core.Mods.Testing;
    using Godot;

    /// <summary>Cell reservations, timing and poses work without a scene tree or a sprite.</summary>
    public class RpgActorStateTests
    {
        [ModTest("actor_state/reserves_destination_before_arrival")]
        static void ReservesDestinationBeforeArrival(ModTestContext ctx)
        {
            var actor = new RpgActorState { Speed = 2 };
            actor.Place(new Vector2I(3, 4), RpgDirection.Down);
            actor.StepTo(RpgDirection.Right);
            ModAssert.True(actor.Cell == new Vector2I(4, 4), "destination is reserved immediately");
            ModAssert.CloseTo(3, actor.Position.X, what: "drawing starts at the source");
            ModAssert.True(actor.IsMoving, "an unfinished step");
            ModAssert.False(actor.Advance(0.25), "half a second per cell at speed 2");
            ModAssert.CloseTo(3.5, actor.Position.X, what: "position is measured in cells");
            ModAssert.CloseTo(4, actor.Position.Y, what: "unchanged axis");
            ModAssert.True(actor.Advance(0.25), "arrival is reported once");
            ModAssert.False(actor.Advance(1), "idle time never repeats arrival");
            ModAssert.CloseTo(4, actor.Position.X, what: "destination reached exactly");
        }

        [ModTest("actor_state/teleport_cancels_pending_arrival")]
        static void TeleportCancelsPendingArrival(ModTestContext ctx)
        {
            var actor = new RpgActorState();
            actor.StepTo(RpgDirection.Down);
            actor.Advance(0.05);
            actor.Place(new Vector2I(8, 2), RpgDirection.Left);
            ModAssert.False(actor.IsMoving, "teleport stops interpolation");
            ModAssert.False(actor.Advance(1), "old step cannot complete after teleport");
            ModAssert.CloseTo(8, actor.Position.X, what: "new x");
            ModAssert.CloseTo(2, actor.Position.Y, what: "new y");
            ModAssert.True(actor.Facing == RpgDirection.Left, "new facing");
        }

        [ModTest("actor_state/moving_command_does_not_reserve_second_cell")]
        static void MovingCommandDoesNotReserveSecondCell(ModTestContext ctx)
        {
            var actor = new RpgActorState { Speed = 2 };
            actor.StepTo(RpgDirection.Right);
            actor.Advance(0.25);
            actor.StepTo(RpgDirection.Up);
            ModAssert.True(actor.Cell == new Vector2I(1, 0), "commands do not stack during a step");
            ModAssert.CloseTo(0.5, actor.Position.X, what: "interpolation is not restarted");
            actor.Face(RpgDirection.Left);
            ModAssert.True(actor.Facing == RpgDirection.Left, "turning is independent of displacement");
            ModAssert.True(actor.Advance(0.25), "original step finishes on time");
        }

        [ModTest("actor_state/speed_clamp_and_large_frame")]
        static void SpeedClampAndLargeFrame(ModTestContext ctx)
        {
            var actor = new RpgActorState { Speed = 0 };
            actor.StepTo(RpgDirection.Up);
            ModAssert.False(actor.Advance(0.5), "speed clamps to one cell per second");
            ModAssert.CloseTo(-0.5, actor.Position.Y, what: "halfway up");
            ModAssert.True(actor.Advance(20), "a large frame completes one step");
            ModAssert.CloseTo(-1, actor.Position.Y, what: "no overshoot");
            actor.Speed = 100;
            actor.StepTo(RpgDirection.Right);
            ModAssert.False(actor.Advance(1.0 / 24), "speed clamps to twelve");
            ModAssert.CloseTo(0.5, actor.Position.X, what: "upper speed limit");
        }

        [ModTest("actor_state/frame_partition_keeps_pose_and_animation")]
        static void FramePartitionKeepsPoseAndAnimation(ModTestContext ctx)
        {
            var a = new RpgActorState { Speed = 2 };
            var b = new RpgActorState { Speed = 2 };
            a.StepTo(RpgDirection.Down);
            b.StepTo(RpgDirection.Down);
            a.Advance(0.2);
            b.Advance(0.1);
            b.Advance(0.1);
            ModAssert.CloseTo(a.Position.Y, b.Position.Y, what: "same movement without renderer timing");
            ModAssert.CloseTo(a.AnimationTime, b.AnimationTime, what: "same animation phase");
            ModAssert.False(a.Advance(0), "zero time cannot complete an unfinished step");
        }
    }
}
