# 📦 Đặc Tả Các Phân Hệ Chức Năng (System Modules)

![Status](https://img.shields.io/badge/Status-Active_Development-brightgreen)
![Platform](https://img.shields.io/badge/Platform-.NET_MAUI-512BD4)
![Architecture](https://img.shields.io/badge/Architecture-Shared_Backend-orange)

> Tài liệu này liệt kê và mô tả chi tiết 9 phân hệ (modules) cấu thành nên hệ thống **GpsGeoFenceApp**. Hệ thống được thiết kế tối ưu hóa cho cả thiết bị di động (End-user) và giao diện quản trị (Admin).

## 📑 Mục lục
1. [GPS Tracking](#1-gps-tracking-theo-thời-gian-thực)
2. [Geofence Engine](#2-geofence--kích-hoạt-điểm-thuyết-minh)
3. [Narration Engine](#3-thuyết-minh-tự-động-narration-engine)
4. [POI Management](#4-quản-lý-dữ-liệu-poi)
5. [Map View](#5-map-view-giao-diện-bản-đồ)
6. [Web CMS](#6-hệ-thống-quản-trị-nội-dung-cms)
7. [Analytics](#7-phân-tích-dữ-liệu-analytics)
8. [QR Code Trigger](#8-qr-kích-hoạt-nội-dung)
9. [Core Workflow](#9-luồng-hoạt-động-cốt-lõi-workflow)

---

## 1. GPS Tracking theo thời gian thực
Phân hệ quản lý vị trí không gian của người dùng, đóng vai trò "mắt thần" cho toàn bộ hệ thống.
* **Cơ chế hoạt động:** Lấy vị trí người dùng liên tục ở cả trạng thái **Foreground** (ứng dụng đang mở) và **Background** (ứng dụng chạy ngầm).
* **Tối ưu hóa:** Cân bằng giữa độ chính xác của tọa độ GPS và mức độ tiêu thụ pin của thiết bị. (Sử dụng *Fused Location Provider Client* trên Android).

## 2. Geofence / Kích hoạt điểm thuyết minh
Phân hệ cốt lõi chịu trách nhiệm dựng "hàng rào ảo" và phát hiện các sự kiện xâm nhập vùng.
* **Thiết lập POI (Point of Interest):** Cấu hình các điểm tham quan với Tọa độ (Lat/Lng), Bán kính kích hoạt (Radius) và Mức độ ưu tiên (Priority).
* **Trigger Events:** Tự động kích hoạt khi người dùng đi vào vùng (Enter) hoặc đến rất gần điểm.
* **Cơ chế Anti-Spam:** Tích hợp thuật toán `Debounce` và thời gian `Cooldown` để tránh việc thiết bị phát lại liên tục khi người dùng đứng ở ranh giới vùng.

## 3. Thuyết minh tự động (Narration Engine)
Phân hệ xử lý âm thanh đầu ra, mang lại trải nghiệm "Hướng dẫn viên ảo".
* **Text-to-Speech (TTS):** Chuyển đổi văn bản thành giọng nói. Hoạt động linh hoạt, hỗ trợ đa ngôn ngữ, dung lượng siêu nhẹ (phù hợp chạy offline).
* **Pre-recorded Audio:** Hỗ trợ phát các file Audio có sẵn với giọng đọc tự nhiên, chuyên nghiệp (yêu cầu dung lượng lưu trữ lớn hơn).
* **Audio Queue Management:** Quản lý hàng chờ âm thanh; đảm bảo không phát trùng lặp và tự động tạm dừng (pause/stop) khi thiết bị có cuộc gọi hoặc thông báo hệ thống khác.

## 4. Quản lý dữ liệu POI
Cấu trúc dữ liệu tĩnh được đồng bộ từ Server xuống Local Database (SQLite). Mỗi POI bao gồm:
* Thông tin định danh và mô tả văn bản.
* Hình ảnh minh họa & Link bản đồ (MapLink).
* Dữ liệu đa ngôn ngữ (File Audio MP3 hoặc Kịch bản Script TTS).

## 5. Map View (Giao diện bản đồ)
Giao diện tương tác trực quan (Front-end) dành cho du khách.
* **Live Tracking:** Hiển thị vị trí thực tế của người dùng trên bản đồ (Blue dot).
* **POI Markers:** Hiển thị tất cả các điểm tham quan xung quanh.
* **Smart Highlight:** Tự động làm nổi bật điểm POI đang ở gần người dùng nhất.
* **Detail View:** Pop-up hiển thị chi tiết thông tin khi người dùng chạm vào một điểm.

## 6. Hệ thống quản trị nội dung (CMS)
Cổng thông tin (Web Admin) dành cho Ban quản lý dự án.
* **CRUD Operations:** Quản lý toàn diện dữ liệu POI, File Audio, và các bản dịch đa ngôn ngữ.
* **Tour Management:** Nhóm các POI riêng lẻ thành các tuyến Tour du lịch hoàn chỉnh.
* **System Monitor:** Xem lịch sử sử dụng và giám sát hoạt động hệ thống.

## 7. Phân tích dữ liệu (Analytics)
Phân hệ thu thập nhật ký (Log) để hỗ trợ ra quyết định kinh doanh.
* **Route Tracking:** Lưu vết tuyến đường di chuyển của du khách (dữ liệu được ẩn danh hoàn toàn).
* **Metrics:** Thống kê các điểm được nghe nhiều nhất và thời gian dừng chân trung bình tại 1 điểm.
* **Heatmap:** Xây dựng bản đồ nhiệt để xem khu vực nào đang thu hút đông khách nhất.

## 8. QR kích hoạt nội dung
Giải pháp dự phòng (Fallback) và mở rộng trải nghiệm không phụ thuộc vào GPS.
* **Vị trí triển khai:** Các trạm dừng xe buýt cố định (VD: phường Khánh Hội, Vĩnh Hội, Xóm Chiếu).
* **Cách thức:** Quét mã QR code bằng camera của App để lập tức tải và nghe nội dung thuyết minh.

## 9. Luồng hoạt động cốt lõi (Workflow)
Sự kết hợp của tất cả các module trên tạo thành một vòng lặp khép kín:
1. **Sync:** App tải danh sách POI (Tọa độ, bán kính, ưu tiên, nội dung).
2. **Track:** Background Service liên tục cập nhật vị trí người dùng.
3. **Detect:** `Geofence Engine` xác định POI gần nhất lọt vào bán kính $\rightarrow$ Bắn tín hiệu Trigger.
4. **Execute:** `Narration Engine` kiểm tra trạng thái Cooldown. Nếu hợp lệ $\rightarrow$ Phát TTS/Audio.
5. **Log:** Hệ thống lưu lại lịch sử đã phát để khóa Cooldown và đồng bộ Analytics lên server.