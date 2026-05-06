using System.Buffers.Binary;
using System.IO.Compression;

namespace Nocturne.Connectors.Twiist.Utilities;

public static class TwiistBlobDecoder
{
    public const long TwiistEpochUnixSeconds = 1_199_145_600;

    public static DateTimeOffset DecodeTimestamp(uint raw)
    {
        return DateTimeOffset.FromUnixTimeSeconds(TwiistEpochUnixSeconds + raw);
    }

    public static byte[] DecodeRaw(string base64)
    {
        var cleaned = new string(base64.Where(c => !char.IsWhiteSpace(c)).ToArray());
        var compressed = Convert.FromBase64String(cleaned);

        using var input = new MemoryStream(compressed);
        using var deflate = new DeflateStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        deflate.CopyTo(output);
        return output.ToArray();
    }

    public static IReadOnlyList<TwiistGlucoseRecord> DecodeGlucoseBlob(string base64)
    {
        return ParseGlucoseRecords(DecodeRaw(base64));
    }

    public static IReadOnlyList<TwiistGlucoseRecord> ParseGlucoseRecords(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length % 8 != 0)
            throw new InvalidOperationException($"Glucose blob length {bytes.Length} is not a multiple of 8.");

        var records = new List<TwiistGlucoseRecord>(bytes.Length / 8);
        for (var offset = 0; offset < bytes.Length; offset += 8)
        {
            var timestamp = BinaryPrimitives.ReadUInt32LittleEndian(bytes[offset..(offset + 4)]);
            var rawMgdl = BinaryPrimitives.ReadUInt32LittleEndian(bytes[(offset + 4)..(offset + 8)]);
            records.Add(new TwiistGlucoseRecord(
                DecodeTimestamp(timestamp),
                rawMgdl / 100m));
        }

        return records;
    }

    public static IReadOnlyList<TwiistInsulinDelivery> DecodeInsulinDeliveryBlob(string base64)
    {
        return ParseInsulinDeliveries(DecodeRaw(base64));
    }

    public static IReadOnlyList<TwiistInsulinDelivery> ParseInsulinDeliveries(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length % 10 != 0)
            throw new InvalidOperationException(
                $"InsulinDelivery blob length {bytes.Length} is not a multiple of 10.");

        var records = new List<TwiistInsulinDelivery>(bytes.Length / 10);
        for (var offset = 0; offset < bytes.Length; offset += 10)
        {
            var start = BinaryPrimitives.ReadUInt32LittleEndian(bytes[offset..(offset + 4)]);
            var end = BinaryPrimitives.ReadUInt32LittleEndian(bytes[(offset + 4)..(offset + 8)]);
            var delta = BinaryPrimitives.ReadInt16LittleEndian(bytes[(offset + 8)..(offset + 10)]);
            records.Add(new TwiistInsulinDelivery(
                DecodeTimestamp(start),
                DecodeTimestamp(end),
                delta));
        }

        return records;
    }

    public static decimal AbsoluteRateUnitsPerHour(
        TwiistInsulinDelivery record,
        decimal scheduledRateUnitsPerHour)
    {
        return scheduledRateUnitsPerHour + record.DeltaUnitsPerHourX100 / 100m;
    }

    public static IReadOnlyList<TwiistBasalPhase> AggregatePulsesToPhases(
        IReadOnlyList<TwiistInsulinDelivery> pulses,
        decimal scheduledRateUnitsPerHour)
    {
        if (pulses.Count <= 1)
            return [];

        var phases = new List<TwiistBasalPhase>();
        TwiistBasalPhase? current = null;

        foreach (var pulse in pulses.Take(pulses.Count - 1))
        {
            var rate = AbsoluteRateUnitsPerHour(pulse, scheduledRateUnitsPerHour);
            if (current is { } phase
                && phase.RateUnitsPerHour == rate
                && phase.End == pulse.Start)
            {
                current = phase with { End = pulse.End };
                continue;
            }

            if (current is not null)
                phases.Add(current);

            current = new TwiistBasalPhase(pulse.Start, pulse.End, rate, scheduledRateUnitsPerHour);
        }

        if (current is not null)
            phases.Add(current);

        return phases;
    }
}

public readonly record struct TwiistGlucoseRecord(DateTimeOffset At, decimal Mgdl);

public readonly record struct TwiistInsulinDelivery(
    DateTimeOffset Start,
    DateTimeOffset End,
    short DeltaUnitsPerHourX100);

public sealed record TwiistBasalPhase(
    DateTimeOffset Start,
    DateTimeOffset End,
    decimal RateUnitsPerHour,
    decimal ScheduledRateUnitsPerHour)
{
    public double DurationMinutes => Math.Max(0, (End - Start).TotalMinutes);
}
