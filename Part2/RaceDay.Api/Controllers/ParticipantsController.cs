using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Models;

namespace RaceDay.Api.Controllers;

[ApiController]
[Route("api/participants")]
[Produces("application/json")]
public class ParticipantsController(RaceDayContext db) : ControllerBase
{
    private int? UserId => HttpContext.Session.GetInt32("userId");
    private string? Role => HttpContext.Session.GetString("role");

    /// <summary>Lists all registered participants (available to Organisers).</summary>
    /// <response code="200">List of participants.</response>
    /// <response code="401">Caller is not authenticated.</response>
    /// <response code="403">Caller is not an Organiser.</response>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAll()
    {
        if (UserId is null) return Unauthorized();
        if (Role != "Organiser") return StatusCode(StatusCodes.Status403Forbidden);

        var list = await db.Participants
            .AsNoTracking()
            .Select(p => new
            {
                p.ParticipantId,
                p.Name,
                p.Email,
                p.Age,
                p.Gender
            })
            .ToListAsync();

        return Ok(list);
    }

    /// <summary>Returns the profile details for a specific participant.</summary>
    /// <param name="id">Participant ID.</param>
    /// <response code="200">Participant details.</response>
    /// <response code="401">Caller is not authenticated.</response>
    /// <response code="403">Caller is not authorised to view this participant.</response>
    /// <response code="404">Participant not found.</response>
    [HttpGet("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(int id)
    {
        if (UserId is null) return Unauthorized();
        if (Role == "Participant" && id != UserId) return StatusCode(StatusCodes.Status403Forbidden);

        var p = await db.Participants
            .AsNoTracking()
            .Where(x => x.ParticipantId == id)
            .Select(x => new
            {
                x.ParticipantId,
                x.Name,
                x.Email,
                x.Age,
                x.Gender
            })
            .SingleOrDefaultAsync();

        return p is null ? NotFound() : Ok(p);
    }

    /// <summary>Updates a participant's details (accessible to that participant).</summary>
    /// <param name="id">Participant ID.</param>
    /// <param name="request">Update data containing name, age, and gender.</param>
    /// <response code="200">Participant updated successfully.</response>
    /// <response code="400">Invalid update data.</response>
    /// <response code="401">Caller is not authenticated.</response>
    /// <response code="403">Caller cannot update another participant's record.</response>
    /// <response code="404">Participant not found.</response>
    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, ParticipantUpdateRequest request)
    {
        if (UserId is null) return Unauthorized();
        if (Role != "Participant" || id != UserId) return StatusCode(StatusCodes.Status403Forbidden);

        var p = await db.Participants.FindAsync(id);
        if (p is null) return NotFound();

        if (!string.IsNullOrWhiteSpace(request.Name))
            p.Name = request.Name.Trim();
        if (request.Age is not null)
            p.Age = request.Age.Value;
        if (!string.IsNullOrWhiteSpace(request.Gender))
            p.Gender = request.Gender.Trim();

        await db.SaveChangesAsync();

        return Ok(new
        {
            p.ParticipantId,
            p.Name,
            p.Email,
            p.Age,
            p.Gender
        });
    }

    /// <summary>Deletes a participant account.</summary>
    /// <param name="id">Participant ID.</param>
    /// <response code="204">Participant deleted successfully.</response>
    /// <response code="401">Caller is not authenticated.</response>
    /// <response code="403">Caller is not authorised to delete this participant.</response>
    /// <response code="404">Participant not found.</response>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id)
    {
        if (UserId is null) return Unauthorized();
        if (Role != "Organiser" && (Role != "Participant" || id != UserId))
            return StatusCode(StatusCodes.Status403Forbidden);

        var p = await db.Participants.FindAsync(id);
        if (p is null) return NotFound();

        db.Participants.Remove(p);
        await db.SaveChangesAsync();

        return NoContent();
    }
}
