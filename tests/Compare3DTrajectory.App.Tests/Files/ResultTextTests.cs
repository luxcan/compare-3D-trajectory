using System.Globalization;
using Compare3DTrajectory.App.Files;
using Compare3DTrajectory.Core.Alignment;
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
}
