# 💎 15. Đặc Tả Module: Phân Hóa Trải Nghiệm (Freemium & Offline Mode)

![Status](https://img.shields.io/badge/Status-In_Development-orange)
![Business](https://img.shields.io/badge/Model-Freemium-brightgreen)
![Tech](https://img.shields.io/badge/Feature-Offline_Sync_%7C_Background_Audio-blue)

> **Mục tiêu:** Tạo ra động lực cốt lõi để khách du lịch nâng cấp lên gói PRO. Phân hóa rõ rệt trải nghiệm giữa người dùng FREE (Sử dụng AI TTS cơ bản, bắt buộc có mạng, giới hạn ngôn ngữ) và người dùng PRO (Nghe Audio phòng thu, nội dung podcast chuyên sâu, tải tour ngoại tuyến và hỗ trợ chạy nền).

---

## 🗄️ 1. Ánh xạ Cơ sở dữ liệu (Database Mapping)

Hệ thống sử dụng các bảng đã được thiết kế tối ưu trong CSDL để phân luồng quyền lợi người dùng:

**Bảng `SupportedLanguages` (Quản lý Ngôn ngữ VIP):**
Cờ `IsPremium` quyết định xem ngôn ngữ đó là miễn phí hay trả phí.
* VD: Tiếng Việt (`vi-VN`), Tiếng Anh (`en-US`) $\rightarrow$ `IsPremium = 0` (FREE).
* VD: Tiếng Nhật (`ja-JP`), Tiếng Hàn (`ko-KR`) $\rightarrow$ `IsPremium = 1` (PRO).

**Bảng `PoiLanguage` (Quản lý Nội dung theo Hạng):**
Lưu trữ nội dung độc lập cho từng hạng tài khoản trên cùng 1 ngôn ngữ.
* `TextToSpeech`: Kịch bản tóm tắt ngắn (30s - 1 phút) dành cho App tự động đọc bằng Engine TTS (Dành cho **FREE**).
* `ProPodcastScript`: Kịch bản chuyên sâu (5-10 phút) chứa giai thoại, lịch sử chi tiết (Dành cho **PRO**).
* `ProAudioUrl`: Link file âm thanh được thu âm chuyên nghiệp, có lồng ghép tiếng động môi trường, nhạc nền (Dành cho **PRO**).

---

## 🎧 2. Tính năng: Trải nghiệm Nghe - Nhìn (Media & Content)

### 2.1. Chất lượng Giọng đọc & Độ sâu Nội dung
* **Logic Backend (API Content):** Khi Mobile App gọi `GET /api/v1/pois/{id}/content?lang=vi-VN`, API kiểm tra `PlanType` của người dùng:
    * **Người dùng FREE:** API chỉ trả về chuỗi `TextToSpeech`. App nạp chuỗi này vào Text-to-Speech của Android/iOS để phát âm thanh Robot.
    * **Người dùng PRO:** API ưu tiên trả về `ProAudioUrl` và `ProPodcastScript`. App sẽ bỏ qua TTS, mở giao diện Media Player cao cấp và stream trực tiếp file âm thanh phòng thu.

### 2.2. Phát nhạc dưới nền (Background Audio)
* **Người dùng FREE:** Trình đọc TTS bị ràng buộc bởi Lifecycle của App. Khi màn hình tắt (`OnSleep`), App tự động gọi hàm `Cancel` để ngắt giọng đọc. Người dùng buộc phải mở sáng màn hình.
* **Người dùng PRO:** Ứng dụng .NET MAUI đăng ký **Foreground Service** (Android) và **Audio Session** (iOS). Khi tắt màn hình, hệ điều hành hiển thị trình điều khiển Media ở Lockscreen. Du khách cất điện thoại vào túi, kết nối tai nghe Bluetooth và tận hưởng chuyến đi một cách rảnh tay.

---

## 📴 3. Tính năng: Tiện ích Du lịch (Travel Utilities)

### 3.1. Chế độ Ngoại tuyến toàn diện (Full Offline Mode)
Giải pháp tối thượng cho du khách quốc tế không có sim 4G tại Việt Nam.

* **Luồng xử lý (PRO Only):**
    1. **Chuẩn bị:** Khách ở khách sạn có Wi-Fi, chọn Tour muốn đi và bấm **"Tải xuống ngoại tuyến"**.
    2. **Đóng gói (Backend):** Backend gom toàn bộ Data của Tour đó thành 1 gói JSON duy nhất (Danh sách POI, Tọa độ, Text đa ngôn ngữ, link Ảnh, link Audio).
    3. **Đồng bộ (MAUI):** App lưu JSON vào Database cục bộ (**SQLite**). Đồng thời chạy Background Transfer để tải các file MP3 và JPG về bộ nhớ máy (`FileSystem.AppDataDirectory`).
    4. **Hoạt động (Offline):** Khi ra ngoài đường, thiết bị tắt mạng. Geofence Engine bắt tín hiệu vệ tinh GPS vật lý. Khi khách chạm vùng POI, App truy vấn SQLite và phát thẳng file MP3 từ ổ cứng điện thoại (Độ trễ 0 giây).

### 3.2. Rào cản Đa ngôn ngữ (Auto-Translate Gates)
* Khi Request API gọi nội dung với ngôn ngữ VIP (VD: `lang=ja-JP`).
* Backend tra cứu bảng `SupportedLanguages`. Thấy `IsPremium = 1`.
* Backend tiếp tục kiểm tra `PlanType` của User. Nếu User là `FREE`, hệ thống từ chối truy xuất DB và ném lỗi `HTTP 403 Forbidden` kèm thông báo: *"Ngôn ngữ này nằm trong gói Premium. Vui lòng nâng cấp để tiếp tục."*

---

## 🔌 4. Thiết kế API Endpoints (Backend ASP.NET Core)

| Method | Endpoint | Quyền | Trả về / Mô tả |
| :--- | :--- | :---: | :--- |
| `GET` | `/api/v1/pois/{id}/content?lang={lang}` | User | Controller xử lý logic FREE/PRO. Bắn lỗi 403 nếu người dùng FREE cố tình chọn ngôn ngữ VIP. |
| `GET` | `/api/v1/tours/{id}/offline-pack` | **PRO** | Trả về Data nguyên khối (All POIs + URLs) của Tour để App tiến hành tải file vật lý. |
| `GET` | `/api/v1/languages` | Public | Trả về danh sách ngôn ngữ. Kèm cờ `isPremium: true/false` để App vẽ icon 🔒 khóa trên UI. |

---

## ⚙️ 5. Luồng xử lý (Sequence Diagram): Chế độ Tải Tour Ngoại tuyến

Sơ đồ mô tả quy trình ứng dụng xử lý việc tải Tour xuống máy:

```mermaid
sequenceDiagram
    participant User as 📱 Người dùng (App)
    participant MAUI as ⚙️ MAUI (SQLite & File)
    participant API as ⚡ Backend API
    participant CDN as ☁️ File Server (Ảnh/Audio)

    User->>MAUI: Chọn Tour -> Bấm "Tải Tour (Offline)"
    MAUI->>API: GET /tours/{id}/offline-pack?lang=en-US
    activate API
    
    API->>API: Kiểm tra User.PlanType == 'PRO'
    alt Tài khoản FREE
        API-->>MAUI: HTTP 403 Forbidden
        MAUI-->>User: Hiện Popup "Tính năng chỉ dành cho gói PRO"
    else Tài khoản PRO
        API-->>MAUI: Trả về JSON Data Pack (Danh sách URLs)
        deactivate API
        
        MAUI->>MAUI: Lưu Meta-data vào SQLite cục bộ
        MAUI-->>User: Hiển thị thanh tiến trình "Đang tải dữ liệu (0%)..."
        
        loop Duyệt qua từng URL (Ảnh & MP3)
            MAUI->>CDN: Download Request
            CDN-->>MAUI: Dữ liệu nhị phân (Binary stream)
            MAUI->>MAUI: Lưu file vào thiết bị (FileSystem)
            MAUI-->>User: Cập nhật Progress Bar (10%, 20%...)
        end
        
        MAUI-->>User: "Đã tải xong! Bạn có thể ngắt kết nối mạng và sử dụng."
    end