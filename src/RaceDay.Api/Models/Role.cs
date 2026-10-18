namespace RaceDay.Api.Models;

/// <summary>
/// Lookup for the two distinct system roles (Organiser, Participant).
/// Maps to dbo.Roles — CHECK constraint enforces exactly these two values.
/// </summary>
public class Role
{
    /// <summary>Surrogate PK — referenced by Users.RoleId.</summary>
    public int RoleId { get; set; }

    /// <summary>Role name — either 'Organiser' or 'Participant'.</summary>
    public string RoleName { get; set; } = null!; // Organiser or Participant

    /// <summary>All users assigned to this role.</summary>
    public ICollection<User> Users { get; set; } = new List<User>();
}
