namespace Cthangover.Mods.Rpg.Model
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Text.Json.Nodes;
    using Cthangover.Core.Mods.Patches;
    using Cthangover.Core.Utils;

    /// <summary>
    /// Computes what one placement of an <see cref="RpgObject"/> actually is (contract section 7):
    /// <list type="number">
    /// <item>the object after id-override, <c>Extends</c> and its own patches — that is already the
    /// entity resolver's output (the core resolves <c>Extends</c> for every kind), so it is the
    /// input here and is never re-merged;</item>
    /// <item><c>Variants[placement.Variant ?? map.Variant]</c>, deep-merged;</item>
    /// <item>the placement's overrides, deep-merged;</item>
    /// <item>at run time, the active state: the last state whose <c>When</c> holds replaces the
    /// components it names wholesale (<c>null</c> removes one).</item>
    /// </list>
    /// Deep merge is the patch engine (<see cref="JsonPatchMerger"/> with <see cref="RpgObject"/> as the
    /// type, so <c>Parts$add</c> merges parts by id) — the same rules as patches and <c>Extends</c>.
    /// <para>
    /// Pure functions of their inputs, no Godot: the inputs are treated as immutable and every
    /// result is a new object (or the input itself when nothing applies). A layer that cannot apply
    /// (a bad operation, a wrong value type) is logged as ERROR and skipped, the others still apply —
    /// one broken placement override never takes the object off the map.
    /// </para>
    /// </summary>
    public static class RpgObjectResolve
    {
        /// <summary>The components a state may replace (contract 5.4), in their JSON spelling.</summary>
        public static readonly IReadOnlyList<string> StateComponents = new[]
        {
            "Parts", "Charset", "Facing", "Column", "Footprint", "Solid", "Interact", "Light", "Lights", "Route", "Hidden",
        };

        private static readonly string[] FixedFields = { "Id", "Form", "Extends" };

        /// <summary>
        /// Steps 2–3: the object with the variant (<paramref name="placement"/>'s own <c>Variant</c>, else
        /// <paramref name="mapVariant"/>) and the placement's overrides merged in. The result has no
        /// <c>Variants</c> (they are spent) and keeps the object's <c>Id</c>; the instance identity is the
        /// placement. Brush objects (surfaces, segments) have no placement: pass null.
        /// </summary>
        public static RpgObject Effective(RpgObject resolved, string mapVariant, RpgPlacement placement = null)
        {
            if (resolved == null)
                return null;
            var overrides = OverridesOf(placement, resolved.ID);
            if (resolved.Variants == null && overrides == null)
                return resolved;

            var variant = string.IsNullOrEmpty(placement?.Variant) ? mapVariant : placement.Variant;
            var node = ToNode(resolved);
            var variantPatch = VariantOf(resolved, variant);
            if (variantPatch != null)
                node = Step(node, variantPatch, $"object '{resolved.ID}': variant '{variant}'");
            if (overrides != null)
                node = Step(node, overrides, $"placement '{placement.ID}' of '{resolved.ID}'");
            RemoveProperty(node, "Variants");
            return node.Deserialize<RpgObject>(JsonOptionsProvider.ReadOptions);
        }

        /// <summary>
        /// Index of the state that applies: the last one whose <c>When</c> holds by
        /// <paramref name="holds"/> (the caller evaluates in the placement's <c>self.</c> scope);
        /// -1 when none does. A state without a condition never applies.
        /// </summary>
        public static int ActiveState(RpgObject effective, Func<string, bool> holds)
        {
            var states = effective?.States;
            if (states == null || holds == null)
                return -1;
            for (var i = states.Count - 1; i >= 0; i--)
            {
                var when = states[i]?.When;
                if (!string.IsNullOrWhiteSpace(when) && holds(when))
                    return i;
            }
            return -1;
        }

        /// <summary>
        /// <see cref="ActiveState(RpgObject, Func{string, bool})"/> with conditions checked in the
        /// <c>self.</c> scope <paramref name="scope"/> (the placement id): <paramref name="holds"/> gets the
        /// condition and the scope, as <c>ScenarioCondition.Evaluate(condition, scope)</c> takes them.
        /// </summary>
        public static int ActiveState(RpgObject effective, Func<string, string, bool> holds, string scope)
        {
            var states = effective?.States;
            if (states == null || holds == null)
                return -1;
            for (var i = states.Count - 1; i >= 0; i--)
            {
                var when = states[i]?.When;
                if (!string.IsNullOrWhiteSpace(when) && holds(when, scope))
                    return i;
            }
            return -1;
        }

        /// <summary>
        /// Step 4: <paramref name="effective"/> with state <paramref name="stateIndex"/> applied — each
        /// component the state names replaces the current one wholesale, a <c>null</c> one is removed
        /// (back to its default). The result keeps <c>States</c>, so it can be re-evaluated. An index
        /// outside the list returns <paramref name="effective"/> itself. A name that is not a state
        /// component is an ERROR and ignored.
        /// </summary>
        public static RpgObject WithState(RpgObject effective, int stateIndex)
        {
            var states = effective?.States;
            if (states == null || stateIndex < 0 || stateIndex >= states.Count || states[stateIndex]?.Components == null)
                return effective;

            var state = states[stateIndex];
            var node = ToNode(effective);
            foreach (var pair in state.Components)
            {
                var component = Named(StateComponents, pair.Key);
                if (component == null)
                {
                    GameLogger.Log("RPG", $"object '{effective.ID}': state '{state.Id ?? stateIndex.ToString()}': '{pair.Key}' is not a state component ({string.Join(", ", StateComponents)}); ignored", LogLevel.Error);
                    continue;
                }
                if (pair.Value.ValueKind == JsonValueKind.Null)
                    RemoveProperty(node, component);
                else
                    node[component] = JsonNode.Parse(pair.Value.GetRawText());
            }

            try
            {
                return node.Deserialize<RpgObject>(JsonOptionsProvider.ReadOptions);
            }
            catch (JsonException ex)
            {
                GameLogger.Log("RPG", $"object '{effective.ID}': state '{state.Id ?? stateIndex.ToString()}': {ex.Path ?? "$"}: {ex.Message}; state not applied", LogLevel.Error);
                return effective;
            }
        }

        private static JsonObject Step(JsonObject node, JsonObject patch, string what)
        {
            var attempt = (JsonObject)node.DeepClone();
            try
            {
                JsonPatchMerger.Apply(attempt, patch, typeof(RpgObject));
                attempt.Deserialize<RpgObject>(JsonOptionsProvider.ReadOptions);
                return attempt;
            }
            catch (JsonPatchException ex)
            {
                GameLogger.Log("RPG", $"{what}: {ex.Path}: {ex.Message}; not applied", LogLevel.Error);
            }
            catch (JsonException ex)
            {
                GameLogger.Log("RPG", $"{what}: {ex.Path ?? "$"}: {ex.Message}; not applied", LogLevel.Error);
            }
            return node;
        }

        private static JsonObject VariantOf(RpgObject resolved, string variant)
        {
            if (string.IsNullOrEmpty(variant) || resolved.Variants == null)
                return null;
            foreach (var pair in resolved.Variants)
            {
                if (string.Equals(pair.Key, variant, StringComparison.OrdinalIgnoreCase))
                    return pair.Value.ValueKind == JsonValueKind.Object ? JsonNode.Parse(pair.Value.GetRawText()) as JsonObject : null;
            }
            return null;
        }

        private static JsonObject OverridesOf(RpgPlacement placement, string objectId)
        {
            if (placement?.Overrides == null || placement.Overrides.Count == 0)
                return null;
            var result = new JsonObject();
            foreach (var pair in placement.Overrides)
            {
                if (Named(FixedFields, pair.Key) != null)
                {
                    GameLogger.Log("RPG", $"placement '{placement.ID}' of '{objectId}': '{pair.Key}' cannot be overridden by a placement; ignored", LogLevel.Error);
                    continue;
                }
                result[pair.Key] = JsonNode.Parse(pair.Value.GetRawText());
            }
            return result.Count > 0 ? result : null;
        }

        private static string Named(IReadOnlyList<string> names, string name)
        {
            foreach (var candidate in names)
            {
                if (string.Equals(candidate, name, StringComparison.OrdinalIgnoreCase))
                    return candidate;
            }
            return null;
        }

        private static JsonObject ToNode(RpgObject value)
            => (JsonObject)JsonSerializer.SerializeToNode(value, JsonOptionsProvider.ReadOptions);

        private static void RemoveProperty(JsonObject node, string name)
        {
            string existing = null;
            foreach (var pair in node)
            {
                if (string.Equals(pair.Key, name, StringComparison.OrdinalIgnoreCase))
                {
                    existing = pair.Key;
                    break;
                }
            }
            if (existing != null)
                node.Remove(existing);
        }
    }
}
