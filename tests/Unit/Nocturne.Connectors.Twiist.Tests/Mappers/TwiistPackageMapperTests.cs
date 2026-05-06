using System.IO.Compression;
using FluentAssertions;
using Nocturne.Connectors.Twiist.Mappers;
using Nocturne.Connectors.Twiist.Models;
using Nocturne.Connectors.Twiist.Utilities;
using Nocturne.Core.Constants;
using Nocturne.Core.Models;
using Nocturne.Core.Models.V4;
using Xunit;
using V4BolusType = Nocturne.Core.Models.V4.BolusType;

namespace Nocturne.Connectors.Twiist.Tests.Mappers;

public class TwiistPackageMapperTests
{
    [Theory]
    [InlineData("→", GlucoseDirection.Flat)]
    [InlineData("singleUp", GlucoseDirection.SingleUp)]
    [InlineData("⇈", GlucoseDirection.DoubleUp)]
    [InlineData("↘", GlucoseDirection.FortyFiveDown)]
    [InlineData("NOT COMPUTABLE", GlucoseDirection.NotComputable)]
    [InlineData("RATE OUT OF RANGE", GlucoseDirection.RateOutOfRange)]
    public void NormalizeDirection_MapsTwiistArrows(string raw, GlucoseDirection expected)
    {
        TwiistPackageMapper.NormalizeDirection(raw).Should().Be(expected);
    }

    [Fact]
    public void Map_ConvertsPackageIntoNocturneRecords()
    {
        var package = CreatePackage();

        var mapped = TwiistPackageMapper.Map(package);

        mapped.SensorGlucose.Should().ContainSingle();
        var glucose = mapped.SensorGlucose[0];
        glucose.Mgdl.Should().Be(123);
        glucose.Direction.Should().Be(GlucoseDirection.Flat);
        glucose.DataSource.Should().Be(DataSources.TwiistConnector);
        glucose.App.Should().Be("TwiistSync");
        glucose.Device.Should().Be("Twiist/Test PWD");

        mapped.Boluses.Should().ContainSingle();
        mapped.Boluses[0].Insulin.Should().Be(1.5);
        mapped.Boluses[0].BolusType.Should().Be(V4BolusType.Normal);
        mapped.Boluses[0].SyncIdentifier.Should().Be("dose-bolus");

        mapped.CarbIntakes.Should().ContainSingle();
        mapped.CarbIntakes[0].Carbs.Should().Be(42);
        mapped.CarbIntakes[0].AbsorptionTime.Should().Be(120);

        mapped.TempBasals.Should().HaveCount(2);
        mapped.TempBasals.Should().Contain(t => t.Rate == 0.7 && t.Origin == TempBasalOrigin.Scheduled);
        mapped.TempBasals.Should().Contain(t => t.Rate == 1.6 && t.Origin == TempBasalOrigin.Algorithm);

        mapped.DeviceEvents.Should().HaveCount(3);
        mapped.DeviceEvents.Select(e => e.EventType).Should().Contain([
            DeviceEventType.PumpSuspend,
            DeviceEventType.PumpResume,
            DeviceEventType.SiteChange
        ]);

        mapped.Notes.Should().HaveCount(2);
        mapped.Notes.Should().Contain(n => n.IsAnnouncement && n.Text == "Alarm: Occlusion from Pump");
        mapped.Notes.Should().Contain(n => !n.IsAnnouncement && n.Text == "Loop failed");

        mapped.DeviceStatuses.Should().ContainSingle();
        var ds = mapped.DeviceStatuses[0];
        ds.Device.Should().Be("Twiist/Test PWD");
        ds.Loop!.Iob!.Iob.Should().Be(0.59);
        ds.Loop.Cob!.Cob.Should().Be(12);
        ds.Pump!.Reservoir.Should().Be(104.68);
        ds.Pump.Battery!.Percent.Should().Be(48);
        ds.Pump.Manufacturer.Should().Be("Sequel");
        ds.Pump.Model.Should().Be("Twiist");
    }

    private static TwiistPackage CreatePackage()
    {
        var glucoseAt = DateTimeOffset.Parse("2026-04-20T19:47:55Z");
        var doseAt = DateTimeOffset.Parse("2026-04-20T19:40:00Z");
        var basalAt = DateTimeOffset.Parse("2026-04-20T19:30:00Z");
        var suspendAt = DateTimeOffset.Parse("2026-04-20T19:45:00Z");
        var resumeAt = DateTimeOffset.Parse("2026-04-20T19:46:00Z");
        var iobAt = DateTimeOffset.Parse("2026-04-20T19:50:00Z");
        var cobAt = DateTimeOffset.Parse("2026-04-20T19:49:00Z");

        return new TwiistPackage
        {
            PwdId = Guid.Parse("72826af6-68c9-4d6f-8f98-2ec2b07acda3"),
            PwdNickname = "Test PWD",
            Status = new TwiistStatus
            {
                Date = DateTimeOffset.Parse("2026-04-20T19:47:58Z"),
                Summary = new TwiistSummary
                {
                    GlucoseDate = glucoseAt,
                    GlucoseQuantity = 123m,
                    GlucoseUnit = "mg/dL",
                    CgmRateArrow = "→",
                    LastCassetteChangeDate = DateTimeOffset.Parse("2026-04-18T12:00:00Z"),
                    PumpBatteryLevel = 0.48m,
                    PumpCassetteVolumeUnits = 104.68m,
                    NetBasalUnitsPerHour = 0m,
                    IsBasalActive = true
                },
                Details = new TwiistDetails
                {
                    ActiveInsulinUnits = 0.59m,
                    ActiveInsulinDate = iobAt,
                    ActiveCarbsGrams = 12m,
                    ActiveCarbsDate = cobAt,
                    BasalRateUnitsPerHour = 0.6m
                },
                InsulinHistory =
                [
                    new()
                    {
                        Identifier = "dose-bolus",
                        DoseType = "bolus",
                        StartDate = doseAt,
                        EndDate = doseAt,
                        Value = 1.5m,
                        ValueUnit = "IU"
                    },
                    new()
                    {
                        Identifier = "dose-basal",
                        DoseType = "basal",
                        StartDate = basalAt,
                        EndDate = basalAt.AddMinutes(30),
                        Value = 0.7m,
                        ValueUnit = "U/hr"
                    },
                    new() { DoseType = "Suspend", StartDate = suspendAt, EndDate = suspendAt, Value = 0m },
                    new() { DoseType = "Resume", StartDate = resumeAt, EndDate = resumeAt, Value = 0m }
                ],
                InsulinDelivery = new TwiistRawBlob
                {
                    Data = InsulinDeliveryBlob(
                        DateTimeOffset.Parse("2026-04-20T19:00:00Z"),
                        delta: 100)
                },
                MealHistory =
                [
                    new()
                    {
                        StartDate = DateTimeOffset.Parse("2026-04-20T19:20:00Z"),
                        AddedDate = DateTimeOffset.Parse("2026-04-20T19:19:00Z"),
                        Grams = 42m,
                        AbsorptionTimeSeconds = 7200m,
                        FoodType = "meal",
                        AssociatedBolusIds = ["dose-bolus"]
                    }
                ],
                Events =
                [
                    new()
                    {
                        Id = "Occlusion",
                        Type = "Alarm",
                        Source = "Pump",
                        Timestamp = DateTimeOffset.Parse("2026-04-20T19:48:00Z")
                    }
                ],
                LoopAlgorithm = new TwiistLoopAlgorithm
                {
                    LastLoopRunDate = DateTimeOffset.Parse("2026-04-20T19:48:30Z"),
                    LastLoopError = "Loop failed"
                }
            }
        };
    }

    private static string InsulinDeliveryBlob(DateTimeOffset start, short delta)
    {
        var startRaw = RawTimestamp(start);
        var midRaw = RawTimestamp(start.AddMinutes(5));
        var endRaw = RawTimestamp(start.AddMinutes(10));
        var projectionEndRaw = RawTimestamp(start.AddMinutes(40));

        var bytes = new List<byte>();
        bytes.AddRange(BitConverter.GetBytes(startRaw));
        bytes.AddRange(BitConverter.GetBytes(midRaw));
        bytes.AddRange(BitConverter.GetBytes(delta));
        bytes.AddRange(BitConverter.GetBytes(midRaw));
        bytes.AddRange(BitConverter.GetBytes(endRaw));
        bytes.AddRange(BitConverter.GetBytes(delta));
        bytes.AddRange(BitConverter.GetBytes(endRaw));
        bytes.AddRange(BitConverter.GetBytes(projectionEndRaw));
        bytes.AddRange(BitConverter.GetBytes((short)0));

        using var output = new MemoryStream();
        using (var deflate = new DeflateStream(output, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            deflate.Write(bytes.ToArray());
        }

        return Convert.ToBase64String(output.ToArray());
    }

    private static uint RawTimestamp(DateTimeOffset timestamp)
    {
        return (uint)(timestamp.ToUnixTimeSeconds() - TwiistBlobDecoder.TwiistEpochUnixSeconds);
    }
}
