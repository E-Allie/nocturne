using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nocturne.Connectors.Core.Extensions;
using Nocturne.Connectors.Core.Interfaces;
using Nocturne.Connectors.Core.Services;
using Nocturne.Connectors.Twiist.Configurations;
using Nocturne.Connectors.Twiist.Services;

namespace Nocturne.Connectors.Twiist;

public class TwiistConnectorInstaller : IConnectorInstaller
{
    public string ConnectorName => "Twiist";

    public void Install(IServiceCollection services, IConfiguration configuration)
    {
        var config = services.AddConnectorConfiguration<TwiistConnectorConfiguration>(
            configuration,
            "Twiist"
        );

        if (!config.Enabled)
            return;

        services.AddHttpClient<TwiistCognitoTokenProvider>();
        services.AddHttpClient<TwiistFollowerClient>();
        services.AddHttpClient<TwiistConnectorService>();

        services.AddSingleton(sp =>
        {
            var factory = sp.GetRequiredService<IHttpClientFactory>();
            var httpClient = factory.CreateClient(nameof(TwiistCognitoTokenProvider));
            var options = sp.GetRequiredService<IOptions<TwiistConnectorConfiguration>>();
            var logger = sp.GetRequiredService<ILogger<TwiistCognitoTokenProvider>>();
            return new TwiistCognitoTokenProvider(httpClient, options, logger);
        });

        services.AddSingleton(sp =>
        {
            var factory = sp.GetRequiredService<IHttpClientFactory>();
            var httpClient = factory.CreateClient(nameof(TwiistFollowerClient));
            var options = sp.GetRequiredService<IOptions<TwiistConnectorConfiguration>>();
            var tokenProvider = sp.GetRequiredService<TwiistCognitoTokenProvider>();
            var logger = sp.GetRequiredService<ILogger<TwiistFollowerClient>>();
            return new TwiistFollowerClient(httpClient, options, tokenProvider, logger);
        });

        services.AddScoped<IConnectorSyncExecutor, TwiistSyncExecutor>();
    }
}

public class TwiistSyncExecutor
    : ConnectorSyncExecutor<TwiistConnectorService, TwiistConnectorConfiguration>
{
    public override string ConnectorId => "twiist";

    protected override string ConnectorName => "Twiist";
}
