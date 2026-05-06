using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nocturne.Connectors.Core.Interfaces;
using Nocturne.Connectors.Core.Models;
using Nocturne.Connectors.Core.Services;
using Nocturne.Connectors.Twiist.Configurations;
using Nocturne.Connectors.Twiist.Mappers;
using Nocturne.Core.Constants;
using Nocturne.Core.Models;

namespace Nocturne.Connectors.Twiist.Services;

public sealed class TwiistConnectorService(
    HttpClient httpClient,
    IOptions<TwiistConnectorConfiguration> config,
    ILogger<TwiistConnectorService> logger,
    TwiistCognitoTokenProvider tokenProvider,
    TwiistFollowerClient followerClient,
    IConnectorPublisher? publisher = null)
    : BaseConnectorService<TwiistConnectorConfiguration>(httpClient, logger, publisher)
{
    private readonly TwiistConnectorConfiguration _config = config.Value;

    public override string ServiceName => "Twiist";
    protected override string ConnectorSource => DataSources.TwiistConnector;

    public override List<SyncDataType> SupportedDataTypes =>
    [
        SyncDataType.Glucose,
        SyncDataType.Boluses,
        SyncDataType.CarbIntake,
        SyncDataType.StateSpans,
        SyncDataType.DeviceEvents,
        SyncDataType.Notes,
        SyncDataType.DeviceStatus
    ];

    public override bool IsHealthy =>
        FailedRequestCount < MaxFailedRequestsBeforeUnhealthy && !tokenProvider.IsTokenExpired;

    public override async Task<bool> AuthenticateAsync()
    {
        try
        {
            _config.Validate();
            var token = await tokenProvider.GetAccessTokenAsync();
            if (string.IsNullOrWhiteSpace(token))
            {
                TrackFailedRequest("Token missing");
                return false;
            }

            TrackSuccessfulRequest();
            return true;
        }
        catch (Exception ex)
        {
            TrackFailedRequest(ex.Message);
            _logger.LogWarning(ex, "Twiist authentication failed.");
            return false;
        }
    }

    public override Task<IEnumerable<Entry>> FetchGlucoseDataAsync(DateTime? since = null)
    {
        return Task.FromResult(Enumerable.Empty<Entry>());
    }

    protected override async Task<SyncResult> PerformSyncInternalAsync(
        SyncRequest request,
        TwiistConnectorConfiguration config,
        CancellationToken cancellationToken,
        ISyncProgressReporter? progressReporter = null)
    {
        var result = new SyncResult { StartTime = DateTimeOffset.UtcNow, Success = true };

        if (!request.DataTypes.Any())
            request.DataTypes = SupportedDataTypes;

        var enabledTypes = config.GetEnabledDataTypes(SupportedDataTypes);
        var activeTypes = request.DataTypes.Where(enabledTypes.Contains).ToHashSet();

        try
        {
            if (!await AuthenticateAsync())
            {
                result.Success = false;
                result.Errors.Add("Twiist authentication failed");
                return Finish(result);
            }

            var overviews = await followerClient.GetOverviewsAsync(cancellationToken);
            var patientId = TwiistPwdSelector.SelectPatientId(config.PatientId, overviews);
            var package = await followerClient.GetPackageAsync(patientId, cancellationToken);
            var mapped = TwiistPackageMapper.Map(package);
            ApplyDateRange(mapped, request.From, request.To);

            await PublishMappedDataAsync(mapped, activeTypes, config, result, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during Twiist sync.");
            result.Success = false;
            result.Errors.Add($"Sync error: {ex.Message}");
        }

        return Finish(result);
    }

    private async Task PublishMappedDataAsync(
        TwiistMappedData mapped,
        HashSet<SyncDataType> activeTypes,
        TwiistConnectorConfiguration config,
        SyncResult result,
        CancellationToken cancellationToken)
    {
        if (activeTypes.Contains(SyncDataType.Glucose) && mapped.SensorGlucose.Count > 0)
        {
            var success = await PublishSensorGlucoseDataAsync(
                mapped.SensorGlucose,
                config,
                cancellationToken);
            TrackPublishResult(result, SyncDataType.Glucose, mapped.SensorGlucose.Count, success);
            result.LastEntryTimes[SyncDataType.Glucose] = mapped.SensorGlucose.Max(g => g.Timestamp);
        }

        if (activeTypes.Contains(SyncDataType.Boluses) && mapped.Boluses.Count > 0)
        {
            var success = await PublishBolusDataAsync(mapped.Boluses, config, cancellationToken);
            TrackPublishResult(result, SyncDataType.Boluses, mapped.Boluses.Count, success);
            result.LastEntryTimes[SyncDataType.Boluses] = mapped.Boluses.Max(b => b.Timestamp);
        }

        if (activeTypes.Contains(SyncDataType.CarbIntake) && mapped.CarbIntakes.Count > 0)
        {
            var success = await PublishCarbIntakeDataAsync(mapped.CarbIntakes, config, cancellationToken);
            TrackPublishResult(result, SyncDataType.CarbIntake, mapped.CarbIntakes.Count, success);
            result.LastEntryTimes[SyncDataType.CarbIntake] = mapped.CarbIntakes.Max(c => c.Timestamp);
        }

        if (activeTypes.Contains(SyncDataType.StateSpans) && mapped.TempBasals.Count > 0)
        {
            var success = await PublishTempBasalDataAsync(mapped.TempBasals, config, cancellationToken);
            TrackPublishResult(result, SyncDataType.StateSpans, mapped.TempBasals.Count, success);
            result.LastEntryTimes[SyncDataType.StateSpans] = mapped.TempBasals.Max(t => t.StartTimestamp);
        }

        if (activeTypes.Contains(SyncDataType.DeviceEvents) && mapped.DeviceEvents.Count > 0)
        {
            var success = await PublishDeviceEventDataAsync(mapped.DeviceEvents, config, cancellationToken);
            TrackPublishResult(result, SyncDataType.DeviceEvents, mapped.DeviceEvents.Count, success);
            result.LastEntryTimes[SyncDataType.DeviceEvents] = mapped.DeviceEvents.Max(e => e.Timestamp);
        }

        if (activeTypes.Contains(SyncDataType.Notes) && mapped.Notes.Count > 0)
        {
            var success = await PublishNoteDataAsync(mapped.Notes, config, cancellationToken);
            TrackPublishResult(result, SyncDataType.Notes, mapped.Notes.Count, success);
            result.LastEntryTimes[SyncDataType.Notes] = mapped.Notes.Max(n => n.Timestamp);
        }

        if (activeTypes.Contains(SyncDataType.DeviceStatus) && mapped.DeviceStatuses.Count > 0)
        {
            var success = await PublishDeviceStatusAsync(mapped.DeviceStatuses, config, cancellationToken);
            TrackPublishResult(result, SyncDataType.DeviceStatus, mapped.DeviceStatuses.Count, success);
            result.LastEntryTimes[SyncDataType.DeviceStatus] = DateTimeOffset
                .FromUnixTimeMilliseconds(mapped.DeviceStatuses.Max(ds => ds.Mills))
                .UtcDateTime;
        }
    }

    private static void TrackPublishResult(
        SyncResult result,
        SyncDataType type,
        int count,
        bool success)
    {
        result.ItemsSynced[type] = count;
        if (!success)
        {
            result.Success = false;
            result.Errors.Add($"{type} publish failed");
        }
    }

    private static SyncResult Finish(SyncResult result)
    {
        result.EndTime = DateTimeOffset.UtcNow;
        return result;
    }

    private static void ApplyDateRange(TwiistMappedData data, DateTime? from, DateTime? to)
    {
        if (from == null && to == null)
            return;

        data.SensorGlucose.RemoveAll(g => OutsideRange(g.Timestamp, from, to));
        data.Boluses.RemoveAll(b => OutsideRange(b.Timestamp, from, to));
        data.CarbIntakes.RemoveAll(c => OutsideRange(c.Timestamp, from, to));
        data.TempBasals.RemoveAll(t => OutsideRange(t.StartTimestamp, from, to));
        data.DeviceEvents.RemoveAll(e => OutsideRange(e.Timestamp, from, to));
        data.Notes.RemoveAll(n => OutsideRange(n.Timestamp, from, to));
        data.DeviceStatuses.RemoveAll(ds =>
            OutsideRange(DateTimeOffset.FromUnixTimeMilliseconds(ds.Mills).UtcDateTime, from, to));
    }

    private static bool OutsideRange(DateTime timestamp, DateTime? from, DateTime? to)
    {
        return from.HasValue && timestamp < from.Value
               || to.HasValue && timestamp > to.Value;
    }
}
