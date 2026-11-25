namespace Cthangover.Mods.Rpg
{
    using Cthangover.Core.Items;
    using Godot;

    /// <summary>Maps reward locations onto the live tile presentation, including after returning from battle.</summary>
    public sealed class RpgGroundRewards : IInventoryGroundWorld
    {
        public string Domain => "rpg";

        public bool TryCapture(string scene, out InventoryGroundPosition point)
        {
            point = null;
            var session = RpgWorldHost.Current?.Session;
            Vector2I cell;
            if (session?.Map.ID == scene)
            {
                cell = session.Player.Cell;
            }
            else
            {
                var saved = RpgState.Instance.Data;
                if (saved.Map != scene) return false;
                cell = new Vector2I(saved.X, saved.Y);
            }

            point = new InventoryGroundPosition { Domain = Domain, Location = scene, X = cell.X, Y = cell.Y };
            return true;
        }

        public bool TryProject(InventoryGroundPosition point, out Vector2 screen, out bool inReach)
        {
            screen = default;
            inReach = false;
            var host = RpgWorldHost.Current;
            if (host?.Session?.Map.ID != point.Location || host.View?.Presentation == null) return false;
            var position = host.View.Presentation.ProjectToView(new Vector3(point.X + .5f, point.Y + .5f, 0));
            screen = host.View.GetGlobalTransformWithCanvas() * position;
            inReach = host.Session.Player.Position.DistanceTo(new Vector2(point.X, point.Y)) <= 1.5f;
            return true;
        }
    }
}
