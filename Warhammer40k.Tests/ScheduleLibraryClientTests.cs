using System.Net;
using System.Text;
using System.Text.Json;
using Warhammer40k.Core;
using Warhammer40k._11;

namespace Warhammer40k.Tests;

public class ScheduleLibraryClientTests
{
    [Theory]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.NotFound)]
    public async Task Failed_HTTP_load_is_not_returned_as_an_empty_library(HttpStatusCode status)
    {
        using var http = Client(status, "{}");
        var api = Api(http);
        await Assert.ThrowsAsync<HttpRequestException>(() => api.GetScheduleLibraryAsync());
    }

    [Theory]
    [InlineData("null")]
    [InlineData("<html>Not JSON</html>")]
    [InlineData("{\"schedules\":null}")]
    [InlineData("{\"schedules\":[null]}")]
    [InlineData("{\"schedules\":[{\"key\":\"\",\"windows\":[]}]}")]
    [InlineData("{\"schedules\":[{\"key\":\"strat|core|15.02\",\"windows\":null}]}")]
    [InlineData("{\"schedules\":[{\"key\":\"strat|core|15.02\",\"windows\":[null]}]}")]
    public async Task Missing_or_invalid_schedule_data_is_not_accepted(string json)
    {
        using var http = Client(HttpStatusCode.OK, json);
        await Assert.ThrowsAsync<JsonException>(() => Api(http).GetScheduleLibraryAsync());
    }

    [Fact]
    public async Task A_successfully_loaded_empty_library_is_valid()
    {
        using var http = Client(HttpStatusCode.OK, "{\"schedules\":[]}");
        Assert.Empty((await Api(http).GetScheduleLibraryAsync()).Schedules);
    }

    private static IApiClient Api(HttpClient http) =>
        (IApiClient)Activator.CreateInstance(typeof(SettingsState).Assembly.GetType("Warhammer40k._11.ApiClient", throwOnError: true)!, http)!;

    private static HttpClient Client(HttpStatusCode status, string json) =>
        new(new ResponseHandler(status, json)) { BaseAddress = new Uri("http://localhost/") };

    private sealed class ResponseHandler(HttpStatusCode status, string json) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") });
    }
}
