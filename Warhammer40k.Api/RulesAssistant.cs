using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Warhammer40k.Api.Rules;
using Warhammer40k.Core.RulesAssistant;

namespace Warhammer40k.Api;

/// <summary>
/// Rules Assistant under <c>/api/rules/search</c> (removable feature — see docs/rules-assistant-REMOVE.md).
/// <c>POST</c> a question and get back the best-matching Core-Rules cards <b>verbatim</b> plus citations,
/// produced by the deterministic <see cref="RulesSearchEngine"/> — no language model is involved. SWA gates
/// <c>/api/*</c> to the authenticated role, so the function stays <see cref="AuthorizationLevel.Anonymous"/>.
/// </summary>
public class RulesAssistant(RulesProvider provider)
{
    [Function("SearchRules")]
    public async Task<IActionResult> Search(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "rules/search")] HttpRequest req,
        CancellationToken cancellationToken)
    {
        RulesQuery? body;
        try
        {
            body = await req.ReadFromJsonAsync<RulesQuery>(cancellationToken);
        }
        catch (JsonException)
        {
            return new BadRequestObjectResult("A valid query is required.");
        }

        var query = body?.Query ?? "";
        var max = body?.MaxMatches is > 0 and <= 20 ? body.MaxMatches : 6;

        var answer = RulesSearchEngine.Search(provider.Rules, query, max);
        return new OkObjectResult(answer);
    }
}

/// <summary>The request body for <c>POST /api/rules/search</c>.</summary>
public sealed class RulesQuery
{
    [JsonPropertyName("query")] public string Query { get; set; } = "";

    [JsonPropertyName("maxMatches")] public int MaxMatches { get; set; } = 6;
}
