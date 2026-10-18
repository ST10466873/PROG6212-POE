namespace RaceDay.Api.Models;

public class Route
{
    public int RouteId { get; set; }
    public int EventId { get; set; }
    public Event Event { get; set; } = null!;
    public string StartPoint { get; set; } = null!;
    public string EndPoint { get; set; } = null!;
    public int ElevationGainM { get; set; }
    public string? GpxTrackUrl { get; set; }
}
