# 📄 PRODUCT REQUIREMENTS DOCUMENT (PRD)
**Tên dự án:** GpsGeoFenceApp - Hệ thống Thuyết minh Du lịch Tự động
**Phiên bản:** 1.0.0
**Ngày cập nhật:** 16/04/2026

---

## 1. Tổng quan sản phẩm (Product Overview)
**GpsGeoFenceApp** là một giải pháp chuyển đổi số trong lĩnh vực du lịch, cung cấp ứng dụng di động đóng vai trò như một "Hướng dẫn viên ảo". Ứng dụng sử dụng công nghệ định vị toàn cầu (GPS) và hàng rào điện tử (Geofence) để nhận diện vị trí của du khách, từ đó tự động phát nội dung thuyết minh (Audio/Text-to-Speech) khi họ bước vào bán kính của một Điểm tham quan (POI - Point of Interest).

Hệ thống bao gồm 2 thành phần chính:
1. **Mobile App (.NET MAUI):** Ứng dụng dành cho du khách, có khả năng hoạt động độc lập (Offline) khi mất kết nối mạng nhờ cơ chế đồng bộ dữ liệu cục bộ.
2. **Web CMS (Admin Panel):** Hệ thống quản trị trung tâm, cho phép ban quản lý tạo mới, chỉnh sửa thông tin POI, upload file Audio, và theo dõi dữ liệu di chuyển của du khách (Heatmap).

---

## 2. Vấn đề và Giải pháp

### 🛑 Vấn đề hiện tại
- **Du khách:** Thường thiếu thông tin khi tự túc tham quan, hoặc phải thuê hướng dẫn viên với chi phí cao. Việc đọc bảng thông tin bằng chữ tại điểm tham quan gây nhàm chán và mất tập trung vào cảnh vật.
- **Ban quản lý:** Khó khăn trong việc cập nhật nội dung thuyết minh, thiếu dữ liệu thực tế về hành vi di chuyển của du khách (khu vực nào đông đúc, khu vực nào bị bỏ qua).

### ✅ Giải pháp của hệ thống
- Tự động hóa hoàn toàn trải nghiệm nghe thuyết minh đa ngôn ngữ (Zero-touch experience).
- Đồng bộ dữ liệu POI xuống thiết bị người dùng (SQLite) để trải nghiệm không bị gián đoạn do sóng yếu.
- Thu thập nhật ký di chuyển (Log ẩn danh) để xây dựng bản đồ nhiệt (Heatmap), hỗ trợ ban quản lý tối ưu hóa luồng khách.

---

## 3. Yêu cầu chức năng sản phẩm (Functional Requirements)

### 3.1. Mobile Application (Dành cho Du khách)
- **F-M01. Quản lý Quyền:** Yêu cầu quyền truy cập Vị trí (Foreground & Background) và quyền Storage (nếu cần).
- **F-M02. GPS Tracking:** Cập nhật vị trí người dùng liên tục theo thời gian thực.
- **F-M03. Geofence Trigger:** Xác định khi người dùng đi vào bán kính của một POI ưu tiên cao nhất.
- **F-M04. Narration Engine (Audio/TTS):** Tự động phát âm thanh mô tả. Có cơ chế chống phát trùng lặp (Cooldown/Debounce) và ưu tiên hàng chờ audio.
- **F-M05. Offline Mode:** Tự động đồng bộ cấu hình Tour/POI từ Server về SQLite cục bộ.
- **F-M06. Map View:** Hiển thị vị trí người dùng và các POI lân cận trực quan trên bản đồ.
- **F-M07. Quét mã QR:** Kích hoạt bài thuyết minh thủ công mà không cần GPS (Dùng cho trạm xe buýt/điểm cố định).

### 3.2. Web CMS (Dành cho Ban quản trị)
- **F-W01. Quản lý POI:** Thêm/Sửa/Xóa điểm tham quan, kéo thả ghim trên bản đồ web để lưu tọa độ, thiết lập bán kính kích hoạt.
- **F-W02. Quản lý Đa ngôn ngữ:** Upload file MP3 hoặc nhập Script để ứng dụng tự sinh Text-to-Speech (Việt, Anh, Hàn, v.v.).
- **F-W03. Quản lý Tuyến Tour:** Nhóm các POI theo chủ đề/tuyến đường.
- **F-W04. Dashboard & Heatmap:** Hiển thị thống kê lượt nghe và bản đồ nhiệt vị trí di chuyển của du khách.

---

## 4. Công nghệ và Kiến trúc tổng thể

### 🛠 Tech Stack
- **Mobile Front-end:** .NET MAUI (iOS/Android), C# 14, .NET 10.
- **Web Admin Front-end:** Blazor WebAssembly / ASP.NET Core MVC.
- **Backend API:** ASP.NET Core Web API 10 (Shared cho cả App và Web).
- **Database:**
  - *Offline (Mobile):* SQLite.
  - *Online (Server):* SQL Server (Quản lý qua SSMS).

### 🏗 Architecture Diagram
```mermaid
graph TD
    subgraph Client_Side [Phía Client]
        A[📱 Mobile App .NET MAUI] -->|Lưu trữ POI, Log| B[(SQLite Offline)]
        A -->|GPS/Geofence Engine| C(Location Services)
    end

    subgraph Admin_Side [Phía Quản trị]
        D[💻 Web CMS]
    end

    subgraph Server_Side [Phía Server - Shared Backend]
        E[⚡ ASP.NET Core Web API]
        F[(🗄️ SQL Server Online)]
        G[📁 File Server / Audio]
    end

    A <-->|Wifi/4G: Sync Data & Upload Logs| E
    D <-->|CRUD Operations| E
    E <--> F
    E <--> G
## 5. Thiết kế Cơ sở dữ liệu (ERD)

Cơ sở dữ liệu `GpsApi` được thiết kế tối ưu trên SQL Server, phân chia rõ ràng giữa dữ liệu danh mục (Tours, POIs), dữ liệu đa phương tiện/ngôn ngữ (PoiLanguage, PoiMedia), và dữ liệu phân tích/lịch sử (HistoryPoi, Analytics).

### 5.1. Sơ đồ Thực thể Liên kết (Entity Relationship Diagram)

```mermaid
erDiagram
    %% Bảng Danh mục hệ thống
    USERS {
        uniqueidentifier UserId PK
        nvarchar Username
        nvarchar Mail "UNIQUE"
        nvarchar PasswordHash
        bit IsActive
    }

    TOURS {
        int Id PK
        nvarchar Name
        nvarchar Description
        bit IsActive
        datetime2 CreatedAt
    }

    POIS {
        int Id PK
        nvarchar Name
        int RadiusMeters "Bán kính kích hoạt"
        float Latitude
        float Longitude
        int CooldownSeconds "Chống spam"
        bit IsActive
        geography Geo "Dữ liệu không gian"
    }

    %% Bảng Trung gian (Nhiều - Nhiều)
    TOUR_POIS {
        int TourId PK, FK
        int PoiId PK, FK
        int SortOrder
    }

    %% Bảng Mapping Đa ngôn ngữ và Media
    POI_LANGUAGE {
        bigint IdLang PK
        int IdPoi FK
        nvarchar LanguageTag "VD: vi, en"
        nvarchar TextToSpeech "Nội dung TTS"
    }

    POI_MEDIA {
        bigint Idm PK
        int IdPoi FK
        nvarchar Image "Link ảnh"
        nvarchar MapLink
        nvarchar Audio "Link file mp3"
    }

    %% Bảng Ghi nhận Lịch sử & Thống kê
    HISTORY_POI {
        bigint Id PK
        int IdPoi FK
        uniqueidentifier IdUser FK
        int Quantity "Số lần ghé thăm"
        int TotalDurationSeconds "Tổng tgian nghe"
        datetime2 LastVisitedAt
    }
    
    ANALYTICS_VISIT {
        bigint Id PK
        uniqueidentifier SessionId
        int PoiId
        nvarchar Action "VD: Enter, Leave, Played"
        datetime2 Timestamp
    }

    %% Định nghĩa các mối quan hệ (Relationships)
    USERS ||--o{ HISTORY_POI : "Có lịch sử tham quan"
    POIS ||--o{ HISTORY_POI : "Được lưu vết"
    
    TOURS ||--o{ TOUR_POIS : "Chứa nhiều POI"
    POIS ||--o{ TOUR_POIS : "Nằm trong Tour"
    
    POIS ||--o{ POI_LANGUAGE : "Có các bản dịch"
    POIS ||--o| POI_MEDIA : "Có file đính kèm"
    
    POIS ||--o{ ANALYTICS_VISIT : "Thu thập Analytics"


GpsGeoFenceApp/
├── 📁 MobileApp/                # Chứa source code .NET MAUI
│   ├── 📁 Models/               # Entities: Poi, Tour, Log...
│   ├── 📁 Views/                # XAML UI: MapPage, ListTourPage...
│   ├── 📁 ViewModels/           # Logic giao diện
│   ├── 📁 Services/             # Chứa các engine xử lý cốt lõi
│   │   ├── GeofenceService.cs
│   │   ├── AudioPlayerService.cs
│   │   ├── LocalDbService.cs    # Thao tác SQLite
│   │   └── ApiClientService.cs  # Gọi HTTP Request lên Backend
│   └── 📁 Platforms/            # Native code cho Android (Foreground Service) / iOS
│
├── 📁 BackendAPI/               # ASP.NET Core Web API
│   ├── 📁 Controllers/          # PoiController, LogController
│   ├── 📁 Data/                 # Entity Framework DbContext
│   └── 📁 DTOs/                 # Data Transfer Objects
│
├── 📁 WebCMS/                   # Blazor WebAssembly (Giao diện Admin)
│   ├── 📁 Pages/                # Dashboard, PoiManagement
│   └── 📁 Components/           # MapsComponent, UploadFile
│
├── 📄 PRD.md                    # Tài liệu yêu cầu sản phẩm này
├── 📄 README.md                 # Hướng dẫn setup dự án
└── 📄 GpsGeoFenceApp.sln        # Solution chính chứa cả 3 project
