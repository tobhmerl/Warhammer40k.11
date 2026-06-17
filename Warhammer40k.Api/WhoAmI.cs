using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Warhammer40k.Core;

namespace Warhammer40k.Api;

/// <summary>
/// <c>GET /api/whoami</c> - returns the signed-in user by decoding the
/// <c>x-ms-client-principal</c> header that Azure Static Web Apps injects after login.
/// Returns <see cref="UserInfo.Anonymous"/> when no one is authenticated.
/// </summary>
public class WhoAmI
{
    [Function("WhoAmI")]
    public IActionResult Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "whoami")] HttpRequest req)
        => new OkObjectResult(ClientPrincipalReader.ResolveUser(req));
}
