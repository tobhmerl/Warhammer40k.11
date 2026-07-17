using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Warhammer40k.Core.Play;

namespace Warhammer40k.Api;

/// <summary>
/// The signed-in user's cross-roster scheduling library under <c>/api/schedule-library</c>: <c>GET</c>
/// returns their saved library (or an empty one when none), <c>PUT</c> saves it. Identity is taken from the
/// Static Web Apps principal header, so each account only ever sees its own defaults.
/// </summary>
public class ScheduleLibraryFunctions(IScheduleLibraryRepository repository)
{
    [Function("GetScheduleLibrary")]
    public async Task<IActionResult> Get(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "schedule-library")] HttpRequest req,
        CancellationToken cancellationToken)
    {
        var userId = ClientPrincipalReader.GetUserId(req);
        if (userId is null)
            return new OkObjectResult(ScheduleLibrary.Empty);

        var library = await repository.GetAsync(userId, cancellationToken) ?? ScheduleLibrary.Empty;
        return new OkObjectResult(library);
    }

    [Function("SaveScheduleLibrary")]
    public async Task<IActionResult> Save(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "schedule-library")] HttpRequest req,
        CancellationToken cancellationToken)
    {
        var userId = ClientPrincipalReader.GetUserId(req);
        if (userId is null)
            return new UnauthorizedResult();

        ScheduleLibrary? library;
        try
        {
            library = await req.ReadFromJsonAsync<ScheduleLibrary>(cancellationToken);
        }
        catch (JsonException)
        {
            return new BadRequestObjectResult("A valid schedule library is required.");
        }

        if (library is null)
            return new BadRequestObjectResult("A valid schedule library is required.");

        var saved = await repository.SaveAsync(userId, library, cancellationToken);
        return new OkObjectResult(saved);
    }
}
