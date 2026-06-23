namespace TodoBackend.Models;

public class CalendarEvent
{
    public string Id { get; set; } = string.Empty;
    public required string FamilyId { get; set; } = string.Empty;
    public string? MemberId { get; set; }
    public List<string>? MemberIds { get; set; }
    public required string Title { get; set; } = string.Empty;
    public DateTime StartAt { get; set; }
    public DateTime EndAt { get; set; }
    public bool? IsAllDay { get; set; }
    public string Notes { get; set; } = string.Empty;
    public string Tone { get; set; } = "teal";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
