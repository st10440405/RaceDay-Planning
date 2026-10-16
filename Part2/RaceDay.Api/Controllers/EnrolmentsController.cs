using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Models;

namespace RaceDay.Api.Controllers;

[ApiController]
[Route("api/enrolments")]
public class EnrolmentsController(RaceDayContext db) : ControllerBase
{
    private int? UserId => HttpContext.Session.GetInt32("userId");
    private string? Role => HttpContext.Session.GetString("role");

    /// <summary>Enrols the signed-in participant in an available event category.</summary>
    [HttpPost]
    public async Task<IActionResult> Enrol(EnrolRequest request)
    {
        if (UserId is null) return Unauthorized(); if (Role != "Participant") return StatusCode(StatusCodes.Status403Forbidden);
        var offering = await db.EventCategories.AnyAsync(x => x.EventId == request.EventId && x.CategoryId == request.CategoryId);
        if (!offering) return BadRequest(new { message = "The selected category is not available for this event." });
        if (await db.Enrolments.AnyAsync(x => x.ParticipantId == UserId && x.EventId == request.EventId)) return Conflict(new { message = "You are already enrolled in this event." });
        var item = new Enrolment { ParticipantId = UserId.Value, EventId = request.EventId, CategoryId = request.CategoryId };
        db.Enrolments.Add(item); await db.SaveChangesAsync(); return Created($"/api/enrolments/participant/{UserId}", new { item.EnrolmentId, item.ParticipantId, item.EventId, item.CategoryId, item.Status, item.RegistrationDate });
    }

    /// <summary>Lists the caller's enrolments and any recorded results.</summary>
    [HttpGet("participant/{id:int}")]
    public async Task<IActionResult> ForParticipant(int id)
    {
        if (UserId is null) return Unauthorized(); if (Role != "Participant") return StatusCode(StatusCodes.Status403Forbidden); if (id != UserId) return StatusCode(StatusCodes.Status403Forbidden);
        return Ok(await db.Enrolments.Where(x => x.ParticipantId == id).Select(x => new { x.EnrolmentId, x.EventId, EventName = x.Event!.Name, x.CategoryId, CategoryName = x.Category!.Name, x.Status, x.RegistrationDate, x.FinishTime, x.FinishPosition }).ToListAsync());
    }

    /// <summary>Lists enrolments for an event owned by the signed-in organiser.</summary>
    [HttpGet("event/{id:int}")]
    public async Task<IActionResult> ForEvent(int id)
    {
        if (UserId is null) return Unauthorized(); if (Role != "Organiser") return StatusCode(StatusCodes.Status403Forbidden);
        if (!await db.Events.AnyAsync(x => x.EventId == id && x.OrganiserId == UserId)) return NotFound();
        return Ok(await db.Enrolments.Where(x => x.EventId == id).Select(x => new { x.EnrolmentId, x.ParticipantId, ParticipantName = x.Participant!.Name, x.CategoryId, CategoryName = x.Category!.Name, x.Status, x.RegistrationDate, x.FinishTime, x.FinishPosition }).ToListAsync());
    }

    /// <summary>Records or updates an enrolment's finish time and finishing position.</summary>
    [HttpPut("{id:int}/result")]
    public async Task<IActionResult> RecordResult(int id, ResultRequest request)
    {
        if (UserId is null) return Unauthorized(); if (Role != "Organiser") return StatusCode(StatusCodes.Status403Forbidden);
        var enrolment = await db.Enrolments.Include(x => x.Event).SingleOrDefaultAsync(x => x.EnrolmentId == id);
        if (enrolment is null) return NotFound(); if (enrolment.Event!.OrganiserId != UserId) return StatusCode(StatusCodes.Status403Forbidden);
        if (request.FinishTime < TimeSpan.Zero) return BadRequest();
        enrolment.FinishTime = request.FinishTime; enrolment.FinishPosition = request.FinishPosition; await db.SaveChangesAsync(); return NoContent();
    }
}

