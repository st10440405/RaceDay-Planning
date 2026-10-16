using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;

namespace RaceDay.Api.Controllers;

[ApiController]
[Route("api/profile")]
public class ProfileController(RaceDayContext db) : ControllerBase
{
    /// <summary>Returns the authenticated user's own profile according to their session role.</summary>
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var id = HttpContext.Session.GetInt32("userId"); var role = HttpContext.Session.GetString("role"); if (id is null || role is null) return Unauthorized();
        if (role == "Organiser") { var x = await db.Organisers.FindAsync(id); return x is null ? Unauthorized() : Ok(new { x.OrganiserId, x.Name, x.Email, role }); }
        var p = await db.Participants.FindAsync(id); return p is null ? Unauthorized() : Ok(new { p.ParticipantId, p.Name, p.Email, p.Age, p.Gender, role });
    }

    /// <summary>Updates the authenticated user's own profile fields.</summary>
    [HttpPut]
    public async Task<IActionResult> Update(ProfileUpdate request)
    {
        var id = HttpContext.Session.GetInt32("userId"); var role = HttpContext.Session.GetString("role"); if (id is null || role is null) return Unauthorized();
        if (role == "Organiser") { var x = await db.Organisers.FindAsync(id); if (x is null) return Unauthorized(); x.Name = request.Name.Trim(); }
        else { var p = await db.Participants.FindAsync(id); if (p is null) return Unauthorized(); p.Name = request.Name.Trim(); if (request.Age is not null) p.Age = request.Age.Value; if (!string.IsNullOrWhiteSpace(request.Gender)) p.Gender = request.Gender.Trim(); }
        await db.SaveChangesAsync(); return NoContent();
    }
}

public record ProfileUpdate(string Name, int? Age, string? Gender);
