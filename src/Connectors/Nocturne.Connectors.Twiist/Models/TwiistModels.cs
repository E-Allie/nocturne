using System.Text.Json.Serialization;

namespace Nocturne.Connectors.Twiist.Models;

public class TwiistPackage
{
    [JsonPropertyName("pwdId")]
    public Guid PwdId { get; set; }

    [JsonPropertyName("pwdNickname")]
    public string PwdNickname { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public TwiistStatus Status { get; set; } = new();
}

public class TwiistStatus
{
    [JsonPropertyName("date")]
    public DateTimeOffset? Date { get; set; }

    [JsonPropertyName("summary")]
    public TwiistSummary? Summary { get; set; }

    [JsonPropertyName("details")]
    public TwiistDetails? Details { get; set; }

    [JsonPropertyName("loopAlgorithm")]
    public TwiistLoopAlgorithm? LoopAlgorithm { get; set; }

    [JsonPropertyName("events")]
    public List<TwiistEvent>? Events { get; set; }

    [JsonPropertyName("activeEvents")]
    public List<string>? ActiveEvents { get; set; }

    [JsonPropertyName("insulinHistory")]
    public List<TwiistInsulinDose>? InsulinHistory { get; set; }

    [JsonPropertyName("mealHistory")]
    public List<TwiistMeal>? MealHistory { get; set; }

    [JsonPropertyName("glucoseForecast")]
    public TwiistRawBlob? GlucoseForecast { get; set; }

    [JsonPropertyName("glucoseHistory")]
    public TwiistRawBlob? GlucoseHistory { get; set; }

    [JsonPropertyName("insulinDelivery")]
    public TwiistRawBlob? InsulinDelivery { get; set; }

    [JsonPropertyName("activeInsulin")]
    public TwiistRawBlob? ActiveInsulin { get; set; }

    [JsonPropertyName("activeCarbohydrates")]
    public TwiistRawBlob? ActiveCarbohydrates { get; set; }

    [JsonPropertyName("correctionRange")]
    public TwiistRawBlob? CorrectionRange { get; set; }
}

public class TwiistRawBlob
{
    [JsonPropertyName("data")]
    public string? Data { get; set; }
}

public class TwiistSummary
{
    [JsonPropertyName("glucoseDate")]
    public DateTimeOffset? GlucoseDate { get; set; }

    [JsonPropertyName("glucoseQuantity")]
    [JsonConverter(typeof(TwiistNullableDecimalConverter))]
    public decimal? GlucoseQuantity { get; set; }

    [JsonPropertyName("glucoseUnit")]
    public string? GlucoseUnit { get; set; }

    [JsonPropertyName("glucoseHighlightState")]
    public string? GlucoseHighlightState { get; set; }

    [JsonPropertyName("glucoseHighlightStateString")]
    public string? GlucoseHighlightStateString { get; set; }

    [JsonPropertyName("cgmRateArrow")]
    public string? CgmRateArrow { get; set; }

    [JsonPropertyName("closedLoopEnabled")]
    public bool? ClosedLoopEnabled { get; set; }

    [JsonPropertyName("isBasalActive")]
    public bool? IsBasalActive { get; set; }

    [JsonPropertyName("lastCassetteChangeDate")]
    public DateTimeOffset? LastCassetteChangeDate { get; set; }

    [JsonPropertyName("loopRingColor")]
    public string? LoopRingColor { get; set; }

    [JsonPropertyName("maximumBasalRate_UnitsPerHour")]
    [JsonConverter(typeof(TwiistNullableDecimalConverter))]
    public decimal? MaximumBasalRateUnitsPerHour { get; set; }

    [JsonPropertyName("netBasal_UnitsPerHour")]
    [JsonConverter(typeof(TwiistNullableDecimalConverter))]
    public decimal? NetBasalUnitsPerHour { get; set; }

    [JsonPropertyName("pumpAlarmState")]
    public string? PumpAlarmState { get; set; }

    [JsonPropertyName("pumpBatteryLevel")]
    [JsonConverter(typeof(TwiistNullableDecimalConverter))]
    public decimal? PumpBatteryLevel { get; set; }

    [JsonPropertyName("pumpCassetteFilledVolume_Units")]
    [JsonConverter(typeof(TwiistNullableDecimalConverter))]
    public decimal? PumpCassetteFilledVolumeUnits { get; set; }

    [JsonPropertyName("pumpCassetteVolume_Units")]
    [JsonConverter(typeof(TwiistNullableDecimalConverter))]
    public decimal? PumpCassetteVolumeUnits { get; set; }

    [JsonPropertyName("pumpEventsComplete")]
    public bool? PumpEventsComplete { get; set; }
}

public class TwiistDetails
{
    [JsonPropertyName("activeCarbs_grams")]
    [JsonConverter(typeof(TwiistNullableDecimalConverter))]
    public decimal? ActiveCarbsGrams { get; set; }

    [JsonPropertyName("activeInsulin_Units")]
    [JsonConverter(typeof(TwiistNullableDecimalConverter))]
    public decimal? ActiveInsulinUnits { get; set; }

    [JsonPropertyName("activeInsulinDate")]
    public DateTimeOffset? ActiveInsulinDate { get; set; }

    [JsonPropertyName("activeCarbsDate")]
    public DateTimeOffset? ActiveCarbsDate { get; set; }

    [JsonPropertyName("basalRate_UnitsPerHour")]
    [JsonConverter(typeof(TwiistNullableDecimalConverter))]
    public decimal? BasalRateUnitsPerHour { get; set; }

    [JsonPropertyName("lastBolusDate")]
    public DateTimeOffset? LastBolusDate { get; set; }

    [JsonPropertyName("lastBolusVolume_Units")]
    [JsonConverter(typeof(TwiistNullableDecimalConverter))]
    public decimal? LastBolusVolumeUnits { get; set; }

    [JsonPropertyName("insulinSince_Units")]
    [JsonConverter(typeof(TwiistNullableDecimalConverter))]
    public decimal? InsulinSinceUnits { get; set; }

    [JsonPropertyName("insulinSinceDate")]
    public DateTimeOffset? InsulinSinceDate { get; set; }

    [JsonPropertyName("carbsSince_grams")]
    [JsonConverter(typeof(TwiistNullableDecimalConverter))]
    public decimal? CarbsSinceGrams { get; set; }

    [JsonPropertyName("carbsSinceDate")]
    public DateTimeOffset? CarbsSinceDate { get; set; }

    [JsonPropertyName("highGlucoseTargetOverride")]
    [JsonConverter(typeof(TwiistNullableDecimalConverter))]
    public decimal? HighGlucoseTargetOverride { get; set; }

    [JsonPropertyName("lowGlucoseTargetOverride")]
    [JsonConverter(typeof(TwiistNullableDecimalConverter))]
    public decimal? LowGlucoseTargetOverride { get; set; }

    [JsonPropertyName("targetOverrideDurationSeconds")]
    public long? TargetOverrideDurationSeconds { get; set; }

    [JsonPropertyName("targetOverrideUnit")]
    public string? TargetOverrideUnit { get; set; }

    [JsonPropertyName("preMealTargetActive")]
    public bool? PreMealTargetActive { get; set; }

    [JsonPropertyName("workoutTargetActive")]
    public bool? WorkoutTargetActive { get; set; }

    [JsonPropertyName("openLoopTempBasalActive")]
    public bool? OpenLoopTempBasalActive { get; set; }

    [JsonPropertyName("mobileBatteryState")]
    public string? MobileBatteryState { get; set; }
}

public class TwiistLoopAlgorithm
{
    [JsonPropertyName("closedLoopSetting")]
    public string? ClosedLoopSetting { get; set; }

    [JsonPropertyName("lastLoopRunDate")]
    public DateTimeOffset? LastLoopRunDate { get; set; }

    [JsonPropertyName("lastLoopError")]
    public string? LastLoopError { get; set; }
}

public class TwiistEvent
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("source")]
    public string? Source { get; set; }

    [JsonPropertyName("timestamp")]
    public DateTimeOffset? Timestamp { get; set; }
}

public class TwiistInsulinDose
{
    [JsonPropertyName("identifier")]
    public string? Identifier { get; set; }

    [JsonPropertyName("doseType")]
    public string? DoseType { get; set; }

    [JsonPropertyName("startDate")]
    public DateTimeOffset? StartDate { get; set; }

    [JsonPropertyName("endDate")]
    public DateTimeOffset? EndDate { get; set; }

    [JsonPropertyName("value")]
    [JsonConverter(typeof(TwiistNullableDecimalConverter))]
    public decimal? Value { get; set; }

    [JsonPropertyName("valueUnit")]
    public string? ValueUnit { get; set; }
}

public class TwiistMeal
{
    [JsonPropertyName("addedDate")]
    public DateTimeOffset? AddedDate { get; set; }

    [JsonPropertyName("startDate")]
    public DateTimeOffset? StartDate { get; set; }

    [JsonPropertyName("absorptionTime_seconds")]
    [JsonConverter(typeof(TwiistNullableDecimalConverter))]
    public decimal? AbsorptionTimeSeconds { get; set; }

    [JsonPropertyName("associatedBolusIDs")]
    public List<string>? AssociatedBolusIds { get; set; }

    [JsonPropertyName("foodType")]
    public string? FoodType { get; set; }

    [JsonPropertyName("grams")]
    [JsonConverter(typeof(TwiistNullableDecimalConverter))]
    public decimal? Grams { get; set; }
}
