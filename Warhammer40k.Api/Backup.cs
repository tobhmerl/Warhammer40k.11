using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Warhammer40k.Core;

namespace Warhammer40k.Api;

/// <summary>
/// Backup/restore of the signed-in user's data (AB8). <c>GET /api/backup</c> assembles a portable
/// <see cref="BackupBundle"/> (settings + customized catalogue + rosters); <c>POST /api/restore</c> replaces
/// the user's data from a bundle. Composes the per-user repositories so every operation stays partitioned.
/// </summary>
public class Backup(
    ISettingsRepository settings,
    ICatalogueRepository catalogue,
    IScheduleLibraryRepository scheduleLibrary,
    IRosterRepository rosters)
{
    [Function("GetBackup")]
    public async Task<IActionResult> Get(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "backup")] HttpRequest req,
        CancellationToken cancellationToken)
    {
        var userId = ClientPrincipalReader.GetUserId(req);
        if (userId is null)
            return new UnauthorizedResult();

        var bundle = new BackupBundle
        {
            CreatedUtc = DateTimeOffset.UtcNow,
            Settings = await settings.GetAsync(userId, cancellationToken) ?? UserSettings.Default,
            Catalogue = await catalogue.GetAsync(userId, cancellationToken), // null when on the default
            ScheduleLibrary = await scheduleLibrary.GetAsync(userId, cancellationToken),
            Rosters = (await rosters.ListAsync(userId, cancellationToken)).ToList(),
        };

        return new OkObjectResult(bundle);
    }

    [Function("RestoreBackup")]
    public async Task<IActionResult> Restore(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "restore")] HttpRequest req,
        CancellationToken cancellationToken)
    {
        var userId = ClientPrincipalReader.GetUserId(req);
        if (userId is null)
            return new UnauthorizedResult();

        BackupBundle? bundle;
        try
        {
            bundle = await req.ReadFromJsonAsync<BackupBundle>(cancellationToken);
        }
        catch (JsonException)
        {
            return new BadRequestObjectResult("A valid backup bundle is required.");
        }

        if (bundle is null)
            return new BadRequestObjectResult("A valid backup bundle is required.");

        await settings.SaveAsync(userId, bundle.Settings ?? UserSettings.Default, cancellationToken);

        if (bundle.Catalogue is { Datasheets.Count: > 0 } customCatalogue)
            await catalogue.SaveAsync(userId, customCatalogue, cancellationToken);
        else
            await catalogue.ResetAsync(userId, cancellationToken);

        if (bundle.ScheduleLibrary is { } library)
            await scheduleLibrary.SaveAsync(userId, library, cancellationToken);

        // Replace rosters: clear the user's current set, then recreate the bundle's with fresh server ids.
        foreach (var existing in await rosters.ListAsync(userId, cancellationToken))
            await rosters.DeleteAsync(userId, existing.Id, cancellationToken);

        foreach (var roster in bundle.Rosters)
        {
            roster.Id = string.Empty;
            await rosters.UpsertAsync(userId, roster, cancellationToken);
        }

        return new NoContentResult();
    }
}
