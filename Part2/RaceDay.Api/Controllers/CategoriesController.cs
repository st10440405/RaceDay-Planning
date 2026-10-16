using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Models;

namespace RaceDay.Api.Controllers;

[ApiController]
[Route("api/categories")]
[Produces("application/json")]
public class CategoriesController(RaceDayContext db) : ControllerBase
{
    private int? UserId => HttpContext.Session.GetInt32("userId");
    private bool IsOrganiser => HttpContext.Session.GetString("role") == "Organiser";

    /// <summary>Lists all available age and distance categories in the system.</summary>
    /// <response code="200">List of all available categories.</response>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll()
    {
        var items = await db.Categories
            .AsNoTracking()
            .Select(c => new
            {
                c.CategoryId,
                c.Name,
                c.Description
            })
            .ToListAsync();

        return Ok(items);
    }

    /// <summary>Returns details for a specific category by ID.</summary>
    /// <param name="id">Category ID.</param>
    /// <response code="200">Category details.</response>
    /// <response code="404">Category not found.</response>
    [HttpGet("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(int id)
    {
        var item = await db.Categories
            .AsNoTracking()
            .Where(c => c.CategoryId == id)
            .Select(c => new
            {
                c.CategoryId,
                c.Name,
                c.Description
            })
            .SingleOrDefaultAsync();

        return item is null ? NotFound() : Ok(item);
    }

    /// <summary>Allows an organiser to define a new category (e.g. Under 20, Senior, 10km, 21km).</summary>
    /// <param name="request">Category creation payload containing name and description.</param>
    /// <response code="201">Category created successfully.</response>
    /// <response code="400">Invalid category data.</response>
    /// <response code="401">Caller is not authenticated.</response>
    /// <response code="403">Caller is not an Organiser.</response>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create(CategoryRequest request)
    {
        if (UserId is null) return Unauthorized();
        if (!IsOrganiser) return StatusCode(StatusCodes.Status403Forbidden);

        var item = new Category
        {
            Name = request.Name.Trim(),
            Description = request.Description ?? ""
        };

        db.Categories.Add(item);
        await db.SaveChangesAsync();

        return CreatedAtAction(nameof(Get), new { id = item.CategoryId }, new
        {
            item.CategoryId,
            item.Name,
            item.Description
        });
    }
}
