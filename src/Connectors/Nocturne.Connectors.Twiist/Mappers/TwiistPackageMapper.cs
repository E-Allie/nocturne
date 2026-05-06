using System.Globalization;
using Nocturne.Connectors.Twiist.Configurations;
using Nocturne.Connectors.Twiist.Models;
using Nocturne.Connectors.Twiist.Utilities;
using Nocturne.Core.Constants;
using Nocturne.Core.Models;
using Nocturne.Core.Models.V4;
using V4BolusType = Nocturne.Core.Models.V4.BolusType;

namespace Nocturne.Connectors.Twiist.Mappers;

public static class TwiistPackageMapper
{
    public static TwiistMappedData Map(TwiistPackage package)
    {
        var data = new TwiistMappedData();
        var device = $"Twiist/{package.PwdNickname}";
        var status = package.Status;

        data.SensorGlucose.AddRange(MapGlucose(status, device));

        if (status.InsulinHistory is { Count: > 0 })
        {
            foreach (var dose in status.InsulinHistory)
                MapInsulinDose(dose, device, data);
        }

        data.TempBasals.AddRange(MapInsulinDelivery(status, device));

        if (status.MealHistory is { Count: > 0 })
        {
            foreach (var meal in status.MealHistory)
            {
                var carb = MapMeal(meal, device);
                if (carb != null)
                    data.CarbIntakes.Add(carb);
            }
        }

        var statusDoc = MapDeviceStatus(status, device);
        if (statusDoc != null)
            data.DeviceStatuses.Add(statusDoc);

        if (status.Events is { Count: > 0 })
        {
            foreach (var ev in status.Events)
            {
                var note = MapAlarmEvent(ev, device);
                if (note != null)
                    data.Notes.Add(note);
            }
        }

        var cassetteChange = MapCassetteChange(status.Summary, device);
        if (cassetteChange != null)
            data.DeviceEvents.Add(cassetteChange);

        var loopError = MapLoopError(status.LoopAlgorithm, device);
        if (loopError != null)
            data.Notes.Add(loopError);

        return data;
    }

    public static GlucoseDirection? NormalizeDirection(string? raw)
    {
        return raw?.Trim() switch
        {
            "flat" or "Flat" or "->" or "→" => GlucoseDirection.Flat,
            "singleUp" or "SingleUp" or "↑" => GlucoseDirection.SingleUp,
            "doubleUp" or "DoubleUp" or "⇈" or "↑↑" => GlucoseDirection.DoubleUp,
            "fortyFiveUp" or "FortyFiveUp" or "↗" => GlucoseDirection.FortyFiveUp,
            "singleDown" or "SingleDown" or "↓" => GlucoseDirection.SingleDown,
            "doubleDown" or "DoubleDown" or "⇊" or "↓↓" => GlucoseDirection.DoubleDown,
            "fortyFiveDown" or "FortyFiveDown" or "↘" => GlucoseDirection.FortyFiveDown,
            "none" or "None" or "" => null,
            "NOT COMPUTABLE" or "NotComputable" => GlucoseDirection.NotComputable,
            "RATE OUT OF RANGE" or "RateOutOfRange" => GlucoseDirection.RateOutOfRange,
            _ => null
        };
    }

    private static IEnumerable<SensorGlucose> MapGlucose(TwiistStatus status, string device)
    {
        var bySecond = new SortedDictionary<long, SensorGlucose>();

        var historyBlob = status.GlucoseHistory?.Data;
        if (!string.IsNullOrWhiteSpace(historyBlob))
        {
            try
            {
                foreach (var record in TwiistBlobDecoder.DecodeGlucoseBlob(historyBlob))
                {
                    var legacyId = LegacyId("cgm", record.At);
                    bySecond[record.At.ToUnixTimeSeconds()] = CreateSensorGlucose(
                        record.At,
                        record.Mgdl,
                        direction: null,
                        legacyId,
                        device);
                }
            }
            catch
            {
                // A corrupt opaque blob should not prevent summary glucose or other package data from syncing.
            }
        }

        if (status.Summary is { GlucoseDate: not null, GlucoseQuantity: not null } summary)
        {
            var at = summary.GlucoseDate.Value;
            var legacyId = LegacyId("cgm", at);
            bySecond[at.ToUnixTimeSeconds()] = CreateSensorGlucose(
                at,
                ToMgdl(summary.GlucoseQuantity.Value, summary.GlucoseUnit),
                NormalizeDirection(summary.CgmRateArrow),
                legacyId,
                device);
        }

        return bySecond.Values;
    }

    private static SensorGlucose CreateSensorGlucose(
        DateTimeOffset at,
        decimal mgdl,
        GlucoseDirection? direction,
        string legacyId,
        string device)
    {
        var now = DateTime.UtcNow;
        return new SensorGlucose
        {
            Id = Guid.CreateVersion7(),
            Timestamp = at.UtcDateTime,
            LegacyId = legacyId,
            Device = device,
            App = TwiistConstants.AppName,
            DataSource = DataSources.TwiistConnector,
            Mgdl = (double)mgdl,
            Direction = direction,
            CreatedAt = now,
            ModifiedAt = now
        };
    }

    private static void MapInsulinDose(
        TwiistInsulinDose dose,
        string device,
        TwiistMappedData data)
    {
        if (dose.StartDate == null)
            return;

        var doseType = dose.DoseType?.Trim().ToLowerInvariant();
        switch (doseType)
        {
            case "bolus":
                if (dose.Value is > 0)
                    data.Boluses.Add(CreateBolus(dose, device));
                break;
            case "basal":
            case "tempbasal":
                if (dose.Value != null)
                    data.TempBasals.Add(CreateTempBasalFromDose(dose, device, doseType));
                break;
            case "suspend":
                data.DeviceEvents.Add(CreateDeviceEvent(
                    dose.StartDate.Value,
                    "suspend",
                    DeviceEventType.PumpSuspend,
                    "Twiist pump suspend",
                    device,
                    dose.Identifier));
                break;
            case "resume":
                data.DeviceEvents.Add(CreateDeviceEvent(
                    dose.StartDate.Value,
                    "resume",
                    DeviceEventType.PumpResume,
                    "Twiist pump resume",
                    device,
                    dose.Identifier));
                break;
        }
    }

    private static Bolus CreateBolus(TwiistInsulinDose dose, string device)
    {
        var at = dose.StartDate!.Value;
        var legacyId = LegacyId("bolus", at);
        var now = DateTime.UtcNow;
        return new Bolus
        {
            Id = Guid.CreateVersion7(),
            Timestamp = at.UtcDateTime,
            LegacyId = legacyId,
            SyncIdentifier = dose.Identifier ?? legacyId,
            Device = device,
            App = TwiistConstants.AppName,
            DataSource = DataSources.TwiistConnector,
            Insulin = (double)dose.Value!.Value,
            BolusType = V4BolusType.Normal,
            Kind = BolusKind.Manual,
            Automatic = false,
            Duration = 0,
            CreatedAt = now,
            ModifiedAt = now,
            AdditionalProperties = BuildAdditionalProperties(
                ("doseType", dose.DoseType),
                ("valueUnit", dose.ValueUnit),
                ("identifier", dose.Identifier))
        };
    }

    private static TempBasal CreateTempBasalFromDose(
        TwiistInsulinDose dose,
        string device,
        string normalizedDoseType)
    {
        var start = dose.StartDate!.Value;
        var end = dose.EndDate?.UtcDateTime;
        var rate = dose.Value!.Value;
        var legacyId = LegacyId("basal", start);
        var now = DateTime.UtcNow;

        return new TempBasal
        {
            Id = Guid.CreateVersion7(),
            StartTimestamp = start.UtcDateTime,
            EndTimestamp = end,
            LegacyId = legacyId,
            PumpRecordId = dose.Identifier ?? legacyId,
            Device = device,
            App = TwiistConstants.AppName,
            DataSource = DataSources.TwiistConnector,
            Rate = (double)rate,
            Origin = rate <= 0
                ? TempBasalOrigin.Suspended
                : normalizedDoseType == "tempbasal"
                    ? TempBasalOrigin.Manual
                    : TempBasalOrigin.Scheduled,
            CreatedAt = now,
            ModifiedAt = now,
            AdditionalProperties = BuildAdditionalProperties(
                ("doseType", dose.DoseType),
                ("valueUnit", dose.ValueUnit),
                ("identifier", dose.Identifier))
        };
    }

    private static IEnumerable<TempBasal> MapInsulinDelivery(TwiistStatus status, string device)
    {
        var blob = status.InsulinDelivery?.Data;
        if (string.IsNullOrWhiteSpace(blob))
            return [];

        var scheduled = DeriveScheduledBasalRate(status);
        if (scheduled == null)
            return [];

        IReadOnlyList<TwiistInsulinDelivery> pulses;
        try
        {
            pulses = TwiistBlobDecoder.DecodeInsulinDeliveryBlob(blob);
        }
        catch
        {
            return [];
        }

        var now = DateTime.UtcNow;
        return TwiistBlobDecoder.AggregatePulsesToPhases(pulses, scheduled.Value)
            .Select(phase =>
            {
                var legacyId = LegacyId("basal", phase.Start);
                return new TempBasal
                {
                    Id = Guid.CreateVersion7(),
                    StartTimestamp = phase.Start.UtcDateTime,
                    EndTimestamp = phase.End.UtcDateTime,
                    LegacyId = legacyId,
                    PumpRecordId = legacyId,
                    Device = device,
                    App = TwiistConstants.AppName,
                    DataSource = DataSources.TwiistConnector,
                    Rate = (double)phase.RateUnitsPerHour,
                    ScheduledRate = (double)phase.ScheduledRateUnitsPerHour,
                    Origin = phase.RateUnitsPerHour <= 0
                        ? TempBasalOrigin.Suspended
                        : phase.RateUnitsPerHour == phase.ScheduledRateUnitsPerHour
                            ? TempBasalOrigin.Scheduled
                            : TempBasalOrigin.Algorithm,
                    CreatedAt = now,
                    ModifiedAt = now,
                    AdditionalProperties = new Dictionary<string, object?>
                    {
                        ["source"] = "insulinDelivery",
                        ["durationMinutes"] = phase.DurationMinutes
                    }
                };
            })
            .ToList();
    }

    private static decimal? DeriveScheduledBasalRate(TwiistStatus status)
    {
        if (status.Details?.BasalRateUnitsPerHour == null
            || status.Summary?.NetBasalUnitsPerHour == null)
        {
            return null;
        }

        return status.Details.BasalRateUnitsPerHour.Value
            - status.Summary.NetBasalUnitsPerHour.Value;
    }

    private static CarbIntake? MapMeal(TwiistMeal meal, string device)
    {
        var at = meal.StartDate ?? meal.AddedDate;
        if (at == null || meal.Grams is not > 0)
            return null;

        var legacyId = LegacyId("meal", at.Value);
        var now = DateTime.UtcNow;
        return new CarbIntake
        {
            Id = Guid.CreateVersion7(),
            Timestamp = at.Value.UtcDateTime,
            LegacyId = legacyId,
            SyncIdentifier = legacyId,
            Device = device,
            App = TwiistConstants.AppName,
            DataSource = DataSources.TwiistConnector,
            Carbs = (double)meal.Grams.Value,
            AbsorptionTime = meal.AbsorptionTimeSeconds.HasValue
                ? (int)Math.Round((double)(meal.AbsorptionTimeSeconds.Value / 60m))
                : null,
            CreatedAt = now,
            ModifiedAt = now,
            AdditionalProperties = BuildAdditionalProperties(
                ("foodType", meal.FoodType),
                ("associatedBolusIds", meal.AssociatedBolusIds))
        };
    }

    private static Note? MapAlarmEvent(TwiistEvent ev, string device)
    {
        if (ev.Timestamp == null || string.IsNullOrWhiteSpace(ev.Type))
            return null;

        var lower = ev.Type.Trim().ToLowerInvariant();
        if (lower is "bolus event" or "basal event")
            return null;

        if (lower is not ("critical alarm" or "urgent alert" or "alert" or "alarm"))
            return null;

        var source = string.IsNullOrWhiteSpace(ev.Source) ? "unknown" : ev.Source;
        var text = string.IsNullOrWhiteSpace(ev.Id)
            ? $"{ev.Type} from {source}"
            : $"{ev.Type}: {ev.Id} from {source}";

        return CreateNote(
            ev.Timestamp.Value,
            "alarm",
            text,
            "Announcement",
            isAnnouncement: true,
            device,
            ev.Id,
            BuildAdditionalProperties(
                ("eventType", ev.Type),
                ("source", ev.Source),
                ("eventId", ev.Id)));
    }

    private static DeviceEvent? MapCassetteChange(TwiistSummary? summary, string device)
    {
        if (summary?.LastCassetteChangeDate == null)
            return null;

        return CreateDeviceEvent(
            summary.LastCassetteChangeDate.Value,
            "sitechange",
            DeviceEventType.SiteChange,
            "Twiist cassette change",
            device,
            externalIdentifier: null);
    }

    private static Note? MapLoopError(TwiistLoopAlgorithm? algorithm, string device)
    {
        var error = algorithm?.LastLoopError?.Trim();
        if (string.IsNullOrWhiteSpace(error)
            || error.Equals("noerror", StringComparison.OrdinalIgnoreCase)
            || error.Equals("nil", StringComparison.OrdinalIgnoreCase)
            || algorithm?.LastLoopRunDate == null)
        {
            return null;
        }

        return CreateNote(
            algorithm.LastLoopRunDate.Value,
            "twiist-looperr",
            error,
            "Note",
            isAnnouncement: false,
            device,
            externalIdentifier: null,
            BuildAdditionalProperties(("reason", "loop error")));
    }

    private static DeviceEvent CreateDeviceEvent(
        DateTimeOffset at,
        string legacyKind,
        DeviceEventType eventType,
        string notes,
        string device,
        string? externalIdentifier)
    {
        var legacyId = LegacyId(legacyKind, at);
        var now = DateTime.UtcNow;
        return new DeviceEvent
        {
            Id = Guid.CreateVersion7(),
            Timestamp = at.UtcDateTime,
            LegacyId = legacyId,
            SyncIdentifier = externalIdentifier ?? legacyId,
            Device = device,
            App = TwiistConstants.AppName,
            DataSource = DataSources.TwiistConnector,
            EventType = eventType,
            Notes = notes,
            CreatedAt = now,
            ModifiedAt = now
        };
    }

    private static Note CreateNote(
        DateTimeOffset at,
        string legacyKind,
        string text,
        string eventType,
        bool isAnnouncement,
        string device,
        string? externalIdentifier,
        Dictionary<string, object?>? additionalProperties)
    {
        var legacyId = LegacyId(legacyKind, at);
        var now = DateTime.UtcNow;
        return new Note
        {
            Id = Guid.CreateVersion7(),
            Timestamp = at.UtcDateTime,
            LegacyId = legacyId,
            SyncIdentifier = externalIdentifier ?? legacyId,
            Device = device,
            App = TwiistConstants.AppName,
            DataSource = DataSources.TwiistConnector,
            Text = text,
            EventType = eventType,
            IsAnnouncement = isAnnouncement,
            CreatedAt = now,
            ModifiedAt = now,
            AdditionalProperties = additionalProperties
        };
    }

    private static DeviceStatus? MapDeviceStatus(TwiistStatus status, string device)
    {
        var loop = MapLoopStatus(status.Details);
        var summaryDate = status.Summary?.GlucoseDate;
        var pumpClock = status.Date ?? summaryDate;
        var pump = MapPumpStatus(status.Summary, pumpClock);
        if (loop == null && pump == null)
            return null;

        var newest = new[]
            {
                status.Date,
                summaryDate,
                status.Details?.ActiveInsulinDate,
                status.Details?.ActiveCarbsDate
            }
            .Where(d => d.HasValue)
            .Select(d => d!.Value)
            .DefaultIfEmpty()
            .Max();

        if (newest == default)
            return null;

        var legacyId = LegacyId("devicestatus", newest);
        return new DeviceStatus
        {
            Id = legacyId,
            Mills = newest.ToUnixTimeMilliseconds(),
            Date = newest.ToUnixTimeMilliseconds(),
            CreatedAt = newest.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture),
            UtcOffset = 0,
            Device = device,
            Loop = loop,
            Pump = pump
        };
    }

    private static LoopStatus? MapLoopStatus(TwiistDetails? details)
    {
        if (details == null)
            return null;

        var hasIob = details.ActiveInsulinUnits != null && details.ActiveInsulinDate != null;
        var hasCob = details.ActiveCarbsGrams != null && details.ActiveCarbsDate != null;
        if (!hasIob && !hasCob)
            return null;

        return new LoopStatus
        {
            Name = "Twiist",
            Version = "follower",
            Iob = hasIob
                ? new LoopIob
                {
                    Iob = (double)details.ActiveInsulinUnits!.Value,
                    Timestamp = details.ActiveInsulinDate!.Value.ToString("O")
                }
                : null,
            Cob = hasCob
                ? new LoopCob
                {
                    Cob = (double)details.ActiveCarbsGrams!.Value,
                    Timestamp = details.ActiveCarbsDate!.Value.ToString("O")
                }
                : null
        };
    }

    private static PumpStatus? MapPumpStatus(TwiistSummary? summary, DateTimeOffset? clock)
    {
        if (summary == null)
            return null;

        var reservoir = summary.PumpCassetteVolumeUnits;
        var batteryPercent = summary.PumpBatteryLevel.HasValue
            ? (int?)Math.Round(summary.PumpBatteryLevel.Value * 100m, MidpointRounding.AwayFromZero)
            : null;

        if (reservoir == null && batteryPercent == null)
            return null;

        return new PumpStatus
        {
            Reservoir = reservoir.HasValue ? (double)reservoir.Value : null,
            Battery = batteryPercent.HasValue ? new PumpBattery { Percent = batteryPercent.Value } : null,
            Clock = clock?.ToString("O"),
            Status = summary.IsBasalActive.HasValue
                ? new PumpStatusDetails
                {
                    Suspended = summary.IsBasalActive == false,
                    Status = summary.IsBasalActive == false ? "suspended" : "normal"
                }
                : null,
            Manufacturer = "Sequel",
            Model = "Twiist"
        };
    }

    private static decimal ToMgdl(decimal glucose, string? unit)
    {
        return unit?.Contains("mmol", StringComparison.OrdinalIgnoreCase) == true
            ? glucose * 18.0182m
            : glucose;
    }

    private static string LegacyId(string kind, DateTimeOffset at)
    {
        return $"{kind}-{at.ToUnixTimeSeconds()}";
    }

    private static Dictionary<string, object?>? BuildAdditionalProperties(
        params (string Key, object? Value)[] values)
    {
        var dict = values
            .Where(pair => pair.Value != null)
            .ToDictionary(pair => pair.Key, pair => pair.Value);

        return dict.Count == 0 ? null : dict;
    }
}
