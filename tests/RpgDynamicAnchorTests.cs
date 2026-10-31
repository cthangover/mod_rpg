namespace Cthangover.Mods.Rpg.Render.Tests
{
    using Cthangover.Core.Mods.Testing;
    using Cthangover.Mods.Rpg.Model;
    using Cthangover.Mods.Rpg.Play;
    using Cthangover.Mods.TileRender;
    using Godot;

    /// <summary>
    /// A dynamic sprite (a barrel with an interaction) is drawn as poses on the cell of its placement; its
    /// part visuals must carry the same anchor a static copy of the same object gets, or it draws off its cell
    /// (the supply barrel of rpg_forest, isometric pass).
    /// </summary>
    public class RpgDynamicAnchorTests
    {
        [ModTest("render/dynamic_sprite_anchor_matches_its_cell")]
        static void DynamicSpriteAnchorMatchesItsCell(ModTestContext ctx)
        {
            var map = RpgWorldHost.MapOf("rpg_forest");
            ModAssert.NotNull(map, "rpg_forest is a map");
            var session = RpgWorldHost.CreateSession(map);
            var instance = session.Find("rpg_forest/supply_barrel");
            ModAssert.NotNull(instance, "the supply barrel is a dynamic instance");
            var current = instance.Current;
            var cell = new Vector2(instance.Placement.X, instance.Placement.Y);
            ModAssert.True(instance.Actor.Cell == new Vector2I(instance.Placement.X, instance.Placement.Y),
                $"the actor stands on its placement, actor {instance.Actor.Cell}, placement {instance.Placement.X},{instance.Placement.Y}");
            ModAssert.True(instance.Actor.Position == cell, $"the pose position is the cell, got {instance.Actor.Position}");

            var texture = ImageTexture.CreateFromImage(Image.CreateEmpty(4, 4, false, Image.Format.Rgba8));
            var builder = new RpgRenderSceneBuilder(_ => texture, _ => null);
            var looks = builder.PartLooks(current, 32);
            ModAssert.True(looks.Count > 0, $"the barrel has part looks, object {current.ID}, width {current.Width}, height {current.Height}, anchor {current.Anchor?.Length ?? 0}");
            var expected = current.Anchor is { Length: >= 2 }
                ? new Vector2(current.Anchor[0], current.Anchor[1])
                : new Vector2(current.Width / 2f, current.Height);
            foreach (var look in looks)
            {
                ModAssert.True(look.Anchor == expected,
                    $"part {look.Id} anchor {look.Anchor}, expected {expected} (width {current.Width}, height {current.Height})");
                ModAssert.True(look.Bounds.Size.X > 0 && look.Bounds.Size.Y > 0, $"part {look.Id} has a size {look.Bounds.Size}");
            }
        }
    }
}
