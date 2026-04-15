using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace MapApi.Controllers
{
    [Route("api/v1/[controller]")]
    [ApiController]
    public class TicketsController : ControllerBase
    {
        private readonly string _connStr;

        public TicketsController(IConfiguration config)
        {
            _connStr = config.GetConnectionString("DefaultConnection") ?? "";
        }

        // Dành cho Web Admin: Tạo mã vé QR
        [HttpPost("generate")]
        public async Task<IActionResult> GenerateTicket([FromBody] CreateTicketReq req)
        {
            var ticketCode = "TICKET-" + Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper();
            
            using var conn = new SqlConnection(_connStr);
            await conn.OpenAsync();
            var cmd = new SqlCommand(@"
                INSERT INTO PoiTickets (TicketCode, IdPoi, LanguageTag, MaxUses, CurrentUses) 
                VALUES (@code, @poiId, @lang, @max, 0)", conn);
            
            cmd.Parameters.AddWithValue("@code", ticketCode);
            cmd.Parameters.AddWithValue("@poiId", req.PoiId);
            cmd.Parameters.AddWithValue("@lang", req.Language);
            cmd.Parameters.AddWithValue("@max", req.MaxUses > 0 ? req.MaxUses : 5);

            await cmd.ExecuteNonQueryAsync();

            return Ok(new { TicketCode = ticketCode });
        }

        // Dành cho App Mobile: Kiểm tra và tăng số lần quét
        [HttpPost("{ticketCode}/scan")]
        public async Task<IActionResult> ScanTicket(string ticketCode)
        {
            using var conn = new SqlConnection(_connStr);
            await conn.OpenAsync();

            // Lấy thông tin vé
            var cmd = new SqlCommand("SELECT IdPoi, LanguageTag, MaxUses, CurrentUses FROM PoiTickets WHERE TicketCode = @code", conn);
            cmd.Parameters.AddWithValue("@code", ticketCode);
            
            using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync()) 
                return BadRequest("Vé không tồn tại!");

            int poiId = reader.GetInt32(0);
            string lang = reader.GetString(1);
            int maxUses = reader.GetInt32(2);
            int currentUses = reader.GetInt32(3);
            reader.Close();

            // Kiểm tra số lần quét
            if (currentUses >= maxUses)
                return BadRequest("Vé này đã vượt quá số lần sử dụng!");

            // Tăng số lần quét lên 1
            var updateCmd = new SqlCommand("UPDATE PoiTickets SET CurrentUses = CurrentUses + 1 WHERE TicketCode = @code", conn);
            updateCmd.Parameters.AddWithValue("@code", ticketCode);
            await updateCmd.ExecuteNonQueryAsync();

            return Ok(new { PoiId = poiId, Language = lang, Remaining = maxUses - currentUses - 1 });
        }
    }
    public class CreateTicketReq
    {
        public int PoiId { get; set; }
        public string Language { get; set; } = "vi-VN";
        public int MaxUses { get; set; } = 5;
    }
}