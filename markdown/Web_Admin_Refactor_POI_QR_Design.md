# Thiết kế chỉnh sửa Web Admin & QR Landing cho GpsGeoFenceApp

> Tài liệu mô tả phương án **refactor Web Admin**, **bảo trì tham chiếu backend**, **quản lý POI (CRUD + media)** và **thiết kế lại luồng QR code** cho dự án **GpsGeoFenceApp**.

---

## 1. Bối cảnh hệ thống hiện tại

Dự án `GpsGeoFenceApp` hiện có cấu trúc chính gồm:
- **Application (.NET MAUI)**: app mobile, có `Pages`, `PageModels`, `Services`, `MapPage`, `QrScanPage`, `ManageMetaPage`, ...
- **MapApi (ASP.NET Core + EF Core)**: backend API, dùng `AppDb`, minimal APIs trong `Program.cs`, một số controller như `PoiMediaController`, `TranslatorController`, `WeatherForecastController`.
- **SQL Server**: DB `GpsApi`, có các bảng như `Pois`, `PoiLanguage`, `PoiMedia`, `PoiImages`, `Tours`, `TourPois`, `Users`, `HistoryPoi`, `Analytics_*`, ...
- **Admin Web / CMS**: trong tài liệu kiến trúc tổng quan có mô tả lớp **Admin Web (wwwroot)** dùng để quản trị dữ liệu và dashboard.

### 1.1 Các điểm quan sát từ repo hiện tại
- Tài liệu tổng quan hệ thống mô tả **12 module**, trong đó có **Web CMS / Admin** và backend **ASP.NET Core + EF Core**. 
- `MapApi/Program.cs` đã có `DbContext<AppDb>`, retry SQL, CORS và một số endpoint `/api/v1/pois`, `/health`, ...
- `PoiMediaController` hiện hỗ trợ upload **audio** (`.mp3`, `.wav`) và **image** (`.jpg`, `.jpeg`, `.png`) với giới hạn dung lượng và cập nhật URL vào entity POI.
- `moduleadd.md` đã mô tả hướng mở rộng **multi-image gallery**, trong đó `PoiImages` đã có trong schema DB.

---

## 2. Mục tiêu chỉnh sửa

### 2.1 Mục tiêu chính
- Refactor code Web Admin để **dễ bảo trì**, **dễ mở rộng**, và **rõ luồng backend**.
- Chuẩn hóa tham chiếu giữa **frontend admin → backend API → database**.
- Hoàn thiện module **POI Management** gồm thêm / sửa / xóa, upload audio theo 2 cách (**URL hoặc file**), và chỉnh sửa phần thêm ảnh trong trang sửa POI.
- Thiết kế lại module **QR Code** để hỗ trợ 2 trường hợp:
  1. Quét bằng **camera điện thoại thường** → mở **website landing POI**.
  2. Quét bằng **camera trong app** → mở **thuyết minh POI trong app**.

---

## 3. Refactor Web Admin phục vụ bảo trì

## 3.1 Vấn đề hiện tại cần xử lý
- Logic admin và logic domain có nguy cơ bị trộn lẫn nếu frontend gọi trực tiếp quá nhiều endpoint rời rạc.
- Việc upload media (audio/image) hiện đã có ở backend nhưng cần thống nhất lại cách gọi từ admin.
- Chưa có mô hình rõ ràng cho **POI CRUD + language + media + QR + analytics** trên Web Admin.

## 3.2 Mục tiêu refactor
- Tách rõ các lớp:
  - **Presentation Layer** (Admin UI)
  - **Application/API Layer** (MapApi)
  - **Domain/Data Layer** (AppDb + SQL)
- Tạo các service phía admin để gom việc gọi API, tránh hardcode URL trong nhiều component.
- Chuẩn hóa DTO/request/response giữa admin và backend.

## 3.3 Kiến trúc đề xuất cho Web Admin
```text
[Admin UI]
   ├── Pages
   ├── Components
   ├── ViewModels / State
   └── ApiClient Services
            ↓
[MapApi / Backend]
   ├── POI Application Service
   ├── Media Service
   ├── QR Service
   ├── Landing Service
   └── Analytics Service
            ↓
[AppDb / SQL Server]
```

## 3.4 Gợi ý chia module phía Admin
### A. POI Management Module
- Danh sách POI
- Form tạo POI
- Form sửa POI
- Xóa POI
- Quản lý ngôn ngữ
- Quản lý audio
- Quản lý ảnh

### B. QR Management Module
- Sinh QR theo POI
- Xem landing link
- Chọn loại QR:
  - QR mở website landing
  - QR mở app flow (nếu app đã cài)

### C. Analytics Module
- Lượt quét QR
- Lượt phát thuyết minh web
- Lượt phát thuyết minh app
- Top POI được truy cập

### D. Settings / System Module
- Cấu hình số lượt phát miễn phí trên web (mặc định 3)
- Cấu hình URL store tải app
- Cấu hình domain QR / landing page

---

## 4. Bảo trì việc tham chiếu giữa các phương thức backend

## 4.1 Vấn đề
Hiện backend có cả **minimal APIs** trong `Program.cs` và **controllers** như `PoiMediaController`. Điều này có thể khiến Web Admin khó theo dõi khi số lượng chức năng tăng.

## 4.2 Đề xuất
- Chuẩn hóa backend thành các nhóm rõ ràng:
  - `PoiController` / `PoiAdminController`
  - `PoiMediaController`
  - `PoiImageController` (nếu tách riêng)
  - `QrController`
  - `LandingController`
  - `AnalyticsController`
- Mỗi nhóm API có **route prefix** riêng.

### 4.3 API grouping đề xuất
```text
/api/admin/pois
/api/admin/pois/{id}
/api/admin/pois/{id}/audio
/api/admin/pois/{id}/images
/api/admin/pois/{id}/languages
/api/admin/pois/{id}/qr
/api/public/poi/{slug}
/api/public/poi/{slug}/narration/play
/api/public/poi/{slug}/download-app
```

## 4.4 Service facade nên có ở backend
- `PoiAdminService`
- `PoiMediaService`
- `PoiImageService`
- `QrCodeService`
- `PoiLandingService`
- `UsageGateService`
- `AnalyticsService`

Mục tiêu là để Web Admin gọi vào một mặt phẳng API rõ ràng, thay vì biết quá nhiều chi tiết của EF Core hay cấu trúc DB.

---

## 5. Review và giải thích mô hình hệ thống

## 5.1 Mô hình hệ thống hiện tại (rút gọn)
```text
Mobile App (.NET MAUI)
    ↓
MapApi (ASP.NET Core + EF Core)
    ↓
SQL Server (GpsApi)
    ↓
Admin Web / CMS (wwwroot hoặc web front-end riêng)
```

## 5.2 Diễn giải
- **Mobile App** chịu trách nhiệm định vị, geofence, narration, sync local, QR scan, analytics.
- **MapApi** chịu trách nhiệm dữ liệu trung tâm: POI, media, user, analytics, QR, landing page.
- **SQL Server** lưu cấu hình POI, narration đa ngôn ngữ, media, lịch sử nghe, analytics và metadata QR.
- **Admin Web** là lớp quản trị nội dung, không nên chứa logic domain phức tạp.

## 5.3 Nguyên tắc thiết kế sau khi refactor
- Admin Web chỉ làm:
  - nhập dữ liệu
  - chỉnh sửa nội dung
  - gọi API
  - hiển thị phản hồi
- Backend xử lý:
  - validate nghiệp vụ
  - lưu DB
  - generate QR
  - kiểm soát lượt phát web
  - ghi analytics

---

## 6. Thiết kế lại module Quản lý POI

## 6.1 Chức năng POI CRUD
### Yêu cầu
Web Admin phải có các chức năng:
- **Thêm POI**
- **Sửa POI**
- **Xóa POI**
- **Bật/tắt hoạt động POI**
- **Quản lý radius / tọa độ / ưu tiên / ngôn ngữ / media**

### Form thông tin cơ bản đề xuất
- Tên POI
- Mô tả
- Latitude / Longitude
- RadiusMeters
- CooldownSeconds
- IsActive
- PriorityLevel (nếu áp dụng module ưu tiên POI)

---

## 6.2 Thiết kế lại phần audio trong trang sửa POI

### Yêu cầu mới
Khi sửa POI, phần audio phải cho phép **2 chế độ**:

#### Cách 1: Nhập URL audio
- Admin nhập URL có sẵn của file audio.
- Backend validate URL hợp lệ.
- Nếu hợp lệ thì lưu vào trường audio tương ứng.

#### Cách 2: Gắn tệp file audio
- Admin chọn file `.mp3` hoặc `.wav`.
- Backend upload file qua API media.
- Backend trả về `AudioUrl` sau khi lưu file.

### UI đề xuất
```text
[Audio Source]
(o) Nhập URL
( ) Tải tệp lên

Nếu chọn URL:
- Input: https://...

Nếu chọn Tải tệp lên:
- FilePicker
- Nút Upload
- Preview AudioUrl sau khi upload thành công
```

### Validation đề xuất
- Chỉ cho phép `.mp3`, `.wav`
- Giới hạn kích thước file
- Nếu chọn URL, phải kiểm tra định dạng URL và extension/media-type hợp lệ

---

## 6.3 Thiết kế lại phần ảnh trong trang sửa POI

### Mục tiêu
- Hỗ trợ **1 ảnh cover** + **nhiều ảnh gallery**
- Tận dụng hướng thiết kế từ `PoiImages`

### Đề xuất UI
#### A. Ảnh đại diện (Cover)
- Chọn 1 ảnh cover chính
- Có preview
- Có nút thay ảnh / xóa ảnh

#### B. Thư viện ảnh (Gallery)
- Upload nhiều ảnh
- Hiển thị danh sách thumbnail
- Có `SortOrder`
- Có nút xóa từng ảnh

### Hành vi backend
- Cover image có thể lưu ở `PoiMedia.Image` hoặc field riêng nếu refactor schema.
- Gallery image lưu ở `PoiImages`.

---

## 7. Thiết kế lại QR Code

## 7.1 Mục tiêu mới
QR code phải hỗ trợ 2 ngữ cảnh sử dụng khác nhau:

### Trường hợp A — Quét bằng camera điện thoại thường
Nếu người dùng **chưa tải app** và dùng camera điện thoại thường để quét QR:
- QR phải dẫn đến **website landing page** của POI.
- Website hiển thị:
  - thông tin mô tả POI
  - nội dung thuyết minh của POI
  - 2 nút hành động:
    1. **Phát thuyết minh**
    2. **Tải app**

### Trường hợp B — Quét bằng camera trong app
Nếu người dùng dùng **camera bên trong app** để quét QR:
- App đọc mã QR
- App mở trực tiếp POI tương ứng
- App hiển thị thuyết minh POI
- Áp dụng giới hạn lượt sử dụng **như logic hiện tại của app**

---

## 7.2 Thiết kế landing page khi quét bằng camera điện thoại thường

### URL đề xuất
```text
https://yourdomain.com/p/{PoiSlugOrId}
```

### Nội dung landing page
- Tên POI
- Ảnh đại diện
- Mô tả ngắn
- Nút **Phát thuyết minh**
- Nút **Tải app**

### Giới hạn sử dụng trên web
- Người dùng chưa cài app chỉ được **phát thuyết minh tối đa 3 lần** trên web landing.
- Sau khi vượt quá 3 lần:
  - hiện thông báo hết lượt
  - khuyến khích tải app

### Gợi ý tracking
- Lưu theo:
  - `DeviceFingerprint` hoặc cookie/session id
  - `PoiId`
  - `PlayCount`
  - `LastPlayedAt`

---

## 7.3 Thiết kế hành vi nút trên landing page

### Nút 1: Phát thuyết minh
#### Nếu còn lượt
- phát audio narration trực tiếp trên web
- tăng bộ đếm sử dụng
- ghi analytics

#### Nếu hết lượt
- disable nút hoặc hiện popup:
  > “Bạn đã dùng hết 3 lượt nghe trên web. Vui lòng tải app để tiếp tục trải nghiệm.”

### Nút 2: Tải app
- Điều hướng đến App Store / Google Play hoặc trang hướng dẫn cài đặt.
- Ghi analytics `DownloadAppClick`.

---

## 7.4 Thiết kế hành vi khi quét bằng camera trong app

### Luồng
1. App quét QR
2. Giải mã payload / POI id
3. Tìm POI trong local DB hoặc gọi API
4. Mở màn hình chi tiết POI
5. Phát narration theo logic bình thường của app

### Ghi chú
- Giới hạn lượt phát trong app **giữ nguyên theo logic hiện tại**
- Không dùng quota 3 lượt của web

---

## 7.5 Thiết kế payload QR đề xuất
### Phương án đơn giản
QR chỉ chứa URL public:
```text
https://yourdomain.com/p/123
```

### Phương án tốt hơn
QR chứa payload có thể được app hiểu nhanh hơn:
```json
{
  "type": "poi",
  "poiId": 123,
  "slug": "bao-tang-lich-su",
  "v": 1
}
```

Nếu dùng camera thường:
- mở landing page web

Nếu dùng app:
- app parse payload → mở POI trong app

---

## 8. Thiết kế dữ liệu bổ sung cho QR / web narration

## 8.1 Bảng QR cấu hình theo POI
```sql
CREATE TABLE dbo.PoiQrCodes(
    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
    PoiId INT NOT NULL,
    PublicUrl NVARCHAR(1000) NOT NULL,
    QrPayload NVARCHAR(2000) NOT NULL,
    IsActive BIT NOT NULL DEFAULT (1),
    CreatedAt DATETIME2(3) NOT NULL DEFAULT (SYSUTCDATETIME())
);
```

## 8.2 Bảng theo dõi lượt phát narration web
```sql
CREATE TABLE dbo.WebNarrationUsage(
    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
    PoiId INT NOT NULL,
    DeviceKey NVARCHAR(200) NOT NULL,
    PlayCount INT NOT NULL DEFAULT (0),
    LastPlayedAt DATETIME2(3) NULL,
    CreatedAt DATETIME2(3) NOT NULL DEFAULT (SYSUTCDATETIME())
);
```

---

## 9. API backend đề xuất

## 9.1 Admin APIs
```text
GET    /api/admin/pois
GET    /api/admin/pois/{id}
POST   /api/admin/pois
PUT    /api/admin/pois/{id}
DELETE /api/admin/pois/{id}

POST   /api/admin/pois/{id}/audio/upload
PUT    /api/admin/pois/{id}/audio/url
POST   /api/admin/pois/{id}/images/upload
DELETE /api/admin/pois/{id}/images/{imageId}
POST   /api/admin/pois/{id}/qr/generate
```

## 9.2 Public APIs
```text
GET  /api/public/poi/{slug}
POST /api/public/poi/{slug}/play-narration
GET  /api/public/poi/{slug}/download-app
```

## 9.3 App APIs
```text
GET  /api/app/pois/{id}
POST /api/app/qr/resolve
POST /api/app/analytics/qr-scan
POST /api/app/analytics/narration-play
```

---

## 10. Quy tắc nghiệp vụ chính

## 10.1 POI CRUD
- Không cho xóa POI nếu POI đang được dùng trong tour mà chưa xác nhận.
- Nếu xóa POI, media liên quan phải được xử lý đúng (`cascade` hoặc soft delete).

## 10.2 Audio
- Mỗi POI có thể có:
  - 1 URL audio active
  - hoặc 1 file audio được upload và sinh `AudioUrl`
- Không nên để cả 2 nguồn cùng active mà không có quy tắc rõ ràng.

## 10.3 Web narration quota
- Mặc định: **3 lần / thiết bị / POI**
- Sau khi hết quota, chỉ còn nút tải app.

## 10.4 App narration quota
- Theo logic usage gate hiện tại của app (giữ nguyên).

---

## 11. Gợi ý refactor code chi tiết

## 11.1 Phía backend
### Tách service
- `PoiAdminService`
- `PoiMediaService`
- `PoiImageService`
- `PoiQrService`
- `PoiLandingService`
- `WebNarrationUsageService`

### Chuẩn hóa DTO
- `PoiCreateRequest`
- `PoiUpdateRequest`
- `PoiAudioSourceRequest`
- `PoiImageUploadResponse`
- `PoiLandingDto`
- `QrCodeDto`

## 11.2 Phía admin web
### Tách lớp
- `PoiAdminApiClient`
- `MediaApiClient`
- `QrApiClient`
- `PoiFormState`
- `PoiEditorViewModel`

### Refactor màn hình
- `PoiListPage`
- `PoiEditorPage`
- `PoiMediaTab`
- `PoiQrTab`
- `PoiPreviewPage`

---

## 12. Kịch bản sử dụng tiêu biểu

## 12.1 Admin sửa POI và gắn audio bằng URL
1. Admin mở màn hình sửa POI
2. Chọn tab Audio
3. Chọn chế độ `Nhập URL`
4. Dán URL audio
5. Bấm Lưu
6. Backend validate URL và cập nhật POI

## 12.2 Admin sửa POI và gắn audio bằng file
1. Admin mở màn hình sửa POI
2. Chọn tab Audio
3. Chọn chế độ `Tải tệp lên`
4. Chọn file `.mp3/.wav`
5. Upload thành công
6. Backend trả `AudioUrl`
7. UI cập nhật preview

## 12.3 Người dùng chưa cài app quét QR
1. Dùng camera điện thoại thường quét QR
2. Mở trang landing POI
3. Xem mô tả
4. Bấm `Phát thuyết minh` (tối đa 3 lần)
5. Nếu muốn trải nghiệm tiếp → bấm `Tải app`

## 12.4 Người dùng dùng camera trong app quét QR
1. App scan QR
2. App resolve POI
3. Mở chi tiết POI trong app
4. Phát narration theo logic app

---

## 13. Kết luận
Thiết kế chỉnh sửa này giúp:
- Web Admin dễ bảo trì hơn sau refactor
- Tham chiếu giữa admin và backend rõ ràng hơn
- Quản lý POI hoàn chỉnh hơn (CRUD + audio + image)
- QR code trở thành cầu nối tốt giữa **web preview** và **app experience**
- Tăng khả năng chuyển đổi từ người dùng web sang người dùng app thông qua nút **Tải app** và quota 3 lượt trên web

Tài liệu này có thể dùng tiếp để:
- Viết FR/NFR cho Web Admin
- Thiết kế API spec chi tiết
- Thiết kế DB migration
- Vẽ UML / sequence diagram cho luồng QR và POI management
