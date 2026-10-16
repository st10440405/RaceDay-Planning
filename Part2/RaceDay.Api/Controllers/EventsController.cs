using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Models;

namespace RaceDay.Api.Controllers;

[ApiController]
[Route("api/events")]
public class EventsController(RaceDayContext db) : ControllerBase
{
    private int? UserId => HttpContext.Session.GetInt32("userId");
    private bool IsOrganiser => HttpContext.Session.GetString("role") == "Organiser";

    /// <summary>Lists upcoming events with name, date, location, distance, type, and organiser.</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll() => Ok(await db.Events.Where(e => e.EventDate >= DateTime.Today).OrderBy(e => e.EventDate).Select(e => new { e.EventId, e.Name, e.EventDate, e.Location, e.Description, e.DistanceKm, e.EventType, e.OrganiserId }).ToListAsync());

    /// <summary>Returns the requested event details or 404 when the event does not exist.</summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id) { var item = await db.Events.AsNoTracking().Where(e => e.EventId == id).Select(e => new { e.EventId, e.Name, e.EventDate, e.Location, e.Description, e.DistanceKm, e.EventType, e.OrganiserId }).SingleOrDefaultAsync(); return item is null ? NotFound() : Ok(item); }

    /// <summary>Creates an event owned by the signed-in organiser.</summary>
    [HttpPost]
    public async Task<IActionResult> Create(EventRequest request)
    {
        if (UserId is null) return Unauthorized(); if (!IsOrganiser) return StatusCode(StatusCodes.Status403Forbidden);
        if (request.EventDate == default || request.EventDate.Date < DateTime.Today) return BadRequest(new { message = "Event date must be today or later." });
        var item = new RaceEvent { OrganiserId = UserId.Value, Name = request.Name.Trim(), EventDate = request.EventDate.Date, Location = request.Location.Trim(), Description = request.Description ?? "", DistanceKm = request.DistanceKm, EventType = request.EventType };
        db.Events.Add(item); await db.SaveChangesAsync(); return CreatedAtAction(nameof(Get), new { id = item.EventId }, item);
    }

    /// <summary>Updates an event owned by the signed-in organiser.</summary>
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, EventRequest request)
    {
        if (UserId is null) return Unauthorized(); if (!IsOrganiser) return StatusCode(StatusCodes.Status403Forbidden);
        if (request.EventDate == default || request.EventDate.Date < DateTime.Today) return BadRequest(new { message = "Event date must be today or later." });
        var item = await db.Events.FindAsync(id); if (item is null) return NotFound(); if (item.OrganiserId != UserId) return StatusCode(StatusCodes.Status403Forbidden);
        item.Name = request.Name.Trim(); item.EventDate = request.EventDate.Date; item.Location = request.Location.Trim(); item.Description = request.Description ?? ""; item.DistanceKm = request.DistanceKm; item.EventType = request.EventType;
        await db.SaveChangesAsync(); return NoContent();
    }

    /// <summary>Deletes an event owned by the signed-in organiser.</summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        if (UserId is null) return Unauthorized(); if (!IsOrganiser) return StatusCode(StatusCodes.Status403Forbidden);
        var item = await db.Events.FindAsync(id); if (item is null) return NotFound(); if (item.OrganiserId != UserId) return StatusCode(StatusCodes.Status403Forbidden);
        if (await db.Enrolments.AnyAsync(x => x.EventId == id)) return Conflict(new { message = "Events with enrolments cannot be deleted." });
        db.Events.Remove(item); await db.SaveChangesAsync(); return NoContent();
    }

    /// <summary>Lists the age or distance categories assigned to an event.</summary>
    [HttpGet("{id:int}/categories")]
    public async Task<IActionResult> Categories(int id)
    {
        if (!await db.Events.AnyAsync(e => e.EventId == id)) return NotFound();
        return Ok(await db.EventCategories.Where(x => x.EventId == id).Select(x => new { x.Category!.CategoryId, x.Category.Name, x.Category.Description }).ToListAsync());
    }

    /// <summary>Assigns an existing category to an event owned by the organiser.</summary>
    [HttpPost("{id:int}/categories")]
    public async Task<IActionResult> AddCategory(int id, CategoryAssignment request)
    {
        if (UserId is null) return Unauthorized(); if (!IsOrganiser) return StatusCode(StatusCodes.Status403Forbidden);
        var item = await db.Events.FindAsync(id); if (item is null) return NotFound(); if (item.OrganiserId != UserId) return StatusCode(StatusCodes.Status403Forbidden);
        if (!await db.Categories.AnyAsync(c => c.CategoryId == request.CategoryId)) return BadRequest(new { message = "Category does not exist." });
        if (await db.EventCategories.AnyAsync(x => x.EventId == id && x.CategoryId == request.CategoryId)) return Conflict(new { message = "Category is already assigned." });
        db.EventCategories.Add(new EventCategory { EventId = id, CategoryId = request.CategoryId }); await db.SaveChangesAsync(); return Created($"/api/events/{id}/categories", new { id, request.CategoryId });
    }

    /// <summary>Creates a category and assigns it to an event owned by the organiser.</summary>
    [HttpPost("{id:int}/categories/new")]
    public async Task<IActionResult> CreateCategory(int id, CategoryRequest request)
    {
        if (UserId is null) return Unauthorized(); if (!IsOrganiser) return StatusCode(StatusCodes.Status403Forbidden);
        var item = await db.Events.FindAsync(id); if (item is null) return NotFound(); if (item.OrganiserId != UserId) return StatusCode(StatusCodes.Status403Forbidden);
        var category = new Category { Name = request.Name.Trim(), Description = request.Description ?? "" }; db.Categories.Add(category); await db.SaveChangesAsync();
        db.EventCategories.Add(new EventCategory { EventId = id, CategoryId = category.CategoryId }); await db.SaveChangesAsync(); return Created($"/api/events/{id}/categories", category);
    }
}

