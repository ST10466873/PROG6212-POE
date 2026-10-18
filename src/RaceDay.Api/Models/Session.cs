namespace RaceDay.Api.Models;

public class Session
{
    public int SessionId { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public string SessionToken { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    public bool IsActive { get; set; } = true;
}
