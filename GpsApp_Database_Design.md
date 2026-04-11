# GpsApp — Tài liệu thiết kế cơ sở dữ liệu

## Tổng quan

Cơ sở dữ liệu **GpsApi** phục vụ ứng dụng GPS hướng dẫn du lịch theo địa điểm (Point of Interest - POI). Khi người dùng đến gần một địa điểm, hệ thống phát audio narration bằng ngôn ngữ phù hợp, ghi lại lịch sử ghé thăm và cung cấp thống kê.

---

## Sơ đồ quan hệ (ERD)

```
Pois ──< PoiLanguage   (1 POI có nhiều bản ngôn ngữ)
Pois ──< PoiMedia      (1 POI có nhiều ảnh/link bản đồ)
Pois ──< HistoryPoi    (1 POI được nhiều người ghé thăm)
Users ──< HistoryPoi   (1 người dùng ghé thăm nhiều POI)
```

---

## Chi tiết các bảng

### 1. Pois — Bảng địa điểm chính

Lưu thông tin cốt lõi của mỗi địa điểm. Không chứa nội dung đa ngôn ngữ (đã tách sang `PoiLanguage`).

| Cột | Kiểu | Ràng buộc | Mô tả |
|---|---|---|---|
| `Id` | `nvarchar(64)` | PK | Mã định danh POI (slug hoặc UUID) |
| `Name` | `nvarchar(200)` | NOT NULL | Tên mặc định (fallback khi không có ngôn ngữ) |
| `Description` | `nvarchar(2000)` | NULL | Mô tả ngắn mặc định |
| `RadiusMeters` | `int` | NOT NULL, DEFAULT 120 | Bán kính kích hoạt (mét) |
| `Latitude` | `float` | NOT NULL, CHECK(-90..90) | Vĩ độ |
| `Longitude` | `float` | NOT NULL, CHECK(-180..180) | Kinh độ |
| `CooldownSeconds` | `int` | NOT NULL, DEFAULT 30 | Thời gian chờ giữa hai lần kích hoạt |
| `IsActive` | `bit` | NOT NULL, DEFAULT 1 | Trạng thái hoạt động |
| `CreatedAt` | `datetime2(3)` | NOT NULL, DEFAULT UTC | Thời điểm tạo |
| `UpdatedAt` | `datetime2(3)` | NOT NULL, DEFAULT UTC | Thời điểm cập nhật |

**Cột tính toán:**

```sql
[Geo] AS (geography::Point(Latitude, Longitude, 4326)) PERSISTED
```

Dùng cho truy vấn không gian địa lý (tìm POI trong bán kính).

**Index:**
- `SIDX_Pois_Geo` — Spatial index trên cột `Geo` để tìm kiếm theo vị trí nhanh
- `IX_Pois_Active` — `(IsActive, Name)` cho danh sách POI đang hoạt động

---

### 2. PoiLanguage — Nội dung đa ngôn ngữ

Lưu tên, mô tả và nội dung TTS (Text-to-Speech) theo từng ngôn ngữ.

| Cột | Kiểu | Ràng buộc | Mô tả |
|---|---|---|---|
| `IdLang` | `bigint` | PK, IDENTITY | Khóa chính tự tăng |
| `IdPoi` | `nvarchar(64)` | FK → Pois.Id | Tham chiếu đến địa điểm |
| `NamePoi` | `nvarchar(200)` | NOT NULL | Tên POI theo ngôn ngữ |
| `NarTTS` | `nvarchar(4000)` | NULL | Nội dung narration phát bằng TTS |
| `LanguageTag` | `nvarchar(10)` | NOT NULL | BCP-47 tag: `vi-VN`, `en-US`, `ja-JP`... |
| `Description` | `nvarchar(2000)` | NULL | Mô tả đầy đủ theo ngôn ngữ |

**Ràng buộc duy nhất:** `UX_PoiLanguage_Key` trên `(IdPoi, LanguageTag)` — mỗi POI chỉ có một bản dịch mỗi ngôn ngữ.

**Ghi chú thiết kế:** Bảng này thay thế cả `PoiNarration` và trường `Language` cũ trong `Pois`, gộp Enter/Near/Tap thành một bản narration duy nhất. Nếu cần phân biệt sự kiện (Enter vs Near vs Tap), có thể thêm cột `EventType tinyint`.

---

### 3. PoiMedia — Hình ảnh và bản đồ

Lưu đường dẫn ảnh và link bản đồ của từng địa điểm.

| Cột | Kiểu | Ràng buộc | Mô tả |
|---|---|---|---|
| `Idm` | `bigint` | PK, IDENTITY | Khóa chính tự tăng |
| `IdPoi` | `nvarchar(64)` | FK → Pois.Id | Tham chiếu đến địa điểm |
| `Image` | `nvarchar(1000)` | NULL | URL hình ảnh |
| `MapLink` | `nvarchar(1000)` | NULL | URL bản đồ (Google Maps, Apple Maps...) |

**Ghi chú thiết kế:** Bảng đơn giản hóa so với phiên bản cũ (bỏ `MediaType`, `MimeType`, `FileSizeBytes`...) theo đúng yêu cầu. Nếu sau này cần phân loại media phức tạp hơn, có thể thêm cột `MediaType` trở lại.

---

### 4. Users — Người dùng

| Cột | Kiểu | Ràng buộc | Mô tả |
|---|---|---|---|
| `UserId` | `uniqueidentifier` | PK, DEFAULT NEWID() | Khóa chính UUID |
| `Username` | `nvarchar(100)` | NOT NULL, UNIQUE | Tên đăng nhập |
| `Mail` | `nvarchar(200)` | NOT NULL, UNIQUE | Địa chỉ email |
| `PasswordHash` | `nvarchar(256)` | NOT NULL | Mật khẩu đã hash (bcrypt/Argon2) |
| `CreatedAt` | `datetime2(3)` | NOT NULL, DEFAULT UTC | Thời điểm đăng ký |
| `IsActive` | `bit` | NOT NULL, DEFAULT 1 | Trạng thái tài khoản |

> **Bảo mật:** Cột `PasswordHash` chỉ lưu hash, **không bao giờ** lưu mật khẩu plaintext. Khuyến nghị dùng BCrypt (cost factor ≥ 12) hoặc Argon2id.

---

### 5. HistoryPoi — Lịch sử ghé thăm

Ghi lại mỗi lần người dùng ghé thăm (hoặc nghe narration tại) một địa điểm. Là nền tảng cho module thống kê.

| Cột | Kiểu | Ràng buộc | Mô tả |
|---|---|---|---|
| `Id` | `bigint` | PK, IDENTITY | Khóa chính tự tăng |
| `IdPoi` | `nvarchar(64)` | FK → Pois.Id | Địa điểm được ghé thăm |
| `IdUser` | `uniqueidentifier` | FK → Users.UserId | Người dùng |
| `PoiName` | `nvarchar(200)` | NOT NULL | Snapshot tên POI tại thời điểm ghé thăm |
| `Quantity` | `int` | NOT NULL, DEFAULT 1 | Số lần ghé thăm tích lũy |
| `LastVisitedAt` | `datetime2(3)` | NOT NULL, DEFAULT UTC | Thời điểm ghé thăm gần nhất |
| `TotalDurationSeconds` | `int` | NULL | Tổng thời gian dừng tại POI (giây) |

**Index:**
- `IX_History_Poi` trên `(IdPoi, LastVisitedAt DESC)` — tối ưu thống kê theo POI
- `IX_History_User` trên `(IdUser, LastVisitedAt DESC)` — tối ưu lịch sử theo người dùng

---

## Thống kê

### Top địa điểm được nghe nhiều nhất

```sql
SELECT
    h.IdPoi,
    h.PoiName,
    SUM(h.Quantity)      AS TotalVisits,
    COUNT(DISTINCT h.IdUser) AS UniqueVisitors
FROM HistoryPoi h
GROUP BY h.IdPoi, h.PoiName
ORDER BY TotalVisits DESC;
```

### Thời gian trung bình dừng tại 1 POI

```sql
SELECT
    h.IdPoi,
    h.PoiName,
    AVG(h.TotalDurationSeconds)           AS AvgDurationSeconds,
    AVG(h.TotalDurationSeconds) / 60.0    AS AvgDurationMinutes
FROM HistoryPoi h
WHERE h.TotalDurationSeconds IS NOT NULL
GROUP BY h.IdPoi, h.PoiName
ORDER BY AvgDurationSeconds DESC;
```

### Top POI theo ngôn ngữ người dùng (kết hợp với PoiLanguage)

```sql
SELECT
    pl.LanguageTag,
    h.IdPoi,
    h.PoiName,
    SUM(h.Quantity) AS TotalVisits
FROM HistoryPoi h
JOIN PoiLanguage pl ON pl.IdPoi = h.IdPoi
GROUP BY pl.LanguageTag, h.IdPoi, h.PoiName
ORDER BY pl.LanguageTag, TotalVisits DESC;
```

---

## Stored Procedure: UpsertPoiLanguage

Thay thế `UpsertPoiNarration` cũ, dùng cho import/cập nhật nội dung đa ngôn ngữ.

```sql
CREATE PROCEDURE UpsertPoiLanguage
(
    @IdPoi       nvarchar(64),
    @LanguageTag nvarchar(10),
    @NamePoi     nvarchar(200)  = NULL,
    @NarTTS      nvarchar(4000) = NULL,
    @Description nvarchar(2000) = NULL
)
AS
BEGIN
    SET NOCOUNT ON;
    IF (@IdPoi IS NULL OR LTRIM(RTRIM(@IdPoi)) = N'')
        THROW 50001, 'IdPoi is required.', 1;
    IF (@LanguageTag IS NULL OR LTRIM(RTRIM(@LanguageTag)) = N'')
        THROW 50002, 'LanguageTag is required.', 1;
    IF NOT EXISTS (SELECT 1 FROM dbo.Pois WHERE Id = @IdPoi)
        THROW 50003, 'POI does not exist.', 1;

    MERGE dbo.PoiLanguage AS target
    USING (SELECT @IdPoi AS IdPoi, @LanguageTag AS LanguageTag) AS src
        ON target.IdPoi = src.IdPoi AND target.LanguageTag = src.LanguageTag
    WHEN MATCHED THEN
        UPDATE SET
            NamePoi     = ISNULL(@NamePoi, target.NamePoi),
            NarTTS      = ISNULL(@NarTTS, target.NarTTS),
            Description = ISNULL(@Description, target.Description)
    WHEN NOT MATCHED THEN
        INSERT (IdPoi, LanguageTag, NamePoi, NarTTS, Description)
        VALUES (@IdPoi, @LanguageTag, @NamePoi, @NarTTS, @Description);
END
```

---

## So sánh thiết kế cũ và mới

| Bảng cũ | Bảng mới | Thay đổi |
|---|---|---|
| `Pois` (có NarrationText, AudioUrl, ImageUrl, Language) | `Pois` (chỉ dữ liệu cốt lõi) | Tách nội dung ngôn ngữ và media ra ngoài |
| `PoiNarration` (Enter/Near/Tap × Language) | `PoiLanguage` | Gộp đơn giản hơn, thêm Description |
| `PoiMedia` (MediaType, MimeType, FileSizeBytes...) | `PoiMedia` (Image, MapLink) | Đơn giản hóa theo yêu cầu |
| `PoiPlaybackLog` | `HistoryPoi` | Thêm quan hệ User, thêm Quantity và Duration |
| *(không có)* | `Users` | Bảng mới quản lý người dùng |

---

## Ghi chú triển khai

**SQL Server version:** Tương thích SQL Server 2019+ (CompatibilityLevel 150+).

**Spatial index** (`SIDX_Pois_Geo`) yêu cầu cột `Geo` kiểu `geography` — đảm bảo SQL Server có cài đặt Spatial feature.

**EF Core migration:** Nếu dùng Entity Framework Core, cần cấu hình `HasComputedColumnSql` cho cột `Geo` và `UseNetTopologySuite()` trong `DbContext`.
