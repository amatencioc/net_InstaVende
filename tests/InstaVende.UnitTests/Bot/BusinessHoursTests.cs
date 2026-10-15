using InstaVende.Web.Services.Bot;

namespace InstaVende.UnitTests.Bot;

public class BusinessHoursTests
{
    // 2026-10-14 es miércoles
    private static DateTime Wed(int h, int m = 0) => new(2026, 10, 14, h, m, 0);
    private static DateTime Sun(int h, int m = 0) => new(2026, 10, 18, h, m, 0);

    [Theory]
    [InlineData("Lunes a Viernes 9:00-18:00", 10, true)]
    [InlineData("Lunes a Viernes 9:00-18:00", 18, false)]
    [InlineData("Lunes a Viernes 9:00-18:00", 8, false)]
    [InlineData("09:00 - 23:30", 23, true)]
    [InlineData("9 a 18", 12, true)]
    public void IsOpen_EnDiaHabil(string hours, int hour, bool expected)
        => Assert.Equal(expected, BusinessHours.IsOpen(hours, Wed(hour)));

    [Fact]
    public void IsOpen_FueraDelRangoDeDias()
        => Assert.False(BusinessHours.IsOpen("Lunes a Viernes 9:00-18:00", Sun(10)));

    [Theory]
    [InlineData(23, true)]
    [InlineData(1, true)]
    [InlineData(3, false)]
    public void IsOpen_RangoQueCruzaMedianoche(int hour, bool expected)
        => Assert.Equal(expected, BusinessHours.IsOpen("20:00-02:00", Wed(hour)));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Atendemos cuando podemos")]
    public void IsOpen_DevuelveNullSiNoSePuedeInterpretar(string? hours)
        => Assert.Null(BusinessHours.IsOpen(hours, Wed(10)));
}
