using Nocturne.Connectors.Core.Interfaces;
using Nocturne.Connectors.Core.Models;
using Nocturne.Connectors.Twiist.Configurations;
using Nocturne.Connectors.Twiist.Services;

namespace Nocturne.API.Services.BackgroundServices;

/// <summary>
/// Background service that periodically syncs Twiist Insight follower data.
/// </summary>
public class TwiistConnectorBackgroundService : ConnectorBackgroundService<TwiistConnectorConfiguration>
{
    public TwiistConnectorBackgroundService(
        IServiceProvider serviceProvider,
        TwiistConnectorConfiguration config,
        ILogger<TwiistConnectorBackgroundService> logger)
        : base(serviceProvider, config, logger)
    {
    }

    protected override string ConnectorName => "Twiist";

    protected override async Task<SyncResult> PerformSyncAsync(
        IServiceProvider scopeProvider,
        CancellationToken cancellationToken,
        ISyncProgressReporter? progressReporter = null)
    {
        var connectorService = scopeProvider.GetRequiredService<TwiistConnectorService>();
        return await connectorService.SyncDataAsync(
            Config,
            cancellationToken,
            since: null,
            progressReporter);
    }
}
