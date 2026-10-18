namespace RaceDay.Api.Models;

public class Role
{
    public int RoleId { get; set; }
    public string RoleName { get; set; } = null!; // Organiser or Participant
    public ICollection<User> Users { get; set; } = new List<User>();
}
