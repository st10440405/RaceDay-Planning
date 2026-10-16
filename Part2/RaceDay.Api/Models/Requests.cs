using System.ComponentModel.DataAnnotations;

namespace RaceDay.Api.Models;

public record RegisterOrganiser([Required, StringLength(100)] string Name, [Required, EmailAddress, StringLength(100)] string Email, [Required, MinLength(8)] string Password);
public record RegisterParticipant([Required, StringLength(100)] string Name, [Required, EmailAddress, StringLength(100)] string Email, [Required, MinLength(8)] string Password, [Range(1, 120)] int Age, [Required, StringLength(20)] string Gender);
public record LoginRequest([Required, EmailAddress] string Email, [Required] string Password);
public record EventRequest([Required, StringLength(200)] string Name, DateTime EventDate, [Required, StringLength(200)] string Location, string Description, [Range(0.1, 500)] decimal DistanceKm, [RegularExpression("(?i)Run|Walk|Cycle")] string EventType);
public record EnrolRequest(int EventId, int CategoryId);
public record ResultRequest(TimeSpan FinishTime, [Range(1, int.MaxValue)] int FinishPosition);
public record CategoryAssignment([Range(1, int.MaxValue)] int CategoryId);
public record CategoryRequest([Required, StringLength(100)] string Name, string Description);
