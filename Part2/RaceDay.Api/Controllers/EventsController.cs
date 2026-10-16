using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Models;

namespace RaceDay.Api.Controllers;

[ApiController]
[Route("api/events")]
[Produces("application/json")]
public class EventsController(RaceDayContext db) : ControllerBase
{
    private int? UserId => HttpContext.Session.GetInt32("userId");
    private bool IsOrganiser => HttpContext.Session.GetString("role") == "Organiser";

    /// <summary>Lists upcoming events with name, date, location, distance, type, and organiser ID.</summary>
    /// <param name="upcomingOnly">Whether to restrict results to upcoming events (default: true).</param>
    /// <response code="200">List of events.</response>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] bool upcomingOnly = true)
    {
        var query = db.Events.AsNoTracking();
        if (upcomingOnly)
        {
            query = query.Where(e => e.EventDate >= DateTime.Today);
        }

        var list = await query
            .OrderBy(e => e.EventDate)
            .Select(e => new
            {
                e.EventId,
                e.Name,
                e.EventDate,
                e.Location,
                e.Description,
                e.DistanceKm,
                e.EventType,
                e.OrganiserId
            })
            .ToListAsync();

        return Ok(list);
    }

    /// <summary>Returns the requested event details or 404 when the event does not exist.</summary>
    /// <param name="id">Event ID.</param>
    /// <response code="200">Event details.</response>
    /// <response code="404">Event not found.</response>
    [HttpGet("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(int id)
    {
        var item = await db.Events
            .AsNoTracking()
            .Where(e => e.EventId == id)
            .Select(e => new
            {
                e.EventId,
                e.Name,
                e.EventDate,
                e.Location,
                e.Description,
                e.DistanceKm,
                e.EventType,
                e.OrganiserId
            })
            .SingleOrDefaultAsync();

        return item is null ? NotFound() : Ok(item);
    }

    /// <summary>Creates a new event owned by the signed-in organiser.</summary>
    /// <param name="request">Event creation data including name, date, location, distance, and type (run, walk, or cycle).</param>
    /// <response code="201">Event created successfully.</response>
    /// <response code="400">Invalid event data or date is in the past.</response>
    /// <response code="401">Caller is not authenticated.</response>
    /// <response code="403">Caller is not an Organiser.</response>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create(EventRequest request)
    {
        if (UserId is null) return Unauthorized();
        if (!IsOrganiser) return StatusCode(StatusCodes.Status403Forbidden);

        if (request.EventDate == default || request.EventDate.Date < DateTime.Today)
            return BadRequest(new { message = "Event date must be today or later." });

        var item = new RaceEvent
        {
            OrganiserId = UserId.Value,
            Name = request.Name.Trim(),
            EventDate = request.EventDate.Date,
            Location = request.Location.Trim(),
            Description = request.Description ?? "",
            DistanceKm = request.DistanceKm,
            EventType = request.EventType
        };

        db.Events.Add(item);
        await db.SaveChangesAsync();

        return CreatedAtAction(nameof(Get), new { id = item.EventId }, new
        {
            item.EventId,
            item.Name,
            item.EventDate,
            item.Location,
            item.Description,
            item.DistanceKm,
            item.EventType,
            item.OrganiserId
        });
    }

    /// <summary>Updates an existing event owned by the signed-in organiser.</summary>
    /// <param name="id">Event ID.</param>
    /// <param name="request">Updated event details.</param>
    /// <response code="204">Event updated successfully.</response>
    /// <response code="400">Invalid event data or date is in the past.</response>
    /// <response code="401">Caller is not authenticated.</response>
    /// <response code="403">Caller is not the event owner or not an organiser.</response>
    /// <response code="404">Event not found.</response>
    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, EventRequest request)
    {
        if (UserId is null) return Unauthorized();
        if (!IsOrganiser) return StatusCode(StatusCodes.Status403Forbidden);

        if (request.EventDate == default || request.EventDate.Date < DateTime.Today)
            return BadRequest(new { message = "Event date must be today or later." });

        var item = await db.Events.FindAsync(id);
        if (item is null) return NotFound();
        if (item.OrganiserId != UserId) return StatusCode(StatusCodes.Status403Forbidden);

        item.Name = request.Name.Trim();
        item.EventDate = request.EventDate.Date;
        item.Location = request.Location.Trim();
        item.Description = request.Description ?? "";
        item.DistanceKm = request.DistanceKm;
        item.EventType = request.EventType;

        await db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Deletes an event owned by the signed-in organiser.</summary>
    /// <param name="id">Event ID.</param>
    /// <response code="204">Event deleted successfully.</response>
    /// <response code="401">Caller is not authenticated.</response>
    /// <response code="403">Caller is not the event owner or not an organiser.</response>
    /// <response code="404">Event not found.</response>
    /// <response code="409">Cannot delete an event that already has participant enrolments.</response>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(int id)
    {
        if (UserId is null) return Unauthorized();
        if (!IsOrganiser) return StatusCode(StatusCodes.Status403Forbidden);

        var item = await db.Events.FindAsync(id);
        if (item is null) return NotFound();
        if (item.OrganiserId != UserId) return StatusCode(StatusCodes.Status403Forbidden);

        if (await db.Enrolments.AnyAsync(x => x.EventId == id))
            return Conflict(new { message = "Events with enrolments cannot be deleted." });

        db.Events.Remove(item);
        await db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Lists the age or distance categories assigned to an event.</summary>
    /// <param name="id">Event ID.</param>
    /// <response code="200">List of categories assigned to the event.</response>
    /// <response code="404">Event not found.</response>
    [HttpGet("{id:int}/categories")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Categories(int id)
    {
        if (!await db.Events.AnyAsync(e => e.EventId == id)) return NotFound();

        var categories = await db.EventCategories
            .Where(x => x.EventId == id)
            .Select(x => new
            {
                x.Category!.CategoryId,
                x.Category.Name,
                x.Category.Description
            })
            .ToListAsync();

        return Ok(categories);
    }

    /// <summary>Assigns an existing category to an event owned by the signed-in organiser.</summary>
    /// <param name="id">Event ID.</param>
    /// <param name="request">Category assignment payload containing CategoryId.</param>
    /// <response code="201">Category successfully assigned to event.</response>
    /// <response code="400">Category does not exist.</response>
    /// <response code="401">Caller is not authenticated.</response>
    /// <response code="403">Caller does not own the event or is not an organiser.</response>
    /// <response code="404">Event not found.</response>
    /// <response code="409">Category is already assigned to this event.</response>
    [HttpPost("{id:int}/categories")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AddCategory(int id, CategoryAssignment request)
    {
        if (UserId is null) return Unauthorized();
        if (!IsOrganiser) return StatusCode(StatusCodes.Status403Forbidden);

        var item = await db.Events.FindAsync(id);
        if (item is null) return NotFound();
        if (item.OrganiserId != UserId) return StatusCode(StatusCodes.Status403Forbidden);

        if (!await db.Categories.AnyAsync(c => c.CategoryId == request.CategoryId))
            return BadRequest(new { message = "Category does not exist." });

        if (await db.EventCategories.AnyAsync(x => x.EventId == id && x.CategoryId == request.CategoryId))
            return Conflict(new { message = "Category is already assigned to this event." });

        db.EventCategories.Add(new EventCategory { EventId = id, CategoryId = request.CategoryId });
        await db.SaveChangesAsync();

        return Created($"/api/events/{id}/categories", new { id, request.CategoryId });
    }

    /// <summary>Creates a new category and assigns it to an event owned by the signed-in organiser.</summary>
    /// <param name="id">Event ID.</param>
    /// <param name="request">Category creation data including name and description.</param>
    /// <response code="201">Category created and assigned successfully.</response>
    /// <response code="400">Invalid category data.</response>
    /// <response code="401">Caller is not authenticated.</response>
    /// <response code="403">Caller does not own the event or is not an organiser.</response>
    /// <response code="404">Event not found.</response>
    [HttpPost("{id:int}/categories/new")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateCategory(int id, CategoryRequest request)
    {
        if (UserId is null) return Unauthorized();
        if (!IsOrganiser) return StatusCode(StatusCodes.Status403Forbidden);

        var item = await db.Events.FindAsync(id);
        if (item is null) return NotFound();
        if (item.OrganiserId != UserId) return StatusCode(StatusCodes.Status403Forbidden);

        var category = new Category { Name = request.Name.Trim(), Description = request.Description ?? "" };
        db.Categories.Add(category);
        await db.SaveChangesAsync();

        db.EventCategories.Add(new EventCategory { EventId = id, CategoryId = category.CategoryId });
        await db.SaveChangesAsync();

        return Created($"/api/events/{id}/categories", new
        {
            category.CategoryId,
            category.Name,
            category.Description
        });
    }

    /// <summary>Lists recorded race results and finishing positions for an event.</summary>
    /// <param name="id">Event ID.</param>
    /// <response code="200">List of participant results for the event.</response>
    /// <response code="404">Event not found.</response>
    [HttpGet("{id:int}/results")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetResults(int id)
    {
        if (!await db.Events.AnyAsync(e => e.EventId == id)) return NotFound();

        var results = await db.Enrolments
            .Where(e => e.EventId == id && e.FinishTime != null)
            .OrderBy(e => e.FinishPosition ?? int.MaxValue)
            .ThenBy(e => e.FinishTime)
            .Select(e => new
            {
                e.EnrolmentId,
                e.ParticipantId,
                ParticipantName = e.Participant!.Name,
                e.CategoryId,
                CategoryName = e.Category!.Name,
                e.FinishTime,
                e.FinishPosition,
                e.Status
            })
            .ToListAsync();

        return Ok(results);
    }
}
