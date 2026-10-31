namespace Cthangover.Mods.Rpg.Render.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json;
    using Cthangover.Core.Mods.Testing;
    using Cthangover.Mods.Rpg.Model;
    using Cthangover.Mods.TileRender;
    using Godot;

    /// <summary>
    /// The v2 render scene of a fixture map with every kind of picture: three surface materials packed
    /// into the runtime strip (plus one of the wrong patch size), fences and a wall joined by their
    /// <c>Connect</c> key, a tree (upright), a room (top-down ground picture + isometric quad), a gate of
    /// two planes, a dynamic barrel and an actor — built, then mounted in both projections.
    /// Pictures are in-memory solid colours, so the test needs no mod files.
    /// </summary>
    public class RpgRenderSceneTests
    {
        private static readonly Color Grass = new(0.2f, 0.8f, 0.2f);
        private static readonly Color Path = new(0.6f, 0.4f, 0.2f);
        private static readonly Color Water = new(0.1f, 0.2f, 0.9f);

        private const string MapJson = @"{""Id"": ""m"", ""Width"": 8, ""Height"": 6, ""TileSize"": 32, ""PatchSize"": 128, ""Zoom"": 1.5,
            ""Legend"": {""g"": ""t/grass"", ""p"": ""t/path"", ""w"": ""t/water"", ""s"": ""t/small"", ""f"": ""t/fence"", ""W"": ""t/wall""},
            ""Ground"": [""ggggpppp"", ""ggggpppp"", ""gwwgpppp"", ""gggg...."", ""ssgggggg"", ""gggggggg""],
            ""Structure"": [""fff....."", ""f......."", ""........"", "".....W.."", ""....fW.."", ""........""]}";

        private static readonly string[] ObjectsJson =
        {
            @"{""Id"": ""t/grass"", ""Form"": ""surface"", ""Surface"": {""Image"": ""ground/grass""}}",
            @"{""Id"": ""t/path"", ""Form"": ""surface"", ""Surface"": {""Image"": ""ground/path"", ""Diffuse"": false}}",
            @"{""Id"": ""t/water"", ""Form"": ""surface"", ""Solid"": true, ""Water"": true, ""Surface"": {""Image"": ""ground/water""}}",
            @"{""Id"": ""t/small"", ""Form"": ""surface"", ""Surface"": {""Image"": ""ground/small""}}",
            @"{""Id"": ""t/fence"", ""Form"": ""segment"", ""Segment"": {""Image"": ""structure/fence""}}",
            @"{""Id"": ""t/wall"", ""Form"": ""segment"", ""Segment"": {""Image"": ""structure/wall"", ""Connect"": ""wall"", ""Height"": 2}}",
            @"{""Id"": ""t/tree"", ""Form"": ""sprite"", ""Footprint"": {""Size"": [2, 2], ""Solid"": ["".."", "".#""]}, ""Anchor"": [1, 2],
                ""Parts"": [{""Image"": ""vegetation/oak"", ""At"": [0, -1], ""SortOffset"": [0, 0.25]}]}",
            @"{""Id"": ""t/room"", ""Form"": ""sprite"", ""Footprint"": {""Size"": [3, 2]}, ""Parts"": [
                {""Id"": ""whole"", ""Image"": ""interior/room"", ""Geometry"": ""ground"", ""Only"": ""topdown""},
                {""Id"": ""floor"", ""Image"": ""interior/room"", ""Crop"": [0, 32, 96, 32], ""Geometry"": ""quad"", ""DrawOrder"": ""BelowCharacters"",
                 ""Points"": [[0, 1, 0], [3, 1, 0], [3, 2, 0], [0, 2, 0]], ""Only"": ""isometric""}]}",
            @"{""Id"": ""t/gate"", ""Form"": ""sprite"", ""Footprint"": {""Size"": [2, 1]}, ""Parts"": [
                {""Id"": ""left"", ""Image"": ""door/gate"", ""Geometry"": ""plane""},
                {""Id"": ""right"", ""Image"": ""door/gate"", ""Geometry"": ""plane"", ""At"": [1, 0]}]}",
            @"{""Id"": ""t/lost"", ""Form"": ""sprite"", ""Parts"": [{""Image"": ""decor/missing""}]}",
            @"{""Id"": ""t/barrel"", ""Form"": ""sprite"", ""Parts"": [{""Image"": ""container/barrel""}],
                ""Interact"": {""Commands"": [""flag self.open 1""]}}",
            @"{""Id"": ""t/npc"", ""Form"": ""actor"", ""Charset"": ""people/npc"", ""Facing"": ""Left""}",
        };

        [ModTest("render/fixture_scene_parts_segments_surfaces")]
        static void FixtureScene(ModTestContext ctx)
        {
            var owned = new List<Texture2D>();
            try
            {
                using var builder = Builder(owned);
                var (map, cells, objects) = Fixture();
                var scene = builder.Build(map, cells, Statics(objects));

                ModAssert.CloseTo(2, scene.Zoom, what: "a float zoom is rounded to whole pixels");
                ModAssert.True(scene.Warnings.Any(w => w.Contains("ground/small") && w.Contains("PatchSize")), "a material of another patch size is a load error");
                ModAssert.True(scene.Warnings.Any(w => w.Contains("decor/missing")), "a missing part picture is a load error");

                var strip = builder.Strip;
                ModAssert.Equal(3, strip.Images.Count, "three materials packed, the wrong-sized one left out");
                ModAssert.Equal("ground/grass", strip.Images[0], "slots in the order met");
                ModAssert.Equal(-1, strip.IndexOf("ground/small"), "no slot for the rejected material");
                ModAssert.True(strip.Texture.GetSize() == new Vector2(384, 128), "strip of three 128 px patches");
                using (var pixels = strip.Texture.GetImage())
                    ModAssert.True(pixels.GetPixel(128 + 5, 7).IsEqualApprox(Path), "the second slot holds the path picture");

                ModAssert.Equal(2, scene.Water.Count, "water cells listed");
                ModAssert.True(scene.Water.Contains(new Vector2(1, 2)) && scene.Water.Contains(new Vector2(2, 2)), "at the water cells");
                var ground = scene.Visuals.Where(v => v.Id == RpgRenderSceneBuilder.GroundVisualId).ToList();
                ModAssert.Equal(1, ground.Count, "one 32-cell chunk covers the 8×6 map");
                ModAssert.True(ground[0].Bounds == new Rect2(0, 0, 8, 6), "chunk bounds");
                using (var chunk = ground[0].Texture.GetImage())
                {
                    ModAssert.True(chunk.GetPixel(16, 16).IsEqualApprox(Grass), "an all-grass neighbourhood copies the grass patch");
                    ModAssert.True(chunk.GetPixel(6 * 32 + 16, 16).IsEqualApprox(Path), "a non-diffuse floor stays crisp");
                    ModAssert.CloseTo(0, chunk.GetPixel(5 * 32 + 16, 3 * 32 + 16).A, what: "an empty cell stays transparent");
                    ModAssert.CloseTo(0, chunk.GetPixel(16, 4 * 32 + 16).A, what: "a rejected material leaves its cells empty");
                }

                var fences = scene.Visuals.Where(v => v.Id == "t/fence").ToList();
                ModAssert.Equal(6, fences.Count, "five fence cells, the corner gets a second quad");
                var corner = fences.Where(v => v.Anchor == new Vector2(0.5f, 1)).ToList();
                ModAssert.Equal(2, corner.Count, "corner (0,0): a cross of an x and a y plane");
                ModAssert.Null(corner[0].Only, "the first quad of a cross is drawn by every projection");
                ModAssert.Equal("isometric", corner[1].Only, "the second only where it has depth");
                Quad(fences.Single(v => v.Anchor == new Vector2(1.5f, 1)), new Vector3(1, 0.5f, 1), "(1,0) runs along x");
                Quad(fences.Single(v => v.Anchor == new Vector2(0.5f, 2)), new Vector3(0.5f, 2, 1), "(0,1) runs along y");
                var lone = fences.Single(v => v.Anchor == new Vector2(4.5f, 5));
                Quad(lone, new Vector3(4, 4.5f, 1), "a fence touching a wall of another Connect key stays a lone x plane");
                ModAssert.True(fences.All(v => v.DrawOrder == TileDrawOrder.ByPosition && v.Upright), "segments are sorted by position");

                var walls = scene.Visuals.Where(v => v.Id == "t/wall").ToList();
                ModAssert.Equal(2, walls.Count, "two wall cells, no cross with the fence beside");
                var lower = walls.Single(v => v.Anchor == new Vector2(5.5f, 5));
                ModAssert.True(lower.Bounds == new Rect2(5, 3, 1, 2), "a two-cell-high segment stands on its cell");
                Quad(lower, new Vector3(5.5f, 5, 2), "the wall runs along y up to z = Height");

                var tree = scene.Visuals.Single(v => v.Id == "m/tree#0");
                ModAssert.True(tree.Bounds == new Rect2(6, -1, 2, 3), "the 64×96 picture spans 2×3 cells from At");
                ModAssert.True(tree.Anchor == new Vector2(7, 2), "object anchor at the origin");
                ModAssert.True(tree.SortAnchor == new Vector2(7, 2.25f), "SortOffset moves only the sort point");
                ModAssert.True(tree.Upright && tree.DrawOrder == TileDrawOrder.ByPosition && tree.WorldQuad == null, "an upright part is a billboard");

                var whole = scene.Visuals.Single(v => v.Id == "m/room#whole");
                ModAssert.True(whole.Bounds == new Rect2(0, 3, 3, 2) && !whole.Upright, "the room picture lies on its footprint");
                ModAssert.True(whole.DrawOrder == TileDrawOrder.BelowCharacters && whole.Only == "topdown", "under characters, top-down only");
                var floor = scene.Visuals.Single(v => v.Id == "m/room#floor");
                ModAssert.True(floor.Source == new Rect2(0, 32, 96, 32), "Crop in pixels");
                Quad(floor, new Vector3(0, 4, 0), "quad points relative to the origin");

                var left = scene.Visuals.Single(v => v.Id == "m/gate#left");
                var right = scene.Visuals.Single(v => v.Id == "m/gate#right");
                ModAssert.True(right.Bounds == new Rect2(3, 2, 1, 1) && right.Anchor == new Vector2(3.5f, 3), "a plane stands on its own cell");
                Quad(left, new Vector3(2, 2.5f, 1), "the left plane along x");
                Quad(right, new Vector3(3, 2.5f, 1), "the right plane along x");

                ModAssert.Equal(14, scene.Visuals.Count, "one visual per chunk, segment quad and part — nothing per cell of an object");
                ModAssert.False(scene.Visuals.Any(v => v.Id?.StartsWith("m/barrel") == true || v.Id?.StartsWith("m/npc") == true),
                    "dynamic placements and actors are not baked");

                var rebuilt = builder.Build(map, cells, Statics(objects));
                ModAssert.Equal(0, builder.LastBuiltSurfaceChunks, "an unchanged ground is not rasterized again");
                ModAssert.True(ReferenceEquals(strip, builder.Strip), "the strip is kept");
                ModAssert.True(rebuilt.Warnings.Any(w => w.Contains("ground/small")), "a rebuilt scene still lists the material error");
                ModAssert.True(rebuilt.Revision > scene.Revision, "every build is a new revision");
            }
            finally
            {
                foreach (var texture in owned)
                    texture.Dispose();
            }
        }

        [ModTest("render/dynamic_looks_and_actor_sheet")]
        static void DynamicLooks(ModTestContext ctx)
        {
            var owned = new List<Texture2D>();
            try
            {
                using var builder = Builder(owned);
                var (_, _, objects) = Fixture();
                var looks = builder.PartLooks(objects["t/barrel"], 32);
                ModAssert.Equal(1, looks.Count, "one look per part");
                ModAssert.True(looks[0].Bounds == new Rect2(0, 0, 1, 1) && looks[0].Anchor == new Vector2(0.5f, 1), "relative to the origin");
                var hidden = JsonSerializer.Deserialize<RpgObject>(@"{""Id"": ""h"", ""Form"": ""sprite"", ""Hidden"": true, ""Parts"": [{""Image"": ""container/barrel""}]}");
                ModAssert.Equal(0, builder.PartLooks(hidden, 32).Count, "a hidden object has no look");

                var npc = builder.ActorLook(objects["t/npc"], 32);
                ModAssert.True(npc.Bounds == new Rect2(0, 0, 1, 1) && npc.Anchor == new Vector2(0.5f, 1), "a 32 px frame stands on its cell");
                ModAssert.True(npc.Source == new Rect2(32, 32, 32, 32), "idle column, the Left row of the sheet");
                var missing = JsonSerializer.Deserialize<RpgObject>(@"{""Id"": ""x"", ""Form"": ""actor"", ""Charset"": ""people/none""}");
                ModAssert.Null(builder.ActorLook(missing, 32), "a missing sheet gives no look");
                ModAssert.Null(builder.ActorLook(objects["t/barrel"], 32), "a sprite is not an actor");
            }
            finally
            {
                foreach (var texture in owned)
                    texture.Dispose();
            }
        }

        [ModTest("render/fixture_mounts_in_both_projections")]
        static void BothProjections(ModTestContext ctx)
        {
            var owned = new List<Texture2D>();
            try
            {
                using var builder = Builder(owned);
                var (map, cells, objects) = Fixture();
                var scene = builder.Build(map, cells, Statics(objects));
                foreach (var (renderer, expected) in new[] { ("topdown", 11), ("isometric", 12) })
                {
                    var factory = TileRendererRegistry.Instance.Resolve(renderer);
                    var projection = factory.Projection(scene.TileSize);
                    using var view = factory.Create();
                    ctx.SandboxRoot.AddChild(view.Root);
                    view.Mount(scene);
                    var sprites = new List<TileCanvasSprite>();
                    Collect(view.Root, sprites);
                    ModAssert.Equal(expected, sprites.Count, renderer + ": one node per visual of this projection");
                    ModAssert.True(sprites.Any(s => s.Position.DistanceTo(projection.Project(new Vector3(7, 2.25f, 0))) < 0.01f),
                        renderer + ": the tree sorts at its anchor plus SortOffset");

                    var frame = new TileFrame { Focus = new Vector2(4, 3) };
                    frame.Actors.Add(new TileActorPose { Id = "npc", Visible = true, Charset = true, Facing = 1, Column = TileWalkSheet.IdleColumn,
                        Position = new Vector2(3, 1), Look = builder.ActorLook(objects["t/npc"], scene.TileSize) });
                    frame.Actors.Add(new TileActorPose { Id = "barrel", Visible = true, Position = new Vector2(4, 1),
                        Look = builder.PartLooks(objects["t/barrel"], scene.TileSize)[0] });
                    view.Present(frame, 0);
                    var actors = view.Root.GetNode("Actors");
                    ModAssert.True(actors.GetChildren().OfType<TileCanvasSprite>().Any(s => s.Position.DistanceTo(projection.Project(new Vector3(3.5f, 2, 0))) < 0.01f),
                        renderer + ": the actor stands on its cell");
                    ModAssert.True(actors.GetChildren().OfType<TileCanvasSprite>().Any(s => s.Position.DistanceTo(projection.Project(new Vector3(4.5f, 2, 0))) < 0.01f),
                        renderer + ": the dynamic barrel stands on its cell");
                }
                scene.Release();
            }
            finally
            {
                foreach (var texture in owned)
                    texture.Dispose();
            }
        }

        private static void Collect(Node node, List<TileCanvasSprite> into)
        {
            foreach (var child in node.GetChildren())
            {
                if (child is TileCanvasSprite sprite)
                    into.Add(sprite);
                Collect(child, into);
            }
        }

        private static void Quad(TileVisual visual, Vector3 topLeft, string what)
        {
            ModAssert.NotNull(visual.WorldQuad, what + ": has a world quad");
            ModAssert.True(visual.WorldQuad[0].IsEqualApprox(topLeft), $"{what}: top-left {visual.WorldQuad[0]}, expected {topLeft}");
        }

        private static IEnumerable<(string, RpgObject, int, int)> Statics(Dictionary<string, RpgObject> objects)
        {
            yield return ("m/tree", objects["t/tree"], 6, 0);
            yield return ("m/room", objects["t/room"], 0, 3);
            yield return ("m/gate", objects["t/gate"], 2, 2);
            yield return ("m/lost", objects["t/lost"], 7, 5);
        }

        private static (Model.RpgMap, RpgMapCells, Dictionary<string, RpgObject>) Fixture()
        {
            var objects = new Dictionary<string, RpgObject>(StringComparer.OrdinalIgnoreCase);
            foreach (var json in ObjectsJson)
            {
                var obj = JsonSerializer.Deserialize<RpgObject>(json);
                objects[obj.ID] = obj;
            }
            var map = JsonSerializer.Deserialize<Model.RpgMap>(MapJson);
            var cells = RpgMapCells.Build(map, id => objects.TryGetValue(id, out var o) ? o : null);
            ModAssert.Equal(0, cells.Errors.Count, "the fixture map itself is valid");
            return (map, cells, objects);
        }

        private static RpgRenderSceneBuilder Builder(List<Texture2D> owned)
        {
            var images = new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase)
            {
                ["ground/grass"] = Solid(owned, 128, 128, Grass),
                ["ground/path"] = Solid(owned, 128, 128, Path),
                ["ground/water"] = Solid(owned, 128, 128, Water),
                ["ground/small"] = Solid(owned, 64, 64, Colors.Red),
                ["structure/fence"] = Solid(owned, 32, 32, Colors.Brown),
                ["structure/wall"] = Solid(owned, 32, 64, Colors.Gray),
                ["vegetation/oak"] = Solid(owned, 64, 96, Colors.DarkGreen),
                ["interior/room"] = Solid(owned, 96, 64, Colors.Wheat),
                ["door/gate"] = Solid(owned, 32, 32, Colors.SaddleBrown),
                ["container/barrel"] = Solid(owned, 32, 32, Colors.Peru),
            };
            var charsets = new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase)
            {
                ["people/npc"] = Solid(owned, 96, 128, Colors.Pink),
            };
            return new RpgRenderSceneBuilder(path => images.TryGetValue(path, out var t) ? t : null,
                path => charsets.TryGetValue(path, out var t) ? t : null);
        }

        private static Texture2D Solid(List<Texture2D> owned, int width, int height, Color color)
        {
            using var image = Image.CreateEmpty(width, height, false, Image.Format.Rgba8);
            image.Fill(color);
            var texture = ImageTexture.CreateFromImage(image);
            owned.Add(texture);
            return texture;
        }
    }
}
