# 👤 13. Đặc Tả Module: Quản Lý Trang Cá Nhân & Gói PRO (Profile & Subscription)

![Status](https://img.shields.io/badge/Status-Proposed-orange)
![Monetization](https://img.shields.io/badge/Monetization-Freemium_Model-brightgreen)
![Feature](https://img.shields.io/badge/Feature-Smart_Routing-blue)

> **Mục tiêu:** Xây dựng trung tâm quản lý tài khoản cho du khách (Profile Dashboard). Điểm nhấn của module này là hệ thống **Nâng cấp tài khoản (Gói PRO)**, cho phép người dùng mở khóa các đặc quyền vượt trội như: nghe thuyết minh vô hạn, chỉ đường trực tiếp tới POI, quét QR không giới hạn và xem lại bản đồ hành trình đã đi.

---

## 📱 1. Giao diện Quản lý Trang cá nhân (Profile Dashboard)

Giao diện được thiết kế theo dạng danh sách (List View) trực quan, chia thành các phân vùng:

* **Khu vực Header (Thông tin chung):**
    * Hiển thị **Avatar** hình tròn (lấy từ `AvatarUrl`), Tên hiển thị (`Username`/`FullName`) và Email.
    * **Badge Trạng thái:** Thẻ tag nhỏ ghi `Tài khoản Miễn phí` hoặc `🌟 Tài khoản PRO` màu vàng nổi bật.
* **Khu vực Call-to-Action (Nâng cấp):**
    * Banner/Nút bấm nổi bật: **"🚀 Nâng cấp Gói PRO - Trải nghiệm du lịch không giới hạn!"**.
* **Khu vực Lịch sử & Hoạt động:**
    * 📍 **Lịch sử tham quan:** Danh sách các POI đã nghe thuyết minh (Truy xuất từ bảng `HistoryPoi`).
    * 🗺️ **Nhật ký hành trình (Tính năng PRO):** Mở ra bản đồ có vẽ đường màu đỏ nối các điểm người dùng đã đi qua.
* **Khu vực Thiết lập:**
    * ✏️ **Cập nhật thông tin:** Form thay đổi Tên, Số điện thoại (`PhoneNumber`), cập nhật Avatar.
    * 🚪 **Đăng xuất tài khoản**.

---

## 💎 2. Phân hệ Nâng cấp Gói PRO (Subscription UI)

Khi người dùng nhấn vào nút nâng cấp, một trang thông tin sẽ hiện ra. Giao diện được thiết kế theo phong cách thẻ song song (Card-based UI) sang trọng (Dark mode), giúp người dùng dễ dàng so sánh:

| Tính năng / Đặc quyền | 🆓 Gói Cơ bản (Free) | 🌟 Gói PRO (Premium) |
| :--- | :--- | :--- |
| **Mức giá** | Miễn phí | `X.000 VNĐ / Tháng` (Hoặc mua theo Tour) |
| **Thuyết minh POI** | Giới hạn số lần nghe/ngày | **Vô hạn (Nghe không giới hạn)** |
| **Quét mã QR tại trạm** | Tối đa 5 lần/mã (`MaxUses = 5`) | **Vô hạn số lần quét** (Bỏ qua `MaxUses`) |
| **Chỉ đường (Smart Routing)** | ❌ Không hỗ trợ | ✅ **Có nút chỉ đường** vẽ tuyến đi tới POI |
| **Nhật ký hành trình** | ❌ Không hỗ trợ | ✅ **Lưu vết đường đi (Màu đỏ trên bản đồ)** |
| **Hành động (Button)** | *Đang sử dụng* | **[ Nâng Cấp Ngay ]** |

---

## 🗺️ 3. Đặc tả Kỹ thuật: Các Tính Năng PRO Cốt Lõi

### 3.1. Tính năng: Chỉ đường trực tiếp tới POI (Smart Routing)
* **Quy trình:** Khi người dùng mở xem chi tiết một điểm tham quan (POI), giao diện sẽ xuất hiện thêm nút **"Đường đi tới đây"** (Directions).
* **Hoạt động:** Hệ thống sử dụng tọa độ GPS hiện tại của người dùng và `Latitude`/`Longitude` của POI, sau đó gọi API bản đồ (Google Maps Directions API hoặc Mapbox). Kết quả trả về là một đường polyline màu xanh (Blue Route) vẽ trực tiếp trên màn hình bản đồ của App, hướng dẫn đường đi chi tiết.

### 3.2. Tính năng: Quét QR vô hạn
* **Quy trình:** API `/api/v1/tickets/scan/{ticketCode}` sẽ kiểm tra trạng thái tài khoản.
* **Hoạt động:** Thông thường, bảng `PoiTickets` sẽ chặn nếu `CurrentUses >= MaxUses` (5 lần). Nhưng nếu API xác định User đang sở hữu `PlanType = PRO`, rào cản này sẽ bị vô hiệu hóa, cho phép trả về dữ liệu thuyết minh ngay lập tức.

### 3.3. Tính năng: Nhật ký tuyến đường (Travel History)
* **Quy trình:** Khi người dùng chọn "Nhật ký hành trình" trong Trang cá nhân.
* **Hoạt động:** Hệ thống sẽ truy xuất bảng `Analytics_Route` dựa trên định danh của người dùng. Tập hợp các điểm `Latitude`, `Longitude` sắp xếp theo `RecordedAt` sẽ được nối lại với nhau tạo thành một **đường thẳng màu đỏ (Red Polyline)** vẽ đè lên bản đồ. Tính năng này giúp khách du lịch xem lại toàn bộ dấu chân của mình trong ngày.

---

## 🗄️ 4. Nâng cấp Cơ sở dữ liệu (Database Update)

Để hệ thống phân biệt được người dùng Free và Pro, cần chạy script bổ sung 2 cột vào bảng `Users` hiện tại:

```sql
-- Thêm cột quản lý Gói cước vào bảng Users
ALTER TABLE [dbo].[Users] 
ADD [PlanType] [varchar](20) NOT NULL DEFAULT ('FREE'), -- 'FREE' hoặc 'PRO'
    [ProExpiryDate] [datetime2](3) NULL; -- Thời hạn kết thúc gói PRO (nếu có)

sequenceDiagram
    participant App as 📱 MAUI App
    participant API as ⚡ Backend API
    participant DB as 🗄️ SQL Server
    participant Maps as 🌍 Route Provider (Google)

    App->>API: GET /pois/{id}/directions (Truyền Token)
    activate API
    
    API->>DB: Lấy thông tin User
    DB-->>API: PlanType = "FREE" / "PRO"
    
    alt PlanType == "FREE" (Chưa nâng cấp)
        API-->>App: HTTP 403 Forbidden
        App->>App: Mở màn hình giới thiệu "Gói PRO" (Bảng giá so sánh)
    else PlanType == "PRO" (Đã nâng cấp)
        API->>DB: Lấy tọa độ POI đích
        DB-->>API: Lat, Lng
        
        API->>Maps: Request Route (Từ GPS User -> GPS POI)
        Maps-->>API: Dữ liệu vẽ đường (Polyline)
        
        API-->>App: HTTP 200 OK + Route Data
        App->>App: Render đường chỉ dẫn màu Xanh lên Map
    end
    deactivate API