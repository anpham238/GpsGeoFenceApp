# 🗺️ AUDIO TOUR GUIDE APP — VIBE CODING SPECIFICATION

> **Dự án:** Ứng dụng thuyết minh tham quan tự động theo GPS  
> **Stack:** .NET MAUI + SQLite (Mobile) · ASP.NET Core Web API + SQL Server (Backend)  
> **Target Platform:** Android / iOS  
> **Runtime:** .NET 10  

---

## ⚙️ TECH STACK

| Layer | Công nghệ | Vai trò |
|---|---|---|
| Mobile Client | .NET MAUI (.NET 10) | UI, GPS, Audio, QR |
| Local Storage | SQLite | Offline POI cache |
| Backend API | ASP.NET Core Web API (.NET 10) | REST endpoints |
| Central DB | SQL Server | Master data, CMS, Analytics |

---

## 🏛️ KIẾN TRÚC TỔNG THỂ (TECH STACK MAPPING)

```
┌─────────────────────────────────────────────────────────────┐
│                    MOBILE APP (Client)                      │
│              .NET MAUI · Android / iOS · .NET 10            │
│                                                             │
│  ┌──────────┐ ┌──────────┐ ┌──────────┐ ┌──────────────┐  │
│  │   GPS    │ │GeoFence  │ │  Audio   │ │   Map UI     │  │
│  │ Tracking │→│ Engine   │→│  Engine  │ │   + QR Scan  │  │
│  └──────────┘ └──────────┘ └──────────┘ └──────────────┘  │
│                        ↕ SQLite (Offline Cache)             │
└────────────────────────┬────────────────────────────────────┘
                         │ HTTPS / REST API (Wi-Fi sync)
┌────────────────────────▼────────────────────────────────────┐
│              BACKEND SERVER (ASP.NET Core Web API)          │
│  ┌─────────────────┐         ┌──────────────────────────┐  │
│  │  Data Sync API  │         │  Analytics & Logging API │  │
│  └─────────────────┘         └──────────────────────────┘  │
│                        ↕ SQL Server                         │
│  ┌──────────────────────────────────────────────────────┐  │
│  │      SQL Server · Master Data · CMS · Logs           │  │
│  └──────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────┘
```

---

## 📱 PHẦN 1 — MOBILE APP MODULES (.NET MAUI + SQLite)

---

### Module 1 · Core GPS Tracking *(Theo dõi vị trí)*

**Mục tiêu:** Lấy vị trí người dùng liên tục theo thời gian thực.

**Yêu cầu kỹ thuật:**
- Chạy cả **Foreground** (app đang mở) và **Background** (app bị ẩn / khóa màn hình)
- Sử dụng **Fused Location Provider Client** — tối ưu đặc biệt cho Android
- Cân bằng giữa **độ chính xác định vị** và **mức tiêu thụ pin**

**Behavior:**
```
App Start
  └─► Khởi động Background Service
        └─► Đăng ký Fused Location Provider
              └─► Nhận location updates định kỳ
                    └─► Gửi tọa độ → GeoFence Engine
```

**Permissions cần khai báo:**
- `ACCESS_FINE_LOCATION` (Android)
- `ACCESS_BACKGROUND_LOCATION` (Android 10+)
- `NSLocationAlwaysUsageDescription` (iOS)

---

### Module 2 · GeoFence Engine *(Xử lý không gian & Tọa độ)*

**Mục tiêu:** Xác định POI gần nhất / có ưu tiên cao nhất trong bán kính cho phép.

**Trigger events:**
- `Enter zone` — người dùng bước vào vùng bán kính của POI
- `Approach` — người dùng đang tiến gần đến ngưỡng kích hoạt

**Luồng xử lý:**
```
Nhận tọa độ GPS mới
  └─► Duyệt danh sách POI từ SQLite
        └─► Tính khoảng cách Haversine đến từng POI
              └─► Tìm POI gần nhất trong bán kính
                    ├─► [Đã trong vùng] → Gửi sự kiện → Audio Engine
                    └─► [Ngoài vùng]   → Chờ update tiếp theo
```

**Anti-spam (bắt buộc):**
- **Debounce:** Chờ tối thiểu X giây sau lần kích hoạt cuối cùng trước khi kích hoạt lại
- **Cooldown per POI:** Mỗi POI có cooldown riêng (VD: 10 phút) — tránh loop khi người dùng đi qua lại ranh giới

**Data cần từ SQLite:**
```
POI { id, name, lat, lng, radius_meters, priority, cooldown_minutes }
```

---

### Module 3 · Narration & Audio Engine *(Xử lý thuyết minh)*

**Mục tiêu:** Phát nội dung thuyết minh đúng lúc, không trùng lặp, không gây phiền.

**Hai nguồn phát:**

| Loại | Ưu điểm | Nhược điểm |
|---|---|---|
| **Text-to-Speech (TTS)** | Nhẹ, đa ngôn ngữ, linh hoạt | Giọng tổng hợp |
| **File Audio (.mp3/.aac)** | Giọng tự nhiên, chuyên nghiệp | Nặng dữ liệu |

**Audio Queue Management:**
```
Nhận sự kiện kích hoạt POI
  └─► Kiểm tra: đang phát bài nào không?
        ├─► [Đang phát] → Queue / Skip tuỳ priority
        └─► [Rảnh]      → Kiểm tra: POI này đã phát trong cooldown chưa?
                              ├─► [Đã phát] → Bỏ qua
                              └─► [Chưa]    → Phát Audio hoặc TTS
                                              └─► Ghi log: POI_ID + timestamp
```

**Nguyên tắc bắt buộc:**
- ❌ Không được phát đè lên nhau
- ❌ Không lặp lại POI đã phát trong session hiện tại (trừ khi user yêu cầu)
- ✅ Tự động **Pause** khi có cuộc gọi / thông báo hệ thống
- ✅ **Resume** sau khi cuộc gọi kết thúc (nếu user muốn)

---

### Module 4 · Map View & UI *(Giao diện Bản đồ)*

**Mục tiêu:** Hiển thị trực quan POI và vị trí người dùng lên bản đồ.

**Tính năng:**
- 📍 Hiển thị vị trí thực của người dùng (real-time, cập nhật liên tục)
- 📌 Render toàn bộ POI dưới dạng marker trên bản đồ
- 🔆 **Highlight** POI gần nhất đang trong vùng kích hoạt
- 🖱️ Tap vào POI → xem **Chi tiết POI**:
  - Tên điểm tham quan
  - Mô tả văn bản
  - Hình ảnh minh họa
  - Link bản đồ (Google Maps / Apple Maps)
  - Nút phát thuyết minh thủ công

---

### Module 5 · QR Code Scanner *(Quét mã ngoại tuyến)*

**Mục tiêu:** Kích hoạt thuyết minh tức thì, độc lập với GPS.

**Use-case:** Dành cho các **trạm dừng cố định** (VD: Trạm xe buýt Khánh Hội, Vĩnh Hội, Xóm Chiếu).

**Luồng:**
```
User nhấn [Quét QR]
  └─► Mở camera scanner
        └─► Đọc QR Code → giải mã POI_ID
              └─► Tra cứu POI_ID trong SQLite
                    ├─► [Tìm thấy] → Phát thuyết minh ngay lập tức
                    └─► [Không có] → Hiển thị thông báo lỗi
```

**QR Payload format:**
```json
{ "poi_id": "KH-001", "version": 1 }
```

---

### Module 6 · Local Data Synchronization *(Đồng bộ dữ liệu SQLite)*

**Mục tiêu:** Tải toàn bộ dữ liệu cần thiết về thiết bị qua Wi-Fi để dùng offline.

**Trigger sync:** Thủ công (user bấm) hoặc tự động khi phát hiện Wi-Fi + có version mới.

**Dữ liệu tải về & lưu vào SQLite:**

```
POI Table:
  - poi_id, name, description_text, lat, lng
  - radius_meters, priority, cooldown_minutes
  - tts_script (text), audio_file_url, language_code
  - image_urls (JSON array)
  - tour_id (foreign key)

Tour Table:
  - tour_id, name, description, ordered_poi_ids

Config Table:
  - key, value (các cấu hình hệ thống)
```

**Chiến lược sync:**
- So sánh `version` / `last_updated` từ server trước khi tải
- Tải delta (chỉ tải những gì thay đổi) nếu server hỗ trợ
- Download file audio vào local storage của thiết bị

---

## 🖥️ PHẦN 2 — BACKEND & API MODULES (ASP.NET Core Web API + SQL Server)

---

### Module 7 · Data Sync API *(Cung cấp dữ liệu)*

**Mục tiêu:** Các RESTful endpoint để Mobile App tải cấu hình và dữ liệu POI về.

**Endpoints:**

```
GET  /api/sync/version
     → Trả về version hiện tại của dataset (để app kiểm tra trước khi tải)

GET  /api/sync/pois?lang={code}
     → Danh sách toàn bộ POI kèm tọa độ, script TTS, metadata

GET  /api/sync/tours
     → Danh sách Tour và POI thuộc từng Tour

GET  /api/sync/config
     → Các cấu hình hệ thống (default radius, cooldown mặc định...)

GET  /api/audio/{poi_id}?lang={code}
     → Trả về URL hoặc stream file audio của POI
```

---

### Module 8 · Data Analytics & Logging *(Phân tích dữ liệu)*

**Mục tiêu:** Nhận dữ liệu từ App, xử lý và lưu thống kê phục vụ CMS Dashboard.

**Endpoints:**

```
POST /api/analytics/visit
     Body: { session_id, poi_id, action: "enter"|"listen"|"exit", timestamp }

POST /api/analytics/route
     Body: { session_id, waypoints: [{lat, lng, timestamp}] }
     → Lưu tuyến đường ẩn danh (anonymous)

POST /api/analytics/listen-duration
     Body: { session_id, poi_id, duration_seconds }
```

**Tính năng phân tích:**

| Metric | Mô tả |
|---|---|
| Top POI nghe nhiều | Thống kê lượt truy cập theo POI_ID |
| Avg. listening time | Thời gian trung bình nghe tại 1 POI |
| User heatmap | Mật độ tọa độ waypoints → vẽ Heatmap |
| Route patterns | Tuyến đường phổ biến nhất |

**Nguyên tắc privacy:**
- Không lưu thông tin định danh cá nhân
- `session_id` là UUID ngẫu nhiên, tạo mới mỗi session, không liên kết với user

---

## 🖱️ PHẦN 3 — CMS SYSTEM MODULES *(Hệ thống quản trị web)*

---

### Module 9 · Content Management *(Quản lý nội dung)*

#### 9.1 Quản lý POI
- CRUD điểm tham quan: Tên, Mô tả, Tọa độ (Lat/Lng), Ảnh
- Thiết lập **bán kính kích hoạt** (radius_meters)
- Thiết lập **mức độ ưu tiên** (priority) và **cooldown**
- Preview vị trí trên mini-map

#### 9.2 Quản lý Thuyết minh
- Upload file Audio (.mp3, .aac) hoặc nhập **Script TTS**
- Nghe thử trực tiếp trên CMS
- Quản lý theo từng ngôn ngữ (VD: VI, EN, FR, ZH)

#### 9.3 Quản lý Bản dịch (Localization)
- Giao diện dịch thuật song song (ngôn ngữ gốc ↔ ngôn ngữ đích)
- Trạng thái dịch: `Draft` | `Review` | `Published`
- Export/Import file dịch (CSV / JSON)

#### 9.4 Quản lý Tour
- Tạo Tour mới: Tên, Mô tả, chọn POI thuộc Tour
- Sắp xếp thứ tự POI trong Tour (drag & drop)
- Publish / Unpublish Tour

#### 9.5 Analytics Dashboard
- 📊 Biểu đồ Top POI được nghe nhiều nhất
- ⏱️ Thời gian nghe trung bình tại từng POI
- 🔥 Heatmap hành vi người dùng trên bản đồ
- 📅 Lọc theo khoảng thời gian (ngày / tuần / tháng)

---

## 🗃️ DATABASE SCHEMA OVERVIEW (SQL Server)

```sql
-- Master POI table
POI (
  poi_id        NVARCHAR(50)  PK,
  name          NVARCHAR(255),
  lat           FLOAT,
  lng           FLOAT,
  radius_meters INT,
  priority      INT,
  cooldown_min  INT,
  is_active     BIT,
  created_at    DATETIME2,
  updated_at    DATETIME2
)

-- Multilingual content
POI_Content (
  content_id    INT           PK IDENTITY,
  poi_id        NVARCHAR(50)  FK → POI,
  lang_code     NVARCHAR(10),  -- 'vi', 'en', 'fr'...
  description   NVARCHAR(MAX),
  tts_script    NVARCHAR(MAX),
  audio_url     NVARCHAR(500),
  status        NVARCHAR(20)   -- 'draft', 'published'
)

-- Tour grouping
Tour (
  tour_id       INT           PK IDENTITY,
  name          NVARCHAR(255),
  description   NVARCHAR(MAX),
  is_active     BIT
)

Tour_POI (
  tour_id       INT           FK → Tour,
  poi_id        NVARCHAR(50)  FK → POI,
  sort_order    INT
)

-- Analytics
Analytics_Visit (
  visit_id      BIGINT        PK IDENTITY,
  session_id    UNIQUEIDENTIFIER,
  poi_id        NVARCHAR(50),
  action        NVARCHAR(20),
  timestamp     DATETIME2
)

Analytics_Route (
  point_id      BIGINT        PK IDENTITY,
  session_id    UNIQUEIDENTIFIER,
  lat           FLOAT,
  lng           FLOAT,
  recorded_at   DATETIME2
)

Analytics_ListenDuration (
  id            BIGINT        PK IDENTITY,
  session_id    UNIQUEIDENTIFIER,
  poi_id        NVARCHAR(50),
  duration_sec  INT,
  recorded_at   DATETIME2
)
```

---

## 🔑 CONSTRAINTS & NON-FUNCTIONAL REQUIREMENTS

| Yêu cầu | Chi tiết |
|---|---|
| **Offline-first** | App phải hoạt động đầy đủ khi không có mạng (sau lần sync đầu) |
| **Battery optimization** | GPS interval có thể tăng khi pin thấp (< 20%) |
| **Audio interrupt** | Tự động pause khi có cuộc gọi đến / thông báo ưu tiên cao |
| **Anti-spam GeoFence** | Debounce ≥ 5s · Cooldown per POI ≥ 10 phút (cấu hình được) |
| **Anonymous analytics** | Không lưu PII, session_id reset mỗi lần mở app |
| **Sync strategy** | Chỉ sync qua Wi-Fi, kiểm tra version trước khi tải |
| **Multi-language** | Tối thiểu: Tiếng Việt (VI) + Tiếng Anh (EN) |
| **API versioning** | `/api/v1/...` — bắt buộc từ đầu để tránh breaking changes |

---

## 📁 PROJECT STRUCTURE GỢI Ý

```
/Solution
├── AudioTourApp.sln
│
├── src/
│   ├── AudioTourApp.Mobile/          # .NET MAUI project
│   │   ├── Modules/
│   │   │   ├── GpsTracking/
│   │   │   ├── GeoFenceEngine/
│   │   │   ├── AudioEngine/
│   │   │   ├── MapView/
│   │   │   ├── QrScanner/
│   │   │   └── DataSync/
│   │   ├── Data/                     # SQLite models & repositories
│   │   └── Platforms/                # Android / iOS specific code
│   │
│   ├── AudioTourApp.Api/             # ASP.NET Core Web API
│   │   ├── Controllers/
│   │   │   ├── SyncController.cs
│   │   │   └── AnalyticsController.cs
│   │   ├── Services/
│   │   └── Data/                     # EF Core + SQL Server
│   │
│   └── AudioTourApp.Shared/          # DTOs dùng chung Mobile + API
│       └── Models/
│
└── cms/                              # Web CMS (tuỳ chọn: Blazor / React)
    └── AudioTourApp.Cms/
```

---

*Spec version: 1.0 · Ngày tạo: 2026 · Mục đích: Vibe Coding Reference*
