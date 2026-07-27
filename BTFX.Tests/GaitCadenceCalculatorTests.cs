using BTFX.Helpers;
using BTFX.Services.Implementations;
using Xunit;

namespace BTFX.Tests;

public sealed class GaitCadenceCalculatorTests
{
    [Fact]
    public void CalculateFromFullCycle_UsesTwoStepsPerCycle()
    {
        var cadence = GaitCadenceCalculator.CalculateFromFullCycle(1.53);

        Assert.NotNull(cadence);
        Assert.Equal(78.431, cadence.Value, precision: 3);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0d)]
    [InlineData(-1d)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void CalculateFromFullCycle_ReturnsNullForInvalidDuration(double? cycleDurationSeconds)
    {
        Assert.Null(GaitCadenceCalculator.CalculateFromFullCycle(cycleDurationSeconds));
    }

    [Fact]
    public void PreferCycleDerived_FallsBackOnlyWhenCycleIsUnavailable()
    {
        Assert.Equal(80, GaitCadenceCalculator.PreferCycleDerived(1.5, 44.9));
        Assert.Equal(44.9, GaitCadenceCalculator.PreferCycleDerived(null, 44.9));
    }

    [Fact]
    public async Task AnalysisOutputReader_PrefersCycleDerivedCadenceOverAlgorithmValue()
    {
        var outputDirectory = Path.Combine(Path.GetTempPath(), "btfx-cadence-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outputDirectory);

        try
        {
            await File.WriteAllTextAsync(
                Path.Combine(outputDirectory, "result.json"),
                """
                {
                  "task_id": "CADENCE_TEST",
                  "video_info": { "fps": 60, "duration_sec": 10, "frame_count": 600 },
                  "gait_cycle": {
                    "left_cycles": [
                      { "cycle_id": 1, "start_frame": 60, "end_frame": 150, "duration_sec": 1.5 }
                    ],
                    "right_cycles": [
                      { "cycle_id": 1, "start_frame": 105, "end_frame": 195, "duration_sec": 1.5 }
                    ]
                  },
                  "spatiotemporal_parameters": {
                    "cadence_step_per_min": 44.9
                  }
                }
                """);

            var output = await new AnalysisOutputReader().ReadAsync(outputDirectory);

            Assert.Equal(1.5, output.Summary.GaitEventParameters?.GaitCycleDurationS);
            Assert.Equal(80, output.Summary.GaitEventParameters?.CadenceStepPerMin);
        }
        finally
        {
            Directory.Delete(outputDirectory, recursive: true);
        }
    }
}
