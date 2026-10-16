using System.ComponentModel.DataAnnotations;

namespace RaceDay.Api.Models;

/// <summary>Request payload for organiser registration.</summary>
public record RegisterOrganiser(
    [Required, StringLength(100)] string Name,
    [Required, EmailAddress, StringLength(100)] string Email,
    [Required, MinLength(8)] string Password);

/// <summary>Request payload for participant registration.</summary>
public record RegisterParticipant(
    [Required, StringLength(100)] string Name,
    [Required, EmailAddress, StringLength(100)] string Email,
    [Required, MinLength(8)] string Password,
    [Range(1, 120)] int Age,
    [Required, StringLength(20)] string Gender);

/// <summary>Request payload for unified registration where role is selected.</summary>
public record RegisterRequest(
    [Required, RegularExpression("(?i)Organiser|Participant")] string Role,
    [Required, StringLength(100)] string Name,
    [Required, EmailAddress, StringLength(100)] string Email,
    [Required, MinLength(8)] string Password,
    [Range(1, 120)] int? Age,
    [StringLength(20)] string? Gender);

/// <summary>Request payload for authentication login.</summary>
public record LoginRequest(
    [Required, EmailAddress] string Email,
    [Required] string Password);

/// <summary>Request payload for creating or updating an athletic event.</summary>
public record EventRequest(
    [Required, StringLength(200)] string Name,
    DateTime EventDate,
    [Required, StringLength(200)] string Location,
    string? Description,
    [Range(0.1, 500)] decimal DistanceKm,
    [RegularExpression("(?i)Run|Walk|Cycle")] string EventType = "Run");

/// <summary>Request payload to enrol in an event category.</summary>
public record EnrolRequest(
    [Range(1, int.MaxValue)] int EventId,
    [Range(1, int.MaxValue)] int CategoryId);

/// <summary>Request payload to record participant finish time and position.</summary>
public record ResultRequest(
    TimeSpan FinishTime,
    [Range(1, int.MaxValue)] int FinishPosition);

/// <summary>Request payload to assign an existing category to an event.</summary>
public record CategoryAssignment(
    [Range(1, int.MaxValue)] int CategoryId);

/// <summary>Request payload to create a new category.</summary>
public record CategoryRequest(
    [Required, StringLength(100)] string Name,
    string? Description);

/// <summary>Request payload to update participant details.</summary>
public record ParticipantUpdateRequest(
    [StringLength(100)] string? Name,
    [Range(1, 120)] int? Age,
    [StringLength(20)] string? Gender);
