# 📱 14. Đặc Tả Module: Quản Lý & Giám Sát Thiết Bị (Device Management)

![Status](https://img.shields.io/badge/Status-Proposed-orange)
![Module](https://img.shields.io/badge/Module-Web_Admin_CMS-blue)
![Realtime](https://img.shields.io/badge/Tracking-Near_Real--time-brightgreen)

> **Mục tiêu:** Cung cấp cho Ban quản trị (Admin) một Dashboard tổng quan để giám sát lượng khách du lịch đang sử dụng ứng dụng di động. Module này thống kê tổng số thiết bị đã tải App, số lượng người đang trực tuyến (Online) và danh sách chi tiết các thiết bị kèm theo trạng thái hoạt động mới nhất.

---

## 📊 1. Giao diện Web Admin (CMS Dashboard)

Giao diện Quản lý Thiết bị được thiết kế tối ưu hóa cho màn hình máy tính, chia làm 2 thành phần chính:

### 1.1. Khu vực Thẻ thống kê (Overview Cards)
Nằm ở trên cùng của trang, cung cấp số liệu tổng quan (Cập nhật tự động mỗi 30 giây):
* 📲 **Tổng thiết bị đã tải App:** Số lượng toàn bộ thiết bị từng mở ứng dụng.
* 🟢 **Đang trực tuyến (Online):** Số lượng thiết bị đang mở ứng dụng tại thời điểm hiện tại.
* 🔴 **Ngoại tuyến (Offline):** Số lượng thiết bị đã tắt ứng dụng hoặc mất kết nối.

### 1.2. Bảng Danh sách Thiết bị (Device Data Table)
Bảng dữ liệu liệt kê chi tiết từng máy với các cột thông tin:
* **Mã thiết bị (Device ID):** Hiển thị rút gọn (VD: `d7a8e2...`) để bảo mật.
* **Hệ điều hành (Platform):** Hiển thị icon 🤖 Android hoặc 🍏 iOS.
* **Phiên bản App:** (VD: `v1.2.0`).
* **Hoạt động cuối (Last Active):** Hiển thị thời gian cụ thể (VD: `10:30 AM, 19/04/2026`).
* **Trạng thái (Status):** * 🟢 `Online` (Nền xanh lá): Hoạt động trong vòng 5 phút qua.
    * ⚪ `Offline` (Nền xám): Không có tín hiệu > 5 phút.
* **Nút "Xem vị trí":** Click vào sẽ mở pop-up hiển thị vị trí cuối cùng của thiết bị trên Bản đồ (Sử dụng cột `LastLatitude`, `LastLongitude`).

---

## ⚙️ 2. Thuật toán xác định Trạng thái (Online/Offline Logic)

Do ứng dụng di động không phải lúc nào cũng gửi được sự kiện "Tắt App" (VD: Mất mạng, sập nguồn, force close), hệ thống Backend không lưu cứng chữ "Online" hay "Offline" vào Database. 

Thay vào đó, hệ thống sử dụng **Cơ chế Timeout dựa trên `LastActiveAt`**:
* **Mobile App:** Liên tục bắn tín hiệu Ping (cập nhật tọa độ) lên Server mỗi 3 phút/lần. Server cập nhật `LastActiveAt = SYSUTCDATETIME()`.
* **Backend API (Khi Admin gọi list):** * Nếu thời gian hiện tại trừ đi `LastActiveAt` **<= 5 phút** $\rightarrow$ Thiết bị đang **ONLINE**.
  * Nếu thời gian hiện tại trừ đi `LastActiveAt` **> 5 phút** $\rightarrow$ Thiết bị đã **OFFLINE**.

---

## 🗄️ 3. Truy vấn Cơ sở dữ liệu (SQL Queries)

Tận dụng trực tiếp bảng `[dbo].[GuestDevices]` trong DB của bạn. Dưới đây là các câu lệnh SQL cốt lõi Backend sẽ sử dụng:

**1. Thống kê số lượng (Tổng quát & Online):**
```sql
DECLARE @TimeoutThreshold datetime2(3) = DATEADD(minute, -5, SYSUTCDATETIME());

-- Lấy Tổng thiết bị
SELECT COUNT(DeviceId) AS TotalDevices FROM dbo.GuestDevices;

-- Lấy Tổng thiết bị đang Online
SELECT COUNT(DeviceId) AS TotalOnline 
FROM dbo.GuestDevices 
WHERE LastActiveAt >= @TimeoutThreshold;