using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RaceDay.Api.Data;

namespace RaceDay.Api.Tests;

public class ApiTests
{
    [Fact]
    public async Task EventCreationRequiresAuthentication()
    {
        using var app = new ApiFactory(); using var client = app.CreateClient();
        var response = await client.PostAsJsonAsync("/api/events", EventBody());
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task SwaggerListsDocumentedApiOperations()
    {
        using var app = new ApiFactory(); using var client = app.CreateClient();
        var json = await client.GetStringAsync("/swagger/v1/swagger.json");
        Assert.Contains("/api/events", json);
        Assert.Contains("Creates an organiser account using a unique email and a hashed password.", json);
    }

    [Fact]
    public async Task OrganiserCanCreateAndBrowseEvent()
    {
        using var app = new ApiFactory(); using var client = app.CreateClient();
        await client.PostAsJsonAsync("/api/organisers/register", new { name = "Race Org", email = "org@example.com", password = "strong-pass-1" });
        var login = await client.PostAsJsonAsync("/api/organisers/login", new { email = "org@example.com", password = "strong-pass-1" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var created = await client.PostAsJsonAsync("/api/events", EventBody());
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var events = await client.GetStringAsync("/api/events");
        Assert.Contains("City Run", events);
    }

    [Fact]
    public async Task OrganiserCannotCreateEventInThePast()
    {
        using var app = new ApiFactory(); using var client = app.CreateClient();
        await RegisterAndLoginOrganiser(client);
        var response = await client.PostAsJsonAsync("/api/events", new { name = "Past Run", eventDate = DateTime.Today.AddDays(-1), location = "Park", description = "Run", distanceKm = 10, eventType = "Run" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task LogoutClearsAuthenticatedSession()
    {
        using var app = new ApiFactory(); using var client = app.CreateClient();
        await RegisterAndLoginOrganiser(client);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/profile")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/api/logout", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/profile")).StatusCode);
    }

    [Fact]
    public async Task RegistrationHashesPasswordAndRejectsDuplicateEmail()
    {
        using var app = new ApiFactory(); using var client = app.CreateClient();
        var response = await client.PostAsJsonAsync("/api/organisers/register", new { name = "Race Org", email = "ORG@example.com", password = "strong-pass-1" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var duplicate = await client.PostAsJsonAsync("/api/participants/register", new { name = "Runner", email = "org@example.com", password = "strong-pass-1", age = 26, gender = "Other" });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        using var scope = app.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<RaceDayContext>();
        Assert.NotEqual("strong-pass-1", (await db.Organisers.SingleAsync()).PasswordHash);
    }

    [Fact]
    public async Task ParticipantCannotCreateEventButCanEnrollInAvailableCategory()
    {
        using var app = new ApiFactory(); using var client = app.CreateClient();
        await client.PostAsJsonAsync("/api/participants/register", new { name = "Runner", email = "runner@example.com", password = "strong-pass-1", age = 26, gender = "Other" });
        var login = await client.PostAsJsonAsync("/api/participants/login", new { email = "runner@example.com", password = "strong-pass-1" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/events", EventBody())).StatusCode);
        using var scope = app.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<RaceDayContext>();
        db.Categories.Add(new RaceDay.Api.Models.Category { Name = "10K", Description = "10 km" }); await db.SaveChangesAsync();
        db.Events.Add(new RaceDay.Api.Models.RaceEvent { Name = "City Run", EventDate = DateTime.Today.AddDays(20), Location = "Park", Description = "Run", DistanceKm = 10, EventType = "Run", OrganiserId = 1 });
        await db.SaveChangesAsync(); db.EventCategories.Add(new RaceDay.Api.Models.EventCategory { EventId = 1, CategoryId = 1 }); await db.SaveChangesAsync();
        var enrol = await client.PostAsJsonAsync("/api/enrolments", new { eventId = 1, categoryId = 1 });
        Assert.Equal(HttpStatusCode.Created, enrol.StatusCode);
        Assert.Single(await db.Enrolments.ToListAsync());
    }

    [Fact]
    public async Task LoginRejectsBadPassword()
    {
        using var app = new ApiFactory(); using var client = app.CreateClient();
        await client.PostAsJsonAsync("/api/organisers/register", new { name = "Race Org", email = "org@example.com", password = "strong-pass-1" });
        var response = await client.PostAsJsonAsync("/api/organisers/login", new { email = "org@example.com", password = "wrong-password" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static object EventBody() => new { name = "City Run", eventDate = DateTime.Today.AddDays(20), location = "Park", description = "Run", distanceKm = 10, eventType = "Run" };

    private static async Task RegisterAndLoginOrganiser(HttpClient client)
    {
        await client.PostAsJsonAsync("/api/organisers/register", new { name = "Race Org", email = "org@example.com", password = "strong-pass-1" });
        var response = await client.PostAsJsonAsync("/api/organisers/login", new { email = "org@example.com", password = "strong-pass-1" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}

public class ApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = Guid.NewGuid().ToString();
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<RaceDayContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<RaceDayContext>>();
            services.AddDbContext<RaceDayContext>(options => options.UseInMemoryDatabase(_databaseName));
        });
    }
}
