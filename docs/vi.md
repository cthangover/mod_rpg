# RPG World — đi lại trên bản đồ ô, trò chuyện, mở rương, chiến đấu

RPG World biến trò chơi thành RPG bản đồ ô theo tinh thần các game nhập vai 2D cổ điển: bạn đi trên lưới ô bằng bàn phím, nói chuyện với mọi người, mở rương, bước qua cửa và chiến đấu với quái vật, rồi trở về đúng chỗ cũ. Bản đồ vẫn là một cảnh bình thường của trò chơi, nên hộp hội thoại, kịch bản, lưu game, nhạc và thời gian trong ngày đều hoạt động trên bản đồ như trong tiểu thuyết thị giác.

Chính nó vận hành làng, rừng và sa mạc của **RPG Plus**. Bản đồ và nhân vật đến từ các mod nội dung như `rpg_plus_game`; còn mod này cung cấp luật chơi.

![Đang khám phá làng trong RPG Plus: tên bản đồ ở trên trái, thanh công cụ ở trên phải, gợi ý điều khiển ở dưới trái.](assets/village.jpg)

## Chơi trên bản đồ như thế nào

- **Đi lại** bằng WASD hoặc phím mũi tên; giữ **Shift** để chạy.
- **Tương tác** bằng **E** hoặc **F**: nói chuyện với người đang đối mặt, mở rương, đọc biển hiệu.
- **Bước lên** cửa hoặc rìa bản đồ để sang bản đồ kế tiếp.
- Ở một số nơi, ví dụ khu rừng, **trận chiến ngẫu nhiên** có thể nổ ra khi bạn đi lại.
- Sau trận chiến bạn trở về đúng ô cũ của bản đồ cũ.
- Trò chơi lưu vị trí bạn đang đứng; tải lại là về đúng đó.

Ô có thể tương tác được viền khung, và gợi ý ở dưới trái đổi thành **E / F · Interact**.

## Ví dụ: chiếc rương vẫn mở

Rương trong nhà trưởng làng là một lần đặt vật thể «rương», có hai trạng thái. Khi cờ cốt truyện `rpg_house_chest` chưa được đặt, hiện rương đóng; kịch bản của nó đưa ba đồng xu và đặt cờ. Từ đó trở đi, trạng thái mở được hiển thị.

![Trước: nhân vật chính đứng cạnh rương đóng trong nhà trưởng lão lúc buổi trưa.](assets/chest-before.jpg)

![Dòng chữ của rương: tất cũ, mẩu giấy "Đừng động vào", và ba đồng xu bên dưới.](assets/chest-text.jpg)

![Sau: ô cũ giờ hiển thị rương mở, được viền bởi dấu tương tác; gợi ý dưới trái hiện "E / F · Interact".](assets/chest-after.jpg)

```json
{"Id": "rpg_elder_house/chest", "Object": "village_pack/chest", "X": 9, "Y": 7,
 "Interact": {"Scenario": "rpg_plus/events/house_chest", "Commands": null},
 "States$add": [{"Id": "open", "When": "rpg_house_chest == 1",
                 "Interact": {"Scenario": "rpg_plus/events/chest_empty", "Commands": null}}]}
```

```scenario
ptext "Trong rương: tất cũ và một mẩu giấy ghi "Đừng động vào". Muộn rồi. Dưới đống tất: ba đồng xu." key=rpg_plus/chest/house
action item.add id=money/coin count=3
flag rpg_house_chest 1
end
```

## Dành cho tác giả: bản đồ

Bản đồ là một tệp JSON trong thư mục `maps/` của mod. Bản đồ chỉ chứa mặt đất: những thứ đứng trên đó (cây, nhà, thùng, người) được đặt bằng các lần đặt (placement). Cảnh cùng tên trong `scenes/` cung cấp nền cho trận chiến và âm thanh môi trường của bản đồ.

- **Legend**: một ký tự cho mỗi loại mặt đất hoặc hàng rào, ví dụ `"g": "village_pack/ground/grass"`. `.` là ô trống.
- **Ground** và **Structure**: các dòng ký tự theo từng ô: mặt đất (cỏ, nước, sàn) và hàng rào hoặc tường.
- **Collision**: các dòng gồm `#` (không đi được), `o` (buộc cho đi được, ví dụ khe hở trên tường) và `.` (do vật thể quyết định).
- **Spawn**, **Zoom**, **Ambient** (màu sáng cố định cho nội thất), **Atmosphere** (`woodland`, `winter_snow`, `desert_dust`, `rain`), **Encounters** (`Steps` và `Groups`):

```json
"Encounters": { "Steps": 48, "Groups": ["rpg_slime", "rpg_slime,rpg_slime"] }
```

## Dành cho tác giả: vật thể và lần đặt

Vật thể được định nghĩa trong `objects/*.json` (`rpg_object`). Một lần đặt đưa một vật thể lên một bản đồ: tệp `placements/<bản đồ>/*.json` (`rpg_placement`) với `Id` dạng `<bản đồ>/<tên>`, `Object`, `X`, `Y` và các trường chỉ thay đổi riêng cá thể đó.

- **Form**: `surface` (mặt đất), `segment` (một đoạn hàng rào, nối với đoạn bên cạnh), `sprite` (hình vẽ trên một vùng ô: cây, nhà, thùng, cửa), `actor` (nhân vật có hình đi bộ).
- **Solid**: có chặn đường đi không. Sprite mặc định chặn; một trigger cần `"Solid": false`.
- **Interact** — `Trigger` là `Use` (đối diện và E/F) hoặc `Step` (khi vào). Dùng `Scenario`, `Commands` hoặc cả hai: lệnh chạy sau kịch bản, kể cả sau `end`. `Teleport` (`{Map, X, Y, Dir}`) chạy riêng và bỏ qua lệnh hội thoại.
- **States**: `When` (điều kiện) và phần thay đổi: `Hidden`, hình khác, `Interact`, `Route`. Trạng thái cuối cùng có `When` đúng sẽ được áp dụng. Trong một lần đặt, `self.tên` là cờ riêng của lần đặt đó: `<Id>:tên`.
- **Route**, **Light**, **Lights**: như trước (`Steps`, `Every`; ánh sáng có `Radius`, `Color` và vị trí `At` tính bằng ô so với vật thể).
- **Extends** và **Variants**: kế thừa từ vật thể khác và có các phiên bản (ví dụ bản mùa đông).
- **Thêm vào bản đồ của mod khác**: đặt một lần đặt với `Id` của riêng bạn. Để thay thế của người khác, dùng cùng `Id` và `depends` tới chủ sở hữu; để xóa, dùng bản vá có `"$delete": true`.
- **Diện mạo nhân vật chính**: `Charset` của đội trưởng (ví dụ `village/hero`).

## Hành động kịch bản

```scenario
action rpg.battle enemies=rpg_wolf,rpg_slime win_flag=rpg_wolf_dead
action rpg.teleport map=rpg_forest x=1 y=8 dir=right
```

- `rpg.battle` mở một trận chiến bằng lõi chiến đấu của trò chơi rồi trở về đúng ô cũ; khi thắng, `win_flag` được đặt thành 1, nhờ vậy quái vật trên bản đồ mà điều kiện của nó kiểm tra cờ này sẽ biến mất. Hãy kết thúc kịch bản sau hành động này.
- `rpg.teleport` đưa người chơi tới một ô trên bản đồ này hoặc bản đồ khác.

## Cài đặt trò chơi

- **Phép chiếu bản đồ** (`tile.renderer`): nhìn từ trên xuống hoặc đẳng cự; xem Tile Rendering.

## Khi có trục trặc

1. **Nhân vật hoặc rương không phản ứng.** Đứng đối diện và nhấn E hoặc F. Vật thể có `Step` phản ứng khi bạn bước lên nó.
2. **Quái vật hoặc rương quay lại.** `When` của trạng thái ẩn hoặc đổi nó phải kiểm tra cờ mà kịch bản hay `win_flag` đặt.
3. **Không đi được chỗ trông như mở.** Vật thể rắn (`Solid`) hoặc ký tự `#` trong lớp va chạm của bản đồ đang chặn ô đó.
4. **Không có nền trận chiến.** Cảnh của bản đồ `scenes/<map>.json` cần `defaultBackground`.

## Sự kiện thuộc bản đồ và chiến đấu tùy chọn

Vùng vô hình được lưu trực tiếp trong `Events` của bản đồ: `Id` cục bộ, `X`, `Y`, `Width`, `Height`, `Trigger`, `When`, `Solid`, cùng `Scenario` và `Commands` hoặc `Teleport` riêng. Không cần đối tượng hay vị trí đặt. `self.done` thuộc `<map>/<event>:done`; giữ ID ổn định. Trong trình sửa, chọn Event, kéo hình chữ nhật, nhập lệnh, Apply event rồi Save.

Thế giới không phụ thuộc chiến đấu. `rpg.battle` và giao chiến ngẫu nhiên chỉ có khi nạp cả `turn_battle_base`, qua `rpg__battles`. Thành viên đi theo thủ lĩnh; `npc_barks_base` tùy chọn thêm các hội thoại nền đồng thời.
