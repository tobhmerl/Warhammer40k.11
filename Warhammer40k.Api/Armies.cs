using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Warhammer40k.Core;

namespace Warhammer40k.Api;

/// <summary>
/// CRUD for the signed-in user's armies under <c>/api/armies</c>. The owning user id is always taken
/// from the Static Web Apps principal header (never from the request body or route), so a user can
/// only ever read or modify their own data.
/// </summary>
public class Armies(IArmyRepository repository)
{
    [Function("ListArmies")]
    public async Task<IActionResult> List(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "armies")] HttpRequest req,
        CancellationToken cancellationToken)
    {
        var userId = ClientPrincipalReader.GetUserId(req);
        if (userId is null)
            return new UnauthorizedResult();

        var armies = await repository.ListAsync(userId, cancellationToken);
        return new OkObjectResult(armies);
    }

    [Function("GetArmy")]
    public async Task<IActionResult> Get(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "armies/{id}")] HttpRequest req,
        string id,
        CancellationToken cancellationToken)
    {
        var userId = ClientPrincipalReader.GetUserId(req);
        if (userId is null)
            return new UnauthorizedResult();

        var army = await repository.GetAsync(userId, id, cancellationToken);
        return army is null ? new NotFoundResult() : new OkObjectResult(army);
    }

    [Function("CreateArmy")]
    public async Task<IActionResult> Create(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "armies")] HttpRequest req,
        CancellationToken cancellationToken)
    {
        var userId = ClientPrincipalReader.GetUserId(req);
        if (userId is null)
            return new UnauthorizedResult();

        var army = await ReadArmyAsync(req, cancellationToken);
        if (army is null)
            return new BadRequestObjectResult("Army requires a name and a faction.");

        army.Id = string.Empty; // server assigns the id on create
        var saved = await repository.UpsertAsync(userId, army, cancellationToken);
        return new CreatedResult($"/api/armies/{saved.Id}", saved);
    }

    [Function("UpdateArmy")]
    public async Task<IActionResult> Update(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "armies/{id}")] HttpRequest req,
        string id,
        CancellationToken cancellationToken)
    {
        var userId = ClientPrincipalReader.GetUserId(req);
        if (userId is null)
            return new UnauthorizedResult();

        var army = await ReadArmyAsync(req, cancellationToken);
        if (army is null)
            return new BadRequestObjectResult("Army requires a name and a faction.");

        army.Id = id; // the route id is authoritative
        var saved = await repository.UpsertAsync(userId, army, cancellationToken);
        return new OkObjectResult(saved);
    }

    [Function("DeleteArmy")]
    public async Task<IActionResult> Delete(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "armies/{id}")] HttpRequest req,
        string id,
        CancellationToken cancellationToken)
    {
        var userId = ClientPrincipalReader.GetUserId(req);
        if (userId is null)
            return new UnauthorizedResult();

        await repository.DeleteAsync(userId, id, cancellationToken);
        return new NoContentResult();
    }

    private static async Task<Army?> ReadArmyAsync(HttpRequest req, CancellationToken cancellationToken)
    {
        try
        {
            var army = await req.ReadFromJsonAsync<Army>(cancellationToken);
            if (army is null || string.IsNullOrWhiteSpace(army.Name) || string.IsNullOrWhiteSpace(army.Faction))
                return null;
            return army;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
