using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;

namespace RaceDay.Api.Controllers;

[ApiController]
[Route("api/profile")]
[Produces("application/json")]
public class ProfileController(RaceDayContext db) : ControllerBase
{
    private int? UserId => HttpContext.Session.GetInt32("userId");
    private string? Role => HttpContext.Session.GetString("role");

    /// <summary>Returns the authenticated user's own profile according to their session role.</summary>
    /// <response code="200">Current user's profile details.</response>
    /// <response code="401">Caller is not authenticated.</response>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Get()
    {
        if (UserId is null || Role is null) return Unauthorized();

        if (Role == "Organiser")
        {
            var org = await db.Organisers.FindAsync(UserId);
            return org is null ? Unauthorized() : Ok(new { org.OrganiserId, org.Name, org.Email, role = Role });
        }

        var part = await db.Participants.FindAsync(UserId);
        return part is null ? Unauthorized() : Ok(new { part.ParticipantId, part.Name, part.Email, part.Age, part.Gender, role = Role });
    }

    /// <summary>Updates the authenticated user's own profile fields.</summary>
    /// <param name="request">Updated profile information.</param>
    /// <response code="204">Profile updated successfully.</response>
    /// <response code="400">Invalid profile update data.</response>
    /// <response code="401">Caller is not authenticated.</response>
    [HttpPut]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Update(ProfileUpdate request)
    {
        if (UserId is null || Role is null) return Unauthorized();
        if (string.IsNullOrWhiteSpace(request.Name)) return BadRequest(new { message = "Name cannot be empty." });

        if (Role == "Organiser")
        {
            var org = await db.Organisers.FindAsync(UserId);
            if (org is null) return Unauthorized();
            org.Name = request.Name.Trim();
        }
        else
        {
            var part = await db.Participants.FindAsync(UserId);
            if (part is null) return Unauthorized();
            part.Name = request.Name.Trim();
            if (request.Age is not null)
            {
                if (request.Age is < 1 or > 120) return BadRequest(new { message = "Age must be between 1 and 120." });
                part.Age = request.Age.Value;
            }
            if (!string.IsNullOrWhiteSpace(request.Gender))
                part.Gender = request.Gender.Trim();
        }

        await db.SaveChangesAsync();
        return NoContent();
    }
}

/// <summary>Profile update payload.</summary>
public record ProfileUpdate(string Name, int? Age, string? Gender);
