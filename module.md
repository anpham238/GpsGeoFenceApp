# 👥 14. Đặc Tả Module: Quản Lý Người Dùng, Thiết Bị & Doanh Thu (User & Device Dashboard)

![Status](https://img.shields.io/badge/Status-Redesigned-orange)
![Module](https://img.shields.io/badge/Module-Web_Admin_CMS-blue)
![Monetization](https://img.shields.io/badge/Metrics-Revenue_Tracking-brightgreen)

> **Mục tiêu:** Cung cấp cho Ban quản trị (Admin) một Dashboard "All-in-One" để giám sát toàn cảnh lượng khách du lịch sử dụng hệ thống. Module bao quát từ khách vãng lai tải App (Thiết bị), số lượng thiết bị đang Online realtime, cho đến danh sách người dùng đã đăng ký tài khoản và doanh thu mang lại từ việc nâng cấp gói PRO.

---

## 📊 1. Giao diện Web Admin (CMS Dashboard)

Giao diện được chia làm 2 khu vực chính: **Thẻ thống kê (Overview Cards)** trên cùng và **Bảng dữ liệu (Data Table)** bên dưới.

### 1.1. Thẻ Thống kê Tổng quan (Real-time Metrics)
Hiển thị 4 con số quan trọng nhất của dự án (Tự động làm mới mỗi 30 giây):
1. 📱 **Tổng thiết bị (Lượt tải App):** Đếm tổng số thiết bị đã từng mở ứng dụng ít nhất một lần (Kể cả không đăng ký).
2. 🟢 **Thiết bị đang Online:** Số lượng thiết bị đang mở và tương tác với ứng dụng tại thời điểm hiện tại (Dựa vào thuật toán Timeout 5 phút).
3. 👤 **Tổng User Đăng ký:** Số lượng tài khoản chính thức đã được tạo trong hệ thống.
4. 💰 **Tổng Doanh thu gói PRO:** Tổng số tiền thu được từ những người dùng đã nâng cấp lên `PlanType = 'PRO'`.

### 1.2. Bảng Danh sách Thông tin Người dùng
Hiển thị chi tiết các tài khoản (User) kèm chức năng tìm kiếm và phân trang:
* **Tài khoản:** Avatar (Hình tròn nhỏ), Họ tên (`FullName`), Username.
* **Liên hệ:** Email (`Mail`), Số điện thoại (`PhoneNumber`).
* **Hạng gói (Plan):** * Thẻ xám: `FREE` (Cơ bản).
  * Thẻ vàng rực: `🌟 PRO` (Kèm ngày hết hạn `ProExpiryDate`).
* **Trạng thái:** 🟢 `Active` (Hoạt động) hoặc 🔴 `Locked` (Bị khóa).
* **Ngày tham gia:** Định dạng `dd/MM/yyyy` (Từ cột `CreatedAt`).
* **Hành động:** Nút "Sửa thông tin", Nút "Khóa/Mở khóa tài khoản".

---

## 🗄️ 2. Truy vấn Cơ sở dữ liệu (SQL Queries)

Module này giao tiếp với 2 bảng chính là `[dbo].[GuestDevices]` (Dành cho thiết bị vãng lai) và `[dbo].[Users]` (Dành cho tài khoản).

**2.1. Lấy dữ liệu cho 4 thẻ Thống kê (Dashboard Stats):**
*(Lưu ý: Giả định giá gói PRO là 50,000 VNĐ / tài khoản để tính doanh thu ước tính)*

```sql
DECLARE @TimeoutThreshold datetime2(3) = DATEADD(minute, -5, SYSUTCDATETIME());
DECLARE @ProPrice int = 50000; -- Giá trị gói PRO (VNĐ)

SELECT 
    (SELECT COUNT(DeviceId) FROM dbo.GuestDevices) AS TotalDevices,
    (SELECT COUNT(DeviceId) FROM dbo.GuestDevices WHERE LastActiveAt >= @TimeoutThreshold) AS OnlineDevices,
    (SELECT COUNT(UserId) FROM dbo.Users) AS TotalRegisteredUsers,
    (SELECT COUNT(UserId) * @ProPrice FROM dbo.Users WHERE PlanType = 'PRO') AS TotalProRevenue;