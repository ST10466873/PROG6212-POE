using System.ComponentModel.DataAnnotations;

namespace RaceDay.Web.Models;

// ============================================================
// View models for the MVC layer. These mirror API JSON contracts only —
// no entity classes, DbContext or domain rules live in this project.
// ============================================================

/// <summary>Login form data posted to the MVC application.</summary>
public class LoginViewModel
{
    [Required, EmailAddress]
    public string Email { get; set; } = "";

    [Required, DataType(DataType.Password)]
    public string Password { get; set; } = "";

    public string? ReturnUrl { get; set; }
}

/// <summary>Registration form data. Role is restricted to the two supported roles.</summary>
public class RegisterViewModel
{
    [Required, MaxLength(50), Display(Name = "First name")]
    public string FirstName { get; set; } = "";

    [Required, MaxLength(50), Display(Name = "Last name")]
    public string LastName { get; set; } = "";

    [Required, EmailAddress]
    public string Email { get; set; } = "";

    [Required, MinLength(6), DataType(DataType.Password)]
    public string Password { get; set; } = "";

    [Required, DataType(DataType.Date), Display(Name = "Date of birth")]
    public DateTime DateOfBirth { get; set; } = new DateTime(1995, 1, 1);

    [Display(Name = "Phone number")]
    public string? PhoneNumber { get; set; }

    [Required]
    public string Role { get; set; } = "Participant";
}

/// <summary>Login response returned by the API (token is stored only in server-side session).</summary>
public sealed record AuthResponse(int UserId, string Email, string Role, string SessionToken, DateTime ExpiresAt);

/// <summary>Event card/detail projection returned by the API.</summary>
public sealed record EventVm(
    int EventId, int OrganiserId, string EventName, string Description, DateTime EventDate,
    string Location, decimal DistanceKm, string EventType, RouteVm? Route,
    string OrganiserName, string? BannerImageUrl = null);

/// <summary>Optional 1:1 route details for an event.</summary>
public sealed record RouteVm(string StartPoint, string EndPoint, int ElevationGainM, string? GpxTrackUrl);

/// <summary>Category projection used on event detail and Organiser category management.</summary>
public sealed record CategoryVm(int CategoryId, int EventId, string CategoryName, int? MinAge, int? MaxAge);

/// <summary>Enrolment projection used by both role interfaces.</summary>
public sealed record EnrolmentVm(
    int EnrolmentId, int EventId, string EventName, int UserId, string ParticipantName,
    int CategoryId, string CategoryName, DateTime EnrolmentDate, string Status);

/// <summary>Result projection for public tables and the Organiser results page.</summary>
public sealed record ResultVm(
    int ResultId, int EnrolmentId, int EventId, string EventName, string ParticipantName,
    string FinishTime, int FinishingPosition, DateTime CapturedAt);

/// <summary>Participant race-history row including the field size.</summary>
public sealed record MyResultVm(
    int ResultId, int EnrolmentId, int EventId, string EventName, DateTime EventDate,
    string CategoryName, string FinishTime, int FinishingPosition, int TotalFinishers, DateTime CapturedAt);

/// <summary>Profile projection returned by GET /api/users/me.</summary>
public sealed record ProfileVm(
    int UserId, string FirstName, string LastName, string Email, string Role,
    string? PhoneNumber, DateTime DateOfBirth, bool IsActive, DateTime CreatedAt,
    string? ProfilePictureUrl = null);

/// <summary>Organiser dashboard projection returned by the aggregate API endpoint.</summary>
public sealed record DashboardVm(
    int TotalEvents, int TotalEnrolments, int UpcomingEvents, int ResultsCaptured,
    List<EventSummaryVm> Events);

/// <summary>One dashboard row: event with enrolment/result counts.</summary>
public sealed record EventSummaryVm(
    int EventId, string EventName, DateTime EventDate, string Location, string EventType,
    string? BannerImageUrl, int EnrolmentCount, int ResultCount, bool IsUpcoming);

/// <summary>Event create/edit form with the six required fields.</summary>
public class EventFormViewModel
{
    public int EventId { get; set; }

    [Required, MaxLength(100)]
    public string Name { get; set; } = "";

    [Required, MaxLength(500)]
    public string Description { get; set; } = "";

    [Required, DataType(DataType.Date), Display(Name = "Event date")]
    public DateTime Date { get; set; } = DateTime.Today.AddDays(14);

    [Required, MaxLength(100)]
    public string Location { get; set; } = "";

    [Required, Range(0.01, 1000), Display(Name = "Distance (km)")]
    public decimal DistanceKm { get; set; } = 10;

    [Required, Display(Name = "Event type")]
    public string EventType { get; set; } = "Run";

    [Display(Name = "Banner image")]
    public IFormFile? Banner { get; set; }

    public string? BannerImageUrl { get; set; }
}

/// <summary>Category create/edit form.</summary>
public class CategoryFormViewModel
{
    public int CategoryId { get; set; }
    public int EventId { get; set; }

    [Required, MaxLength(50), Display(Name = "Category name")]
    public string CategoryName { get; set; } = "";

    [Display(Name = "Minimum age")]
    public int? MinAge { get; set; }

    [Display(Name = "Maximum age")]
    public int? MaxAge { get; set; }
}

/// <summary>Result capture/correction form. Finish time uses HH:MM:SS.fff.</summary>
public class ResultFormViewModel
{
    public int EnrolmentId { get; set; }
    public int EventId { get; set; }
    public string ParticipantName { get; set; } = "";
    public string CategoryName { get; set; } = "";
    public int? ExistingResultId { get; set; }

    [Required, Display(Name = "Finish time (HH:MM:SS.fff)")]
    public string FinishTime { get; set; } = "00:00:00.000";

    [Required, Range(1, int.MaxValue), Display(Name = "Finishing position")]
    public int Position { get; set; } = 1;
}

/// <summary>Profile edit form.</summary>
public class ProfileFormViewModel
{
    [Required, MaxLength(50), Display(Name = "First name")]
    public string FirstName { get; set; } = "";

    [Required, MaxLength(50), Display(Name = "Last name")]
    public string LastName { get; set; } = "";

    [Required, EmailAddress]
    public string Email { get; set; } = "";

    [Display(Name = "Phone number")]
    public string? PhoneNumber { get; set; }

    [Required, DataType(DataType.Date), Display(Name = "Date of birth")]
    public DateTime DateOfBirth { get; set; }

    [Display(Name = "Profile picture")]
    public IFormFile? Picture { get; set; }

    public string? ProfilePictureUrl { get; set; }
}
