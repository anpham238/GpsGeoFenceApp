USE [GpsApi]
GO
SET NOCOUNT ON;

-- ============================================================
-- RESET + SEED: PoiImages gallery
-- Xóa toàn bộ dữ liệu cũ và insert lại với URLs đã xác nhận
-- ============================================================

DELETE FROM dbo.PoiImages;
DBCC CHECKIDENT ('dbo.PoiImages', RESEED, 0);

INSERT INTO dbo.PoiImages (IdPoi, ImageUrl, SortOrder, CreatedAt) VALUES

-- ── POI 1: Chợ Bến Thành ──────────────────────────────────────────────────────
(1, N'https://upload.wikimedia.org/wikipedia/commons/9/91/Ben_Thanh_market_2.jpg', 0, SYSUTCDATETIME()),

-- ── POI 2: Nhà thờ Đức Bà ─────────────────────────────────────────────────────
(2, N'https://upload.wikimedia.org/wikipedia/commons/b/b7/20190923_Notre-Dame_Cathedral_Basilica_of_Saigon-1.jpg', 0, SYSUTCDATETIME()),

-- ── POI 3: Dinh Độc Lập ───────────────────────────────────────────────────────
(3, N'https://upload.wikimedia.org/wikipedia/commons/7/7d/20190923_Independence_Palace-10.jpg', 0, SYSUTCDATETIME()),

-- ── POI 4: Bảo tàng Chứng tích Chiến tranh ───────────────────────────────────
(4, N'https://upload.wikimedia.org/wikipedia/commons/4/47/War_Remnants_Museum%2C_HCMC%2C_front.JPG', 0, SYSUTCDATETIME()),

-- ── POI 5: Bưu điện Trung tâm Sài Gòn ───────────────────────────────────────
(5, N'https://upload.wikimedia.org/wikipedia/commons/7/78/Ho_Chi_Minh_City%2C_Central_Post_Office%2C_2020-01_CN-01.jpg', 0, SYSUTCDATETIME()),

-- ── POI 6: Phố đi bộ Nguyễn Huệ ─────────────────────────────────────────────
(6, N'https://upload.wikimedia.org/wikipedia/commons/a/a3/Ho_Chi_Minh_City%2C_Nguyen_Hue_Street%2C_2020-01_CN-01.jpg', 0, SYSUTCDATETIME()),

-- ── POI 7: Nhà hát Thành phố ─────────────────────────────────────────────────
(7, N'https://upload.wikimedia.org/wikipedia/commons/1/10/Ho_Chi_Minh_City_Opera_House.jpg', 0, SYSUTCDATETIME()),

-- ── POI 8: UBND Thành phố ────────────────────────────────────────────────────
(8, N'https://upload.wikimedia.org/wikipedia/commons/3/3d/Ho_Chi_Minh_City%2C_City_Hall%2C_2020-01_CN-02.jpg', 0, SYSUTCDATETIME()),

-- ── POI 10: Tháp Bitexco ─────────────────────────────────────────────────────
(10, N'https://upload.wikimedia.org/wikipedia/commons/d/db/Bitexco_Financial_Tower%2C_Ho_Chi_Minh_City%2C_Vietnam.jpg', 0, SYSUTCDATETIME()),

-- ── POI 11: Chợ Bình Tây ─────────────────────────────────────────────────────
(11, N'https://upload.wikimedia.org/wikipedia/commons/d/d1/Binh_Tay_market_2.jpg', 0, SYSUTCDATETIME()),

-- ── POI 14: Chùa Ngọc Hoàng ──────────────────────────────────────────────────
(14, N'https://upload.wikimedia.org/wikipedia/commons/1/10/Jade_Emperor_Pagoda_Saigon.jpg', 0, SYSUTCDATETIME()),

-- ── POI 15: Landmark 81 ──────────────────────────────────────────────────────
(15, N'https://upload.wikimedia.org/wikipedia/commons/5/51/Landmark_81%2C_Ho_Chi_Minh_City%2C_Vietnam_-_February_2021.jpg', 0, SYSUTCDATETIME()),

-- ── POI 18: Cầu Ba Son ───────────────────────────────────────────────────────
(18, N'https://upload.wikimedia.org/wikipedia/commons/a/a9/C%E1%BA%A7u_Ba_Son%2C_TP.HCM%2C_042022.jpg', 0, SYSUTCDATETIME()),

-- ── POI 20: Bến Nhà Rồng ─────────────────────────────────────────────────────
(20, N'https://upload.wikimedia.org/wikipedia/commons/c/ce/Ben_Nha_Rong.JPG', 0, SYSUTCDATETIME()),

-- ── POI 21: Lăng Chủ tịch Hồ Chí Minh ──────────────────────────────────────
(21, N'https://upload.wikimedia.org/wikipedia/commons/3/33/2012_Ho_Chi_Minh_Mausoleum.jpg', 0, SYSUTCDATETIME()),

-- ── POI 22: Văn Miếu - Quốc Tử Giám ─────────────────────────────────────────
(22, N'https://upload.wikimedia.org/wikipedia/commons/d/d5/20220605_Temple_of_Literature%2C_Hanoi_01.jpg', 0, SYSUTCDATETIME()),

-- ── POI 23: Cầu Rồng (Đà Nẵng) ──────────────────────────────────────────────
(23, N'https://upload.wikimedia.org/wikipedia/commons/d/d9/Da_Nang_Dragon_Bridge.jpg', 0, SYSUTCDATETIME()),

-- ── POI 28: Ngọn Hải Đăng (Vũng Tàu) ────────────────────────────────────────
(28, N'https://upload.wikimedia.org/wikipedia/commons/d/df/Vung_Tau_lighthouse.JPG', 0, SYSUTCDATETIME()),

-- ── POI 33: Hồ Xuân Hương (Đà Lạt) ──────────────────────────────────────────
(33, N'https://upload.wikimedia.org/wikipedia/commons/1/1f/Da_Lat%2C_view_to_Xuan_Huong_lake.jpg', 0, SYSUTCDATETIME());

GO
SELECT 'PoiImages: ' + CAST(COUNT(*) AS VARCHAR) + ' records inserted' AS Result FROM dbo.PoiImages;
PRINT N'Seed PoiImages hoàn tất.';
