using System.Globalization;
using Compare3DTrajectory.App.Files;
using Compare3DTrajectory.Core.Alignment;
using Compare3DTrajectory.Core.Comparison;
using Compare3DTrajectory.Core.Geometry;

namespace Compare3DTrajectory.App.Tests.Files;

public sealed class ResultTextTests : IDisposable
{
    private readonly CultureInfo _culture = CultureInfo.CurrentCulture;

    public ResultTextTests() => CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-SG");

    public void Dispose() => CultureInfo.CurrentCulture = _culture;

    [Theory]
    [InlineData(-35, "turned 35.0° clockwise")]
    [InlineData(12.26, "turned 12.3° anticlockwise")]
    [InlineData(0.01, "no turn")]
    public void Alignment_Fitted_SaysHowFarTheRunWasMovedAndTurned(double degrees, string turn)
    {
        var fit = new HeadingFit(new HeadingTransform(degrees * Math.PI / 180, Vec3.Zero), 0.28, new Vec3(30, 40, 0));

        ResultText.Alignment(fit).Should().Be($"Fitted: moved 50.0 m, {turn} · fit RMS 0.28 m");
    }

    [Fact]
    public void Alignment_NotFitted_SaysSo()
    {
        ResultText.Alignment(HeadingFit.None).Should().Be("Compared where the file puts it (fitting is off)");
    }

    /// <summary>F26: a run that never moves is not fitted, and the row says why rather than that fitting is off.</summary>
    [Fact]
    public void Alignment_RunThatCouldNotBeFitted_SaysWhy()
    {
        HeadingFit fit = HeadingFit.NotFitted("the run never moves, so there is no shape to fit");

        ResultText.Alignment(fit).Should().Be("Compared where the file puts it: the run never moves, so there is no shape to fit");
    }

    /// <summary>F64: only the length of the move was given, so a shift straight up or down read like one sideways.</summary>
    [Theory]
    [InlineData(0, 0, -30, "moved 30.0 m down")]
    [InlineData(0, 0, 69.6, "moved 69.6 m up")]
    [InlineData(400, -250, 3, "moved 471.7 m across and 3.00 m up")]
    [InlineData(30, 40, 0.2, "moved 50.0 m")]
    public void Alignment_Fitted_SplitsTheMoveIntoAcrossAndUpOrDown(double east, double north, double up, string moved)
    {
        var fit = new HeadingFit(HeadingTransform.Identity, 0.28, new Vec3(east, north, up));

        ResultText.Alignment(fit).Should().Be($"Fitted: {moved}, no turn · fit RMS 0.28 m");
    }

    /// <summary>F31: the verdict's "worst" left out missed stretches, so a run that stopped half way read as barely off.</summary>
    [Fact]
    public void Verdict_RunThatStoppedHalfWay_CountsTheMissedStretchInTheWorst()
    {
        var summary = new ComparisonSummary(MaxDeviation: 0.1, MeanDeviation: 0.1, RmsDeviation: 0.1, WithinTolerancePercent: 100, CoveragePercent: 50);
        var missed = new TrajectoryIssue(IssueKind.Missed, 50, 100, 500, 500, Vec3.Zero, [Vec3.Zero]);
        var result = new ComparisonResult(HeadingFit.None, [], [], [missed], summary);

        ResultText.Verdict(result, 2).Should().Be("✗ 1 issue · worst 500.0 m off");
    }
}
