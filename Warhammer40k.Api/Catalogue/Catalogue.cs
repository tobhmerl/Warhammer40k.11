using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Warhammer40k.Api;

/// <summary>
/// <c>GET /api/catalogue</c> - returns the enriched Necron catalogue (datasheets + Pantheon bindings)
/// from the embedded seed. Read-only reference data; the Static Web Apps platform gates <c>/api/*</c>
/// to the <c>owner</c> role, so the function itself is <see cref="AuthorizationLevel.Anonymous"/>.
/// </summary>
public class Catalogue(CatalogueProvider provider)
{
    [Function("GetCatalogue")]
    public IActionResult Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "catalogue")] HttpRequest req)
        => new OkObjectResult(provider.Catalogue);
}
