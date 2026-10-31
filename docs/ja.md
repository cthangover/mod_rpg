# RPG World — タイルマップを歩き、話し、開け、戦う

RPG World は、ゲームをクラシックな 2D RPG の趣のあるタイルマップ式 RPG に変えます。キーボードでマス目状のマップを歩き、人と話し、宝箱を開け、ドアをくぐり、モンスターと戦う——そして戦いのあとは元の場所へ戻ります。マップはゲームのごく普通のシーンなので、会話ウィンドウもシナリオもセーブも音楽も時間の流れも、ビジュアルノベルと同じようにマップ上で働きます。

**RPG Plus** の村、森、砂漠はこの仕組みで動いています。マップや登場人物そのものは `rpg_plus_game` のようなコンテンツ Mod が担い、この Mod がルールを提供します。

![RPG Plus で村を探索したところ。左上にマップ名、右上にツールバー、左下に操作ヒント。](assets/village.jpg)

## マップでの遊び方

- **移動**は WASD か矢印キー。**Shift** を押している間は走ります。
- **操作**は **E** または **F**。向いている人に話しかける、宝箱を開ける、看板を読む。
- **ドアやマップの端に歩いて乗ると**、次のマップへ移動します。
- 場所によっては、歩いているだけで**ランダムエンカウント**が起こります（森など）。
- 戦闘が終わると、同じマップの同じマスへ戻ります。
- ゲームは立っている位置をセーブします。ロードすればそこへ戻れます。

操作できるマスは枠で囲まれ、左下のヒントが **E / F · Interact** に変わります。

## 例：開いたままの宝箱

村長の家の宝箱は、「宝箱」オブジェクトの配置 1 つで、状態を 2 つ持ちます。ストーリーフラグ `rpg_house_chest` が立っていない間は閉じた宝箱が表示され、そのシナリオが硬貨 3 枚を渡してフラグを立てます。それ以降は開いた状態が表示されます。

![変更前：正午の長老の家。閉じた宝箱のそばに主人公が立っている。](assets/chest-before.jpg)

![宝箱の中身のテキスト：古い靴下、「さわるな」というメモ、そしてその下の硬貨 3 枚。](assets/chest-text.jpg)

![変更後：同じマスには開いた宝箱が表示され、インタラクションマーカーの枠が付く。左下のヒントは「E / F · Interact」。](assets/chest-after.jpg)

```json
{"Id": "rpg_elder_house/chest", "Object": "village_pack/container/chest", "X": 9, "Y": 7,
 "Interact": {"Scenario": "rpg_demo/events/house_chest", "Commands": null},
 "States$add": [{"Id": "open", "When": "rpg_house_chest == 1",
                 "Interact": {"Scenario": "rpg_demo/events/chest_empty", "Commands": null}}]}
```

```scenario
ptext "宝箱の中には古い靴下と「さわるな」と書かれたメモ。もう遅い。靴下の下には硬貨が 3 枚。" key=rpg_demo/chest/house
action item.add id=money/coin count=3
flag rpg_house_chest 1
end
```

## 作者向け：マップ

マップは Mod の `maps/` フォルダにある JSON ファイルです。地面だけを持ち、その上に立つもの（木、家、樽、人）は配置（placement）で置きます。`scenes/` の同名のシーンが、戦闘の背景とマップの環境音を担当します。

- **Legend**: 地面や柵の種類ごとに 1 文字（例: `"g": "village_pack/ground/grass"`）。`.` は空のセルです。
- **Ground** と **Structure**: セルごとの文字の行。地面（草、水、床）と柵や壁。
- **Collision**: `#`（通行不可）、`o`（強制的に通行可、壁の隙間など）、`.`（オブジェクトに任せる）の行。
- **Spawn**、**Zoom**、**Ambient**（屋内の固定光の色）、**Atmosphere**（`woodland`、`winter_snow`、`desert_dust`、`rain`）、**Encounters**（`Steps` と `Groups`）:

```json
"Encounters": { "Steps": 48, "Groups": ["rpg_slime", "rpg_slime,rpg_slime"] }
```

## 作者向け：オブジェクトと配置

オブジェクトは `objects/*.json`（`rpg_object`）で定義します。配置は 1 つのオブジェクトを 1 つのマップに置きます。`placements/<マップ>/*.json`（`rpg_placement`）に、`Id`（`<マップ>/<名前>`）、`Object`、`X`、`Y`、そしてその 1 つだけを変える項目を書きます。

- **Form**: `surface`（地面）、`segment`（柵の 1 区画。隣と繋がる）、`sprite`（セル範囲に描かれる絵：木、家、樽、扉）、`actor`（歩く絵を持つ人物）。
- **Solid**: 通行を止めるか。sprite は既定で止めます。トリガーには `"Solid": false` が必要です。
- **Interact**: `Trigger` は `Use`（向いて E または F）か `Step`（そのセルに入る）。続けて `Scenario`、`Commands`（シナリオ言語の行）、`Teleport`（`{Map, X, Y, Dir}`）のどれか。
- **States**: `When`（条件）と変化する内容: `Hidden`、別の絵、`Interact`、`Route`。`When` が成り立つ最後の状態が有効です。配置の中では `self.名前` がその配置自身のフラグ `<Id>:名前` を指します。
- **Route**、**Light**、**Lights**: 従来どおり（`Steps`、`Every`。光は `Radius`、`Color`、オブジェクトからのセル位置 `At`）。
- **Extends** と **Variants**: 他のオブジェクトを継承し、版（例: 冬の版）を持ちます。
- **他の Mod のマップに追加**: 自分の `Id` を持つ配置を置きます。相手のものを置き換えるには同じ `Id` と持ち主への `depends`。削除は `"$delete": true` のパッチです。
- **主人公の見た目**: パーティのリーダーのキャラクターの `Charset`（例: `village/hero`）。

## シナリオアクション

```scenario
action rpg.battle enemies=rpg_wolf,rpg_slime win_flag=rpg_wolf_dead
action rpg.teleport map=rpg_forest x=1 y=8 dir=right
```

- `rpg.battle` はゲームのバトルコアで戦闘を始め、同じマスへ戻します。勝つと `win_flag` が 1 になるので、そのフラグを条件で見ているマップ上のモンスターは消えます。このアクションのあとでシナリオを終えてください。
- `rpg.teleport` はプレイヤーを、このマップまたは別のマップのマスへ移動させます。

## ゲーム設定

- **マップの表示方式**（`tile.renderer`）: 真上か等角（アイソメ）。Tile Rendering を参照。

## うまくいかないとき

1. **人物や宝箱が反応しない。** 正面に立って E または F を押してください。`Step` のオブジェクトは踏むと反応します。
2. **敵や宝箱が戻ってくる。** それを隠すか変える状態の `When` が、シナリオや `win_flag` が立てるフラグを確かめている必要があります。
3. **開いて見える場所を通れない。** 固いオブジェクト（`Solid`）か、マップの衝突データの `#` がセルを塞いでいます。
4. **戦闘の背景がない。** マップのシーン `scenes/<map>.json` に `defaultBackground` が必要です。
