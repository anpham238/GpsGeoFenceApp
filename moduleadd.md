# 🖼️ 11. Đặc Tả Module: Quản Lý Thư Viện Ảnh POI (Multi-Image Gallery)

![Status](https://img.shields.io/badge/Status-Proposed-orange)
![Platform](https://img.shields.io/badge/Platform-Web_CMS_%26_MAUI-512BD4)
![UX](https://img.shields.io/badge/UX-Bottom_Sheet_Carousel-brightgreen)

> **Mục tiêu:** Nâng cấp trải nghiệm thị giác cho du khách bằng cách cho phép một Điểm tham quan (POI) sở hữu nhiều hình ảnh thay vì chỉ một ảnh duy nhất. 
> - **Web Admin:** Có thể upload hàng loạt ảnh cho 1 POI.
> - **Mobile App:** Khi mở Bottom Sheet thông tin POI, người dùng có thể lướt ngang (Swipe Left/Right) để xem toàn bộ các ảnh này một cách mượt mà.

---

## 🗄️ 1. Cập nhật Cơ sở dữ liệu (Database Schema Update)

**Vấn đề hiện tại:** Trong file `GpsApp_Redesigned.sql`, bảng `PoiMedia` đang lưu trữ ảnh dưới dạng một cột duy nhất `[Image] [nvarchar](1000) NULL`. Thiết kế này chỉ cho phép lưu 1 ảnh.

**Giải pháp:** Xóa cột `Image` khỏi bảng `PoiMedia` (hoặc giữ lại làm ảnh đại diện - Cover), và tạo thêm một bảng mới `PoiImages` (quan hệ 1-Nhiều) để lưu danh sách ảnh.

```sql
-- Tạo bảng lưu trữ danh sách ảnh cho POI
CREATE TABLE [dbo].[PoiImages](
    [Id] [bigint] IDENTITY(1,1) NOT NULL,
    [IdPoi] [int] NOT NULL,
    [ImageUrl] [nvarchar](1000) NOT NULL,
    [SortOrder] [int] NOT NULL DEFAULT(0), -- Dùng để sắp xếp ảnh nào hiện trước/sau
    [CreatedAt] [datetime2](3) NOT NULL DEFAULT(SYSUTCDATETIME()),
    CONSTRAINT [PK_PoiImages] PRIMARY KEY CLUSTERED ([Id] ASC)
);

-- Tạo khóa ngoại liên kết với bảng Pois
ALTER TABLE [dbo].[PoiImages]  WITH CHECK ADD  CONSTRAINT [FK_PoiImages_Pois] FOREIGN KEY([IdPoi])
REFERENCES [dbo].[Pois] ([Id])
ON DELETE CASCADE;

ALTER TABLE [dbo].[PoiImages] CHECK CONSTRAINT [FK_PoiImages_Pois];
```

> **Trạng thái SQL:** Bảng `PoiImages` **đã có** trong `GpsApp_Redesigned.sql`. Bảng `PoiMedia` vẫn giữ cột `[Image]` làm ảnh cover đại diện.

---

## ⚙️ 2. Backend API (MapApi - C#)

### 2.1 Model EF Core

Tạo file `MapApi/Models/PoiImage.cs`:

```csharp
namespace MapApi.Models;

public sealed class PoiImage
{
    public long Id { get; set; }
    public int IdPoi { get; set; }
    public string ImageUrl { get; set; } = "";
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; }

    public Poi? Poi { get; set; }
}
```

### 2.2 Đăng ký DbContext (`AppDb.cs`)

Thêm vào `AppDb`:

```csharp
public DbSet<PoiImage> PoiImages => Set<PoiImage>();
```

Thêm cấu hình trong `OnModelCreating`:

```csharp
modelBuilder.Entity<PoiImage>(e =>
{
    e.ToTable("PoiImages");
    e.HasKey(x => x.Id);
    e.Property(x => x.SortOrder).HasDefaultValue(0);
    e.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
    e.HasOne(x => x.Poi)
     .WithMany()
     .HasForeignKey(x => x.IdPoi)
     .OnDelete(DeleteBehavior.Cascade);
});
```

### 2.3 EF Migration

```bash
dotnet ef migrations add AddPoiImages --project MapApi
dotnet ef database update --project MapApi
```

### 2.4 Controller Endpoints

Thêm vào `PoiMediaController.cs` (hoặc tạo `PoiImagesController.cs` riêng):

```csharp
// GET api/v1/pois/{id}/images — lấy danh sách ảnh
[HttpGet("images")]
public async Task<IActionResult> GetImages(int id, CancellationToken ct)
{
    var images = await _db.PoiImages
        .Where(x => x.IdPoi == id)
        .OrderBy(x => x.SortOrder).ThenBy(x => x.CreatedAt)
        .Select(x => new { x.Id, x.ImageUrl, x.SortOrder })
        .ToListAsync(ct);
    return Ok(images);
}

// POST api/v1/pois/{id}/images — upload ảnh mới (multipart/form-data)
[HttpPost("images")]
public async Task<IActionResult> AddImage(int id, IFormFile file, CancellationToken ct)
{
    if (file is null || file.Length == 0) return BadRequest("No file");
    var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
    if (ext is not ".jpg" and not ".jpeg" and not ".png") return BadRequest("Only .jpg/.png");
    if (file.Length > 10 * 1024 * 1024) return BadRequest("File too large (max 10 MB)");

    var poi = await _db.Pois.FindAsync(new object[] { id }, ct);
    if (poi is null) return NotFound();

    var dir = Path.Combine(_env.WebRootPath, "images");
    Directory.CreateDirectory(dir);
    var safeName = $"{id}_{Guid.NewGuid():N}{ext}";
    await using var fs = System.IO.File.Create(Path.Combine(dir, safeName));
    await file.CopyToAsync(fs, ct);

    var nextOrder = await _db.PoiImages.Where(x => x.IdPoi == id).MaxAsync(x => (int?)x.SortOrder, ct) ?? -1;
    var entity = new PoiImage { IdPoi = id, ImageUrl = $"/images/{safeName}", SortOrder = nextOrder + 1 };
    _db.PoiImages.Add(entity);
    await _db.SaveChangesAsync(ct);
    return Ok(new { entity.Id, entity.ImageUrl, entity.SortOrder });
}

// DELETE api/v1/pois/{id}/images/{imageId} — xóa 1 ảnh
[HttpDelete("images/{imageId:long}")]
public async Task<IActionResult> DeleteImage(int id, long imageId, CancellationToken ct)
{
    var img = await _db.PoiImages.FirstOrDefaultAsync(x => x.Id == imageId && x.IdPoi == id, ct);
    if (img is null) return NotFound();
    _db.PoiImages.Remove(img);
    await _db.SaveChangesAsync(ct);
    return NoContent();
}

// PUT api/v1/pois/{id}/images/reorder — cập nhật thứ tự ảnh
public sealed record ReorderRequest(List<long> OrderedIds);

[HttpPut("images/reorder")]
public async Task<IActionResult> Reorder(int id, [FromBody] ReorderRequest req, CancellationToken ct)
{
    var images = await _db.PoiImages.Where(x => x.IdPoi == id).ToListAsync(ct);
    for (var i = 0; i < req.OrderedIds.Count; i++)
    {
        var img = images.FirstOrDefault(x => x.Id == req.OrderedIds[i]);
        if (img is not null) img.SortOrder = i;
    }
    await _db.SaveChangesAsync(ct);
    return NoContent();
}
```

---

## 📦 3. MAUI App — Model & API Client

### 3.1 Cập nhật `PoiDto.cs`

```csharp
// Thêm vào class PoiDto (Application/Models/PoiDto.cs)
public List<string> ImageUrls { get; set; } = new();
```

> `ImageUrl` (cũ, từ `PoiMedia.Image`) vẫn giữ làm ảnh cover fallback. `ImageUrls` là danh sách từ `PoiImages`.

### 3.2 Cập nhật `PoiApiClient.cs`

Khi fetch POI detail, gọi thêm endpoint `/images` và gán vào `ImageUrls`:

```csharp
// Trong PoiApiClient — sau khi lấy được PoiDto
var images = await _http.GetFromJsonAsync<List<PoiImageDto>>($"api/v1/pois/{poi.Id}/images", ct);
poi.ImageUrls = images?.Select(x => x.ImageUrl).ToList() ?? new();
```

Thêm DTO nhỏ:

```csharp
public sealed class PoiImageDto
{
    public long Id { get; set; }
    public string ImageUrl { get; set; } = "";
    public int SortOrder { get; set; }
}
```

---

## 📱 4. MAUI UI — Carousel trong Bottom Sheet

### 4.1 Logic hiển thị

- Nếu `poi.ImageUrls.Count > 0` → dùng `ImageUrls` làm nguồn carousel.
- Nếu `ImageUrls` rỗng nhưng `ImageUrl != null` → hiện 1 ảnh cover (tương thích ngược).
- Nếu cả hai đều null → hiện placeholder.

### 4.2 XAML — CarouselView trong BottomSheet POI

Thay thế `<Image>` đơn lẻ hiện tại bằng:

```xml
<!-- Trong MapPage.xaml — phần BottomSheet hiển thị ảnh POI -->
<Grid>
    <!-- Carousel ảnh (hiện khi có nhiều ảnh) -->
    <CarouselView x:Name="PoiImageCarousel"
                  ItemsSource="{Binding SelectedPoi.ImageUrls}"
                  IsVisible="{Binding SelectedPoi.ImageUrls.Count, Converter={StaticResource CountToBoolConverter}}"
                  HeightRequest="220"
                  Loop="False">
        <CarouselView.ItemTemplate>
            <DataTemplate x:DataType="x:String">
                <Image Source="{Binding .}"
                       Aspect="AspectFill"
                       HeightRequest="220" />
            </DataTemplate>
        </CarouselView.ItemTemplate>
    </CarouselView>

    <!-- Chỉ báo vị trí (dots) -->
    <IndicatorView x:Name="PoiImageIndicator"
                   CarouselView.ItemsSourceBy="{x:Reference PoiImageCarousel}"
                   HorizontalOptions="Center"
                   VerticalOptions="End"
                   Margin="0,0,0,6"
                   IndicatorColor="#80FFFFFF"
                   SelectedIndicatorColor="White" />

    <!-- Fallback: 1 ảnh cover (tương thích ngược) -->
    <Image Source="{Binding SelectedPoi.ImageUrl}"
           Aspect="AspectFill"
           HeightRequest="220"
           IsVisible="{Binding SelectedPoi.ImageUrls.Count, Converter={StaticResource ZeroCountToBoolConverter}}" />
</Grid>
```

### 4.3 Converters cần thêm

```csharp
// CountToBoolConverter: trả True nếu Count > 0
// ZeroCountToBoolConverter: trả True nếu Count == 0
```

---

## ✅ 5. Checklist Triển Khai

- [ ] **DB**: Xác nhận bảng `PoiImages` đã tạo trên SQL Server (đã có trong `GpsApp_Redesigned.sql`)
- [ ] **Backend**: Tạo `PoiImage.cs` model
- [ ] **Backend**: Đăng ký `DbSet<PoiImage>` vào `AppDb`
- [ ] **Backend**: Chạy EF Migration `AddPoiImages`
- [ ] **Backend**: Thêm 4 endpoints (GET/POST/DELETE/PUT reorder) vào `PoiMediaController`
- [ ] **MAUI**: Thêm `ImageUrls` vào `PoiDto`
- [ ] **MAUI**: Thêm `PoiImageDto` + cập nhật `PoiApiClient` fetch images
- [ ] **MAUI**: Thay `<Image>` đơn bằng `<CarouselView>` + `<IndicatorView>` trong `MapPage.xaml`
- [ ] **MAUI**: Thêm `CountToBoolConverter` và `ZeroCountToBoolConverter`
- [ ] **Test**: Upload nhiều ảnh qua Swagger, kiểm tra carousel lướt mượt trên thiết bị Android