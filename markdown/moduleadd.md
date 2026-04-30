# 📲 20. Đặc Tả Module: Phân Phối & Tải App Qua Mã QR Thông Minh

![Status](https://img.shields.io/badge/Status-Proposed-orange)
![Type](https://img.shields.io/badge/Module-Growth_%26_Distribution-blue)
![Platform](https://img.shields.io/badge/Platform-Android_%26_iOS-brightgreen)

> **Mục tiêu:** Tạo ra một trải nghiệm cài đặt ứng dụng không rào cản (Frictionless Onboarding). Du khách chỉ cần dùng camera mặc định của điện thoại quét 1 mã QR duy nhất được in trên các bảng hướng dẫn. Hệ thống sẽ tự động phân tích thiết bị và điều hướng họ thẳng đến **Google Play (Android)** hoặc **App Store (iOS)** để tải ứng dụng ngay lập tức.

---

## 🏗️ 1. Kiến trúc Logic: Điều Hướng Thông Minh (Smart Redirect)

Để chỉ dùng **1 mã QR duy nhất** cho mọi dòng điện thoại, mã QR vật lý in trên bảng chỉ dẫn sẽ không chứa liên kết trực tiếp của kho ứng dụng. Thay vào đó, nó chứa một **Dynamic Link (Liên kết động)** trỏ về máy chủ Backend của bạn.

### 1.1. Luồng xử lý kỹ thuật (Workflow)
1. **Quét mã:** Du khách mở Camera quét mã QR. Camera đọc được URL: `https://api.yourdomain.com/api/v1/download/1` (Số `1` là ID của điểm đặt mã QR).
2. **Nhận diện thiết bị (Backend):** Khi điện thoại gửi HTTP Request, Server ASP.NET Core sẽ trích xuất và đọc chuỗi `User-Agent` trong Header.
3. **Chuyển hướng (HTTP 302 Redirect):**
   * 🤖 **Nếu `User-Agent` chứa 'Android':** Server lập tức trả về lệnh chuyển hướng đưa trình duyệt sang `market://details?id=com.your.gpsapp` (Mở ứng dụng Google Play Store).
   * 🍏 **Nếu `User-Agent` chứa 'iPhone' hoặc 'iPad':** Server chuyển hướng sang `itms-apps://itunes.apple.com/app/id123456789` (Mở ứng dụng Apple App Store).
   * 💻 **Nếu là PC/Desktop:** Chuyển hướng về Landing Page giới thiệu dự án với 2 nút tải riêng biệt.

---

## 🗄️ 2. Ánh xạ Cơ sở dữ liệu (Analytics Tracking)

Để đo lường chiến dịch truyền thông (Ví dụ: Biết được mã QR đặt ở cổng Dinh Độc Lập mang về bao nhiêu lượt tải so với mã đặt tại Bưu Điện), hệ thống sử dụng trực tiếp 2 bảng đã được định nghĩa trong CSDL:

* **Bảng `[dbo].[AppDownloadSources]`**: 
  Quản lý danh sách các điểm đặt mã QR vật lý.
  * `SourceId` (int): Định danh mã QR.
  * `LocationName`: Tên địa điểm đặt bảng (VD: "Cổng chính Dinh Độc Lập").
  * `CampaignCode`: Mã chiến dịch.

* **Bảng `[dbo].[Analytics_AppDownloadScans]`**:
  Lưu lại log mỗi khi có người quét mã để thống kê.
  * `SourceId`: Khóa ngoại trỏ về bảng nguồn.
  * `Platform`: Lưu hệ điều hành vừa phát hiện ('Android', 'iOS', 'Web').
  * `ScannedAt`: Thời điểm quét mã.

---

## 🔌 3. Thiết kế API Endpoints (Backend ASP.NET Core)

API xử lý luồng này được thiết kế cực kỳ tinh gọn, đảm bảo tốc độ phản hồi (Latency) dưới 50ms để người dùng không cảm thấy độ trễ khi bị chuyển hướng.

| Method | Endpoint | Quyền truy cập | Nhiệm vụ |
| :--- | :--- | :---: | :--- |
| `GET` | `/api/v1/download/{sourceId}` | Public | 1. Phân tích `User-Agent`.<br>2. `INSERT` log vào bảng `Analytics_AppDownloadScans`.<br>3. `Return Redirect(storeUrl)`. |
| `GET` | `/api/v1/admin/analytics/downloads`| Admin | Query đếm số lượt tải theo từng `LocationName` để vẽ biểu đồ cho Web Admin. |

---

## 🎨 4. Tiêu chuẩn Thiết kế & In ấn Mã QR vật lý

Để đảm bảo camera mặc định của 100% điện thoại đều lấy nét và quét được mã QR ngay lập tức ngoài trời sáng:

* **Tối giản dữ liệu (URL Ngắn):** Domain API cấu hình càng ngắn càng tốt để các điểm pixel (chấm vuông) trên QR Code to và thưa. Dễ lấy nét.
* **Màu sắc & Độ tương phản:** Bắt buộc dùng màu tối (Đen/Xanh đen) trên nền sáng (Trắng/Vàng). Không dùng nền trong suốt hoặc hình chìm làm giảm độ tương phản.
* **Kích thước in ấn:** Tối thiểu **4x4 cm** đối với tờ rơi cầm tay, và **12x12 cm** đối với bảng thông tin đứng (Standee) cách xa người dùng 1-1.5 mét.
* **Call-to-Action (Nút kêu gọi):** Phía dưới hoặc viền quanh mã QR luôn phải có dòng chữ hướng dẫn rõ ràng: *"Mở Camera điện thoại quét mã để tải Ứng dụng"*. 

---

## ⚙️ 5. Sơ đồ Trải nghiệm Người dùng (UX Sequence Diagram)

Sơ đồ mô tả quy trình "Mô-tơ vô hình" phía sau giúp người dùng tải app chỉ với 1 thao tác duy nhất.

```mermaid
sequenceDiagram
    participant User as 🤳 Du khách (Android)
    participant QR as 🖼️ Bảng hướng dẫn (Mã QR)
    participant API as ⚡ Backend (.NET 10)
    participant Store as 🏬 Google Play Store

    User->>QR: Đưa Camera điện thoại lên quét
    QR-->>User: Hiện thông báo bật lên: "Mở liên kết api.gpsapp.vn..."
    
    User->>API: Chạm vào thông báo (Trình duyệt gọi HTTP GET)
    activate API
    Note over API: Thuật toán nhận diện User-Agent: Phân tích thấy "Android"
    
    API->>API: Lưu Database: Nguồn = Bảng số 1, Thiết bị = Android
    API-->>User: HTTP 302 Found (Kèm theo URL market://...)
    deactivate API
    
    Note over User, Store: Trình duyệt nhận HTTP 302 và tự động kích hoạt App Store hệ thống
    
    User->>Store: Google Play tự động bật lên
    Store-->>User: Hiện màn hình tải app "GPS GeoFence Tour" -> User bấm [Cài đặt]