# 🔐 12. Đặc Tả Module: Xác Thực Người Dùng (Authentication)

![Status](https://img.shields.io/badge/Status-Proposed-orange)
![Security](https://img.shields.io/badge/Security-JWT_%26_BCrypt-red)
![Database](https://img.shields.io/badge/Table-Users-success)

> **Mục tiêu:** Xây dựng hệ thống định danh người dùng (Khách du lịch đăng ký tài khoản hoặc Admin). Hệ thống được thiết kế để mang lại trải nghiệm tối ưu nhất:
> - Đăng nhập "thông minh" bằng 1 thanh input duy nhất (hỗ trợ cả Username và Email).
> - Đăng ký linh hoạt: Cho phép tải lên Avatar cá nhân hoặc tự động sử dụng Avatar mặc định nếu bỏ qua.

---

## 🗄️ 1. Ánh xạ Cơ sở dữ liệu (Database Mapping)

Module này sử dụng trực tiếp bảng `[dbo].[Users]` đã được định nghĩa trong hệ thống. Cấu trúc đã sẵn sàng cho mọi nghiệp vụ:

| Cột (Column) | Ràng buộc / Tính chất | Mục đích |
| :--- | :--- | :--- |
| `UserId` | `uniqueidentifier`, PK | Định danh duy nhất (GUID). |
| `Username` | `nvarchar(100)`, UNIQUE | Tên đăng nhập (Viết liền không dấu). |
| `Mail` | `nvarchar(200)`, UNIQUE | Địa chỉ Email dùng để liên lạc & đăng nhập. |
| `PhoneNumber` | `varchar(20)`, NULL | Số điện thoại liên hệ (Tùy chọn). |
| `PasswordHash` | `nvarchar(256)` | Mật khẩu đã được mã hóa 1 chiều (BCrypt). |
| `AvatarUrl` | DEFAULT `default-avatar.png` | Link ảnh đại diện. Tự động gán nếu user không upload. |

---

## 📱 2. Yêu cầu chức năng Giao diện (UI/UX)

### 2.1. Form Đăng ký (Register)
Giao diện đăng ký được chia làm 2 phần chính:
* **Khu vực Avatar (Ảnh đại diện):**
    * Hiển thị khung ảnh hình tròn.
    * Nút "Chọn ảnh / Tải lên" mở thư viện Media của điện thoại/máy tính.
    * *Logic Backend:* Nếu Form được submit mà không đính kèm file ảnh, hệ thống tự động lưu với giá trị Default của Database.
* **Khu vực Nhập liệu (Inputs):**
    * **Tên đăng nhập:** Ràng buộc không khoảng trắng, không ký tự đặc biệt.
    * **Email:** Ràng buộc định dạng chuẩn `@domain`.
    * **Số điện thoại:** Bàn phím số (Number pad).
    * **Mật khẩu:** Có nút icon 👁️ (Mắt) để ẩn/hiện mật khẩu. Yêu cầu độ dài tối thiểu (VD: 6 ký tự).
* **Hành động:** Nút bấm `Tạo tài khoản`. Bên dưới có link: *Đã có tài khoản? Đăng nhập*.

### 2.2. Form Đăng nhập (Smart Login)
Giao diện tối giản nhằm tăng tỷ lệ chuyển đổi:
* **Thanh định danh (Identifier):**
    * Nhãn (Label): `Tên đăng nhập hoặc Email`.
    * Người dùng không cần phải chọn mình đang nhập bằng gì. Backend sẽ tự động phân tích.
* **Mật khẩu:** Khung nhập mật khẩu (che dấu `*`).
* **Hành động:** Nút `Đăng nhập`. Link `Quên mật khẩu?`.

---

## 🔌 3. Thiết kế API Endpoints (Backend .NET 10)

Sử dụng cơ chế **JWT (JSON Web Token)** để cấp phiên làm việc không trạng thái (Stateless), giúp App Mobile hoạt động mượt mà.

| Method | Endpoint | Kiểu dữ liệu nhận | Nhiệm vụ |
| :--- | :--- | :--- | :--- |
| `POST` | `/api/v1/auth/register` | `multipart/form-data` | Nhận text và `File`. Mã hóa mật khẩu. Trả về 201 Created. |
| `POST` | `/api/v1/auth/login` | `application/json` | Nhận `{ "identifier", "password" }`. Trả về JWT Token. |
| `GET` | `/api/v1/auth/me` | `Authorization: Bearer` | Lấy profile của User đang đăng nhập dựa trên Token. |

---

## ⚙️ 4. Luồng xử lý nghiệp vụ (UML Sequence Diagrams)

### 4.1. Thuật toán Đăng nhập thông minh (Smart Login)
Sơ đồ mô tả cách Backend phân biệt Email và Username:

```mermaid
sequenceDiagram
    participant App as Mobile App
    participant API as Backend API
    participant DB as SQL Server

    App->>API: POST /login (Identifier, Password)
    activate API
    
    Note over API: Phân tích cú pháp Identifier
    API->>API: Identifier có chứa "@" không?
    
    alt Có "@" (Là Email)
        API->>DB: SELECT * FROM Users WHERE Mail = @Identifier
    else Không có "@" (Là Username)
        API->>DB: SELECT * FROM Users WHERE Username = @Identifier
    end
    
    DB-->>API: Trả về dòng dữ liệu (Nếu có)
    
    alt Không có dữ liệu HOẶC Verify(Password) == False
        API-->>App: HTTP 401 - Sai tài khoản hoặc mật khẩu
    else Xác thực thành công
        API->>API: Generate JWT Token (payload: UserId)
        API-->>App: HTTP 200 OK + { Token, AvatarUrl, Username }
    end
    deactivate API