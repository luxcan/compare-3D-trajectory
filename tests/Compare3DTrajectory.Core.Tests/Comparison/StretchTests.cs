using Compare3DTrajectory.Core.Comparison;

namespace Compare3DTrajectory.Core.Tests.Comparison;

public class StretchTests
{
    [Fact]
    public void Find_NoiseHoveringAtTolerance_StaysOneStretch()
    {
        double[] distances = [0.5, 2.1, 1.9, 2.05, 1.95, 2.2, 1.7, 2.1, 0.5, 0.4];

        List<Stretch> found = Stretch.Find(distances, index => index * 10.0, tolerance: 2);

        found.Should().Equal(new Stretch(1, 7));
    }

    [Fact]
    public void Find_TwoExcursionsWithAShortGoodGap_AreMergedIntoOne()
    {
        double[] distances = [0, 5, 5, 0, 5, 5, 0];

        // Points 1 m apart, tolerance 2 m: the 2 m of good path between is too short to count.
        Stretch.Find(distances, index => index, tolerance: 2.5).Should().Equal(new Stretch(1, 5));
    }

    [Fact]
    public void Find_TwoExcursionsWithALongGoodGap_StayApart()
    {
        double[] distances = [0, 5, 5, 0, 5, 5, 0];

        Stretch.Find(distances, index => index * 100.0, tolerance: 2.5).Should().Equal(new Stretch(1, 2), new Stretch(4, 5));
    }

    [Fact]
    public void Find_EndsOffThePath_StretchRunsToTheLastPoint()
    {
        Stretch.Find([0, 0, 3, 4], index => index, tolerance: 1).Should().Equal(new Stretch(2, 3));
    }

    /// <summary>
    /// F27: a run that strays briefly and then goes on steadily just inside tolerance, but above 80%
    /// of it, never ended its stretch: three points became an issue to the end of the run.
    /// </summary>
    [Fact]
    public void Find_ShortExcursionThenSteadyJustInsideTolerance_EndsAtTheExcursion()
    {
        double[] distances = [.. Enumerable.Range(0, 1001).Select(i => i is >= 10 and <= 12 ? 2.5 : 1.8)];

        Stretch.Find(distances, index => index, tolerance: 2).Should().Equal(new Stretch(10, 12));
    }

    [Fact]
    public void Find_AllWithinTolerance_FindsNothing()
    {
        Stretch.Find([0, 0.5, 1, 0.99], index => index, tolerance: 1).Should().BeEmpty();
    }
}
