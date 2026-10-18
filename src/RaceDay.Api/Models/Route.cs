namespace RaceDay.Api.Models;

/// <summary>
/// Live route information for an event — the 1:1 side of Events:Routes.
/// Maps to dbo.Routes (UQ on EventId).
/// </summary>
public class Route
{
    /// <summary>Surrogate PK.</summary>
    public int RouteId { get; set; }

    /// <summary>FK to the single event this route belongs to (1:1).</summary>
    public int EventId { get; set; }

    /// <summary>Parent event navigation.</summary>
    public Event Event { get; set; } = null!;

    /// <summary>Start location (km 0) shown on race-day info.</summary>
    public string StartPoint { get; set; } = null!;

    /// <summary>Finish location shown on race-day info.</summary>
    public string EndPoint { get; set; } = null!;

    /// <summary>Total elevation gain in metres — 0 for flat routes.</summary>
    public int ElevationGainM { get; set; }

    /// <summary>Optional link to the GPX track file.</summary>
    public string? GpxTrackUrl { get; set; }
}
