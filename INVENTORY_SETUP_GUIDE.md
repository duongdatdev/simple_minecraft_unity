# INVENTORY SYSTEM - HƯỚNG DẪN CÀI ĐẶT CHI TIẾT

Tài liệu này hướng dẫn từng bước để thiết lập hệ thống Inventory, đảm bảo bạn kéo thả đúng các component và file vào đúng chỗ.

---

## 📦 TỔNG QUAN CÁC SCRIPT CẦN DÙNG

| Script | Vị trí gắn (GameObject) | Chức năng |
|--------|-------------------------|-----------|
| `ItemDatabase.cs` | GameObject `ItemDatabase` | Chứa dữ liệu tất cả item và icon |
| `Inventory.cs` | GameObject `Player` | Quản lý dữ liệu túi đồ của người chơi |
| `BlockInteraction.cs` | GameObject `Main Camera` | Xử lý đập/đặt block và liên kết với inventory |
| `InventoryUI.cs` | GameObject `Canvas` | Quản lý hiển thị UI túi đồ |
| `InventorySlotUI.cs` | Prefab `InventorySlot` | Quản lý hiển thị từng ô chứa đồ |

---

## 🛠️ BƯỚC 1: TẠO ITEM DATABASE

1. **Tạo GameObject**:
   - Trong Hierarchy, chuột phải → Create Empty.
   - Đặt tên: `ItemDatabase`.

2. **Gắn Script**:
   - Chọn `ItemDatabase`.
   - Kéo script `Assets/Scripts/Core/ItemDatabase.cs` vào Inspector.

3. **Cấu hình (Inspector)**:
   - Script sẽ có rất nhiều trường `Sprite` (Icon) như `Grass Block Icon`, `Dirt Block Icon`, `Wooden Pickaxe Icon`...
   - **Hành động**: Kéo các Sprite (ảnh 2D) tương ứng vào các ô này.
   - *Lưu ý*: Nếu chưa có Sprite, bạn có thể để trống (None), nhưng trong game icon sẽ không hiện hoặc hiện ô trắng.

---

## 🎮 BƯỚC 2: SETUP PLAYER

### A. Gắn Inventory cho Player

1. **Chọn Player**:
   - Tìm GameObject `Player` trong Hierarchy.

2. **Gắn Script**:
   - Kéo script `Assets/Scripts/Core/Inventory.cs` vào Inspector của Player.

3. **Cấu hình**:
   - `Selected Hotbar Slot`: Để mặc định là `0`.

### B. Setup Block Interaction (Trên Camera)

1. **Chọn Main Camera**:
   - Tìm `Main Camera` (thường là con của `Player`).

2. **Gắn Script**:
   - Kéo script `Assets/Scripts/Player/BlockInteraction.cs` vào Inspector của Main Camera.

3. **Cấu hình (Quan trọng)**:
   - **Player Camera**: Kéo chính `Main Camera` vào.
   - **Player Inventory**: Kéo GameObject `Player` (nơi chứa script Inventory) vào.
   - **Inventory UI**: Để trống tạm thời (sẽ quay lại gắn sau khi tạo UI ở Bước 3).

---

## 🖼️ BƯỚC 3: TẠO UI VÀ PREFAB

### ⚠️ LƯU Ý QUAN TRỌNG VỀ HÌNH ẢNH (SPRITE)
Trước khi làm UI, hãy đảm bảo các ảnh (Icon item, khung Hotbar) đã được cài đặt đúng:
1. Chọn file ảnh trong Project window.
2. Trong Inspector, chỉnh **Texture Type** thành `Sprite (2D and UI)`.
3. Nhấn **Apply** ở cuối Inspector.
4. Nếu ảnh bị mờ hoặc đen, kiểm tra `Color` của Image component phải là màu Trắng (255,255,255,255).

### A. Tạo Canvas & Panel Cơ Bản

1. **Tạo Canvas**: 
   - Hierarchy → UI → Canvas.
   - Trong Inspector của **Canvas Scaler**:
     - `UI Scale Mode`: Chọn **Scale With Screen Size**.
     - `Reference Resolution`: Đặt **1920 x 1080**.

2. **Tạo Hotbar Panel (Thanh dưới cùng)**:
   - Trong Canvas, tạo **Image** (Chuột phải → UI → Image) thay vì Panel, đặt tên `HotbarPanel`.
   - **Hình ảnh**:
     - Kéo sprite "Thanh Hotbar 9 ô" của bạn vào ô `Source Image`.
     - Nhấn nút **Set Native Size** trong Inspector để ảnh về đúng tỉ lệ chuẩn.
   - **Vị trí (Rect Transform)**:
     - Anchor Presets: Giữ `Alt` + `Shift` chọn **Bottom Center**.
     - `Pos Y`: 10.
   - **Cấu trúc con (Chứa Item)**:
     - Tạo Empty Object con bên trong `HotbarPanel`, đặt tên `HotbarSlots`.
     - Anchor Presets: Giữ `Alt` + `Shift` chọn **Stretch / Stretch** (lấp đầy cha).
   - **Layout (Gắn vào HotbarSlots)**:
     - Chọn object `HotbarSlots`.
     - Add Component `Horizontal Layout Group`.
     - `Child Alignment`: **Middle Center**.
     - `Control Child Size`: **Bỏ chọn** (Uncheck) cả Width và Height.
     - `Child Force Expand`: **Bỏ chọn** (Uncheck) cả Width và Height.
     - **QUAN TRỌNG - CĂN CHỈNH VỊ TRÍ (FIX LỆCH)**:
       - Vì hình nền của bạn có viền, các slot item đang bị lệch so với ô vẽ sẵn.
       - `Padding`: Hãy tăng số `Left` (ví dụ: 5) để đẩy toàn bộ hàng slot sang phải, hoặc `Top`/`Bottom` để đẩy xuống/lên.
       - `Spacing`: Chỉnh khoảng cách giữa các slot (ví dụ: 3 hoặc 4) để chúng giãn ra khớp với hình nền.
       - **Mẹo**: Hãy nhấn **Play**, sau đó chọn `HotbarSlots` và chỉnh trực tiếp các số `Padding` / `Spacing` này trong khi nhìn màn hình Game. Khi thấy khớp, hãy nhớ các con số đó, tắt Play và điền lại.

3. **Tạo Main Inventory Panel (Túi đồ chính)**:
   - Trong Canvas, tạo Panel tên `MainInventoryPanel`.
   - **Vị trí (Rect Transform)**:
     - Anchor Presets: Giữ `Alt` + `Shift` chọn **Center Middle**.
     - `Width`: 600.
     - `Height`: 300.
   - **Hình ảnh**: Chọn màu nền tối (đen mờ) hoặc ảnh khung túi đồ.
   - **Cấu trúc con**:
     - Tạo Empty Object con bên trong `MainInventoryPanel`, đặt tên `MainInventorySlots`.
     - Set Anchor của `MainInventorySlots` là **Stretch / Stretch**.
   - **Layout (Gắn vào MainInventorySlots)**:
     - Chọn object `MainInventorySlots`.
     - Add Component `Grid Layout Group`.
     - `Padding`: Chỉnh 20 cho cả 4 phía.
     - `Cell Size`: X = 50, Y = 50.
     - `Spacing`: X = 10, Y = 10.
     - `Constraint`: Chọn **Fixed Column Count**.
     - `Constraint Count`: Nhập **9**.

### B. Tạo & Cấu Hình Prefab "InventorySlot"

Đây là bước quan trọng nhất để hiển thị đúng item.

1. **Tạo Slot UI**:
   - Tạo 1 Image trong Canvas, đặt tên `InventorySlot`.
   - **Kích thước (Rect Transform)**: Đặt `Width` = 40, `Height` = 40 (hoặc thử 36..44 tùy ô trên background của bạn).
   - **Anchor / Pivot**: Anchor = Center, Pivot = (0.5, 0.5).
   - **Scale**: X, Y = 1 (reset scale để tránh ảnh hưởng sizing).
   - **Màu nền**: Vì bạn đã có hình nền Hotbar, đặt `Color` của Image thành **Transparent** (Alpha = 0) hoặc tắt component Image để không che mất background.

   - **Thêm LayoutElement (rất quan trọng)**:
     - Trên `InventorySlot` Add Component → `Layout Element`.
     - Bật `Preferred Width` và `Preferred Height`, nhập cùng giá trị với RectTransform (ví dụ 40 và 40).
     - (Tùy chọn) có thể đặt `Min Width/Height` và `Max` nếu muốn giới hạn.
     - Lý do: `LayoutElement` bắt buộc để `Horizontal Layout Group` tôn trọng kích thước cố định của từng slot.

   - **Tạo các object con bên trong `InventorySlot`**:
     - `Icon` (Image) - Đặt `RectTransform` full stretch (Anchor stretch) với `Padding` nhỏ, bật `Preserve Aspect` trên Image nếu muốn giữ tỉ lệ.
     - `CountText` (TextMeshPro - Text) - Đặt góc dưới phải, `Font Size` nhỏ (ví dụ 18).
     - `DurabilityBar` (Image) - Đặt ở dưới cùng, Type = Filled.
     - `SelectionHighlight` (Image) - Là một Image con (viền). Kích thước hơi lớn hơn `InventorySlot` (Padding -2) hoặc dùng Outline.
     - `Background` (Image) - Nếu dùng, đặt Alpha = 0 hoặc disable để không che background hotbar.

   - **Kiểm tra và hiệu chỉnh kích thước (tại Play Mode)**:
     - Nhấn **Play**.
     - Chọn `HotbarSlots` → Trong `Horizontal Layout Group`: tinh chỉnh `Padding.Left` / `Padding.Top` và `Spacing` cho đến khi các slot khớp với ô trên background.
     - Nếu slot quá to/nhỏ: chọn prefab `InventorySlot` và thay `Preferred Size` của `Layout Element` (ví dụ 36 → 40 → 44) để thử cho khớp.
     - **Ghi chú**: Bạn có thể chỉnh trực tiếp trong Play để tìm giá trị khớp rồi dừng Play và copy giá trị đó sang prefab trong Editor.

   - **Aspect / Icon fit**:
     - Trên `Icon` image: bật `Preserve Aspect` và chọn `Set Native Size` nếu muốn.
     - Hoặc dùng `Mask` + `Icon` (child) để đảm bảo icon không tràn ra ngoài ô.

2. **Gắn Script InventorySlotUI**:
   - Chọn object `InventorySlot`.
   - Kéo script `Assets/Scripts/UI/InventorySlotUI.cs` vào.

3. **Kéo Thả Reference (Rất Quan Trọng)**:
   - Tại component `Inventory Slot UI` trong Inspector, kéo các object con vào các ô tương ứng:
     - **Icon Image** ➔ Kéo object `Icon`.
     - **Count Text** ➔ Kéo object `CountText`.
     - **Durability Bar** ➔ Kéo object `DurabilityBar`.
     - **Selection Highlight** ➔ Kéo object `SelectionHighlight`.
     - **Background Image** ➔ Kéo object `Background` (hoặc chính `InventorySlot`).

4. **Tạo Prefab**:
   - Kéo object `InventorySlot` từ Hierarchy vào thư mục `Assets/Prefabs`.
   - Xóa `InventorySlot` khỏi Hierarchy.

### C. Setup InventoryUI trên Canvas

1. **Chọn Canvas**:
   - Click vào GameObject `Canvas`.

2. **Gắn Script**:
   - Kéo script `Assets/Scripts/UI/InventoryUI.cs` vào.

3. **Cấu hình Reference (Kéo thả đầy đủ)**:
   - **Player Inventory**: Kéo GameObject `Player` vào.
   - **Hotbar Panel**: Kéo GameObject `HotbarPanel` vào.
   - **Main Inventory Panel**: Kéo GameObject `MainInventoryPanel` vào.
   - **Inventory Slot Prefab**: Kéo Prefab `InventorySlot` (từ thư mục Project/Prefabs) vào.
   - **Hotbar Slots Parent**: Kéo GameObject `HotbarSlots` (trong Hierarchy) vào.
   - **Main Inventory Slots Parent**: Kéo GameObject `MainInventorySlots` (trong Hierarchy) vào.

4. **Settings Khác**:
   - **Toggle Inventory Key**: Chọn `E`.
   - **Inventory Rows**: `3`.
   - **Inventory Columns**: `9`.

---

## 🔄 BƯỚC 4: HOÀN TẤT LIÊN KẾT

1. **Quay lại BlockInteraction**:
   - Chọn `Main Camera`.
   - Tại component `Block Interaction`, ô **Inventory UI**: Kéo GameObject `Canvas` (nơi chứa script InventoryUI) vào.

---

## ✅ CHECKLIST KIỂM TRA CUỐI CÙNG

Trước khi nhấn Play, hãy kiểm tra:

1. [ ] **ItemDatabase** đã nằm trong scene và (tùy chọn) đã có icon.
2. [ ] **Player** đã có script `Inventory`.
3. [ ] **Main Camera** đã có script `BlockInteraction` và đã link tới Player + Canvas.
4. [ ] **Canvas** đã có script `InventoryUI` và đã link tới Prefab + các Panel.
5. [ ] **Prefab InventorySlot** đã được gán đủ Icon, Text, Bar vào script `InventorySlotUI`.

## 🐞 DEBUG NHANH

Nếu gặp lỗi **NullReferenceException**:
- Kiểm tra xem đã kéo Prefab vào `InventoryUI` chưa.
- Kiểm tra xem trong Prefab `InventorySlot`, các ô Icon/Text đã được kéo vào script chưa.
- Kiểm tra xem `BlockInteraction` đã có reference tới `InventoryUI` chưa.

Chúc bạn thành công!
