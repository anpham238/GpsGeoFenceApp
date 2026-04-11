# 🌍 Thuyết Minh Du Lịch Tự Động (GPS & Geofence App)

![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?style=flat&logo=dotnet)
![MAUI](https://img.shields.io/badge/Framework-MAUI-blue?style=flat)
![C#](https://img.shields.io/badge/Language-C%23-239120?style=flat&logo=c-sharp)
![IDE](https://img.shields.io/badge/IDE-VS_2026-purple?style=flat&logo=visual-studio)
![License](https://img.shields.io/badge/license-MIT-green)

> Ứng dụng di động hướng dẫn viên du lịch tự động dựa trên vị trí người dùng. Hệ thống sử dụng công nghệ Geofencing để tự động phát nội dung thuyết minh (Audio/TTS) khi người dùng di chuyển đến các điểm tham quan (POI - Point of Interest).

## 🛠 Công nghệ sử dụng

* **Front-end (Mobile):** .NET MAUI (Android / iOS)
* **Ngôn ngữ & IDE:** C# 14, Visual Studio 2026, .NET 10
* **Database (Offline):** SQLite (Lưu trữ danh sách POI, log, audio cache để hoạt động không cần mạng)
* **Database (Online):** SQL Server (Quản lý qua SSMS) dùng cho hệ thống CMS đồng bộ dữ liệu.
* **Location Service:** Fused Location Provider Client (Android), Background Services.

---

## 🚀 Luồng hoạt động cốt lõi (Workflow)

1.  **Sync Data:** App tải danh sách POI từ SQL Server (Online) về SQLite (Offline) bao gồm `Lat/Lng`, bán kính, ưu tiên, nội dung thuyết minh.
2.  **Tracking:** Khi người dùng di chuyển, Background Service liên tục cập nhật vị trí.
3.  **Geofence Engine:** Xác định POI gần nhất/ưu tiên cao nhất trong bán kính 👉 Gửi Event Trigger.
4.  **Narrator Engine:** Kiểm tra trạng thái (Đang phát chưa? Đã phát trong X phút qua chưa?) 👉 Quyết định phát Audio/TTS.
5.  **Logging:** Ghi log vào SQLite (chống lặp, chống spam) và đồng bộ lên Server khi có Wifi.

---

## 📑 Tính năng nghiệp vụ chi tiết

### 1. GPS Tracking Real-time
* Theo dõi vị trí người dùng liên tục ở cả **Foreground** và **Background**.
* Tối ưu hóa tiêu thụ pin và độ chính xác của GPS.

### 2. Geofence & Kích hoạt tự động
* Thiết lập các POI với tọa độ, bán kính kích hoạt và mức độ ưu tiên.
* Tự động kích hoạt khi: Đi vào vùng (Enter) hoặc Đến gần điểm.
* **Chống spam:** Áp dụng thuật toán `Debounce` và `Cooldown`.

### 3. Thuyết minh tự động (Audio/TTS)
* **TTS (Text-to-Speech):** Linh hoạt, đa ngôn ngữ, dung lượng nhẹ.
* **Audio có sẵn:** Giọng đọc tự nhiên, chuyên nghiệp (cần quản lý tải file).
* **Quản lý hàng chờ (Queue):** Không phát trùng lặp, tự động dừng (pause/stop) khi có thông báo hệ thống khác.

### 4. Quản lý dữ liệu POI (Point of Interest)
* Mỗi POI bao gồm: Tên, mô tả văn bản, ảnh minh họa, link bản đồ, và file Audio / Script TTS.

### 5. Map View (Giao diện bản đồ)
* Hiển thị vị trí người dùng (User dot).
* Hiển thị marker cho tất cả POI.
* **Highlight** POI đang ở gần nhất.
* Popup xem chi tiết POI khi click.

### 6. Quét QR kích hoạt nội dung
* Sử dụng tại các trạm xe buýt/điểm cố định (Khánh Hội, Vĩnh Hội, Xóm Chiếu).
* Quét mã QR để nghe nội dung ngay lập tức mà **không cần phụ thuộc GPS**.

### 7. Hệ thống CMS (Web Admin)
* Quản lý danh sách POI, file Audio, bản dịch đa ngôn ngữ.
* Quản lý danh sách Tour du lịch.
* Xem lịch sử sử dụng của người dùng.

### 8. Phân tích dữ liệu (Analytics)
* Lưu vết tuyến đường di chuyển (ẩn danh).
* Thống kê: Top địa điểm được nghe nhiều nhất, thời gian trung bình dừng tại 1 POI.
* **Heatmap:** Bản đồ nhiệt vị trí người dùng.

---

## 🎯 Gap Analysis (Đánh giá tiến độ dự án hiện tại)
Dựa trên Repository [GpsGeoFenceApp](https://github.com/LuDaddy1509/GpsGeoFenceApp.git), dưới đây là danh sách những tính năng **có thể bạn đang thiếu** và cần phát triển thêm để hoàn thiện hệ thống theo thiết kế trên:

- [x] Thiết lập .NET MAUI & Xin quyền Location.
- [x] Lấy vị trí GPS cơ bản.
- [ ] **Background Service:** Chạy ngầm GPS khi tắt màn hình (Cần custom handler cho Android/iOS).
- [ ] **Offline DB Sync:** Tích hợp `SQLite` để cache POI, chưa có cơ chế đồng bộ lên `SQL Server`.
- [ ] **Narrator Engine:** Quản lý hàng chờ âm thanh, chống phát chồng chéo (Debounce/Cooldown).
- [ ] **Map View:** Tích hợp Google Maps hoặc nền tảng bản đồ để hiển thị trực quan.
- [ ] **QR Code Scanner:** Quét mã kích hoạt Audio không cần GPS.
- [ ] **CMS Web & API Backend:** Xây dựng backend độc lập bằng ASP.NET Core API kết nối SQL Server.
- [ ] **Analytics & Heatmap:** Gom log gửi về server để vẽ bản đồ nhiệt.

---

## 💻 Cài đặt & Chạy dự án (Local)

<details>
  <summary><b>Click để xem hướng dẫn cấu hình</b></summary>

1. Clone dự án:
   ```bash
   git clone [https://github.com/LuDaddy1509/GpsGeoFenceApp.git](https://github.com/LuDaddy1509/GpsGeoFenceApp.git)