using InstaVende.Core.Domain;

namespace InstaVende.UnitTests.Domain;

public class BusinessLocaleTests
{
    [Theory]
    [InlineData("PEN - Sol Peruano", "PEN", "S/")]
    [InlineData("USD - Dólar Americano", "USD", "US$")]
    [InlineData("MXN - Peso Mexicano", "MXN", "MX$")]
    [InlineData(null, "PEN", "S/")]
    [InlineData("", "PEN", "S/")]
    [InlineData("valor raro", "PEN", "S/")]
    public void Moneda_SeInterpretaDesdeElValorGuardado(string? stored, string code, string symbol)
    {
        Assert.Equal(code, BusinessLocale.CurrencyCode(stored));
        Assert.Equal(symbol, BusinessLocale.CurrencySymbol(stored));
    }

    [Fact]
    public void FormatMoney_UsaSimboloYDosDecimales()
        => Assert.Equal("S/ 1,250.50", BusinessLocale.FormatMoney(1250.5m, "PEN - Sol Peruano"));

    [Fact]
    public void Lima_EsUtcMenos5_SinHorarioDeVerano()
    {
        var utc = new DateTime(2026, 7, 1, 17, 30, 0, DateTimeKind.Utc);

        var local = BusinessLocale.ToLocal(utc, "Perú");

        Assert.Equal(new DateTime(2026, 7, 1, 12, 30, 0), local);
        Assert.Equal(utc, BusinessLocale.ToUtc(local, "Perú"));
    }

    [Fact]
    public void PaisDesconocido_UsaLimaPorDefecto()
        => Assert.Equal(BusinessLocale.TimeZone("Perú").BaseUtcOffset, BusinessLocale.TimeZone("Atlántida").BaseUtcOffset);

    [Fact]
    public void StartOfTodayUtc_CorrespondeALaMedianocheLocal()
    {
        var start = BusinessLocale.StartOfTodayUtc("Perú");

        Assert.Equal(DateTimeKind.Utc, start.Kind);
        Assert.Equal(TimeSpan.Zero, BusinessLocale.ToLocal(start, "Perú").TimeOfDay);
    }
}
