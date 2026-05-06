using System.Reflection;
using FluentAssertions;
using Nocturne.Connectors.Core.Extensions;
using Nocturne.Connectors.Core.Models;
using Nocturne.Connectors.Core.Services;
using Nocturne.Connectors.Twiist.Configurations;
using Nocturne.Core.Constants;
using Xunit;

namespace Nocturne.Connectors.Twiist.Tests.Configurations;

public class TwiistConnectorConfigurationTests
{
    [Fact]
    public void Registration_MetadataIsDiscoverable()
    {
        _ = typeof(TwiistConnectorConfiguration);

        var metadata = ConnectorMetadataService.GetByConnectorId("twiist");

        metadata.Should().NotBeNull();
        metadata!.ConnectorName.Should().Be("Twiist");
        metadata.DataSourceId.Should().Be(DataSources.TwiistConnector);
        metadata.Category.Should().Be(ConnectorCategory.Sync);
    }

    [Fact]
    public void Registration_DeclaresExpectedSupportedDataTypes()
    {
        var registration = typeof(TwiistConnectorConfiguration)
            .GetCustomAttribute<ConnectorRegistrationAttribute>();

        registration.Should().NotBeNull();
        registration!.SupportedDataTypes.Should().BeEquivalentTo([
            SyncDataType.Glucose,
            SyncDataType.Boluses,
            SyncDataType.CarbIntake,
            SyncDataType.StateSpans,
            SyncDataType.DeviceEvents,
            SyncDataType.Notes,
            SyncDataType.DeviceStatus
        ]);
    }

    [Fact]
    public void ConnectorProperties_IncludeCredentialsPatientServerAndSupportedSyncToggles()
    {
        var properties = typeof(TwiistConnectorConfiguration)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Concat(typeof(BaseConnectorConfiguration).GetProperties(BindingFlags.Public | BindingFlags.Instance))
            .Select(p => new
            {
                Property = p,
                Attribute = p.GetCustomAttribute<ConnectorPropertyAttribute>()
            })
            .Where(x => x.Attribute != null)
            .ToList();

        properties.Should().Contain(x => x.Attribute!.Key == ConnectorPropertyKey.Username && x.Attribute.Required);
        properties.Should().Contain(x => x.Attribute!.Key == ConnectorPropertyKey.Password && x.Attribute.Required && x.Attribute.Secret);
        properties.Should().Contain(x => x.Attribute!.Key == ConnectorPropertyKey.PatientId);
        properties.Should().Contain(x => x.Attribute!.Key == ConnectorPropertyKey.Server
            && x.Property.GetValue(new TwiistConnectorConfiguration())!.Equals(TwiistConstants.DefaultFollowerServiceUrl));

        properties.Select(x => x.Attribute!.Key).Should().Contain([
            ConnectorPropertyKey.SyncGlucose,
            ConnectorPropertyKey.SyncBoluses,
            ConnectorPropertyKey.SyncCarbIntake,
            ConnectorPropertyKey.SyncStateSpans,
            ConnectorPropertyKey.SyncDeviceEvents,
            ConnectorPropertyKey.SyncNotes,
            ConnectorPropertyKey.SyncDeviceStatus
        ]);
    }
}
