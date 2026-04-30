# UML ĐỒ ÁN – GpsGeoFenceApp
# Hệ thống Thuyết minh Du lịch Thông minh theo Vị trí Địa lý

> **Ghi chú**: Tất cả diagram dưới đây được vẽ bằng PlantUML syntax.  
> Render tại: https://www.plantuml.com/plantuml/uml/ hoặc plugin VS Code "PlantUML".

---

## 1. USE CASE DIAGRAM

```plantuml
@startuml UseCase_GpsGeoFenceApp
skinparam actorStyle awesome
skinparam usecase {
  BackgroundColor #1a2a3a
  BorderColor #1976D2
  FontColor white
}
left to right direction

actor "Du khách\n(Mobile User)" as Tourist
actor "Du khách Web\n(Browser User)" as WebUser
actor "Quản trị viên\n(Admin)" as Admin

rectangle "GpsGeoFenceApp" {
  '--- Tourist use cases ---
  usecase "Xem bản đồ & POI" as UC1
  usecase "Nghe thuyết minh\n(Geofence tự động)" as UC2
  usecase "Quét mã QR\n(trong App)" as UC3
  usecase "Nghe thuyết minh\n(từ QR)" as UC4
  usecase "Xem lịch sử tham quan" as UC5
  usecase "Đăng nhập / Đăng ký" as UC6
  usecase "Đổi mật khẩu" as UC7
  usecase "Nâng cấp gói Pro" as UC8
  usecase "Nhận thông báo POI mới" as UC9

  '--- Web User use cases ---
  usecase "Quét QR bằng Camera\n(không cần app)" as UC10
  usecase "Nghe thuyết minh\ntrên trình duyệt (TTS)" as UC11
  usecase "Tải / Mở App" as UC12

  '--- Admin use cases ---
  usecase "Quản lý POI\n(Thêm/Sửa/Xóa)" as UC13
  usecase "Quản lý Gallery ảnh" as UC14
  usecase "Gắn Audio thuyết minh" as UC15
  usecase "Tạo mã QR cho POI" as UC16
  usecase "Xem Analytics" as UC17
  usecase "Quản lý người dùng" as UC18
  usecase "Cấu hình hệ thống" as UC19
}

Tourist --> UC1
Tourist --> UC2
Tourist --> UC3
Tourist --> UC5
Tourist --> UC6
Tourist --> UC7
Tourist --> UC8
Tourist --> UC9
UC3 ..> UC4 : <<include>>
UC2 ..> UC4 : <<include>>

WebUser --> UC10
UC10 ..> UC11 : <<include>>
UC10 ..> UC12 : <<extend>>

Admin --> UC13
Admin --> UC14
Admin --> UC15
Admin --> UC16
Admin --> UC17
Admin --> UC18
Admin --> UC19
UC14 ..> UC13 : <<include>>
UC15 ..> UC13 : <<include>>
UC16 ..> UC13 : <<include>>

@enduml
```

---

## 2. CLASS DIAGRAM – BACKEND DOMAIN MODEL

```plantuml
@startuml ClassDiagram_Domain
skinparam classBackgroundColor #1a2a3a
skinparam classBorderColor #1976D2
skinparam classFontColor white
skinparam arrowColor #64b5f6

class Poi {
  +Id: int
  +Name: string
  +Description: string?
  +Latitude: double
  +Longitude: double
  +RadiusMeters: int = 120
  +CooldownSeconds: int = 30
  +IsActive: bool = true
  +PriorityLevel: int = 0
  +CreatedAt: DateTime
  +UpdatedAt: DateTime
}

class PoiLanguage {
  +IdLang: long
  +IdPoi: int
  +LanguageTag: string
  +TextToSpeech: string?
  +ProPodcastScript: string?
  +ProAudioUrl: string?
}

class PoiMedia {
  +Idm: long
  +IdPoi: int
  +Image: string?
  +MapLink: string?
}

class PoiImages {
  +Id: long
  +IdPoi: int
  +ImageUrl: string
  +SortOrder: int
  +CreatedAt: DateTime
}

class PoiQrCode {
  +Id: long
  +PoiId: int
  +PublicUrl: string
  +QrPayload: string
  +IsActive: bool
  +CreatedAt: DateTime
}

class PoiTicket {
  +TicketCode: string
  +IdPoi: int
  +LanguageTag: string
  +MaxUses: int
  +CurrentUses: int
  +CreatedAt: DateTime
}

class WebNarrationUsage {
  +Id: long
  +PoiId: int
  +DeviceKey: string
  +PlayCount: int = 0
  +LastPlayedAt: DateTime?
  +CreatedAt: DateTime
}

class User {
  +UserId: Guid
  +Username: string
  +Mail: string
  +PasswordHash: string
  +PhoneNumber: string?
  +AvatarUrl: string?
  +PlanType: string
  +ProExpiryDate: DateTime?
  +IsActive: bool
  +CreatedAt: DateTime
}

class HistoryPoi {
  +Id: long
  +IdPoi: int
  +IdUser: Guid
  +PoiName: string
  +Quantity: int
  +TotalDurationSeconds: int
  +LastVisitedAt: DateTime
}

class Analytics_Visit {
  +Id: long
  +SessionId: Guid
  +PoiId: int
  +Action: string
  +Timestamp: DateTime
}

class DailyUsageTracking {
  +EntityId: string
  +ActionType: string
  +UsedCount: int
  +LastResetAt: DateTime
}

Poi "1" *-- "0..1" PoiMedia : has
Poi "1" *-- "0..*" PoiLanguage : translated by
Poi "1" *-- "0..*" PoiImages : gallery
Poi "1" *-- "0..*" PoiQrCode : has QR
Poi "1" *-- "0..*" PoiTicket : has tickets
Poi "1" *-- "0..*" WebNarrationUsage : web quota
Poi "1" *-- "0..*" Analytics_Visit : tracked
Poi "1" *-- "0..*" HistoryPoi : history
User "1" *-- "0..*" HistoryPoi : visited

@enduml
```

---

## 3. CLASS DIAGRAM – MOBILE NARRATION SYSTEM

```plantuml
@startuml ClassDiagram_Narration
skinparam classBackgroundColor #1a2a3a
skinparam classBorderColor #4CAF50
skinparam classFontColor white
skinparam arrowColor #A5D6A7

enum PoiEventType {
  Enter
  Near
  Tap
}

enum NarrationState {
  Idle
  Queued
  Playing
  Paused
  Interrupted
  Completed
  Skipped
}

class Announcement <<record>> {
  +Poi: POI
  +Lang: string
  +EventType: PoiEventType
  +CreatedAtUtc: DateTime
}

class POI <<Mobile Model>> {
  +Id: int
  +Name: string
  +Description: string?
  +Latitude: double
  +Longitude: double
  +RadiusMeters: int
  +CooldownSeconds: int
  +IsActive: bool
  +PriorityLevel: int
  +NarrationText: string?
  +ImageUrl: string?
  +AudioUrl: string?
  +Language: string?
}

class PoiCandidate {
  +PoiId: int
  +PoiName: string
  +PriorityLevel: int
  +PriorityType: string
  +DistanceMeters: double
  +CooldownSeconds: int
  +LastPlayedAt: DateTime?
  +IsTapped: bool
  +AllowInterrupt: bool
}

class NarrationQueueItem {
  +PoiId: int
  +PoiName: string
  +PriorityLevel: int
  +PriorityType: string
  +DistanceMeters: double
  +FinalPriorityScore: double
  +TriggeredAt: DateTime
  +ExpiresAt: DateTime?
  +AllowInterrupt: bool
  +IsTapBoosted: bool
}

class PriorityResolverOptions {
  +MidZoneThresholdMeters: double = 5
  +TapBoost: double = 5
  +DistanceNearBonus: double = 3
  +CooldownPenalty: double = 10
  +TourBoost: double = 2
  +EnableTourBoost: bool = true
}

class PoiPriorityResolver {
  -_options: PriorityResolverOptions
  +PoiPriorityResolver(options)
  +Resolve(candidates, nowUtc, currentTourStep?): List<NarrationQueueItem>
  -CalcScore(candidate, nowUtc, tourStep?): double
}

class NarrationQueueManager {
  +CurrentState: NarrationState
  +Queue: IReadOnlyList<NarrationQueueItem>
  +CurrentPlaying: NarrationQueueItem?
  +PlayRequested: Action<NarrationQueueItem>
  +QueueEmpty: Action
  +EnqueueRange(items): void
  +TryInterrupt(incoming): bool
  +CompleteCurrentAndPlayNext(): NarrationQueueItem?
  +SkipCurrent(): void
  +RemoveExpired(nowUtc): void
  +Clear(): void
}

class NarrationManager {
  -_queue: Queue<QueueItem>
  -_workerTask: Task
  +HandleAsync(announcement, overrideText?, ct): Task
  +Stop(): void
  -PlayOneAsync(item, ct): Task
  -SelectOutput(poi, lang): (audioUrl?, text?)
  -ComposeFallbackText(poi, lang): string
}

interface INarrationManager {
  +HandleAsync(announcement, overrideText?, ct): Task
  +Stop(): void
}

NarrationManager ..|> INarrationManager
NarrationManager --> NarrationQueueManager : uses
NarrationQueueManager --> NarrationQueueItem : manages
PoiPriorityResolver --> PriorityResolverOptions : configured by
PoiPriorityResolver --> PoiCandidate : resolves
PoiPriorityResolver --> NarrationQueueItem : produces
Announcement --> POI : references
Announcement --> PoiEventType : has
NarrationQueueManager --> NarrationState : tracks

@enduml
```

---

## 4. SEQUENCE DIAGRAM – GEOFENCE → NARRATION

```plantuml
@startuml Sequence_Geofence_Narration
skinparam sequenceArrowThickness 2
skinparam sequenceParticipantBackgroundColor #1a2a3a
skinparam sequenceParticipantBorderColor #1976D2
skinparam sequenceFontColor white
skinparam noteBackgroundColor #2a3a4a

actor "Du khách" as User
participant "LocationService\n(GPS)" as GPS
participant "AndroidGeofence\nService" as GeoSvc
participant "GeofenceEvent\nGate" as Gate
participant "MapPage" as Map
participant "PoiNarration\nApiClient" as Api
participant "NarrationManager" as NM
participant "AudioPlayer\n/ TTS" as Audio
participant "PlaybackApi\nClient" as Log

User -> GPS : Di chuyển đến POI

GPS -> GeoSvc : Cập nhật vị trí
GeoSvc -> GeoSvc : Android Geofence\ndetects ENTER/DWELL
GeoSvc -> Gate : OnPoiEvent(poi, type)

note over Gate
  Kiểm tra:
  1. Debounce 3 giây
  2. Cooldown POI
end note

alt Cho phép phát
  Gate -> Map : OnGeofenceEvent(poi, ENTER/NEAR)
  Map -> Map : HighlightPoi(poi)\nShowDetail(poi)
  
  Map -> Api : GetNarrationAsync(poiId, lang, eventType)
  
  alt Cache hit
    Api --> Map : NarrationText (từ cache)
  else Cache miss
    Api -> Api : GET /api/v1/pois/{id}/narration\n?lang=vi-VN&eventType=0
    Api --> Map : NarrationText (từ server)
  end
  
  Map -> NM : HandleAsync(Announcement)
  
  NM -> NM : Check duplicate (2s window)
  NM -> NM : Sort by priority\n(Tap > Enter > Near)
  
  alt AudioUrl tồn tại
    NM -> Audio : PlayFileAsync(audioUrl)
  else
    NM -> Audio : SpeakAsync(narrationText, locale)
  end
  
  Audio --> NM : Phát xong
  NM -> Log : LogAsync(poiId, "Enter", duration)
  
else Bị chặn (cooldown / debounce)
  Gate -> Gate : Bỏ qua sự kiện
end

@enduml
```

---

## 5. SEQUENCE DIAGRAM – QR SCAN (TRONG APP) → NARRATION

```plantuml
@startuml Sequence_QrScan_App
skinparam sequenceArrowThickness 2
skinparam sequenceParticipantBackgroundColor #1a2a3a
skinparam sequenceParticipantBorderColor #4CAF50
skinparam sequenceFontColor white

actor "Du khách" as User
participant "QrScanPage\n(Camera)" as QrPage
participant "ExtractQrData\n(Parser)" as Parser
participant "TicketApiClient\n/ PoiDatabase" as DB
participant "NarrationManager" as NM
participant "PlaybackApiClient" as Log

User -> QrPage : Mở camera, quét QR

QrPage -> QrPage : OnBarcodesDetected()
QrPage -> QrPage : Check quota\n(DailyUsageTracking)

QrPage -> Parser : ExtractQrData(rawValue)
note over Parser
  Parse pattern:
  - /p/{id}?lang=xx → PoiId + Lang
  - Ticket URL → TicketId
  - Other URL → Browser
end note
Parser --> QrPage : (TicketId?, PoiId?, Lang)

alt URL thường (http/https)
  QrPage -> QrPage : Launcher.OpenAsync(uri)
  note right: Mở trình duyệt → Web Landing

else QR POI (poiId detected)
  QrPage -> DB : GetByIdAsync(poiId)
  
  alt POI có trong local DB
    DB --> QrPage : POI data
    QrPage -> NM : HandleAsync(\n  Announcement(poi, lang, Tap)\n)
    NM -> NM : Play audio / TTS
    NM --> QrPage : Done
  else POI không tồn tại
    QrPage -> QrPage : Launcher.OpenAsync(\n  http://server/p/{id}?lang=xx\n)
  end

else Ticket QR
  QrPage -> DB : ScanTicketAsync(ticketCode)
  note over DB : POST /api/v1/tickets/{code}/scan\nKiểm tra & tăng CurrentUses
  DB --> QrPage : { PoiId, Lang, Remaining }
  
  QrPage -> DB : GetByIdAsync(poiId)
  QrPage -> NM : HandleAsync(Announcement)
  NM -> NM : Play narration
end

QrPage -> Log : LogAsync(poiId, "QR_SCAN")
QrPage -> QrPage : Đóng camera

@enduml
```

---

## 6. SEQUENCE DIAGRAM – QR SCAN (BROWSER) → WEB LANDING → TTS

```plantuml
@startuml Sequence_WebLanding
skinparam sequenceArrowThickness 2
skinparam sequenceParticipantBackgroundColor #1a2a3a
skinparam sequenceParticipantBorderColor #FF9800
skinparam sequenceFontColor white

actor "Du khách\n(Browser)" as User
participant "Trình duyệt\n(Browser)" as Browser
participant "LandingController\n(ASP.NET)" as API
database "SQL Server\n(AppDb)" as DB
participant "Web Speech API\n(Browser TTS)" as TTS

User -> Browser : Quét QR bằng camera
Browser -> API : GET /p/{poiId}?lang=vi-VN
API --> Browser : HTML page (embedded JS)

Browser -> Browser : init() – render page

Browser -> API : GET /api/public/poi/{id}?lang=vi-VN
API -> DB : Query Pois + PoiLanguage\n+ PoiMedia
DB --> API : POI data
API --> Browser : { name, description, imageUrl,\n  audioUrl?, narrationText? }
Browser -> Browser : Hiển thị thông tin POI

User -> Browser : Nhấn "Nghe thuyết minh"
Browser -> Browser : Lấy deviceKey\ntừ localStorage (_dk)

Browser -> API : POST /api/public/poi/{id}/play-narration\nBody: { deviceKey, lang }
API -> DB : Query/Create WebNarrationUsage\n(deviceKey + poiId)
API -> DB : Check playCount >= 3 (quota)

alt Còn quota (playCount < 3)
  API -> DB : UPDATE playCount++
  API -> DB : INSERT Analytics_Visit
  API --> Browser : { allowed: true, audioUrl?,\n  narrationText?, playCount, quota: 3 }
  
  Browser -> Browser : Cập nhật thanh quota
  
  alt audioUrl có sẵn
    Browser -> Browser : <audio>.play(audioUrl)
  else
    Browser -> TTS : speechSynthesis.speak(\n  narrationText, lang\n)
    TTS --> Browser : Phát âm thanh
  end
  
  Browser -> Browser : Hiển thị nội dung\nthuyết minh

else Hết quota (playCount >= 3)
  API --> Browser : { allowed: false,\n  message: "Đã hết lượt nghe" }
  Browser -> Browser : Hiện nút "Tải App"
  User -> Browser : Nhấn "Tải / Mở App"
  Browser -> Browser : intent://poi/{id}?lang=xx\n#Intent;scheme=smarttourism;\npackage=com.companyname.mauiapp1;\nS.browser_fallback_url=...;end
  note right: Nếu app đã cài → mở app\nNếu chưa → tải APK
end

@enduml
```

---

## 7. ACTIVITY DIAGRAM – GEOFENCE DETECTION FLOW

```plantuml
@startuml Activity_Geofence
skinparam activityBackgroundColor #1a2a3a
skinparam activityBorderColor #1976D2
skinparam activityFontColor white
skinparam arrowColor #64b5f6

start

:GPS cập nhật vị trí người dùng;

:Tính khoảng cách đến tất cả POI;

if (Có POI trong vùng radius?) then (Có)
  :Android Geofence API\nphát hiện ENTER / DWELL;
  
  :GeofenceEventGate nhận sự kiện;
  
  if (Trong vòng 3 giây từ lần trước?) then (Có - Debounce)
    :Bỏ qua sự kiện;
    stop
  else (Không)
  end if
  
  if (POI còn trong cooldown\n(CooldownSeconds)?) then (Có)
    :Bỏ qua sự kiện;
    stop
  else (Không)
  end if
  
  :MapPage.OnGeofenceEvent(poi, type);
  
  :Cập nhật UI\n(Highlight POI, Show Detail);
  
  :Fetch narration text\ntừ API / Cache;
  
  if (Có narration text?) then (Có)
    :Tạo Announcement(poi, lang, Enter/Near);
    
    :NarrationManager.HandleAsync();
    
    if (Có AudioUrl?) then (Có)
      :AudioPlayer.PlayFileAsync();
    else (Không)
      :TextToSpeech.SpeakAsync();
    end if
    
    :Chờ phát xong;
    :Log analytics;
    
  else (Không)
    :Bỏ qua (không có nội dung);
  end if
  
else (Không)
  :Tiếp tục theo dõi GPS;
end if

stop

@enduml
```

---

## 8. ACTIVITY DIAGRAM – NARRATION RESOLUTION FLOW

```plantuml
@startuml Activity_NarrationResolution
skinparam activityBackgroundColor #1a2a3a
skinparam activityBorderColor #4CAF50
skinparam activityFontColor white
skinparam arrowColor #A5D6A7

start

:Nhận Announcement\n(poi, lang, eventType);

:Kiểm tra duplicate\n(window 2 giây);

if (Trùng với announcement vừa phát?) then (Có)
  :Bỏ qua;
  stop
else (Không)
end if

:Thêm vào hàng đợi NarrationQueue;

:Sắp xếp theo FinalPriorityScore\n= PriorityLevel × 10\n  + TapBoost (nếu Tap)\n  + DistanceBonus (nếu < 10m)\n  - CooldownPenalty (nếu trong cooldown)\n  + TourBoost (nếu đang trong tour);

if (Đang phát bài khác?) then (Có)
  if (FinalPriorityScore > CurrentPlaying.Score\nVÀ AllowInterrupt = true?) then (Có - Interrupt)
    :Dừng bài đang phát;
    :Phát bài ưu tiên cao hơn;
  else (Không - Chờ hàng đợi)
    :Đợi đến lượt;
  end if
else (Không)
  :Bắt đầu phát ngay;
end if

if (POI có ProAudioUrl\n(ngôn ngữ khớp)?) then (Có)
  :Phát audio file (URL);
else if (POI có TextToSpeech?) then (Có)
  :TextToSpeech (MAUI)\nvới locale khớp ngôn ngữ;
else (Không có gì)
  :Tạo text fallback:\n"Bạn đang ở gần [Tên POI]";
  :TextToSpeech fallback text;
end if

:Phát xong → cập nhật LastPlayedAt;
:Gọi CompleteCurrentAndPlayNext();

if (Queue còn item?) then (Có)
  :Phát item tiếp theo;
else (Không)
  :State → Idle;
  :QueueEmpty event;
end if

stop

@enduml
```

---

## 9. DATABASE ER DIAGRAM

```plantuml
@startuml ERDiagram
skinparam classBackgroundColor #1a2a3a
skinparam classBorderColor #FF9800
skinparam classFontColor white
skinparam arrowColor #FFB74D

entity "Pois" as Pois {
  * Id : int <<PK>>
  --
  Name : nvarchar(200)
  Latitude : float
  Longitude : float
  RadiusMeters : int
  CooldownSeconds : int
  PriorityLevel : int
  IsActive : bit
}

entity "PoiLanguage" as PoiLang {
  * IdLang : bigint <<PK>>
  --
  * IdPoi : int <<FK>>
  LanguageTag : nvarchar(10)
  TextToSpeech : nvarchar(4000)
  ProAudioUrl : nvarchar(1000)
}

entity "PoiMedia" as PoiMedia {
  * Idm : bigint <<PK>>
  --
  * IdPoi : int <<FK>>
  Image : nvarchar(1000)
  MapLink : nvarchar(1000)
}

entity "PoiImages" as PoiImages {
  * Id : bigint <<PK>>
  --
  * IdPoi : int <<FK>>
  ImageUrl : nvarchar(1000)
  SortOrder : int
}

entity "PoiQrCodes" as PoiQr {
  * Id : bigint <<PK>>
  --
  * PoiId : int <<FK>>
  PublicUrl : nvarchar(1000)
  QrPayload : nvarchar(2000)
  IsActive : bit
}

entity "PoiTickets" as Tickets {
  * TicketCode : varchar(50) <<PK>>
  --
  * IdPoi : int <<FK>>
  LanguageTag : nvarchar(10)
  MaxUses : int
  CurrentUses : int
}

entity "WebNarrationUsage" as WebUsage {
  * Id : bigint <<PK>>
  --
  * PoiId : int <<FK>>
  DeviceKey : nvarchar(200)
  PlayCount : int
  LastPlayedAt : datetime2
}

entity "Users" as Users {
  * UserId : uniqueidentifier <<PK>>
  --
  Username : nvarchar(100)
  Mail : nvarchar(200)
  PasswordHash : nvarchar(256)
  PlanType : varchar(20)
  ProExpiryDate : datetime2
}

entity "HistoryPoi" as History {
  * Id : bigint <<PK>>
  --
  * IdPoi : int <<FK>>
  * IdUser : uniqueidentifier <<FK>>
  Quantity : int
  TotalDurationSeconds : int
  LastVisitedAt : datetime2
}

entity "Analytics_Visit" as Analytics {
  * Id : bigint <<PK>>
  --
  * PoiId : int <<FK>>
  Action : nvarchar(20)
  Timestamp : datetime2
}

entity "DailyUsageTracking" as Usage {
  * EntityId : varchar(100) <<PK>>
  * ActionType : varchar(20) <<PK>>
  --
  UsedCount : int
  LastResetAt : datetime2
}

entity "GuestDevices" as Devices {
  * DeviceId : varchar(100) <<PK>>
  --
  Platform : nvarchar(20)
  AppVersion : nvarchar(20)
  LastActiveAt : datetime2
}

Pois ||--o{ PoiLang : "has"
Pois ||--o| PoiMedia : "has"
Pois ||--o{ PoiImages : "gallery"
Pois ||--o{ PoiQr : "has QR"
Pois ||--o{ Tickets : "has"
Pois ||--o{ WebUsage : "web quota"
Pois ||--o{ Analytics : "tracked"
Pois ||--o{ History : "visited"
Users ||--o{ History : "has"

@enduml
```

---

## 10. COMPONENT / DEPLOYMENT DIAGRAM

```plantuml
@startuml Deployment_Architecture
skinparam componentBackgroundColor #1a2a3a
skinparam componentBorderColor #1976D2
skinparam componentFontColor white
skinparam nodeBackgroundColor #0d1117
skinparam nodeBorderColor #444
skinparam nodeFontColor #ccc
skinparam arrowColor #64b5f6

node "Điện thoại Android\n(Du khách)" as Phone {
  component "MAUI App\n(MauiApp1)" as App {
    component "MapPage\n(GPS + Geofence)" as MapComp
    component "QrScanPage\n(Camera)" as QrComp
    component "NarrationManager\n(Audio + TTS)" as NarComp
    component "SQLite DB\n(PoiDatabase)" as SQLite
  }
}

node "PC Quản trị\n(Admin)" as AdminPC {
  component "Web Browser\n(admin.html)" as AdminWeb
}

node "Điện thoại\n(Du khách - Web)" as WebPhone {
  component "Mobile Browser\n(Chrome / Safari)" as Browser
}

node "Server\n(Windows / Linux)" as Server {
  component "ASP.NET Core\n(MapApi :5150)" as API {
    component "LandingController" as LC
    component "PoiController" as PC
    component "PoiMediaController" as PMC
  }
  database "SQL Server\n(GpsApi)" as SQLDB
  folder "wwwroot" as Static {
    component "admin.html" as AdminHtml
    component "downloads/app.apk" as APK
    component "p/{id} landing" as Landing
  }
}

App --> API : REST API\nHTTP :5150
AdminWeb --> API : REST API (Admin)\nHTTP :5150
Browser --> API : GET /p/{id}?lang=xx\nPOST /play-narration

API --> SQLDB : EF Core\nSQL Server

AdminWeb --> AdminHtml : load
Browser --> Landing : QR redirect
Phone --> APK : Download / DeepLink

@enduml
```

---

## 11. BẢNG TÓM TẮT CHO BẢO VỆ ĐỒ ÁN

### Công nghệ sử dụng

| Thành phần | Công nghệ | Mục đích |
|---|---|---|
| Mobile App | .NET MAUI (C#) | Cross-platform, Android target |
| Backend API | ASP.NET Core Minimal API | REST endpoints |
| Database | SQL Server + EF Core | Lưu trữ dữ liệu |
| Map | Google Maps SDK | Bản đồ + POI markers |
| Geofence | Android Geofence API | Phát hiện vị trí tự động |
| Audio | MAUI Audio + TTS | Phát thuyết minh |
| Web TTS | Web Speech API | Thuyết minh trên trình duyệt |
| QR Code | ZXing (MAUI) + QRCoder | Tạo và đọc QR |
| Deep Link | Android Intent URL | Mở app từ browser |
| Auth | JWT Bearer Token | Xác thực người dùng |
| Offline | SQLite (local DB) + Sync | Hoạt động không có mạng |
| Notification | NotificationCompat (Android) | Push notification POI mới |

### Chức năng đã triển khai

| # | Chức năng | Trạng thái |
|---|---|---|
| 1 | GPS tracking & Geofence tự động | ✅ Hoàn thành |
| 2 | Phát thuyết minh Audio / TTS | ✅ Hoàn thành |
| 3 | Narration Queue + Priority Engine | ✅ Hoàn thành |
| 4 | QR Code tích hợp (App + Browser) | ✅ Hoàn thành |
| 5 | Web Landing Page (không cần app) | ✅ Hoàn thành |
| 6 | Web Speech API TTS fallback | ✅ Hoàn thành |
| 7 | Deep Link (QR → mở app) | ✅ Hoàn thành |
| 8 | Đa ngôn ngữ (vi, en, ja, ko, de) | ✅ Hoàn thành |
| 9 | Offline-first (SQLite cache) | ✅ Hoàn thành |
| 10 | Analytics (visit, duration, route) | ✅ Hoàn thành |
| 11 | Freemium / Pro plan | ✅ Hoàn thành |
| 12 | Web Admin CMS | ✅ Hoàn thành |
| 13 | Gallery quản lý ảnh POI | ✅ Hoàn thành |
| 14 | Đổi mật khẩu (Profile) | ✅ Hoàn thành |
| 15 | Push Notification POI mới | ✅ Hoàn thành |
| 16 | QR Quota (3 lượt/thiết bị/POI) | ✅ Hoàn thành |
| 17 | APK download + Auto-install | ✅ Hoàn thành |

---

> **Hướng dẫn render**: Copy từng block `@startuml ... @enduml` vào https://www.plantuml.com/plantuml/uml/ để xem diagram.
> Hoặc cài extension **PlantUML** trong VS Code (cần Java + Graphviz).
