using FluentAssertions;
using Nocturne.Connectors.Twiist.Models;
using Nocturne.Connectors.Twiist.Services;
using Xunit;

namespace Nocturne.Connectors.Twiist.Tests.Services;

public class TwiistPwdSelectorTests
{
    private static readonly Guid PatientA = Guid.Parse("72826af6-68c9-4d6f-8f98-2ec2b07acda3");
    private static readonly Guid PatientB = Guid.Parse("dd34e56f-2bd8-45c2-97e5-48d421dc487c");

    [Fact]
    public void SelectPatientId_WhenExplicitPatientId_ReturnsConfiguredUuid()
    {
        var selected = TwiistPwdSelector.SelectPatientId(PatientB.ToString(), [Package(PatientA)]);

        selected.Should().Be(PatientB);
    }

    [Fact]
    public void SelectPatientId_WhenSingleOverviewAndNoConfig_AutoSelects()
    {
        var selected = TwiistPwdSelector.SelectPatientId(null, [Package(PatientA)]);

        selected.Should().Be(PatientA);
    }

    [Fact]
    public void SelectPatientId_WhenMultipleOverviewsAndNoConfig_ThrowsClearError()
    {
        var act = () => TwiistPwdSelector.SelectPatientId("", [Package(PatientA), Package(PatientB)]);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*multiple visible PWDs*PatientId*");
    }

    private static TwiistPackage Package(Guid id) => new()
    {
        PwdId = id,
        PwdNickname = id.ToString()
    };
}
