-- ============================================================
-- RESET + SEED: PoiMedia (ảnh đại diện + Google Maps link)
-- Xóa toàn bộ dữ liệu cũ và insert lại với URLs đã xác nhận
-- ============================================================
SET NOCOUNT ON;
USE [GpsApi];

DELETE FROM dbo.PoiMedia;
DBCC CHECKIDENT ('dbo.PoiMedia', RESEED, 0);

INSERT INTO dbo.PoiMedia (IdPoi, Image, MapLink) VALUES

-- ── TP. Hồ Chí Minh ───────────────────────────────────────────────────────────

-- 1. Chợ Bến Thành
(1,  N'https://upload.wikimedia.org/wikipedia/commons/9/91/Ben_Thanh_market_2.jpg',
     N'https://maps.google.com/?q=10.7725,106.6980'),

-- 2. Nhà thờ Đức Bà
(2,  N'https://upload.wikimedia.org/wikipedia/commons/b/b7/20190923_Notre-Dame_Cathedral_Basilica_of_Saigon-1.jpg',
     N'https://maps.google.com/?q=10.7798,106.6991'),

-- 3. Dinh Độc Lập
(3,  N'https://upload.wikimedia.org/wikipedia/commons/7/7d/20190923_Independence_Palace-10.jpg',
     N'https://maps.google.com/?q=10.7770,106.6953'),

-- 4. Bảo tàng Chứng tích Chiến tranh
(4,  N'https://upload.wikimedia.org/wikipedia/commons/4/47/War_Remnants_Museum%2C_HCMC%2C_front.JPG',
     N'https://maps.google.com/?q=10.7780,106.6922'),

-- 5. Bưu điện Trung tâm Sài Gòn
(5,  N'https://upload.wikimedia.org/wikipedia/commons/7/78/Ho_Chi_Minh_City%2C_Central_Post_Office%2C_2020-01_CN-01.jpg',
     N'https://maps.google.com/?q=10.7799,106.6999'),

-- 6. Phố đi bộ Nguyễn Huệ
(6,  N'https://upload.wikimedia.org/wikipedia/commons/a/a3/Ho_Chi_Minh_City%2C_Nguyen_Hue_Street%2C_2020-01_CN-01.jpg',
     N'https://maps.google.com/?q=10.7731,106.7032'),

-- 7. Nhà hát Thành phố
(7,  N'https://upload.wikimedia.org/wikipedia/commons/1/10/Ho_Chi_Minh_City_Opera_House.jpg',
     N'https://maps.google.com/?q=10.7766,106.7031'),

-- 8. UBND Thành phố (Tòa nhà UBND)
(8,  N'https://upload.wikimedia.org/wikipedia/commons/3/3d/Ho_Chi_Minh_City%2C_City_Hall%2C_2020-01_CN-02.jpg',
     N'https://maps.google.com/?q=10.7763,106.7014'),

-- 10. Tháp Bitexco
(10, N'https://upload.wikimedia.org/wikipedia/commons/d/db/Bitexco_Financial_Tower%2C_Ho_Chi_Minh_City%2C_Vietnam.jpg',
     N'https://maps.google.com/?q=10.7716,106.7044'),

-- 11. Chợ Bình Tây (Chợ Lớn)
(11, N'https://upload.wikimedia.org/wikipedia/commons/d/d1/Binh_Tay_market_2.jpg',
     N'https://maps.google.com/?q=10.7496,106.6508'),

-- 14. Chùa Ngọc Hoàng
(14, N'https://upload.wikimedia.org/wikipedia/commons/1/10/Jade_Emperor_Pagoda_Saigon.jpg',
     N'https://maps.google.com/?q=10.7915,106.6983'),

-- 15. Landmark 81
(15, N'https://upload.wikimedia.org/wikipedia/commons/5/51/Landmark_81%2C_Ho_Chi_Minh_City%2C_Vietnam_-_February_2021.jpg',
     N'https://maps.google.com/?q=10.7946,106.7218'),

-- 18. Cầu Ba Son
(18, N'https://upload.wikimedia.org/wikipedia/commons/a/a9/C%E1%BA%A7u_Ba_Son%2C_TP.HCM%2C_042022.jpg',
     N'https://maps.google.com/?q=10.7816,106.7099'),

-- 20. Bến Nhà Rồng
(20, N'https://upload.wikimedia.org/wikipedia/commons/c/ce/Ben_Nha_Rong.JPG',
     N'https://maps.google.com/?q=10.7681,106.7067'),

-- ── Hà Nội ────────────────────────────────────────────────────────────────────

-- 21. Lăng Chủ tịch Hồ Chí Minh
(21, N'https://upload.wikimedia.org/wikipedia/commons/3/33/2012_Ho_Chi_Minh_Mausoleum.jpg',
     N'https://maps.google.com/?q=21.0368,105.8346'),

-- 22. Văn Miếu - Quốc Tử Giám
(22, N'https://upload.wikimedia.org/wikipedia/commons/d/d5/20220605_Temple_of_Literature%2C_Hanoi_01.jpg',
     N'https://maps.google.com/?q=21.0293,105.8361'),

-- ── Đà Nẵng ───────────────────────────────────────────────────────────────────

-- 23. Cầu Rồng
(23, N'https://upload.wikimedia.org/wikipedia/commons/d/d9/Da_Nang_Dragon_Bridge.jpg',
     N'https://maps.google.com/?q=16.0612,108.2274'),

-- ── Vũng Tàu ──────────────────────────────────────────────────────────────────

-- 28. Ngọn Hải Đăng Vũng Tàu
(28, N'https://upload.wikimedia.org/wikipedia/commons/d/df/Vung_Tau_lighthouse.JPG',
     N'https://maps.google.com/?q=10.3341,107.0784'),

-- ── Đà Lạt ────────────────────────────────────────────────────────────────────

-- 33. Hồ Xuân Hương
(33, N'https://upload.wikimedia.org/wikipedia/commons/1/1f/Da_Lat%2C_view_to_Xuan_Huong_lake.jpg',
     N'https://maps.google.com/?q=11.9404,108.4404');

GO
SELECT 'PoiMedia: ' + CAST(COUNT(*) AS VARCHAR) + ' records inserted' AS Result FROM dbo.PoiMedia;
PRINT N'Seed PoiMedia_Full hoàn tất.';
