namespace Cthangover.Mods.Rpg.Tests
{
    using Cthangover.Core.Mods.Testing;
    using Cthangover.Mods.TileRender;
    using Godot;

    /// <summary>Replacing real renderer instances cannot change the logical actor clock.</summary>
    public class RpgActorPresentationTests
    {
        private static Node2D Actor(ITileWorldPresentation view) => (Node2D)view.Root.GetNode("Actors").GetChild(0);

        [ModTest("actor_presentation/recreation_keeps_inflight_state")]
        static void RecreationKeepsInflightState(ModTestContext ctx)
        {
            var state = new RpgActorState { Speed = 2 };
            state.Place(new Vector2I(1, 1), RpgDirection.Right);
            state.StepTo(RpgDirection.Right);
            state.Advance(0.25);
            using var image = Image.CreateEmpty(1, 1, false, Image.Format.Rgba8);
            image.Fill(Colors.White);
            using var texture = ImageTexture.CreateFromImage(image);
            var frame = new TileFrame();
            var pose = new TileActorPose { Id = "player", Visible = true, Position = state.Position,
                Look = new TileVisual { Texture = texture, Bounds = new Rect2(0, 0, 1, 1), Source = new Rect2(0, 0, 1, 1) } };
            frame.Actors.Add(pose);
            var first = TileRendererRegistry.Instance.Resolve("topdown").Create();
            try
            {
                first.Mount(new TileRenderScene { Size = new Vector2I(8, 8), TileSize = 32, Zoom = 1 });
                first.Present(frame, 0);
                first.Present(frame, 0);
                ModAssert.CloseTo(48, Actor(first).Position.X, what: "first projection");
                ModAssert.CloseTo(1.5, state.Position.X, what: "presenting twice does not tick");
            }
            finally { first.Dispose(); }

            var second = TileRendererRegistry.Instance.Resolve("topdown").Create();
            try
            {
                second.Mount(new TileRenderScene { Size = new Vector2I(8, 8), TileSize = 64, Zoom = 1 });
                second.Present(frame, 0);
                ModAssert.CloseTo(96, Actor(second).Position.X, what: "another projection uses the same pose");
                ModAssert.True(state.IsMoving, "freeing the old presentation does not cancel the step");
                ModAssert.True(state.Advance(0.25), "remaining duration is preserved");
                pose.Position = state.Position;
                second.Present(frame, 0);
                ModAssert.CloseTo(128, Actor(second).Position.X, what: "arrival updates the replacement");
            }
            finally { second.Dispose(); }
        }
    }
}
