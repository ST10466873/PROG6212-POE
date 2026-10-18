namespace RaceDay.Api.Models;

public class Event
{
    public int EventId { get; set; }
    public int OrganiserId { get; set; }
    public User Organiser { get; set; } = null!;
    public string EventName { get; set; } = null!;
    public string Description { get; set; } = null!;
    public DateTime EventDate { get; set; }
    public string Location { get; set; } = null!;
    public decimal DistanceKm { get; set; }
    public string EventType { get; set; } = null!; // Run | Walk | Cycle

    public Route? Route { get; set; }
    public ICollection<EventCategory> Categories { get; set; } = new List<EventCategory>();
    public ICollection<Enrolment> Enrolments { get; set; } = new List<Enrolment>();
    public ICollection<Result> Results { get; set; } = new List<Result>();
}
