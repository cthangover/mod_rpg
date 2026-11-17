namespace Cthangover.Mods.Rpg.Validation
{
    using System;
    using System.Collections.Generic;

    /// <summary>Canonical identities, explanations and audited save policy; emitters and tools share immutable rows.</summary>
    public static class RpgDataRules
    {
        /// <summary>Rejects missing or extra corners before numeric and affine checks.</summary>
        public const string QuadCount = "quad_count";

        /// <summary>Identifies malformed or nonfinite 3D corner coordinates.</summary>
        public const string QuadNumber = "quad_number";

        /// <summary>Distinguishes a degenerate 3D surface from a valid rotated or vertical one.</summary>
        public const string QuadArea = "quad_area";

        /// <summary>Identifies corners unsupported by the affine drawing contract.</summary>
        public const string QuadAffine = "quad_affine";

        /// <summary>Names invalid two-component origins and offsets without rejecting negative values.</summary>
        public const string Vector = "vector";

        /// <summary>Identifies nonpositive or nonfinite authored dimensions.</summary>
        public const string Size = "size";

        /// <summary>Reports authored scale before a DTO setter can clamp it.</summary>
        public const string Scale = "scale";

        /// <summary>Names an invalid pixel rectangle independently of world-cell coordinates.</summary>
        public const string Crop = "crop";

        /// <summary>Checks square materials and the using map size when that context exists.</summary>
        public const string Material = "material";

        /// <summary>Identifies named image references that cannot be decoded.</summary>
        public const string Image = "image";

        /// <summary>Names walking-sheet dimension and layout-metadata problems.</summary>
        public const string Sheet = "sheet";

        /// <summary>Identifies a malformed part entry while allowing intentionally empty parts.</summary>
        public const string Part = "part";

        /// <summary>Protects stable nonempty identities used by keyed part merging.</summary>
        public const string DuplicatePart = "duplicate_part";

        /// <summary>Reports a value outside a known geometry or metadata enum.</summary>
        public const string Enum = "enum";

        /// <summary>Preserves advisory status for an additional renderer provider.</summary>
        public const string Projection = "projection";

        /// <summary>Identifies invalid authored map or pixel-grid dimensions.</summary>
        public const string MapSize = "map_size";

        /// <summary>Names an explicit layer whose rows do not match the declared rectangle.</summary>
        public const string MapRows = "map_rows";

        /// <summary>Identifies a missing used resource without rejecting unused legend entries.</summary>
        public const string Reference = "reference";

        /// <summary>Distinguishes resource presence from suitability for a painted or placed layer.</summary>
        public const string LayerForm = "layer_form";

        /// <summary>Marks a structural read failure and an incomplete data audit.</summary>
        public const string Json = "json";

        /// <summary>Keeps traversal limits advisory rather than imposing a map-format ban.</summary>
        public const string Budget = "budget";

        /// <summary>Provides the localized fallback for an unregistered diagnostic identity.</summary>
        public const string Unknown = "unknown";

        private static readonly IReadOnlyList<RpgDataRule> rules = Array.AsReadOnly(new[]
        {
            new RpgDataRule(QuadCount, "rpg/data_check/quad_count", true),
            new RpgDataRule(QuadNumber, "rpg/data_check/quad_number", true),
            new RpgDataRule(QuadArea, "rpg/data_check/quad_area", true),
            new RpgDataRule(QuadAffine, "rpg/data_check/quad_affine", true),
            new RpgDataRule(Vector, "rpg/data_check/vector", true),
            new RpgDataRule(Size, "rpg/data_check/size", true),
            new RpgDataRule(Scale, "rpg/data_check/scale", true),
            new RpgDataRule(Crop, "rpg/data_check/crop", true),
            new RpgDataRule(Material, "rpg/data_check/material", true),
            new RpgDataRule(Image, "rpg/data_check/image", true),
            new RpgDataRule(Sheet, "rpg/data_check/sheet", true),
            new RpgDataRule(Part, "rpg/data_check/part", true),
            new RpgDataRule(DuplicatePart, "rpg/data_check/duplicate_part", true),
            new RpgDataRule(Enum, "rpg/data_check/enum", true),
            new RpgDataRule(Projection, "rpg/data_check/projection", false),
            new RpgDataRule(MapSize, "rpg/data_check/map_size", true),
            new RpgDataRule(MapRows, "rpg/data_check/map_rows", true),
            new RpgDataRule(Reference, "rpg/data_check/reference", true),
            new RpgDataRule(LayerForm, "rpg/data_check/layer_form", true),
            new RpgDataRule(Json, "rpg/data_check/json", true),
            new RpgDataRule(Budget, "rpg/data_check/budget", false),
            new RpgDataRule(Unknown, "rpg/data_check/unknown", false),
        });

        private static readonly Dictionary<string, RpgDataRule> index = BuildIndex();

        /// <summary>Read-only catalogue used for reporting, translation coverage and the frozen content-audit contract.</summary>
        public static IReadOnlyList<RpgDataRule> All
        {
            get
            {
                return rules;
            }
        }

        /// <summary>Returns the canonical row or null for unknown input; lookup preserves case-sensitive diagnostic identities.</summary>
        public static RpgDataRule Find(string code)
        {
            return code != null && index.TryGetValue(code, out var rule) ? rule : null;
        }

        private static Dictionary<string, RpgDataRule> BuildIndex()
        {
            var result = new Dictionary<string, RpgDataRule>(StringComparer.Ordinal);
            foreach (var rule in rules)
            {
                result.Add(rule.Code, rule);
            }
            return result;
        }
    }
}
