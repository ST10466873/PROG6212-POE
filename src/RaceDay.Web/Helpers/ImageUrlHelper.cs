namespace RaceDay.Web.Helpers;

/// <summary>
/// Converts an API-returned image path into a browser-reachable URL. Azure URLs are
/// absolute and used directly; local development files are proxied through the MVC
/// Media controller so the browser never needs to reach the API host directly.
/// </summary>
public static class ImageUrlHelper
{
    public const string Placeholder = "/img/event-placeholder.svg";

    public static string Resolve(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return Placeholder;
        if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
            url.StartsWith("/", StringComparison.Ordinal))
            return url;

        return "/Media/Get?path=" + Uri.EscapeDataString(url);
    }

    /// <summary>Ordinal suffix for positions: 1st, 2nd, 3rd, 4th, 11th, 47th…</summary>
    public static string Position(int position)
    {
        var remainder100 = position % 100;
        if (remainder100 is >= 11 and <= 13) return $"{position}th";
        return (position % 10) switch
        {
            1 => $"{position}st",
            2 => $"{position}nd",
            3 => $"{position}rd",
            _ => $"{position}th"
        };
    }

    /// <summary>Short, human-friendly date format used consistently on every page.</summary>
    public static string RaceDate(DateTime date) => date.ToString("ddd d MMM yyyy");
}
