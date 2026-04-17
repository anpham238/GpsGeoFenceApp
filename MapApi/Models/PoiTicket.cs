using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MapApi.Models;

[Table("PoiTickets")]
public class PoiTicket
{
    [Key]
    [Column(TypeName = "varchar(50)")]
    public string TicketCode { get; set; } = null!;
    public int IdPoi { get; set; }
    [Column(TypeName = "nvarchar(10)")]
    public string LanguageTag { get; set; } = "vi-VN";
    public int MaxUses { get; set; } = 5;
    public int CurrentUses { get; set; } = 0;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}