using System.ComponentModel.DataAnnotations;

namespace RaceDay.Api.DTOs;

// ============================================================
// Authentication DTOs — match POST /api/auth/register & /login
// ============================================================

/// <summary>
/// Register payload — creates a new Organiser or Participant account.
/// Role is validated to exactly "Organiser" or "Participant" in the controller.
/// </summary>
public record RegisterRequest(
    [Required, MaxLength(50)] string FirstName, // given name, required
    [Required, MaxLength(50)] string LastName,  // family name, required
    [Required, EmailAddress, MaxLength(100)] string Email, // unique login
    [Required, MinLength(6)] string Password, // plain text, hashed with BCrypt before storage
    [Required] string Role, // Organiser | Participant — maps to Roles.RoleName
    [Required] DateTime DateOfBirth, // past date, used for age-category checks
    string? PhoneNumber // optional contact
);

/// <summary>Login payload — email + password.</summary>
public record LoginRequest(
    [Required, EmailAddress] string Email, // case-insensitive lookup
    [Required] string Password // verified against BCrypt hash
);

/// <summary>Returned by login — includes opaque session token for X-Session-Token header.</summary>
public record AuthResponse(int UserId, string Email, string Role, string SessionToken, DateTime ExpiresAt);

// ============================================================
// User profile DTOs — GET/PUT /api/users/me
// ============================================================

/// <summary>Public profile view — includes role name via join.</summary>
public record UserProfileDto(int UserId, string FirstName, string LastName, string Email, string Role, string? PhoneNumber, DateTime DateOfBirth, bool IsActive, DateTime CreatedAt);

/// <summary>Update payload for own profile — email uniqueness checked in controller (409).</summary>
public record UpdateProfileRequest(string FirstName, string LastName, string Email, string? PhoneNumber, DateTime DateOfBirth);

// ============================================================
// Event DTOs — POST/PUT /api/events and GET list/detail
// ============================================================

/// <summary>Create/update payload — validated for distance &gt; 0 and EventType in Run/Walk/Cycle.</summary>
public record CreateEventRequest(
    [Required, MaxLength(100)] string Name, // maps to Events.EventName
    [Required, MaxLength(500)] string Description, // event card blurb
    [Required] DateTime Date, // race day
    [Required, MaxLength(100)] string Location, // venue/city
    [Required, Range(0.01, 1000)] decimal DistanceKm, // &gt; 0, e.g. 10.00
    [Required] string EventType // Run/Walk/Cycle — CK_Events_EventType
);
/// <summary>Event detail — includes optional Route and organiser display name.</summary>
public record EventDto(int EventId, int OrganiserId, string EventName, string Description, DateTime EventDate, string Location, decimal DistanceKm, string EventType, RouteDto? Route, string OrganiserName);
/// <summary>1:1 route info shown on GET /api/events/{id}.</summary>
public record RouteDto(string StartPoint, string EndPoint, int ElevationGainM, string? GpxTrackUrl);

// ============================================================
// Category DTOs — /api/events/{id}/categories and /api/categories/{id}
// ============================================================

/// <summary>Create/update category — MinAge/MaxAge nullable for distance categories (null = no bound).</summary>
public record CreateCategoryRequest([Required, MaxLength(50)] string CategoryName, int? MinAge, int? MaxAge);
/// <summary>Category view — mirrors EventCategories row.</summary>
public record CategoryDto(int CategoryId, int EventId, string CategoryName, int? MinAge, int? MaxAge);

// ============================================================
// Enrolment DTOs — entry into an event with chosen category
// ============================================================

/// <summary>Enrol payload — only categoryId required, event comes from route.</summary>
public record EnrolRequest(int CategoryId);
/// <summary>Enrolment view — includes participant and category names for organiser screens.</summary>
public record EnrolmentDto(int EnrolmentId, int EventId, string EventName, int UserId, string ParticipantName, int CategoryId, string CategoryName, DateTime EnrolmentDate, string Status);

// ============================================================
// Result DTOs — capture/correct/view race results
// ============================================================

/// <summary>Capture payload — FinishTime as HH:MM:SS.fff string, Position as 1-based rank.</summary>
public record CreateResultRequest([Required] string FinishTime, [Required, Range(1, int.MaxValue)] int Position);
/// <summary>Result view — ordered by FinishingPosition on GET /api/events/{id}/results.</summary>
public record ResultDto(int ResultId, int EnrolmentId, int EventId, string EventName, string ParticipantName, string FinishTime, int FinishingPosition, DateTime CapturedAt);
