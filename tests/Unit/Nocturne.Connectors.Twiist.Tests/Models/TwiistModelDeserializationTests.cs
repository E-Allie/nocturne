using System.Text.Json;
using FluentAssertions;
using Nocturne.Connectors.Twiist.Models;
using Xunit;

namespace Nocturne.Connectors.Twiist.Tests.Models;

public class TwiistModelDeserializationTests
{
    [Fact]
    public void Overview_WithStringDecimalFields_Deserializes()
    {
        const string json = """
        [{
          "pwdId":"72826af6-68c9-4d6f-8f98-2ec2b07acda3",
          "pwdNickname":"Test Person",
          "status":{
            "date":"2026-04-19T05:54:01Z",
            "summary":{
              "glucoseDate":"2026-04-19T05:53:41Z",
              "glucoseUnit":"mg/dL",
              "cgmRateArrow":"→",
              "isBasalActive":true,
              "loopRingColor":"green",
              "glucoseQuantity":"289.0",
              "pumpBatteryLevel":"0.67",
              "closedLoopEnabled":true,
              "pumpEventsComplete":true,
              "glucoseHighlightState":"NoHighlight",
              "netBasal_UnitsPerHour":"0.32",
              "lastCassetteChangeDate":"2026-04-16T22:04:29Z",
              "pumpCassetteVolume_Units":"145.4",
              "maximumBasalRate_UnitsPerHour":"3.0",
              "pumpCassetteFilledVolume_Units":"250.0"
            }
          }
        }]
        """;

        var overviews = JsonSerializer.Deserialize<List<TwiistPackage>>(json, TwiistJson.Options);

        overviews.Should().ContainSingle();
        var summary = overviews![0].Status.Summary!;
        overviews[0].PwdNickname.Should().Be("Test Person");
        summary.GlucoseQuantity.Should().Be(289.0m);
        summary.PumpBatteryLevel.Should().Be(0.67m);
        summary.NetBasalUnitsPerHour.Should().Be(0.32m);
        summary.PumpCassetteVolumeUnits.Should().Be(145.4m);
        summary.ClosedLoopEnabled.Should().BeTrue();
    }

    [Fact]
    public void InsulinAndMealHistory_WithStringDecimals_Deserializes()
    {
        const string json = """
        {
          "identifier":"dose-xyz",
          "doseType":"bolus",
          "startDate":"2026-04-19T11:50:00Z",
          "endDate":"2026-04-19T11:50:00Z",
          "value":"1.5",
          "valueUnit":"IU"
        }
        """;

        var dose = JsonSerializer.Deserialize<TwiistInsulinDose>(json, TwiistJson.Options);

        dose!.Value.Should().Be(1.5m);

        const string mealJson = """
        {
          "addedDate":"2026-04-19T12:00:00Z",
          "startDate":"2026-04-19T12:05:00Z",
          "absorptionTime_seconds":"7200",
          "associatedBolusIDs":["dose-xyz"],
          "foodType":"meal",
          "grams":"42.5"
        }
        """;

        var meal = JsonSerializer.Deserialize<TwiistMeal>(mealJson, TwiistJson.Options);

        meal!.AbsorptionTimeSeconds.Should().Be(7200m);
        meal.Grams.Should().Be(42.5m);
        meal.AssociatedBolusIds.Should().ContainSingle("dose-xyz");
    }
}
