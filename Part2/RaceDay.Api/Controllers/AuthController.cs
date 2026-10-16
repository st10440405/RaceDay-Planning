using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Models;

namespace RaceDay.Api.Controllers;

[ApiController]
[Route("api")]
public class AuthController(RaceDayContext db) : ControllerBase
{
    private readonly PasswordHasher<string> _hasher = new();

    /// <summary>Creates an organiser account using a unique email and a hashed password.</summary>
    [HttpPost("organisers/register")]
    public async Task<IActionResult> RegisterOrganiser(RegisterOrganiser request)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        if (await db.Organisers.AnyAsync(x => x.Email == email) || await db.Participants.AnyAsync(x => x.Email == email)) return Conflict(new { message = "Email is already registered." });
        var user = new Organiser { Name = request.Name.Trim(), Email = email };
        user.PasswordHash = _hasher.HashPassword(user.Email, request.Password);
        db.Organisers.Add(user); await db.SaveChangesAsync();
        return Created($"/api/organisers/{user.OrganiserId}", new { user.OrganiserId, user.Name, user.Email, role = "Organiser" });
    }

    /// <summary>Creates a participant account including age and gender profile details.</summary>
    [HttpPost("participants/register")]
    public async Task<IActionResult> RegisterParticipant(RegisterParticipant request)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        if (await db.Organisers.AnyAsync(x => x.Email == email) || await db.Participants.AnyAsync(x => x.Email == email)) return Conflict(new { message = "Email is already registered." });
        var user = new Participant { Name = request.Name.Trim(), Email = email, Age = request.Age, Gender = request.Gender.Trim() };
        user.PasswordHash = _hasher.HashPassword(user.Email, request.Password);
        db.Participants.Add(user); await db.SaveChangesAsync();
        return Created($"/api/participants/{user.ParticipantId}", new { user.ParticipantId, user.Name, user.Email, user.Age, user.Gender, role = "Participant" });
    }

    /// <summary>Verifies organiser credentials and starts a role-specific server session.</summary>
    [HttpPost("organisers/login")]
    public async Task<IActionResult> OrganiserLogin(LoginRequest request)
    {
        var user = await db.Organisers.SingleOrDefaultAsync(x => x.Email == request.Email.Trim().ToLowerInvariant());
        if (user is null || _hasher.VerifyHashedPassword(user.Email, user.PasswordHash, request.Password) == PasswordVerificationResult.Failed) return Unauthorized(new { message = "Invalid email or password." });
        SetSession("Organiser", user.OrganiserId, user.Name, user.Email);
        return Ok(new { user.OrganiserId, user.Name, user.Email, role = "Organiser" });
    }

    /// <summary>Verifies participant credentials and starts a role-specific server session.</summary>
    [HttpPost("participants/login")]
    public async Task<IActionResult> ParticipantLogin(LoginRequest request)
    {
        var user = await db.Participants.SingleOrDefaultAsync(x => x.Email == request.Email.Trim().ToLowerInvariant());
        if (user is null || _hasher.VerifyHashedPassword(user.Email, user.PasswordHash, request.Password) == PasswordVerificationResult.Failed) return Unauthorized(new { message = "Invalid email or password." });
        SetSession("Participant", user.ParticipantId, user.Name, user.Email);
        return Ok(new { user.ParticipantId, user.Name, user.Email, role = "Participant" });
    }

    /// <summary>Clears the caller's server-side session.</summary>
    [HttpPost("logout")]
    public IActionResult Logout() { HttpContext.Session.Clear(); return NoContent(); }

    private void SetSession(string role, int id, string name, string email)
    {
        HttpContext.Session.Clear();
        HttpContext.Session.SetString("role", role); HttpContext.Session.SetInt32("userId", id);
        HttpContext.Session.SetString("name", name); HttpContext.Session.SetString("email", email);
    }
}
