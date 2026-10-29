namespace Cthangover.Mods.Rpg.Tests
{
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Text.Json;
    using System.Threading.Tasks;
    using Cthangover.Core.Mods.Testing;
    using Cthangover.Core.Utils;
    using Cthangover.Mods.TileRender;
    using Godot;
    /// <summary>Opt-in measurements with the same map, materials, camera rectangle and edits for both projections.</summary>
    public static class TileRenderBenchmark
    {
        public static async Task Run(Node owner, string output)
        {
            var results = new List<Dictionary<string, object>>();
            using var textures = new RpgTextures();
            using var builder = new RpgRenderSceneBuilder(textures);
            var tileset = RpgTilesetFactory.Instance.Get("village");
            var viewport = owner.GetViewport().GetViewportRid();
            using var process = Process.GetCurrentProcess();
            RenderingServer.ViewportSetMeasureRenderTime(viewport, true);
            foreach (var size in new[] { 128, 256 })
            {
                var grid = new RpgMapGrid(size, size);
                for (var y = 0; y < size; y++) for (var x = 0; x < size; x++) grid.SetTile(RpgLayer.Back, x, y, 0);
                var trees = 0;
                for (var y = 8; y < size - 3; y += 8)
                for (var x = 8; x < size - 3; x += 8)
                {
                    for (var ty = 0; ty < 3; ty++) for (var tx = 0; tx < 3; tx++)
                        grid.SetTile(RpgLayer.Main, x + tx, y + ty, 64 + ty * 3 + tx);
                    trees++;
                }
                var map = new RpgMap { ID = "benchmark", Width = size, Height = size, Tileset = "village", Zoom = 1 };
                var clock = Stopwatch.StartNew();
                var scene = builder.Build(map, grid, tileset);
                var row = new Dictionary<string, object> { ["cells_per_axis"] = size, ["cold_ms"] = clock.Elapsed.TotalMilliseconds,
                    ["surface_texture_bytes"] = builder.SurfaceTextureBytes, ["cold_chunks"] = builder.LastBuiltSurfaceChunks,
                    ["trees"] = trees, ["lights"] = 16 };
                foreach (var id in new[] { "topdown", "isometric" })
                {
                    var view = TileRendererRegistry.Instance.Resolve(id).Create();
                    owner.AddChild(view.Root);
                    try
                    {
                        clock.Restart();
                        view.Mount(scene);
                        view.Resize(new Vector2(1920, 1024));
                        var frame = new TileFrame { Focus = new Vector2(size, size) / 2, Ambient = Colors.White };
                        for (var i = 0; i < 16; i++) frame.Lights.Add(new TileLight {
                            Position = frame.Focus + new Vector2(i % 4 * 3 - 6, i / 4 * 3 - 6), Color = Colors.Orange, Radius = 4 });
                        view.Present(frame, 0);
                        row[id + "_mount_ms"] = clock.Elapsed.TotalMilliseconds;
                        for (var i = 0; i < 8; i++) await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
                        double presentMs = 0, cpuMs = 0, gpuMs = 0;
                        var wall = Stopwatch.StartNew();
                        for (var i = 0; i < 16; i++)
                        {
                            clock.Restart();
                            view.Present(frame, 1.0 / 60);
                            presentMs += clock.Elapsed.TotalMilliseconds;
                            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
                            cpuMs += RenderingServer.ViewportGetMeasuredRenderTimeCpu(viewport);
                            gpuMs += RenderingServer.ViewportGetMeasuredRenderTimeGpu(viewport);
                        }
                        row[id + "_present_cpu_ms"] = presentMs / 16;
                        row[id + "_frame_wall_ms"] = wall.Elapsed.TotalMilliseconds / 16;
                        row[id + "_draw_calls"] = Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame);
                        row[id + "_total_video_bytes"] = Performance.GetMonitor(Performance.Monitor.RenderVideoMemUsed);
                        row[id + "_render_cpu_ms"] = cpuMs / 16;
                        row[id + "_render_gpu_ms"] = gpuMs / 16;
                        process.Refresh();
                        row[id + "_process_working_set_bytes"] = process.WorkingSet64;
                    }
                    finally { view.Dispose(); }
                }
                scene.Release();
                clock.Restart();
                scene = builder.Build(map, grid, tileset);
                row["warm_ms"] = clock.Elapsed.TotalMilliseconds;
                ModAssert.Equal(0, builder.LastBuiltSurfaceChunks, "unchanged map reuses every surface texture");
                scene.Release();
                grid.SetTile(RpgLayer.Back, 31, size / 2, 4);
                clock.Restart();
                scene = builder.Build(map, grid, tileset);
                row["edit_ms"] = clock.Elapsed.TotalMilliseconds;
                row["edit_chunks"] = builder.LastBuiltSurfaceChunks;
                ModAssert.Equal(4, builder.LastBuiltSurfaceChunks, "a corner edit rebuilds only four halo chunks");
                scene.Release();
                results.Add(row);
                GameLogger.Log("TEST", "TILE_BENCHMARK " + JsonSerializer.Serialize(row));
            }
            RenderingServer.ViewportSetMeasureRenderTime(viewport, false);
            if (!string.IsNullOrEmpty(output))
            {
                Directory.CreateDirectory(output);
                File.WriteAllText(Path.Combine(output, "benchmark.json"), JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
            }
        }
    }
}
