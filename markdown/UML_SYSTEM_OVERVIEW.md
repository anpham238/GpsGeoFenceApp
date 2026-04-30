# GPS GeoFence App - Tong Quan Module va Huong Dan Thiet Ke UML

Tai lieu nay tong hop kien truc he thong de phuc vu thiet ke UML cho do an (Use Case, Class Diagram, Activity Diagram, Sequence Diagram).

## 1) Tong quan he thong co bao nhieu module

He thong hien tai co **12 module** (9 module nghiep vu chinh + 3 module ho tro van hanh):

### 9 module nghiep vu chinh
1. GPS Tracking (Realtime + Background)
2. Geofence Engine
3. Narration Engine (TTS/Audio)
4. POI Data Management (Sync + Local DB)
5. Map View (Mobile UI)
6. Web CMS / Admin
7. Analytics
8. QR Trigger (Narration QR + App Download QR)
9. Core Workflow Orchestrator (Sync -> Track -> Detect -> Execute -> Log)

### 3 module ho tro van hanh
10. Auth + Profile + Plan (FREE/PRO)
11. Guest Device Presence + Realtime Monitor (SignalR)
12. Freemium Usage Gate (quota QR_SCAN / POI_LISTEN)

---

## 2) Kien truc tong the de ve UML

### Tang he thong
- **Mobile App (.NET MAUI)**: GPS, geofence, map, narration, quet QR, dong bo du lieu, gui analytics.
- **Backend API (ASP.NET Core + EF Core)**: quan ly POI, user, analytics, QR, ticket, travel history.
- **Database (SQL Server)**: Pois, PoiLanguage, PoiMedia, Tours, Users, HistoryPoi, Analytics tables, QR tables.
- **Admin Web (wwwroot)**: giao dien CMS quan tri du lieu va dashboard.

### Tac nhan (Actors) cho Use Case
- **Guest User**
- **Registered User**
- **PRO User**
- **Admin**
- **External Services**: OSRM (chi duong), App Store / Play Store redirect

---

## 3) Chi tiet tung module (de dung cho Use Case, Class, Activity, Sequence)

## Module 1 - GPS Tracking

- **Muc tieu**: Lay vi tri nguoi dung lien tuc o foreground/background.
- **Use cases chinh**:
  - Bat/tat dinh vi
  - Nhan cap nhat vi tri dinh ky
  - Chuyen sang tracking nen khi app sleep
- **Lop goi y cho Class Diagram**:
  - `ILocationService`
  - `AndroidLocationService`
  - `BackgroundLocationService`
  - `IBackgroundLocationRuntime`
  - `AndroidBackgroundLocationRuntime`
- **Luong Activity**:
  1. App xin quyen location
  2. Neu duoc cap quyen -> bat tracking foreground
  3. Khi app sleep -> start foreground service
  4. Khi app resume -> stop foreground service
  5. Day toa do moi cho module geofence/map/analytics
- **Luong Sequence (rut gon)**:
  - User -> App: Mo app
  - App -> Permission API: Request location permission
  - App -> LocationService: StartTracking()
  - App -> BackgroundRuntime: StartAsync() khi sleep
  - App -> BackgroundRuntime: StopAsync() khi resume

## Module 2 - Geofence Engine

- **Muc tieu**: Phat hien vao/ra/gan POI.
- **Use cases**:
  - Dang ky geofence theo danh sach POI
  - Nhan su kien ENTER/EXIT/DWELL
  - Chan spam bang debounce/cooldown
- **Lop goi y**:
  - `IGeofenceService`
  - `AndroidGeofenceService`
  - `GeofenceBroadcastReceiver`
  - `GeofenceEventHub`
  - `GeofenceEventGate`
- **Luong Activity**:
  1. App nap danh sach POI
  2. Dang ky geofence voi ban kinh cua tung POI
  3. Android phat event transition
  4. EventGate kiem tra debounce/cooldown
  5. Event hop le -> gui sang Narration + UI
- **Luong Sequence**:
  - MapPage -> GeofenceService: RegisterAsync(pois)
  - Android OS -> BroadcastReceiver: Geofence event
  - Receiver -> EventHub -> GeofenceService: Transition
  - GeofenceService -> EventGate: ShouldAccept()
  - GeofenceService -> MapPage: OnPoiEvent()

## Module 3 - Narration Engine

- **Muc tieu**: Phat noi dung huong dan bang audio/TTS.
- **Use cases**:
  - Phat narration khi Enter/Near/Tap
  - Quan ly hang doi narration (queue)
  - Dung narration khi co cuoc goi/notification can ngat
- **Lop goi y**:
  - `INarrationManager`
  - `NarrationManager`
  - `IAudioPlayer`
  - `AndroidAudioPlayer`
  - `CallStateReceiver`
- **Luong Activity**:
  1. Nhan su kien narration
  2. Dua vao queue
  3. Worker lay lan luot
  4. Neu co file audio -> play file
  5. Neu khong -> TTS theo ngon ngu
  6. Log thoi luong nghe
- **Luong Sequence**:
  - MapPage/QrScanPage -> NarrationManager: HandleAsync(announcement)
  - NarrationManager -> QueueWorker: Enqueue
  - QueueWorker -> AudioPlayer/TTS: Play
  - QueueWorker -> Playback/Analytics API: Log

## Module 4 - POI Data Management

- **Muc tieu**: Quan ly du lieu POI va dong bo ve local.
- **Use cases**:
  - Sync version POI
  - Tai danh sach POI active
  - Lay narration theo language/event
  - Quan ly media (image/maplink/audio)
- **Lop goi y**:
  - Mobile: `PoiSyncService`, `PoiDatabase`, `PoiApiClient`, `PoiNarrationApiClient`, `PoiNarrationCache`
  - Backend: `PoiController`, `PoiManagementService`, `AppDb`, entities (`Poi`, `PoiLanguage`, `PoiMedia`, `PoiImage`)
- **Luong Activity**:
  1. Mobile check server version
  2. Neu version moi -> get all POI
  3. Upsert vao SQLite
  4. Cache narration theo `poiId + event + lang`
- **Luong Sequence**:
  - Mobile -> API: GET /sync/version
  - Mobile -> API: GET /pois
  - Mobile -> Local DB: SaveAsync/upsert
  - Mobile -> API: GET /pois/{id}/narration

## Module 5 - Map View

- **Muc tieu**: Hien thi ban do, vi tri user, POI, chi tiet POI.
- **Use cases**:
  - Live tracking
  - Highlight POI gan nhat
  - Tap marker xem chi tiet, nghe narration, mo map chi duong
- **Lop goi y**:
  - `MapPage`
  - `Pin`, `Map`, `Circle`, `Polyline` (map controls)
  - `ProfileApiClient` (directions)
- **Luong Activity**:
  1. Khoi tao map
  2. Ve markers + geofence circles
  3. Theo doi user location
  4. Highlight nearest POI
  5. User tap marker -> show detail + narration
- **Luong Sequence**:
  - User -> MapPage: Open map
  - MapPage -> PoiSyncService/DB: Load POIs
  - MapPage -> GeofenceService: Register
  - User -> MapPage: Tap marker
  - MapPage -> NarrationManager/API: Fetch content + play

## Module 6 - Web CMS / Admin

- **Muc tieu**: Quan tri noi dung va theo doi he thong.
- **Use cases**:
  - CRUD POI
  - Quan ly users
  - Quan ly devices
  - Tao QR narration/distribution
  - Xem dashboard stats
- **Lop goi y**:
  - `AdminQrController`
  - Admin endpoints trong `Program.cs` (`/api/v1/admin/*`)
  - `AppDb` + entities
- **Luong Activity**:
  1. Admin dang nhap CMS
  2. Chon module quan tri (POI/User/QR/Stats)
  3. Goi API backend
  4. Backend cap nhat DB
  5. CMS refresh danh sach/dashboard
- **Luong Sequence**:
  - Admin UI -> Admin API: CRUD request
  - API -> AppDb: Query/Insert/Update
  - API -> Admin UI: Response + result

## Module 7 - Analytics

- **Muc tieu**: Thu thap hanh vi de thong ke route/visit/listen/heatmap.
- **Use cases**:
  - Log visit action (enter/near/tap)
  - Log route points
  - Log listen duration
  - Xem dashboard va heatmap
- **Lop goi y**:
  - `AnalyticsClient` (mobile)
  - backend endpoints `/api/v1/analytics/*`
  - entities `AnalyticsVisit`, `AnalyticsRoute`, `AnalyticsListenDuration`
- **Luong Activity**:
  1. Mobile phat sinh event
  2. Gui log async "silent"
  3. Backend luu DB
  4. Admin dashboard query aggregate
- **Luong Sequence**:
  - Mobile -> API: POST analytics/visit|route|listen-duration
  - API -> DB: Insert
  - Admin -> API: GET analytics/dashboard|heatmap

## Module 8 - QR Trigger

- **Muc tieu**: Kich hoat noi dung boi QR va tang truong cai app.
- **Use cases**:
  - Quet QR ticket narration
  - Quet QR POI direct
  - Tao QR distribution de redirect store theo platform
- **Lop goi y**:
  - Mobile: `QrScanPage`, `TicketApiClient`
  - Backend: `AdminQrController`, `DownloadController`
  - Entities: `PoiTicket`, `AppDownloadSource`, `AnalyticsAppDownloadScan`
- **Luong Activity**:
  1. User quet QR
  2. Tach payload (ticket/poi/url)
  3. Neu ticket -> verify usage -> play narration
  4. Neu distribution link -> backend detect user-agent -> redirect store
  5. Ghi analytics scan
- **Luong Sequence**:
  - User -> QrScanPage: Scan
  - QrScanPage -> API: ScanTicket / open download link
  - API -> DB: Validate + update uses / insert scan log
  - API -> User: Narration result / HTTP redirect

## Module 9 - Core Workflow Orchestrator

- **Muc tieu**: Dam bao chu trinh nghiep vu xuyen suot.
- **Use cases**:
  - Auto-run workflow khi user bat dau su dung map
  - Xu ly event lien module
- **Lop goi y**:
  - `MapPage` (orchestrator o mobile)
  - `PoiSyncService`, `ILocationService`, `IGeofenceService`, `NarrationManager`, `AnalyticsClient`
- **Luong Activity**:
  1. Sync POI
  2. Track location
  3. Detect geofence/nearest
  4. Execute narration
  5. Log analytics + history
- **Luong Sequence**:
  - MapPage -> SyncService -> DB
  - MapPage -> Location/Geofence
  - Geofence -> MapPage -> Narration
  - MapPage -> Playback/Analytics APIs

## Module 10 - Auth + Profile + Plan

- **Muc tieu**: Quan ly user, xac thuc JWT, profile, goi FREE/PRO.
- **Use cases**:
  - Register/Login
  - Xem/cap nhat profile
  - Nang cap PRO
  - Xem lich su tham quan
- **Lop goi y**:
  - API endpoints `/api/v1/auth/*`, `/api/v1/profile/*`
  - entity `Users`, `HistoryPoi`
  - mobile clients `AuthApiClient`, `ProfileApiClient`
- **Luong Activity**:
  1. User dang ky/dang nhap
  2. He thong cap JWT
  3. User su dung tinh nang
  4. He thong gate theo plan

## Module 11 - Guest Device Presence + Realtime

- **Muc tieu**: Theo doi trang thai online/offline cua thiet bi khach.
- **Use cases**:
  - Dang ky danh tinh thiet bi
  - Gui heartbeat + location dinh ky
  - Admin xem realtime snapshot
- **Lop goi y**:
  - `GuestDeviceService`, `GuestHeartbeatService`
  - `DeviceHub`, `DevicePresenceService`
  - entity `GuestDevice`
- **Luong Activity**:
  1. App tao/luu device id
  2. Gui heartbeat theo chu ky
  3. Backend cap nhat last active
  4. Admin query online devices

## Module 12 - Freemium Usage Gate

- **Muc tieu**: Gioi han hanh vi FREE va mo rong cho PRO.
- **Use cases**:
  - Check quota QR_SCAN
  - Check quota POI_LISTEN
  - Reset theo chu ky
- **Lop goi y**:
  - `UsageApiClient`
  - endpoint `/api/v1/usage/check`
  - entity `DailyUsageTracking`
- **Luong Activity**:
  1. Client goi check action
  2. Server kiem tra plan user
  3. Neu PRO -> bypass
  4. Neu FREE -> check used/limit + reset window
  5. Tra ket qua allow/deny

---

## 4) Khung thiet ke Use Case Diagram (goi y)

### System boundary
- GPS GeoFence Tourism System

### Actors
- Guest User
- Registered User
- PRO User
- Admin
- External Map/Store Services

### Nhom use case chinh
- Track location
- View map + nearby POI
- Listen narration (auto/manual/QR)
- Scan QR
- Search POI
- View travel history (PRO)
- Manage POI/Tour/QR (Admin)
- View analytics dashboard (Admin)
- Manage users/devices (Admin)

---

## 5) Khung thiet ke Class Diagram (goi y package)

- **Package Mobile.Presentation**
  - `MapPage`, `QrScanPage`, `ProfilePage`
- **Package Mobile.Application**
  - `PoiSyncService`, `NarrationManager`, `GeofenceEventGate`, `GuestHeartbeatService`
- **Package Mobile.Infrastructure**
  - `PoiApiClient`, `AnalyticsClient`, `UsageApiClient`, `TicketApiClient`
  - `PoiDatabase`, `PoiNarrationCache`
- **Package Platform.Android**
  - `AndroidLocationService`, `AndroidGeofenceService`, `BackgroundLocationService`
- **Package Backend.API**
  - `PoiController`, `AdminQrController`, `DownloadController`
- **Package Backend.Domain**
  - `Poi`, `PoiLanguage`, `PoiMedia`, `Tour`, `Users`, `HistoryPoi`, `Analytics*`
- **Package Backend.Infrastructure**
  - `AppDb`, `HistoryService`, `DevicePresenceService`

---

## 6) Khung thiet ke Activity Diagram (goi y 3 luong quan trong)

1. **Auto Narration Flow**
   - Start -> Permission -> Sync -> Track -> Detect -> Check Cooldown -> Fetch Content -> Play -> Log -> End

2. **QR Narration Ticket Flow**
   - Start Scan -> Parse QR -> Validate Ticket -> Check Remaining Uses -> Load POI -> Play -> Log -> End

3. **Download QR Redirect Flow**
   - Scan QR -> GET /download/{id} -> Detect User-Agent -> Log Scan -> Redirect Android/iOS/Web -> End

---

## 7) Khung thiet ke Sequence Diagram (goi y 4 sequence nen ve)

1. Mobile startup + sync + geofence register
2. Geofence event -> narration queue -> analytics logging
3. QR scan ticket -> verify -> narration playback
4. Admin tao QR distribution -> user scan -> redirect store

---

## 8) Goi y cach tach file UML de lam do an

Ban nen tach thanh bo file nhu sau:

- `uml/usecase-system.puml`
- `uml/class-mobile.puml`
- `uml/class-backend.puml`
- `uml/activity-auto-narration.puml`
- `uml/activity-qr-flow.puml`
- `uml/sequence-geofence-to-narration.puml`
- `uml/sequence-admin-qr-download.puml`

Neu can, co the bo sung:
- `uml/deployment-architecture.puml` (Mobile, API, DB, Admin Web, External Services)

---

## 9) Tieu chi hoan thien tai lieu UML

- Moi module co:
  - Muc tieu
  - Actors
  - Input/Output
  - Luong chinh
  - Luong loi (exception)
  - Lop tham gia
- Moi diagram can:
  - Dat ten ro theo use case
  - Co pre-condition/post-condition
  - Co ghi chu rule nghiep vu (cooldown, quota, premium gate)

---

## 10) Ket luan

He thong hien tai da du do lon de xay dung bo UML day du theo huong:
- **Nghiep vu** (Use Case + Activity),
- **Ky thuat** (Class + Sequence),
- **Trien khai** (Deployment).

Tai lieu nay la ban do tong quan de ban trien khai cac file UML chi tiet cho do an.
