# Thiết Kế PlantUML Cho Các Module Trọng Tâm (GpsGeoFenceApp)

Tài liệu này cung cấp mã nguồn **PlantUML** chi tiết cho các luồng hoạt động (Activity) và biểu đồ tuần tự (Sequence) của 6 module mà bạn đã yêu cầu. Bạn có thể chép trực tiếp các đoạn code nằm trong block ` ```plantuml ` vào các công cụ như [PlantText](https://www.planttext.com/), IDE Plugin (VS Code PlantUML), hoặc máy chủ PlantUML của bạn để kết xuất ra hình ảnh.

---

## 1. Module 4: POI Data Management (Sync + Local DB)

### Activity Diagram
```plantuml
@startuml
skinparam style strictuml

start
:Ứng dụng Mobile khởi động;
:Kiểm tra Server Version (GET /sync/version);
if (Có phiên bản mới?) then (Yes)
  :Tải toàn bộ dữ liệu POI (GET /pois);
  :Lưu/Cập nhật vào SQLite (Upsert);
  :Tải trước nội dung Audio/TTS (GET /pois/{id}/narration);
  :Lưu vào Local Cache;
else (No)
  :Sử dụng dữ liệu Local DB hiện tại;
endif
:Hoàn tất đồng bộ, sẵn sàng hiển thị Map;
stop
@enduml
```

### Sequence Diagram
```plantuml
@startuml
actor User
participant "Mobile App" as App
participant "SQLite (Local DB)" as DB
participant "Backend API" as API

User -> App: Mở ứng dụng
activate App
App -> API: GET /sync/version
activate API
API --> App: Version mới nhất
deactivate API

alt Cần cập nhật dữ liệu
    App -> API: GET /pois
    activate API
    API --> App: Danh sách POIs (JSON)
    deactivate API
    App -> DB: SaveAsync(POIs) (Lưu/Upsert)
    
    App -> API: GET /pois/{id}/narration
    activate API
    API --> App: Audio/TTS Data
    deactivate API
    App -> DB: Cache Narration Data
end

App -> DB: Truy vấn danh sách hiển thị
activate DB
DB --> App: Dữ liệu POI
deactivate DB
App --> User: Hiển thị Bản đồ
deactivate App
@enduml
```

---

## 2. Module 6: Web CMS / Admin

### Activity Diagram
```plantuml
@startuml
start
:Admin đăng nhập CMS;
:Chọn chức năng Quản lý (POI, User, Device);
if (Thao tác thực hiện?) then (Thêm/Sửa/Xóa POI)
  :Gọi API Quản lý POI;
  :Cập nhật dữ liệu vào Database;
else if (Thống kê / Dashboard) then
  :Gọi API lấy số liệu Analytics;
  :Truy vấn Database & Aggregate;
else (Quản lý User)
  :Gọi API Quản lý User;
  :Cập nhật trạng thái User;
endif
:Làm mới giao diện Admin CMS hiển thị kết quả;
stop
@enduml
```

### Sequence Diagram
```plantuml
@startuml
actor Admin
participant "Admin UI (React/Blazor)" as UI
participant "Admin API" as API
participant "SQL Database" as DB

Admin -> UI: Chọn thêm/sửa POI
activate UI
UI -> API: POST/PUT /api/v1/admin/pois
activate API
API -> DB: INSERT/UPDATE bảng Pois
activate DB
DB --> API: Lệnh thực thi thành công
deactivate DB
API --> UI: HTTP 200 OK + Dữ liệu trả về
deactivate API
UI --> Admin: Cập nhật danh sách trên giao diện
deactivate UI
@enduml
```

---

## 3. Module 7: Analytics

### Activity Diagram
```plantuml
@startuml
start
:Người dùng thực hiện hành động (Nghe, Đi qua POI);
:App thu thập Event (Visit, Route, Listen);
:Lưu Event vào Hàng chờ (Queue) cục bộ;
fork
  :Tiếp tục chạy ngầm định kỳ;
  :Gửi Batch Logs lên Backend (POST /analytics/*);
fork again
  :Backend nhận Logs;
  :Lưu vào Database Analytics Table;
end fork
:Admin truy cập Dashboard CMS;
:Backend query và tính toán (Sum, Count) -> Trả về JSON cho Heatmap;
stop
@enduml
```

### Sequence Diagram
```plantuml
@startuml
participant "Mobile App" as App
participant "Backend API" as API
participant "Analytics Table (DB)" as DB
participant "Admin Dashboard" as Admin

Note over App: Người dùng đi vào POI hoặc nghe Audio
App -> API: POST /api/v1/analytics/visit (Bất đồng bộ)
activate API
API -> DB: Insert Record (POI_ID, Timestamp, UserID)
activate DB
DB --> API: OK
deactivate DB
API --> App: HTTP 202 Accepted
deactivate API

Note over Admin: Ban quản lý xem báo cáo
Admin -> API: GET /api/v1/analytics/dashboard
activate API
API -> DB: Query & Aggregate Data
activate DB
DB --> API: Dữ liệu thống kê
deactivate DB
API --> Admin: Trả về dữ liệu vẽ Biểu đồ & Heatmap
deactivate API
@enduml
```

---

## 4. Module 8: QR Trigger

### Activity Diagram
```plantuml
@startuml
start
:User mở Camera ứng dụng (Quét QR);
:Phân tích chuỗi URL/Payload từ QR;
if (Loại QR là gì?) then (Ticket / POI Audio)
  :Xác thực mã Ticket qua API;
  if (Hợp lệ & Còn lượt?) then (Yes)
    :Trừ lượt sử dụng;
    :Kích hoạt Narration Engine (Phát Audio POI);
  else (No)
    :Hiển thị thông báo "Hết hạn hoặc Lỗi";
  endif
else (App Download / Distribution)
  :Server nhận diện User-Agent (iOS/Android/Web);
  :Chuyển hướng (Redirect) sang App Store / CH Play;
endif
:Lưu Log quét QR vào Analytics;
stop
@enduml
```

### Sequence Diagram
```plantuml
@startuml
actor User
participant "QrScanPage (App)" as App
participant "Backend API" as API
participant "AppDb" as DB
participant "App Store / Play Store" as Store

User -> App: Quét mã QR thực tế
activate App
App -> API: Gửi URL quét được
activate API
alt Loại QR: Tải App (Distribution)
    API -> DB: Log Analytics Scan
    API --> App: Trả về Redirect URL (Store)
    App -> Store: Mở kho ứng dụng (Hệ điều hành tương ứng)
else Loại QR: Nghe Nội dung (Ticket)
    API -> DB: Validate Ticket & Lượt dùng
    activate DB
    DB --> API: Trả về trạng thái hợp lệ
    deactivate DB
    API --> App: Cho phép phát nội dung
    App -> App: Phát Audio (Narration)
end
deactivate API
deactivate App
@enduml
```

---

## 5. Module 9: Core Workflow Orchestrator

### Activity Diagram
```plantuml
@startuml
start
:App khởi động;
:1. Gọi Module 4 (Sync POI & Data);
:2. Kích hoạt Module 1 (GPS Tracking Foreground/Background);
repeat :Di chuyển
  :3. Module 2 (Geofence Engine) phát hiện vào vùng POI;
  :Kiểm tra quy tắc chống Spam (Cooldown/Debounce);
  if (Hợp lệ?) then (Yes)
    :4. Gọi Module 3 (Narration Engine) -> Thêm vào hàng đợi & Phát tiếng;
    :Tô sáng (Highlight) UI Map cho POI hiện tại;
    :5. Gọi Module 7 (Analytics) -> Bắn log lên Server;
  else (No)
    :Bỏ qua tín hiệu;
  endif
repeat while (App vẫn đang mở/chạy ngầm)
stop
@enduml
```

### Sequence Diagram
```plantuml
@startuml
participant "MapPage (Orchestrator)" as Map
participant "PoiSyncService" as Sync
participant "Location & Geofence" as Geo
participant "NarrationManager" as Narration
participant "AnalyticsClient" as Analytics

Map -> Sync: 1. Sync Data từ Backend
activate Sync
Sync --> Map: Đồng bộ xong
deactivate Sync

Map -> Geo: 2. Bắt đầu Tracking & Đăng ký Geofence
activate Geo

note over Geo: Người dùng đang di chuyển...
Geo --> Map: 3. Sự kiện OnPoiEvent (Đi vào điểm tham quan)
Map -> Narration: 4. Yêu cầu phát nội dung Audio
activate Narration
Narration -> Narration: Play() / TTS
Narration --> Map: Đang phát
deactivate Narration

Map -> Analytics: 5. Gửi log Visit / Listen
activate Analytics
Analytics --> Map: Gửi ngầm thành công
deactivate Analytics
deactivate Geo
@enduml
```

---

## 6. Module 10: Auth + Profile + Plan (FREE/PRO)

### Activity Diagram
```plantuml
@startuml
start
:Khách dùng App (Guest);
if (Có tài khoản?) then (Đăng nhập)
  :Gửi thông tin Login;
else (Chưa có tài khoản)
  :Đăng ký mới (Register);
endif

:Xác thực thành công;
:Hệ thống cấp JWT Token;
:Xác định PlanType (FREE/PRO);

if (Gói cước hiện tại?) then (FREE)
  :Truy cập các tính năng cơ bản;
  :Kiểm tra giới hạn Quota khi dùng;
else (PRO)
  :Truy cập toàn bộ tính năng;
  :Bypass các giới hạn Quota;
endif
stop
@enduml
```

### Sequence Diagram
```plantuml
@startuml
actor User
participant "Mobile App" as App
participant "Auth API" as API
participant "Database" as DB

User -> App: Nhập Username/Password
activate App
App -> API: POST /api/v1/auth/login
activate API
API -> DB: Truy vấn & Check PasswordHash
activate DB
DB --> API: Hợp lệ + Dữ liệu User
deactivate DB

API -> API: Generate JWT Token (PlanType)
API --> App: Trả về Token + User Profile
deactivate API

App -> App: Lưu trữ JWT (Local Storage)
App --> User: Chuyển hướng vào màn hình chính
deactivate App

note over App, API: Khi thực hiện Request tiếp theo
App -> API: Request API kèm [Bearer Token]
activate API
API -> API: Validate Token & Kiểm tra Quyền
API --> App: Trả kết quả (200 OK / 403 Forbidden)
deactivate API
@enduml
```

---

## 7. Module 11: Guest Device Presence + Realtime Monitor

### Activity Diagram
```plantuml
@startuml
start
:Ứng dụng Mobile khởi chạy;
:Định danh thiết bị (Tự sinh Device ID hoặc ID tài khoản);
repeat :Vòng lặp (Mỗi 30s)
  :Thu thập tọa độ hiện tại;
  :Gửi gói tin Ping (Heartbeat) kèm Tọa độ qua API / SignalR;
  :Backend nhận tín hiệu;
  :Cập nhật CSDL / Redis trường LastActive = Now() và Tọa độ;
repeat while (App còn hoạt động)
stop
@enduml
```

### Sequence Diagram
```plantuml
@startuml
skinparam style strictuml

actor "AdminWebCMS" as Admin
participant "Mobile App" as App
participant "GuestDevicesController" as Api
participant "Device Hub (SignalR)" as Hub
participant "DevicePresenceService" as Presence
participant "Database (SQL)" as DB

== Đăng ký Nhận Sự Kiện Thực Tế (Admin) ==
Admin -> Hub: Kết nối WebSockets (/hubs/device)
activate Hub
Hub --> Admin: Kết nối thành công (Lắng nghe sự kiện)
deactivate Hub

== Luồng Thiết Bị Online (Connect) ==
App -> Hub: Mở ứng dụng & Kết nối (OnConnectedAsync)
activate App
activate Hub
Hub -> Presence: MarkConnected(DeviceId)
activate Presence
Presence --> Hub: Đánh dấu Online
deactivate Presence

Hub -> DB: Lưu/Cập nhật Device (FirstSeenAt, LastActiveAt)
activate DB
DB --> Hub: OK
deactivate DB

Hub -> Hub: Tạo DTO (IsOnline = true)
Hub --> Admin: Broadcast "DeviceStatusChanged" (IsOnline = true)
Admin -> Admin: Thêm/Đổi màu Marker (Online) trên Bản đồ
deactivate Hub

== Luồng Cập Nhật Vị Trí (Heartbeat/Polling) ==
loop Mỗi 30-60 giây
    alt Gọi qua HTTP API
        App -> Api: POST /api/v1/guest-devices/heartbeat (Tọa độ)
        activate Api
        Api -> DB: Cập nhật LastLatitude, LastLongitude, LastActiveAt
        activate DB
        DB --> Api: OK
        deactivate DB
        Api --> App: HTTP 200 OK
        deactivate Api
    else Gọi qua SignalR
        App -> Hub: Gọi hàm SendLocation(Tọa độ)
        activate Hub
        Hub -> DB: Cập nhật LastLatitude, LastLongitude, LastActiveAt
        activate DB
        DB --> Hub: OK
        deactivate DB
        deactivate Hub
    end
end

== Luồng Thiết Bị Offline (Disconnect) ==
Note over App: Đóng ứng dụng hoặc mất mạng
App -[hidden]-> Hub: Mất kết nối
deactivate App
Note right of Hub: Kích hoạt OnDisconnectedAsync()
activate Hub

Hub -> Presence: MarkDisconnected(DeviceId)
activate Presence
Presence --> Hub: Đánh dấu Offline
deactivate Presence

Hub -> DB: Cập nhật LastActiveAt
activate DB
DB --> Hub: OK
deactivate DB

Hub -> Hub: Tạo DTO (IsOnline = false)
Hub --> Admin: Broadcast "DeviceStatusChanged" (IsOnline = false)
Admin -> Admin: Đổi màu Marker (Offline/Xám) trên Bản đồ
deactivate Hub

@enduml
```

---

## 8. Tổng Quan Hệ Thống

### Sơ Đồ Use Case (Use Case Diagram)
Biểu đồ thể hiện các tác nhân (Actor) và những chức năng (Use Case) chính mà họ tương tác với hệ thống.

```plantuml
@startuml
left to right direction

actor "Guest User" as Guest
actor "Registered User" as User
actor "PRO User" as Pro
actor "Admin" as Admin

package "GpsGeoFenceApp System" {
  usecase "Track Location (Realtime/Background)" as UC1
  usecase "View Map & POIs" as UC2
  usecase "Auto Narration (TTS/Audio)" as UC3
  usecase "Scan QR (Listen/Download)" as UC4
  usecase "View Travel History" as UC5
  usecase "Manage POIs, Tours & Media" as UC6
  usecase "Manage Users & Devices" as UC7
  usecase "View Analytics Dashboard" as UC8
}

Guest --> UC1
Guest --> UC2
Guest --> UC3
Guest --> UC4

User --> UC1
User --> UC2
User --> UC3
User --> UC4

Pro --> UC1
Pro --> UC2
Pro --> UC3
Pro --> UC4
Pro --> UC5

Admin --> UC6
Admin --> UC7
Admin --> UC8

User -|> Guest
Pro -|> User
@enduml
```

### Sơ Đồ Thực Thể Liên Kết Cơ Sở Dữ Liệu (ERD Diagram)
Biểu đồ ERD được trích xuất dựa trên cấu trúc các thực thể (Entities) của `AppDb.cs` dùng cho hệ thống Backend.

```plantuml
@startuml
!define PRIMARY_KEY(x) <b><color:#b8861b><&key></color> x</b>
!define FOREIGN_KEY(x) <color:#aaaaaa><&key></color> x
!define COLUMN(x) x

entity "Pois" as Pois {
  PRIMARY_KEY(Id) : int
  COLUMN(Name) : nvarchar(200)
  COLUMN(Description) : nvarchar(2000)
  COLUMN(RadiusMeters) : int
  COLUMN(CooldownSeconds) : int
  COLUMN(IsActive) : bit
  COLUMN(CreatedAt) : datetime2
  COLUMN(UpdatedAt) : datetime2
}

entity "PoiLanguage" as PoiLanguage {
  PRIMARY_KEY(IdLang) : int
  FOREIGN_KEY(IdPoi) : int
  COLUMN(LanguageTag) : nvarchar(10)
  COLUMN(TextToSpeech) : nvarchar(4000)
  COLUMN(ProPodcastScript) : nvarchar(max)
  COLUMN(ProAudioUrl) : nvarchar(1000)
}

entity "PoiMedia" as PoiMedia {
  PRIMARY_KEY(Idm) : int
  FOREIGN_KEY(IdPoi) : int
  COLUMN(Image) : nvarchar(1000)
  COLUMN(MapLink) : nvarchar(1000)
}

entity "PoiImage" as PoiImage {
  PRIMARY_KEY(Id) : int
  FOREIGN_KEY(IdPoi) : int
  COLUMN(ImageUrl) : nvarchar(1000)
  COLUMN(SortOrder) : int
  COLUMN(CreatedAt) : datetime2
}

entity "Tours" as Tours {
  PRIMARY_KEY(Id) : int
  COLUMN(Name) : nvarchar(200)
  COLUMN(Description) : nvarchar(2000)
  COLUMN(IsActive) : bit
  COLUMN(CreatedAt) : datetime2
}

entity "TourPois" as TourPois {
  PRIMARY_KEY(TourId) : int <<FK>>
  PRIMARY_KEY(PoiId) : int <<FK>>
  COLUMN(SortOrder) : int
}

entity "Users" as Users {
  PRIMARY_KEY(UserId) : uniqueidentifier
  COLUMN(Username) : nvarchar(100)
  COLUMN(Mail) : nvarchar(200)
  COLUMN(PhoneNumber) : nvarchar(20)
  COLUMN(PasswordHash) : nvarchar(256)
  COLUMN(AvatarUrl) : nvarchar(1000)
  COLUMN(PlanType) : nvarchar(20)
  COLUMN(IsActive) : bit
  COLUMN(CreatedAt) : datetime2
  COLUMN(ProExpiryDate) : datetime2
}

entity "HistoryPoi" as HistoryPoi {
  PRIMARY_KEY(Id) : int
  FOREIGN_KEY(IdPoi) : int
  FOREIGN_KEY(IdUser) : uniqueidentifier
  COLUMN(PoiName) : nvarchar(200)
  COLUMN(Quantity) : int
  COLUMN(LastVisitedAt) : datetime2
}

entity "PoiTickets" as PoiTickets {
  PRIMARY_KEY(TicketCode) : nvarchar(450)
  COLUMN(MaxUses) : int
  COLUMN(CurrentUses) : int
  COLUMN(CreatedAt) : datetime2
}

entity "GuestDevices" as GuestDevices {
  PRIMARY_KEY(DeviceId) : nvarchar(100)
  COLUMN(Platform) : nvarchar(20)
  COLUMN(AppVersion) : nvarchar(20)
  COLUMN(FirstSeenAt) : datetime2
  COLUMN(LastActiveAt) : datetime2
}

entity "DailyUsageTracking" as DailyUsageTracking {
  PRIMARY_KEY(EntityId) : nvarchar(100)
  PRIMARY_KEY(ActionType) : nvarchar(20)
  COLUMN(UsedCount) : int
  COLUMN(LastResetAt) : datetime2
}

entity "SupportedLanguages" as SupportedLanguages {
  PRIMARY_KEY(LanguageTag) : nvarchar(10)
  COLUMN(LanguageName) : nvarchar(50)
  COLUMN(IsPremium) : bit
  COLUMN(IsActive) : bit
}

entity "AppDownloadSources" as AppDownloadSources {
  PRIMARY_KEY(SourceId) : int
  COLUMN(LocationName) : nvarchar(200)
  COLUMN(CampaignCode) : nvarchar(100)
  COLUMN(CreatedAt) : datetime2
}

entity "Analytics_AppDownloadScans" as AnalyticsAppDownloadScans {
  PRIMARY_KEY(Id) : int
  FOREIGN_KEY(SourceId) : int
  COLUMN(Platform) : nvarchar(50)
  COLUMN(ScannedAt) : datetime2
}

' Relationships
Pois ||--o{ PoiLanguage : "1:N"
Pois ||--o{ PoiMedia : "1:N"
Pois ||--o{ PoiImage : "1:N"
Pois ||--o{ HistoryPoi : "1:N"
Users ||--o{ HistoryPoi : "1:N"
Tours ||--o{ TourPois : "1:N"
Pois ||--o{ TourPois : "1:N"
AppDownloadSources ||--o{ AnalyticsAppDownloadScans : "1:N"

@enduml
```
