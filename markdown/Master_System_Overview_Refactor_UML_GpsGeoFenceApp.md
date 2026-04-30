# MASTER – Tổng Quan Hệ Thống & Refactor Kiến Trúc (GpsGeoFenceApp)

> **MỤC ĐÍCH FILE**  
> File `.md` này được tạo ra để:
> - Tổng quan **toàn bộ chức năng của hệ thống** GpsGeoFenceApp
> - Định hướng **refactor toàn bộ code & kiến trúc** nhằm phục vụ bảo trì và review
> - Làm **tài liệu gốc** để giải thích và vẽ **UML Diagram** (Use Case, Class, Sequence, Activity)
> - Dùng trực tiếp cho **đồ án / báo cáo / thuyết trình kiến trúc**

---

## 1. Tổng quan hệ thống

### 1.1 Bài toán hệ thống giải quyết

GpsGeoFenceApp là hệ thống **hướng dẫn thông minh theo vị trí địa lý (POI – Point of Interest)**:
- Người dùng di chuyển trong không gian thực
- Hệ thống dùng GPS + Geofence để xác định vị trí
- Khi người dùng đến gần POI → phát **thuyết minh (audio narration)**
- Hỗ trợ:
  - đa ngôn ngữ
  - QR Code
  - Offline-first
  - Analytics
  - Web Admin quản trị nội dung

---

## 2. Kiến trúc tổng thể (AS-IS)

```text
[ Mobile App (.NET MAUI) ]
        ↓ REST API
[ Backend MapApi (ASP.NET Core) ]
        ↓ EF Core
[ SQL Server – GpsApi ]
        ↑ Admin API
[ Web Admin / CMS ]
```

### 2.1 Vai trò từng thành phần
- **Mobile App**: xử lý realtime (GPS, Map, Geofence, Audio)
- **Backend API**: xử lý nghiệp vụ trung tâm
- **Database**: lưu POI, media, lịch sử, analytics
- **Web Admin**: quản trị POI, QR, media, cấu hình

---

## 3. Tổng quan chức năng toàn hệ thống (Function Map)

### 3.1 Mobile App Functions
- GPS Tracking (Foreground / Background)
- Geofence Detection
- Map & POI visualization
- Narration Engine (Audio + Queue + Priority)
- QR Scan trong app
- Offline cache + Sync
- Analytics client

---

### 3.2 Backend (MapApi) Functions
- POI Management (CRUD + Geo + Priority)
- Media Management (Audio URL / Upload, Image Gallery)
- Narration Service (đa ngôn ngữ)
- QR Code Service (Generate / Resolve)
- Web Landing Service (QR không cần app)
- Analytics & History Tracking

---

### 3.3 Web Admin Functions
- Quản lý POI (Thêm / Sửa / Xóa)
- Gắn narration (URL hoặc File)
- Quản lý ảnh (Cover + Gallery)
- Tạo & quản lý QR
- Xem Analytics
- Cấu hình hệ thống

---

## 4. Vấn đề hiện tại (Pain Points)

### 4.1 Kiến trúc
- Logic nghiệp vụ phân tán nhiều nơi
- UI, Service, Controller lẫn trách nhiệm
- Backend trộn minimal API & controller
- Web Admin chưa có API facade rõ ràng

### 4.2 Code & Review
- Khó review end-to-end flow
- Khó giải thích khi bảo vệ đồ án
- UML khó vẽ vì thiếu ranh giới module

---

## 5. Mục tiêu refactor

- Chuẩn hóa kiến trúc theo **Layer + Module**
- Giảm coupling, tăng cohesion
- Code dễ đọc – dễ review – dễ bảo trì
- UML biểu diễn được 1–1 với code

---

## 6. Kiến trúc sau refactor (TO-BE)

```text
┌────────────────────────────┐
│ Presentation Layer         │
│ (Mobile App / Web Admin)   │
└────────────▲───────────────┘
             │ DTO
┌────────────┴───────────────┐
│ Application Layer          │
│ (UseCase / Services)       │
└────────────▲───────────────┘
             │ Domain Logic
┌────────────┴───────────────┐
│ Domain Layer               │
│ (Entities / Business Rules)│
└────────────▲───────────────┘
             │ ORM / IO
┌────────────┴───────────────┐
│ Infrastructure Layer       │
│ (EF / File / External)     │
└────────────────────────────┘
```

---

## 7. Refactor theo Module

### 7.1 POI Module
- PoiService (CRUD)
- PoiMediaService (Audio / Image)
- PoiPriorityService

### 7.2 Narration Module
- NarrationResolver
- NarrationQueueManager
- NarrationPlayer

### 7.3 QR Module
- QrCodeService
- QrResolverService
- WebNarrationUsageService

### 7.4 Analytics Module
- VisitTrackingService
- ListenTrackingService
- QrTrackingService

---

## 8. UML Mapping (Cực kỳ quan trọng cho đồ án)

### 8.1 Use Case Diagram
- Manage POI (Admin)
- Listen Narration (User)
- Scan QR (User)
- View Analytics (Admin)

### 8.2 Class Diagram
- Poi
- PoiLanguage
- PoiMedia / PoiImages
- NarrationQueue
- QrCode

### 8.3 Sequence Diagram
- User enters POI → Play narration
- Scan QR → Resolve → Web/App

### 8.4 Activity Diagram
- Geofence Detection Flow
- Narration Resolution Flow

---

## 9. Lợi ích đạt được sau refactor

- ✅ Kiến trúc rõ ràng
- ✅ Code dễ review và bảo trì
- ✅ UML dễ vẽ – dễ giải thích
- ✅ Phù hợp cho đồ án & phát triển thật
- ✅ Trình bày ý tưởng thiết kế mạch lạc

---

## 10. Kết luận

File `.md` này là **FILE GỐC – MASTER DESIGN DOCUMENT** của hệ thống GpsGeoFenceApp.

Từ file này có thể tiếp tục:
- Tách FR / NFR
- Vẽ UML chi tiết
- Lập kế hoạch refactor code
- Viết báo cáo kiến trúc hệ thống

> ✅ Khuyến nghị: **in hoặc export file này kèm UML khi bảo vệ đồ án**
