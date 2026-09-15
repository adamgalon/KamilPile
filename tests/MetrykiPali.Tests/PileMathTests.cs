namespace MetrykiPali.Tests;

public class PileMathTests
{
    [Fact]
    public void TheoreticalVolume_is_the_shaft_cylinder()
    {
        // pi/4 * 0.4^2 * 9 = 1.1310 m3
        Assert.Equal(1.1310, PileMath.TheoreticalVolume(0.4, 9), 4);
        Assert.Equal(0.0, PileMath.TheoreticalVolume(0.4, 0), 6);
    }

    /// <summary>
    /// These are the volumes printed in the reference documentation for a 0.4 m
    /// CFA pile. If the formula or the default coefficient ever drifts, the
    /// generated metryki stop matching the paperwork they are modelled on.
    /// </summary>
    [Theory]
    [InlineData(6, 0.98)]
    [InlineData(7, 1.14)]
    [InlineData(8, 1.31)]
    [InlineData(9, 1.47)]
    [InlineData(10, 1.63)]
    [InlineData(11, 1.80)]
    [InlineData(12, 1.96)]
    public void Concrete_matches_the_reference_documentation(double length, double expected)
        => Assert.Equal(expected, PileMath.Concrete(0.4, length, 1.30));

    [Fact]
    public void Concrete_is_rounded_to_two_decimals()
    {
        var value = PileMath.Concrete(0.4, 9, 1.30);
        Assert.Equal(value, Math.Round(value, 2));
    }

    [Fact]
    public void Concrete_rounds_half_away_from_zero()
    {
        // pi/4 * 0.4^2 * L * k chosen so the third decimal is exactly 5.
        // Banker's rounding would give 1.12 here; the metryki use 1.13.
        const double target = 1.125;
        var length = target / (Math.PI * 0.4 * 0.4 / 4.0);
        Assert.Equal(1.13, PileMath.Concrete(0.4, length, 1.0));
    }

    [Fact]
    public void Concrete_scales_with_the_coefficient()
    {
        var plain = PileMath.Concrete(0.4, 10, 1.0);
        var withOverbreak = PileMath.Concrete(0.4, 10, 1.30);
        Assert.True(withOverbreak > plain);
        Assert.Equal(1.26, plain);
    }

    [Fact]
    public void Concrete_grows_with_diameter_squared()
    {
        var narrow = PileMath.TheoreticalVolume(0.4, 10);
        var wide = PileMath.TheoreticalVolume(0.8, 10);
        Assert.Equal(4.0, wide / narrow, 6);
    }
}
