namespace Cthangover.Mods.Rpg
{
    using System.Collections.Generic;
    using Godot;

    /// <summary>Saved object definitions reach the mounted world without scene scripts rerunning. Actor cells, facing and route progress survive.</summary>
    public partial class RpgWorldHost
    {
        private bool savedObjectsPending;
        /// <summary>Schedules an object-definition refresh without replaying map entry scripts or depending on authoring mods.</summary>
        public void RequestObjectRefresh() => savedObjectsPending = true;
        private void RefreshSavedObjects()
        {
            if (!savedObjectsPending || Session == null || Session.Player.IsMoving || RpgInput.FindDialogBox()?.IsRunning == true) return;
            foreach (var instance in Session.Instances) if (instance.Actor.IsMoving) return;
            savedObjectsPending = false;
            var map = Session.Map.ID;
            var player = Session.Player;
            var actors = new Dictionary<string, (Vector2I Cell, RpgDirection Facing, int Route, double Timer)>();
            foreach (var instance in Session.Instances)
                actors[instance.Id] = (instance.Actor.Cell, instance.Actor.Facing, instance.Actor.RouteIndex, instance.Actor.RouteTimer);
            RpgState.Instance.Place(map, player.Cell.X, player.Cell.Y, player.Facing);
            Mount(map);
            foreach (var pair in actors)
            {
                var instance = Session.Find(pair.Key);
                if (instance == null || !Session.Cells.InBounds(pair.Value.Cell.X, pair.Value.Cell.Y)) continue;
                instance.Actor.Place(pair.Value.Cell, pair.Value.Facing);
                instance.Actor.RouteIndex = pair.Value.Route; instance.Actor.RouteTimer = pair.Value.Timer;
                Session.Walk.SetDynamic(instance.Id, instance.Current, instance.Actor.Cell.X, instance.Actor.Cell.Y);
            }
        }
    }
}
