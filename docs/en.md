# RPG World — walk a tile map, talk, open, fight

RPG World turns a game into a tile-map RPG in the spirit of classic 2D role-playing games: you walk a grid map with the keyboard, talk to people, open chests, step through doors and fight monsters, then come back to the same spot. A map is an ordinary scene of the game, so the dialog box, scenarios, saving, music and time of day all work on maps as they do in a visual novel.

It powers the village, forest and desert of **RPG Plus**. The maps and people themselves come from content mods such as `rpg_plus_game`; this mod supplies the rules.

![Exploring the village in RPG Plus: the map name at the top left, the toolbar at the top right, the controls hint at the bottom left.](assets/village.jpg)

## How to play on a map

- **Walk** with WASD or the arrow keys; hold **Shift** to run.
- **Act** with **E** or **F**: talk to the person you face, open the chest, read the sign.
- **Step** onto a doorway or a map edge to go to the next map.
- **Random battles** can start as you walk in some places, such as the forest.
- After a battle you are back on the same cell of the same map.
- The game saves where you stand; loading puts you back there.

The cell you can act on is framed, and the hint at the bottom left changes to **E / F · Interact**.

## Example: a chest that stays open

The chest in the elder's house is one placement of the chest object, with two states. While the story flag `rpg_house_chest` is not set, the closed chest shows; its scenario gives three coins and sets the flag. From then on the open state shows instead.

![Before: the hero next to the closed chest in the elder's house at noon.](assets/chest-before.jpg)

![The chest's text: old socks, a note saying "Don't touch", and three coins underneath.](assets/chest-text.jpg)

![After: the same cell now shows the open chest, framed by the interaction marker; the hint at the bottom left reads "E / F · Interact".](assets/chest-after.jpg)

```json
{"Id": "rpg_elder_house/chest", "Object": "village_pack/container/chest", "X": 9, "Y": 7,
 "Interact": {"Scenario": "rpg_demo/events/house_chest", "Commands": null},
 "States$add": [{"Id": "open", "When": "rpg_house_chest == 1",
                 "Interact": {"Scenario": "rpg_demo/events/chest_empty", "Commands": null}}]}
```

```scenario
ptext "In the chest: old socks and a note saying 'Don't touch'. Too late. Under the socks: three coins." key=rpg_demo/chest/house
action item.add id=money/coin count=3
flag rpg_house_chest 1
end
```

## For authors: maps

A map is a JSON file in a mod's `maps/` folder. It holds the ground only: what stands on it (trees, houses, barrels, people) is placed by placements. The scene with the same name in `scenes/` gives the battle background and the map's ambience.

- **Legend** — one character per kind of surface or fence: `"g": "village_pack/ground/grass"`. The dot `.` is an empty cell.
- **Ground** and **Structure** — rows of characters, one per cell: surfaces (grass, water, floors) and fences or walls.
- **Collision** — rows of `#` (blocked), `o` (forced open, for example a gap in a wall) and `.` (as the objects decide).
- **Spawn**, **Zoom**, **Ambient** (a fixed light colour for interiors), **Atmosphere** (`woodland`, `winter_snow`, `desert_dust`, `rain`) and **Encounters** (`Steps` and `Groups`):

```json
"Encounters": { "Steps": 48, "Groups": ["rpg_slime", "rpg_slime,rpg_slime"] }
```

## For authors: objects and placements

Objects are defined in `objects/*.json` (`rpg_object`). A placement puts one object on a map: a file `placements/<map>/*.json` (`rpg_placement`) with an `Id` of the form `<map>/<name>`, `Object`, `X`, `Y`, and the fields that change only that one instance.

- **Form** — `surface` (ground), `segment` (a piece of fence that joins its neighbours), `sprite` (a picture over an area of cells: trees, houses, barrels, doors) or `actor` (a character with a walking sheet).
- **Solid** — blocks walking. A sprite blocks by default; a trigger needs `"Solid": false`.
- **Interact** — `Trigger` is `Use` (E or F while facing it) or `Step` (stepping onto the cell); then one of `Scenario`, `Commands` (lines of the scenario language) or `Teleport` (`{Map, X, Y, Dir}`).
- **States** — `When` (a condition) and what changes: `Hidden`, another picture, `Interact`, `Route`. The last state whose `When` holds applies. In a placement, `self.name` is its own flag `<Id>:name`.
- **Route**, **Light** and **Lights** — as before (`Steps`, `Every`; a glow with `Radius`, `Color` and a cell offset `At` from the object).
- **Extends** and **Variants** — inherit from another object and keep versions (for example a winter one).
- **Add to another mod's map** — ship a placement with your own `Id`. Replace one by the same `Id` with `depends` on its owner; delete one with a patch `"$delete": true`.
- **The hero's look** — the charset of the party leader (the character's `Charset`, for example `village/hero`).

## Scenario actions

```scenario
action rpg.battle enemies=rpg_wolf,rpg_slime win_flag=rpg_wolf_dead
action rpg.teleport map=rpg_forest x=1 y=8 dir=right
```

- `rpg.battle` starts a battle with the game's battle core and returns to the same cell; on victory `win_flag` is set to 1, so a map monster whose condition tests that flag disappears. End the scenario after it.
- `rpg.teleport` moves the player to a cell, on this map or another one.

## Game settings

- **Map projection** (`tile.renderer`) — top-down or isometric; see Tile Rendering.

## If something goes wrong

1. **A person or chest does not react.** Face it and press E or F; a `Step` object reacts when you step onto it.
2. **A monster or chest comes back.** The state that hides or changes it must test the flag that its scenario or `win_flag` sets.
3. **You cannot walk somewhere that looks open.** A solid object (`Solid`) or a `#` in the map's collision blocks the cell.
4. **No battle background.** The map's scene in `scenes/<map>.json` needs a `defaultBackground`.
