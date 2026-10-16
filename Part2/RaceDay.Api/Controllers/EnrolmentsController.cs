using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Models;

namespace RaceDay.Api.Controllers;

[ApiController]
[Route("api/enrolments")]
[Produces("application/json")]
public class EnrolmentsController(RaceDayContext db) : ControllerBase
{
    private int? UserId => HttpContext.Session.GetInt32("userId");
    private string? Role => HttpContext.Session.GetString("role");

    /// <summary>Enrols the signed-in participant into an available category of an athletic event.</summary>
    /// <param name="request">Enrolment payload containing EventId and CategoryId.</param>
    /// <response code="201">Participant successfully enrolled.</response>
    /// <response code="400">The selected category is not offered for the specified event.</response>
    /// <response code="401">Caller is not authenticated.</response>
    /// <response code="403">Caller is not a Participant.</response>
    /// <response code="409">Participant is already enrolled in this event.</response>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Enrol(EnrolRequest request)
    {
        if (UserId is null) return Unauthorized();
        if (Role != "Participant") return StatusCode(StatusCodes.Status403Forbidden);

        var offering = await db.EventCategories.AnyAsync(x => x.EventId == request.EventId && x.CategoryId == request.CategoryId);
        if (!offering)
            return BadRequest(new { message = "The selected category is not available for this event." });

        if (await db.Enrolments.AnyAsync(x => x.ParticipantId == UserId && x.EventId == request.EventId))
            return Conflict(new { message = "You are already enrolled in this event." });

        var item = new Enrolment
        {
            ParticipantId = UserId.Value,
            EventId = request.EventId,
            CategoryId = request.CategoryId,
            Status = "Registered",
            RegistrationDate = DateTime.UtcNow
        };

        db.Enrolments.Add(item);
        await db.SaveChangesAsync();

        return Created($"/api/enrolments/participant/{UserId}", new
        {
            item.EnrolmentId,
            item.ParticipantId,
            item.EventId,
            item.CategoryId,
            item.Status,
            item.RegistrationDate
        });
    }

    /// <summary>Lists the caller's enrolments and any recorded race results.</summary>
    /// <param name="id">Participant ID (must match the signed-in participant).</param>
    /// <response code="200">List of enrolments and results for the participant.</response>
    /// <response code="401">Caller is not authenticated.</response>
    /// <response code="403">Caller is not a Participant or is attempting to view another participant's enrolments.</response>
    [HttpGet("participant/{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ForParticipant(int id)
    {
        if (UserId is null) return Unauthorized();
        if (Role != "Participant" || id != UserId) return StatusCode(StatusCodes.Status403Forbidden);

        var list = await db.Enrolments
            .AsNoTracking()
            .Where(x => x.ParticipantId == id)
            .Select(x => new
            {
                x.EnrolmentId,
                x.EventId,
                EventName = x.Event!.Name,
                x.CategoryId,
                CategoryName = x.Category!.Name,
                x.Status,
                x.RegistrationDate,
                x.FinishTime,
                x.FinishPosition
            })
            .ToListAsync();

        return Ok(list);
    }

    /// <summary>Lists the signed-in participant's own enrolments and race results.</summary>
    /// <response code="200">List of caller's enrolments and results.</response>
    /// <response code="401">Caller is not authenticated.</response>
    /// <response code="403">Caller is not a Participant.</response>
    [HttpGet("my")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public Task<IActionResult> MyEnrolments() => ForParticipant(UserId ?? 0);

    /// <summary>Lists all participant enrolments for an event owned by the signed-in organiser.</summary>
    /// <param name="id">Event ID.</param>
    /// <response code="200">List of enrolments for the event.</response>
    /// <response code="401">Caller is not authenticated.</response>
    /// <response code="403">Caller is not an Organiser or does not own the event.</response>
    /// <response code="404">Event not found.</response>
    [HttpGet("event/{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ForEvent(int id)
    {
        if (UserId is null) return Unauthorized();
        if (Role != "Organiser") return StatusCode(StatusCodes.Status403Forbidden);

        var evt = await db.Events.FindAsync(id);
        if (evt is null) return NotFound();
        if (evt.OrganiserId != UserId) return StatusCode(StatusCodes.Status403Forbidden);

        var list = await db.Enrolments
            .AsNoTracking()
            .Where(x => x.EventId == id)
            .Select(x => new
            {
                x.EnrolmentId,
                x.ParticipantId,
                ParticipantName = x.Participant!.Name,
                x.CategoryId,
                CategoryName = x.Category!.Name,
                x.Status,
                x.RegistrationDate,
                x.FinishTime,
                x.FinishPosition
            })
            .ToListAsync();

        return Ok(list);
    }

    /// <summary>Records or updates an enrolment's finish time and finishing position.</summary>
    /// <param name="id">Enrolment ID.</param>
    /// <param name="request">Result data containing finish time and position.</param>
    /// <response code="204">Result recorded successfully.</response>
    /// <response code="400">Invalid finish time or finishing position.</response>
    /// <response code="401">Caller is not authenticated.</response>
    /// <response code="403">Caller does not own the event or is not an organiser.</response>
    /// <response code="404">Enrolment not found.</response>
    [HttpPut("{id:int}/result")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RecordResult(int id, ResultRequest request)
    {
        if (UserId is null) return Unauthorized();
        if (Role != "Organiser") return StatusCode(StatusCodes.Status403Forbidden);

        var enrolment = await db.Enrolments.Include(x => x.Event).SingleOrDefaultAsync(x => x.EnrolmentId == id);
        if (enrolment is null) return NotFound();
        if (enrolment.Event!.OrganiserId != UserId) return StatusCode(StatusCodes.Status403Forbidden);

        if (request.FinishTime < TimeSpan.Zero || request.FinishPosition <= 0)
            return BadRequest(new { message = "Finish time cannot be negative and position must be positive." });

        enrolment.FinishTime = request.FinishTime;
        enrolment.FinishPosition = request.FinishPosition;
        enrolment.Status = "Completed";

        await db.SaveChangesAsync();
        return NoContent();
    }
}
