using FluentAssertions;
using InstaVende.Web.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Moq;

namespace InstaVende.Tests;

// ??????????????????????????????????????????????????????????????????????????????
// Helpers compartidos
// ??????????????????????????????????????????????????????????????????????????????

file static class EnvFactory
{
    public static IHostEnvironment Create(bool isDevelopment)
    {
        var env = new Mock<IHostEnvironment>();
        env.Setup(e => e.EnvironmentName)
           .Returns(isDevelopment ? Environments.Development : Environments.Production);
        return env.Object;
    }
}

// ??????????????????????????????????????????????????????????????????????????????
// OpenAiOptionsValidator
// ??????????????????????????????????????????????????????????????????????????????

public class OpenAiOptionsValidatorTests
{
    private static ValidateOptionsResult Validate(OpenAiOptions opts, bool isDevelopment = false)
    {
        var validator = new OpenAiOptionsValidatorAccessor(EnvFactory.Create(isDevelopment));
        return validator.Validate(null, opts);
    }

    [Fact]
    public void ApiKey_Empty_Development_ReturnsSuccess()
    {
        var result = Validate(new OpenAiOptions { ApiKey = string.Empty }, isDevelopment: true);
        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void ApiKey_Empty_Production_ReturnsFail()
    {
        var result = Validate(new OpenAiOptions { ApiKey = string.Empty }, isDevelopment: false);
        result.Succeeded.Should().BeFalse();
        result.FailureMessage.Should().Contain("OpenAI:ApiKey");
    }

    [Fact]
    public void ApiKey_Whitespace_Production_ReturnsFail()
    {
        var result = Validate(new OpenAiOptions { ApiKey = "   " }, isDevelopment: false);
        result.Succeeded.Should().BeFalse();
        result.FailureMessage.Should().Contain("OpenAI:ApiKey");
    }

    [Fact]
    public void ApiKey_Valid_Model_Valid_Production_ReturnsSuccess()
    {
        var result = Validate(
            new OpenAiOptions { ApiKey = "sk-real-key", Model = "gpt-4o" },
            isDevelopment: false);
        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void ApiKey_Valid_Model_Empty_Production_ReturnsFail()
    {
        var result = Validate(
            new OpenAiOptions { ApiKey = "sk-real-key", Model = string.Empty },
            isDevelopment: false);
        result.Succeeded.Should().BeFalse();
        result.FailureMessage.Should().Contain("OpenAI:Model");
    }

    [Fact]
    public void ApiKey_Valid_Model_Whitespace_Production_ReturnsFail()
    {
        var result = Validate(
            new OpenAiOptions { ApiKey = "sk-real-key", Model = "  " },
            isDevelopment: false);
        result.Succeeded.Should().BeFalse();
        result.FailureMessage.Should().Contain("OpenAI:Model");
    }
}

// ??????????????????????????????????????????????????????????????????????????????
// HttpClientOptionsValidator
// ??????????????????????????????????????????????????????????????????????????????

public class HttpClientOptionsValidatorTests
{
    private static ValidateOptionsResult Validate(HttpClientOptions opts, string clientName = "wa-test")
    {
        // HttpClientOptionsValidator aplica en todos los entornos — no hay parámetro de entorno.
        var validator = new HttpClientOptionsValidatorAccessor(clientName);
        return validator.Validate(null, opts);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void TimeoutSeconds_ZeroOrNegative_ReturnsFail(int timeout)
    {
        var result = Validate(new HttpClientOptions { TimeoutSeconds = timeout });
        result.Succeeded.Should().BeFalse();
        result.FailureMessage.Should().Contain("TimeoutSeconds");
        result.FailureMessage.Should().Contain("wa-test");
        result.FailureMessage.Should().Contain(timeout.ToString());
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(30)]
    [InlineData(int.MaxValue)]
    public void TimeoutSeconds_Positive_ReturnsSuccess(int timeout)
    {
        var result = Validate(new HttpClientOptions { TimeoutSeconds = timeout });
        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void FailureMessage_ContainsClientName()
    {
        var result = Validate(new HttpClientOptions { TimeoutSeconds = 0 }, clientName: "wa-health");
        result.FailureMessage.Should().Contain("wa-health");
    }

    [Fact]
    public void FailureMessage_ContainsEnvironmentVariableHint()
    {
        var result = Validate(new HttpClientOptions { TimeoutSeconds = -5 }, clientName: "wa-send");
        result.FailureMessage.Should().Contain("wa-send");
        // El mensaje debe orientar al operador sobre la variable de entorno equivalente
        result.FailureMessage.Should().Contain("appsettings.json");
    }
}

// ??????????????????????????????????????????????????????????????????????????????
// Accessors — exponen los validadores internos (internal sealed) para los tests
// ??????????????????????????????????????????????????????????????????????????????

// Los validadores son 'internal sealed'; se accede a ellos a través de subclases
// en el mismo assembly de pruebas mediante [assembly: InternalsVisibleTo(...)].
// Como alternativa sin modificar el ensamblado principal, usamos la reflexión
// solo para instanciar; la llamada a Validate es directa vía la interfaz pública.

file sealed class OpenAiOptionsValidatorAccessor : IValidateOptions<OpenAiOptions>
{
    private readonly IValidateOptions<OpenAiOptions> _inner;

    public OpenAiOptionsValidatorAccessor(IHostEnvironment env)
    {
        // Instanciamos mediante reflexión para no romper el encapsulamiento 'internal'.
        var type = typeof(InstaVende.Web.Services.OpenAiOptions).Assembly
            .GetType("InstaVende.Web.Services.OpenAiOptionsValidator")!;
        _inner = (IValidateOptions<OpenAiOptions>)Activator.CreateInstance(type, env)!;
    }

    public ValidateOptionsResult Validate(string? name, OpenAiOptions options)
        => _inner.Validate(name, options);
}

file sealed class HttpClientOptionsValidatorAccessor : IValidateOptions<HttpClientOptions>
{
    private readonly IValidateOptions<HttpClientOptions> _inner;

    public HttpClientOptionsValidatorAccessor(string clientName)
    {
        var type = typeof(InstaVende.Web.Services.HttpClientOptions).Assembly
            .GetType("InstaVende.Web.Services.HttpClientOptionsValidator")!;
        _inner = (IValidateOptions<HttpClientOptions>)Activator.CreateInstance(type, clientName)!;
    }

    public ValidateOptionsResult Validate(string? name, HttpClientOptions options)
        => _inner.Validate(name, options);
}
