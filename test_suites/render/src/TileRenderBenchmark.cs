namespace Cthangover.Mods.Rpg.Tests
{
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Text.Json;
    using System.Threading.Tasks;
    using Cthangover.Core.Mods.Testing;
    using Cthangover.Core.Utils;
    using Cthangover.Mods.Rpg.Model;
    using Cthangover.Mods.Rpg.Play;
    using Cthangover.Mods.Rpg.Render;
    using Cthangover.Mods.TileRender;
    using Godot;
    /// <summary>
    /// Opt-in measurements (<c>--tile-benchmark</c>) with the same map, materials, camera rectangle and edits for
    /// both projections: a v2 map of sand with a palm (a 3×4 static sprite) every 8 cells.
    /// </summary>
    public static class TileRenderBenchmark
    {
        public static async Task Run(Node owner, string output)
        {
            var results = new List<Dictionary<string, object>>();
            using var textures = new RpgTextures();
            using var builder = new RpgRenderSceneBuilder(textures.Image, textures.Charset);
            var objects = new RpgObjectLookup();
            var palm = objects.Find("village_pack/vegetation/palm");
            var viewport = owner.GetViewport().GetViewportRid();
            using var process = Process.GetCurrentProcess();
            RenderingServer.ViewportSetMeasureRenderTime(viewport, true);
            foreach (var size in new[] { 128, 256 })
            {
                var map = new RpgMap { ID = "benchmark", Width = size, Height = size, Zoom = 1,
                    Legend = new() { ["s"] = "village_pack/ground/desert_sand", ["r"] = "village_pack/ground/rocky_ground" } };
                for (var y = 0; y < size; y++) map.Ground.Add(new string('s', size));
                // Both materials are on the map from the start, so the edit below changes cells, not the strip.
                map.Ground[size - 1] = new string('s', size - 1) + "r";
                var statics = new List<(string Id, RpgObject Object, int X, int Y)>();
                for (var y = 8; y < size - 4; y += 8)
                for (var x = 8; x < size - 3; x += 8)
                    statics.Add(("benchmark/palm_" + statics.Count, palm, x, y));
                var trees = statics.Count;
                var clock = Stopwatch.StartNew();
                var scene = builder.Build(map, RpgMapCells.Build(map, objects.Find), statics);
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
                scene = builder.Build(map, RpgMapCells.Build(map, objects.Find), statics);
                row["warm_ms"] = clock.Elapsed.TotalMilliseconds;
                ModAssert.Equal(0, builder.LastBuiltSurfaceChunks, "unchanged map reuses every surface texture");
                scene.Release();
                var row31 = map.Ground[size / 2].ToCharArray();
                row31[31] = 'r';
                map.Ground[size / 2] = new string(row31);
                clock.Restart();
                scene = builder.Build(map, RpgMapCells.Build(map, objects.Find), statics);
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
