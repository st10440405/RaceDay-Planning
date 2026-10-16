using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Models;

namespace RaceDay.Api.Controllers;

[ApiController]
[Route("api")]
[Produces("application/json")]
public class AuthController(RaceDayContext db) : ControllerBase
{
    private readonly PasswordHasher<string> _hasher = new();

    /// <summary>Creates an organiser account using a unique email and a hashed password.</summary>
    /// <param name="request">Organiser registration payload including name, email, and password (min 8 characters).</param>
    /// <response code="201">Organiser account created successfully.</response>
    /// <response code="400">Invalid registration data or password requirements not met.</response>
    /// <response code="409">Email address is already in use by another account.</response>
    [HttpPost("organisers/register")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RegisterOrganiser(RegisterOrganiser request)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        if (await db.Organisers.AnyAsync(x => x.Email == email) || await db.Participants.AnyAsync(x => x.Email == email))
            return Conflict(new { message = "Email is already registered." });

        var user = new Organiser { Name = request.Name.Trim(), Email = email };
        user.PasswordHash = _hasher.HashPassword(user.Email, request.Password);
        db.Organisers.Add(user);
        await db.SaveChangesAsync();

        return Created($"/api/organisers/{user.OrganiserId}", new { user.OrganiserId, user.Name, user.Email, role = "Organiser" });
    }

    /// <summary>Creates a participant account including age and gender profile details.</summary>
    /// <param name="request">Participant registration payload including name, email, password, age, and gender.</param>
    /// <response code="201">Participant account created successfully.</response>
    /// <response code="400">Invalid registration data or validation requirements not met.</response>
    /// <response code="409">Email address is already in use by another account.</response>
    [HttpPost("participants/register")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RegisterParticipant(RegisterParticipant request)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        if (await db.Organisers.AnyAsync(x => x.Email == email) || await db.Participants.AnyAsync(x => x.Email == email))
            return Conflict(new { message = "Email is already registered." });

        var user = new Participant { Name = request.Name.Trim(), Email = email, Age = request.Age, Gender = request.Gender.Trim() };
        user.PasswordHash = _hasher.HashPassword(user.Email, request.Password);
        db.Participants.Add(user);
        await db.SaveChangesAsync();

        return Created($"/api/participants/{user.ParticipantId}", new { user.ParticipantId, user.Name, user.Email, user.Age, user.Gender, role = "Participant" });
    }

    /// <summary>Registers an account by providing personal details and selecting either Organiser or Participant role.</summary>
    /// <param name="request">Registration payload with personal details and role selection.</param>
    /// <response code="201">Account created successfully.</response>
    /// <response code="400">Invalid registration data or role.</response>
    /// <response code="409">Email address is already registered.</response>
    [HttpPost("register")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register(RegisterRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        if (await db.Organisers.AnyAsync(x => x.Email == email) || await db.Participants.AnyAsync(x => x.Email == email))
            return Conflict(new { message = "Email is already registered." });

        if (string.Equals(request.Role, "Organiser", StringComparison.OrdinalIgnoreCase))
        {
            var org = new Organiser { Name = request.Name.Trim(), Email = email };
            org.PasswordHash = _hasher.HashPassword(org.Email, request.Password);
            db.Organisers.Add(org);
            await db.SaveChangesAsync();
            return Created($"/api/organisers/{org.OrganiserId}", new { org.OrganiserId, org.Name, org.Email, role = "Organiser" });
        }
        else if (string.Equals(request.Role, "Participant", StringComparison.OrdinalIgnoreCase))
        {
            if (request.Age is null or < 1 or > 120 || string.IsNullOrWhiteSpace(request.Gender))
                return BadRequest(new { message = "Participants must provide valid age (1-120) and gender." });

            var part = new Participant { Name = request.Name.Trim(), Email = email, Age = request.Age.Value, Gender = request.Gender.Trim() };
            part.PasswordHash = _hasher.HashPassword(part.Email, request.Password);
            db.Participants.Add(part);
            await db.SaveChangesAsync();
            return Created($"/api/participants/{part.ParticipantId}", new { part.ParticipantId, part.Name, part.Email, part.Age, part.Gender, role = "Participant" });
        }

        return BadRequest(new { message = "Role must be either 'Organiser' or 'Participant'." });
    }

    /// <summary>Verifies organiser credentials and starts a role-specific server session.</summary>
    /// <param name="request">Login credentials (email and password).</param>
    /// <response code="200">Session successfully started with role 'Organiser'.</response>
    /// <response code="400">Missing or malformed credentials.</response>
    /// <response code="401">Invalid email or password.</response>
    [HttpPost("organisers/login")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> OrganiserLogin(LoginRequest request)
    {
        var user = await db.Organisers.SingleOrDefaultAsync(x => x.Email == request.Email.Trim().ToLowerInvariant());
        if (user is null || _hasher.VerifyHashedPassword(user.Email, user.PasswordHash, request.Password) == PasswordVerificationResult.Failed)
            return Unauthorized(new { message = "Invalid email or password." });

        SetSession("Organiser", user.OrganiserId, user.Name, user.Email);
        return Ok(new { user.OrganiserId, user.Name, user.Email, role = "Organiser" });
    }

    /// <summary>Verifies participant credentials and starts a role-specific server session.</summary>
    /// <param name="request">Login credentials (email and password).</param>
    /// <response code="200">Session successfully started with role 'Participant'.</response>
    /// <response code="400">Missing or malformed credentials.</response>
    /// <response code="401">Invalid email or password.</response>
    [HttpPost("participants/login")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ParticipantLogin(LoginRequest request)
    {
        var user = await db.Participants.SingleOrDefaultAsync(x => x.Email == request.Email.Trim().ToLowerInvariant());
        if (user is null || _hasher.VerifyHashedPassword(user.Email, user.PasswordHash, request.Password) == PasswordVerificationResult.Failed)
            return Unauthorized(new { message = "Invalid email or password." });

        SetSession("Participant", user.ParticipantId, user.Name, user.Email);
        return Ok(new { user.ParticipantId, user.Name, user.Email, role = "Participant" });
    }

    /// <summary>Unified login endpoint that authenticates either an organiser or a participant.</summary>
    /// <param name="request">Login credentials (email and password).</param>
    /// <response code="200">Login successful, returns user identity and role.</response>
    /// <response code="400">Missing or malformed credentials.</response>
    /// <response code="401">Invalid email or password.</response>
    [HttpPost("login")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        var org = await db.Organisers.SingleOrDefaultAsync(x => x.Email == email);
        if (org is not null && _hasher.VerifyHashedPassword(org.Email, org.PasswordHash, request.Password) != PasswordVerificationResult.Failed)
        {
            SetSession("Organiser", org.OrganiserId, org.Name, org.Email);
            return Ok(new { userId = org.OrganiserId, org.Name, org.Email, role = "Organiser" });
        }

        var part = await db.Participants.SingleOrDefaultAsync(x => x.Email == email);
        if (part is not null && _hasher.VerifyHashedPassword(part.Email, part.PasswordHash, request.Password) != PasswordVerificationResult.Failed)
        {
            SetSession("Participant", part.ParticipantId, part.Name, part.Email);
            return Ok(new { userId = part.ParticipantId, part.Name, part.Email, role = "Participant" });
        }

        return Unauthorized(new { message = "Invalid email or password." });
    }

    /// <summary>Clears the caller's server-side session and invalidates the session cookie.</summary>
    /// <response code="204">Session successfully terminated.</response>
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public IActionResult Logout()
    {
        HttpContext.Session.Clear();
        return NoContent();
    }

    /// <summary>Returns the currently authenticated user's session details.</summary>
    /// <response code="200">Current session details.</response>
    /// <response code="401">Caller has no active session.</response>
    [HttpGet("me")]
    [HttpGet("auth/me")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult GetSession()
    {
        var userId = HttpContext.Session.GetInt32("userId");
        var role = HttpContext.Session.GetString("role");
        var name = HttpContext.Session.GetString("name");
        var email = HttpContext.Session.GetString("email");

        if (userId is null || role is null)
            return Unauthorized(new { message = "No active session found." });

        return Ok(new { userId = userId.Value, name, email, role });
    }

    /// <summary>Returns organiser profile details by ID.</summary>
    /// <param name="id">Organiser ID.</param>
    /// <response code="200">Organiser details.</response>
    /// <response code="404">Organiser not found.</response>
    [HttpGet("organisers/{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetOrganiser(int id)
    {
        var org = await db.Organisers.AsNoTracking().Where(o => o.OrganiserId == id).Select(o => new { o.OrganiserId, o.Name, o.Email }).SingleOrDefaultAsync();
        return org is null ? NotFound() : Ok(org);
    }

    private void SetSession(string role, int id, string name, string email)
    {
        HttpContext.Session.Clear();
        HttpContext.Session.SetString("role", role);
        HttpContext.Session.SetInt32("userId", id);
        HttpContext.Session.SetString("name", name);
        HttpContext.Session.SetString("email", email);
    }
}
