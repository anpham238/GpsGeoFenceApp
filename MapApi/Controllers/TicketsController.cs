using MapApi.Data;
using MapApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MapApi.Controllers
{
    [Route("api/v1/[controller]")]
    [ApiController]
    public class TicketsController(AppDb db) : ControllerBase
    {
        [HttpPost("generate")]
        public async Task<IActionResult> GenerateTicket([FromBody] CreateTicketReq req)
        {
            var ticketCode = "QR-" + Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper();
            var ticket = new PoiTicket
            {
                TicketCode = ticketCode,
                IdPoi = req.PoiId,
                LanguageTag = req.Language ?? "vi-VN",
                MaxUses = req.MaxUses > 0 ? req.MaxUses : 5,
                CurrentUses = 0,
                CreatedAt = DateTime.UtcNow
            };
            db.PoiTickets.Add(ticket);
            await db.SaveChangesAsync();
            return Ok(new { TicketCode = ticketCode });
        }
        [HttpPost("scan/{ticketCode}")]
        public async Task<IActionResult> ScanTicket(string ticketCode)
        {
            var ticket = await db.PoiTickets.FirstOrDefaultAsync(t => t.TicketCode == ticketCode);
            if (ticket == null) return NotFound(new { message = "Mã QR không tồn tại!" });
            if (ticket.CurrentUses >= ticket.MaxUses) return StatusCode(403, new { message = "Vé đã hết hạn!" });
            ticket.CurrentUses += 1;
            await db.SaveChangesAsync();
            return Ok(new { PoiId = ticket.IdPoi, Language = ticket.LanguageTag, Remaining = ticket.MaxUses - ticket.CurrentUses });
        }
    }
    public class CreateTicketReq
    {
        public int PoiId { get; set; }
        public string? Language { get; set; }
        public int MaxUses { get; set; }
    }
}