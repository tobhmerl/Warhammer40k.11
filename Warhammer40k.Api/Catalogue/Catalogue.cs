using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Warhammer40k.Core.Catalogue;

namespace Warhammer40k.Api;

/// <summary>
/// The Necron catalogue under <c>/api/catalogue</c>. <c>GET</c> returns the signed-in user's editable
/// catalogue (their saved copy, or the embedded default when they have none); <c>PUT</c> saves it and
/// <c>POST .../reset</c> reverts to the default (AB7). The Static Web Apps platform gates <c>/api/*</c> to the
/// authenticated role, so the functions stay <see cref="AuthorizationLevel.Anonymous"/>.
/// </summary>
public class Catalogue(CatalogueProvider provider, ICatalogueRepository repository)
{
    [Function("GetCatalogue")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "catalogue")] HttpRequest req,
        CancellationToken cancellationToken)
    {
        var userId = ClientPrincipalReader.GetUserId(req);
        var catalogue = userId is null
            ? provider.Catalogue
            : await repository.GetAsync(userId, cancellationToken) ?? provider.Catalogue;

        return new OkObjectResult(catalogue);
    }

    [Function("SaveCatalogue")]
    public async Task<IActionResult> Save(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "catalogue")] HttpRequest req,
        CancellationToken cancellationToken)
    {
        var userId = ClientPrincipalReader.GetUserId(req);
        if (userId is null)
            return new UnauthorizedResult();

        CatalogueData? catalogue;
        try
        {
            catalogue = await req.ReadFromJsonAsync<CatalogueData>(cancellationToken);
        }
        catch (JsonException)
        {
            return new BadRequestObjectResult("A valid catalogue document is required.");
        }

        if (catalogue is null || catalogue.Datasheets.Count == 0)
            return new BadRequestObjectResult("A catalogue with at least one datasheet is required.");

        var saved = await repository.SaveAsync(userId, catalogue, cancellationToken);
        return new OkObjectResult(saved);
    }

    [Function("ResetCatalogue")]
    public async Task<IActionResult> Reset(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "catalogue/reset")] HttpRequest req,
        CancellationToken cancellationToken)
    {
        var userId = ClientPrincipalReader.GetUserId(req);
        if (userId is null)
            return new UnauthorizedResult();

        await repository.ResetAsync(userId, cancellationToken);
        return new OkObjectResult(provider.Catalogue);
    }
}
