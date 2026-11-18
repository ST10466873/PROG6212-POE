namespace RaceDay.Web.Services;

/// <summary>Authenticated user details cached in the MVC session after login.</summary>
public sealed record CurrentUser(int UserId, string Role, string Name, string Email);

/// <summary>
/// Session helper used by controllers, the action filter and the shared layout.
/// The API session token never leaves the server-side ASP.NET Core session.
/// </summary>
public static class SessionExtensions
{
    private const string TokenKey = "RaceDay.Token";
    private const string UserIdKey = "RaceDay.UserId";
    private const string RoleKey = "RaceDay.Role";
    private const string NameKey = "RaceDay.Name";
    private const string EmailKey = "RaceDay.Email";

    /// <summary>Stores the API token and identity after a successful login.</summary>
    public static void SignIn(this ISession session, string token, int userId, string role, string name, string email)
    {
        session.SetString(TokenKey, token);
        session.SetInt32(UserIdKey, userId);
        session.SetString(RoleKey, role);
        session.SetString(NameKey, name);
        session.SetString(EmailKey, email);
    }

    /// <summary>Clears all server-side authentication state.</summary>
    public static void SignOut(this ISession session) => session.Clear();

    /// <summary>Returns the cached user, or null for an anonymous visitor.</summary>
    public static CurrentUser? GetCurrentUser(this ISession session)
    {
        var id = session.GetInt32(UserIdKey);
        var role = session.GetString(RoleKey);
        if (id == null || string.IsNullOrWhiteSpace(role)) return null;
        return new CurrentUser(id.Value, role, session.GetString(NameKey) ?? "", session.GetString(EmailKey) ?? "");
    }

    /// <summary>Returns the opaque token that must be sent to the API.</summary>
    public static string? GetApiToken(this ISession session) => session.GetString(TokenKey);

    /// <summary>Role check used by navigation and the RequireSession filter.</summary>
    public static bool IsInRole(this ISession session, string role)
        => string.Equals(session.GetString(RoleKey), role, StringComparison.OrdinalIgnoreCase);

    /// <summary>Refreshes the name/email shown in the navbar after a profile update.</summary>
    public static void UpdateIdentity(this ISession session, string name, string email)
    {
        session.SetString(NameKey, name);
        session.SetString(EmailKey, email);
    }
}
