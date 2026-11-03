using Cthangover.Core.Scenes;
using Cthangover.Core.UI;
using Cthangover.Mods.Dialog;
using Godot;

namespace Cthangover.Mods.Rpg
{
    /// <summary>
    /// Keyboard of the RPG world. Keys are read by their <b>physical</b> position
    /// (<see cref="Input.IsPhysicalKeyPressed"/>), so WASD and E/F stay where
    /// they are on a Russian or any other layout; the arrows work too.
    /// <para/>
    /// The world must not walk while something else owns the keyboard or the
    /// player's attention: a dialog, a toolbar panel (inventory, journal) or a
    /// window-like overlay opened by a scenario (a trade window, a workbench), a
    /// focused editor window or text field, a paused tree, a scene transition.
    /// <see cref="IsWalkBlocked"/> collects those checks in one place.
    /// </summary>
    public static class RpgInput
    {
        private static readonly System.Collections.Generic.HashSet<Node> observers = new();

        /// <summary>Scopes non-modal observation to actual live tool nodes, never a global ignore-all-panels switch.</summary>
        public static void Observe(Node owner, bool enabled)
        {
            if (enabled) observers.Add(owner); else observers.Remove(owner);
        }

        private static bool IsObserved(Node node)
        {
            foreach (var observer in observers)
                if (GodotObject.IsInstanceValid(observer) && observer.IsInsideTree()
                    && (observer == node || observer.IsAncestorOf(node))) return true;
            return false;
        }

        /// <summary>Modal gameplay still pauses simulation; registered authoring windows and their text fields do not.</summary>
        public static bool IsSimulationBlocked(Node node)
        {
            if (node == null || !node.IsInsideTree() || node.GetTree().Paused || GodotSceneService.IsTransitioning
                || FindDialogBox()?.IsRunning == true) return true;
            var viewport = node.GetViewport();
            var focus = viewport.GuiGetFocusOwner();
            if (focus is LineEdit or TextEdit && !IsObserved(focus)) return true;
            foreach (var window in viewport.GetEmbeddedSubwindows())
                if (window.Visible && window.HasFocus() && !IsObserved(window)) return true;
            return IsPanelOpen();
        }

        /// <summary>The held direction, or null. Vertical keys win over horizontal ones when both are held.</summary>
        public static RpgDirection? HeldDirection()
        {
            if (Input.IsPhysicalKeyPressed(Key.W) || Input.IsPhysicalKeyPressed(Key.Up))
                return RpgDirection.Up;
            if (Input.IsPhysicalKeyPressed(Key.S) || Input.IsPhysicalKeyPressed(Key.Down))
                return RpgDirection.Down;
            if (Input.IsPhysicalKeyPressed(Key.A) || Input.IsPhysicalKeyPressed(Key.Left))
                return RpgDirection.Left;
            if (Input.IsPhysicalKeyPressed(Key.D) || Input.IsPhysicalKeyPressed(Key.Right))
                return RpgDirection.Right;
            return null;
        }

        /// <summary>True for a fresh press of the action key (E or F).</summary>
        public static bool IsActionPress(InputEvent @event)
        {
            return @event is InputEventKey key && key.Pressed && !key.Echo
                && (key.PhysicalKeycode == Key.E || key.PhysicalKeycode == Key.F);
        }

        // The frame memo of IsWalkBlockedThisFrame: the process frame it was taken in, and the node.
        private static ulong memoFrame = ulong.MaxValue;
        private static ulong memoNode;
        private static bool memoBlocked;

        /// <summary>
        /// <see cref="IsWalkBlocked"/> evaluated at most once per process frame: the per-frame path
        /// (the host's <c>_Process</c>, the frame presentation) asks here, so the panel and window
        /// walk runs once per frame however many presentation calls follow. Input events never use
        /// it (they run between frames and may change the answer), they call <see cref="IsWalkBlocked"/>.
        /// </summary>
        public static bool IsWalkBlockedThisFrame(Node node)
        {
            if (node == null)
                return true;
            var frame = Godot.Engine.GetProcessFrames();
            var id = node.GetInstanceId();
            if (frame == memoFrame && id == memoNode)
                return memoBlocked;
            memoBlocked = IsWalkBlocked(node);
            memoFrame = frame;
            memoNode = id;
            return memoBlocked;
        }

        /// <summary>The shell's dialog box, or null outside the shell.</summary>
        public static DialogBox FindDialogBox() => SceneContextNode.FindNode<DialogBox>("DialogBox");

        /// <summary>True when the player must not walk or interact right now.</summary>
        public static bool IsWalkBlocked(Node node)
        {
            if (node == null || !node.IsInsideTree() || node.GetTree().Paused || GodotSceneService.IsTransitioning)
                return true;
            if (FindDialogBox()?.IsRunning == true)
                return true;

            var viewport = node.GetViewport();
            var focus = viewport.GuiGetFocusOwner();
            if (focus is LineEdit || focus is TextEdit)
                return true;
            foreach (var window in viewport.GetEmbeddedSubwindows())
            {
                if (window.Visible && window.HasFocus())
                    return true;
            }
            return IsPanelOpen();
        }

        // Panels and "once" overlays opened on demand are window-like entries on the
        // host layer. Overlays shown from the start (a HUD strip) do not block walking.
        private static bool IsPanelOpen()
        {
            var host = UiHost.Instance;
            if (host == null)
                return false;
            foreach (var entry in UiRegistry.Entries)
            {
                if (!UiRegistry.IsPanel(entry) && !(entry.Once && !entry.ShowOnStart))
                    continue;
                if (host.TryGetInstance(entry.Id, out var panel) && panel.IsVisibleInTree() && !IsObserved(panel))
                    return true;
            }
            return false;
        }
    }
}
