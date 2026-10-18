namespace RaceDay.Api.Models;

/// <summary>
/// Category defined per event (e.g. Under 20, Senior, 10km Open).
/// Maps to dbo.EventCategories — unique per (EventId, CategoryName).
/// </summary>
public class EventCategory
{
    /// <summary>Surrogate PK for /api/categories/{id}.</summary>
    public int CategoryId { get; set; }

    /// <summary>FK to the owning event.</summary>
    public int EventId { get; set; }

    /// <summary>Parent event navigation.</summary>
    public Event Event { get; set; } = null!;

    /// <summary>Display name — must be unique inside one event.</summary>
    public string CategoryName { get; set; } = null!;

    /// <summary>Inclusive lower age bound; null for distance-based categories.</summary>
    public int? MinAge { get; set; }

    /// <summary>Inclusive upper age bound; null means no upper limit.</summary>
    public int? MaxAge { get; set; }

    /// <summary>Enrolments that chose this category.</summary>
    public ICollection<Enrolment> Enrolments { get; set; } = new List<Enrolment>();
}
