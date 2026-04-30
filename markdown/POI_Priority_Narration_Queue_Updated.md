# Module Ưu Tiên POI & Hàng Đợi Thuyết Minh (Bản Cập Nhật)

> Tài liệu thiết kế **module bổ sung** cho hệ thống **GpsGeoFenceApp** nhằm xử lý tình huống **người dùng đứng/chạm giữa hai (hoặc nhiều) POI gần nhau** (vòng tròn đỏ chồng lấn), hệ thống sẽ **xếp hàng và phát thuyết minh theo độ ưu tiên**.
>
> **Bản cập nhật này bổ sung:**
> - Gợi ý mở rộng cho module hiện tại
> - Quy tắc xử lý vùng giao nhau (mid-zone)
> - State machine cho narration
> - Pseudo-code C# cho `Priority Resolver` và `Queue Manager`

---

## 1. Mục tiêu
- Tránh phát **đồng thời** nhiều thuyết minh khi POI gần nhau.
- Đảm bảo **POI quan trọng hơn** được phát trước.
- Có **hàng đợi (queue)** rõ ràng để phát lần lượt các POI còn lại.
- Hoạt động tốt cho cả **chạm thủ công** (tap) và **đứng trong vùng geofence**.
- Hỗ trợ mở rộng về sau cho **tour mode**, **analytics** và **AI priority**.

---

## 2. Phạm vi áp dụng
- Áp dụng cho **Pois** trong cơ sở dữ liệu `GpsApi`.
- Tích hợp với các module hiện có:
  - Geofence detection
  - NarrationManager / Audio playback
  - PoiLanguage / PoiMedia
  - HistoryPoi / Analytics_Visit
  - TourPois / Tours

---

## 3. Bổ sung dữ liệu (Database Design)

### 3.1 Thêm cột ưu tiên cho POI
**Bảng:** `Pois`

```sql
ALTER TABLE dbo.Pois
ADD PriorityLevel INT NOT NULL DEFAULT (0);
```

**Ý nghĩa PriorityLevel:**
- `0` : ưu tiên thấp (POI phụ)
- `1` : ưu tiên trung bình
- `2` : ưu tiên cao
- `3+`: POI đặc biệt (bắt buộc phát trước)

> Quy ước: **số càng lớn → ưu tiên càng cao**.

---

### 3.2 (Tuỳ chọn) Thêm loại ưu tiên
Nếu muốn linh hoạt hơn:

```sql
ALTER TABLE dbo.Pois
ADD PriorityType VARCHAR(20) NOT NULL DEFAULT ('NORMAL');
```

Ví dụ:
- `NORMAL`
- `IMPORTANT`
- `EMERGENCY`
- `MANUAL_TAP`

---

### 3.3 (Khuyến nghị) Bảng quy tắc ưu tiên linh hoạt
Nếu muốn quản lý ưu tiên động không cần sửa code:

```sql
CREATE TABLE dbo.PoiPriorityRules(
    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
    PoiId INT NOT NULL,
    ContextType VARCHAR(30) NOT NULL,
    PriorityBoost INT NOT NULL DEFAULT (0),
    IsInterruptAllowed BIT NOT NULL DEFAULT (0),
    CreatedAt DATETIME2(3) NOT NULL DEFAULT (SYSUTCDATETIME())
);
```

**Ví dụ `ContextType`:**
- `GEOFENCE`
- `TAP`
- `TOUR_MODE`
- `MID_ZONE`

---

## 4. Điều kiện kích hoạt module

Module được kích hoạt khi **một trong hai điều kiện** xảy ra:

### 4.1 Người dùng **đứng trong vùng giao nhau** của ≥ 2 POI
- Geofence phát hiện **nhiều POI cùng active** trong cùng thời điểm.

### 4.2 Người dùng **chạm (tap)** vào khu vực giữa các POI
- Tap nằm trong bán kính của ≥ 2 vòng tròn POI.

---

## 5. Bổ sung gợi ý xử lý xung đột POI

## 5.1 Mid-Zone Detection (phát hiện vùng giữa 2 POI)
Ngoài việc kiểm tra người dùng có nằm trong nhiều bán kính POI hay không, nên thêm quy tắc **mid-zone**:

### Mid-zone được xác định khi:
- Người dùng nằm trong **vùng giao nhau** của 2 hoặc nhiều geofence, hoặc
- Khoảng cách đến POI A và POI B chênh lệch nhỏ hơn một ngưỡng xác định.

**Công thức gợi ý:**
```text
|distance(A) - distance(B)| <= MidZoneThresholdMeters
```

**Ví dụ:**
- `MidZoneThresholdMeters = 5m`

---

## 5.2 Tap Boost (ưu tiên khi người dùng chạm)
Nếu người dùng **tap** vào POI hoặc tap gần POI:
- POI đó được cộng thêm điểm ưu tiên tạm thời
- Hoặc gán `PriorityType = MANUAL_TAP`

**Lưu ý:**
- Tap boost chỉ nên áp dụng cho **lần phát hiện hiện tại**
- Không nên ghi thẳng vào DB nếu chỉ là ưu tiên runtime

---

## 5.3 Priority Score tổng hợp
Thay vì chỉ so sánh `PriorityLevel`, nên tính **điểm ưu tiên cuối cùng**:

```text
FinalPriorityScore =
  (PriorityLevel * 10)
+ TapBoost
+ DistanceScore
- CooldownPenalty
+ TourBoost
```

### Gợi ý thành phần:
- `PriorityLevel`: từ DB
- `TapBoost`: +5 nếu người dùng tap trực tiếp
- `DistanceScore`: POI gần hơn được cộng điểm hơn
- `CooldownPenalty`: POI vừa phát xong bị trừ điểm
- `TourBoost`: nếu đang trong tour mode và POI đúng thứ tự

---

## 5.4 Queue Expire / Timeout
Một POI trong queue không nên tồn tại mãi mãi.

### Gợi ý:
- Mỗi item có `ExpiresAt`
- Nếu người dùng rời xa POI trước khi đến lượt phát:
  - remove khỏi queue

Điều này giúp tránh phát audio cho POI mà người dùng đã đi khỏi.

---

## 5.5 Non-Interrupt Mode
Không phải lúc nào POI mới ưu tiên cao hơn cũng nên cắt ngang audio đang phát.

### Gợi ý cấu hình:
- `AllowInterrupt = false` (mặc định)
- Chỉ interrupt khi:
  - `PriorityLevel >= 3`
  - hoặc `PriorityType = EMERGENCY`

---

## 6. Luồng xử lý nghiệp vụ (Business Flow)

### 6.1 Tổng quan luồng
```text
[Detect POIs gần nhau]
        ↓
[Tạo danh sách POI ứng viên]
        ↓
[Tính FinalPriorityScore]
        ↓
[Sắp xếp theo score DESC]
        ↓
[Đưa vào Narration Queue]
        ↓
[Phát POI ưu tiên cao nhất]
        ↓
[Kết thúc → phát POI tiếp theo]
```

---

### 6.2 Luồng chi tiết

#### Bước 1: Phát hiện POI chồng lấn
- `GeofenceService` hoặc `MapTapHandler` trả về danh sách POI ứng viên:

```text
[PoiA, PoiB, PoiC]
```

#### Bước 2: Xếp hạng ưu tiên
- Tính `FinalPriorityScore` cho từng POI
- Sắp xếp theo:
  1. `FinalPriorityScore` (DESC)
  2. Khoảng cách tới người dùng (ASC)
  3. Thời gian chưa được phát gần nhất (ASC)

#### Bước 3: Tạo hàng đợi
- Push vào `NarrationQueue`:

```text
Queue = [PoiB (Score=37), PoiA (Score=28), PoiC (Score=12)]
```

#### Bước 4: Phát thuyết minh
- Chỉ phát **1 POI tại 1 thời điểm**.
- Khi audio kết thúc → dequeue → phát POI tiếp theo.

#### Bước 5: Làm sạch queue
- Nếu POI quá hạn (`ExpiresAt`) hoặc user rời vùng:
  - remove khỏi queue

---

## 7. Thiết kế State Machine cho Narration

## 7.1 Các trạng thái đề xuất
- `Idle` – không phát gì
- `Queued` – đang chờ trong queue
- `Playing` – đang phát audio
- `Paused`
- `Interrupted`
- `Completed`
- `Skipped`

---

## 7.2 Ý nghĩa
- `Idle`: hệ thống chưa phát gì
- `Queued`: POI đã được đưa vào queue nhưng chưa tới lượt
- `Playing`: đang phát thuyết minh
- `Interrupted`: bị cắt ngang bởi POI ưu tiên cao hơn (nếu cho phép)
- `Skipped`: bị loại vì user rời vùng hoặc timeout
- `Completed`: phát xong bình thường

---

## 8. Thiết kế Narration Queue

### 8.1 Cấu trúc item queue
```csharp
public class NarrationQueueItem
{
    public int PoiId { get; set; }
    public string PoiName { get; set; } = string.Empty;
    public int PriorityLevel { get; set; }
    public string PriorityType { get; set; } = "NORMAL";
    public double DistanceMeters { get; set; }
    public double FinalPriorityScore { get; set; }
    public DateTime TriggeredAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public bool AllowInterrupt { get; set; }
    public bool IsTapBoosted { get; set; }
}
```

---

### 8.2 Quy tắc queue
- Không cho **trùng POI** trong queue.
- Nếu POI mới có `FinalPriorityScore` cao hơn POI đang phát:
  - nếu `AllowInterrupt = true` → có thể interrupt
  - nếu `AllowInterrupt = false` → chờ POI hiện tại phát xong
- Queue phải hỗ trợ:
  - enqueue
  - dequeue
  - remove expired
  - reprioritize

---

## 9. Quy tắc phát thuyết minh

### 9.1 Ưu tiên khi chạm tay (Tap)
- POI được tap trực tiếp sẽ được:
  - tăng điểm ưu tiên tạm thời
  - hoặc tăng `PriorityLevel` runtime
  - đẩy lên đầu queue nếu điểm cuối cao nhất

### 9.2 Ưu tiên khi đứng trong vùng
- POI có `FinalPriorityScore` cao hơn phát trước.
- Nếu bằng nhau:
  - POI gần hơn phát trước.

### 9.3 Cooldown
- Dùng `CooldownSeconds` trong bảng `Pois`:
  - POI vừa phát xong sẽ **không được đưa lại queue** trong thời gian cooldown.

### 9.4 Tour Mode
- Nếu app đang ở `Tour Mode`, có thể cộng thêm điểm cho POI có `TourPois.SortOrder` gần với bước hiện tại của tour.

---

## 10. Ghi nhận lịch sử & analytics

Khi POI được phát:
- Ghi vào:
  - `HistoryPoi`
  - `Analytics_Visit` (Action = `NarrationPlayed`)
  - `Analytics_ListenDuration`

Khi POI bị xếp hàng:
- (Tuỳ chọn) log `QueuedButNotPlayed`

Khi có xung đột:
- (Khuyến nghị) log thêm:
  - `NarrationConflictDetected`
  - `NarrationQueued`
  - `NarrationInterrupted`
  - `NarrationSkipped`

---

## 11. Hành vi UI/UX đề xuất

### 11.1 Trên bản đồ
- Hiển thị POI đang phát bằng:
  - vòng tròn đỏ đậm hơn
  - icon loa / sóng âm.

### 11.2 Khi có nhiều POI
- Hiển thị text nhỏ:
  > “Đang phát POI A · Tiếp theo: POI B”

### 11.3 Queue Preview
- Hiển thị ngắn danh sách queue:
  - `1. POI A (đang phát)`
  - `2. POI B (sắp phát)`
  - `3. POI C (đang chờ)`

### 11.4 Khi tap giữa 2 POI
- Hiển thị popup chọn (tuỳ chọn):
  - Phát ngay POI ưu tiên
  - Hoặc cho người dùng chọn thủ công.

---

## 12. API & Service cần bổ sung

### 12.1 Service
- `PoiPriorityResolver`
- `NarrationQueueManager`
- `PoiConflictDetector`
- `NarrationStateTracker`

### 12.2 API (nếu có backend hỗ trợ)
- `GET /api/pois/nearby?lat=&lng=`
- `POST /api/narration/queue`
- `POST /api/narration/conflict-log`

---

## 13. Pseudo-code C# cho Priority Resolver

```csharp
public class PoiCandidate
{
    public int PoiId { get; set; }
    public string PoiName { get; set; } = string.Empty;
    public int PriorityLevel { get; set; }
    public string PriorityType { get; set; } = "NORMAL";
    public double DistanceMeters { get; set; }
    public int CooldownSeconds { get; set; }
    public DateTime? LastPlayedAt { get; set; }
    public bool IsTapped { get; set; }
    public bool AllowInterrupt { get; set; }
    public int? TourSortOrder { get; set; }
}

public class PriorityResolverOptions
{
    public double MidZoneThresholdMeters { get; set; } = 5;
    public double TapBoost { get; set; } = 5;
    public double DistanceNearBonus { get; set; } = 3;
    public double CooldownPenalty { get; set; } = 10;
    public double TourBoost { get; set; } = 2;
    public bool EnableTourBoost { get; set; } = true;
}

public class PoiPriorityResolver
{
    private readonly PriorityResolverOptions _options;

    public PoiPriorityResolver(PriorityResolverOptions options)
    {
        _options = options;
    }

    public List<NarrationQueueItem> Resolve(
        IEnumerable<PoiCandidate> candidates,
        DateTime nowUtc,
        int? currentTourStep = null)
    {
        var result = new List<NarrationQueueItem>();

        foreach (var c in candidates)
        {
            double score = c.PriorityLevel * 10;

            // Tap boost
            if (c.IsTapped)
                score += _options.TapBoost;

            // Distance bonus (càng gần càng được cộng)
            if (c.DistanceMeters <= 10)
                score += _options.DistanceNearBonus;
            else if (c.DistanceMeters <= 30)
                score += 1;

            // Cooldown penalty
            if (c.LastPlayedAt.HasValue)
            {
                var secondsSinceLastPlay = (nowUtc - c.LastPlayedAt.Value).TotalSeconds;
                if (secondsSinceLastPlay < c.CooldownSeconds)
                {
                    score -= _options.CooldownPenalty;
                }
            }

            // Tour boost
            if (_options.EnableTourBoost && currentTourStep.HasValue && c.TourSortOrder.HasValue)
            {
                var delta = Math.Abs(c.TourSortOrder.Value - currentTourStep.Value);
                if (delta == 0) score += _options.TourBoost;
                else if (delta == 1) score += 1;
            }

            result.Add(new NarrationQueueItem
            {
                PoiId = c.PoiId,
                PoiName = c.PoiName,
                PriorityLevel = c.PriorityLevel,
                PriorityType = c.PriorityType,
                DistanceMeters = c.DistanceMeters,
                FinalPriorityScore = score,
                TriggeredAt = nowUtc,
                ExpiresAt = nowUtc.AddSeconds(30),
                AllowInterrupt = c.AllowInterrupt,
                IsTapBoosted = c.IsTapped
            });
        }

        return result
            .OrderByDescending(x => x.FinalPriorityScore)
            .ThenBy(x => x.DistanceMeters)
            .ThenBy(x => x.TriggeredAt)
            .ToList();
    }
}
```

---

## 14. Pseudo-code C# cho Queue Manager

```csharp
public enum NarrationState
{
    Idle,
    Queued,
    Playing,
    Paused,
    Interrupted,
    Completed,
    Skipped
}

public class NarrationQueueManager
{
    private readonly List<NarrationQueueItem> _queue = new();
    private NarrationQueueItem? _currentPlaying;
    public NarrationState CurrentState { get; private set; } = NarrationState.Idle;

    public IReadOnlyList<NarrationQueueItem> Queue => _queue.AsReadOnly();
    public NarrationQueueItem? CurrentPlaying => _currentPlaying;

    public void EnqueueRange(IEnumerable<NarrationQueueItem> items)
    {
        foreach (var item in items)
        {
            if (_currentPlaying?.PoiId == item.PoiId)
                continue;

            if (_queue.Any(q => q.PoiId == item.PoiId))
                continue;

            _queue.Add(item);
        }

        ReorderQueue();

        if (_currentPlaying == null && _queue.Count > 0)
        {
            PlayNext();
        }
        else
        {
            CurrentState = _queue.Count > 0 ? NarrationState.Queued : NarrationState.Idle;
        }
    }

    public void ReorderQueue()
    {
        _queue.Sort((a, b) =>
        {
            var scoreCompare = b.FinalPriorityScore.CompareTo(a.FinalPriorityScore);
            if (scoreCompare != 0) return scoreCompare;

            var distanceCompare = a.DistanceMeters.CompareTo(b.DistanceMeters);
            if (distanceCompare != 0) return distanceCompare;

            return a.TriggeredAt.CompareTo(b.TriggeredAt);
        });
    }

    public void RemoveExpired(DateTime nowUtc)
    {
        _queue.RemoveAll(q => q.ExpiresAt.HasValue && q.ExpiresAt.Value < nowUtc);

        if (_queue.Count == 0 && _currentPlaying == null)
            CurrentState = NarrationState.Idle;
    }

    public bool TryInterrupt(NarrationQueueItem incoming)
    {
        if (_currentPlaying == null)
            return false;

        if (!incoming.AllowInterrupt)
            return false;

        if (incoming.FinalPriorityScore <= _currentPlaying.FinalPriorityScore)
            return false;

        // Đưa item đang phát trở lại queue nếu muốn resume sau
        _queue.Add(_currentPlaying);
        _currentPlaying = incoming;
        _queue.RemoveAll(q => q.PoiId == incoming.PoiId);
        CurrentState = NarrationState.Interrupted;

        // Sau đó hệ thống audio manager sẽ play incoming item
        return true;
    }

    public NarrationQueueItem? PlayNext()
    {
        RemoveExpired(DateTime.UtcNow);

        if (_queue.Count == 0)
        {
            _currentPlaying = null;
            CurrentState = NarrationState.Idle;
            return null;
        }

        _currentPlaying = _queue[0];
        _queue.RemoveAt(0);
        CurrentState = NarrationState.Playing;
        return _currentPlaying;
    }

    public NarrationQueueItem? CompleteCurrentAndPlayNext()
    {
        if (_currentPlaying != null)
        {
            CurrentState = NarrationState.Completed;
        }

        _currentPlaying = null;
        return PlayNext();
    }

    public void SkipCurrent()
    {
        if (_currentPlaying != null)
        {
            CurrentState = NarrationState.Skipped;
            _currentPlaying = null;
        }

        PlayNext();
    }

    public void Clear()
    {
        _queue.Clear();
        _currentPlaying = null;
        CurrentState = NarrationState.Idle;
    }
}
```

---

## 15. Khả năng mở rộng
- Hỗ trợ **tour mode**: ưu tiên theo `TourPois.SortOrder`.
- Hỗ trợ **AI priority**: ưu tiên theo hành vi người dùng.
- Hỗ trợ **silent POI**: chỉ hiển thị, không phát audio.
- Hỗ trợ **resume interrupted POI** nếu audio bị ngắt giữa chừng.

---

## 16. Kết luận
Module **Ưu Tiên POI & Hàng Đợi Thuyết Minh** giúp:
- Trải nghiệm người dùng mượt hơn khi POI gần nhau.
- Tránh xung đột audio.
- Tăng khả năng mở rộng cho tour, analytics và UX nâng cao.
- Tạo nền tảng rõ ràng để triển khai bằng code trong `GpsGeoFenceApp`.

Bản cập nhật này đã bổ sung thêm:
- Mid-zone rule
- Priority score tổng hợp
- Queue expire / timeout
- Narration state machine
- Pseudo-code C# cho `Priority Resolver` và `Queue Manager`
