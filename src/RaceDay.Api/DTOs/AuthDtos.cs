using System.ComponentModel.DataAnnotations;

namespace RaceDay.Api.DTOs;

// Register
public record RegisterRequest(
    [Required, MaxLength(50)] string FirstName,
    [Required, MaxLength(50)] string LastName,
    [Required, EmailAddress, MaxLength(100)] string Email,
    [Required, MinLength(6)] string Password,
    [Required] string Role, // Organiser | Participant
    [Required] DateTime DateOfBirth,
    string? PhoneNumber
);

public record LoginRequest(
    [Required, EmailAddress] string Email,
    [Required] string Password
);

public record AuthResponse(int UserId, string Email, string Role, string SessionToken, DateTime ExpiresAt);

// Users/me
public record UserProfileDto(int UserId, string FirstName, string LastName, string Email, string Role, string? PhoneNumber, DateTime DateOfBirth, bool IsActive, DateTime CreatedAt);
public record UpdateProfileRequest(string FirstName, string LastName, string Email, string? PhoneNumber, DateTime DateOfBirth);

// Events
public record CreateEventRequest(
    [Required, MaxLength(100)] string Name,
    [Required, MaxLength(500)] string Description,
    [Required] DateTime Date,
    [Required, MaxLength(100)] string Location,
    [Required, Range(0.01, 1000)] decimal DistanceKm,
    [Required] string EventType // Run/Walk/Cycle
);
public record EventDto(int EventId, int OrganiserId, string EventName, string Description, DateTime EventDate, string Location, decimal DistanceKm, string EventType, RouteDto? Route, string OrganiserName);
public record RouteDto(string StartPoint, string EndPoint, int ElevationGainM, string? GpxTrackUrl);

// Categories
public record CreateCategoryRequest([Required, MaxLength(50)] string CategoryName, int? MinAge, int? MaxAge);
public record CategoryDto(int CategoryId, int EventId, string CategoryName, int? MinAge, int? MaxAge);

// Enrolments
public record EnrolRequest(int CategoryId);
public record EnrolmentDto(int EnrolmentId, int EventId, string EventName, int UserId, string ParticipantName, int CategoryId, string CategoryName, DateTime EnrolmentDate, string Status);

// Results
public record CreateResultRequest([Required] string FinishTime, [Required, Range(1, int.MaxValue)] int Position);
public record ResultDto(int ResultId, int EnrolmentId, int EventId, string EventName, string ParticipantName, string FinishTime, int FinishingPosition, DateTime CapturedAt);
