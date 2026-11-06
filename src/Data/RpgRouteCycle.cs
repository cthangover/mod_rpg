namespace Cthangover.Mods.Rpg
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>Per-actor cycle choice stays fixed during a walk and changes only at its end. Legacy routes keep Steps.</summary>
    public sealed class RpgRouteCycle
    {
        private RpgMoveRoute source;
        private readonly List<int> remaining = new();
        public int Variant { get; private set; } = -1;

        /// <summary>Current instructions; changing the effective route starts a new choice.</summary>
        public IReadOnlyList<string> Steps(RpgMoveRoute route, Random random)
        {
            if (!ReferenceEquals(source, route))
            {
                source = route;
                remaining.Clear();
                Variant = -1;
                Next(random);
            }
            return Variant >= 0 ? route.Variants[Variant] : route?.Steps;
        }

        /// <summary>Consumes every nonempty variant before reuse, avoiding the last choice across round boundaries.</summary>
        public void Next(Random random)
        {
            if (source?.Variants == null || !source.Variants.Any(v => v?.Count > 0)) { Variant = -1; return; }
            if (remaining.Count == 0)
            {
                for (var i = 0; i < source.Variants.Count; i++) if (source.Variants[i]?.Count > 0) remaining.Add(i);
                for (var i = remaining.Count-1; i > 0; i--)
                {
                    var next = random.Next(i+1);
                    (remaining[i], remaining[next]) = (remaining[next], remaining[i]);
                }
                if (remaining.Count > 1 && remaining[0] == Variant)
                    (remaining[0], remaining[1]) = (remaining[1], remaining[0]);
            }
            Variant = remaining[0];
            remaining.RemoveAt(0);
        }
    }
}
