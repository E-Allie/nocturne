using FluentAssertions;
using Nocturne.Connectors.Twiist.Utilities;
using Xunit;

namespace Nocturne.Connectors.Twiist.Tests.Utilities;

public class TwiistBlobDecoderTests
{
    [Fact]
    public void ParseGlucoseRecords_ReadsTimestampAndMgdlPairs()
    {
        var t1 = RawTimestamp(2026, 4, 19, 5, 30, 0);
        var t2 = t1 + 300;
        var bytes = new List<byte>();
        bytes.AddRange(BitConverter.GetBytes(t1));
        bytes.AddRange(BitConverter.GetBytes(12_345u));
        bytes.AddRange(BitConverter.GetBytes(t2));
        bytes.AddRange(BitConverter.GetBytes(9_876u));

        var records = TwiistBlobDecoder.ParseGlucoseRecords(bytes.ToArray());

        records.Should().HaveCount(2);
        records[0].At.Should().Be(DateTimeOffset.Parse("2026-04-19T05:30:00Z"));
        records[0].Mgdl.Should().Be(123.45m);
        records[1].Mgdl.Should().Be(98.76m);
    }

    [Fact]
    public void ParseInsulinDeliveries_ReadsSignedDeltaAndAbsoluteRate()
    {
        var start = RawTimestamp(2026, 4, 20, 13, 32, 52);
        var end = start + 300;
        var bytes = new List<byte>();
        bytes.AddRange(BitConverter.GetBytes(start));
        bytes.AddRange(BitConverter.GetBytes(end));
        bytes.AddRange(BitConverter.GetBytes((short)224));

        var records = TwiistBlobDecoder.ParseInsulinDeliveries(bytes.ToArray());

        records.Should().ContainSingle();
        records[0].DeltaUnitsPerHourX100.Should().Be(224);
        TwiistBlobDecoder.AbsoluteRateUnitsPerHour(records[0], 0.6m).Should().Be(2.84m);
    }

    [Fact]
    public void AggregatePulsesToPhases_DropsTrailingProjectionAndMergesContiguousRate()
    {
        var pulses = new[]
        {
            new TwiistInsulinDelivery(TwiistBlobDecoder.DecodeTimestamp(0), TwiistBlobDecoder.DecodeTimestamp(127), 224),
            new TwiistInsulinDelivery(TwiistBlobDecoder.DecodeTimestamp(127), TwiistBlobDecoder.DecodeTimestamp(300), 224),
            new TwiistInsulinDelivery(TwiistBlobDecoder.DecodeTimestamp(300), TwiistBlobDecoder.DecodeTimestamp(2100), 0),
        };

        var phases = TwiistBlobDecoder.AggregatePulsesToPhases(pulses, 0.6m);

        phases.Should().ContainSingle();
        phases[0].RateUnitsPerHour.Should().Be(2.84m);
        phases[0].DurationMinutes.Should().Be(5);
    }

    [Fact]
    public void DecodeRaw_DecodesObservedRawDeflatePayload()
    {
        var decoded = TwiistBlobDecoder.DecodeRaw("Y2AQUH+jAwA=");

        decoded.Should().HaveCount(6);
        BitConverter.ToUInt16(decoded, 0).Should().Be(0);
        BitConverter.ToUInt16(decoded, 2).Should().Be(10_000);
        BitConverter.ToUInt16(decoded, 4).Should().Be(11_500);
    }

    private static uint RawTimestamp(int year, int month, int day, int hour, int minute, int second)
    {
        var unix = new DateTimeOffset(year, month, day, hour, minute, second, TimeSpan.Zero)
            .ToUnixTimeSeconds();
        return (uint)(unix - TwiistBlobDecoder.TwiistEpochUnixSeconds);
    }
}
