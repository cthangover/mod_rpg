# RPG World — 在图块地图上行走、交谈、开启与战斗

RPG World 把游戏变成经典 2D 角色扮演风格的图块地图 RPG：用键盘在网格地图上行走，与人交谈，打开宝箱，穿过房门，与怪物战斗——打完再回到原地。地图本身就是游戏的普通场景，所以对话框、剧本、存档、音乐和昼夜变化在地图上的表现与视觉小说里完全一样。

**RPG Plus** 的村庄、森林和沙漠都由它驱动。地图和人物本身来自 `rpg_plus_game` 之类的内容模组；这个模组提供的是规则。

![在 RPG Plus 中探索村庄：左上角是地图名，右上角是工具栏，左下角是操作提示。](assets/village.jpg)

## 在地图上怎么玩

- **行走**用 WASD 或方向键；按住 **Shift** 奔跑。
- **互动**用 **E** 或 **F**：与面前的人交谈、打开宝箱、阅读告示牌。
- **走上**门口或地图边缘即可前往下一张地图。
- 在某些地方（例如森林）行走时可能触发**随机战斗**。
- 战斗结束后，你会回到原地图的原格子。
- 游戏会保存你站立的位置；读档后回到原地。

可以互动的格子会被框起来，左下角的提示随之变为 **E / F · Interact**。

## 示例：保持开启的宝箱

村长家的宝箱是“宝箱”对象的一次放置，带两个状态。在剧情旗标 `rpg_house_chest` 未设置时显示关闭的宝箱；它的剧本给出三枚硬币并设置该旗标。之后显示打开的状态。

![之前：正午，长者之家中主角站在关闭的宝箱旁。](assets/chest-before.jpg)

![宝箱的文字：旧袜子、一张写着"别碰"的纸条，以及压在下面的三枚硬币。](assets/chest-text.jpg)

![之后：同一格子现在显示打开的宝箱，并被互动标记框住；左下角提示为"E / F · Interact"。](assets/chest-after.jpg)

```json
{"Id": "rpg_elder_house/chest", "Object": "village_pack/chest", "X": 9, "Y": 7,
 "Interact": {"Scenario": "rpg_plus/events/house_chest", "Commands": null},
 "States$add": [{"Id": "open", "When": "rpg_house_chest == 1",
                 "Interact": {"Scenario": "rpg_plus/events/chest_empty", "Commands": null}}]}
```

```scenario
ptext "箱子里：旧袜子和一张写着"别碰"的纸条。太迟了。袜子下面有三枚硬币。" key=rpg_plus/chest/house
action item.add id=money/coin count=3
flag rpg_house_chest 1
end
```

## 给作者：地图

地图是模组 `maps/` 文件夹里的一个 JSON 文件。它只包含地面：站在地面上的东西（树、房屋、桶、人）通过放置来摆放。`scenes/` 中同名的场景提供战斗背景和地图环境音。

- **Legend**：每种地表或栅栏对应一个字符，例如 `"g": "village_pack/ground/grass"`。`.` 表示空格子。
- **Ground** 和 **Structure**：按格子排列的字符行：地表（草地、水、地板），以及栅栏或墙。
- **Collision**：`#`（不可通行）、`o`（强制可通行，例如墙上的缺口）、`.`（由对象决定）组成的行。
- **Spawn**、**Zoom**、**Ambient**（室内固定光色）、**Atmosphere**（`woodland`、`winter_snow`、`desert_dust`、`rain`）、**Encounters**（`Steps` 和 `Groups`）:

```json
"Encounters": { "Steps": 48, "Groups": ["rpg_slime", "rpg_slime,rpg_slime"] }
```

## 给作者：对象与放置

对象在 `objects/*.json`（`rpg_object`）中定义。放置把一个对象摆到一张地图上：`placements/<地图>/*.json`（`rpg_placement`），包含 `Id`（`<地图>/<名称>`）、`Object`、`X`、`Y`，以及只改变这一个实例的字段。

- **Form**：`surface`（地表）、`segment`（一段栅栏，会与相邻段连接）、`sprite`（覆盖一片格子的图：树、房屋、桶、门）、`actor`（带行走图的人物）。
- **Solid**：是否阻挡通行。sprite 默认阻挡；触发器需要 `"Solid": false`。
- **Interact** — `Trigger` 为 `Use`（面向物件按 E/F）或 `Step`（进入区域）。可设置 `Scenario`、`Commands` 或两者：内联命令在脚本后执行，即使脚本以 `end` 结束。`Teleport`（`{Map, X, Y, Dir}`）独立执行并跳过对话命令。
- **States**：`When`（条件）和改变的内容：`Hidden`、另一张图、`Interact`、`Route`。最后一个 `When` 成立的状态生效。在放置中，`self.名称` 指该放置自己的旗标 `<Id>:名称`。
- **Route**、**Light**、**Lights**：与以前相同（`Steps`、`Every`；光源有 `Radius`、`Color` 以及相对对象的格子位置 `At`）。
- **Extends** 与 **Variants**：继承另一个对象，并提供不同版本（例如冬季版）。
- **在其他模组的地图上添加**：放一个使用自己 `Id` 的放置。要替换别人的，用相同 `Id` 并对所有者声明 `depends`；要删除，用 `"$delete": true` 的补丁。
- **主角外观**：队伍队长的 `Charset`（例如 `village/hero`）。

## 剧本动作

```scenario
action rpg.battle enemies=rpg_wolf,rpg_slime win_flag=rpg_wolf_dead
action rpg.teleport map=rpg_forest x=1 y=8 dir=right
```

- `rpg.battle` 用游戏的战斗核心开启一场战斗，并回到同一格；胜利时 `win_flag` 被置为 1，于是地图上条件检查该旗标的怪物就会消失。这个动作之后应结束剧本。
- `rpg.teleport` 把玩家移动到本地图或另一张地图的某格。

## 游戏设置

- **地图投影**（`tile.renderer`）：俯视或等距视角，见 Tile Rendering。

## 如果遇到问题

1. **人物或宝箱没有反应。** 面对它并按 E 或 F。`Step` 对象在你踏上时才会反应。
2. **怪物或宝箱又出现了。** 隐藏或改变它的状态的 `When` 必须检查其剧本或 `win_flag` 设置的旗标。
3. **无法通过看似开放的地方。** 实体对象（`Solid`）或地图碰撞层中的 `#` 挡住了格子。
4. **战斗没有背景。** 地图场景 `scenes/<map>.json` 需要 `defaultBackground`。

## 地图事件与可选战斗

不可见区域直接保存在地图的 `Events` 中：局部 `Id`、`X`、`Y`、`Width`、`Height`、`Trigger`、`When`、`Solid`，以及 `Scenario` 加 `Commands`，或独立 `Teleport`。无需对象或放置。`self.done` 属于 `<map>/<event>:done`，请保持 ID 稳定。在编辑器选择 Event，拖出矩形，输入命令，点击 Apply event 和 Save。

世界本身不依赖战斗。只有同时加载 `turn_battle_base` 时，`rpg__battles` 才提供 `rpg.battle` 和随机战斗。队员跟随队长；可选的 `npc_barks_base` 提供并发背景对话。
