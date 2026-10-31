namespace Cthangover.Mods.Rpg.Model.Tests
{
    using System;
    using System.Text.Json;
    using Cthangover.Core.Mods.Testing;
    using Cthangover.Core.Utils;

    /// <summary>
    /// The resolution of a placed object after the entity resolver: map variant, placement
    /// overrides (both deep merges with the patch engine), then the active state (wholesale
    /// component replacement), including <c>null</c> at every stage and skipped broken layers.
    /// </summary>
    public class RpgObjectResolveTests
    {
        private const string Oak = @"{""Id"": ""village_pack/vegetation/oak"", ""Form"": ""sprite"", ""Category"": ""vegetation"",
            ""Footprint"": {""Size"": [2, 2], ""Solid"": ["".."", "".#""]}, ""Anchor"": [1, 2],
            ""Parts"": [{""Id"": ""tree"", ""Image"": ""vegetation/oak"", ""Crop"": [0, 0, 64, 96]}],
            ""Light"": {""Radius"": 1},
            ""Variants"": {
                ""winter"": {""Parts$add"": [{""Id"": ""tree"", ""Image"": ""vegetation/oak_winter""}, {""Id"": ""snow"", ""Image"": ""vegetation/snow_cap""}]},
                ""autumn"": {""Parts"": [{""Image"": ""vegetation/oak_autumn""}], ""Light"": null}
            }}";

        private const string Door = @"{""Id"": ""village_pack/door/wood_double"", ""Form"": ""sprite"", ""Category"": ""door"",
            ""Footprint"": {""Size"": [2, 1]}, ""Column"": 2,
            ""Parts"": [{""Id"": ""left"", ""Image"": ""door/left"", ""Geometry"": ""plane""}, {""Id"": ""right"", ""Image"": ""door/right"", ""At"": [1, 0], ""Geometry"": ""plane""}],
            ""Interact"": {""Trigger"": ""Step"", ""Teleport"": {""Map"": ""somewhere"", ""X"": 1, ""Y"": 1}},
            ""Light"": {""Radius"": 2},
            ""States"": [
                {""Id"": ""locked"", ""When"": ""self.locked == 1"", ""Interact"": {""Commands"": [""ptext \""Locked.\""""]}, ""Light"": null, ""Column"": null},
                {""Id"": ""gone"", ""When"": ""self.gone == 1"", ""Hidden"": true, ""Parts"": [{""Id"": ""left"", ""Image"": ""door/broken""}], ""Name"": ""not a component""},
                {""Id"": ""silent"", ""When"": """"}
            ]}";

        private static RpgObject Object(string json) => JsonSerializer.Deserialize<RpgObject>(json, JsonOptionsProvider.ReadOptions);

        private static RpgPlacement Placement(string json) => JsonSerializer.Deserialize<RpgPlacement>(json, JsonOptionsProvider.ReadOptions);

        private static Func<string, bool> Holding(params string[] conditions) => condition => Array.IndexOf(conditions, condition) >= 0;

        [ModTest("resolve/no_variant_no_overrides_is_the_object")]
        static void NothingToApply(ModTestContext ctx)
        {
            var grass = Object(@"{""Id"": ""g"", ""Form"": ""surface"", ""Surface"": {""Image"": ""ground/grass""}}");
            ModAssert.True(ReferenceEquals(grass, RpgObjectResolve.Effective(grass, "winter")), "nothing to merge: the same instance");
            ModAssert.Null(RpgObjectResolve.Effective(null, "winter"), "no object");

            var oak = Object(Oak);
            var summer = RpgObjectResolve.Effective(oak, "summer");
            ModAssert.Equal("vegetation/oak", summer.Parts[0].Image, "unknown variant: the object's picture");
            ModAssert.Null(summer.Variants, "variants are spent in the effective object");
            ModAssert.True(oak.Variants.Count == 2, "the input is not changed");
            ModAssert.Equal("..", summer.Footprint.Solid[0], "everything else kept");
            ModAssert.Equal(1f, summer.Anchor[0], 1e-6f, "anchor kept");
        }

        [ModTest("resolve/variant_deep_merges_parts_by_id")]
        static void VariantMergesById(ModTestContext ctx)
        {
            var oak = Object(Oak);
            var winter = RpgObjectResolve.Effective(oak, "Winter");
            ModAssert.Equal(2, winter.Parts.Count, "the variant's Parts$add merges one part and adds one");
            ModAssert.Equal("vegetation/oak_winter", winter.Parts[0].Image, "part merged by id, variant name case-insensitive");
            ModAssert.Equal(96, winter.Parts[0].Crop[3], "the merged part keeps its crop");
            ModAssert.Equal("vegetation/snow_cap", winter.Parts[1].Image, "new part appended");
            ModAssert.NotNull(winter.Light, "untouched component kept");
            ModAssert.Equal("vegetation/oak", oak.Parts[0].Image, "the object itself is not changed");
        }

        [ModTest("resolve/variant_null_removes_and_array_replaces")]
        static void VariantNullRemoves(ModTestContext ctx)
        {
            var autumn = RpgObjectResolve.Effective(Object(Oak), "autumn");
            ModAssert.Null(autumn.Light, "null in a variant removes the component");
            ModAssert.Equal(1, autumn.Parts.Count, "an array without an operation replaces");
            ModAssert.Equal("vegetation/oak_autumn", autumn.Parts[0].Image, "the variant's picture");
            ModAssert.Null(autumn.Parts[0].Crop, "replaced, not merged");
        }

        [ModTest("resolve/placement_variant_beats_map_variant")]
        static void PlacementVariant(ModTestContext ctx)
        {
            var oak = Object(Oak);
            var bare = Placement(@"{""Id"": ""rpg_village/oak_1"", ""Object"": ""village_pack/vegetation/oak"", ""X"": 3, ""Y"": 4, ""Variant"": ""autumn""}");
            ModAssert.Equal("vegetation/oak_autumn", RpgObjectResolve.Effective(oak, "winter", bare).Parts[0].Image, "the placement's own variant wins");
            var plain = Placement(@"{""Id"": ""rpg_village/oak_2"", ""Object"": ""village_pack/vegetation/oak"", ""X"": 3, ""Y"": 4}");
            ModAssert.Equal("vegetation/oak_winter", RpgObjectResolve.Effective(oak, "winter", plain).Parts[0].Image, "no own variant: the map's");
        }

        [ModTest("resolve/placement_overrides_deep_merge")]
        static void PlacementOverrides(ModTestContext ctx)
        {
            var door = Object(Door);
            var placement = Placement(@"{""Id"": ""rpg_village/door_townhouse"", ""Object"": ""village_pack/door/wood_double"", ""X"": 40, ""Y"": 11,
                ""Interact"": {""Teleport"": {""Map"": ""rpg_plus_townhouse"", ""X"": 5, ""Y"": 8, ""Dir"": ""Up""}},
                ""Parts$add"": [{""Id"": ""right"", ""Image"": ""door/right_painted""}], ""Footprint"": {""Solid"": [""##""]}}");
            var effective = RpgObjectResolve.Effective(door, null, placement);
            ModAssert.Equal("village_pack/door/wood_double", effective.ID, "the effective object keeps the object id");
            ModAssert.True(effective.Interact.Trigger == RpgInteractTrigger.Step, "trigger kept from the object (deep merge)");
            ModAssert.Equal("rpg_plus_townhouse", effective.Interact.Teleport.Map, "teleport target from the placement");
            ModAssert.True(effective.Interact.Teleport.Dir == RpgDirection.Up, "facing from the placement");
            ModAssert.Equal(2, effective.Parts.Count, "Parts$add by id does not duplicate");
            ModAssert.Equal("door/right_painted", effective.Parts[1].Image, "this instance's picture");
            ModAssert.Equal(1f, effective.Parts[1].At[0], 1e-6f, "merged part keeps its offset");
            ModAssert.Equal(2, effective.Footprint.Width, "footprint size kept");
            ModAssert.Equal("##", effective.Footprint.Solid[0], "footprint mask from the placement");
            ModAssert.Equal("somewhere", door.Interact.Teleport.Map, "the object is not changed");
            ModAssert.Equal(3, effective.States.Count, "states kept for the runtime");
        }

        [ModTest("resolve/placement_null_and_add")]
        static void PlacementNullAndAdd(ModTestContext ctx)
        {
            var barrel = Object(@"{""Id"": ""barrel"", ""Form"": ""sprite"", ""Parts"": [{""Image"": ""container/barrel""}],
                ""Interact"": {""Commands"": [""flag self.open 1""]}, ""Route"": {""Steps"": [""wait""]}}");
            var still = RpgObjectResolve.Effective(barrel, null, Placement(@"{""Id"": ""m/deco"", ""Object"": ""barrel"", ""Interact"": null, ""Route"": null}"));
            ModAssert.Null(still.Interact, "null removes the interaction of this instance");
            ModAssert.Null(still.Route, "null removes the route");
            var rich = RpgObjectResolve.Effective(barrel, null, Placement(@"{""Id"": ""m/supply"", ""Object"": ""barrel"",
                ""Interact"": {""Commands$add"": [""action item.add id=money/coin count=2""]}}"));
            ModAssert.Equal(2, rich.Interact.Commands.Count, "Commands$add appends");
            ModAssert.Equal("action item.add id=money/coin count=2", rich.Interact.Commands[1], "after the object's line");
        }

        [ModTest("resolve/placement_cannot_change_identity")]
        static void PlacementFixedFields(ModTestContext ctx)
        {
            var barrel = Object(@"{""Id"": ""barrel"", ""Form"": ""sprite"", ""Parts"": []}");
            var effective = RpgObjectResolve.Effective(barrel, null, Placement(@"{""Id"": ""m/b"", ""Object"": ""barrel"", ""Form"": ""actor"", ""Extends"": ""other"", ""Hidden"": true}"));
            ModAssert.True(effective.Form == RpgObjectForm.Sprite, "Form is not overridable");
            ModAssert.Null(effective.Extends, "Extends is not overridable");
            ModAssert.Equal("barrel", effective.ID, "id kept");
            ModAssert.True(effective.Hidden, "other fields in the same placement still apply");
        }

        [ModTest("resolve/broken_layer_is_skipped_alone")]
        static void BrokenLayerSkipped(ModTestContext ctx)
        {
            var oak = Object(Oak);
            var badOperation = Placement(@"{""Id"": ""m/o"", ""Object"": ""oak"", ""Parts$add"": ""not a list""}");
            var effective = RpgObjectResolve.Effective(oak, "winter", badOperation);
            ModAssert.Equal("vegetation/oak_winter", effective.Parts[0].Image, "the variant still applies");
            ModAssert.Equal(2, effective.Parts.Count, "the broken placement layer is not applied");

            var badType = Placement(@"{""Id"": ""m/o2"", ""Object"": ""oak"", ""Column"": ""wide""}");
            ModAssert.Equal(1, RpgObjectResolve.Effective(oak, null, badType).Column, "a value of the wrong type skips the layer");
        }

        [ModTest("resolve/last_holding_state_wins")]
        static void ActiveState(ModTestContext ctx)
        {
            var door = Object(Door);
            ModAssert.Equal(-1, RpgObjectResolve.ActiveState(door, Holding()), "nothing holds");
            ModAssert.Equal(0, RpgObjectResolve.ActiveState(door, Holding("self.locked == 1")), "the locked state");
            ModAssert.Equal(1, RpgObjectResolve.ActiveState(door, Holding("self.locked == 1", "self.gone == 1")), "the later state wins");
            ModAssert.Equal(-1, RpgObjectResolve.ActiveState(door, Holding("")), "a state without a condition never applies");
            ModAssert.Equal(-1, RpgObjectResolve.ActiveState(Object(Oak), Holding("x")), "an object without states");
            ModAssert.Equal(-1, RpgObjectResolve.ActiveState(door, null), "no evaluator");
        }

        [ModTest("resolve/state_replaces_components_wholesale")]
        static void StateReplaces(ModTestContext ctx)
        {
            var door = RpgObjectResolve.Effective(Object(Door), null, Placement(@"{""Id"": ""m/d"", ""Object"": ""door"", ""Light"": {""Color"": ""#ff0000""}}"));
            var locked = RpgObjectResolve.WithState(door, 0);
            ModAssert.NotNull(locked.Interact, "the state's interaction");
            ModAssert.Null(locked.Interact.Teleport, "replaced wholesale: the teleport is gone without a null");
            ModAssert.True(locked.Interact.Trigger == RpgInteractTrigger.Use, "replaced wholesale: the trigger is the state's (default), not merged");
            ModAssert.Equal("ptext \"Locked.\"", locked.Interact.Commands[0], "the state's line");
            ModAssert.Null(locked.Light, "null in a state removes the component");
            ModAssert.Equal(1, locked.Column, "a removed value component returns to its default");
            ModAssert.Equal(2, locked.Parts.Count, "components the state does not name are kept");
            ModAssert.Equal(3, locked.States.Count, "states kept for re-evaluation");
            ModAssert.NotNull(door.Interact.Teleport, "the input is not changed");
        }

        [ModTest("resolve/state_parts_replace_not_merge")]
        static void StateParts(ModTestContext ctx)
        {
            var gone = RpgObjectResolve.WithState(Object(Door), 1);
            ModAssert.True(gone.Hidden, "hidden by the state");
            ModAssert.Equal(1, gone.Parts.Count, "a state's parts replace the list, they do not merge by id");
            ModAssert.Equal("door/broken", gone.Parts[0].Image, "the state's picture");
            ModAssert.True(gone.Parts[0].Geometry == RpgPartGeometry.Upright, "nothing merged from the replaced part");
            ModAssert.Null(gone.Name, "a field that is not a state component is ignored");
            ModAssert.NotNull(gone.Interact.Teleport, "unnamed component kept");
        }

        [ModTest("resolve/state_index_outside_is_the_object")]
        static void StateOutside(ModTestContext ctx)
        {
            var door = Object(Door);
            ModAssert.True(ReferenceEquals(door, RpgObjectResolve.WithState(door, -1)), "no active state");
            ModAssert.True(ReferenceEquals(door, RpgObjectResolve.WithState(door, 3)), "past the end");
            ModAssert.True(ReferenceEquals(door, RpgObjectResolve.WithState(door, 2)), "a state naming no components");
            ModAssert.Null(RpgObjectResolve.WithState(null, 0), "no object");
        }

        [ModTest("resolve/full_chain_barrel")]
        static void FullChain(ModTestContext ctx)
        {
            var barrel = Object(@"{""Id"": ""village_pack/container/barrel"", ""Form"": ""sprite"",
                ""Parts"": [{""Id"": ""body"", ""Image"": ""container/barrel_closed""}],
                ""Interact"": {""Commands"": [""flag self.open 1""]},
                ""Variants"": {""winter"": {""Parts$add"": [{""Id"": ""body"", ""Image"": ""container/barrel_snow""}]}},
                ""States"": [{""Id"": ""open"", ""When"": ""self.open == 1"", ""Parts"": [{""Image"": ""container/barrel_open""}],
                             ""Interact"": {""Commands"": [""ptext \""Empty.\""""]}}]}");
            var placement = Placement(@"{""Id"": ""rpg_village/supply_1"", ""Object"": ""village_pack/container/barrel"", ""X"": 19, ""Y"": 11,
                ""Interact"": {""Commands$add"": [""action item.add id=money/coin count=2""]},
                ""States$add"": [{""Id"": ""open"", ""When"": ""self.open >= 1""}]}");

            var effective = RpgObjectResolve.Effective(barrel, "winter", placement);
            ModAssert.Equal("container/barrel_snow", effective.Parts[0].Image, "variant");
            ModAssert.Equal(2, effective.Interact.Commands.Count, "placement adds the reward");
            ModAssert.Equal(1, effective.States.Count, "States$add merges by id");
            ModAssert.Equal("self.open >= 1", effective.States[0].When, "the placement changed the condition");
            ModAssert.True(effective.States[0].Components.ContainsKey("Parts"), "the state's components kept by the merge");

            var index = RpgObjectResolve.ActiveState(effective, Holding("self.open >= 1"));
            var open = RpgObjectResolve.WithState(effective, index);
            ModAssert.Equal("container/barrel_open", open.Parts[0].Image, "the state's picture beats the variant's");
            ModAssert.Equal(1, open.Interact.Commands.Count, "the state's interaction beats the placement's");
        }
    }
}
