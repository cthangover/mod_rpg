namespace Cthangover.Mods.Rpg.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Text.Json;
    using System.Threading.Tasks;
    using Cthangover.Core.Settings;
    using Cthangover.Core.Utils;
    using Cthangover.Mods.TileRender;
    using Godot;

    /// <summary>
    /// Records or compares whole-map pixel snapshots of every RPG map in every tile
    /// renderer (claude_plan.md 6.3). Two passes per map and renderer: the game look
    /// (events, player, lights at noon) and a probe pass of magenta actors on a 3-cell
    /// lattice, which records what static art covers a character and what it does not.
    /// </summary>
    public partial class RpgSnapshotsDriver : Node
    {
        private const int Lattice = 3;
        private const int Tolerance = 2;
        private const long Noon = 12 * 60;
        private string mode = "compare";
        private string dir;
        private HashSet<string> only;
        private Task run;
        private double elapsed;
        private int passed;
        private int failed;

        public override void _Ready()
        {
            foreach (var arg in OS.GetCmdlineUserArgs())
            {
                if (arg.StartsWith("--snapshot-mode=")) mode = arg.Substring(16);
                else if (arg.StartsWith("--snapshot-dir=")) dir = arg.Substring(15);
                else if (arg.StartsWith("--snapshot-maps=")) only = arg.Substring(16).Split(',', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
            }
            dir ??= ProjectSettings.GlobalizePath("user://snapshots/rpg");
        }

        public override void _Process(double delta)
        {
            elapsed += delta;
            if (elapsed > 900) { Finish("timeout"); return; }
            if (run == null)
            {
                if (elapsed < 1) return;
                if (DisplayServer.GetName() == "headless") { Finish("windowed run required: headless draws nothing"); return; }
                if (mode != "record" && mode != "compare") { Finish("--snapshot-mode must be record or compare"); return; }
                run = Run();
                return;
            }
            if (!run.IsCompleted) return;
            try { run.GetAwaiter().GetResult(); Finish(null); }
            catch (Exception ex) { Finish(ex.ToString()); }
        }

        private async Task Run()
        {
            Directory.CreateDirectory(dir);
            var index = new List<Dictionary<string, object>>();
            var allowed = ReadAllowed();
            var all = RpgSnapshotSource.MapIds();
            var maps = all.Where(id => only == null || only.Contains(id)).ToList();
            GameLogger.Log("TEST", $"rpg_snapshots: {mode} {maps.Count} map(s) into {dir}; known: {string.Join(",", all)}");
            // A run that compares nothing must not pass: a renamed map or an empty catalogue would read as green.
            var missing = only?.Where(id => !all.Contains(id)).ToList() ?? new List<string>();
            if (missing.Count > 0) throw new InvalidOperationException("--snapshot-maps names unknown map(s): " + string.Join(",", missing));
            if (maps.Count == 0) throw new InvalidOperationException("no maps to snapshot");
            foreach (var mapId in maps)
            foreach (var renderer in TileRendererRegistry.Instance.Ids)
            {
                var shots = await Capture(mapId, renderer);
                foreach (var (pass, image) in shots)
                {
                    var name = $"{mapId}__{renderer}__{pass}";
                    using (image)
                    {
                        if (mode == "record")
                        {
                            image.SavePng(Path.Combine(dir, name + ".png"));
                            index.Add(new Dictionary<string, object> { ["name"] = name, ["width"] = image.GetWidth(),
                                ["height"] = image.GetHeight(), ["sha256"] = Convert.ToHexString(SHA256.HashData(image.GetData())) });
                            passed++;
                        }
                        else Compare(name, image, allowed.TryGetValue(name, out var limit) ? limit : 0);
                    }
                }
            }
            if (mode == "record")
                File.WriteAllText(Path.Combine(dir, "index.json"), JsonSerializer.Serialize(index, new JsonSerializerOptions { WriteIndented = true }).Replace("\r\n", "\n") + "\n");
        }

        private async Task<List<(string Pass, Image Image)>> Capture(string mapId, string renderer)
        {
            var result = new List<(string, Image)>();
            var viewport = new SubViewport { Size = new Vector2I(64, 64), World2D = new World2D(), TransparentBg = false,
                RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
            AddChild(viewport);
            var view = new RpgWorldView();
            viewport.AddChild(view);
            using var probeImage = Image.CreateEmpty(32, 32, false, Image.Format.Rgba8);
            probeImage.Fill(new Color(1, 0, 1));
            probeImage.FillRect(new Rect2I(0, 28, 32, 4), new Color(0.3f, 0, 0.3f));
            using var probe = ImageTexture.CreateFromImage(probeImage);
            try
            {
                var session = RpgSnapshotSource.Session(mapId);
                SetNoon();
                view.Build(session);
                var spawn = session.Map.Spawn;
                view.PlacePlayer(spawn?.X ?? 0, spawn?.Y ?? 0, spawn?.Dir ?? RpgDirection.Down);
                if (!view.SetRenderer(renderer)) throw new InvalidOperationException($"{mapId}: renderer '{renderer}' failed");
                var presentation = view.Presentation;
                var size = presentation.ContentBounds.Size * presentation.Root.Scale;
                viewport.Size = new Vector2I(Mathf.CeilToInt(size.X) + 2, Mathf.CeilToInt(size.Y) + 2);
                SetNoon();
                view.Present(0);
                result.Add(("game", await Grab(viewport)));

                var frame = new TileFrame { Focus = new Vector2(session.Map.Width, session.Map.Height) / 2, Ambient = Colors.White };
                for (var y = 1; y < session.Map.Height; y += Lattice)
                for (var x = 1; x < session.Map.Width; x += Lattice)
                    frame.Actors.Add(new TileActorPose { Id = $"probe/{x}/{y}", Visible = true, Position = new Vector2(x, y),
                        Look = new TileVisual { Texture = probe, Source = new Rect2(0, 0, 32, 32), Bounds = new Rect2(0, 0, 1, 1), Anchor = new Vector2(0.5f, 1) } });
                presentation.Present(frame, 0);
                result.Add(("probe", await Grab(viewport)));
                GameLogger.Log("TEST", $"rpg_snapshots: {mapId} {renderer} {viewport.Size.X}x{viewport.Size.Y}");
            }
            catch
            {
                foreach (var (_, image) in result) image.Dispose();
                throw;
            }
            finally
            {
                viewport.RemoveChild(view);
                view.Free();
                RemoveChild(viewport);
                viewport.Free();
            }
            return result;
        }

        private async Task<Image> Grab(SubViewport viewport)
        {
            for (var i = 0; i < 3; i++)
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            var image = viewport.GetTexture().GetImage();
            image.Convert(Image.Format.Rgba8);
            return image;
        }

        private static void SetNoon()
        {
            if (GameData.Instance?.Runtime != null) GameData.Instance.Runtime.Time = new TimeData(Noon);
        }

        private void Compare(string name, Image actual, int limit)
        {
            var path = Path.Combine(dir, name + ".png");
            if (!File.Exists(path)) { Fail(name, "no baseline"); return; }
            using var expected = Image.LoadFromFile(path);
            expected.Convert(Image.Format.Rgba8);
            if (expected.GetSize() != actual.GetSize())
            {
                Fail(name, $"size {actual.GetWidth()}x{actual.GetHeight()} != baseline {expected.GetWidth()}x{expected.GetHeight()}");
                Directory.CreateDirectory(Path.Combine(dir, "compare"));
                actual.SavePng(Path.Combine(dir, "compare", name + ".actual.png"));
                return;
            }
            var a = expected.GetData();
            var b = actual.GetData();
            var mask = new byte[a.Length];
            var different = 0;
            for (var i = 0; i < a.Length; i += 4)
            {
                var diff = Math.Max(Math.Max(Math.Abs(a[i] - b[i]), Math.Abs(a[i + 1] - b[i + 1])), Math.Max(Math.Abs(a[i + 2] - b[i + 2]), Math.Abs(a[i + 3] - b[i + 3])));
                var grey = (byte)((a[i] + a[i + 1] + a[i + 2]) / 12);
                mask[i] = diff > Tolerance ? (byte)255 : grey; mask[i + 1] = grey; mask[i + 2] = grey; mask[i + 3] = 255;
                if (diff > Tolerance) different++;
            }
            if (different <= limit)
            {
                passed++;
                GameLogger.Log("TEST", $"rpg_snapshots: {name} ok ({different} px differ, allowed {limit})");
                return;
            }
            Fail(name, $"{different} px differ (allowed {limit})");
            Directory.CreateDirectory(Path.Combine(dir, "compare"));
            using var diffImage = Image.CreateFromData(actual.GetWidth(), actual.GetHeight(), false, Image.Format.Rgba8, mask);
            diffImage.SavePng(Path.Combine(dir, "compare", name + ".diff.png"));
            actual.SavePng(Path.Combine(dir, "compare", name + ".actual.png"));
        }

        private Dictionary<string, int> ReadAllowed()
        {
            var path = Path.Combine(dir, "allowed.json");
            return File.Exists(path) ? JsonSerializer.Deserialize<Dictionary<string, int>>(File.ReadAllText(path)) : new();
        }

        private void Fail(string name, string message)
        {
            failed++;
            GameLogger.Log("TEST", $"rpg_snapshots: {name} FAIL: {message}", LogLevel.Error);
        }

        private void Finish(string error)
        {
            if (error != null) { failed++; GameLogger.Log("TEST", "rpg_snapshots: " + error, LogLevel.Error); }
            GameLogger.Log("TEST", $"[TEST_RESULT] name=rpg_snapshots passed={passed} failed={failed}");
            SetProcess(false);
            Cthangover.Core.Bootstrap.ApplicationExit.Quit(GetTree(), failed == 0 ? 0 : 1);
        }
    }
}
