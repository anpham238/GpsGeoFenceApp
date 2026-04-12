using Microsoft.AspNetCore.Mvc;
using QRCoder;
using System.Drawing;

namespace MapApi.Controllers;

[ApiController]
[Route("api/v1/qr")]
public class QrController : ControllerBase
{
    [HttpGet("generate/{poiId}")]
    public IActionResult GenerateQrCode(int poiId)
    {
        // 1. Tạo chuỗi dữ liệu chuẩn cho App
        string qrData = $"smarttourism://poi/{poiId}";

        // 2. Dùng thư viện QRCoder để vẽ mã QR
        using var qrGenerator = new QRCodeGenerator();
        using var qrCodeData = qrGenerator.CreateQrCode(qrData, QRCodeGenerator.ECCLevel.Q);
        using var qrCode = new PngByteQRCode(qrCodeData);
        byte[] qrCodeImage = qrCode.GetGraphic(20); // 20 là kích thước pixel

        // 4. Trả về file ảnh trực tiếp lên Web
        return File(qrCodeImage, "image/png");
    }
}