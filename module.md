# 🔲 21. Đặc Tả Module: Trung Tâm Quản Lý & Tạo Mã QR (QR Hub)

![Status](https://img.shields.io/badge/Status-Proposed-orange)
![Module](https://img.shields.io/badge/Module-Web_Admin_CMS-blue)
![Feature](https://img.shields.io/badge/Feature-Multi--Purpose_QR-brightgreen)

> **Mục tiêu:** Cung cấp một giao diện hợp nhất giúp Quản trị viên (Admin) tạo nhanh hai loại mã QR cốt lõi của hệ thống trên cùng một trang quản lý:
> 1. **QR Thuyết minh (Nội bộ):** Vé điện tử để kích hoạt nội dung đa phương tiện trực tiếp trong App.
> 2. **QR Tải App (Công cộng):** Tự động nhận diện thiết bị và điều hướng người dùng mới đến Google Play/App Store.

---

## 🎨 1. Thiết kế Giao diện UI/UX (Web Admin)

Màn hình quản lý QR sẽ được chia thành hai phân khu rõ rệt (Card-based UI hoặc Tabs) để tránh nhầm lẫn trong thao tác.

### 1.1. Phân hệ 1: Tạo QR Thuyết minh (POI Tickets)
Dùng để tạo các "vé điện tử" cung cấp cho khách du lịch để nghe thuyết minh tại các trạm.
* **Các trường nhập liệu (Inputs):**
    * **Chọn địa điểm (POI):** Danh sách thả xuống lấy dữ liệu từ bảng `Pois`.
    * **Chọn ngôn ngữ:** Định dạng theo bảng `SupportedLanguages` (VD: Tiếng Việt, English).
    * **Số lần sử dụng (MaxUses):** Giới hạn số lần quét của mã này (Mặc định: 5).
* **Hành động:** Bấm nút **[Tạo vé Thuyết minh]**. Hệ thống sinh mã ngẫu nhiên, lưu vào bảng `PoiTickets` và hiển thị hình ảnh QR Code.

### 1.2. Phân hệ 2: Tạo QR Tải App (Smart Distribution)
Dùng để tạo các mã QR in trên poster, standee tại các điểm tham quan nhằm thu hút người dùng tải App.
* **Các trường nhập liệu (Inputs):**
    * **Vị trí đặt mã (LocationName):** Ví dụ: *"Cổng chính Dinh Độc Lập"*, *"Trạm xe buýt trung tâm"*.
    * **Mã chiến dịch (CampaignCode):** Ví dụ: *"SUMMER_2026"*. (Có thể để trống).
* **Hành động:** Bấm nút **[Tạo QR Tải App]**. Hệ thống lưu vào bảng `AppDownloadSources` và sinh mã QR chứa Smart Link.

---

## 🗄️ 2. Ánh xạ Cơ sở dữ liệu & API Backend

Hệ thống tương tác đồng thời với các bảng nghiệp vụ và bảng thống kê đã thiết kế trong CSDL.

### 2.1. Cấu trúc dữ liệu liên quan
* **Loại 1 (Thuyết minh):** Sử dụng bảng `[dbo].[PoiTickets]` để lưu mã vé, số lần quét hiện tại và cấu hình âm thanh.
* **Loại 2 (Tải App):** Sử dụng bảng `[dbo].[AppDownloadSources]` để định danh nguồn quét và phân tích vị trí đặt bảng hướng dẫn nào mang lại nhiều lượt tải nhất.

### 2.2. Danh sách API Endpoints (.NET 10)

| Method | Endpoint | Quyền | Nhiệm vụ |
| :--- | :--- | :---: | :--- |
| `POST` | `/api/v1/admin/qr/narration` | Admin | Tạo mã vé mới trong bảng `PoiTickets`. Trả về `ticketCode`. |
| `POST` | `/api/v1/admin/qr/distribution` | Admin | Tạo nguồn tải App mới trong bảng `AppDownloadSources`. Trả về `sourceId`. |
| `GET` | `/api/v1/admin/qr/history` | Admin | Lấy danh sách các mã QR đã tạo để xem lại hoặc tải ảnh về. |

---

## ⚙️ 3. Quy trình thực hiện (Sequence Diagram)

Sơ đồ mô tả logic xử lý của hệ thống khi Admin thực hiện tạo mã QR Tải App.

```mermaid
sequenceDiagram
    participant Admin as 💻 Web Admin UI
    participant API as ⚡ Backend API
    participant DB as 🗄️ SQL Server

    Admin->>Admin: Điền form "QR Tải App" (Vị trí: Bưu Điện)
    Admin->>API: POST /qr/distribution
    activate API
    API->>DB: INSERT INTO AppDownloadSources
    DB-->>API: Trả về SourceId (VD: 15)
    API-->>Admin: Trả về JSON { sourceId: 15 }
    deactivate API

    Note over Admin: Render QR Code với cấu trúc Smart Link:
    Note over Admin: [https://api.gpsapp.vn/v1/download/15](https://api.gpsapp.vn/v1/download/15)
    Admin->>Admin: Tải ảnh QR (.png) để gửi đi in ấn