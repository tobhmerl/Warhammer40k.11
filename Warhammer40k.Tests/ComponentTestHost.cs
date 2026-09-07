using System.Net;
using System.Reflection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.Web.HtmlRendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Warhammer40k.Core;
using Warhammer40k.Core.Catalogue;
using Warhammer40k.Core.Play;
using Warhammer40k.Core.Rosters;
using Warhammer40k.Core.Rosters.Validation;
using Warhammer40k.Core.RulesAssistant;
using Warhammer40k.Core.Tactical;
using Warhammer40k._11;

namespace Warhammer40k.Tests;

internal sealed class ComponentTestHost<T> : IAsyncDisposable where T : ComponentBase
{
    private const BindingFlags Members = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    private readonly ServiceProvider _services;
    private readonly HtmlRenderer _renderer;
    private readonly CapturingActivator _activator;
    private HtmlRootComponent _root;

    private ComponentTestHost(IApiClient api, string path)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(api);
        services.AddSingleton<IJSRuntime, TestJsRuntime>();
        services.AddSingleton<NavigationManager>(new TestNavigation(path));
        services.AddScoped<SettingsState>();
        services.AddSingleton<CapturingActivator>();
        services.AddSingleton<IComponentActivator>(provider => provider.GetRequiredService<CapturingActivator>());
        _services = services.BuildServiceProvider();
        _activator = _services.GetRequiredService<CapturingActivator>();
        _renderer = new HtmlRenderer(_services, _services.GetRequiredService<ILoggerFactory>());
    }

    public T Component => _activator.Component ?? throw new InvalidOperationException("Component has not rendered.");

    public static async Task<ComponentTestHost<T>> CreateAsync(IApiClient api, string path, Dictionary<string, object?>? parameters = null)
    {
        var host = new ComponentTestHost<T>(api, path);
        try
        {
            host._root = await host._renderer.Dispatcher.InvokeAsync(() =>
                host._renderer.RenderComponentAsync<T>(ParameterView.FromDictionary(parameters ?? [])));
            return host;
        }
        catch
        {
            await host.DisposeAsync();
            throw;
        }
    }

    public Task<string> HtmlAsync() =>
        _renderer.Dispatcher.InvokeAsync(() => WebUtility.HtmlDecode(_root.ToHtmlString()));

    public TValue Read<TValue>(string name) => (TValue)(typeof(T).GetField(name, Members)?.GetValue(Component)
        ?? typeof(T).GetProperty(name, Members)?.GetValue(Component))!;

    public Task SetAsync(string name, object? value) => _renderer.Dispatcher.InvokeAsync(() =>
    {
        (typeof(T).GetField(name, Members) ?? throw new MissingFieldException(name)).SetValue(Component, value);
        Render();
    });

    public Task InvokeAsync(string method, params object?[] arguments) => _renderer.Dispatcher.InvokeAsync(async () =>
    {
        var result = (typeof(T).GetMethod(method, Members) ?? throw new MissingMethodException(method)).Invoke(Component, arguments);
        if (result is Task task)
            await task;
        Render();
    });

    private void Render() => typeof(ComponentBase).GetMethod("StateHasChanged", Members)!.Invoke(Component, null);

    public async ValueTask DisposeAsync()
    {
        await _renderer.DisposeAsync();
        await _services.DisposeAsync();
    }

    private sealed class CapturingActivator(IServiceProvider services) : IComponentActivator
    {
        public T? Component { get; private set; }

        public IComponent CreateInstance(Type componentType)
        {
            var component = (IComponent)ActivatorUtilities.CreateInstance(services, componentType);
            if (component is T root)
                Component = root;
            return component;
        }
    }

    private sealed class TestNavigation : NavigationManager
    {
        public TestNavigation(string path) => Initialize("http://localhost/", "http://localhost/" + path.TrimStart('/'));
        protected override void NavigateToCore(string uri, bool forceLoad) => Uri = ToAbsoluteUri(uri).ToString();
    }

    private sealed class TestJsRuntime : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => ValueTask.FromResult(default(TValue)!);
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) => ValueTask.FromResult(default(TValue)!);
    }
}

internal sealed class TestApiClient : IApiClient
{
    public CatalogueData Catalogue { get; set; } = new();
    public Roster Roster { get; set; } = new() { Id = "test-roster", Name = "Test army" };
    public ScheduleLibrary Library { get; set; } = new();
    public UserSettings Settings { get; set; } = new() { PlayCardSwipe = true, PlayCompactRules = true };
    public Func<Task<ScheduleLibrary>>? LoadLibrary { get; set; }
    public Func<ScheduleLibrary, Task<ScheduleLibrary>>? SaveLibrary { get; set; }
    public int LibrarySaveCount { get; private set; }

    public Task<UserInfo> GetCurrentUserAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new UserInfo(true, "test-user", "Test player", "github", ["authenticated"]));
    public Task<CatalogueData> GetCatalogueAsync(CancellationToken cancellationToken = default) => Task.FromResult(Catalogue);
    public Task<UserSettings> GetSettingsAsync(CancellationToken cancellationToken = default) => Task.FromResult(Settings);
    public Task<Roster?> GetRosterAsync(string id, CancellationToken cancellationToken = default) => Task.FromResult<Roster?>(Roster);
    public Task<IReadOnlyList<Roster>> GetRostersAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Roster>>([Roster]);
    public Task<Roster> SaveRosterAsync(Roster roster, CancellationToken cancellationToken = default) => Task.FromResult(Roster = roster);
    public Task<ScheduleLibrary> GetScheduleLibraryAsync(CancellationToken cancellationToken = default) => LoadLibrary?.Invoke() ?? Task.FromResult(Library);
    public Task<ScheduleLibrary> SaveScheduleLibraryAsync(ScheduleLibrary library, CancellationToken cancellationToken = default)
    {
        LibrarySaveCount++;
        return SaveLibrary?.Invoke(library) ?? Task.FromResult(Library = library);
    }

    public Task<CatalogueData> SaveCatalogueAsync(CatalogueData catalogue, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<CatalogueData> ResetCatalogueAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<IReadOnlyList<Army>> GetArmiesAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<Army?> GetArmyAsync(string id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<Army> SaveArmyAsync(Army army, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task DeleteArmyAsync(string id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task DeleteRosterAsync(string id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<ValidationResult?> ValidateRosterAsync(Roster roster, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<UserSettings> SaveSettingsAsync(UserSettings settings, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<IReadOnlyList<TacticalPlan>> GetTacticalPlansAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<TacticalPlan?> GetTacticalPlanAsync(string id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<TacticalPlan> SaveTacticalPlanAsync(TacticalPlan plan, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task DeleteTacticalPlanAsync(string id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<BackupBundle?> GetBackupAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task RestoreBackupAsync(BackupBundle bundle, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<RulesAnswer?> SearchRulesAsync(string query, CancellationToken cancellationToken = default) => throw new NotSupportedException();
}
