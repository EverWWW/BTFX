using BTFX.Helpers;
using BTFX.Models;
using Xunit;

namespace BTFX.Tests;

public sealed class PatientNameSearchMatcherTests
{
    [Fact]
    public void Matches_ReturnsFalse_WhenKeywordOnlyAppearsInPhoneOrIdNumber()
    {
        var patient = new Patient
        {
            Name = "张三",
            Phone = "13811112222",
            IdNumber = "410111199001011111"
        };

        Assert.False(PatientNameSearchMatcher.Matches(patient, "1111"));
    }

    [Fact]
    public void Matches_ReturnsTrue_WhenNameContainsKeywordIgnoringCase()
    {
        var patient = new Patient { Name = "Test Patient" };

        Assert.True(PatientNameSearchMatcher.Matches(patient, "patient"));
    }
}
