using Cthangover.Mods.Rpg.Play;
using Godot;

namespace Cthangover.Mods.Rpg
{
    /// <summary>
    /// Maps unconsumed viewport clicks to cardinal intents; existing shell focus and gameplay gates remain authoritative.
    /// </summary>
    public partial class RpgWorldHost
    {
        private void OnWorldGuiInput(InputEvent input)
        {
            if (View != null && TryClickWalk(input, true))
            {
                AcceptEvent();
            }
        }

        private bool TryClickWalk(InputEvent input, bool hostLocal = false)
        {
            if (SimulationPaused || View.Presentation == null || RpgInput.IsWalkBlocked(this))
            {
                return false;
            }

            if (input is InputEventKey key && key.Pressed && !key.Echo
                && (key.PhysicalKeycode == Key.Escape || key.Keycode == Key.Escape))
            {
                var active = Session.ClickWalk.Status is RpgClickWalkStatus.Searching or RpgClickWalkStatus.Walking;
                if (active)
                {
                    Session.ClickWalk.Cancel();
                }

                return active;
            }

            if (input is not InputEventMouseButton pointer || pointer.ButtonIndex != MouseButton.Left || !pointer.Pressed)
            {
                return false;
            }

            // TryPickGround already inverts the presentation's camera. Convert only the outer view transform here.
            var viewportPoint = hostLocal ? GetGlobalTransformWithCanvas() * pointer.Position : pointer.Position;
            var point = View.GetGlobalTransformWithCanvas().AffineInverse() * viewportPoint;
            if (!View.Presentation.TryPickGround(point, out var cell))
            {
                return false;
            }

            Session.ClickWalk.Request(cell);
            return true;
        }

        private string MovementPromptKey()
        {
            if (View.Presentation == null)
            {
                return "rpg/ui/no_renderer";
            }

            return Session.ClickWalk.Status switch
            {
                RpgClickWalkStatus.Searching => "rpg/ui/click_searching",
                RpgClickWalkStatus.Walking => "rpg/ui/click_walking",
                RpgClickWalkStatus.BlockedGoal => "rpg/ui/click_blocked",
                RpgClickWalkStatus.Unreachable => "rpg/ui/click_unreachable",
                RpgClickWalkStatus.BudgetExhausted => "rpg/ui/click_budget",
                RpgClickWalkStatus.InvalidGoal => "rpg/ui/click_invalid",
                _ => View.InteractionTarget() != null ? "rpg/ui/interact" : "rpg/ui/walk_click",
            };
        }
    }
}
