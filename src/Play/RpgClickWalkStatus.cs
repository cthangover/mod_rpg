namespace Cthangover.Mods.Rpg.Play
{
    /// <summary>Transient navigation outcomes; failures never authorize a direct step to the requested goal.</summary>
    public enum RpgClickWalkStatus
    {
        /// <summary>No target has been requested in this session.</summary>
        Idle,

        /// <summary>Bounded search is unfinished; the player waits without taking an unverified step.</summary>
        Searching,

        /// <summary>A verified cardinal path is available; ordinary session ticks own its steps.</summary>
        Walking,

        /// <summary>The final reserved step has finished at the requested cell.</summary>
        Arrived,

        /// <summary>Future intent was cancelled by keyboard, input ownership, relocation or an explicit call.</summary>
        Cancelled,

        /// <summary>The requested coordinate lies outside the current map.</summary>
        InvalidGoal,

        /// <summary>The effective grid rejects the goal, including a newly occupying actor.</summary>
        BlockedGoal,

        /// <summary>The current occupancy snapshot has no cardinal connection to the goal.</summary>
        Unreachable,

        /// <summary>The attempt used its total frontier-removal budget; no path is supplied.</summary>
        BudgetExhausted,
    }
}
