using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using Godot;

namespace Cthangover.Mods.Rpg.Validation
{
    /// <summary>Pure checks over an effective JSON object; image sizes are borrowed metadata, never GPU textures or owned images.</summary>
    public sealed class RpgDataCheck
    {
        private readonly List<RpgDataIssue> issues = new();

        private readonly Func<string, string, Vector2I?> sizes;

        private readonly string projection;

        private bool hidden;

        private bool partial;

        private RpgDataCheck(string projection, Func<string, string, Vector2I?> sizes)
        {
            this.projection = projection;
            this.sizes = sizes;
        }

        /// <summary>
        /// Checks a complete resolved object in one projection. Partial variants/states must first use the ordinary resolver.
        /// No setters, patches, files, logging or Godot servers are invoked; unknown fields remain untouched.
        /// </summary>
        public static IReadOnlyList<RpgDataIssue> Object(JsonObject data, string projection, int patchSize,
            Func<string, string, Vector2I?> imageSizes)
        {
            ArgumentNullException.ThrowIfNull(data);
            var check = new RpgDataCheck(projection, imageSizes);
            check.CheckObject(data, patchSize);
            return check.issues;
        }

        /// <summary>
        /// Checks authored fields before DTO normalization, including partial variants, states and keyed part additions.
        /// Missing inherited fields are not errors; complete effective inputs still use Object for reference and crop bounds.
        /// </summary>
        public static IReadOnlyList<RpgDataIssue> Authored(JsonObject data, string projection, int patchSize,
            Func<string, string, Vector2I?> imageSizes)
        {
            var check = new RpgDataCheck(projection, imageSizes)
            {
                partial = true,
            };
            check.CheckAuthored(data, patchSize, "", 0);
            return check.issues;
        }

        private void CheckAuthored(JsonObject data, int patchSize, string prefix, int depth)
        {
            if (data == null)
            {
                return;
            }

            if (depth > 32)
            {
                Add("budget", prefix, null);
                return;
            }

            var start = issues.Count;
            CheckObject(data, patchSize);
            for (var i = start; i < issues.Count; i++)
            {
                issues[i].Field = prefix + issues[i].Field;
            }

            if (data["Variants"] is JsonObject variants)
            {
                foreach (var variant in variants)
                {
                    CheckAuthored(variant.Value as JsonObject, patchSize, prefix + "Variants[" + variant.Key + "].", depth + 1);
                }
            }

            if (data["States"] is JsonArray states)
            {
                for (var i = 0; i < states.Count; i++)
                {
                    CheckAuthored(states[i] as JsonObject, patchSize, prefix + "States[" + i + "].", depth + 1);
                }
            }
        }

        private void CheckObject(JsonObject data, int patchSize)
        {
            hidden = data["Hidden"]?.ToJsonString() == "true";
            var form = RpgDataValue.Text(data["Form"]);
            if ((!partial || data["Form"] != null) && !Named(form, "surface", "segment", "sprite", "actor"))
            {
                Add("enum", "Form", data["Form"]?.ToString());
            }

            Vector(data, "Anchor", "Anchor");
            if (data["Scale"] != null && (!RpgDataValue.Number(data["Scale"], out var scale) || scale < 0.01 || scale > 100))
            {
                Add("scale", "Scale", data["Scale"].ToString());
            }

            Order(data["DrawOrder"], "DrawOrder");
            if (form?.Equals("surface", StringComparison.OrdinalIgnoreCase) == true)
            {
                var size = Image(data["Surface"]?["Image"], "Surface.Image", "rpg_images");
                if (size != null && (size.Value.X != patchSize || size.Value.Y != patchSize))
                {
                    Add("material", "Surface.Image", size.Value.X + "×" + size.Value.Y + "; " + patchSize + "×" + patchSize);
                }
            }

            if (form?.Equals("segment", StringComparison.OrdinalIgnoreCase) == true)
            {
                var segment = data["Segment"] as JsonObject;
                Image(segment?["Image"], "Segment.Image", "rpg_images");
                if (segment?["Height"] != null
                    && (!RpgDataValue.Number(segment["Height"], out var height) || height <= 0))
                {
                    Add("size", "Segment.Height", segment["Height"].ToString());
                }

                Enum(segment?["Along"], "Segment.Along", "auto", "x", "y");
            }

            if (!hidden && form?.Equals("actor", StringComparison.OrdinalIgnoreCase) == true)
            {
                var size = Image(data["Charset"], "Charset", "charsets");
                if (size != null && (size.Value.Y % 4 != 0 || size.Value.X <= 0))
                {
                    Add("sheet", "Charset", size.Value.X + "×" + size.Value.Y);
                }
            }

            var partsField = partial && data["Parts"] == null && data["Parts$add"] != null ? "Parts$add" : "Parts";
            if (data[partsField] is JsonArray parts)
            {
                var ids = new HashSet<string>(StringComparer.Ordinal);
                for (var i = 0; i < parts.Count; i++)
                {
                    var path = partsField + "[" + i + "]";
                    if (parts[i] is not JsonObject part)
                    {
                        Add("part", path, null);
                        continue;
                    }

                    var id = RpgDataValue.Text(part["Id"]);
                    if (!string.IsNullOrEmpty(id) && !ids.Add(id))
                    {
                        Add("duplicate_part", path + ".Id", id);
                    }

                    CheckPart(part, path);
                }
            }
            else if (data[partsField] != null)
            {
                Add("part", partsField, null);
            }
        }

        private void CheckPart(JsonObject part, string path)
        {
            Vector(part, "At", path + ".At");
            Vector(part, "SortOffset", path + ".SortOffset");
            if (part["Size"] != null && !RpgDataValue.Vector(part["Size"], 2, true))
            {
                Add("size", path + ".Size", part["Size"].ToString());
            }

            Order(part["DrawOrder"], path + ".DrawOrder");
            Enum(part["Geometry"], path + ".Geometry", "upright", "ground", "plane", "quad");
            Enum(part["Along"], path + ".Along", "auto", "x", "y");
            var only = RpgDataValue.Text(part["Only"]);
            if (part["Only"] != null && !Named(only, "topdown", "isometric"))
            {
                Add("projection", path + ".Only", only);
            }

            if (string.Equals(RpgDataValue.Text(part["Geometry"]), "quad", StringComparison.OrdinalIgnoreCase))
            {
                CheckQuad(part["Points"], path + ".Points");
            }

            if (only != null && !only.Equals(projection, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var size = Image(part["Image"], path + ".Image", "rpg_images");
            if (part["Crop"] != null)
            {
                CheckCrop(part["Crop"], size, path + ".Crop");
            }
        }

        private void CheckQuad(JsonNode node, string path)
        {
            if (node is not JsonArray array || array.Count != 4)
            {
                Add("quad_count", path, null);
                return;
            }

            var points = new List<double[]>();
            foreach (var item in array)
            {
                if (!RpgDataValue.Vector(item, 3))
                {
                    Add("quad_number", path, null);
                    return;
                }

                var values = new double[3];
                for (var i = 0; i < 3; i++)
                {
                    RpgDataValue.Number(item[i], out values[i]);
                }

                points.Add(values);
            }

            var problem = RpgQuadCheck.Problem(points);
            if (problem != null)
            {
                Add(problem, path, null);
            }
        }

        private void CheckCrop(JsonNode node, Vector2I? size, string path)
        {
            if (!RpgDataValue.Vector(node, 4))
            {
                Add("crop", path, null);
                return;
            }

            var values = new double[4];
            for (var i = 0; i < 4; i++)
            {
                RpgDataValue.Number(node[i], out values[i]);
                if (values[i] != Math.Truncate(values[i]))
                {
                    Add("crop", path, null);
                    return;
                }
            }

            if (values[0] < 0 || values[1] < 0 || values[2] <= 0 || values[3] <= 0
                || size != null && (values[0] + values[2] > size.Value.X || values[1] + values[3] > size.Value.Y))
            {
                Add("crop", path, node.ToJsonString());
            }
        }

        private Vector2I? Image(JsonNode node, string path, string group)
        {
            var id = RpgDataValue.Text(node);
            if (hidden || partial && node == null)
            {
                return null;
            }

            var size = string.IsNullOrWhiteSpace(id) ? null : sizes?.Invoke(group, id);
            if (size == null || size.Value.X <= 0 || size.Value.Y <= 0)
            {
                Add("image", path, id);
                return null;
            }

            return size;
        }

        private void Vector(JsonObject node, string field, string path)
        {
            if (node[field] != null && !RpgDataValue.Vector(node[field], 2))
            {
                Add("vector", path, node[field].ToString());
            }
        }

        private void Order(JsonNode node, string path)
        {
            if (RpgDataValue.Number(node, out var index) && index == Math.Truncate(index) && index >= 0 && index < 3)
            {
                return;
            }

            Enum(node, path, "BelowCharacters", "ByPosition", "AboveCharacters");
        }

        private void Enum(JsonNode node, string path, params string[] values)
        {
            if (node == null)
            {
                return;
            }

            if (!Named(RpgDataValue.Text(node), values))
            {
                Add("enum", path, node.ToString());
            }
        }

        private static bool Named(string value, params string[] names)
        {
            foreach (var name in names)
            {
                if (string.Equals(value, name, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private void Add(string code, string path, string detail)
        {
            issues.Add(new RpgDataIssue
            {
                Code = code,
                Field = path,
                Detail = detail,
                Projection = projection,
            });
        }
    }
}
