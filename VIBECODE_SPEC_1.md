# 🎫 ĐẶC TẢ MODULE: TẠO VÀ QUẢN LÝ QR CODE (GIỚI HẠN LƯỢT QUÉT)

![Status](https://img.shields.io/badge/Status-In_Planning-orange)
![Module](https://img.shields.io/badge/Module-Web_CMS_%26_Mobile-blue)
![Database](https://img.shields.io/badge/Table-PoiTickets-success)

> **Mục tiêu:** Cung cấp công cụ cho Web Admin tạo ra các mã QR Code định danh chứa nội dung thuyết minh của một Điểm tham quan (POI) cụ thể theo ngôn ngữ được chọn. Mỗi mã QR được cấp một `TicketCode` duy nhất và chỉ có giá trị sử dụng tối đa **5 lần quét**.

---

## 1. Yêu cầu chức năng (Functional Requirements)

### 1.1. Phía Quản trị viên (Web Admin CMS)
* **Giao diện tạo QR:**
    * Dropdown chọn **POI** (Load từ bảng `Pois`).
    * Dropdown chọn **Ngôn ngữ** (Load từ bảng `PoiLanguage` tương ứng với POI đã chọn).
    * Nút bấm **"Tạo QR Code"**.
* **Xử lý kết quả:**
    * Hệ thống sinh ra một chuỗi ngẫu nhiên, duy nhất làm mã vé (`TicketCode`).
    * Tạo hình ảnh mã QR chứa đường dẫn (Deep link/URL) có nhúng `TicketCode` này.
    * Hiển thị hình ảnh mã QR lên màn hình kèm theo nút **"Tải ảnh QR Code (.PNG)"** để Admin in ấn hoặc chia sẻ.

### 1.2. Phía Người dùng (End-User / Mobile App)
* **Hành động quét:** Người dùng sử dụng App (hoặc Camera điện thoại) quét mã QR.
* **Xác thực:** Hệ thống kiểm tra số lượt đã sử dụng (`CurrentUses`) so với giới hạn (`MaxUses = 5`).
* **Hiển thị kết quả:**
    * **Thành công (< 5 lần):** Tăng `CurrentUses` lên 1. Mở màn hình hiển thị nội dung thuyết minh (ảnh, audio, text) từ bảng `PoiLanguage` và `PoiMedia` tương ứng.
    * **Thất bại (>= 5 lần):** Hiển thị thông báo lỗi: *"Mã QR này đã vượt quá số lần sử dụng cho phép (5/5)."*

---

## 2. Ánh xạ Cơ sở dữ liệu (Database Mapping)

Tính năng này sẽ sử dụng trực tiếp bảng `[dbo].[PoiTickets]` đã được định nghĩa trong file `GpsApp_Redesigned.sql`.

| Cột (Column) | Kiểu dữ liệu | Ràng buộc / Mặc định | Ý nghĩa nghiệp vụ |
| :--- | :--- | :--- | :--- |
| `TicketCode` | `varchar(50)` | **PK** | Chuỗi mã hóa nhúng vào trong hình ảnh QR Code (VD: `QR-POI1-VI-XYZ123`). |
| `IdPoi` | `int` | **FK** -> `Pois.Id` | Khóa ngoại trỏ đến Điểm tham quan. |
| `LanguageTag` | `nvarchar(10)`| Không | Mã ngôn ngữ (VD: `vi`, `en`) để query bảng `PoiLanguage`. |
| `MaxUses` | `int` | Default: `5` | Giới hạn số lần quét (Cứng: 5 lần). |
| `CurrentUses`| `int` | Default: `0` | Số lần đã quét thực tế (Tăng dần sau mỗi lần người dùng quét). |
| `CreatedAt` | `datetime2(3)`| Default: `sysutcdatetime()` | Thời điểm tạo mã QR. |

---

## 3. Thiết kế API Endpoints (Backend - C# .NET 10)

| HTTP Method | Endpoint | Quyền (Auth) | Mô tả |
| :--- | :--- | :---: | :--- |
| `POST` | `/api/v1/tickets/generate` | **Admin** | Nhận payload `{ IdPoi, LanguageTag }`. Sinh `TicketCode`, Insert vào DB và trả về dạng Base64/Link ảnh QR. |
| `POST` | `/api/v1/tickets/scan/{ticketCode}`| **Public/User**| Hàm thực thi nghiệp vụ quét. Trừ lượt và trả về Data POI. |

---

## 4. Luồng xử lý nghiệp vụ (UML Sequence Diagram)

Sơ đồ dưới đây mô tả luồng kiểm tra logic cực kỳ chặt chẽ khi người dùng thực hiện quét mã:

```mermaid
sequenceDiagram
    participant User as Người dùng (App)
    participant API as Backend API
    participant DB as SQL Server (PoiTickets)

    User->>API: POST /api/v1/tickets/scan/{TicketCode}
    activate API
    
    API->>DB: Kiểm tra TicketCode
    DB-->>API: Trả về dòng dữ liệu Ticket (IdPoi, Lang, Max, Current)
    
    alt Không tìm thấy TicketCode
        API-->>User: Lỗi 404 - "Mã QR không tồn tại"
    else Tồn tại Ticket
        alt CurrentUses >= MaxUses
            API-->>User: Lỗi 403 - "Mã QR đã hết hạn sử dụng (5/5)"
        else CurrentUses < MaxUses
            %% Bắt đầu Transaction
            API->>DB: UPDATE PoiTickets SET CurrentUses = CurrentUses + 1 WHERE TicketCode = ...
            API->>DB: SELECT data FROM PoiLanguage, PoiMedia WHERE IdPoi = ... AND Lang = ...
            DB-->>API: Dữ liệu thuyết minh (TextToSpeech, Audio, Image)
            API-->>User: HTTP 200 OK + Dữ liệu thuyết minh
            User->>User: App mở màn hình phát Audio / Hiển thị Text
        end
    end
    deactivate API