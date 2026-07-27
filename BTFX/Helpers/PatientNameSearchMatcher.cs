using BTFX.Models;

namespace BTFX.Helpers;

internal static class PatientNameSearchMatcher
{
    internal static bool Matches(Patient patient, string? searchText)
    {
        ArgumentNullException.ThrowIfNull(patient);

        if (string.IsNullOrWhiteSpace(searchText))
        {
            return true;
        }

        return patient.Name.Contains(searchText.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}
