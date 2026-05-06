using Nocturne.Connectors.Core.Extensions;
using Nocturne.Connectors.Core.Models;
using Nocturne.Core.Constants;

namespace Nocturne.Connectors.Twiist.Configurations;

[ConnectorRegistration(
    "Twiist",
    ServiceNames.TwiistConnector,
    "TWIIST",
    "ConnectSource.Twiist",
    DataSources.TwiistConnector,
    "twiist",
    ConnectorCategory.Sync,
    "Connect to Twiist Insight follower data",
    "Twiist",
    SupportsHistoricalSync = false,
    MaxHistoricalDays = 0,
    SupportsManualSync = true,
    SupportedDataTypes = [
        SyncDataType.Glucose,
        SyncDataType.Boluses,
        SyncDataType.CarbIntake,
        SyncDataType.StateSpans,
        SyncDataType.DeviceEvents,
        SyncDataType.Notes,
        SyncDataType.DeviceStatus
    ]
)]
public class TwiistConnectorConfiguration : BaseConnectorConfiguration
{
    public TwiistConnectorConfiguration()
    {
        ConnectSource = ConnectSource.Twiist;
        SyncIntervalMinutes = 5;
    }

    [ConnectorProperty(ConnectorPropertyKey.Username, Required = true)]
    public string Username { get; set; } = string.Empty;

    [ConnectorProperty(ConnectorPropertyKey.Password, Required = true, Secret = true)]
    public string Password { get; set; } = string.Empty;

    [ConnectorProperty(ConnectorPropertyKey.PatientId)]
    public string PatientId { get; set; } = string.Empty;

    [ConnectorProperty(
        ConnectorPropertyKey.Server,
        Format = "uri",
        DefaultValue = TwiistConstants.DefaultFollowerServiceUrl)]
    public string Server { get; set; } = TwiistConstants.DefaultFollowerServiceUrl;

    public string FollowerServiceUrl =>
        string.IsNullOrWhiteSpace(Server)
            ? TwiistConstants.DefaultFollowerServiceUrl
            : Server.TrimEnd('/');
}
