# 🎨 17. Đặc Tả Module: Nâng cấp Trải nghiệm UX/UI (Thiết kế chuẩn Google)

![Status](https://img.shields.io/badge/Status-Redesigning-orange)
![Tech](https://img.shields.io/badge/Tech-.NET_MAUI_XAML-512BD4)
![UX](https://img.shields.io/badge/UX-Google_Material_Design-brightgreen)

> **Mục tiêu:** Cải tổ toàn diện giao diện Mobile App nhằm mang lại trải nghiệm mượt mà, hiện đại và trực quan hơn cho du khách. Mọi logic xử lý dữ liệu và API Backend giữ nguyên 100%. Trọng tâm thay đổi nằm ở cấu trúc Component XAML, hiệu ứng Animation và Layout.

---

## 🗺️ 1. Tối ưu Màn hình Bản đồ Chính (Main Map View)

Dựa trên hình ảnh thực tế, giao diện bản đồ cũ đang bị chiếm dụng không gian bởi thanh Navigation Bar và khung chọn Tour. Chúng ta sẽ "giải phóng" không gian này để bản đồ tràn viền.

### 1.1. Dọn dẹp không gian (Clean up)
* **Xóa bỏ Top App Bar:** Xóa hoàn toàn thanh điều hướng màu đen trên cùng (Chứa nút Hamburger `≡`, chữ `QR` và menu 3 chấm `⋮`).
* **Xóa bỏ Dropdown Tour:** Xóa bỏ khung chọn "Tất cả địa điểm / Tour POI" cũ rườm rà.

### 1.2. Floating Search Bar (Thanh tìm kiếm nổi)
* Thay thế chức năng Tour bằng một **Thanh tìm kiếm POI nổi (Floating Search Bar)** nằm bo tròn ở cạnh trên màn hình (Giống hệt Google Maps).
* Khung Search này có đổ bóng (Shadow) nhẹ. Bên trái chứa icon 🔍, bên phải tích hợp luôn icon **Quét mã QR** và icon **Avatar Người dùng** (Bấm vào Avatar sẽ mở trang Profile).

---

## ✨ 2. Tương tác POI & Hiệu ứng Camera (Smart Interactions)

Mang lại cảm giác mượt mà và tập trung khi người dùng tương tác với các điểm tham quan trên bản đồ.

### 2.1. Hiệu ứng Zoom-in (Camera Animation)
* Khi người dùng chạm (Tap) vào một Marker POI trên bản đồ, hệ thống không chỉ hiện Bottom Sheet mà sẽ kích hoạt hàm di chuyển Camera của MAUI Map.
* **Hành động:** Bản đồ tự động mượt mà lướt tới (Pan) và **phóng to (Zoom in)** tập trung chính giữa điểm POI đó.

### 2.2. Tái cấu trúc Bottom Sheet (Chuẩn Google Maps)
Bottom Sheet trượt từ dưới lên sẽ được thiết kế lại hoàn toàn chia làm 3 phân vùng rõ rệt:
1. **Khu vực Media (Trên cùng):** * Chiếm khoảng 30% chiều cao Bottom Sheet. 
    * Nếu POI có nhiều ảnh, hiển thị dưới dạng thanh trượt ngang (Horizontal Scroll/Carousel). Người dùng vuốt sang trái/phải để xem ảnh, ảnh bo góc mượt mà.
2. **Khu vực Thông tin (Giữa):**
    * **Tên địa điểm:** Font chữ to, đậm (Heading 1).
    * **Tọa độ (Lat/Long):** Font nhỏ, màu xám nhạt nằm dưới tên.
    * **Mô tả/Đánh giá:** Một đoạn text tóm tắt nội dung POI.
3. **Khu vực Nút Hành động (Dưới cùng - Action Row):** Dàn hàng ngang các nút bấm (Bo góc tròn giống Google):
    * 🚙 **Đường đi (Chỉ dành cho PRO):** Nút nổi bật nhất màu xanh dương. Nếu là User Free, có icon ổ khóa nhỏ bên cạnh. Bấm vào sẽ vẽ đường màu xanh trên map.
    * 🗺️ **Mở Google Maps:** Nút màu xám/trắng. Bấm vào sẽ dùng `Launcher.OpenAsync` đẩy tọa độ sang app Google Maps của điện thoại.
    * 🎧 **Nghe Thuyết minh:** Nút phát Audio TTS/Podcast.

---

## 👤 3. Lột xác Giao diện Quản lý Cá nhân (Google Account Style)

Xóa bỏ giao diện Profile màu xanh đen cũ. Áp dụng phong cách Material Design của trang quản lý "Google Account".

### 3.1. Nút Thoát nhanh (Exit/Close Button)
* Ở góc trên cùng bên phải (hoặc trái) của trang Profile và trang Upgrade PRO, thêm một **nút Dấu "X" (Close)** hoặc mũi tên "←" to, rõ ràng.
* Hành động: Bấm vào sẽ đóng trang Cá nhân và quay lập tức về màn hình Bản đồ chính trọn vẹn.

### 3.2. Bố cục Profile (Layout)
* **Khu vực Header:**
    * Chứa Avatar hình tròn cỡ lớn nằm chính giữa màn hình (Có icon 📷 nhỏ đính kèm để thay ảnh).
    * Dòng chữ chào mừng: `Xin chào, [Tên Người Dùng]!` (Font to, thanh lịch).
    * Nút badge quản lý: Nút hiển thị hạng tài khoản `Tài khoản Miễn phí` (Màu xám) hoặc `🌟 Tài khoản PRO` (Hào quang vàng).
* **Khu vực Menu (List View):**
    * Đặt trong các thẻ Card màu trắng, nền trang màu kem/xám nhạt. Các mục menu được bo góc.
    * 🚀 **Nâng cấp Gói PRO** (Nằm tách biệt ở trên cùng để thu hút sự chú ý).
    * 👤 Hồ sơ của bạn (Cập nhật thông tin).
    * 📈 Dòng thời gian của bạn (Nhật ký hành trình - PRO).
    * 📍 Các địa điểm đã lưu/nghe (Lịch sử xem POI).
    * 🚪 Đăng xuất (Nằm cuối cùng, text màu đỏ).

### 3.3. Màn hình Nâng cấp PRO
* Khi bấm "Nâng cấp Gói PRO" từ Profile, mở ra Modal/Trang mới có nút "X" để thoát.
* Giao diện trình bày các quyền lợi độc quyền (Nghe vô hạn, Quét QR vô hạn, Dẫn đường) dưới dạng thẻ Card lớn, sử dụng icon rực rỡ để kích thích tỷ lệ chuyển đổi (Conversion Rate).