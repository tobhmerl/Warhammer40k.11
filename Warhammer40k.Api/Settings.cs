using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Warhammer40k.Core;

namespace Warhammer40k.Api;

/// <summary>
/// The signed-in user's app settings under <c>/api/settings</c> (AB8): <c>GET</c> returns their saved settings
/// (or defaults when none), <c>PUT</c> saves them. Identity is taken from the Static Web Apps principal header.
/// </summary>
public class Settings(ISettingsRepository repository)
{
    [Function("GetSettings")]
    public async Task<IActionResult> Get(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "settings")] HttpRequest req,
        CancellationToken cancellationToken)
    {
        var userId = ClientPrincipalReader.GetUserId(req);
        if (userId is null)
            return new OkObjectResult(UserSettings.Default);

        var settings = await repository.GetAsync(userId, cancellationToken) ?? UserSettings.Default;
        return new OkObjectResult(settings);
    }

    [Function("SaveSettings")]
    public async Task<IActionResult> Save(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "settings")] HttpRequest req,
        CancellationToken cancellationToken)
    {
        var userId = ClientPrincipalReader.GetUserId(req);
        if (userId is null)
            return new UnauthorizedResult();

        UserSettings? settings;
        try
        {
            settings = await req.ReadFromJsonAsync<UserSettings>(cancellationToken);
        }
        catch (JsonException)
        {
            return new BadRequestObjectResult("Valid settings are required.");
        }

        if (settings is null)
            return new BadRequestObjectResult("Valid settings are required.");

        var saved = await repository.SaveAsync(userId, settings, cancellationToken);
        return new OkObjectResult(saved);
    }
}
