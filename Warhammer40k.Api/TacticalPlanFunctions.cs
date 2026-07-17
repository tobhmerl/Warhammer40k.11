using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Warhammer40k.Core.Tactical;

namespace Warhammer40k.Api;

/// <summary>
/// CRUD for the signed-in user's tactical plans under <c>/api/tactical-plans</c>. The owning user id is
/// always taken from the Static Web Apps principal header (never the body or route), so a user can only
/// ever read or modify their own plans.
/// </summary>
public class TacticalPlans(ITacticalPlanRepository repository)
{
    [Function("ListTacticalPlans")]
    public async Task<IActionResult> List(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "tactical-plans")] HttpRequest req,
        CancellationToken cancellationToken)
    {
        var userId = ClientPrincipalReader.GetUserId(req);
        if (userId is null)
            return new UnauthorizedResult();

        var plans = await repository.ListAsync(userId, cancellationToken);
        return new OkObjectResult(plans);
    }

    [Function("GetTacticalPlan")]
    public async Task<IActionResult> Get(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "tactical-plans/{id}")] HttpRequest req,
        string id,
        CancellationToken cancellationToken)
    {
        var userId = ClientPrincipalReader.GetUserId(req);
        if (userId is null)
            return new UnauthorizedResult();

        var plan = await repository.GetAsync(userId, id, cancellationToken);
        return plan is null ? new NotFoundResult() : new OkObjectResult(plan);
    }

    [Function("CreateTacticalPlan")]
    public async Task<IActionResult> Create(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "tactical-plans")] HttpRequest req,
        CancellationToken cancellationToken)
    {
        var userId = ClientPrincipalReader.GetUserId(req);
        if (userId is null)
            return new UnauthorizedResult();

        var plan = await ReadPlanAsync(req, cancellationToken);
        if (plan is null)
            return new BadRequestObjectResult("A tactical plan body is required.");

        plan.Id = string.Empty; // server assigns the id on create
        var saved = await repository.UpsertAsync(userId, plan, cancellationToken);
        return new CreatedResult($"/api/tactical-plans/{saved.Id}", saved);
    }

    [Function("UpdateTacticalPlan")]
    public async Task<IActionResult> Update(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "tactical-plans/{id}")] HttpRequest req,
        string id,
        CancellationToken cancellationToken)
    {
        var userId = ClientPrincipalReader.GetUserId(req);
        if (userId is null)
            return new UnauthorizedResult();

        var plan = await ReadPlanAsync(req, cancellationToken);
        if (plan is null)
            return new BadRequestObjectResult("A tactical plan body is required.");

        plan.Id = id; // the route id is authoritative
        var saved = await repository.UpsertAsync(userId, plan, cancellationToken);
        return new OkObjectResult(saved);
    }

    [Function("DeleteTacticalPlan")]
    public async Task<IActionResult> Delete(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "tactical-plans/{id}")] HttpRequest req,
        string id,
        CancellationToken cancellationToken)
    {
        var userId = ClientPrincipalReader.GetUserId(req);
        if (userId is null)
            return new UnauthorizedResult();

        await repository.DeleteAsync(userId, id, cancellationToken);
        return new NoContentResult();
    }

    private static async Task<TacticalPlan?> ReadPlanAsync(HttpRequest req, CancellationToken cancellationToken)
    {
        try
        {
            return await req.ReadFromJsonAsync<TacticalPlan>(cancellationToken);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
