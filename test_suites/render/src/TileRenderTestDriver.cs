namespace Cthangover.Mods.Rpg.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Cthangover.Core.Mods;
    using Cthangover.Core.Games;
    using Cthangover.Core.Mods.HotReload;
    using Cthangover.Core.Scenes;
    using Cthangover.Core.Mods.Testing;
    using Cthangover.Core.Utils;
    using Cthangover.Mods.Interface.View;
    using Cthangover.Mods.Rpg.Render;
    using Cthangover.Mods.TileRender;
    using Godot;
    /// <summary>Compares both presentations of identical prepared resources and tests projection/picking invariants.</summary>
    public partial class TileRenderTestDriver : Node
    {
        private readonly string[] maps = { "rpg_village", "rpg_elder_house", "rpg_forest" };
        private readonly List<ITileWorldPresentation> views = new();
        private readonly RpgTextures textures = new();
        private readonly TileFrame frame = new();
        private CanvasLayer overlay;
        private RpgRenderSceneBuilder builder;
        private TileRenderScene scene;
        private int ticks;
        private int step;
        private int passed;
        private double elapsed;
        private string shots;
        private Task contracts;

        public override void _Ready()
        {
            foreach (var arg in OS.GetCmdlineUserArgs()) if (arg.StartsWith("--shots=")) shots = arg.Substring(8);
            overlay = new CanvasLayer { Layer = 90 };
            AddChild(overlay);
            overlay.AddChild(new ColorRect { Size = new Vector2(1920, 1024), Color = new Color(0.04f, 0.05f, 0.06f) });
        }

        public override void _Process(double delta)
        {
            elapsed += delta;
            if (elapsed > 300) { Finish("timeout"); return; }
            if (++ticks < 30) return;
            try
            {
                if (contracts != null)
                {
                    if (contracts.IsCompleted) { contracts.GetAwaiter().GetResult(); Finish(null); }
                    return;
                }
                if (views.Count == 0)
                    Build(maps[step]);
                foreach (var view in views) view.Present(frame, 0);
                if (ticks < 45) return;
                if (shots != null && DisplayServer.GetName() != "headless")
                {
                    DirAccess.MakeDirRecursiveAbsolute(shots);
                    ViewportCapture.SavePng(GetViewport(), shots.TrimEnd('/') + "/compare_" + maps[step] + ".png");
                }
                Release();
                ticks = 0;
                if (++step == maps.Length) contracts = RunContracts();
            }
            catch (Exception ex) { Finish(ex.ToString()); }
        }

        private async Task RunContracts()
        {
            passed += TileRenderDeltaContract.Run(this);
            GetTree().Root.GetNode<SceneManager>("SceneManager").SwitchScene("rpg_village");
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var host = RpgWorldHost.Current;
            ModAssert.NotNull(host, "shell world host");
            host.Mount("rpg_village");
            host.PlacePlayer(15, 14, RpgDirection.Up);
            var session = host.Session;
            session.Tick(0.05, false, RpgDirection.Up);
            host.View.Present(0);
            var pose = session.Player.Position;
            var cell = session.Player.Cell;
            ModAssert.True(session.Player.IsMoving, "switch test starts during a step");
            ModAssert.True(host.View.SetRenderer("isometric"), "iso view can replace square view");
            ModAssert.True(session.Player.Position.IsEqualApprox(pose) && session.Player.Cell == cell, "switch preserves step progress and reservation");
            ModAssert.True(host.View.SetRenderer("topdown"), "switch back");
            ModAssert.True(ReferenceEquals(session, host.Session) && session.Player.Position.IsEqualApprox(pose), "one session survives both projections");
            ModAssert.True(host.View.SetRenderer("isometric"), "reload test has a live iso presentation");
            var oldFactory = new WeakReference(TileRendererRegistry.Instance.Resolve("isometric"));
            var oldProjection = ((TileCanvasPresentation)host.View.Presentation).ProjectionLifetime();
            passed += 6;
            session.Tick(1, true);
            var savedCell = session.Player.Cell;
            var savedFacing = session.Player.Facing;
            var verified = await ModHotReloadService.ReloadNowAsync(new[] { "tile_isometric_base" });
            ModAssert.True(verified?.Applied == true, "forced implementation reload applies through the normal session-parking flow");
            GameLogger.Log("TEST", $"renderer lifetime: old factory={oldFactory.IsAlive}, old projection={oldProjection.IsAlive}");
            ModAssert.Equal(0, verified.Leaked.Count, "renderer load context is collectible after disposal");
            // LoadAndEnter schedules the shell; the public reload task ends
            // before the deferred scene entry mounts its world.
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            ModAssert.NotNull(RpgWorldHost.Current?.View?.Presentation, "kept host restores its presentation after reload");
            ModAssert.True(savedCell == RpgWorldHost.Current.Session.Player.Cell, "reload restores saved logical cell");
            ModAssert.True(savedFacing == RpgWorldHost.Current.Session.Player.Facing, "reload restores saved world facing");
            passed += 5;
            foreach (var arg in OS.GetCmdlineUserArgs())
                if (arg == "--tile-benchmark") await TileRenderBenchmark.Run(this, shots);
        }

        private void Build(string mapId)
        {
            var map = RpgWorldHost.MapOf(mapId);
            var session = RpgWorldHost.CreateSession(map);
            builder ??= new RpgRenderSceneBuilder(textures.Image, textures.Charset);
            scene = builder.Build(map, session.Cells, session.Statics);
            scene.Zoom = mapId == "rpg_elder_house" ? 1.1f : 0.55f;
            frame.Focus = new Vector2(map.Width, map.Height) / 2;
            frame.Ambient = Colors.White;
            var index = 0;
            foreach (var id in new[] { "topdown", "isometric" })
            {
                var factory = TileRendererRegistry.Instance.Resolve(id);
                var projection = factory.Projection(scene.TileSize);
                var point = new Vector2(3.25f, 2.75f);
                ModAssert.True(projection.Unproject(TileDrawing.Project(projection, point)).IsEqualApprox(point), "round trip " + id);
                var view = factory.Create();
                var host = new Control { Position = new Vector2(index++ * 960, 0), Size = new Vector2(960, 1024), ClipContents = true };
                overlay.AddChild(host);
                host.AddChild(view.Root);
                view.Mount(scene);
                view.Resize(new Vector2(960, 1024));
                view.Present(frame, 0);
                var pixel = view.ProjectToView(new Vector3(3.5f, 2.5f, 0));
                ModAssert.True(view.TryPickGround(pixel, out var cell) && cell == new Vector2I(3, 2), "picking " + id);
                ModAssert.False(view.TryPickGround(view.ProjectToView(new Vector3(-1, -1, 0)), out _), "outside " + id);
                views.Add(view);
                passed += 3;
            }
            GameLogger.Log("TEST", $"renderer preview {mapId}: {scene.Visuals.Count} visual parts, {scene.OwnedTextures.Count} generated textures");
        }

        private void Release()
        {
            foreach (var view in views)
            {
                var parent = view.Root.GetParent();
                view.Dispose();
                parent.Free();
            }
            views.Clear();
            scene?.Release();
            scene = null;
        }

        private void Finish(string error)
        {
            Release();
            if (error != null) GameLogger.Log("TEST", error, LogLevel.Error);
            GameLogger.Log("TEST", $"[TEST_RESULT] name=tile_render passed={passed} failed={(error == null ? 0 : 1)}");
            builder?.Dispose();
            textures.Dispose();
            Cthangover.Core.Bootstrap.ApplicationExit.Quit(GetTree(), error == null ? 0 : 1);
            SetProcess(false);
        }
    }
}
