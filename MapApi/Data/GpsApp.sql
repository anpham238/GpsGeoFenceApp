/* =========================================================
   GpsApi_Rebuild.sql  (HƯỚNG B: dbo.Pois)
   Tạo mới DB + tables + indexes + spatial index
   ========================================================= */

SET NOCOUNT ON;

------------------------------------------------------------
-- 0) Create database if not exists
------------------------------------------------------------
IF DB_ID(N'GpsApi') IS NULL
BEGIN
    PRINT N'Creating database GpsApi...';
    CREATE DATABASE GpsApi;
END
GO

USE GpsApi;
GO

----------------------------------------------------------
------------------------------------------------------------
-- 2) Main table: dbo.Pois  (khớp API /api/v1/pois)
------------------------------------------------------------
CREATE TABLE dbo.Pois
(
    Id              NVARCHAR(64)    NOT NULL CONSTRAINT PK_Pois PRIMARY KEY,
    Name            NVARCHAR(200)   NOT NULL,
    Description     NVARCHAR(2000)  NULL,

    Latitude        FLOAT           NOT NULL,
    Longitude       FLOAT           NOT NULL,

    RadiusMeters        INT         NOT NULL CONSTRAINT DF_Pois_Radius DEFAULT(120),
    NearRadiusMeters    INT         NOT NULL CONSTRAINT DF_Pois_NearRadius DEFAULT(220),

    DebounceSeconds     INT         NOT NULL CONSTRAINT DF_Pois_Debounce DEFAULT(3),
    CooldownSeconds     INT         NOT NULL CONSTRAINT DF_Pois_Cooldown DEFAULT(30),

    Priority        INT             NULL,
    MapLink         NVARCHAR(1000)  NULL,
    IsActive        BIT             NOT NULL CONSTRAINT DF_Pois_IsActive DEFAULT(1),

    -- ===== Các cột phục vụ trực tiếp MAUI / API hiện tại =====
    NarrationText   NVARCHAR(4000)  NULL,
    AudioUrl        NVARCHAR(1000)  NULL,
    ImageUrl        NVARCHAR(1000)  NULL,

    -- ===== Timestamps =====
    CreatedAt       DATETIME2(3)    NOT NULL CONSTRAINT DF_Pois_CreatedAt DEFAULT SYSUTCDATETIME(),
    UpdatedAt       DATETIME2(3)    NOT NULL CONSTRAINT DF_Pois_UpdatedAt DEFAULT SYSUTCDATETIME(),

    RowVer          ROWVERSION      NOT NULL
);
GO

-- Ràng buộc lat/lng hợp lệ (giữ như thiết kế cũ) [1](https://svsguedu-my.sharepoint.com/personal/3123411204_sv_sgu_edu_vn/Documents/Microsoft%20Copilot%20Chat%20Files/GpsApp.sql)
ALTER TABLE dbo.Pois ADD CONSTRAINT CK_Pois_Latitude  CHECK (Latitude BETWEEN -90 AND 90);
ALTER TABLE dbo.Pois ADD CONSTRAINT CK_Pois_Longitude CHECK (Longitude BETWEEN -180 AND 180);
GO

-- Index thường dùng (active + priority + name) [1](https://svsguedu-my.sharepoint.com/personal/3123411204_sv_sgu_edu_vn/Documents/Microsoft%20Copilot%20Chat%20Files/GpsApp.sql)
CREATE INDEX IX_Pois_ActivePriority ON dbo.Pois (IsActive, Priority, Name);
GO

------------------------------------------------------------
-- 3) PoiNarration: kịch bản theo Event + Language
------------------------------------------------------------
CREATE TABLE dbo.PoiNarration
(
    Id              BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_PoiNarration PRIMARY KEY,
    PoiId           NVARCHAR(64) NOT NULL,
    EventType       TINYINT      NOT NULL,  -- 1=Enter, 2=Near, 3=Tap
    LanguageTag     NVARCHAR(10) NOT NULL,  -- 'vi-VN', 'en-US', ...
    NarrationText   NVARCHAR(4000) NULL,

    CreatedAtUtc    DATETIME2(3) NOT NULL CONSTRAINT DF_PoiNarration_CreatedAt DEFAULT SYSUTCDATETIME(),
    UpdatedAtUtc    DATETIME2(3) NOT NULL CONSTRAINT DF_PoiNarration_UpdatedAt DEFAULT SYSUTCDATETIME(),

    CONSTRAINT FK_PoiNarration_Pois FOREIGN KEY (PoiId) REFERENCES dbo.Pois(Id) ON DELETE CASCADE
);
GO

CREATE UNIQUE INDEX UX_PoiNarration_Key ON dbo.PoiNarration (PoiId, EventType, LanguageTag);
GO

------------------------------------------------------------
-- 4) PoiMedia: ảnh / audio nhiều file
------------------------------------------------------------
CREATE TABLE dbo.PoiMedia
(
    Id              BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_PoiMedia PRIMARY KEY,
    PoiId           NVARCHAR(64) NOT NULL,

    MediaType       TINYINT      NOT NULL,   -- 1=Image, 2=Audio
    LanguageTag     NVARCHAR(10) NULL,       -- audio nên có vi-VN; image có thể NULL
    Url             NVARCHAR(1000) NOT NULL, -- '/audio/..mp3' hoặc absolute URL
    MimeType        NVARCHAR(50) NULL,
    FileSizeBytes   BIGINT       NULL,
    DurationMs      INT          NULL,       -- chỉ audio
    SortOrder       INT          NOT NULL CONSTRAINT DF_PoiMedia_Sort DEFAULT(0),
    IsPrimary       BIT          NOT NULL CONSTRAINT DF_PoiMedia_Primary DEFAULT(0),

    CreatedAtUtc    DATETIME2(3) NOT NULL CONSTRAINT DF_PoiMedia_CreatedAt DEFAULT SYSUTCDATETIME(),

    CONSTRAINT FK_PoiMedia_Pois FOREIGN KEY (PoiId) REFERENCES dbo.Pois(Id) ON DELETE CASCADE
);
GO

CREATE INDEX IX_PoiMedia_PoiType ON dbo.PoiMedia (PoiId, MediaType, IsPrimary, SortOrder);
GO

------------------------------------------------------------
-- 5) Spatial (Geo computed column + spatial index)
--    giống thiết kế cũ nhưng áp cho dbo.Pois [1](https://svsguedu-my.sharepoint.com/personal/3123411204_sv_sgu_edu_vn/Documents/Microsoft%20Copilot%20Chat%20Files/GpsApp.sql)
------------------------------------------------------------
ALTER TABLE dbo.Pois
ADD Geo AS (geography::Point(Latitude, Longitude, 4326)) PERSISTED;
GO

CREATE SPATIAL INDEX SIDX_Pois_Geo ON dbo.Pois(Geo);
GO

------------------------------------------------------------
-- 6) Playback log (tuỳ chọn)
------------------------------------------------------------
CREATE TABLE dbo.PoiPlaybackLog
(
    Id              BIGINT IDENTITY(1,1) PRIMARY KEY,
    PoiId           NVARCHAR(64) NOT NULL,
    EventType       TINYINT NOT NULL,
    FiredAtUtc      DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
    DeviceId        NVARCHAR(64) NULL,
    DistanceMeters  INT NULL,

    CONSTRAINT FK_PlaybackLog_Pois FOREIGN KEY (PoiId) REFERENCES dbo.Pois(Id) ON DELETE CASCADE
);
GO

CREATE INDEX IX_PlaybackLog_PoiTime ON dbo.PoiPlaybackLog(PoiId, FiredAtUtc DESC);
GO

PRINT N'Done. Database schema rebuilt successfully.';
GO


USE GpsApi;
GO

MERGE dbo.Pois AS T
USING (VALUES
 (N'poi-ben-thanh',  N'Chợ Bến Thành', N'Biểu tượng du lịch TP.HCM.', 10.77245, 106.69806, 120, 220, 3, 30, 1,
  N'https://maps.google.com/?q=10.77245,106.69806', 1,
  N'Bạn đang đến Chợ Bến Thành.', NULL, NULL),

 (N'poi-duc-ba',     N'Nhà thờ Đức Bà', N'Kiến trúc cổ nổi bật trung tâm thành phố.', 10.77978, 106.69918, 120, 220, 3, 30, 2,
  N'https://maps.google.com/?q=10.77978,106.69918', 1,
  N'Bạn đang đến Nhà thờ Đức Bà.', NULL, NULL)
) AS S(
  Id, Name, [Description], Latitude, Longitude,
  RadiusMeters, NearRadiusMeters, DebounceSeconds, CooldownSeconds,
  Priority, MapLink, IsActive,
  NarrationText, AudioUrl, ImageUrl
)
ON T.Id = S.Id
WHEN MATCHED THEN UPDATE SET
  T.Name = S.Name,
  T.Description = S.Description,
  T.Latitude = S.Latitude,
  T.Longitude = S.Longitude,
  T.RadiusMeters = S.RadiusMeters,
  T.NearRadiusMeters = S.NearRadiusMeters,
  T.DebounceSeconds = S.DebounceSeconds,
  T.CooldownSeconds = S.CooldownSeconds,
  T.Priority = S.Priority,
  T.MapLink = S.MapLink,
  T.IsActive = S.IsActive,
  T.NarrationText = S.NarrationText,
  T.AudioUrl = S.AudioUrl,
  T.ImageUrl = S.ImageUrl,
  T.UpdatedAt = SYSUTCDATETIME()
WHEN NOT MATCHED THEN
  INSERT (Id, Name, Description, Latitude, Longitude, RadiusMeters, NearRadiusMeters,
          DebounceSeconds, CooldownSeconds, Priority, MapLink, IsActive,
          NarrationText, AudioUrl, ImageUrl, CreatedAt, UpdatedAt)
  VALUES (S.Id, S.Name, S.Description, S.Latitude, S.Longitude, S.RadiusMeters, S.NearRadiusMeters,
          S.DebounceSeconds, S.CooldownSeconds, S.Priority, S.MapLink, S.IsActive,
          S.NarrationText, S.AudioUrl, S.ImageUrl, SYSUTCDATETIME(), SYSUTCDATETIME());
GO

USE GpsApi;
SELECT TOP 5 Id, Name, UpdatedAt
FROM dbo.Pois
WHERE Id = 'poi-test-100';
