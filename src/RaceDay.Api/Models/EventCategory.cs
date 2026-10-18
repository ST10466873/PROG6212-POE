namespace RaceDay.Api.Models;

public class EventCategory
{
    public int CategoryId { get; set; }
    public int EventId { get; set; }
    public Event Event { get; set; } = null!;
    public string CategoryName { get; set; } = null!;
    public int? MinAge { get; set; }
    public int? MaxAge { get; set; }

    public ICollection<Enrolment> Enrolments { get; set; } = new List<Enrolment>();
}
