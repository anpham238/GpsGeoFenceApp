# 📂 TỔNG QUAN CÁC MODULE TRONG DỰ ÁN GPS GEOFENCE APP

Dự án **GpsGeoFenceApp** (Hệ thống Thuyết minh Du lịch Tự động) hiện đang được cấu trúc thành 3 thành phần chính: **Mobile App (.NET MAUI)**, **Backend API (ASP.NET Core)**, và **Web Admin CMS**. Dưới đây là danh sách chi tiết các module hiện có trong dự án dựa trên mã nguồn và tài liệu mô tả.

---

## 📱 1. Hệ thống Ứng dụng Di động (Mobile App / .NET MAUI)
*Thư mục: `Application/`*

Đây là ứng dụng dành riêng cho khách du lịch, hỗ trợ dẫn đường và tự động thuyết minh.

* **🌍 1.1. Module Bản đồ & Định vị (Map & Tracking):**
  * Quản lý giao diện bản đồ, tự động lấy tọa độ GPS người dùng và vẽ lộ trình.
  * Hiển thị các điểm tham quan lân cận.
  * *Tệp tin liên quan:* `MapPage.xaml`, `MapPage.xaml.cs`
* **🎯 1.2. Module Hàng rào Không gian & Thuyết minh (Geofence & Narration):**
  * Core engine nhận diện trạng thái ra/vào điểm đến của người dùng dựa trên toạ độ khu vực (Geofence Trigger).
  * Trình điều khiển tự động phát âm thanh, có hỗ trợ đa ngôn ngữ và chống phát lặp.
  * *Tệp tin liên quan:* `GeofenceEventGate.cs`, Cụm `Services/Audio`, Cụm `Services/Narration`.
* **🔄 1.3. Module Đồng bộ Ngoại tuyến (Offline Sync):**
  * Máy trạm hỗ trợ đồng bộ dữ liệu Tour, Điểm tham quan (POI) vào SQLite cục bộ để hoạt động không cần mạng.
  * *Tệp tin liên quan:* Cụm `Services/Sync`.
* **🔐 1.4. Module Xác thực & Hồ sơ (Auth & Profile):**
  * Cho phép người dùng đăng ký, đăng nhập và quản lý thông tin cá nhân.
  * *Tệp tin liên quan:* `LoginPage`, `RegisterPage`, `ProfilePage`.
* **💎 1.5. Module Nâng cấp Tài khoản (Pro Upgrade):**
  * Hỗ trợ thanh toán và nâng cấp từ tài khoản `FREE` lên `PRO`.
  * *Tệp tin liên quan:* `ProUpgradePage`.
* **🗺️ 1.6. Module Tuyến Tour & Dự án (Tours/Projects Management):**
  * Quản lý, hiển thị và tương tác các tuyến đường, chủ điểm các điểm du lịch.
  * *Tệp tin liên quan:* `ProjectListPage`, `ProjectDetailPage`, `TaskDetailPage`.
* **🧭 1.7. Module Lịch sử Di chuyển (Travel History):**
  * Xem lại nhật ký chuyến đi, vẽ lại tuyến đường đã đi trên bản đồ (Dành cho tài khoản PRO).
  * *Tệp tin liên quan:* `TravelHistoryPage`.
* **📷 1.8. Module Quét Mã QR (QR Scanner):**
  * Kích hoạt nội dung thuyết minh thủ công mà không phụ thuộc vào bộ phát GPS.
  * *Tệp tin liên quan:* `QrScanPage`.
* **⚙️ 1.9. Module Quản lý Cấu hình (Meta Management):**
  * Tuỳ chỉnh cấu hình thông tin phụ về thiết bị/sản phẩm.
  * *Tệp tin liên quan:* `ManageMetaPage`.

---

## ⚡ 2. Hệ thống Backend API (MapApi / ASP.NET Core)
*Thư mục: `MapApi/`*

Xử lý các nghiệp vụ logic nền tảng, lưu trữ dữ liệu và cung cấp REST API chung.

* **📡 2.1. Module Quản lý Thiết bị vãng lai (Guest Devices Controller):**
  * Nhận tín hiệu (Ping) từ điện thoại không đăng ký tài khoản để đếm số lượng người tải app và thiết bị online hiện hành.
  * *Tệp tin:* `GuestDevicesController.cs`.
* **📍 2.2. Module Quản lý Điểm tham quan (POI Controller):**
  * Cung cấp dữ liệu toạ độ, bán kính và thiết lập của các Tour và Điểm tham quan cho Mobile App tải về.
  * Lắng nghe tín hiệu tạo, sửa xoá từ Web Admin.
  * *Tệp tin:* `PoiController.cs`.
* **🗂️ 2.3. Module Đa phương tiện (POI Media Controller):**
  * Quản lý, tải lên, lấy thông tin hình ảnh và file MP3 (thuyết minh) liên quan đến một POI.
  * *Tệp tin:* `PoiMediaController.cs`.
* **qr 2.4. Module Quét QR (QR Controller):**
  * Hỗ trợ các Endpoint để trả về liên kết nội dung khi người dùng quét một mã QR bất kỳ.
  * *Tệp tin:* `QrController.cs`.
* **🎟️ 2.5. Module Quản lý Vé / Abonnement (Tickets Controller):**
  * Logic quản lý hạng thẻ (FREE/PRO), kiểm tra ngày sống của vé và xử lý các giao dịch trên người dùng.
  * *Tệp tin:* `TicketsController.cs`.
* **🌐 2.6. Module Dịch thuật (Translator Controller):**
  * Cung cấp các Endpoint hỗ trợ chuyển đổi ngôn ngữ linh hoạt cho nội dung text-to-speech.
  * *Tệp tin:* `TranslatorController.cs`.
* **📊 2.7. Module Phân tích & Nhật ký (Analytics Engine):**
  * Endpoints tiếp nhận vị trí di chuyển, thời lượng nghe và sự kiện tương tác của người dùng một cách ẩn danh. Lưu trữ dưới bảng `AnalyticsVisits`, `AnalyticsRoutes`, v.v...
  * *Tệp tin:* `Program.cs`, Thư mục `Models/` (Ví dụ `AnalyticsVisit`, `AnalyticsRoute`).

---

## 💻 3. Hệ thống Quản trị Web (CMS Admin Dashboard)
*Thông qua Web Blazor và Backend APIs*

Bao hàm các giao diện quản lý trên phiên bản trình duyệt phục vụ Ban Quản lý Tuyến du lịch.

* **📱 3.1. Module Giám sát Thiết bị theo thời gian thực (Device Dashboard):**
  * Theo dõi tổng thể lượt tải app, con số thiết bị đang *Online* và *Offline*, cùng trạng thái hoạt động mới nhất dựa vào cơ chế *Ping timeout 5 phút*.
* **👥 3.2. Module Quản lý Người dùng & Doanh thu (User & Monetization Dashboard):**
  * Thống kê tổng quan lượng tài khoản có đăng ký, lượng thu nhập đến từ các tài khoản mua gói PRO và công cụ Khoá/Mở khoá tài khoản.
* **🗺️ 3.3. Module Cấu hình Bản đồ (POI/Tour Content Builder):**
  * Cho phép người chủ nhấp/thả ghim tọa độ trên bản đồ Web, sửa bán kính nhận diện, gắn file âm thanh và khai báo ngôn ngữ trực quan.
* **🔥 3.4. Module Phân tích Dữ liệu Nhiệt (Heatmap & Analytics Web View):**
  * Trực quan hoá bằng các biểu đồ về lượng POI được ghé nhiều nhất, điểm có thời lượng khách đứng lại nghe lâu nhất, kèm bản đồ nhiệt về sự phân bổ mật độ người dùng.
