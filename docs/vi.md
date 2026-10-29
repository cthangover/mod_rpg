# RPG World — đi lại trên bản đồ ô, trò chuyện, mở rương, chiến đấu

RPG World biến trò chơi thành RPG bản đồ ô theo tinh thần các game nhập vai 2D cổ điển: bạn đi trên lưới ô bằng bàn phím, nói chuyện với mọi người, mở rương, bước qua cửa và chiến đấu với quái vật, rồi trở về đúng chỗ cũ. Bản đồ vẫn là một cảnh bình thường của trò chơi, nên hộp hội thoại, kịch bản, lưu game, nhạc và thời gian trong ngày đều hoạt động trên bản đồ như trong tiểu thuyết thị giác.

Chính nó vận hành làng, rừng và sa mạc của **RPG Plus**. Bản đồ và nhân vật đến từ các mod nội dung như RPG Demo; còn mod này cung cấp luật chơi.

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

Chiếc rương trong nhà trưởng lão là hai sự kiện trên cùng một ô. Rương đóng hiện ra khi cờ cốt truyện `rpg_house_chest` chưa được đặt; kịch bản của nó trao ba đồng xu và đặt cờ. Từ đó thay vào đó là chiếc rương mở.

![Trước: nhân vật chính đứng cạnh rương đóng trong nhà trưởng lão lúc buổi trưa.](assets/chest-before.jpg)

![Dòng chữ của rương: tất cũ, mẩu giấy "Đừng động vào", và ba đồng xu bên dưới.](assets/chest-text.jpg)

![Sau: ô cũ giờ hiển thị rương mở, được viền bởi dấu tương tác; gợi ý dưới trái hiện "E / F · Interact".](assets/chest-after.jpg)

```json
{ "Id": "chest", "X": 9, "Y": 7, "Tile": 14,
  "Scenario": "rpg_demo/events/house_chest",
  "Condition": "rpg_house_chest != 1" },
{ "Id": "chest_open", "X": 9, "Y": 7, "Tile": 15,
  "Scenario": "rpg_demo/events/chest_empty",
  "Condition": "rpg_house_chest == 1" }
```

```scenario
ptext "Trong rương: tất cũ và một mẩu giấy ghi "Đừng động vào". Muộn rồi. Dưới đống tất: ba đồng xu." key=rpg_demo/chest/house
action item.add id=money/coin count=3
flag rpg_house_chest 1
end
```

## Dành cho tác giả: bản đồ

Bản đồ là một tệp JSON trong thư mục `maps/` của mod, kèm một cảnh cùng tên trong `scenes/` làm nền chiến đấu và âm nền của bản đồ.

- **Ba lớp vẽ**, mỗi lớp là danh sách các hàng số ô (`.` là trống): `Back` — mặt đất, sàn, nước; `Main` — vật thể ngang tầm nhân vật: tường, thân cây, hàng rào; `Front` — vẽ đè lên nhân vật: tán cây, mái hiên, vòm cửa.
- **Va chạm** — lớp theo hàng, ghi đè khả năng đi qua của ô khi cần: bức tường vô hình ở mép vách hay chỗ hở trong hàng rào cây.
- **Tileset** — atlas ô và thuộc tính từng ô (có đặc hay không, nền, vật thể).
- **Spawn** — người mới xuất hiện ở đâu; **Zoom**; màu **Ambient** tùy chọn cho nội thất (nếu không, thời gian trong ngày sẽ chiếu sáng bản đồ) và **Atmosphere** (`woodland` thêm lá, đom đóm và gợn nước).
- **Encounters** — chiến đấu ngẫu nhiên: trung bình một lần mỗi `Steps` bước, gặp một trong các `Groups`.

```json
"Encounters": { "Steps": 48, "Groups": ["rpg_slime", "rpg_slime,rpg_slime"] }
```

## Dành cho tác giả: sự kiện

Sự kiện nằm trong `map_events/<id bản đồ>/*.json` của bất kỳ mod nào. Một sự kiện có **diện mạo** và **tác dụng** được chọn riêng biệt:

- diện mạo: `Sprite` nhân vật, một ô `Tile` từ atlas, `Icon` vật phẩm nằm trên đất, hoặc không gì cả (bộ kích hoạt vô hình);
- tác dụng: `Teleport` tới một ô của bản đồ này hoặc bản đồ khác, hoặc `Scenario` kèm `Commands` viết ngay tại chỗ (tùy chọn) — ngôn ngữ kịch bản đầy đủ, kể cả `action rpg.battle`, cờ và nhiệm vụ;
- `Trigger`: `Action` (E/F khi đang đối mặt) hoặc `Touch` (bước lên);
- `Solid`: có chặn đường hay không (mặc định, sự kiện được vẽ ra sẽ chặn);
- `Condition`: ẩn sự kiện khi điều kiện không thỏa;
- `Pages`: các diện mạo và tác dụng thay thế, mỗi cái một điều kiện; dùng trang cuối cùng có điều kiện thỏa;
- `Route`: di chuyển tự động như `{"Steps": ["left", "wait", "right"], "Every": 0.8}`, với `up`/`down`/`left`/`right`, `random`, `toward`/`away` (về phía hoặc xa khỏi người chơi), `wait` và `face_…`;
- `Light`: quầng sáng quanh sự kiện, `{"Radius": 3, "Color": "#ffd8a0"}`, nhìn thấy lúc chạng vạng và ban đêm.

**Thêm vào hoặc thay đổi bản đồ của mod khác.** Các tệp sự kiện được gộp theo thứ tự nạp: `Id` mới thêm sự kiện vào bản đồ, còn cùng `Id` từ một mod phụ thuộc vào chủ nhân sẽ thay thế nó. RPG Plus đã cho trưởng lão bản demo những hội thoại mới nhờ cách này.

## Hành động kịch bản

```scenario
action rpg.battle enemies=rpg_wolf,rpg_slime win_flag=rpg_wolf_dead
action rpg.teleport map=rpg_forest x=1 y=8 dir=right
```

- `rpg.battle` mở một trận chiến bằng lõi chiến đấu của trò chơi rồi trở về đúng ô cũ; khi thắng, `win_flag` được đặt thành 1, nhờ vậy quái vật trên bản đồ mà điều kiện của nó kiểm tra cờ này sẽ biến mất. Hãy kết thúc kịch bản sau hành động này.
- `rpg.teleport` đưa người chơi tới một ô trên bản đồ này hoặc bản đồ khác.

## Cài đặt trò chơi

- **Diện mạo** (`rpg.player_charset`) — sprite của nhân vật chính trên bản đồ, chọn từ các charset trong trò chơi.
- **Phép chiếu bản đồ** (`tile.renderer`) — nhìn từ trên hoặc đẳng cự; xem Tile Rendering.

## Khi có trục trặc

1. **Nhân vật hay rương không phản ứng.** Đối mặt và nhấn E hoặc F; sự kiện `Touch` chỉ phản ứng khi bạn bước lên.
2. **Quái vật hay rương quay lại.** `Condition` của nó phải kiểm tra đúng cờ mà kịch bản hoặc `win_flag` của nó đặt.
3. **Chỗ trông có vẻ trống lại không đi được.** Ô đó bị ô đặc hoặc lớp va chạm của bản đồ chặn.
4. **Không có nền trận chiến.** Cảnh của bản đồ trong `scenes/<map>.json` cần `defaultBackground`.
