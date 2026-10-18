namespace RaceDay.Api.Models;

/// <summary>
/// Chip time and finishing position captured by the organiser after race day.
/// Maps to dbo.Results — unique per EnrolmentId (1:1) and per (EventId, FinishingPosition).
/// </summary>
public class Result
{
    /// <summary>Surrogate PK.</summary>
    public int ResultId { get; set; }

    /// <summary>FK to the single enrolment this result belongs to (1:1).</summary>
    public int EnrolmentId { get; set; }

    /// <summary>Navigation to the enrolment (includes participant).</summary>
    public Enrolment Enrolment { get; set; } = null!;

    /// <summary>Denormalised EventId — needed to enforce unique position per event in SQL.</summary>
    public int EventId { get; set; }

    /// <summary>Parent event navigation.</summary>
    public Event Event { get; set; } = null!;

    /// <summary>Electronic chip time with millisecond precision.</summary>
    public TimeSpan FinishTime { get; set; }

    /// <summary>Finishing position — 1 is winner, unique within the event.</summary>
    public int FinishingPosition { get; set; }

    /// <summary>UTC timestamp when the organiser captured this result.</summary>
    public DateTime CapturedAt { get; set; } = DateTime.UtcNow;
}
