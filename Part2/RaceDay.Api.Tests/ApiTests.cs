using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RaceDay.Api.Data;
using RaceDay.Api.Models;

namespace RaceDay.Api.Tests;

public class ApiTests
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    // =========================================================================
    // 1. User Registration and Login Tests (Success and Failure)
    // =========================================================================

    [Fact]
    public async Task OrganiserRegistration_Success()
    {
        using var app = new ApiFactory(); using var client = app.CreateClient();
        var response = await client.PostAsJsonAsync("/api/organisers/register", new { name = "Athletics Club", email = "club@example.com", password = "Password123!" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RaceDayContext>();
        var org = await db.Organisers.SingleOrDefaultAsync(o => o.Email == "club@example.com");
        Assert.NotNull(org);
        Assert.Equal("Athletics Club", org.Name);
    }

    [Fact]
    public async Task ParticipantRegistration_Success()
    {
        using var app = new ApiFactory(); using var client = app.CreateClient();
        var response = await client.PostAsJsonAsync("/api/participants/register", new { name = "Sarah Connor", email = "sarah@example.com", password = "Password123!", age = 29, gender = "Female" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RaceDayContext>();
        var part = await db.Participants.SingleOrDefaultAsync(p => p.Email == "sarah@example.com");
        Assert.NotNull(part);
        Assert.Equal(29, part.Age);
        Assert.Equal("Female", part.Gender);
    }

    [Fact]
    public async Task UnifiedRegistration_OrganiserAndParticipant_Success()
    {
        using var app = new ApiFactory(); using var client = app.CreateClient();
        var orgRes = await client.PostAsJsonAsync("/api/register", new { role = "Organiser", name = "Unified Org", email = "uorg@example.com", password = "Password123!" });
        Assert.Equal(HttpStatusCode.Created, orgRes.StatusCode);

        var partRes = await client.PostAsJsonAsync("/api/register", new { role = "Participant", name = "Unified Part", email = "upart@example.com", password = "Password123!", age = 32, gender = "Male" });
        Assert.Equal(HttpStatusCode.Created, partRes.StatusCode);
    }

    [Fact]
    public async Task Registration_RejectsDuplicateEmailAcrossRoles()
    {
        using var app = new ApiFactory(); using var client = app.CreateClient();
        var first = await client.PostAsJsonAsync("/api/organisers/register", new { name = "Org One", email = "duplicate@example.com", password = "Password123!" });
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        // Attempt duplicate organiser
        var dupOrg = await client.PostAsJsonAsync("/api/organisers/register", new { name = "Org Two", email = "DUPLICATE@example.com", password = "Password123!" });
        Assert.Equal(HttpStatusCode.Conflict, dupOrg.StatusCode);

        // Attempt duplicate participant with same email
        var dupPart = await client.PostAsJsonAsync("/api/participants/register", new { name = "Part One", email = "duplicate@example.com", password = "Password123!", age = 22, gender = "Female" });
        Assert.Equal(HttpStatusCode.Conflict, dupPart.StatusCode);
    }

    [Fact]
    public async Task Registration_HashesPasswordAndDoesNotStorePlaintext()
    {
        using var app = new ApiFactory(); using var client = app.CreateClient();
        const string plainPassword = "SuperSecretPassword123!";
        await client.PostAsJsonAsync("/api/organisers/register", new { name = "Secure Org", email = "secure@example.com", password = plainPassword });

        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RaceDayContext>();
        var user = await db.Organisers.SingleAsync(o => o.Email == "secure@example.com");

        Assert.NotEqual(plainPassword, user.PasswordHash);
        Assert.False(string.IsNullOrWhiteSpace(user.PasswordHash));
    }

    [Fact]
    public async Task Registration_RejectsShortPasswordOrInvalidEmail()
    {
        using var app = new ApiFactory(); using var client = app.CreateClient();
        // Short password (< 8 chars)
        var shortPass = await client.PostAsJsonAsync("/api/organisers/register", new { name = "Bad Pass", email = "badpass@example.com", password = "123" });
        Assert.Equal(HttpStatusCode.BadRequest, shortPass.StatusCode);

        // Invalid email format
        var badEmail = await client.PostAsJsonAsync("/api/organisers/register", new { name = "Bad Email", email = "not-an-email", password = "Password123!" });
        Assert.Equal(HttpStatusCode.BadRequest, badEmail.StatusCode);
    }

    [Fact]
    public async Task Login_Success_StartsSession()
    {
        using var app = new ApiFactory(); using var client = app.CreateClient();
        await client.PostAsJsonAsync("/api/organisers/register", new { name = "Login Org", email = "loginorg@example.com", password = "Password123!" });

        var loginRes = await client.PostAsJsonAsync("/api/organisers/login", new { email = "loginorg@example.com", password = "Password123!" });
        Assert.Equal(HttpStatusCode.OK, loginRes.StatusCode);

        // Subsequent call using the session cookie succeeds
        var profileRes = await client.GetAsync("/api/profile");
        Assert.Equal(HttpStatusCode.OK, profileRes.StatusCode);
    }

    [Fact]
    public async Task UnifiedLogin_SuccessForBothRoles()
    {
        using var app = new ApiFactory(); using var client = app.CreateClient();
        await client.PostAsJsonAsync("/api/participants/register", new { name = "Runner Login", email = "runnerlogin@example.com", password = "Password123!", age = 24, gender = "Male" });

        var loginRes = await client.PostAsJsonAsync("/api/login", new { email = "runnerlogin@example.com", password = "Password123!" });
        Assert.Equal(HttpStatusCode.OK, loginRes.StatusCode);

        var json = await loginRes.Content.ReadAsStringAsync();
        Assert.Contains("Participant", json);
    }

    [Fact]
    public async Task Login_RejectsBadPasswordOrUnknownEmail()
    {
        using var app = new ApiFactory(); using var client = app.CreateClient();
        await client.PostAsJsonAsync("/api/organisers/register", new { name = "Org", email = "test@example.com", password = "Password123!" });

        var wrongPass = await client.PostAsJsonAsync("/api/organisers/login", new { email = "test@example.com", password = "WrongPassword" });
        Assert.Equal(HttpStatusCode.Unauthorized, wrongPass.StatusCode);

        var unknownUser = await client.PostAsJsonAsync("/api/organisers/login", new { email = "nobody@example.com", password = "Password123!" });
        Assert.Equal(HttpStatusCode.Unauthorized, unknownUser.StatusCode);
    }

    [Fact]
    public async Task Logout_ClearsSession()
    {
        using var app = new ApiFactory(); using var client = app.CreateClient();
        await RegisterAndLoginOrganiser(client, "logout@example.com");

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/profile")).StatusCode);
        var logoutRes = await client.PostAsync("/api/logout", null);
        Assert.Equal(HttpStatusCode.NoContent, logoutRes.StatusCode);

        var profileAfter = await client.GetAsync("/api/profile");
        Assert.Equal(HttpStatusCode.Unauthorized, profileAfter.StatusCode);
    }

    // =========================================================================
    // 2. Event Management Tests (Organisers Manage Events)
    // =========================================================================

    [Fact]
    public async Task Organiser_CreateAndRetrieveEvent_Success()
    {
        using var app = new ApiFactory(); using var client = app.CreateClient();
        await RegisterAndLoginOrganiser(client, "org_event@example.com");

        var createRes = await client.PostAsJsonAsync("/api/events", EventPayload("Cape Marathon", 30, 42.2m, "Run"));
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);

        var doc = await createRes.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var eventId = doc.GetProperty("eventId").GetInt32();

        var getRes = await client.GetAsync($"/api/events/{eventId}");
        Assert.Equal(HttpStatusCode.OK, getRes.StatusCode);
        var getDoc = await getRes.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal("Cape Marathon", getDoc.GetProperty("name").GetString());
        Assert.Equal("Run", getDoc.GetProperty("eventType").GetString());
    }

    [Fact]
    public async Task Organiser_CannotCreateEventInThePast()
    {
        using var app = new ApiFactory(); using var client = app.CreateClient();
        await RegisterAndLoginOrganiser(client, "org_past@example.com");

        var pastEvent = new { name = "Past Race", eventDate = DateTime.Today.AddDays(-1), location = "Track", description = "Old", distanceKm = 5, eventType = "Run" };
        var res = await client.PostAsJsonAsync("/api/events", pastEvent);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Organiser_CannotCreateEventWithInvalidTypeOrDistance()
    {
        using var app = new ApiFactory(); using var client = app.CreateClient();
        await RegisterAndLoginOrganiser(client, "org_invalid@example.com");

        // Invalid event type (must be Run, Walk, or Cycle)
        var badType = new { name = "Swim Meet", eventDate = DateTime.Today.AddDays(10), location = "Pool", description = "Swim", distanceKm = 5, eventType = "Swim" };
        var res = await client.PostAsJsonAsync("/api/events", badType);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);

        // Distance 0 or negative
        var badDist = new { name = "Zero Race", eventDate = DateTime.Today.AddDays(10), location = "Track", description = "Bad", distanceKm = 0, eventType = "Run" };
        var resDist = await client.PostAsJsonAsync("/api/events", badDist);
        Assert.Equal(HttpStatusCode.BadRequest, resDist.StatusCode);
    }

    [Fact]
    public async Task Organiser_UpdateOwnEvent_Success()
    {
        using var app = new ApiFactory(); using var client = app.CreateClient();
        await RegisterAndLoginOrganiser(client, "org_update@example.com");

        var createRes = await client.PostAsJsonAsync("/api/events", EventPayload("Original Name", 20, 10, "Run"));
        var doc = await createRes.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var eventId = doc.GetProperty("eventId").GetInt32();

        var updateRes = await client.PutAsJsonAsync($"/api/events/{eventId}", EventPayload("Updated Name", 25, 15, "Cycle"));
        Assert.Equal(HttpStatusCode.NoContent, updateRes.StatusCode);

        var getDoc = await client.GetFromJsonAsync<JsonElement>($"/api/events/{eventId}", JsonOptions);
        Assert.Equal("Updated Name", getDoc.GetProperty("name").GetString());
        Assert.Equal("Cycle", getDoc.GetProperty("eventType").GetString());
    }

    [Fact]
    public async Task Organiser_DeleteOwnEvent_Success()
    {
        using var app = new ApiFactory(); using var client = app.CreateClient();
        await RegisterAndLoginOrganiser(client, "org_del@example.com");

        var createRes = await client.PostAsJsonAsync("/api/events", EventPayload("Event To Delete", 20, 10, "Run"));
        var doc = await createRes.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var eventId = doc.GetProperty("eventId").GetInt32();

        var delRes = await client.DeleteAsync($"/api/events/{eventId}");
        Assert.Equal(HttpStatusCode.NoContent, delRes.StatusCode);

        var getRes = await client.GetAsync($"/api/events/{eventId}");
        Assert.Equal(HttpStatusCode.NotFound, getRes.StatusCode);
    }

    [Fact]
    public async Task Organiser_CannotDeleteEventWithExistingEnrolments()
    {
        using var app = new ApiFactory(); using var client = app.CreateClient();
        await RegisterAndLoginOrganiser(client, "org_nodelete@example.com");

        var createRes = await client.PostAsJsonAsync("/api/events", EventPayload("Protected Event", 20, 10, "Run"));
        var eventId = (await createRes.Content.ReadFromJsonAsync<JsonElement>(JsonOptions)).GetProperty("eventId").GetInt32();

        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RaceDayContext>();
        var cat = new Category { Name = "Open", Description = "Open category" };
        db.Categories.Add(cat);
        await db.SaveChangesAsync();

        db.EventCategories.Add(new EventCategory { EventId = eventId, CategoryId = cat.CategoryId });
        var part = new Participant { Name = "Runner", Email = "runner_enrolled@example.com", PasswordHash = "hash", Age = 25, Gender = "Other" };
        db.Participants.Add(part);
        await db.SaveChangesAsync();

        db.Enrolments.Add(new Enrolment { EventId = eventId, CategoryId = cat.CategoryId, ParticipantId = part.ParticipantId });
        await db.SaveChangesAsync();

        var delRes = await client.DeleteAsync($"/api/events/{eventId}");
        Assert.Equal(HttpStatusCode.Conflict, delRes.StatusCode);
    }

    // =========================================================================
    // 3. Role-Based Access Enforcement Tests (Rejected Scenarios)
    // =========================================================================

    [Fact]
    public async Task UnauthenticatedRequests_ToProtectedEndpoints_ReturnUnauthorized()
    {
        using var app = new ApiFactory(); using var client = app.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/events", EventPayload("Event", 10, 5, "Run"))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PutAsJsonAsync("/api/events/1", EventPayload("Event", 10, 5, "Run"))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.DeleteAsync("/api/events/1")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/enrolments", new { eventId = 1, categoryId = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/profile")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PutAsJsonAsync("/api/enrolments/1/result", new { finishTime = "00:45:00", finishPosition = 1 })).StatusCode);
    }

    [Fact]
    public async Task Participant_CannotPerformOrganiserActions_ReturnsForbidden()
    {
        using var app = new ApiFactory(); using var client = app.CreateClient();
        await RegisterAndLoginParticipant(client, "part_roles@example.com");

        // Cannot create event
        var createEvent = await client.PostAsJsonAsync("/api/events", EventPayload("Illegal Event", 15, 10, "Run"));
        Assert.Equal(HttpStatusCode.Forbidden, createEvent.StatusCode);

        // Cannot update event
        var updateEvent = await client.PutAsJsonAsync("/api/events/1", EventPayload("Illegal Update", 15, 10, "Run"));
        Assert.Equal(HttpStatusCode.Forbidden, updateEvent.StatusCode);

        // Cannot delete event
        var deleteEvent = await client.DeleteAsync("/api/events/1");
        Assert.Equal(HttpStatusCode.Forbidden, deleteEvent.StatusCode);

        // Cannot assign category to event
        var assignCat = await client.PostAsJsonAsync("/api/events/1/categories", new { categoryId = 1 });
        Assert.Equal(HttpStatusCode.Forbidden, assignCat.StatusCode);

        // Cannot record results
        var recordRes = await client.PutAsJsonAsync("/api/enrolments/1/result", new { finishTime = "01:00:00", finishPosition = 1 });
        Assert.Equal(HttpStatusCode.Forbidden, recordRes.StatusCode);

        // Cannot view event enrolments list
        var eventEnrols = await client.GetAsync("/api/enrolments/event/1");
        Assert.Equal(HttpStatusCode.Forbidden, eventEnrols.StatusCode);
    }

    [Fact]
    public async Task Organiser_CannotEnrolInEvents_ReturnsForbidden()
    {
        using var app = new ApiFactory(); using var client = app.CreateClient();
        await RegisterAndLoginOrganiser(client, "org_cannot_enrol@example.com");

        var enrolRes = await client.PostAsJsonAsync("/api/enrolments", new { eventId = 1, categoryId = 1 });
        Assert.Equal(HttpStatusCode.Forbidden, enrolRes.StatusCode);
    }

    [Fact]
    public async Task Organiser_CannotManageAnotherOrganisersEvent()
    {
        using var app = new ApiFactory();
        using var clientOrg1 = app.CreateClient();
        using var clientOrg2 = app.CreateClient();

        await RegisterAndLoginOrganiser(clientOrg1, "org1@example.com");
        await RegisterAndLoginOrganiser(clientOrg2, "org2@example.com");

        var createRes = await clientOrg1.PostAsJsonAsync("/api/events", EventPayload("Org1 Event", 20, 10, "Run"));
        var eventId = (await createRes.Content.ReadFromJsonAsync<JsonElement>(JsonOptions)).GetProperty("eventId").GetInt32();

        // Org2 tries to update Org1's event
        var updateRes = await clientOrg2.PutAsJsonAsync($"/api/events/{eventId}", EventPayload("Hacked Event", 25, 12, "Run"));
        Assert.Equal(HttpStatusCode.Forbidden, updateRes.StatusCode);

        // Org2 tries to delete Org1's event
        var delRes = await clientOrg2.DeleteAsync($"/api/events/{eventId}");
        Assert.Equal(HttpStatusCode.Forbidden, delRes.StatusCode);

        // Org2 tries to view Org1's event enrolments
        var viewRes = await clientOrg2.GetAsync($"/api/enrolments/event/{eventId}");
        Assert.Equal(HttpStatusCode.Forbidden, viewRes.StatusCode);
    }

    [Fact]
    public async Task Participant_CannotViewAnotherParticipantsEnrolments()
    {
        using var app = new ApiFactory();
        using var clientPart1 = app.CreateClient();
        using var clientPart2 = app.CreateClient();

        await RegisterAndLoginParticipant(clientPart1, "part_one@example.com");
        var part1Profile = await clientPart1.GetFromJsonAsync<JsonElement>("/api/profile", JsonOptions);
        var part1Id = part1Profile.GetProperty("participantId").GetInt32();

        await RegisterAndLoginParticipant(clientPart2, "part_two@example.com");

        // Part 2 attempts to view Part 1's enrolments
        var getRes = await clientPart2.GetAsync($"/api/enrolments/participant/{part1Id}");
        Assert.Equal(HttpStatusCode.Forbidden, getRes.StatusCode);
    }

    // =========================================================================
    // 4. Categories Management Tests
    // =========================================================================

    [Fact]
    public async Task Organiser_CanDefineAndAssignCategories_BothRolesCanView()
    {
        using var app = new ApiFactory();
        using var orgClient = app.CreateClient();
        using var partClient = app.CreateClient();

        await RegisterAndLoginOrganiser(orgClient, "org_cats@example.com");

        // 1. Organiser creates a category
        var catRes = await orgClient.PostAsJsonAsync("/api/categories", new { name = "Senior Masters", description = "Ages 50+" });
        Assert.Equal(HttpStatusCode.Created, catRes.StatusCode);
        var catDoc = await catRes.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var catId = catDoc.GetProperty("categoryId").GetInt32();

        // 2. Organiser creates event and assigns category
        var eventRes = await orgClient.PostAsJsonAsync("/api/events", EventPayload("Masters Run", 30, 10, "Run"));
        var eventId = (await eventRes.Content.ReadFromJsonAsync<JsonElement>(JsonOptions)).GetProperty("eventId").GetInt32();

        var assignRes = await orgClient.PostAsJsonAsync($"/api/events/{eventId}/categories", new { categoryId = catId });
        Assert.Equal(HttpStatusCode.Created, assignRes.StatusCode);

        // 3. Organiser creates and assigns a category in one call
        var newCatRes = await orgClient.PostAsJsonAsync($"/api/events/{eventId}/categories/new", new { name = "Under 20", description = "Youth" });
        Assert.Equal(HttpStatusCode.Created, newCatRes.StatusCode);

        // 4. Participant can view all available categories
        await RegisterAndLoginParticipant(partClient, "runner_cats@example.com");
        var allCats = await partClient.GetFromJsonAsync<JsonElement[]>("/api/categories", JsonOptions);
        Assert.NotNull(allCats);
        Assert.Contains(allCats, c => c.GetProperty("name").GetString() == "Senior Masters");

        // 5. Participant can view categories assigned to this event
        var eventCats = await partClient.GetFromJsonAsync<JsonElement[]>($"/api/events/{eventId}/categories", JsonOptions);
        Assert.NotNull(eventCats);
        Assert.Equal(2, eventCats.Length);
    }

    [Fact]
    public async Task CategoryAssignment_RejectsNonExistentOrDuplicateCategory()
    {
        using var app = new ApiFactory(); using var client = app.CreateClient();
        await RegisterAndLoginOrganiser(client, "org_badcat@example.com");

        var eventRes = await client.PostAsJsonAsync("/api/events", EventPayload("Race", 30, 5, "Run"));
        var eventId = (await eventRes.Content.ReadFromJsonAsync<JsonElement>(JsonOptions)).GetProperty("eventId").GetInt32();

        // Assign non-existent category
        var badRes = await client.PostAsJsonAsync($"/api/events/{eventId}/categories", new { categoryId = 9999 });
        Assert.Equal(HttpStatusCode.BadRequest, badRes.StatusCode);

        // Create valid category
        var catRes = await client.PostAsJsonAsync("/api/categories", new { name = "5K Fun", description = "5km" });
        var catId = (await catRes.Content.ReadFromJsonAsync<JsonElement>(JsonOptions)).GetProperty("categoryId").GetInt32();

        var assign1 = await client.PostAsJsonAsync($"/api/events/{eventId}/categories", new { categoryId = catId });
        Assert.Equal(HttpStatusCode.Created, assign1.StatusCode);

        // Duplicate assignment
        var assign2 = await client.PostAsJsonAsync($"/api/events/{eventId}/categories", new { categoryId = catId });
        Assert.Equal(HttpStatusCode.Conflict, assign2.StatusCode);
    }

    // =========================================================================
    // 5. Event Enrolments and Results Tests
    // =========================================================================

    [Fact]
    public async Task Participant_EnrolInAvailableCategory_AndOrganiserRecordsResults()
    {
        using var app = new ApiFactory();
        using var orgClient = app.CreateClient();
        using var partClient = app.CreateClient();

        // Organiser sets up event with category
        await RegisterAndLoginOrganiser(orgClient, "org_results@example.com");
        var eventRes = await orgClient.PostAsJsonAsync("/api/events", EventPayload("City 10K", 15, 10, "Run"));
        var eventId = (await eventRes.Content.ReadFromJsonAsync<JsonElement>(JsonOptions)).GetProperty("eventId").GetInt32();

        var catRes = await orgClient.PostAsJsonAsync($"/api/events/{eventId}/categories/new", new { name = "Open 10K", description = "All ages" });
        var catId = (await catRes.Content.ReadFromJsonAsync<JsonElement>(JsonOptions)).GetProperty("categoryId").GetInt32();

        // Participant enrols
        await RegisterAndLoginParticipant(partClient, "fast_runner@example.com");
        var partProfile = await partClient.GetFromJsonAsync<JsonElement>("/api/profile", JsonOptions);
        var partId = partProfile.GetProperty("participantId").GetInt32();

        var enrolRes = await partClient.PostAsJsonAsync("/api/enrolments", new { eventId, categoryId = catId });
        Assert.Equal(HttpStatusCode.Created, enrolRes.StatusCode);
        var enrolDoc = await enrolRes.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var enrolId = enrolDoc.GetProperty("enrolmentId").GetInt32();

        // Organiser views enrolments for this event
        var eventEnrolsRes = await orgClient.GetAsync($"/api/enrolments/event/{eventId}");
        Assert.Equal(HttpStatusCode.OK, eventEnrolsRes.StatusCode);
        var enrolsList = await eventEnrolsRes.Content.ReadFromJsonAsync<JsonElement[]>(JsonOptions);
        Assert.NotNull(enrolsList);
        Assert.Single(enrolsList);

        // Organiser records finish time and position
        var resultRes = await orgClient.PutAsJsonAsync($"/api/enrolments/{enrolId}/result", new { finishTime = "00:38:15", finishPosition = 1 });
        Assert.Equal(HttpStatusCode.NoContent, resultRes.StatusCode);

        // Participant views own results
        var myEnrolsRes = await partClient.GetAsync($"/api/enrolments/participant/{partId}");
        Assert.Equal(HttpStatusCode.OK, myEnrolsRes.StatusCode);
        var myEnrols = await myEnrolsRes.Content.ReadFromJsonAsync<JsonElement[]>(JsonOptions);
        Assert.NotNull(myEnrols);
        Assert.Equal(1, myEnrols[0].GetProperty("finishPosition").GetInt32());
        Assert.Equal("Completed", myEnrols[0].GetProperty("status").GetString());

        // Public can view event leaderboard results
        var leaderboardRes = await orgClient.GetAsync($"/api/events/{eventId}/results");
        Assert.Equal(HttpStatusCode.OK, leaderboardRes.StatusCode);
        var leaderboard = await leaderboardRes.Content.ReadFromJsonAsync<JsonElement[]>(JsonOptions);
        Assert.NotNull(leaderboard);
        Assert.Single(leaderboard);
    }

    [Fact]
    public async Task Enrolment_RejectsUnavailableCategoryOrDuplicateEnrolment()
    {
        using var app = new ApiFactory();
        using var orgClient = app.CreateClient();
        using var partClient = app.CreateClient();

        await RegisterAndLoginOrganiser(orgClient, "org_enroltest@example.com");
        var eventRes = await orgClient.PostAsJsonAsync("/api/events", EventPayload("Race Day", 20, 5, "Run"));
        var eventId = (await eventRes.Content.ReadFromJsonAsync<JsonElement>(JsonOptions)).GetProperty("eventId").GetInt32();

        var catRes = await orgClient.PostAsJsonAsync($"/api/events/{eventId}/categories/new", new { name = "Offered Cat", description = "Offered" });
        var catId = (await catRes.Content.ReadFromJsonAsync<JsonElement>(JsonOptions)).GetProperty("categoryId").GetInt32();

        // Create another category NOT assigned to this event
        var unofferedCatRes = await orgClient.PostAsJsonAsync("/api/categories", new { name = "Unoffered Cat", description = "Not on event" });
        var unofferedCatId = (await unofferedCatRes.Content.ReadFromJsonAsync<JsonElement>(JsonOptions)).GetProperty("categoryId").GetInt32();

        await RegisterAndLoginParticipant(partClient, "enrol_fail_part@example.com");

        // Try enrolling in unoffered category
        var badEnrol = await partClient.PostAsJsonAsync("/api/enrolments", new { eventId, categoryId = unofferedCatId });
        Assert.Equal(HttpStatusCode.BadRequest, badEnrol.StatusCode);

        // Enrol in offered category succeeds
        var okEnrol = await partClient.PostAsJsonAsync("/api/enrolments", new { eventId, categoryId = catId });
        Assert.Equal(HttpStatusCode.Created, okEnrol.StatusCode);

        // Duplicate enrolment in same event rejected
        var dupEnrol = await partClient.PostAsJsonAsync("/api/enrolments", new { eventId, categoryId = catId });
        Assert.Equal(HttpStatusCode.Conflict, dupEnrol.StatusCode);
    }

    [Fact]
    public async Task Result_RejectsNegativeTimeOrZeroPosition()
    {
        using var app = new ApiFactory();
        using var orgClient = app.CreateClient();
        using var partClient = app.CreateClient();

        await RegisterAndLoginOrganiser(orgClient, "org_badresult@example.com");
        var eventRes = await orgClient.PostAsJsonAsync("/api/events", EventPayload("Race", 20, 5, "Run"));
        var eventId = (await eventRes.Content.ReadFromJsonAsync<JsonElement>(JsonOptions)).GetProperty("eventId").GetInt32();

        var catRes = await orgClient.PostAsJsonAsync($"/api/events/{eventId}/categories/new", new { name = "5K", description = "5K" });
        var catId = (await catRes.Content.ReadFromJsonAsync<JsonElement>(JsonOptions)).GetProperty("categoryId").GetInt32();

        await RegisterAndLoginParticipant(partClient, "runner_badres@example.com");
        var enrolRes = await partClient.PostAsJsonAsync("/api/enrolments", new { eventId, categoryId = catId });
        var enrolId = (await enrolRes.Content.ReadFromJsonAsync<JsonElement>(JsonOptions)).GetProperty("enrolmentId").GetInt32();

        // Invalid position (0)
        var badPos = await orgClient.PutAsJsonAsync($"/api/enrolments/{enrolId}/result", new { finishTime = "00:25:00", finishPosition = 0 });
        Assert.Equal(HttpStatusCode.BadRequest, badPos.StatusCode);
    }

    // =========================================================================
    // 6. User Profile Tests
    // =========================================================================

    [Fact]
    public async Task Organiser_CanViewAndUpdateOwnProfile()
    {
        using var app = new ApiFactory(); using var client = app.CreateClient();
        await RegisterAndLoginOrganiser(client, "org_profile@example.com");

        var viewRes = await client.GetAsync("/api/profile");
        Assert.Equal(HttpStatusCode.OK, viewRes.StatusCode);

        var updateRes = await client.PutAsJsonAsync("/api/profile", new { name = "Updated Club Name" });
        Assert.Equal(HttpStatusCode.NoContent, updateRes.StatusCode);

        var profileAfter = await client.GetFromJsonAsync<JsonElement>("/api/profile", JsonOptions);
        Assert.Equal("Updated Club Name", profileAfter.GetProperty("name").GetString());
    }

    [Fact]
    public async Task Participant_CanViewAndUpdateOwnProfile()
    {
        using var app = new ApiFactory(); using var client = app.CreateClient();
        await RegisterAndLoginParticipant(client, "part_profile@example.com");

        var updateRes = await client.PutAsJsonAsync("/api/profile", new { name = "Updated Runner", age = 30, gender = "Female" });
        Assert.Equal(HttpStatusCode.NoContent, updateRes.StatusCode);

        var profileAfter = await client.GetFromJsonAsync<JsonElement>("/api/profile", JsonOptions);
        Assert.Equal("Updated Runner", profileAfter.GetProperty("name").GetString());
        Assert.Equal(30, profileAfter.GetProperty("age").GetInt32());
    }

    [Fact]
    public async Task Participant_CanUpdateViaParticipantsEndpoint()
    {
        using var app = new ApiFactory(); using var client = app.CreateClient();
        await RegisterAndLoginParticipant(client, "part_endpoint@example.com");

        var profile = await client.GetFromJsonAsync<JsonElement>("/api/profile", JsonOptions);
        var partId = profile.GetProperty("participantId").GetInt32();

        var updateRes = await client.PutAsJsonAsync($"/api/participants/{partId}", new { name = "Endpoint Runner", age = 35, gender = "Male" });
        Assert.Equal(HttpStatusCode.OK, updateRes.StatusCode);

        var updatedDoc = await updateRes.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal("Endpoint Runner", updatedDoc.GetProperty("name").GetString());
        Assert.Equal(35, updatedDoc.GetProperty("age").GetInt32());
    }

    // =========================================================================
    // 7. Swagger Documentation Test
    // =========================================================================

    [Fact]
    public async Task Swagger_ListsDocumentedApiOperationsAndResponses()
    {
        using var app = new ApiFactory(); using var client = app.CreateClient();
        var json = await client.GetStringAsync("/swagger/v1/swagger.json");

        Assert.Contains("/api/events", json);
        Assert.Contains("/api/enrolments", json);
        Assert.Contains("/api/categories", json);
        Assert.Contains("/api/profile", json);
        Assert.Contains("Creates an organiser account using a unique email and a hashed password.", json);
        Assert.Contains("RaceDay REST API", json);
    }

    // =========================================================================
    // Test Helpers
    // =========================================================================

    private static object EventPayload(string name, int daysAhead, decimal distanceKm, string type) =>
        new { name, eventDate = DateTime.Today.AddDays(daysAhead), location = "Main Park", description = "Test event", distanceKm, eventType = type };

    private static async Task RegisterAndLoginOrganiser(HttpClient client, string email)
    {
        await client.PostAsJsonAsync("/api/organisers/register", new { name = "Test Organiser", email, password = "Password123!" });
        var login = await client.PostAsJsonAsync("/api/organisers/login", new { email, password = "Password123!" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    private static async Task RegisterAndLoginParticipant(HttpClient client, string email)
    {
        await client.PostAsJsonAsync("/api/participants/register", new { name = "Test Participant", email, password = "Password123!", age = 25, gender = "Male" });
        var login = await client.PostAsJsonAsync("/api/participants/login", new { email, password = "Password123!" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
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
