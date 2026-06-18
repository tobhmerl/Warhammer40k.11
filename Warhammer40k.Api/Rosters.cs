using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Warhammer40k.Core.Rosters;
using Warhammer40k.Core.Rosters.Validation;

namespace Warhammer40k.Api;

/// <summary>
/// CRUD for the signed-in user's rosters under <c>/api/rosters</c>, plus a stateless
/// <c>POST /api/rosters/validate</c> that runs the shared rules engine (R1–R11). The owning user id is
/// always taken from the Static Web Apps principal header (never from the request body or route), so a user
/// can only ever read or modify their own data.
/// </summary>
public class Rosters(IRosterRepository repository, CatalogueProvider catalogue)
{
    [Function("ListRosters")]
    public async Task<IActionResult> List(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "rosters")] HttpRequest req,
        CancellationToken cancellationToken)
    {
        var userId = ClientPrincipalReader.GetUserId(req);
        if (userId is null)
            return new UnauthorizedResult();

        var rosters = await repository.ListAsync(userId, cancellationToken);
        return new OkObjectResult(rosters);
    }

    [Function("GetRoster")]
    public async Task<IActionResult> Get(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "rosters/{id}")] HttpRequest req,
        string id,
        CancellationToken cancellationToken)
    {
        var userId = ClientPrincipalReader.GetUserId(req);
        if (userId is null)
            return new UnauthorizedResult();

        var roster = await repository.GetAsync(userId, id, cancellationToken);
        return roster is null ? new NotFoundResult() : new OkObjectResult(roster);
    }

    [Function("CreateRoster")]
    public async Task<IActionResult> Create(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "rosters")] HttpRequest req,
        CancellationToken cancellationToken)
    {
        var userId = ClientPrincipalReader.GetUserId(req);
        if (userId is null)
            return new UnauthorizedResult();

        var roster = await ReadRosterAsync(req, cancellationToken);
        if (roster is null)
            return new BadRequestObjectResult("Roster requires a name and a faction.");

        roster.Id = string.Empty; // server assigns the id on create
        var saved = await repository.UpsertAsync(userId, roster, cancellationToken);
        return new CreatedResult($"/api/rosters/{saved.Id}", saved);
    }

    [Function("UpdateRoster")]
    public async Task<IActionResult> Update(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "rosters/{id}")] HttpRequest req,
        string id,
        CancellationToken cancellationToken)
    {
        var userId = ClientPrincipalReader.GetUserId(req);
        if (userId is null)
            return new UnauthorizedResult();

        var roster = await ReadRosterAsync(req, cancellationToken);
        if (roster is null)
            return new BadRequestObjectResult("Roster requires a name and a faction.");

        roster.Id = id; // the route id is authoritative
        var saved = await repository.UpsertAsync(userId, roster, cancellationToken);
        return new OkObjectResult(saved);
    }

    [Function("DeleteRoster")]
    public async Task<IActionResult> Delete(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "rosters/{id}")] HttpRequest req,
        string id,
        CancellationToken cancellationToken)
    {
        var userId = ClientPrincipalReader.GetUserId(req);
        if (userId is null)
            return new UnauthorizedResult();

        await repository.DeleteAsync(userId, id, cancellationToken);
        return new NoContentResult();
    }

    [Function("ValidateRoster")]
    public async Task<IActionResult> Validate(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "rosters/validate")] HttpRequest req,
        CancellationToken cancellationToken)
    {
        var userId = ClientPrincipalReader.GetUserId(req);
        if (userId is null)
            return new UnauthorizedResult();

        var roster = await ReadRosterBodyAsync(req, cancellationToken);
        if (roster is null)
            return new BadRequestObjectResult("A roster body is required.");

        // Auto-apply Pantheon bindings (rule R10) against the authoritative server catalogue, then validate.
        var detachment = DetachmentCatalogue.FindById(roster.DetachmentId);
        PantheonBindingApplier.Apply(roster, catalogue.Catalogue, detachment);

        var result = new RosterValidator().Validate(roster, catalogue.Catalogue);
        return new OkObjectResult(result);
    }

    private static async Task<Roster?> ReadRosterAsync(HttpRequest req, CancellationToken cancellationToken)
    {
        var roster = await ReadRosterBodyAsync(req, cancellationToken);
        if (roster is null || string.IsNullOrWhiteSpace(roster.Name) || string.IsNullOrWhiteSpace(roster.Faction))
            return null;
        return roster;
    }

    private static async Task<Roster?> ReadRosterBodyAsync(HttpRequest req, CancellationToken cancellationToken)
    {
        try
        {
            return await req.ReadFromJsonAsync<Roster>(cancellationToken);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
