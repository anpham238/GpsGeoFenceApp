\# 📱 ĐẶC TẢ MODULE: NHẬN DIỆN \& THEO DÕI THIẾT BỊ (GUEST TRACKING)



!\[Status](https://img.shields.io/badge/Status-In\_Planning-orange)

!\[Module](https://img.shields.io/badge/Module-Core\_Tracking-blue)

!\[Privacy](https://img.shields.io/badge/Privacy-Anonymous\_Data-brightgreen)



> \*\*Mục tiêu:\*\* Nhận diện các thiết bị di động tải và mở ứng dụng (Khách du lịch vãng lai) mà \*\*không yêu cầu đăng ký/đăng nhập\*\*. Hệ thống sẽ tự động cấp phát một ID ẩn danh, lưu trữ tọa độ GPS hiện tại và theo dõi trạng thái hoạt động (Online/Offline) của thiết bị đó.

\---

\## 1. Yêu cầu chức năng (Functional Requirements)

\### 1.1. Phía Mobile App (.NET MAUI)

\* \*\*Khởi tạo Device ID:\*\* Trong lần mở App đầu tiên, ứng dụng tự động sinh ra một chuỗi định danh duy nhất (UUID/GUID) và lưu vào `SecureStorage` hoặc `Preferences` của thiết bị.

\* \*\*Ghi nhận thông tin:\*\* Lấy thông tin cơ bản của thiết bị (Hệ điều hành Android/iOS, Version) để phục vụ thống kê.

\* \*\*Heartbeat (Nhịp tim):\*\* Khi App đang mở (Foreground) hoặc chạy ngầm (Background Location đang bật), App sẽ gửi một request (Ping) lên Server mỗi `X` phút hoặc mỗi khi vị trí thay đổi đáng kể, kèm theo tọa độ GPS mới nhất.

\* \*\*Bắt sự kiện Lifecycle:\*\* Gửi tín hiệu khi người dùng tắt App (App Sleep / Destroy) nếu có mạng.

\### 1.2. Phía Backend API \& Server

\* \*\*Quản lý phiên (Session):\*\* Nhận Ping từ thiết bị, nếu `DeviceId` chưa tồn tại thì tạo mới, nếu đã tồn tại thì cập nhật tọa độ và thời gian `LastActiveAt`.

\* \*\*Xác định Online/Offline:\*\* \* Không phụ thuộc hoàn toàn vào tín hiệu tắt App từ điện thoại (vì App có thể bị crash hoặc mất mạng đột ngột).

&#x20; \* \*\*Quy tắc (Timeout Rule):\*\* Thiết bị được coi là \*\*Online\*\* nếu `LastActiveAt` cách thời điểm hiện tại dưới `5 phút`. Nếu quá 5 phút không nhận được Ping, tự động đánh dấu là \*\*Offline\*\*.

\---

\## 2. Thiết kế Cơ sở dữ liệu (Database Schema)



Cần tạo thêm một bảng `GuestDevices` để quản lý các thiết bị này, tách biệt với bảng `Users` (dành cho Admin hoặc người có tài khoản).



```sql

CREATE TABLE \[dbo].\[GuestDevices](

&#x20;   \[DeviceId] \[varchar](100) NOT NULL, -- UUID sinh ra từ App

&#x20;   \[Platform] \[nvarchar](20) NULL,     -- Android / iOS

&#x20;   \[AppVersion] \[nvarchar](20) NULL,

&#x20;   \[LastLatitude] \[float] NULL,

&#x20;   \[LastLongitude] \[float] NULL,

&#x20;   \[FirstSeenAt] \[datetime2](3) NOT NULL DEFAULT (SYSUTCDATETIME()),

&#x20;   \[LastActiveAt] \[datetime2](3) NOT NULL DEFAULT (SYSUTCDATETIME()),

&#x20;   CONSTRAINT \[PK\_GuestDevices] PRIMARY KEY CLUSTERED (\[DeviceId] ASC)

);

\-- Index để tối ưu việc query danh sách thiết bị đang Online

CREATE NONCLUSTERED INDEX \[IX\_GuestDevices\_LastActive] ON \[dbo].\[GuestDevices]

(

&#x20;   \[LastActiveAt] DESC

);

