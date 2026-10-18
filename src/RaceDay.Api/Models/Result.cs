namespace RaceDay.Api.Models;

public class Result
{
    public int ResultId { get; set; }
    public int EnrolmentId { get; set; }
    public Enrolment Enrolment { get; set; } = null!;
    public int EventId { get; set; }
    public Event Event { get; set; } = null!;
    public TimeSpan FinishTime { get; set; }
    public int FinishingPosition { get; set; }
    public DateTime CapturedAt { get; set; } = DateTime.UtcNow;
}
