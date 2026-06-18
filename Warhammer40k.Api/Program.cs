using Azure.Data.Tables;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Warhammer40k.Api;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

builder.Services
    .AddApplicationInsightsTelemetryWorkerService()
    .ConfigureFunctionsApplicationInsights();

// Persistence: Azure Table Storage. Locally this resolves to Azurite via "UseDevelopmentStorage=true"
// (see local.settings.json); in Azure set the "TablesConnectionString" app setting on the SWA resource.
var tablesConnectionString =
    builder.Configuration["TablesConnectionString"] ?? "UseDevelopmentStorage=true";

builder.Services.AddSingleton(new TableServiceClient(tablesConnectionString));
builder.Services.AddSingleton<IArmyRepository, TableArmyRepository>();
builder.Services.AddSingleton<IRosterRepository, TableRosterRepository>();
builder.Services.AddSingleton<ICatalogueRepository, TableCatalogueRepository>();
builder.Services.AddSingleton<ISettingsRepository, TableSettingsRepository>();

// Catalogue: read-only reference data parsed and enriched once from the embedded seed.
builder.Services.AddSingleton<CatalogueProvider>();

builder.Build().Run();
