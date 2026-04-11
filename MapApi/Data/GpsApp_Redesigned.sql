USE [master]
GO

-- ============================================================
-- GpsApi Database — Redesigned Schema
-- Generated: 2026-04-11
-- ============================================================

IF DB_ID('GpsApi') IS NOT NULL
    DROP DATABASE [GpsApi];
GO

CREATE DATABASE [GpsApi]
 CONTAINMENT = NONE
 ON PRIMARY
( NAME = N'GpsApi', FILENAME = N'C:\Program Files\Microsoft SQL Server\MSSQL17.SQLEXPRESS\MSSQL\DATA\GpsApi.mdf',
  SIZE = 8192KB, MAXSIZE = UNLIMITED, FILEGROWTH = 65536KB )
 LOG ON
( NAME = N'GpsApi_log', FILENAME = N'C:\Program Files\Microsoft SQL Server\MSSQL17.SQLEXPRESS\MSSQL\DATA\GpsApi_log.ldf',
  SIZE = 8192KB, MAXSIZE = 2048GB, FILEGROWTH = 65536KB )
 WITH CATALOG_COLLATION = DATABASE_DEFAULT;
GO

ALTER DATABASE [GpsApi] SET COMPATIBILITY_LEVEL = 150;
GO
ALTER DATABASE [GpsApi] SET RECOVERY SIMPLE;
GO
ALTER DATABASE [GpsApi] SET MULTI_USER;
GO
ALTER DATABASE [GpsApi] SET AUTO_UPDATE_STATISTICS ON;
GO
ALTER DATABASE [GpsApi] SET QUERY_STORE = ON;
GO

USE [GpsApi]
GO

-- ============================================================
-- EF Migrations History (giữ lại cho EF Core)
-- ============================================================
CREATE TABLE [dbo].[__EFMigrationsHistory](
    [MigrationId]    [nvarchar](150) NOT NULL,
    [ProductVersion] [nvarchar](32)  NOT NULL,
    CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY CLUSTERED ([MigrationId] ASC)
) ON [PRIMARY];
GO

-- ============================================================
-- 1. Pois — Bảng địa điểm chính
-- ============================================================
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

CREATE TABLE [dbo].[Pois](
    [Id]              [nvarchar](64)   NOT NULL,   -- Slug hoặc UUID
    [Name]            [nvarchar](200)  NOT NULL,   -- Tên fallback (khi không có ngôn ngữ)
    [Description]     [nvarchar](2000) NULL,
    [RadiusMeters]    [int]            NOT NULL,   -- Bán kính kích hoạt (mét)
    [Latitude]        [float]          NOT NULL,
    [Longitude]       [float]          NOT NULL,
    [CooldownSeconds] [int]            NOT NULL,   -- Thời gian chờ giữa 2 lần kích hoạt
    [IsActive]        [bit]            NOT NULL,
    [CreatedAt]       [datetime2](3)   NOT NULL,
    [UpdatedAt]       [datetime2](3)   NOT NULL,
    -- Cột tính toán cho spatial query
    [Geo] AS (geography::Point([Latitude], [Longitude], 4326)) PERSISTED,
    CONSTRAINT [PK_Pois] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [CK_Pois_Latitude]  CHECK ([Latitude]  >= -90  AND [Latitude]  <= 90),
    CONSTRAINT [CK_Pois_Longitude] CHECK ([Longitude] >= -180 AND [Longitude] <= 180)
) ON [PRIMARY];
GO

-- Giá trị mặc định
ALTER TABLE [dbo].[Pois] ADD CONSTRAINT [DF_Pois_RadiusMeters]    DEFAULT (120) FOR [RadiusMeters];
ALTER TABLE [dbo].[Pois] ADD CONSTRAINT [DF_Pois_CooldownSeconds] DEFAULT (30)  FOR [CooldownSeconds];
ALTER TABLE [dbo].[Pois] ADD CONSTRAINT [DF_Pois_IsActive]        DEFAULT (1)   FOR [IsActive];
ALTER TABLE [dbo].[Pois] ADD CONSTRAINT [DF_Pois_CreatedAt]       DEFAULT (SYSUTCDATETIME()) FOR [CreatedAt];
ALTER TABLE [dbo].[Pois] ADD CONSTRAINT [DF_Pois_UpdatedAt]       DEFAULT (SYSUTCDATETIME()) FOR [UpdatedAt];
GO

-- Index thường (active + name)
CREATE NONCLUSTERED INDEX [IX_Pois_Active] ON [dbo].[Pois]
(
    [IsActive] ASC,
    [Name]     ASC
) ON [PRIMARY];
GO

-- ============================================================
-- 2. PoiLanguage — Nội dung đa ngôn ngữ (tên, narration, mô tả)
-- ============================================================
CREATE TABLE [dbo].[PoiLanguage](
    [IdLang]      [bigint]         IDENTITY(1,1) NOT NULL,
    [IdPoi]       [nvarchar](64)   NOT NULL,           -- FK -> Pois.Id
    [NamePoi]     [nvarchar](200)  NOT NULL,           -- Tên POI theo ngôn ngữ
    [NarTTS]      [nvarchar](4000) NULL,               -- Nội dung Text-to-Speech
    [LanguageTag] [nvarchar](10)   NOT NULL,           -- vi-VN, en-US, ja-JP...
    [Description] [nvarchar](2000) NULL,               -- Mô tả theo ngôn ngữ
    CONSTRAINT [PK_PoiLanguage] PRIMARY KEY CLUSTERED ([IdLang] ASC)
) ON [PRIMARY];
GO

ALTER TABLE [dbo].[PoiLanguage] WITH CHECK
    ADD CONSTRAINT [FK_PoiLanguage_Pois] FOREIGN KEY([IdPoi])
    REFERENCES [dbo].[Pois] ([Id]) ON DELETE CASCADE;
GO

-- Unique: mỗi POI chỉ có 1 bản dịch mỗi ngôn ngữ
CREATE UNIQUE NONCLUSTERED INDEX [UX_PoiLanguage_Key] ON [dbo].[PoiLanguage]
(
    [IdPoi]       ASC,
    [LanguageTag] ASC
) ON [PRIMARY];
GO

-- ============================================================
-- 3. PoiMedia — Hình ảnh và link bản đồ
-- ============================================================
CREATE TABLE [dbo].[PoiMedia](
    [Idm]     [bigint]          IDENTITY(1,1) NOT NULL,
    [IdPoi]   [nvarchar](64)    NOT NULL,               -- FK -> Pois.Id
    [Image]   [nvarchar](1000)  NULL,                   -- URL hình ảnh
    [MapLink] [nvarchar](1000)  NULL,                   -- URL Google Maps / Apple Maps
    CONSTRAINT [PK_PoiMedia] PRIMARY KEY CLUSTERED ([Idm] ASC)
) ON [PRIMARY];
GO

ALTER TABLE [dbo].[PoiMedia] WITH CHECK
    ADD CONSTRAINT [FK_PoiMedia_Pois] FOREIGN KEY([IdPoi])
    REFERENCES [dbo].[Pois] ([Id]) ON DELETE CASCADE;
GO

CREATE NONCLUSTERED INDEX [IX_PoiMedia_Poi] ON [dbo].[PoiMedia]
(
    [IdPoi] ASC
) ON [PRIMARY];
GO

-- ============================================================
-- 4. Users — Tài khoản người dùng
-- ============================================================
CREATE TABLE [dbo].[Users](
    [UserId]       [uniqueidentifier] NOT NULL,
    [Username]     [nvarchar](100)    NOT NULL,
    [Mail]         [nvarchar](200)    NOT NULL,
    [PasswordHash] [nvarchar](256)    NOT NULL,   -- BCrypt / Argon2id hash
    [IsActive]     [bit]              NOT NULL,
    [CreatedAt]    [datetime2](3)     NOT NULL,
    CONSTRAINT [PK_Users]          PRIMARY KEY CLUSTERED   ([UserId]   ASC),
    CONSTRAINT [UX_Users_Username] UNIQUE NONCLUSTERED     ([Username] ASC),
    CONSTRAINT [UX_Users_Mail]     UNIQUE NONCLUSTERED     ([Mail]     ASC)
) ON [PRIMARY];
GO

ALTER TABLE [dbo].[Users] ADD CONSTRAINT [DF_Users_UserId]    DEFAULT (NEWID())            FOR [UserId];
ALTER TABLE [dbo].[Users] ADD CONSTRAINT [DF_Users_IsActive]  DEFAULT (1)                  FOR [IsActive];
ALTER TABLE [dbo].[Users] ADD CONSTRAINT [DF_Users_CreatedAt] DEFAULT (SYSUTCDATETIME())   FOR [CreatedAt];
GO

-- ============================================================
-- 5. HistoryPoi — Lịch sử ghé thăm địa điểm
-- ============================================================
CREATE TABLE [dbo].[HistoryPoi](
    [Id]                   [bigint]           IDENTITY(1,1) NOT NULL,
    [IdPoi]                [nvarchar](64)     NOT NULL,       -- FK -> Pois.Id
    [IdUser]               [uniqueidentifier] NOT NULL,       -- FK -> Users.UserId
    [PoiName]              [nvarchar](200)    NOT NULL,       -- Snapshot tên tại thời điểm ghé thăm
    [Quantity]             [int]              NOT NULL,       -- Số lần ghé thăm tích lũy
    [LastVisitedAt]        [datetime2](3)     NOT NULL,       -- Lần ghé thăm gần nhất
    [TotalDurationSeconds] [int]              NULL,           -- Tổng thời gian dừng tại POI (giây)
    CONSTRAINT [PK_HistoryPoi] PRIMARY KEY CLUSTERED ([Id] ASC)
) ON [PRIMARY];
GO

ALTER TABLE [dbo].[HistoryPoi] ADD CONSTRAINT [DF_HistoryPoi_Quantity]       DEFAULT (1)                FOR [Quantity];
ALTER TABLE [dbo].[HistoryPoi] ADD CONSTRAINT [DF_HistoryPoi_LastVisitedAt]  DEFAULT (SYSUTCDATETIME()) FOR [LastVisitedAt];
GO

ALTER TABLE [dbo].[HistoryPoi] WITH CHECK
    ADD CONSTRAINT [FK_HistoryPoi_Pois] FOREIGN KEY([IdPoi])
    REFERENCES [dbo].[Pois] ([Id]) ON DELETE CASCADE;
GO

ALTER TABLE [dbo].[HistoryPoi] WITH CHECK
    ADD CONSTRAINT [FK_HistoryPoi_Users] FOREIGN KEY([IdUser])
    REFERENCES [dbo].[Users] ([UserId]);
GO

-- Index thống kê theo POI và theo User
CREATE NONCLUSTERED INDEX [IX_History_Poi] ON [dbo].[HistoryPoi]
(
    [IdPoi]         ASC,
    [LastVisitedAt] DESC
) ON [PRIMARY];
GO

CREATE NONCLUSTERED INDEX [IX_History_User] ON [dbo].[HistoryPoi]
(
    [IdUser]        ASC,
    [LastVisitedAt] DESC
) ON [PRIMARY];
GO

-- ============================================================
-- Spatial Index (cần SQL Server Spatial feature)
-- ============================================================
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET NUMERIC_ROUNDABORT OFF;
GO

CREATE SPATIAL INDEX [SIDX_Pois_Geo] ON [dbo].[Pois]([Geo])
USING GEOGRAPHY_AUTO_GRID
WITH (CELLS_PER_OBJECT = 12, PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF,
      SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF,
      ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON)
ON [PRIMARY];
GO

-- ============================================================
-- Stored Procedure: UpsertPoiLanguage
-- Thay thế UpsertPoiNarration cũ
-- ============================================================
CREATE PROCEDURE [dbo].[UpsertPoiLanguage]
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
        THROW 50003, 'POI does not exist. Please create the POI first.', 1;

    MERGE dbo.PoiLanguage AS target
    USING (SELECT @IdPoi AS IdPoi, @LanguageTag AS LanguageTag) AS src
        ON  target.IdPoi       = src.IdPoi
        AND target.LanguageTag = src.LanguageTag
    WHEN MATCHED THEN
        UPDATE SET
            NamePoi     = ISNULL(@NamePoi,     target.NamePoi),
            NarTTS      = ISNULL(@NarTTS,      target.NarTTS),
            [Description] = ISNULL(@Description, target.[Description])
    WHEN NOT MATCHED THEN
        INSERT (IdPoi, LanguageTag, NamePoi, NarTTS, [Description])
        VALUES (@IdPoi, @LanguageTag,
                ISNULL(@NamePoi, @IdPoi),
                @NarTTS,
                @Description);
END
GO

-- ============================================================
-- Stored Procedure: RecordPoiVisit
-- Ghi lịch sử ghé thăm (upsert theo IdPoi + IdUser)
-- ============================================================
CREATE PROCEDURE [dbo].[RecordPoiVisit]
(
    @IdPoi          nvarchar(64),
    @IdUser         uniqueidentifier,
    @PoiName        nvarchar(200),
    @DurationSeconds int = NULL
)
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (SELECT 1 FROM dbo.Pois  WHERE Id     = @IdPoi)
        THROW 50010, 'POI does not exist.', 1;
    IF NOT EXISTS (SELECT 1 FROM dbo.Users WHERE UserId = @IdUser)
        THROW 50011, 'User does not exist.', 1;

    MERGE dbo.HistoryPoi AS target
    USING (SELECT @IdPoi AS IdPoi, @IdUser AS IdUser) AS src
        ON  target.IdPoi  = src.IdPoi
        AND target.IdUser = src.IdUser
    WHEN MATCHED THEN
        UPDATE SET
            Quantity             = target.Quantity + 1,
            LastVisitedAt        = SYSUTCDATETIME(),
            TotalDurationSeconds = ISNULL(target.TotalDurationSeconds, 0) + ISNULL(@DurationSeconds, 0)
    WHEN NOT MATCHED THEN
        INSERT (IdPoi, IdUser, PoiName, Quantity, LastVisitedAt, TotalDurationSeconds)
        VALUES (@IdPoi, @IdUser, @PoiName, 1, SYSUTCDATETIME(), @DurationSeconds);
END
GO

-- ============================================================
-- View: vw_TopPois — Top địa điểm được nghe nhiều nhất
-- ============================================================
CREATE VIEW [dbo].[vw_TopPois]
AS
SELECT
    h.IdPoi,
    h.PoiName,
    SUM(h.Quantity)                            AS TotalVisits,
    COUNT(DISTINCT h.IdUser)                   AS UniqueVisitors,
    AVG(CAST(h.TotalDurationSeconds AS float)) AS AvgDurationSeconds
FROM dbo.HistoryPoi h
GROUP BY h.IdPoi, h.PoiName;
GO

-- ============================================================
-- View: vw_AvgPoiDuration — Thời gian trung bình dừng tại POI
-- ============================================================
CREATE VIEW [dbo].[vw_AvgPoiDuration]
AS
SELECT
    h.IdPoi,
    h.PoiName,
    AVG(CAST(h.TotalDurationSeconds AS float))          AS AvgDurationSeconds,
    AVG(CAST(h.TotalDurationSeconds AS float)) / 60.0   AS AvgDurationMinutes,
    SUM(h.Quantity)                                      AS TotalVisits
FROM dbo.HistoryPoi h
WHERE h.TotalDurationSeconds IS NOT NULL
GROUP BY h.IdPoi, h.PoiName;
GO

USE [master]
GO
ALTER DATABASE [GpsApi] SET READ_WRITE;
GO
