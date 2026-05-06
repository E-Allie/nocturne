using Nocturne.Connectors.Twiist.Models;

namespace Nocturne.Connectors.Twiist.Services;

public static class TwiistPwdSelector
{
    public static Guid SelectPatientId(string? configuredPatientId, IReadOnlyList<TwiistPackage> overviews)
    {
        if (!string.IsNullOrWhiteSpace(configuredPatientId))
        {
            if (!Guid.TryParse(configuredPatientId, out var patientId))
            {
                throw new InvalidOperationException(
                    "Twiist PatientId must be a valid PWD UUID from /pwd/overviews.");
            }

            return patientId;
        }

        return overviews.Count switch
        {
            1 => overviews[0].PwdId,
            0 => throw new InvalidOperationException(
                "Twiist follower account has no visible PWDs; check that it follows a PWD."),
            _ => throw new InvalidOperationException(
                "Twiist follower account has multiple visible PWDs; set PatientId to the desired PWD UUID.")
        };
    }
}
