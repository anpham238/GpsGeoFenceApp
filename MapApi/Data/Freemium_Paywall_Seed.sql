USE [GpsApi]
GO

-- ============================================================
-- PHẦN 1: SEED DATA – Areas (4 khu vực từ wireframe)
-- ============================================================
SET IDENTITY_INSERT dbo.Areas OFF;

IF NOT EXISTS (SELECT 1 FROM dbo.Areas WHERE Code = 'HCMC')
    INSERT INTO dbo.Areas (Code, Name, Description, City, Province, IsActive)
    VALUES ('HCMC', N'TP. Hồ Chí Minh', N'Khu vực thành phố Hồ Chí Minh', N'TP. Hồ Chí Minh', N'TP. Hồ Chí Minh', 1);

IF NOT EXISTS (SELECT 1 FROM dbo.Areas WHERE Code = 'HANOI')
    INSERT INTO dbo.Areas (Code, Name, Description, City, Province, IsActive)
    VALUES ('HANOI', N'Hà Nội', N'Khu vực Hà Nội', N'Hà Nội', N'Hà Nội', 1);

IF NOT EXISTS (SELECT 1 FROM dbo.Areas WHERE Code = 'VUNGTAU')
    INSERT INTO dbo.Areas (Code, Name, Description, City, Province, IsActive)
    VALUES ('VUNGTAU', N'Vũng Tàu', N'Khu vực Vũng Tàu', N'Vũng Tàu', N'Bà Rịa - Vũng Tàu', 1);

IF NOT EXISTS (SELECT 1 FROM dbo.Areas WHERE Code = 'DALAT')
    INSERT INTO dbo.Areas (Code, Name, Description, City, Province, IsActive)
    VALUES ('DALAT', N'Đà Lạt', N'Khu vực Đà Lạt', N'Đà Lạt', N'Lâm Đồng', 1);
GO

-- ============================================================
-- PHẦN 2: SEED DATA – Products (Area Packs + Pro Pack)
-- ============================================================

-- Area Pack TP.HCM – 49.000đ / 24h
IF NOT EXISTS (SELECT 1 FROM dbo.Products WHERE ProductCode = 'AREA_HCMC_24H')
    INSERT INTO dbo.Products (ProductCode, ProductName, ProductType, Price, Currency,
        UnlockNarration, UnlockLanguages, UnlockQr, UnlockOffline, DurationHours, IsActive)
    VALUES ('AREA_HCMC_24H', N'Area Pack – TP. Hồ Chí Minh', 'AREA_PACK',
        49000, 'VND', 1, 1, 1, 0, 24, 1);

-- Area Pack Hà Nội – 59.000đ / 24h
IF NOT EXISTS (SELECT 1 FROM dbo.Products WHERE ProductCode = 'AREA_HANOI_24H')
    INSERT INTO dbo.Products (ProductCode, ProductName, ProductType, Price, Currency,
        UnlockNarration, UnlockLanguages, UnlockQr, UnlockOffline, DurationHours, IsActive)
    VALUES ('AREA_HANOI_24H', N'Area Pack – Hà Nội', 'AREA_PACK',
        59000, 'VND', 1, 1, 1, 0, 24, 1);

-- Area Pack Vũng Tàu – 39.000đ / 24h
IF NOT EXISTS (SELECT 1 FROM dbo.Products WHERE ProductCode = 'AREA_VUNGTAU_24H')
    INSERT INTO dbo.Products (ProductCode, ProductName, ProductType, Price, Currency,
        UnlockNarration, UnlockLanguages, UnlockQr, UnlockOffline, DurationHours, IsActive)
    VALUES ('AREA_VUNGTAU_24H', N'Area Pack – Vũng Tàu', 'AREA_PACK',
        39000, 'VND', 1, 1, 1, 0, 24, 1);

-- Area Pack Đà Lạt – 45.000đ / 24h
IF NOT EXISTS (SELECT 1 FROM dbo.Products WHERE ProductCode = 'AREA_DALAT_24H')
    INSERT INTO dbo.Products (ProductCode, ProductName, ProductType, Price, Currency,
        UnlockNarration, UnlockLanguages, UnlockQr, UnlockOffline, DurationHours, IsActive)
    VALUES ('AREA_DALAT_24H', N'Area Pack – Đà Lạt', 'AREA_PACK',
        45000, 'VND', 1, 1, 1, 0, 24, 1);

-- Pro Pack – 199.000đ / 30 ngày (720h)
IF NOT EXISTS (SELECT 1 FROM dbo.Products WHERE ProductCode = 'PRO_30D')
    INSERT INTO dbo.Products (ProductCode, ProductName, ProductType, Price, Currency,
        UnlockNarration, UnlockLanguages, UnlockQr, UnlockOffline, DurationHours, IsActive)
    VALUES ('PRO_30D', N'Pro Pack – 30 ngày', 'PRO',
        199000, 'VND', 1, 1, 1, 1, 720, 1);
GO

-- ============================================================
-- PHẦN 3: SEED DATA – ProductAreas (map Area Pack → Area)
-- ============================================================

INSERT INTO dbo.ProductAreas (ProductId, AreaId)
SELECT p.ProductId, a.AreaId
FROM dbo.Products p
CROSS JOIN dbo.Areas a
WHERE p.ProductCode = 'AREA_HCMC_24H' AND a.Code = 'HCMC'
  AND NOT EXISTS (
      SELECT 1 FROM dbo.ProductAreas pa
      WHERE pa.ProductId = p.ProductId AND pa.AreaId = a.AreaId
  );

INSERT INTO dbo.ProductAreas (ProductId, AreaId)
SELECT p.ProductId, a.AreaId
FROM dbo.Products p
CROSS JOIN dbo.Areas a
WHERE p.ProductCode = 'AREA_HANOI_24H' AND a.Code = 'HANOI'
  AND NOT EXISTS (
      SELECT 1 FROM dbo.ProductAreas pa
      WHERE pa.ProductId = p.ProductId AND pa.AreaId = a.AreaId
  );

INSERT INTO dbo.ProductAreas (ProductId, AreaId)
SELECT p.ProductId, a.AreaId
FROM dbo.Products p
CROSS JOIN dbo.Areas a
WHERE p.ProductCode = 'AREA_VUNGTAU_24H' AND a.Code = 'VUNGTAU'
  AND NOT EXISTS (
      SELECT 1 FROM dbo.ProductAreas pa
      WHERE pa.ProductId = p.ProductId AND pa.AreaId = a.AreaId
  );

INSERT INTO dbo.ProductAreas (ProductId, AreaId)
SELECT p.ProductId, a.AreaId
FROM dbo.Products p
CROSS JOIN dbo.Areas a
WHERE p.ProductCode = 'AREA_DALAT_24H' AND a.Code = 'DALAT'
  AND NOT EXISTS (
      SELECT 1 FROM dbo.ProductAreas pa
      WHERE pa.ProductId = p.ProductId AND pa.AreaId = a.AreaId
  );
GO

-- ============================================================
-- PHẦN 4: SP – GetUsageStatus  (GET /api/me/usage-status)
-- ============================================================
IF OBJECT_ID('dbo.GetUsageStatus', 'P') IS NOT NULL
    DROP PROCEDURE dbo.GetUsageStatus;
GO

CREATE PROCEDURE dbo.GetUsageStatus
    @UserId     UNIQUEIDENTIFIER = NULL,
    @DeviceId   NVARCHAR(100)    = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @SubjectType VARCHAR(20);
    DECLARE @SubjectId   NVARCHAR(100);
    DECLARE @Now         DATETIME2(3) = SYSUTCDATETIME();

    IF @UserId IS NOT NULL
    BEGIN
        SET @SubjectType = 'USER';
        SET @SubjectId   = CONVERT(NVARCHAR(100), @UserId);
    END
    ELSE IF @DeviceId IS NOT NULL
    BEGIN
        SET @SubjectType = 'GUEST_DEVICE';
        SET @SubjectId   = @DeviceId;
    END
    ELSE
        THROW 51003, 'Either UserId or DeviceId must be provided.', 1;

    -- đếm lượt POI_LISTEN trong 24h rolling window
    DECLARE @UsedLast24h INT;
    SELECT @UsedLast24h = COUNT(*)
    FROM dbo.UsageEvents
    WHERE SubjectType = @SubjectType
      AND SubjectId   = @SubjectId
      AND ActionType  = 'POI_LISTEN'
      AND OccurredAt >= DATEADD(HOUR, -24, @Now);

    SET @UsedLast24h = ISNULL(@UsedLast24h, 0);

    -- kiểm tra Pro còn hiệu lực
    DECLARE @HasPro BIT = 0;
    IF @UserId IS NOT NULL AND EXISTS (
        SELECT 1 FROM dbo.Users u
        WHERE u.UserId = @UserId
          AND u.PlanType = 'PRO'
          AND (u.ProExpiryDate IS NULL OR u.ProExpiryDate > @Now)
    )
        SET @HasPro = 1;

    IF @HasPro = 0 AND @UserId IS NOT NULL AND EXISTS (
        SELECT 1
        FROM dbo.UserEntitlements ue
        JOIN dbo.Products p ON p.ProductId = ue.ProductId
        WHERE ue.UserId = @UserId
          AND ue.Status = 'ACTIVE'
          AND (ue.ExpiresAt IS NULL OR ue.ExpiresAt > @Now)
          AND p.ProductType = 'PRO'
    )
        SET @HasPro = 1;

    SELECT
        @SubjectType                                             AS SubjectType,
        @SubjectId                                              AS SubjectId,
        @UsedLast24h                                            AS UsedLast24h,
        CASE WHEN @HasPro = 1 THEN NULL ELSE 5 - @UsedLast24h END
                                                                AS RemainingFreeUses,
        @HasPro                                                 AS HasActivePro,
        CASE WHEN @HasPro = 0 AND @UsedLast24h >= 5 THEN 1 ELSE 0 END
                                                                AS IsFreeLimitExceeded;
END
GO

-- ============================================================
-- PHẦN 5: SP – GetUserEntitlements  (GET /api/me/entitlements)
-- ============================================================
IF OBJECT_ID('dbo.GetUserEntitlements', 'P') IS NOT NULL
    DROP PROCEDURE dbo.GetUserEntitlements;
GO

CREATE PROCEDURE dbo.GetUserEntitlements
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Now DATETIME2(3) = SYSUTCDATETIME();

    SELECT
        ue.EntitlementId,
        ue.ProductId,
        p.ProductCode,
        p.ProductName,
        p.ProductType,
        ue.EntitlementType,
        ue.StartsAt,
        ue.ExpiresAt,
        ue.Status,
        CASE WHEN ue.ExpiresAt IS NULL OR ue.ExpiresAt > @Now THEN 1 ELSE 0 END AS IsValid,
        -- Danh sách AreaId cho AREA_PACK (NULL nếu là PRO)
        (
            SELECT STRING_AGG(CAST(pa.AreaId AS VARCHAR), ',')
            FROM dbo.ProductAreas pa
            WHERE pa.ProductId = p.ProductId
        ) AS AreaIds,
        (
            SELECT STRING_AGG(a.Code, ',')
            FROM dbo.ProductAreas pa
            JOIN dbo.Areas a ON a.AreaId = pa.AreaId
            WHERE pa.ProductId = p.ProductId
        ) AS AreaCodes
    FROM dbo.UserEntitlements ue
    JOIN dbo.Products p ON p.ProductId = ue.ProductId
    WHERE ue.UserId = @UserId
      AND ue.Status = 'ACTIVE'
    ORDER BY ue.StartsAt DESC;
END
GO
