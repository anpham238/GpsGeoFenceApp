# Analytics Dashboard — Tổng quan

## Mục lục
1. [Kiến trúc tổng quan](#kiến-trúc-tổng-quan)
2. [Data Models](#data-models)
3. [API Endpoints](#api-endpoints)
4. [Client-side (Mobile App)](#client-side-mobile-app)
5. [Web Dashboard (Server)](#web-dashboard-server)
6. [Luồng dữ liệu](#luồng-dữ-liệu)
7. [Phân quyền & Tính năng PRO](#phân-quyền--tính-năng-pro)

---

## Kiến trúc tổng quan

```
┌─────────────────────────────────────────────────┐
│               Mobile App (MAUI)                 │
│                                                 │
│  MapPage ──► AnalyticsClient ──► POST /api/v1/  │
│       │                          analytics/*    │
│       └──► TravelHistoryPage ──► GET  /api/v1/  │
│                                  profile/...    │
└───────────────────────┬─────────────────────────┘
                        │ HTTP
┌───────────────────────▼─────────────────────────┐
│                MapApi (Backend)                 │
│                                                 │
│  Program.cs (Endpoints)                         │
│       │                                         │
│       ├── AnalyticsVisits    (table)             │
│       ├── AnalyticsRoutes    (table)             │
│       └── AnalyticsListenDurations (table)      │
│                                                 │
│  wwwroot/dashboard.html  ◄── (hiện đang tắt)    │
└─────────────────────────────────────────────────┘
```

---

## Data Models

### Server-side (`MapApi/Models/Analytics.cs`)

```csharp
// Ghi nhận lần ghé thăm / tương tác POI
public sealed class AnalyticsVisit {
    public long Id { get; set; }
    public Guid SessionId { get; set; }
    public int PoiId { get; set; }
    public string Action { get; set; }   // "enter" | "near" | "tap"
    public DateTime Timestamp { get; set; }
}

// Ghi nhận tọa độ GPS theo thời gian
public sealed class AnalyticsRoute {
    public long Id { get; set; }
    public Guid SessionId { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public DateTime RecordedAt { get; set; }
}

// Ghi nhận thời lượng nghe thuyết minh
public sealed class AnalyticsListenDuration {
    public long Id { get; set; }
    public Guid SessionId { get; set; }
    public int PoiId { get; set; }
    public int DurationSeconds { get; set; }
    public DateTime RecordedAt { get; set; }
}
```

### Client-side DTOs (`ProfileApiClient.cs`)

```csharp
public sealed class RoutePointDto {
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public DateTime RecordedAt { get; set; }
}

public sealed class HistoryPoiDto {
    public long Id { get; set; }
    public int IdPoi { get; set; }
    public string PoiName { get; set; }
    public int Quantity { get; set; }           // Số lần ghé thăm
    public DateTime LastVisitedAt { get; set; }
    public int? TotalDurationSeconds { get; set; }
}
```

### Chart Data (`Models/CategoryChartData.cs`)

```csharp
public class CategoryChartData {
    public string Title { get; set; }
    public int Count { get; set; }
}
```

---

## API Endpoints

### Thu thập dữ liệu (ghi)

| Method | Endpoint | Mô tả |
|--------|----------|-------|
| `POST` | `/api/v1/analytics/visit` | Ghi nhận sự kiện vào/gần/chạm POI |
| `POST` | `/api/v1/analytics/route` | Ghi tọa độ GPS hiện tại |
| `POST` | `/api/v1/analytics/listen-duration` | Ghi thời lượng nghe thuyết minh |

**Request body mẫu:**
```json
// POST /api/v1/analytics/visit
{ "sessionId": "uuid", "poiId": 42, "action": "enter" }

// POST /api/v1/analytics/route
{ "sessionId": "uuid", "latitude": 10.762, "longitude": 106.660 }

// POST /api/v1/analytics/listen-duration
{ "sessionId": "uuid", "poiId": 42, "durationSeconds": 180 }
```

### Truy xuất dữ liệu (đọc)

| Method | Endpoint | Auth | Mô tả |
|--------|----------|------|-------|
| `GET` | `/api/v1/analytics/dashboard` | — | Tổng hợp top POIs + avg duration cho admin |
| `GET` | `/api/v1/analytics/heatmap` | — | 5000 điểm tuyến đường gần nhất (heatmap) |
| `GET` | `/api/v1/profile/history` | JWT | Lịch sử ghé POI của user |
| `GET` | `/api/v1/profile/travel-history?sessionId={id}` | JWT + PRO | Tuyến đường cá nhân |

**Response mẫu `/api/v1/analytics/dashboard`:**
```json
{
  "topPois": [{ "poiId": 1, "totalVisits": 120 }],
  "avgDuration": [{ "poiId": 1, "avgSeconds": 95.4 }]
}
```

---

## Client-side (Mobile App)

### AnalyticsClient (`Services/Api/AnalyticsClient.cs`)

- Quản lý `SessionId` duy nhất per-device, lưu vào `Preferences` với key `analytics_session_id`
- 3 phương thức public:
  - `LogVisitAsync(poiId, action)` — ghi lần ghé thăm
  - `LogRouteAsync(lat, lng)` — ghi tọa độ
  - `LogListenDurationAsync(poiId, durationSeconds)` — ghi thời lượng nghe
- Tất cả lỗi HTTP được nuốt silently — analytics không bao giờ crash app

### MapPage (`Pages/MapPage.xaml.cs`)

Điểm tích hợp analytics chính:

| Sự kiện | Dòng | Hành động |
|---------|------|-----------|
| Geofence enter/near | ~211 | `LogVisitAsync(poi.Id, "enter"/"near")` |
| Narration kết thúc | ~212 | `LogListenDurationAsync(poi.Id, dur)` |
| Tap POI | ~458–459 | `LogVisitAsync(poi.Id, "tap")` + `LogListenDurationAsync` |
| Location update (mỗi 6 tick) | ~510 | `LogRouteAsync(lat, lng)` |

> Route được log mỗi ~3 giây (6 location ticks) để cân bằng độ chính xác và dung lượng storage.

### TravelHistoryPage (`Pages/TravelHistoryPage.xaml.cs`)

- Đọc `SessionId` từ device preferences
- Gọi `ProfileApiClient.GetTravelHistoryAsync(sessionId)`
- Vẽ polyline đỏ trên bản đồ theo tuyến đường đã đi
- Hiển thị: số điểm, khoảng thời gian, auto-zoom về tuyến đường

### CategoryChart (`Pages/Controls/CategoryChart.xaml`)

- Sử dụng Syncfusion `SfCircularChart` (DoughnutSeries)
- Bind vào `CategoryChartData[]` qua `ChartDataLabelConverter`
- Hiện dùng để hiển thị thống kê danh mục (task categories)

---

## Web Dashboard (Server)

**File:** `MapApi/wwwroot/dashboard.html`

> **Trạng thái: Hiện đang bị comment out / tắt**

Khi được kích hoạt, dashboard gồm:

| Thành phần | Công nghệ | Dữ liệu nguồn |
|-----------|-----------|--------------|
| Bar chart top POIs | Chart.js | `/api/v1/analytics/dashboard` |
| Bar chart avg listen duration | Chart.js | `/api/v1/analytics/dashboard` |
| Bảng chi tiết lượt ghé | HTML table | `/api/v1/analytics/dashboard` |
| Bảng chi tiết thời lượng | HTML table | `/api/v1/analytics/dashboard` |
| Heatmap di chuyển | Leaflet.js | `/api/v1/analytics/heatmap` |
| Auto-refresh | JS interval | Cả hai endpoint trên |

---

## Luồng dữ liệu

```
1. Khởi động app
   └─► AnalyticsClient tạo/đọc SessionId từ Preferences

2. User đi trong map
   ├─► Geofence trigger  ──► POST /analytics/visit (enter/near)
   │                     └─► POST /analytics/listen-duration
   ├─► Tap POI           ──► POST /analytics/visit (tap)
   │                     └─► POST /analytics/listen-duration
   └─► Location update   ──► POST /analytics/route (mỗi 6 tick)

3. Server lưu vào DB
   ├─► AnalyticsVisits
   ├─► AnalyticsRoutes
   └─► AnalyticsListenDurations

4. User xem lịch sử
   └─► TravelHistoryPage ──► GET /profile/travel-history
                          └─► Vẽ polyline trên map

5. Admin xem dashboard (hiện tắt)
   └─► dashboard.html ──► GET /analytics/dashboard
                      └─► GET /analytics/heatmap
```

---

## Phân quyền & Tính năng PRO

| Tính năng | Yêu cầu |
|-----------|---------|
| Ghi analytics (visit/route/duration) | Không cần auth |
| Xem lịch sử POI cá nhân | JWT token |
| Xem travel history (tuyến đường) | JWT token + **PRO plan** |
| Admin dashboard | Không có auth hiện tại (endpoint public) |

> **Lưu ý bảo mật:** Endpoint `/api/v1/analytics/dashboard` và `/api/v1/analytics/heatmap` hiện không yêu cầu xác thực — cần xem xét thêm auth nếu deploy production.

---

## Vị trí file quan trọng

```
GpsGeoFenceApp/
├── Application/
│   ├── Services/Api/
│   │   ├── AnalyticsClient.cs          # Client ghi analytics
│   │   └── ProfileApiClient.cs         # Client đọc lịch sử user
│   ├── Pages/
│   │   ├── MapPage.xaml.cs             # Tích hợp analytics chính
│   │   ├── TravelHistoryPage.xaml      # UI hiển thị tuyến đường
│   │   ├── TravelHistoryPage.xaml.cs   # Logic travel history
│   │   └── Controls/
│   │       ├── CategoryChart.xaml      # Doughnut chart control
│   │       ├── CategoryChart.xaml.cs
│   │       └── ChartDataLabelConverter.cs
│   ├── Models/
│   │   └── CategoryChartData.cs        # Model cho chart
│   └── MauiProgram.cs                  # DI registration
├── MapApi/
│   ├── Models/Analytics.cs             # DB entity models
│   ├── Data/AppDb.cs                   # EF Core DbSets
│   ├── Program.cs                      # Endpoint definitions (~line 610)
│   └── wwwroot/dashboard.html          # Web dashboard (tắt)
```
