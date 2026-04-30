# Thiết Kế Hệ Thống GpsGeoFenceApp - UML & Workflow

Tài liệu này cung cấp cái nhìn tổng quan và chi tiết về toàn bộ các module trong hệ thống **GpsGeoFenceApp**, đi kèm là các sơ đồ UML (Use Case, Activity, Sequence) được viết bằng định dạng Markdown (Mermaid) để dễ dàng sao chép và hiển thị.

---

## 1. Hệ thống hiện tại có bao nhiêu module?

Hệ thống hiện tại được thiết kế theo kiến trúc tối ưu hóa với tổng cộng **12 module**, chia làm 2 nhóm:
* **9 Module nghiệp vụ chính**: Xử lý các tính năng cốt lõi cho trải nghiệm người dùng cuối (End-user) và luồng dữ liệu chính.
* **3 Module hỗ trợ vận hành**: Quản lý hạn mức, kiểm soát người dùng và giám sát hệ thống.

**Danh sách 9 module nghiệp vụ chính:**
1. GPS Tracking (Realtime + Background)
2. Geofence Engine
3. Narration Engine (TTS/Audio)
4. POI Data Management (Sync + Local DB)
5. Map View (Mobile UI)
6. Web CMS / Admin
7. Analytics
8. QR Trigger (Narration QR + App Download QR)
9. Core Workflow Orchestrator (Sync -> Track -> Detect -> Execute -> Log)

**Danh sách 3 module hỗ trợ vận hành:**
10. Auth + Profile + Plan (FREE/PRO)
11. Guest Device Presence + Realtime Monitor
12. Freemium Usage Gate

---

## 2. Thiết kế Use Case Tổng Quan Hệ Thống

Sơ đồ Use Case tổng quan mô tả các Actor tham gia vào hệ thống và các chức năng (Use Case) chính mà họ có thể thao tác.

```mermaid
flowchart LR
    Guest(["Guest User"])
    User(["Registered User"])
    Pro(["PRO User"])
    Admin(["Admin"])
    
    subgraph System ["GPS GeoFence Tourism System"]
        UC1(["Track location (GPS)"])
        UC2(["View map & nearby POI"])
        UC3(["Listen narration (Auto/QR)"])
        UC4(["Scan QR Code"])
        UC5(["Search POI"])
        UC6(["View travel history"])
        UC7(["Manage POI/Tour/QR"])
        UC8(["View analytics dashboard"])
        UC9(["Manage users/devices"])
    end
    
    Guest --> UC1
    Guest --> UC2
    Guest --> UC3
    Guest --> UC4
    Guest --> UC5
    
    User --> UC1
    User --> UC2
    User --> UC3
    User --> UC4
    User --> UC5
    
    Pro --> UC1
    Pro --> UC2
    Pro --> UC3
    Pro --> UC4
    Pro --> UC5
    Pro --> UC6
    
    Admin --> UC7
    Admin --> UC8
    Admin --> UC9
```

---

## 3. Chi Tiết Từng Module & Thiết Kế UML (Mermaid)

Dưới đây là mô tả, luồng hoạt động và các biểu đồ Activity / Sequence cho từng module trọng tâm.

### Module 1: GPS Tracking

* **Mô tả:** Phân hệ cốt lõi chịu trách nhiệm thu thập vị trí thiết bị liên tục, hoạt động ổn định ở cả trạng thái ứng dụng đang mở (Foreground) và đang thu nhỏ (Background).
* **Luồng hoạt động:**
  1. Ứng dụng xin quyền cấp vị trí từ người dùng.
  2. Bắt đầu tracking khi được cấp quyền.
  3. Khi App bị thu nhỏ (Sleep), chuyển sang chạy Background Service.
  4. Trả tọa độ liên tục cho các module Bản đồ và Geofence.

**Activity Diagram:**
```mermaid
flowchart TD
    Start([Bắt đầu]) --> ReqPerm{Đã cấp quyền Location?}
    ReqPerm -- "Chưa" --> Ask[Xin quyền Location]
    Ask --> ReqPerm
    ReqPerm -- "Rồi" --> StartFore[Start Foreground Tracking]
    StartFore --> AppState{Trạng thái App?}
    AppState -- "Sleep (Background)" --> StartBG[Start Background Service]
    StartBG --> EmitLoc[Cập nhật tọa độ liên tục]
    AppState -- "Resume (Foreground)" --> StopBG[Stop Background Service]
    StopBG --> StartFore
    EmitLoc --> AppState
```

**Sequence Diagram:**
```mermaid
sequenceDiagram
    participant U as User
    participant A as Mobile App
    participant P as Permission API
    participant LS as LocationService
    participant BG as BackgroundRuntime
    
    U->>A: Mở App
    A->>P: Yêu cầu quyền Location
    P-->>A: Cấp quyền
    A->>LS: StartTracking() (Foreground)
    loop Cập nhật liên tục
        LS-->>A: Tọa độ mới
    end
    U->>A: Thu nhỏ App (Sleep)
    A->>BG: StartAsync()
    loop Tracking ngầm
        BG-->>A: Tọa độ mới
    end
    U->>A: Mở lại App (Resume)
    A->>BG: StopAsync()
```

---

### Module 2: Geofence Engine

* **Mô tả:** Động cơ xử lý không gian, tạo "hàng rào ảo" xung quanh các điểm tham quan (POI) để phát hiện khi người dùng bước vào, đi ra hoặc đến gần.
* **Luồng hoạt động:**
  1. Tải danh sách các POI và bán kính (Radius).
  2. Đăng ký thông số này xuống hệ điều hành.
  3. Hệ điều hành bắn ra event khi phát hiện vi phạm hàng rào (Enter/Near).
  4. Hệ thống kiểm tra chống Spam (Cooldown/Debounce) trước khi quyết định kích hoạt sự kiện.

**Activity Diagram:**
```mermaid
flowchart TD
    A([Nhận Location Mới]) --> B[Kiểm tra tọa độ so với danh sách POI]
    B --> C{Xâm nhập vùng POI?}
    C -- "Có (Enter/Near)" --> D[Kích hoạt Event Transition]
    C -- "Không" --> E([Bỏ qua/Đợi])
    D --> F{Kiểm tra Anti-Spam (Cooldown)}
    F -- "Hợp lệ" --> G[Gửi Event kích hoạt UI & Audio]
    F -- "Spam" --> E
```

**Sequence Diagram:**
```mermaid
sequenceDiagram
    participant M as MapPage
    participant GS as GeofenceService
    participant OS as Mobile OS
    participant G as EventGate
    
    M->>GS: RegisterAsync(danh sách POI)
    GS->>OS: Đăng ký vùng ảo (Geofence)
    Note over OS: Người dùng di chuyển vào vùng
    OS->>GS: Bắn sự kiện Transition (Enter)
    GS->>G: ShouldAccept() (Kiểm tra Cooldown)
    alt Hợp lệ
        G-->>GS: True
        GS->>M: OnPoiEvent() -> Xử lý tiếp
    else Bị chặn (Spam)
        G-->>GS: False (Bỏ qua)
    end
```

---

### Module 3: Narration Engine (Thuyết minh tự động)

* **Mô tả:** Phân hệ chịu trách nhiệm phát âm thanh hướng dẫn viên (file MP3 tải sẵn hoặc dùng TTS - Text To Speech) khi người dùng đến điểm tham quan.
* **Luồng hoạt động:**
  1. Nhận yêu cầu phát âm thanh.
  2. Đưa vào hàng chờ (Queue) để tránh chồng chéo tiếng.
  3. Kiểm tra xem có file Audio thật không, nếu không sẽ dùng TTS đọc kịch bản.
  4. Phát âm thanh, log lại thời gian nghe thực tế.

**Activity Diagram:**
```mermaid
flowchart TD
    Start([Nhận yêu cầu thuyết minh]) --> EnQ[Thêm vào hàng đợi (Queue)]
    EnQ --> CheckQ{Hàng đợi rỗng?}
    CheckQ -- "Không" --> Worker[Lấy Audio/Text tiếp theo]
    Worker --> CheckFile{Có sẵn file MP3?}
    CheckFile -- "Có" --> PlayAudio[Phát Audio File]
    CheckFile -- "Không" --> TTS[Sử dụng TTS đọc Text]
    PlayAudio --> Log[Log dữ liệu thời gian nghe]
    TTS --> Log
    Log --> CheckQ
    CheckQ -- "Có" --> End([Chờ sự kiện mới])
```

**Sequence Diagram:**
```mermaid
sequenceDiagram
    participant UI as MapPage/QrScan
    participant NM as NarrationManager
    participant Q as QueueWorker
    participant P as AudioPlayer/TTS
    participant API as Analytics API
    
    UI->>NM: Yêu cầu thuyết minh (POI x)
    NM->>Q: Enqueue (Xếp hàng)
    Q->>P: Bắt đầu Play()
    Note over P: Hệ thống đang phát tiếng...
    P-->>Q: Hoàn tất phát thanh
    Q->>API: Log(POI x, Duration) lên Server
```

---

### Module 4: POI Data Management

* **Mô tả:** Chịu trách nhiệm đồng bộ và lưu trữ (Offline) toàn bộ dữ liệu về điểm tham quan (Tọa độ, mô tả, hình ảnh, file âm thanh) từ máy chủ xuống.
* **Luồng hoạt động:**
  1. App khởi động, hỏi server xem có phiên bản dữ liệu mới không.
  2. Nếu có, kéo toàn bộ JSON về và lưu vào SQLite (Upsert).
  3. Cung cấp dữ liệu cache cho Map và Narration hoạt động ngay lập tức mà không cần mạng.

**Activity Diagram:**
```mermaid
flowchart TD
    Start([Kiểm tra version dữ liệu]) --> Check{Version Mới?}
    Check -- "Có" --> Fetch[GET All POIs từ Server]
    Fetch --> Save[Cập nhật vào Local SQLite]
    Save --> Cache[Cache thông tin Narration/Hình ảnh]
    Cache --> Ready([Hoàn tất đồng bộ])
    Check -- "Không" --> Ready
```

---

### Module 5: Map View

* **Mô tả:** Giao diện bản đồ hiển thị trực quan các điểm tham quan và vị trí của người dùng.
* **Luồng hoạt động:**
  1. Khởi tạo bản đồ, load tất cả POI từ SQLite.
  2. Vẽ Marker và Circle đại diện cho Geofence.
  3. Theo dõi "Chấm xanh" (Blue dot) của User.
  4. Người dùng bấm (Tap) vào Marker để xem chi tiết hoặc hệ thống tự highlight khi lại gần.

**Activity Diagram:**
```mermaid
flowchart TD
    Start([Khởi tạo Map]) --> LoadPOI[Load POI & Vẽ Marker/Circle]
    LoadPOI --> Track[Cập nhật Blue dot vị trí user]
    Track --> WaitEvent{Sự kiện tương tác}
    WaitEvent -- "Tap Marker" --> ShowDetail[Mở Pop-up & Gọi Narration Engine]
    WaitEvent -- "Đến gần POI" --> Highlight[Đổi màu Marker gần nhất]
```

---

### Module 6: Web CMS / Admin

* **Mô tả:** Cổng quản trị dành cho ban quản lý để cập nhật nội dung POI, quản lý User, xem biểu đồ lượng khách.
* **Luồng hoạt động:**
  1. Admin đăng nhập và lấy Token.
  2. Chọn các chức năng thêm/sửa/xóa POI.
  3. Hệ thống Backend ghi nhận và lưu DB.

**Sequence Diagram:**
```mermaid
sequenceDiagram
    participant Admin as Admin UI
    participant API as Admin API
    participant DB as SQL Server
    
    Admin->>API: Yêu cầu cập nhật POI
    API->>DB: Thực thi Update Database
    DB-->>API: Success
    API-->>Admin: OK 200 (Refresh giao diện)
```

---

### Module 7: Analytics

* **Mô tả:** Thu thập dữ liệu sử dụng một cách âm thầm để vẽ Dashboard và Heatmap (Bản đồ nhiệt) cho Admin.
* **Luồng hoạt động:** Các App sinh ra Log dạng Async (không làm chậm trải nghiệm), sau đó bắn ngầm lên Server để DB tổng hợp.

**Sequence Diagram:**
```mermaid
sequenceDiagram
    participant App as Mobile App
    participant API as Backend API
    participant DB as Analytics Table
    participant Admin as Admin Dashboard
    
    App->>API: POST /analytics/visit (Ẩn danh, không block UI)
    API->>DB: Insert Log
    Admin->>API: GET /analytics/dashboard
    API->>DB: Truy vấn dữ liệu tổng hợp
    DB-->>API: Chart Data
    API-->>Admin: Vẽ biểu đồ và Heatmap
```

---

### Module 8: QR Trigger

* **Mô tả:** Kích hoạt chức năng Tải App hoặc Nghe Thuyết Minh nhanh thông qua việc dùng Camera quét mã QR dán tại các trạm thực tế.
* **Luồng hoạt động:**
  1. Quét mã QR -> Phân tích.
  2. Nếu là QR Ticket tải nội dung: Check số lượt -> Phát Audio.
  3. Nếu là QR Tải App: Detect hệ điều hành -> Redirect sang CH Play / App Store.

**Activity Diagram:**
```mermaid
flowchart TD
    Scan([Camera quét QR]) --> Parse[Phân tích chuỗi URL/Payload]
    Parse --> CheckType{Phân loại QR}
    CheckType -- "Vé Nghe (Ticket)" --> Verify[Xác thực số lượt sử dụng]
    Verify --> Valid{Hợp lệ?}
    Valid -- "Yes" --> Play[Phát Audio POI]
    Valid -- "No" --> Deny[Cảnh báo hết hạn]
    CheckType -- "Link Tải App" --> Detect[Kiểm tra User-Agent (iOS/Android)]
    Detect --> Redirect[Chuyển hướng vào Store tương ứng]
```

---

### Module 9: Core Workflow Orchestrator

* **Mô tả:** Là luồng kết hợp tất cả các module trên, biến chúng thành vòng lặp tự động (Auto Workflow) hoàn hảo.
* **Luồng hoạt động:** Trình tự diễn ra khi một khách du lịch đi tham quan.

**Activity Diagram:**
```mermaid
flowchart LR
    Sync[1. Sync POI] --> Track[2. GPS Tracking]
    Track --> Detect[3. Phát hiện Geofence]
    Detect --> Exec[4. Phát Audio Narration]
    Exec --> Log[5. Bắn log Analytics]
    Log --> Track
```

---
### Các Module Hỗ Trợ Vận Hành (Module 10, 11, 12)

Đối với các module hỗ trợ quản trị, hệ thống áp dụng luồng hoạt động đơn giản như sau:

* **Module 10 (Auth & Plan):**
  * Đăng nhập lấy Token -> Check quyền tài khoản -> Cấp quyền sử dụng dựa trên gói (FREE/PRO).
* **Module 11 (Guest Device Presence & Realtime Monitor):**
  * **Mô tả:** Hệ thống theo dõi trạng thái online/offline của thiết bị khách và hiển thị theo thời gian thực trên bản đồ quản trị (Admin CMS).
  * **Luồng hoạt động:**
    1. Ứng dụng Mobile tự tạo Device ID hoặc sử dụng tài khoản để định danh.
    2. Ứng dụng tự động gửi tín hiệu "Ping" (Heartbeat) định kỳ kèm theo tọa độ hiện tại lên Server.
    3. Backend nhận Heartbeat và cập nhật trạng thái hoạt động (Last Active) của thiết bị trong cơ sở dữ liệu hoặc bộ nhớ đệm (Cache/Redis).
    4. Admin CMS lắng nghe luồng dữ liệu này để hiển thị trạng thái "Đang online" cũng như vị trí hiện tại trên bản đồ quản trị.

**Activity Diagram (Module 11):**
```mermaid
flowchart TD
    Start([Ứng dụng Mobile Khởi động]) --> GenID[Lấy Device ID / Định danh]
    GenID --> LoopHeartbeat[Bắt đầu vòng lặp gửi Heartbeat]
    LoopHeartbeat --> SendPing[Gửi tín hiệu Ping & Tọa độ định kỳ]
    SendPing --> ServerRcv[Server nhận tín hiệu]
    ServerRcv --> UpdateStatus[Cập nhật trạng thái 'Online' & 'Last Active']
    UpdateStatus --> AdminCMS[Admin CMS truy vấn/nhận Realtime data]
    AdminCMS --> DisplayMap[Hiển thị thiết bị trên Bản đồ Quản trị]
    SendPing -. "Chờ Timeout..." .-> LoopHeartbeat
```

**Sequence Diagram (Module 11):**
```mermaid
sequenceDiagram
    participant App as Mobile App
    participant Hub as Device Hub (Backend)
    participant Cache as Database / Redis
    participant Admin as Admin Web
    
    App->>App: Khởi tạo Device ID
    loop Mỗi 30-60 giây
        App->>Hub: Gửi Heartbeat (Ping + Location)
        Hub->>Cache: Cập nhật LastActive = Now()
    end
    
    Admin->>Hub: Theo dõi trạng thái hệ thống
    loop Realtime Update
        Cache-->>Hub: Truy vấn dữ liệu thiết bị Active
        Hub-->>Admin: Gửi tọa độ & trạng thái Online
        Admin->>Admin: Cập nhật vị trí trên Bản đồ
    end
```
* **Module 12 (Freemium Gate):**
  * Khi User (FREE) chuẩn bị nghe Audio -> Gửi API check giới hạn (Quota) -> Nếu còn lượt: Trừ 1 lượt & Phát Audio; Nếu hết: Bật popup mời mua bản quyền (PRO).

---
*Ghi chú: Toàn bộ các đoạn code Mermaid trên bạn có thể chép thẳng vào bất cứ trình chỉnh sửa Markdown nào có hỗ trợ (như GitHub, GitLab, Obsidian, Notion) hoặc xem trực tiếp trên [Mermaid Live Editor](https://mermaid.live/) để xuất ra hình ảnh.*
