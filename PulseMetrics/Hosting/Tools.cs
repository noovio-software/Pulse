using Microsoft.AspNetCore.Hosting.Server;

namespace PulseMetrics.Hosting;

/// <summary>Un outil monté sous son préfixe (« /alt »), ou à la racine pour la page d'accueil.</summary>
internal sealed record Tool(PathString Prefix, WebApplication App, EmbeddedServer Server);

/// <summary>
/// Les outils du processus. Chacun est une application ASP.NET Core complète (conteneur, configuration, middlewares,
/// workers) branchée sur un <see cref="EmbeddedServer"/> ; seul l'hôte écoute un port.
/// </summary>
internal sealed class Tools(string[] args) : IAsyncDisposable
{
    private readonly List<Tool> _tools = [];

    /// <summary>
    /// Constructeur d'application d'un outil : même nom d'application que l'exécutable (le manifeste des ressources
    /// statiques et les secrets utilisateur sont ceux de PulseMetrics), même racine et même configuration que l'hôte.
    /// </summary>
    public static WebApplicationBuilder CreateBuilder(string[] args, out EmbeddedServer server)
    {
        var builder = WebApplication.CreateBuilder(args);
        server = new EmbeddedServer();
        builder.Services.AddSingleton<IServer>(server);
        builder.Services.AddSingleton<IHostLifetime, EmbeddedLifetime>();
        return builder;
    }

    /// <summary>Construit un outil ; <paramref name="build"/> reçoit un constructeur déjà relié à l'hôte.</summary>
    public async Task<WebApplication> AddAsync(string prefix, Func<WebApplicationBuilder, Task<WebApplication>> build)
    {
        var app = await build(CreateBuilder(args, out var server));
        _tools.Add(new Tool(new PathString(prefix), app, server));
        return app;
    }

    public Task<WebApplication> AddAsync(string prefix, Func<WebApplicationBuilder, WebApplication> build) =>
        AddAsync(prefix, builder => Task.FromResult(build(builder)));

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        foreach (var tool in _tools)
        {
            await tool.App.StartAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Dernier maillon de l'hôte : « /front/specs » part vers Front avec PathBase = /front et Path = /specs ;
    /// tout ce qui ne correspond à aucun préfixe va à la page d'accueil, montée à la racine.
    /// </summary>
    public Task DispatchAsync(HttpContext context)
    {
        var request = context.Request;
        foreach (var tool in _tools.Where(t => t.Prefix.HasValue).OrderByDescending(t => t.Prefix.Value!.Length))
        {
            if (!request.Path.StartsWithSegments(tool.Prefix, StringComparison.OrdinalIgnoreCase, out var remaining))
            {
                continue;
            }

            // « /front » sans barre : Blazor exige que l'adresse soit sous sa base (« /front/ »).
            if (!remaining.HasValue)
            {
                context.Response.StatusCode = StatusCodes.Status308PermanentRedirect;
                context.Response.Headers.Location = request.PathBase.Add(tool.Prefix).Add("/") + request.QueryString;
                return Task.CompletedTask;
            }

            request.PathBase = request.PathBase.Add(tool.Prefix);
            request.Path = remaining;
            return tool.Server.ProcessAsync(context);
        }

        if (_tools.FirstOrDefault(t => !t.Prefix.HasValue) is { } root)
        {
            return root.Server.ProcessAsync(context);
        }

        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        foreach (var tool in Enumerable.Reverse(_tools))
        {
            await tool.App.StopAsync(cancellationToken);
        }
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var tool in Enumerable.Reverse(_tools))
        {
            await tool.App.DisposeAsync();
        }
        _tools.Clear();
    }
}
