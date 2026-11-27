using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Http.Features.Authentication;

namespace PulseMetrics.Hosting;

/// <summary>
/// Serveur sans socket d'un outil : l'hôte (seul à écouter, sur Kestrel) lui passe les requêtes de son préfixe.
/// L'outil garde son conteneur, son pipeline et ses services d'arrière-plan, comme s'il tournait seul.
/// </summary>
internal sealed class EmbeddedServer : IServer
{
    /// <summary>
    /// Fonctions posées par l'hôte ou par le pipeline de l'hôte : chaque outil crée les siennes. Repasser celles de l'hôte
    /// ferait résoudre les services de la requête dans le mauvais conteneur.
    /// </summary>
    private static readonly HashSet<Type> HostOwnedFeatures =
    [
        typeof(IServiceProvidersFeature),
        typeof(IItemsFeature),
        typeof(IEndpointFeature),
        typeof(IRouteValuesFeature),
        typeof(IHttpActivityFeature),
        typeof(IHttpMetricsTagsFeature),
        typeof(IHttpAuthenticationFeature),
    ];

    private Func<IFeatureCollection, Task>? _process;

    public IFeatureCollection Features { get; } = new FeatureCollection();

    public Task StartAsync<TContext>(IHttpApplication<TContext> application, CancellationToken cancellationToken)
        where TContext : notnull
    {
        _process = async features =>
        {
            var context = application.CreateContext(features);
            try
            {
                await application.ProcessRequestAsync(context);
                application.DisposeContext(context, null);
            }
            catch (Exception ex)
            {
                application.DisposeContext(context, ex);
                throw;
            }
        };
        return Task.CompletedTask;
    }

    /// <summary>Traite la requête reçue par l'hôte : mêmes flux et connexion (WebSocket du circuit Blazor compris), autre application.</summary>
    public Task ProcessAsync(HttpContext context)
    {
        if (_process is null)
        {
            throw new InvalidOperationException("L'outil n'est pas démarré.");
        }

        var features = new FeatureCollection();
        foreach (var (type, feature) in context.Features)
        {
            if (!HostOwnedFeatures.Contains(type))
            {
                features[type] = feature;
            }
        }
        return _process(features);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _process = null;
        return Task.CompletedTask;
    }

    public void Dispose()
    {
    }
}

/// <summary>Un outil ne réagit pas seul à Ctrl+C / SIGTERM : l'hôte l'arrête après avoir fermé ses connexions.</summary>
internal sealed class EmbeddedLifetime : IHostLifetime
{
    public Task WaitForStartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
