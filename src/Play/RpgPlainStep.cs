namespace Cthangover.Mods.Rpg.Play
{
    using System.Collections.Generic;
    using Cthangover.Core.Mods.Consumers;

    /// <summary>
    /// A mechanic that runs after a plain step: the player arrived on a cell with no <c>Step</c> trigger
    /// (see <see cref="RpgWorldSession.PlainStepFinished"/>). The random encounters of the
    /// <c>rpg__battles</c> integration are the first listener; the world itself knows no battles.
    /// </summary>
    public interface IRpgPlainStepListener
    {
        /// <summary>Called on the main thread with the session the step happened in.</summary>
        void OnPlainStep(RpgWorldSession session);
    }

    /// <summary>The listeners of every loaded mod, in the order the kernel adds them.</summary>
    public static class RpgPlainStepListeners
    {
        private static readonly List<IRpgPlainStepListener> listeners = new();

        /// <summary>Adds a listener contributed by a loaded mod.</summary>
        public static void Add(IRpgPlainStepListener listener)
        {
            if (listener != null && !listeners.Contains(listener))
                listeners.Add(listener);
        }

        /// <summary>Removes a listener when its mod unloads.</summary>
        public static void Remove(IRpgPlainStepListener listener) => listeners.Remove(listener);

        /// <summary>Runs every listener on <paramref name="session"/>; a copy of the list, so a listener may unload its mod.</summary>
        public static void Notify(RpgWorldSession session)
        {
            foreach (var listener in listeners.ToArray())
                listener.OnPlainStep(session);
        }
    }

    /// <summary>Feeds <see cref="RpgPlainStepListeners"/> with the <see cref="IRpgPlainStepListener"/> of every mod.</summary>
    public class RpgPlainStepConsumer : ModTypeConsumer<IRpgPlainStepListener>
    {
        /// <inheritdoc />
        public override string Id => "rpg.plain_step_listeners";

        /// <inheritdoc />
        protected override void Add(IRpgPlainStepListener instance, string modId) => RpgPlainStepListeners.Add(instance);

        /// <inheritdoc />
        protected override void Remove(IRpgPlainStepListener instance, string modId) => RpgPlainStepListeners.Remove(instance);
    }
}
