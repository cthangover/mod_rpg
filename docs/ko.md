# RPG World — 타일 맵을 걷고, 말하고, 열고, 싸우기

RPG World는 게임을 클래식 2D 롤플레이 게임 스타일의 타일 맵 RPG로 바꿉니다. 키보드로 격자 맵을 걷고, 사람들과 대화하고, 상자를 열고, 문을 지나가고, 몬스터와 싸운 뒤 제자리로 돌아옵니다. 맵은 게임의 평범한 씬이므로 대화창, 시나리오, 저장, 음악, 시간대가 비주얼 노벨에서처럼 맵 위에서 똑같이 작동합니다.

**RPG Plus**의 마을, 숲, 사막이 이 엔진으로 돌아갑니다. 맵과 사람 자체는 `rpg_plus_game` 같은 콘텐츠 모드에서 나오며, 이 모드는 규칙을 담당합니다.

![RPG Plus에서 마을 탐험: 왼쪽 위 맵 이름, 오른쪽 위 툴바, 왼쪽 아래 조작 힌트.](assets/village.jpg)

## 맵에서 플레이하기

- **이동**은 WASD 또는 방향키입니다. **Shift**를 누르고 있으면 달립니다.
- **상호 작용**은 **E** 또는 **F**: 마주 본 사람과 대화하기, 상자 열기, 표지판 읽기.
- 문이나 맵 가장자리로 **걸어 들어가면** 다음 맵으로 넘어갑니다.
- 일부 지역(예: 숲)에서는 걷다가 **랜덤 전투**가 시작될 수 있습니다.
- 전투가 끝나면 같은 맵의 같은 칸으로 돌아옵니다.
- 게임은 서 있는 위치를 저장하며, 불러오면 그 자리로 돌아갑니다.

상호 작용할 수 있는 칸에는 테두리가 표시되고, 왼쪽 아래 힌트가 **E / F · Interact**로 바뀝니다.

## 예시: 열린 채로 있는 상자

마을 노인 집의 상자는 “상자” 오브젝트의 배치 한 개이며 상태가 두 개입니다. 스토리 플래그 `rpg_house_chest`가 설정되지 않은 동안에는 닫힌 상자가 보이고, 그 시나리오가 동전 세 닢을 주고 플래그를 설정합니다. 그 뒤로는 열린 상태가 보입니다.

![이전: 정오의 장로의 집, 닫힌 상자 옆에 선 주인공.](assets/chest-before.jpg)

![상자의 문구: 낡은 양말, '만지지 마시오'라고 쓴 쪽지, 그 밑의 동전 세 닢.](assets/chest-text.jpg)

![이후: 같은 칸에 이제 열린 상자가 표시되고 상호 작용 마커 테두리가 둘러짐. 왼쪽 아래 힌트는 "E / F · Interact".](assets/chest-after.jpg)

```json
{"Id": "rpg_elder_house/chest", "Object": "village_pack/container/chest", "X": 9, "Y": 7,
 "Interact": {"Scenario": "rpg_demo/events/house_chest", "Commands": null},
 "States$add": [{"Id": "open", "When": "rpg_house_chest == 1",
                 "Interact": {"Scenario": "rpg_demo/events/chest_empty", "Commands": null}}]}
```

```scenario
ptext "상자 안에는 낡은 양말과 '만지지 마시오'라고 쓴 쪽지. 이미 늦었다. 양말 밑에 동전 세 닢." key=rpg_demo/chest/house
action item.add id=money/coin count=3
flag rpg_house_chest 1
end
```

## 작성자용: 맵

맵은 모드의 `maps/` 폴더에 있는 JSON 파일입니다. 맵에는 바닥만 있고, 그 위에 서는 것(나무, 집, 통, 사람)은 배치로 놓습니다. 같은 이름의 `scenes/` 씬이 전투 배경과 맵의 주변 음향을 담당합니다.

- **Legend**: 바닥이나 울타리 종류마다 글자 하나(예: `"g": "village_pack/ground/grass"`). `.`은 빈 칸입니다.
- **Ground**와 **Structure**: 칸마다 글자를 적은 행. 바닥(풀, 물, 마루)과 울타리 또는 벽.
- **Collision**: `#`(통과 불가), `o`(강제로 통과 가능, 예: 벽의 틈), `.`(오브젝트가 정함)의 행.
- **Spawn**, **Zoom**, **Ambient**(실내의 고정 빛 색), **Atmosphere**(`woodland`, `winter_snow`, `desert_dust`, `rain`), **Encounters**(`Steps`와 `Groups`):

```json
"Encounters": { "Steps": 48, "Groups": ["rpg_slime", "rpg_slime,rpg_slime"] }
```

## 작성자용: 오브젝트와 배치

오브젝트는 `objects/*.json`(`rpg_object`)에 정의합니다. 배치는 오브젝트 하나를 맵 하나에 놓습니다. `placements/<맵>/*.json`(`rpg_placement`)에 `Id`(`<맵>/<이름>`), `Object`, `X`, `Y`와 이 인스턴스만 바꾸는 항목을 씁니다.

- **Form**: `surface`(바닥), `segment`(울타리 한 칸, 이웃과 이어짐), `sprite`(칸 범위 위의 그림: 나무, 집, 통, 문), `actor`(걷는 그림이 있는 인물).
- **Solid**: 이동을 막는지. sprite는 기본으로 막습니다. 트리거에는 `"Solid": false`가 필요합니다.
- **Interact**: `Trigger`는 `Use`(마주 보고 E 또는 F)나 `Step`(칸에 들어감). 이어서 `Scenario`, `Commands`(시나리오 언어 줄), `Teleport`(`{Map, X, Y, Dir}`) 중 하나.
- **States**: `When`(조건)과 바뀌는 내용: `Hidden`, 다른 그림, `Interact`, `Route`. `When`이 참인 마지막 상태가 적용됩니다. 배치 안에서 `self.이름`은 그 배치 자신의 플래그 `<Id>:이름`입니다.
- **Route**, **Light**, **Lights**: 예전과 같습니다(`Steps`, `Every`; 빛은 `Radius`, `Color`, 오브젝트 기준 칸 위치 `At`).
- **Extends**와 **Variants**: 다른 오브젝트를 물려받고, 버전(예: 겨울)을 가집니다.
- **다른 모드의 맵에 추가**: 자신의 `Id`로 배치를 넣습니다. 남의 것을 바꾸려면 같은 `Id`와 주인에 대한 `depends`, 지우려면 `"$delete": true` 패치입니다.
- **주인공 모습**: 파티 리더 캐릭터의 `Charset`(예: `village/hero`).

## 시나리오 액션

```scenario
action rpg.battle enemies=rpg_wolf,rpg_slime win_flag=rpg_wolf_dead
action rpg.teleport map=rpg_forest x=1 y=8 dir=right
```

- `rpg.battle`은 게임의 전투 코어로 전투를 시작하고 같은 칸으로 돌아옵니다. 승리하면 `win_flag`가 1로 설정되어, 그 플래그를 조건으로 검사하는 맵의 몬스터는 사라집니다. 이 액션 뒤에는 시나리오를 끝내세요.
- `rpg.teleport`는 플레이어를 이 맵이나 다른 맵의 칸으로 옮깁니다.

## 게임 설정

- **맵 투영**(`tile.renderer`): 위에서 본 뷰 또는 아이소메트릭. Tile Rendering 참조.

## 문제가 생겼을 때

1. **인물이나 상자가 반응하지 않는다.** 마주 보고 E 또는 F를 누르세요. `Step` 오브젝트는 밟으면 반응합니다.
2. **적이나 상자가 되살아난다.** 그것을 숨기거나 바꾸는 상태의 `When`이 시나리오나 `win_flag`가 설정하는 플래그를 확인해야 합니다.
3. **열려 보이는 곳으로 갈 수 없다.** 단단한 오브젝트(`Solid`) 또는 맵 충돌의 `#`가 칸을 막고 있습니다.
4. **전투 배경이 없다.** 맵 씬 `scenes/<map>.json`에 `defaultBackground`가 필요합니다.
