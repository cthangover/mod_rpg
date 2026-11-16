using System.Collections.Generic;
using System.Text.Json.Nodes;

namespace Cthangover.Mods.Rpg.Validation
{
    /// <summary>Checks authored dimensions and explicit rows before a map DTO or its mutating save cleanup is used.</summary>
    public static class RpgMapSyntax
    {
        /// <summary>Missing optional layers are valid; explicit rows must match their declared map dimensions.</summary>
        public static IReadOnlyList<RpgDataIssue> Check(JsonObject data)
        {
            var issues = new List<RpgDataIssue>();
            var fields = new[]
            {
                "Width",
                "Height",
                "TileSize",
                "PatchSize",
            };
            foreach (var field in fields)
            {
                if ((data?[field] != null || field is "Width" or "Height") && !RpgDataValue.PositiveInteger(data?[field]))
                {
                    issues.Add(new RpgDataIssue
                    {
                        Code = "map_size",
                        Field = field,
                        Detail = data?[field]?.ToString(),
                    });
                }
            }

            if (!RpgDataValue.PositiveInteger(data?["Width"]) || !RpgDataValue.PositiveInteger(data?["Height"]))
            {
                return issues;
            }

            RpgDataValue.Number(data["Width"], out var width);
            RpgDataValue.Number(data["Height"], out var height);
            var layers = new[]
            {
                "Ground",
                "Structure",
                "Collision",
            };
            foreach (var layer in layers)
            {
                if (data[layer] == null || data[layer] is JsonArray { Count: 0 })
                {
                    continue;
                }

                if (data[layer] is not JsonArray rows || rows.Count != height)
                {
                    issues.Add(new RpgDataIssue
                    {
                        Code = "map_rows",
                        Field = layer,
                    });
                    continue;
                }

                for (var i = 0; i < rows.Count; i++)
                {
                    if (RpgDataValue.Text(rows[i])?.Length != width)
                    {
                        issues.Add(new RpgDataIssue
                        {
                            Code = "map_rows",
                            Field = layer + "[" + i + "]",
                        });
                    }
                }
            }

            return issues;
        }
    }
}
