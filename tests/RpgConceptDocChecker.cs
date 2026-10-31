namespace Cthangover.Mods.Rpg.Model.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Nodes;
    using System.Text.Json.Serialization;
    using Cthangover.Core.Mods;
    using Cthangover.Core.Mods.Patches;
    using Cthangover.Core.Mods.Resolvers;
    using Cthangover.Core.Utils;

    /// <summary>
    /// Checks the fenced <c>json</c> blocks of <c>docs/concepts/rpg.md</c> as data. A block holds one or more
    /// top-level values (a file envelope <c>{"Kind", "Items"}</c>, a patch wrapper <c>{"Items"}</c>, or bare items
    /// one per line). An item is read as the DTO its <c>Kind</c> names, or by its shape: a placement has
    /// <c>Object</c>, a map has a grid or <c>Legend</c> key, anything else is an object. A patch item is read as
    /// the DTO of the entity its <c>Id</c> names.
    /// <para>
    /// Unknown keys: a DTO without <see cref="JsonExtensionDataAttribute"/> rejects them. A DTO with it (the
    /// placement's <c>Overrides</c>, a state's components) takes them only when the property carries
    /// <see cref="PatchLayerAttribute"/>, and then each key must be a field of the layer's target. Keys
    /// are compared with their <c>$add</c>/<c>$remove</c>/<c>$set</c> suffix removed, case-insensitively,
    /// as the reader does. Only writable public properties count as fields.
    /// </para>
    /// <para>
    /// References: an object or placement <c>Id</c>, <c>Object</c>, <c>Extends</c>, the values of <c>Legend</c>,
    /// <c>Teleport.Map</c>, <c>Image</c> (picture), <c>Charset</c> and <c>Scenario</c> must exist in the loaded game.
    /// Pictures and charsets are checked by exact path and extension, so a wrong case is a miss.
    /// </para>
    /// </summary>
    public sealed class RpgConceptDocChecker
    {
        private const string KindObject = "object";
        private const string KindPlacement = "placement";
        private const string KindMap = "map";
        private const string KindPicture = "picture";
        private const string KindCharset = "charset";
        private const string KindScenario = "scenario";

        /// <summary>Keys that only a map has; any of them makes an item a map.</summary>
        private static readonly string[] MapKeys =
        {
            "Width", "Height", "Legend", "Ground", "Structure", "Collision", "Strokes", "Spawn", "TileSize",
            "PatchSize", "Zoom", "Encounters", "Atmosphere", "LightMode", "Ambient", "Effects"
        };

        private readonly Dictionary<string, RpgObject> _objects;
        private readonly Dictionary<string, RpgPlacement> _placements;
        private readonly Dictionary<string, RpgMap> _maps;
        private readonly HashSet<string> _pictures;
        private readonly HashSet<string> _charsets;
        private readonly HashSet<string> _scenarios;
        private readonly List<string> _errors = new();
        private int _line;

        /// <summary>Entity kinds read so far: <c>object</c>, <c>placement</c>, <c>map</c>, and <c>patch</c> for a patch wrapper.</summary>
        public HashSet<string> Seen { get; } = new();

        /// <summary>Loads the game data the examples must match: the entities of the three kinds and the file groups.</summary>
        public RpgConceptDocChecker()
        {
            _objects = Entities.Collect<RpgObject>();
            _placements = Entities.Collect<RpgPlacement>();
            _maps = Entities.Collect<RpgMap>();
            var textures = ModConfig.Instance.GetTextureExtensionSet();
            _pictures = Stems("rpg_images", textures);
            _charsets = Stems("charsets", textures);
            _scenarios = Stems("scenarios", new HashSet<string> { ".scenario" });
        }

        /// <summary>
        /// Every fenced <c>```json</c> block of a markdown text with the line where it starts. Only the exact fence
        /// <c>```json</c> opens a block and a line with <c>```</c> closes it.
        /// </summary>
        public static List<(int Line, string Text)> JsonBlocks(string markdown)
        {
            var blocks = new List<(int Line, string Text)>();
            var lines = markdown.Replace("\r\n", "\n").Split('\n');
            var open = false;
            var start = 0;
            var body = new StringBuilder();
            for (var i = 0; i < lines.Length; i++)
            {
                var trimmed = lines[i].Trim();
                if (!open)
                {
                    if (trimmed == "```json")
                    {
                        open = true;
                        start = i + 1;
                        body.Clear();
                    }
                    continue;
                }

                if (trimmed == "```")
                {
                    open = false;
                    blocks.Add((start, body.ToString()));
                    continue;
                }

                body.Append(lines[i]).Append('\n');
            }

            return blocks;
        }

        /// <summary>Checks every json block of the text and returns the problems, one per line, with the block's line.</summary>
        public List<string> Check(string markdown)
        {
            foreach (var block in JsonBlocks(markdown))
            {
                _line = block.Line;
                foreach (var value in TopLevelValues(block.Text))
                    CheckValue(value);
            }

            return _errors;
        }

        /// <summary>Splits a block into its top-level objects or arrays; text outside them is an error.</summary>
        private List<string> TopLevelValues(string text)
        {
            var values = new List<string>();
            var depth = 0;
            var inString = false;
            var escaped = false;
            var start = -1;
            for (var i = 0; i < text.Length; i++)
            {
                var c = text[i];
                if (depth == 0)
                {
                    if (char.IsWhiteSpace(c))
                        continue;
                    if (c == '{' || c == '[')
                    {
                        start = i;
                        depth = 1;
                        continue;
                    }

                    Error("text outside a JSON object or array");
                    return values;
                }

                if (inString)
                {
                    if (escaped)
                        escaped = false;
                    else if (c == '\\')
                        escaped = true;
                    else if (c == '"')
                        inString = false;
                    continue;
                }

                if (c == '"')
                    inString = true;
                else if (c == '{' || c == '[')
                    depth++;
                else if (c == '}' || c == ']')
                {
                    depth--;
                    if (depth == 0)
                        values.Add(text.Substring(start, i - start + 1));
                }
            }

            if (depth != 0)
                Error("unclosed JSON object or array");
            return values;
        }

        private void CheckValue(string value)
        {
            JsonDocument document;
            try
            {
                document = JsonDocument.Parse(value, JsonOptionsProvider.DocumentOptions);
            }
            catch (JsonException ex)
            {
                Error($"JSON does not parse: {ex.Message}");
                return;
            }

            using (document)
            {
                var root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Object)
                {
                    Error("a block holds JSON objects only");
                    return;
                }

                if (root.TryGetProperty("Kind", out var kindNode))
                {
                    var entity = EntityOfKind(kindNode.ValueKind == JsonValueKind.String ? kindNode.GetString() : null);
                    if (entity == null)
                    {
                        Error($"unknown Kind '{kindNode}'");
                        return;
                    }

                    if (!root.TryGetProperty("Items", out var items) || items.ValueKind != JsonValueKind.Array)
                    {
                        Error($"Kind '{kindNode}' without an Items array");
                        return;
                    }

                    foreach (var item in items.EnumerateArray())
                        CheckItem(entity, item, false);
                    return;
                }

                if (root.TryGetProperty("Items", out var patch) && patch.ValueKind == JsonValueKind.Array)
                {
                    Seen.Add("patch");
                    foreach (var item in patch.EnumerateArray())
                    {
                        var entity = EntityOfPatch(item);
                        if (entity != null)
                            CheckItem(entity, item, true);
                    }
                    return;
                }

                CheckItem(EntityOfShape(root), root, false);
            }
        }

        /// <summary>
        /// Reads one item as its entity's DTO, checks its keys, its own id, every reference it makes, and the
        /// change it makes to the entity it names (<see cref="CheckMerge"/>).
        /// </summary>
        private void CheckItem(string entity, JsonElement item, bool patch)
        {
            Seen.Add(entity);
            var type = TypeOf(entity);
            var id = item.TryGetProperty("Id", out var idNode) && idNode.ValueKind == JsonValueKind.String ? idNode.GetString() : null;
            try
            {
                JsonSerializer.Deserialize(item.GetRawText(), type, JsonOptionsProvider.ReadOptions);
            }
            catch (JsonException ex)
            {
                Error($"{entity} '{id}' does not read as {type.Name}: {ex.Message}");
                return;
            }

            if (id == null)
                Error($"{entity} without an Id");
            else
                Require(entity, id);
            CheckKeys(item, type, null, string.Empty);
            CollectReferences(item);
            CheckMerge(entity, id, item, patch);
        }

        /// <summary>
        /// Applies the item as the resolver does and reads the result: a patch entry to the entity it names, a
        /// placement's overrides to its object (<c>RpgObjectResolve.Effective</c>). A value of the wrong type or an
        /// operation that cannot apply is reported here; the resolver would only log and skip that layer.
        /// </summary>
        private void CheckMerge(string entity, string id, JsonElement item, bool patch)
        {
            var change = new JsonObject();
            foreach (var entry in item.EnumerateObject())
            {
                if (patch ? entry.Name == "Id" : IsPlacementField(entry.Name))
                    continue;
                change[entry.Name] = JsonNode.Parse(entry.Value.GetRawText());
            }

            JsonObject target;
            Type type;
            if (patch)
            {
                target = NodeOf(entity, id);
                type = TypeOf(entity);
            }
            else if (entity == KindPlacement)
            {
                var objectId = item.TryGetProperty("Object", out var objectNode) && objectNode.ValueKind == JsonValueKind.String ? objectNode.GetString() : null;
                if (objectId == null || !_objects.TryGetValue(objectId, out var resolved))
                    return;
                target = JsonSerializer.SerializeToNode(resolved, JsonOptionsProvider.ReadOptions) as JsonObject;
                type = typeof(RpgObject);
            }
            else
            {
                return;
            }

            try
            {
                JsonPatchMerger.Apply(target, change, type);
                target.Deserialize(type, JsonOptionsProvider.ReadOptions);
            }
            catch (JsonPatchException ex)
            {
                Error($"{entity} '{id}': {ex.Path}: {ex.Message}");
            }
            catch (JsonException ex)
            {
                Error($"{entity} '{id}' with its changes does not read as {type.Name}: {ex.Message}");
            }
        }

        private JsonObject NodeOf(string entity, string id)
        {
            switch (entity)
            {
                case KindPlacement:
                    return JsonSerializer.SerializeToNode(_placements[id], JsonOptionsProvider.ReadOptions) as JsonObject;
                case KindMap:
                    return JsonSerializer.SerializeToNode(_maps[id], JsonOptionsProvider.ReadOptions) as JsonObject;
                default:
                    return JsonSerializer.SerializeToNode(_objects[id], JsonOptionsProvider.ReadOptions) as JsonObject;
            }
        }

        /// <summary>The keys a placement reads itself; every other key is an override of its object.</summary>
        private static bool IsPlacementField(string name) => name is "Id" or "Object" or "X" or "Y" or "Variant";

        private string EntityOfKind(string kind) => kind switch
        {
            "rpg_object" => KindObject,
            "rpg_placement" => KindPlacement,
            "rpg_map" => KindMap,
            _ => null,
        };

        private string EntityOfPatch(JsonElement item)
        {
            var id = item.TryGetProperty("Id", out var idNode) && idNode.ValueKind == JsonValueKind.String ? idNode.GetString() : null;
            if (id == null)
            {
                Error("patch item without an Id");
                return null;
            }

            if (_objects.ContainsKey(id))
                return KindObject;
            if (_placements.ContainsKey(id))
                return KindPlacement;
            if (_maps.ContainsKey(id))
                return KindMap;
            Error($"patch item names no object, placement or map '{id}'");
            return null;
        }

        private static string EntityOfShape(JsonElement root)
        {
            if (root.TryGetProperty("Object", out _))
                return KindPlacement;
            foreach (var key in MapKeys)
            {
                if (root.TryGetProperty(key, out _))
                    return KindMap;
            }

            return KindObject;
        }

        private static Type TypeOf(string entity) => entity switch
        {
            KindPlacement => typeof(RpgPlacement),
            KindMap => typeof(RpgMap),
            _ => typeof(RpgObject),
        };

        private void Require(string entity, string id)
        {
            var found = entity switch
            {
                KindObject => _objects.ContainsKey(id),
                KindPlacement => _placements.ContainsKey(id),
                KindMap => _maps.ContainsKey(id),
                KindPicture => _pictures.Contains(id),
                KindCharset => _charsets.Contains(id),
                KindScenario => _scenarios.Contains(id),
                _ => false,
            };
            if (!found)
                Error($"{entity} '{id}' not found in the game data");
        }

        /// <summary>Requires every id and path the item names outside its own Id (which <see cref="CheckItem"/> requires).</summary>
        private void CollectReferences(JsonElement node)
        {
            if (node.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in node.EnumerateArray())
                    CollectReferences(item);
                return;
            }

            if (node.ValueKind != JsonValueKind.Object)
                return;

            foreach (var entry in node.EnumerateObject())
            {
                var value = entry.Value;
                var isString = value.ValueKind == JsonValueKind.String;
                var name = entry.Name;
                if (Is(name, "Object") && isString)
                    Require(KindObject, value.GetString());
                else if (Is(name, "Extends") && isString)
                    Require(KindObject, value.GetString());
                else if (Is(name, "Image") && isString)
                    Require(KindPicture, value.GetString());
                else if (Is(name, "Charset") && isString)
                    Require(KindCharset, value.GetString());
                else if (Is(name, "Scenario") && isString)
                    Require(KindScenario, value.GetString());
                else if (Is(name, "Legend") && value.ValueKind == JsonValueKind.Object)
                {
                    foreach (var cell in value.EnumerateObject())
                    {
                        if (cell.Value.ValueKind == JsonValueKind.String)
                            Require(KindObject, cell.Value.GetString());
                    }
                }
                else if (Is(name, "Teleport") && value.ValueKind == JsonValueKind.Object)
                {
                    if (value.TryGetProperty("Map", out var map) && map.ValueKind == JsonValueKind.String && map.GetString().Length > 0)
                        Require(KindMap, map.GetString());
                }

                CollectReferences(value);
            }
        }

        /// <summary>Checks the keys of a node against the type; <paramref name="layer"/> is the target of a patch layer.</summary>
        private void CheckKeys(JsonElement node, Type type, Type layer, string path)
        {
            if (node.ValueKind == JsonValueKind.Array)
            {
                var element = ElementType(type);
                if (element == null)
                    return;
                foreach (var item in node.EnumerateArray())
                    CheckKeys(item, element, layer, path);
                return;
            }

            if (node.ValueKind != JsonValueKind.Object)
                return;

            var values = DictionaryValue(type);
            if (values != null)
            {
                foreach (var entry in node.EnumerateObject())
                    CheckKeys(entry.Value, values, layer, Join(path, entry.Name));
                return;
            }

            if (layer != null)
            {
                CheckObject(node, layer, path);
                return;
            }

            if (!IsLeaf(type))
                CheckObject(node, type, path);
        }

        private void CheckObject(JsonElement node, Type type, string path)
        {
            var extension = ExtensionOf(type);
            var target = extension == null ? null : LayerOf(extension);
            foreach (var entry in node.EnumerateObject())
            {
                if (entry.Name == "$delete")
                    continue;

                var name = BaseName(entry.Name);
                var where = Join(path, entry.Name);
                var property = FindField(type, name);
                if (property != null)
                {
                    CheckKeys(entry.Value, property.PropertyType, LayerOf(property), where);
                    continue;
                }

                if (extension == null)
                {
                    Error($"unknown key '{entry.Name}' in {type.Name} ({where})");
                    continue;
                }

                if (target == null)
                    continue;

                var overridden = FindField(target, name);
                if (overridden == null)
                {
                    Error($"unknown key '{entry.Name}' in {target.Name} ({where}; an override of the {type.Name})");
                    continue;
                }

                CheckKeys(entry.Value, overridden.PropertyType, LayerOf(overridden), where);
            }
        }

        /// <summary>The field a JSON key names: a writable public property, by its <c>JsonPropertyName</c> or name, ignoring case.</summary>
        private static PropertyInfo FindField(Type type, string name)
        {
            foreach (var property in Properties(type))
            {
                if (property.SetMethod == null || !property.SetMethod.IsPublic)
                    continue;
                if (property.IsDefined(typeof(JsonIgnoreAttribute), true) || property.IsDefined(typeof(JsonExtensionDataAttribute), true))
                    continue;
                if (string.Equals(JsonNameOf(property), name, StringComparison.OrdinalIgnoreCase))
                    return property;
            }

            return null;
        }

        private static IEnumerable<PropertyInfo> Properties(Type type) => type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        /// <summary>The property that takes the keys no field of the type names (<see cref="JsonExtensionDataAttribute"/>), or null.</summary>
        private static PropertyInfo ExtensionOf(Type type)
        {
            foreach (var property in Properties(type))
            {
                if (property.IsDefined(typeof(JsonExtensionDataAttribute), true))
                    return property;
            }

            return null;
        }

        private static string JsonNameOf(PropertyInfo property)
            => property.GetCustomAttribute<JsonPropertyNameAttribute>(true)?.Name ?? property.Name;

        private static Type LayerOf(MemberInfo member) => member.GetCustomAttribute<PatchLayerAttribute>(true)?.Target;

        /// <summary>The key without its operation suffix: <c>Commands$add</c> names <c>Commands</c>.</summary>
        private static string BaseName(string key)
        {
            var dollar = key.IndexOf('$');
            return dollar < 0 ? key : key.Substring(0, dollar);
        }

        private static string Join(string path, string name) => path.Length == 0 ? name : path + "." + name;

        private static bool Is(string name, string expected) => string.Equals(name, expected, StringComparison.OrdinalIgnoreCase);

        private static bool IsLeaf(Type type)
        {
            var underlying = Nullable.GetUnderlyingType(type) ?? type;
            return underlying.IsPrimitive || underlying.IsEnum || underlying == typeof(string)
                || underlying == typeof(decimal) || underlying == typeof(JsonElement) || underlying == typeof(object);
        }

        private static Type ElementType(Type type)
        {
            if (type.IsArray)
                return type.GetElementType();
            if (type == typeof(string))
                return null;
            foreach (var candidate in new[] { type }.Concat(type.GetInterfaces()))
            {
                if (candidate.IsGenericType && candidate.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                    return candidate.GetGenericArguments()[0];
            }

            return null;
        }

        private static Type DictionaryValue(Type type)
        {
            foreach (var candidate in new[] { type }.Concat(type.GetInterfaces()))
            {
                if (candidate.IsGenericType && candidate.GetGenericTypeDefinition() == typeof(IDictionary<,>))
                    return candidate.GetGenericArguments()[1];
            }

            return null;
        }

        private static HashSet<string> Stems(string group, ISet<string> extensions)
        {
            var result = new HashSet<string>(StringComparer.Ordinal);
            foreach (var key in Files.Collect(group).Keys)
            {
                var extension = System.IO.Path.GetExtension(key);
                if (extensions.Contains(extension))
                    result.Add(key.Substring(0, key.Length - extension.Length));
            }

            return result;
        }

        private void Error(string message) => _errors.Add($"line {_line}: {message}");
    }
}
