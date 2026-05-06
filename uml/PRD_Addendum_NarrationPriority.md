# PRD BỔ SUNG — Ưu Tiên Thuyết Minh & Hàng Đợi Đa Người Dùng
**Sản phẩm:** GpsGeoFenceApp  
**Phiên bản bổ sung:** 1.1 (Addendum to PRD v1.0)  
**Ngày:** 2026-05-07  
**Thuộc Module:** Module 3 — Narration Engine (Mở rộng)

---

## 1. Bối Cảnh & Lý Do Bổ Sung

PRD v1.0 (Module 3) mô tả Narration Engine theo luồng tuyến tính: nhận yêu cầu → hàng đợi → phát audio. Tuy nhiên, trong thực tế triển khai tại các khu di tích / bảo tàng xuất hiện hai tình huống PRD v1.0 chưa đặc tả:

| # | Tình huống | Hệ quả nếu không xử lý |
|---|-----------|------------------------|
| 1 | Du khách đứng ở **vùng giao thoa** của 2 POI (bán kính chồng lên nhau) | Hệ thống phát 2 thuyết minh cùng lúc → loạn âm thanh |
| 2 | **Nhiều du khách** đồng thời bước vào cùng 1 POI | Mỗi thiết bị kích hoạt đồng thời → CPU spike, tranh chấp TTS |

Addendum này đặc tả **hai tính năng mới** xử lý hai tình huống trên, kèm thiết kế UML và thay đổi data model.

---

## 2. Tính Năng Mới

### 2.1 TN-01 — Ưu Tiên Thuyết Minh Khi Đứng Giữa 2 POI (Winner-Only)

#### 2.1.1 Mô Tả

Khi du khách đứng trong vùng chồng lấn của nhiều POI, hệ thống **chỉ phát thuyết minh cho POI có ưu tiên cao nhất**. Các POI thua bị **loại bỏ hoàn toàn** — không xếp hàng chờ, không phát sau.

#### 2.1.2 Thuật Toán Tính Điểm

```
Score(POI) = PriorityLevel × 10
           + TapBoost         (+ 5 nếu người dùng chủ động nhấn vào)
           + DistanceBonus    (+ 3 nếu khoảng cách < 50% bán kính)
           - CooldownPenalty  (- 10 nếu POI vừa được phát trong cooldown)
           + TourBoost        (+ 2 nếu POI thuộc tour đang chạy)
```

POI có Score cao nhất **thắng** và được phát thuyết minh. Các POI còn lại bị bỏ qua.

#### 2.1.3 Quy Tắc Xung Đột — Winner-Only

| Trường hợp | Hành vi |
|-----------|---------|
| POI mới có `PriorityLevel` thấp hơn POI đang queue | **Loại bỏ POI mới** ngay lập tức |
| POI mới có `PriorityLevel` cao hơn POI đang queue | **Xóa POI cũ**, thêm POI mới vào queue |
| Bằng nhau | Giữ nguyên thứ tự — không thêm loser |

#### 2.1.4 Thay Đổi Data Model

Bảng `Pois` bổ sung trường:

| Trường | Kiểu | Mặc định | Mô tả |
|--------|------|----------|-------|
| `AudioSourceMode` | `VARCHAR(20)` | `AUDIO_FIRST` | Nguồn audio: `AUDIO_FIRST` / `TTS_ONLY` / `AUDIO_ONLY` |

#### 2.1.5 Luồng Hoạt Động

```
Geofence Enter/Near
     │
     ▼
GeofenceEventGate ──── [Cooldown? Debounce?] ──→ Bỏ qua
     │ Hợp lệ
     ▼
NarrationManager.HandleAsync(poi)
     │
     ├─ Queue có POI khác PriorityLevel cao hơn? ──YES──→ Loại bỏ (loser)
     │
     └─ Thắng → Xóa losers khỏi queue → Thêm winner → Phát
```

---

### 2.2 TN-02 — Hàng Đợi Đa Người Dùng Cùng 1 POI (Multi-User Queue)

#### 2.2.1 Mô Tả

Khi **n thiết bị** của n du khách đồng thời bước vào cùng 1 POI, hệ thống **cấp phát slot** cho từng thiết bị theo thứ tự FIFO, có trải đều thời gian bắt đầu để tránh spike tài nguyên.

#### 2.2.2 Tham Số Kỹ Thuật

| Tham số | Giá trị | Mô tả |
|---------|---------|-------|
| `MaxConcurrentPerPoi` | 10 | Tối đa 10 thiết bị phát đồng thời cho 1 POI |
| `SpreadDelayMs` | 80 ms | Trải đều: thiết bị thứ k trễ k×80ms |
| `DeduplicationWindow` | 2 giây | Cùng thiết bị + POI trong 2s → bỏ event trùng |
| `QueueCapacity` | 300 | Tối đa 300 item trong hàng chờ toàn hệ thống |

#### 2.2.3 Vòng Đời Một Item Trong Hàng Đợi

```
        HandleAsync(announcement)
               │
     ┌─────────▼──────────┐
     │ Winner-Only check  │──LOSER──→ [DISCARDED]
     └─────────┬──────────┘
               │ WINNER
           [QUEUED]
               │
        NarrationManager.WorkerLoop()
               │
     ┌─────────▼─────────┐
     │ IsDuplicate(2s)?  │──YES──→ [SKIPPED]
     └─────────┬─────────┘
               │ NO
     ┌─────────▼─────────┐
     │ Acquire Semaphore │  ← PoiNarrationRateLimiter
     │ (MaxConcurrent=10)│
     └─────────┬─────────┘
               │
           [PLAYING]
               │
        ┌──────┴──────┐
        │             │
    Audio OK       TTS fallback
        │             │
        └──────┬───────┘
               │
          [COMPLETED]
               │
         Release Slot
```

#### 2.2.4 Ưu Tiên Phát Trong Queue

| Thứ tự | EventType | Lý do |
|--------|-----------|-------|
| 1 | `Tap` | Du khách chủ động bấm → ý định rõ ràng nhất |
| 2 | `Enter` | Vào vùng POI → có mặt thực tế |
| 3 | `Near` | Gần POI → thấp nhất |

---

## 3. Luồng Tích Hợp Hai Tính Năng

```
GPS Location Update
      │
      ▼
AndroidGeofenceService → detect Enter/Near events
      │
      ▼
GeofenceEventGate (debounce 250ms, cooldown/poi)
      │ Events hợp lệ
      ▼
PoiNarrationHandler.PlayAsync(poi)
      │
      ▼ [TN-01 — Winner-Only]
NarrationManager.HandleAsync(announcement)
   → So sánh PriorityLevel với queue hiện tại
   → Loser bị loại, Winner xóa losers và vào queue
      │
      ▼ [TN-02 — Multi-User]
NarrationManager.Worker
   → Dedup 2s, sort Tap>Enter>Near
   → PoiNarrationRateLimiter: Semaphore(max=10), spread k×80ms
      │
      ▼
IAudioPlayer / TTS → Phát cho từng thiết bị
      │
      ▼
AnalyticsClient.LogListenDuration()
```

---

## 4. Quy Tắc Nghiệp Vụ Bổ Sung

| # | Quy tắc | Chi tiết |
|---|---------|---------|
| BR-01 | Cooldown per POI per Device | Mỗi cặp (POI, Device) có cooldown riêng. Không ảnh hưởng thiết bị khác. |
| BR-02 | TapBoost override | Khi người dùng chủ động nhấn vào POI trên bản đồ, score tăng +5. Đảm bảo ý định rõ ràng luôn được ưu tiên. |
| BR-03 | Winner-Only | Khi nhiều POI chồng lấn, **chỉ 1 POI phát**. Loser bị loại hoàn toàn — không xếp hàng, không phát sau. |
| BR-04 | Spread Delay | k×80ms spread tránh toàn bộ n thiết bị gọi TTS/Audio cùng lúc gây CPU spike. |
| BR-05 | AudioSourceMode per POI | Admin có thể cấu hình từng POI: ưu tiên file audio (`AUDIO_FIRST`), chỉ TTS (`TTS_ONLY`), hoặc chỉ audio file (`AUDIO_ONLY`). |

---

## 5. Sơ Đồ UML

### 5.1 Sequence — Ưu Tiên Khi Đứng Giữa 2 POI (xem file đính kèm: `seq-prd-priority.puml`)

> Mô tả luồng: Du khách đứng giữa POI-A (level=2) và POI-B (level=1) → NarrationManager giữ POI-A, loại bỏ POI-B → chỉ nghe POI-A.

### 5.2 Sequence — Hàng Đợi Đa Người Dùng (xem file đính kèm: `seq-prd-multiuser-queue.puml`)

> Mô tả luồng: 3 thiết bị cùng vào 1 POI → Semaphore cấp slot → spread delay → từng thiết bị phát theo thứ tự.

---

## 6. Tiêu Chí Hoàn Thành (Acceptance Criteria)

| ID | Tiêu chí | Kiểm tra |
|----|---------|---------|
| AC-01 | Khi đứng giữa 2 POI, chỉ phát **1 thuyết minh** (POI ưu tiên cao nhất) | Đứng tại tọa độ giao thoa, nghe đúng 1 audio |
| AC-02 | POI có `PriorityLevel` cao hơn luôn được chọn khi cùng điều kiện | Unit test NarrationManager winner-only |
| AC-03 | Tap vào POI trên bản đồ → phát ngay, dù đang ở POI khác | Tap override |
| AC-04 | n thiết bị vào 1 POI → lần lượt nghe, không bị bỏ sót | Stress test với n=5 thiết bị |
| AC-05 | Cùng thiết bị trigger 2 lần trong 2s → chỉ phát 1 lần | Dedup test |
| AC-06 | POI thua trong vùng chồng lấn **không bao giờ phát** sau POI thắng | Overlap zone test |

---

## 7. Phụ Lục — Thay Đổi So Với PRD v1.0

| Mục | PRD v1.0 | PRD v1.1 (Addendum) |
|-----|----------|---------------------|
| Narration Engine | Queue FIFO đơn giản | Winner-Only + Multi-User Semaphore Queue |
| Data Model Pois | Không có audio mode | `+AudioSourceMode` |
| Quy tắc nghiệp vụ | 1 queue toàn cục | Winner-only per-conflict, semaphore per-POI |
| UML | Activity + Sequence tổng quát | Sequence chi tiết 2 luồng mới (đính kèm) |
