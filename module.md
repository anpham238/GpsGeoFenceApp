# 🛠️ 18. Đặc Tả Module: Tối ưu Hệ thống (Tìm kiếm, Quét QR & CMS)

![Status](https://img.shields.io/badge/Status-Optimizing-orange)
![Tech](https://img.shields.io/badge/Tech-MAUI_%7C_SignalR_%7C_ZXing-512BD4)
![UX](https://img.shields.io/badge/UX-Real--time_%26_Seamless-brightgreen)

> **Mục tiêu:** Nâng cấp các chức năng tương tác cốt lõi của ứng dụng để mang lại trải nghiệm mượt mà, tốc độ cao. Đồng thời, tái cấu trúc Web Admin để sát với dữ liệu thực tế hơn và đưa khả năng giám sát thiết bị lên mức thời gian thực (Real-time).

---

## 🔍 1. Tối Ưu: Thanh Tìm Kiếm POI (Smart Search)

Thay thế chức năng Dropdown/Tour cũ bằng một thanh tìm kiếm thông minh nổi trên bản đồ, mô phỏng trải nghiệm của Google Maps.

### 1.1. Luồng Trải nghiệm (UX Flow)
* **Gợi ý ngay khi gõ (Auto-suggest):** Khi người dùng chạm vào thanh Search và bắt đầu gõ ký tự, một danh sách thả xuống (Dropdown List) xuất hiện ngay lập tức, hiển thị các POI có tên khớp với từ khóa.
* **Tương tác mượt mà:** * Khi người dùng nhấn chọn 1 địa điểm từ danh sách gợi ý, bàn phím tự động ẩn đi.
    * Hệ thống kích hoạt Animation: Camera của bản đồ **tự động lướt (Pan) và phóng to (Zoom)** ngay vào tọa độ `Latitude`, `Longitude` của điểm POI đó.
    * Ngay khi bản đồ dừng lại, **Bottom Sheet** chứa thông tin chi tiết của POI tự động trượt lên.

### 1.2. Yêu cầu Kỹ thuật (.NET MAUI)
* **Control:** Sử dụng `SearchBar` kết hợp với `CollectionView` (được ẩn/hiện động) đặt trong thẻ `Grid` hoặc `AbsoluteLayout` nằm đè lên `Map`.
* **Debounce Search:** Cần cài đặt độ trễ (delay khoảng 300ms - 500ms) khi gõ phím trước khi gọi API `GET /api/v1/pois/search?q=...` để tránh spam truy vấn lên Server.

---

## 📷 2. Tối Ưu: Trải Nghiệm Quét QR Nâng Cao

Module quét mã QR được đập đi xây lại để khắc phục tình trạng giật lag và gián đoạn trải nghiệm.

### 2.1. Thiết kế Giao diện (UI Redesign)
* **Camera tràn viền:** Camera view chiếm toàn bộ màn hình. Lớp phủ (Overlay) màu đen mờ với một khung vuông trong suốt ở giữa có hiệu ứng vạch quét (Scan line) di chuyển lên xuống.
* **Bộ đếm lượt (Quota Indicator):** Nổi bật ở góc dưới màn hình:
    * *Tài khoản FREE:* Hiện Badge nhắc nhở (VD: `⏱️ Còn 3/5 lượt quét hôm nay`). Lấy dữ liệu từ bảng `DailyUsageTracking`.
    * *Tài khoản PRO:* Hiện Badge rực rỡ (VD: `🌟 PRO: Quét không giới hạn`).

### 2.2. Trải nghiệm "Không gián đoạn" (Seamless Flow)
* **Hành vi cũ:** Quét xong $\rightarrow$ Out về màn hình Bản đồ $\rightarrow$ Mở Bottom Sheet. (Gây rối mắt).
* **Hành vi mới:** Quét thành công $\rightarrow$ Giữ nguyên màn hình quét (hoặc làm mờ background camera) $\rightarrow$ **Bottom Sheet / Modal chứa trình phát Audio hiện lên đè trực tiếp lên trang QR**. Nghe xong, vuốt đóng Modal là có thể quét tiếp mã khác ngay lập tức.

### 2.3. Tăng tốc Camera (Performance)
* Đổi thư viện quét (Nếu đang dùng mặc định): Chuyển sang sử dụng thư viện hiệu năng cao như **`ZXing.Net.Maui`** hoặc **`Camera.MAUI`**.
* Tinh chỉnh tham số: Tăng độ phân giải quét, thiết lập Focus tự động liên tục (Continuous AutoFocus) và chỉ quét các định dạng chuẩn (QR_CODE) để giảm tải CPU.

---

## 💻 3. Tối Ưu: Tinh gọn Web Admin (CMS)

### 3.1. Dọn dẹp Menu
* ❌ **Bỏ Trang Dịch tự động:** Do cấu trúc đa ngôn ngữ đã thay đổi (Text, Podcast, Audio phân tách rõ ràng), việc dùng API dịch tự động không còn phù hợp.
* ❌ **Bỏ Trang Quản lý Tour:** Tập trung hệ thống vào việc khám phá POI tự do trên bản đồ thay vì gò bó theo Tour.

### 3.2. Cập nhật Trang "Thêm/Sửa POI"
Form quản lý POI được mở rộng để mapping chính xác với cấu trúc SQL Server (`GpsApp_Redesigned.sql`).

* **Thông tin Cơ bản (`Pois`):** Tên, Mô tả, Bán kính, Tọa độ, Thời gian Cooldown.
* **Quản lý Hình ảnh (`PoiImages`):** Khu vực Kéo-thả (Drag & Drop) cho phép **Upload nhiều ảnh cùng lúc**. Hỗ trợ kéo để sắp xếp thứ tự ảnh (Ảnh đầu tiên làm Cover).
* **Nội dung Đa ngôn ngữ (`PoiLanguage`):** Giao diện dạng Tab (Tab Tiếng Việt, Tab Tiếng Anh...). Mỗi Tab gồm:
    * Khung nhập Text ngắn (TTS - Dành cho FREE).
    * Khung nhập Podcast Script dài (Dành cho PRO).
    * Upload file Audio MP3 chuyên nghiệp (Dành cho PRO).
* **Liên kết phụ (`PoiMedia`):** Khung nhập MapLink ngoài.

---

## 📡 4. Đột Phá: Quản lý Thiết bị Real-time (SignalR)

**Vấn đề cũ:** Dùng Polling 5 phút/lần khiến Admin thấy độ trễ rất lớn. Thiết bị tắt App rồi nhưng Admin vẫn thấy Online.
**Giải pháp mới:** Ứng dụng công nghệ WebSockets (**SignalR**) trong .NET 10.

### 4.1. Cơ chế hoạt động của SignalR
* **Khi Mobile App mở lên:** * App kết nối với `DeviceHub` qua SignalR.
    * Server bắt sự kiện `OnConnectedAsync` $\rightarrow$ Update DB `IsActive = 1` $\rightarrow$ Đẩy tín hiệu (Broadcast) "Có 1 máy vừa Online" sang cho màn hình Web Admin ngay lập tức (Độ trễ < 1s).
* **Khi Mobile App tắt / Đứt mạng / Force Close:**
    * Kết nối WebSocket bị ngắt.
    * Server bắt sự kiện `OnDisconnectedAsync` $\rightarrow$ Update DB `IsActive = 0` $\rightarrow$ Báo cho Web Admin giảm số lượng Online xuống ngay lập tức.

### 4.2. Sơ đồ Hoạt động (Sequence Diagram - SignalR Realtime)

```mermaid
sequenceDiagram
    participant App as 📱 Mobile App
    participant Hub as ⚡ SignalR Hub (.NET)
    participant DB as 🗄️ SQL Server
    participant Admin as 💻 Web Admin (UI)

    Note over App, Admin: Khi người dùng mở App
    App->>Hub: Connect WebSocket (DeviceId)
    activate Hub
    Hub->>DB: UPDATE GuestDevices SET LastActiveAt = NOW
    Hub-->>Admin: Bắn Event "DeviceConnected(DeviceId)"
    Admin->>Admin: Tăng số "Đang Online" lên 1 (+ Hiệu ứng xanh)
    
    Note over App, Admin: Khi người dùng đang dùng App
    App->>Hub: SendLocation(Lat, Lng) mỗi 3 phút
    Hub->>DB: Cập nhật Tọa độ mới
    
    Note over App, Admin: Khi người dùng vuốt tắt App (Hoặc mất mạng)
    App-xHub: Mất kết nối (Disconnect)
    Hub->>DB: Log Offline
    Hub-->>Admin: Bắn Event "DeviceDisconnected(DeviceId)"
    deactivate Hub
    Admin->>Admin: Giảm số "Đang Online" xuống 1 (+ Hiệu ứng xám)