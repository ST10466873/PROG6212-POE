namespace RaceDay.Api.Models;

/// <summary>
/// Race event created and owned by an Organiser.
/// Maps to dbo.Events — unique on (EventName, EventDate, Location).
/// </summary>
public class Event
{
    /// <summary>Surrogate PK for /api/events/{id}.</summary>
    public int EventId { get; set; }

    /// <summary>FK to the owning organiser — drives authorisation (only owner may edit/delete).</summary>
    public int OrganiserId { get; set; }

    /// <summary>Navigation to the organiser user.</summary>
    public User Organiser { get; set; } = null!;

    /// <summary>Display name shown on event cards.</summary>
    public string EventName { get; set; } = null!;

    /// <summary>Rich description shown to participants browsing events.</summary>
    public string Description { get; set; } = null!;

    /// <summary>Race day date — drives upcoming-events filtering.</summary>
    public DateTime EventDate { get; set; }

    /// <summary>Venue/city in South Africa.</summary>
    public string Location { get; set; } = null!;

    /// <summary>Distance in kilometres — must be &gt; 0 (CK_Events_DistanceKm).</summary>
    public decimal DistanceKm { get; set; }

    /// <summary>Type discriminator: Run | Walk | Cycle (CK_Events_EventType).</summary>
    public string EventType { get; set; } = null!; // Run | Walk | Cycle

    /// <summary>1:1 route information for race-day preparation.</summary>
    public Route? Route { get; set; }

    /// <summary>Age/distance categories defined for this event.</summary>
    public ICollection<EventCategory> Categories { get; set; } = new List<EventCategory>();

    /// <summary>All participant enrolments for this event.</summary>
    public ICollection<Enrolment> Enrolments { get; set; } = new List<Enrolment>();

    /// <summary>Results captured after the event (ordered by FinishingPosition).</summary>
    public ICollection<Result> Results { get; set; } = new List<Result>();
}
