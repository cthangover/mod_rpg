namespace Cthangover.Mods.Rpg.Tests
{
    using System;
    using Cthangover.Core.Mods.Testing;
    using Cthangover.Mods.TileRender;
    using Godot;

    /// <summary>Exercises version checks, sparse redraws and removal through the actual presentation API.</summary>
    public static class TileRenderDeltaContract
    {
        public static int Run(Node owner)
        {
            var passed = 0;
            foreach (var id in new[] { "topdown", "isometric" })
            {
                var view = TileRendererRegistry.Instance.Resolve(id).Create();
                owner.AddChild(view.Root);
                try
                {
                    var first = Snapshot(1, Colors.White);
                    view.Mount(first);
                    var unchanged = Snapshot(2, Colors.White);
                    view.Apply(new TileSceneChanges { BaseRevision = 1, Scene = unchanged });
                    ModAssert.Equal(0, ((TileCanvasPresentation)view).LastChangedDrawChunks, "unchanged draw chunks retained " + id);
                    var changed = Snapshot(3, Colors.Red);
                    view.Apply(new TileSceneChanges { BaseRevision = 2, Scene = changed });
                    ModAssert.Equal(1, ((TileCanvasPresentation)view).LastChangedDrawChunks, "one edited draw chunk " + id);
                    var rejected = false;
                    try { view.Apply(new TileSceneChanges { BaseRevision = 1, Scene = Snapshot(4, Colors.White) }); }
                    catch (InvalidOperationException) { rejected = true; }
                    ModAssert.True(rejected, "stale revision rejected " + id);
                    var frame = new TileFrame();
                    frame.Actors.Add(new TileActorPose { Id = "removed", Visible = true });
                    view.Present(frame, 0);
                    ModAssert.Equal(1, view.Root.GetNode<Node2D>("Actors").GetChildCount(), "actor mounted " + id);
                    frame.Actors.Clear();
                    view.Present(frame, 0);
                    ModAssert.Equal(0, view.Root.GetNode<Node2D>("Actors").GetChildCount(), "removed actor released " + id);
                    passed += 5;
                }
                finally { view.Dispose(); }
            }
            return passed;
        }

        private static TileRenderScene Snapshot(long revision, Color firstTint)
        {
            var scene = new TileRenderScene { Size = new Vector2I(64, 64), Revision = revision, Zoom = 1 };
            scene.Visuals.Add(new TileVisual { Bounds = new Rect2(0, 0, 1, 1), Tint = firstTint });
            scene.Visuals.Add(new TileVisual { Bounds = new Rect2(33, 0, 1, 1) });
            scene.Visuals.Add(new TileVisual { Bounds = new Rect2(0, 33, 1, 1) });
            return scene;
        }
    }
}
