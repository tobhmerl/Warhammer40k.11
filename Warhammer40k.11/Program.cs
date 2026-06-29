using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Warhammer40k._11;
using Warhammer40k._11.Features.CombatSimulator;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddWarhammer40k();
builder.Services.AddCombatSimulator(); // CombatSimulator feature (removable — see Features/CombatSimulator/DELETE.md)

await builder.Build().RunAsync();
