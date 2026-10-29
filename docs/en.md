# RPG World — walk a tile map, talk, open, fight

RPG World turns a game into a tile-map RPG in the spirit of classic 2D role-playing games: you walk a grid map with the keyboard, talk to people, open chests, step through doors and fight monsters, then come back to the same spot. A map is an ordinary scene of the game, so the dialog box, scenarios, saving, music and time of day all work on maps as they do in a visual novel.

It powers the village, forest and desert of **RPG Plus**. The maps and people themselves come from content mods such as RPG Demo; this mod supplies the rules.

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

The chest in the elder's house is two events on the same cell. The closed one shows while the story flag `rpg_house_chest` is not set; its scenario gives three coins and sets the flag. From then on the open chest shows instead.

![Before: the hero next to the closed chest in the elder's house at noon.](assets/chest-before.jpg)

![The chest's text: old socks, a note saying "Don't touch", and three coins underneath.](assets/chest-text.jpg)

![After: the same cell now shows the open chest, framed by the interaction marker; the hint at the bottom left reads "E / F · Interact".](assets/chest-after.jpg)

```json
{ "Id": "chest", "X": 9, "Y": 7, "Tile": 14,
  "Scenario": "rpg_demo/events/house_chest",
  "Condition": "rpg_house_chest != 1" },
{ "Id": "chest_open", "X": 9, "Y": 7, "Tile": 15,
  "Scenario": "rpg_demo/events/chest_empty",
  "Condition": "rpg_house_chest == 1" }
```

```scenario
ptext "In the chest: old socks and a note saying 'Don't touch'. Too late. Under the socks: three coins." key=rpg_demo/chest/house
action item.add id=money/coin count=3
flag rpg_house_chest 1
end
```

## For authors: maps

A map is a JSON file in a mod's `maps/` folder, plus a scene with the same name in `scenes/` for its background in battles and its ambience.

- **Three drawn layers**, each a list of rows of tile numbers (`.` for empty): `Back` — ground, floors, water; `Main` — objects at the characters' level: walls, trunks, fences; `Front` — drawn over the characters: tree crowns, eaves, arches.
- **Collision** — a row layer that overrides the tiles' own passability where needed: an invisible wall at a cliff edge or a gap in a hedge.
- **Tileset** — the tile atlas and the properties of its tiles (solid or not, surfaces, objects).
- **Spawn** — where a new arrival appears; **Zoom**; optional **Ambient** colour for interiors (otherwise the time of day lights the map) and **Atmosphere** (`woodland` adds leaves, fireflies and water ripples).
- **Encounters** — random battles: on average one per `Steps` steps, against one of the `Groups`.

```json
"Encounters": { "Steps": 48, "Groups": ["rpg_slime", "rpg_slime,rpg_slime"] }
```

## For authors: events

Events live in `map_events/<map id>/*.json` of any mod. An event has a **look** and an **effect**, chosen independently:

- look: a character `Sprite`, a single atlas `Tile`, an item `Icon` lying on the ground, or nothing (an invisible trigger);
- effect: a `Teleport` to a cell of this or another map, or a `Scenario` followed by optional inline `Commands` — the full scenario language, including `action rpg.battle`, flags and quests;
- `Trigger`: `Action` (E/F while facing it) or `Touch` (stepping onto it);
- `Solid`: whether it blocks the way (by default, drawn events do);
- `Condition`: hide the event unless the condition holds;
- `Pages`: alternative looks and effects, each with its own condition — the last page whose condition holds is used;
- `Route`: autonomous movement such as `{"Steps": ["left", "wait", "right"], "Every": 0.8}` with `up`/`down`/`left`/`right`, `random`, `toward`/`away` (from the player), `wait` and `face_…`;
- `Light`: a glow around the event, `{"Radius": 3, "Color": "#ffd8a0"}`, seen at dusk and night.

**Add to or change another mod's map.** Event files are merged in load order: a new `Id` adds an event to that map, and the same `Id` from a mod that depends on the owner replaces it. This is how RPG Plus gives the demo's elder new dialogs.

## Scenario actions

```scenario
action rpg.battle enemies=rpg_wolf,rpg_slime win_flag=rpg_wolf_dead
action rpg.teleport map=rpg_forest x=1 y=8 dir=right
```

- `rpg.battle` starts a battle with the game's battle core and returns to the same cell; on victory `win_flag` is set to 1, so a map monster whose condition tests that flag disappears. End the scenario after it.
- `rpg.teleport` moves the player to a cell, on this map or another one.

## Game settings

- **Appearance** (`rpg.player_charset`) — the hero's sprite on maps, chosen from the charsets in the game.
- **Map projection** (`tile.renderer`) — top-down or isometric; see Tile Rendering.

## If something goes wrong

1. **A person or chest does not react.** Face it and press E or F; a `Touch` event reacts only when you step on it.
2. **A monster or chest comes back.** Its `Condition` must test the flag that its scenario or `win_flag` sets.
3. **You cannot walk somewhere that looks open.** A solid tile or the map's collision layer blocks that cell.
4. **No battle background.** The map's scene in `scenes/<map>.json` needs a `defaultBackground`.
