using System.Net;
using System.Security.Claims;
using System.Text.Json;
using FluentAssertions;
using InstaVende.Core.Entities;
using InstaVende.Core.Enums;
using InstaVende.Infrastructure.Data;
using InstaVende.Infrastructure.Services;
using InstaVende.Web.Controllers;
using InstaVende.Web.Services;
using InstaVende.Web.ViewModels;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;

namespace InstaVende.Tests;

// ??????????????????????????????????????????????????????????????????????????????
// Infraestructura compartida
// ??????????????????????????????????????????????????????????????????????????????

/// <summary>
/// Fábrica que construye un <see cref="ChannelConfigController"/> con todas sus
/// dependencias bajo control total del test.
/// </summary>
file sealed class ControllerFixture : IDisposable
{
    // ?? Dependencias reales ???????????????????????????????????????????????????
    public AppDbContext             Db       { get; }
    public MemoryCache              Cache    { get; }

    // ?? Mocks controlables desde los tests ???????????????????????????????????
    public Mock<HttpMessageHandler> HttpHandler { get; } = new(); // Loose — la factory crea respuestas frescas

    // ?? El controlador bajo prueba ????????????????????????????????????????????
    public ChannelConfigController  Sut      { get; }

    public ControllerFixture(
        int?   businessId      = 1,
        bool   waNodeOnline    = false,
        string waNodeResponse  = "{\"state\":\"connected\",\"connected\":true,\"qrDataUrl\":null,\"qrExpiresAt\":null,\"info\":null}",
        bool   waNodeThrows    = false)
    {
        // ?? Base de datos en memoria ??????????????????????????????????????????
        Db = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);

        // ?? Caché ?????????????????????????????????????????????????????????????
        Cache = new MemoryCache(new MemoryCacheOptions());

        // ?? HttpMessageHandler simulado ???????????????????????????????????????
        if (waNodeThrows)
        {
            HttpHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ThrowsAsync(new HttpRequestException("Node offline"));
        }
        else
        {
            var sc = waNodeOnline ? HttpStatusCode.OK : HttpStatusCode.ServiceUnavailable;
            // Usar una factory lambda para generar una nueva respuesta en cada llamada
            // y evitar ObjectDisposedException cuando el controlador hace múltiples
            // peticiones HTTP (WaIsReachableAsync + WaStatus en el mismo test).
            HttpHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(() =>
                {
                    var content = waNodeOnline
                        ? new StringContent(waNodeResponse, System.Text.Encoding.UTF8, "application/json")
                        : new StringContent(string.Empty);
                    return new HttpResponseMessage(sc) { Content = content };
                });
        }

        var httpClient = new HttpClient(HttpHandler.Object)
            { Timeout = TimeSpan.FromSeconds(2) };

        // IHttpClientFactory que siempre devuelve el mismo cliente configurado
        var httpFactory = new Mock<IHttpClientFactory>();
        httpFactory
            .Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(httpClient);

        // ?? CurrentUserService ????????????????????????????????????????????????
        var httpContext = new DefaultHttpContext();
        if (businessId.HasValue)
        {
            // Necesitamos un Business en DB para que GetBusinessIdAsync funcione.
            // CurrentUserService busca por UserId del claim.
            const string userId = "test-user-1";
            Db.Businesses.Add(new Business { Id = businessId.Value, UserId = userId, Name = "TestBiz" });
            Db.SaveChanges();

            httpContext.User = new ClaimsPrincipal(
                new ClaimsIdentity(
                    new[] { new Claim(ClaimTypes.NameIdentifier, userId) },
                    "TestAuth"));
        }

        var httpContextAccessor = new Mock<IHttpContextAccessor>();
        httpContextAccessor.Setup(a => a.HttpContext).Returns(httpContext);
        var cu = new CurrentUserService(httpContextAccessor.Object, Db);

        // ?? DataProtectionService (real, proveedor efímero) ???????????????????
        // EphemeralDataProtectionProvider no persiste claves — apto para tests.
        var dpProvider = new EphemeralDataProtectionProvider();
        var dp = new DataProtectionService(dpProvider, NullLogger<DataProtectionService>.Instance);

        // ?? WhatsAppClientOptions ?????????????????????????????????????????????
        var waOpts = Options.Create(new WhatsAppClientOptions
        {
            BaseUrl    = "http://localhost:3001",
            ClientPath = Path.GetTempPath(),   // ruta válida para los tests de WaRestart
            AutoStart  = false,
        });

        // ?? Logger ????????????????????????????????????????????????????????????
        var logger = Mock.Of<ILogger<ChannelConfigController>>();

        // ?? WaClientHostedService (AutoStart=false — no lanza ningún proceso) ???
        var waService = new WaClientHostedService(
            waOpts,
            NullLogger<WaClientHostedService>.Instance,
            httpFactory.Object);

        // ?? Controlador ???????????????????????????????????????????????????????
        Sut = new ChannelConfigController(Db, cu, dp, waOpts, httpFactory.Object, logger, Cache, waService);
        Sut.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext,
        };
        Sut.TempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>());
    }

    public void Dispose() { Db.Dispose(); Cache.Dispose(); }
}

// ??????????????????????????????????????????????????????????????????????????????
// WaStatus
// ??????????????????????????????????????????????????????????????????????????????

public class WaStatusTests
{
    // ?? Caché offline activa ? respuesta inmediata sin llamar a Node ??????????

    [Fact]
    public async Task WaStatus_OfflineCacheActive_ReturnsOfflineJsonWithoutCallingNode()
    {
        using var fix = new ControllerFixture(waNodeOnline: true); // Node estaría activo
        fix.Cache.Set("wa_offline", true, TimeSpan.FromSeconds(30));

        var result = (await fix.Sut.WaStatus()) as ContentResult;

        result.Should().NotBeNull();
        result!.ContentType.Should().Be("application/json");

        var doc = JsonDocument.Parse(result.Content!);
        doc.RootElement.GetProperty("connected").GetBoolean().Should().BeFalse();
        doc.RootElement.GetProperty("state").GetString().Should().Be("disconnected");

        // El handler NO debe haberse invocado
        fix.HttpHandler.Protected()
            .Verify("SendAsync", Times.Never(),
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>());
    }

    // ?? Node responde 200 con JSON válido ? lo reenvía tal cual ??????????????

    [Fact]
    public async Task WaStatus_NodeOnline_ForwardsNodeJson()
    {
        const string nodeJson = "{\"state\":\"qr\",\"connected\":false,\"qrDataUrl\":\"data:image/png;base64,ABC\",\"qrExpiresAt\":\"2099-01-01T00:00:00Z\",\"info\":null}";
        using var fix = new ControllerFixture(waNodeOnline: true, waNodeResponse: nodeJson);

        var result = (await fix.Sut.WaStatus()) as ContentResult;

        result!.ContentType.Should().Be("application/json");
        result.Content.Should().Be(nodeJson);
    }

    // ?? Node responde 503 ? devuelve offline y activa la caché ???????????????

    [Fact]
    public async Task WaStatus_NodeReturns503_ReturnsOfflineAndSetsCache()
    {
        using var fix = new ControllerFixture(waNodeOnline: false);

        var result = (await fix.Sut.WaStatus()) as ContentResult;

        var doc = JsonDocument.Parse(result!.Content!);
        doc.RootElement.GetProperty("connected").GetBoolean().Should().BeFalse();

        fix.Cache.TryGetValue("wa_offline", out _).Should().BeTrue(
            "SetOfflineCache debe activarse cuando Node devuelve un código de error");
    }

    // ?? Node lanza HttpRequestException ? devuelve offline y activa la caché ?

    [Fact]
    public async Task WaStatus_NodeThrowsHttpRequestException_ReturnsOfflineAndSetsCache()
    {
        using var fix = new ControllerFixture(waNodeThrows: true);

        var result = (await fix.Sut.WaStatus()) as ContentResult;

        result!.ContentType.Should().Be("application/json");
        var doc = JsonDocument.Parse(result.Content!);
        doc.RootElement.GetProperty("connected").GetBoolean().Should().BeFalse();

        fix.Cache.TryGetValue("wa_offline", out _).Should().BeTrue();
    }

    // ?? Node lanza TaskCanceledException (timeout) ? mismo comportamiento ????

    [Fact]
    public async Task WaStatus_NodeThrowsTaskCanceledException_ReturnsOfflineAndSetsCache()
    {
        var handler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new TaskCanceledException("timeout"));

        var httpClient  = new HttpClient(handler.Object) { Timeout = TimeSpan.FromSeconds(2) };
        var httpFactory = new Mock<IHttpClientFactory>();
        httpFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        // Construimos un fixture base y reemplazamos solo el factory
        using var fix = new ControllerFixture(waNodeOnline: false); // handler dummy
        // Reconstruimos solo el controlador con el factory que lanza TaskCanceledException
        var waOpts  = Options.Create(new WhatsAppClientOptions { BaseUrl = "http://localhost:3001", ClientPath = Path.GetTempPath() });
        var logger  = Mock.Of<ILogger<ChannelConfigController>>();
        var dp      = new DataProtectionService(new EphemeralDataProtectionProvider(), NullLogger<DataProtectionService>.Instance);
        var httpCtx = new DefaultHttpContext();
        var httpCtxAcc = new Mock<IHttpContextAccessor>();
        httpCtxAcc.Setup(a => a.HttpContext).Returns(httpCtx);
        var cu  = new CurrentUserService(httpCtxAcc.Object, fix.Db);
        var waService = new WaClientHostedService(waOpts, NullLogger<WaClientHostedService>.Instance, httpFactory.Object);
        var sut = new ChannelConfigController(fix.Db, cu, dp, waOpts, httpFactory.Object, logger, fix.Cache, waService);
        sut.ControllerContext = new ControllerContext { HttpContext = httpCtx };

        var result = (await sut.WaStatus()) as ContentResult;

        result!.ContentType.Should().Be("application/json");
        fix.Cache.TryGetValue("wa_offline", out _).Should().BeTrue();
    }

    // ?? Segundo poll con caché offline ? no llama a Node (cero peticiones) ???

    [Fact]
    public async Task WaStatus_CalledTwiceWhileOffline_CallsNodeOnlyOnce()
    {
        using var fix = new ControllerFixture(waNodeOnline: false);

        await fix.Sut.WaStatus(); // primera llamada: Node devuelve 503 ? activa caché
        await fix.Sut.WaStatus(); // segunda llamada: caché activa ? no llama a Node

        fix.HttpHandler.Protected()
            .Verify("SendAsync", Times.Once(),
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>());
    }
}

// ??????????????????????????????????????????????????????????????????????????????
// WaRestart
// ??????????????????????????????????????????????????????????????????????????????

public class WaRestartTests
{
    // ?? Node ya está activo ? responde "ya estaba corriendo" y limpia caché ??

    [Fact]
    public async Task WaRestart_NodeAlreadyRunning_ReturnsAlreadyRunningMessage()
    {
        using var fix = new ControllerFixture(waNodeOnline: true,
            waNodeResponse: "{\"ok\":true,\"state\":\"connected\",\"uptime\":42}");
        fix.Cache.Set("wa_offline", true, TimeSpan.FromSeconds(30)); // simula caché previa

        var result = (await fix.Sut.WaRestart()) as JsonResult;

        result.Should().NotBeNull();
        var json = JsonSerializer.Serialize(result!.Value);
        var doc  = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("ok").GetBoolean().Should().BeTrue();
        doc.RootElement.GetProperty("message").GetString()
            .Should().Contain("ya estaba corriendo");

        fix.Cache.TryGetValue("wa_offline", out _).Should().BeFalse(
            "WaRestart debe eliminar la caché offline cuando el servicio ya corre");
    }

    // ?? Directorio wa-client no existe ? devuelve ok=false con mensaje claro ?

    [Fact]
    public async Task WaRestart_WaClientDirMissing_ReturnsFalseWithPathMessage()
    {
        var missingPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        using var fix   = new ControllerFixture(
            waNodeOnline: false,
            waNodeResponse: string.Empty);

        // Redirigir ClientPath a un directorio que no existe
        var waOpts     = Options.Create(new WhatsAppClientOptions { BaseUrl = "http://localhost:3001", ClientPath = missingPath });
        var logger     = Mock.Of<ILogger<ChannelConfigController>>();
        var dp         = new DataProtectionService(new EphemeralDataProtectionProvider(), NullLogger<DataProtectionService>.Instance);
        var httpCtx    = new DefaultHttpContext();
        var httpCtxAcc = new Mock<IHttpContextAccessor>();
        httpCtxAcc.Setup(a => a.HttpContext).Returns(httpCtx);
        var cu  = new CurrentUserService(httpCtxAcc.Object, fix.Db);

        // Nuevo IHttpClientFactory que simula Node offline
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        var httpClient  = new HttpClient(handler.Object) { Timeout = TimeSpan.FromSeconds(2) };
        var httpFactory = new Mock<IHttpClientFactory>();
        httpFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var waService = new WaClientHostedService(waOpts, NullLogger<WaClientHostedService>.Instance, httpFactory.Object);
        var sut = new ChannelConfigController(fix.Db, cu, dp, waOpts, httpFactory.Object, logger, fix.Cache, waService);
        sut.ControllerContext = new ControllerContext { HttpContext = httpCtx };

        var result = (await sut.WaRestart()) as JsonResult;
        var json   = JsonSerializer.Serialize(result!.Value);
        var doc    = JsonDocument.Parse(json);

        doc.RootElement.GetProperty("ok").GetBoolean().Should().BeFalse();
        doc.RootElement.GetProperty("message").GetString()
            .Should().Contain("no encontrado");
    }

    // ?? index.js no existe dentro del directorio ? ok=false ??????????????????

    [Fact]
    public async Task WaRestart_IndexJsMissing_ReturnsFalse()
    {
        // Directorio real pero vacío (sin index.js)
        var emptyDir = Directory.CreateTempSubdirectory("wa_test_").FullName;
        try
        {
            using var fix = new ControllerFixture(waNodeOnline: false);

            var waOpts     = Options.Create(new WhatsAppClientOptions { BaseUrl = "http://localhost:3001", ClientPath = emptyDir });
            var logger     = Mock.Of<ILogger<ChannelConfigController>>();
            var dp         = new DataProtectionService(new EphemeralDataProtectionProvider(), NullLogger<DataProtectionService>.Instance);
            var httpCtx    = new DefaultHttpContext();
            var httpCtxAcc = new Mock<IHttpContextAccessor>();
            httpCtxAcc.Setup(a => a.HttpContext).Returns(httpCtx);
            var cu = new CurrentUserService(httpCtxAcc.Object, fix.Db);

            var handler = new Mock<HttpMessageHandler>();
            handler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(() => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
            var httpClient  = new HttpClient(handler.Object) { Timeout = TimeSpan.FromSeconds(2) };
            var httpFactory = new Mock<IHttpClientFactory>();
            httpFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

            var waService = new WaClientHostedService(waOpts, NullLogger<WaClientHostedService>.Instance, httpFactory.Object);
            var sut = new ChannelConfigController(fix.Db, cu, dp, waOpts, httpFactory.Object, logger, fix.Cache, waService);
            sut.ControllerContext = new ControllerContext { HttpContext = httpCtx };

            var result = (await sut.WaRestart()) as JsonResult;
            var json   = JsonSerializer.Serialize(result!.Value);
            var doc    = JsonDocument.Parse(json);

            doc.RootElement.GetProperty("ok").GetBoolean().Should().BeFalse();
            doc.RootElement.GetProperty("message").GetString()
                .Should().Contain("index.js");
        }
        finally
        {
            Directory.Delete(emptyDir, recursive: true);
        }
    }

    // ?? Directorio y index.js existen ? ok=true y caché offline eliminada ????
    // No lanzamos Node real; verificamos solo la respuesta del controlador
    // al encontrar los archivos necesarios (el proceso arranca y termina en ms
    // porque "node" sin args existiría, pero en CI puede no estar instalado).
    // Por eso creamos el index.js y verificamos el comportamiento hasta la
    // llamada a Process.Start; si Node no está, el catch devuelve ok=false
    // con el mensaje esperado, lo cual también es comportamiento correcto.

    [Fact]
    public async Task WaRestart_ValidDir_EitherStartsOrReportsNodeMissing()
    {
        var tmpDir  = Directory.CreateTempSubdirectory("wa_test_ok_").FullName;
        var indexJs = Path.Combine(tmpDir, "index.js");
        try
        {
            await System.IO.File.WriteAllTextAsync(indexJs, "// stub");

            using var fix = new ControllerFixture(waNodeOnline: false);

            var waOpts     = Options.Create(new WhatsAppClientOptions { BaseUrl = "http://localhost:3001", ClientPath = tmpDir });
            var logger     = Mock.Of<ILogger<ChannelConfigController>>();
            var dp         = new DataProtectionService(new EphemeralDataProtectionProvider(), NullLogger<DataProtectionService>.Instance);
            var httpCtx    = new DefaultHttpContext();
            var httpCtxAcc = new Mock<IHttpContextAccessor>();
            httpCtxAcc.Setup(a => a.HttpContext).Returns(httpCtx);
            var cu = new CurrentUserService(httpCtxAcc.Object, fix.Db);

            var handler = new Mock<HttpMessageHandler>();
            handler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(() => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
            var httpClient  = new HttpClient(handler.Object) { Timeout = TimeSpan.FromSeconds(2) };
            var httpFactory = new Mock<IHttpClientFactory>();
            httpFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

            var waService = new WaClientHostedService(waOpts, NullLogger<WaClientHostedService>.Instance, httpFactory.Object);
            var sut = new ChannelConfigController(fix.Db, cu, dp, waOpts, httpFactory.Object, logger, fix.Cache, waService);
            sut.ControllerContext = new ControllerContext { HttpContext = httpCtx };

            var result = (await sut.WaRestart()) as JsonResult;
            var json   = JsonSerializer.Serialize(result!.Value);
            var doc    = JsonDocument.Parse(json);

            // ok=true (Node arrancó) o ok=false (Node no instalado en CI):
            // En ambos casos el resultado debe ser un JSON válido con campo "ok".
            doc.RootElement.TryGetProperty("ok", out var okProp).Should().BeTrue();

            if (okProp.GetBoolean())
            {
                // Node arrancó ? caché offline debe haberse limpiado
                fix.Cache.TryGetValue("wa_offline", out _).Should().BeFalse(
                    "WaRestart debe eliminar la caché offline tras iniciar el proceso");
            }
            else
            {
                // Node no instalado ? mensaje informativo
                doc.RootElement.GetProperty("message").GetString()
                    .Should().Contain("Node.js");
            }
        }
        finally
        {
            // Node puede tener el directorio abierto brevemente si está instalado.
            // Ignoramos el error de limpieza — el SO liberará el directorio temporal.
            try { Directory.Delete(tmpDir, recursive: true); } catch (IOException) { }
        }
    }
}

// ????????????????????????????????????????????????????????????????????????????
// WaStatus — umbral de auto-restart configurable
// ????????????????????????????????????????????????????????????????????????????

/// <summary>
/// Verifica que <c>RestartIfDeadAsync</c> se dispara <b>exactamente</b> al alcanzar
/// <c>OfflineFailThreshold</c> fallos consecutivos y no antes.
/// </summary>
public class WaStatusAutoRestartThresholdTests
{
    /// <summary>
    /// Helper: construye un controlador con Node siempre offline y un
    /// <see cref="WaClientHostedService"/> parcialmente mockeado para poder
    /// interceptar la llamada a <see cref="WaClientHostedService.RestartIfDeadAsync"/>.
    /// Como la clase es <c>sealed</c>, usamos una subclase de prueba interna.
    /// </summary>
    private static (ChannelConfigController sut, TrackingWaService waService, MemoryCache cache)
        BuildOfflineSut(int threshold)
    {
        var cache = new MemoryCache(new MemoryCacheOptions());

        // HttpClient que siempre devuelve 503
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        var httpClient  = new HttpClient(handler.Object) { Timeout = TimeSpan.FromSeconds(2) };
        var httpFactory = new Mock<IHttpClientFactory>();
        httpFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var waOpts = Options.Create(new WhatsAppClientOptions
        {
            BaseUrl              = "http://localhost:3001",
            ClientPath           = Path.GetTempPath(),
            AutoStart            = false,
            OfflineFailThreshold = threshold,
        });

        // WaClientHostedService de seguimiento: registra cuántas veces se llama RestartIfDeadAsync
        var waService = new TrackingWaService(waOpts, httpFactory.Object);

        // DB mínima con un Business para que GetBusinessIdAsync no devuelva null
        var db = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);
        const string userId = "user-threshold-test";
        db.Businesses.Add(new Business { Id = 1, UserId = userId, Name = "ThresholdBiz" });
        db.SaveChanges();

        var httpCtx = new DefaultHttpContext();
        httpCtx.User = new ClaimsPrincipal(
            new ClaimsIdentity(
                new[] { new Claim(System.Security.Claims.ClaimTypes.NameIdentifier, userId) },
                "TestAuth"));
        var httpCtxAcc = new Mock<IHttpContextAccessor>();
        httpCtxAcc.Setup(a => a.HttpContext).Returns(httpCtx);
        var cu = new CurrentUserService(httpCtxAcc.Object, db);
        var dp = new DataProtectionService(
            new EphemeralDataProtectionProvider(),
            NullLogger<DataProtectionService>.Instance);
        var logger = Mock.Of<ILogger<ChannelConfigController>>();

        var sut = new ChannelConfigController(
            db, cu, dp, waOpts, httpFactory.Object, logger, cache, waService);
        sut.ControllerContext = new ControllerContext { HttpContext = httpCtx };

        return (sut, waService, cache);
    }

    // ?? Tests ????????????????????????????????????????????????????????????????

    [Fact]
    public async Task WaStatus_RestartNotTriggered_BeforeThreshold()
    {
        var (sut, waService, _) = BuildOfflineSut(threshold: 3);

        // 2 polls fallidos — por debajo del umbral
        await sut.WaStatus();
        await sut.WaStatus();

        // Pequeña espera para que el fire-and-forget se procese si hubiera arrancado
        await Task.Delay(50);

        waService.RestartCallCount.Should().Be(0,
            "RestartIfDeadAsync no debe dispararse antes de alcanzar el umbral");
    }

    [Fact]
    public async Task WaStatus_RestartTriggeredExactlyAtThreshold()
    {
        var (sut, waService, _) = BuildOfflineSut(threshold: 3);

        // 3 polls fallidos — exactamente el umbral
        await sut.WaStatus();
        await sut.WaStatus();
        await sut.WaStatus();

        // Dar tiempo al fire-and-forget (RestartIfDeadAsync es no bloqueante)
        await Task.Delay(100);

        waService.RestartCallCount.Should().Be(1,
            "RestartIfDeadAsync debe dispararse exactamente una vez al alcanzar el umbral");
    }

    [Fact]
    public async Task WaStatus_RestartNotTriggeredTwice_WithinSameCycle()
    {
        var (sut, waService, _) = BuildOfflineSut(threshold: 3);

        // 4 polls fallidos — el contador se reinicia tras el restart en el poll 3;
        // el poll 4 arranca un nuevo ciclo (fail_count = 1, no restart todavía).
        await sut.WaStatus(); // fail=1
        await sut.WaStatus(); // fail=2
        await sut.WaStatus(); // fail=3 ? restart + reset contador
        await sut.WaStatus(); // fail=1 del nuevo ciclo

        await Task.Delay(100);

        waService.RestartCallCount.Should().Be(1,
            "El cuarto poll no debe disparar un segundo restart porque el contador se reinició");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(5)]
    public async Task WaStatus_ThresholdIsRespected_ForDifferentValues(int threshold)
    {
        var (sut, waService, _) = BuildOfflineSut(threshold: threshold);

        // threshold-1 polls: no restart todavía
        for (var i = 0; i < threshold - 1; i++)
            await sut.WaStatus();

        await Task.Delay(50);
        waService.RestartCallCount.Should().Be(0,
            $"No debe reiniciar antes de {threshold} fallos");

        // Poll que completa el umbral
        await sut.WaStatus();
        await Task.Delay(100);
        waService.RestartCallCount.Should().Be(1,
            $"Debe reiniciar exactamente al poll #{threshold}");
    }

    // ?? Subclase de seguimiento ???????????????????????????????????????????????

    /// <summary>
    /// Extiende <see cref="WaClientHostedService"/> registrando cada llamada a
    /// <see cref="RestartIfDeadAsync"/> sin lanzar ningún proceso real.
    /// </summary>
    private sealed class TrackingWaService : WaClientHostedService
    {
        private int _restartCallCount;

        /// <summary>Número de veces que se ha invocado <see cref="RestartIfDeadAsync"/>.</summary>
        public int RestartCallCount => _restartCallCount;

        public TrackingWaService(
            IOptions<WhatsAppClientOptions> waOptions,
            IHttpClientFactory httpClientFactory)
            : base(waOptions, NullLogger<WaClientHostedService>.Instance, httpClientFactory) { }

        public override Task RestartIfDeadAsync(CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _restartCallCount);
            return Task.CompletedTask;
        }
    }
}

// ????????????????????????????????????????????????????????????????????????????
// WaRestartStatus — estado del semáforo y fail_count
// ????????????????????????????????????????????????????????????????????????????

/// <summary>
/// Verifica que <c>WaRestartStatus</c> refleja correctamente si
/// <c>RestartIfDeadAsync</c> mantiene el semáforo y expone el contador de fallos.
/// </summary>
public class WaRestartStatusTests
{
    // ?? Subclase que mantiene el semáforo hasta que la liberemos ?????????????

    /// <summary>
    /// Sobrescribe <see cref="IsRestarting"/> con una propiedad controlable y
    /// bloquea <see cref="RestartIfDeadAsync"/> hasta que <see cref="Release"/> sea
    /// invocado, simulando un reinicio en curso observable desde el controlador.
    /// </summary>
    private sealed class BlockingWaService : WaClientHostedService
    {
        private readonly TaskCompletionSource _releaseSignal = new();
        private volatile bool _restarting;

        public override bool IsRestarting => _restarting;

        public BlockingWaService(
            IOptions<WhatsAppClientOptions> waOptions,
            IHttpClientFactory httpClientFactory)
            : base(waOptions, NullLogger<WaClientHostedService>.Instance, httpClientFactory) { }

        public override async Task RestartIfDeadAsync(CancellationToken cancellationToken = default)
        {
            _restarting = true;
            try   { await _releaseSignal.Task; }
            finally { _restarting = false; }
        }

        /// <summary>Libera el restart simulado para que el test pueda continuar.</summary>
        public void Release() => _releaseSignal.TrySetResult();
    }

    // ?? Helper compartido ????????????????????????????????????????????????????

    private static (ChannelConfigController sut, BlockingWaService waService, MemoryCache cache)
        BuildSut(bool autoStart = true)
    {
        var cache = new MemoryCache(new MemoryCacheOptions());

        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        var httpClient  = new HttpClient(handler.Object) { Timeout = TimeSpan.FromSeconds(2) };
        var httpFactory = new Mock<IHttpClientFactory>();
        httpFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var waOpts = Options.Create(new WhatsAppClientOptions
        {
            BaseUrl   = "http://localhost:3001",
            ClientPath = Path.GetTempPath(),
            AutoStart = autoStart,
            OfflineFailThreshold = 3,
        });

        var waService = new BlockingWaService(waOpts, httpFactory.Object);

        var db = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);
        const string userId = "user-restart-status-test";
        db.Businesses.Add(new Business { Id = 1, UserId = userId, Name = "RestartStatusBiz" });
        db.SaveChanges();

        var httpCtx = new DefaultHttpContext();
        httpCtx.User = new ClaimsPrincipal(
            new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, userId) },
                "TestAuth"));
        var httpCtxAcc = new Mock<IHttpContextAccessor>();
        httpCtxAcc.Setup(a => a.HttpContext).Returns(httpCtx);
        var cu     = new CurrentUserService(httpCtxAcc.Object, db);
        var dp     = new DataProtectionService(new EphemeralDataProtectionProvider(), NullLogger<DataProtectionService>.Instance);
        var logger = Mock.Of<ILogger<ChannelConfigController>>();

        var sut = new ChannelConfigController(db, cu, dp, waOpts, httpFactory.Object, logger, cache, waService);
        sut.ControllerContext = new ControllerContext { HttpContext = httpCtx };

        return (sut, waService, cache);
    }

    // ?? Tests ????????????????????????????????????????????????????????????????

    /// <summary>
    /// Mientras <c>RestartIfDeadAsync</c> mantiene el semáforo (reinicio en curso),
    /// <c>WaRestartStatus</c> debe devolver <c>restarting: true</c>.
    /// </summary>
    [Fact]
    public async Task WaRestartStatus_WhileRestartInProgress_ReturnsRestartingTrue()
    {
        var (sut, waService, _) = BuildSut();

        // Lanzamos el restart en background; BlockingWaService lo retiene hasta Release()
        var restartTask = waService.RestartIfDeadAsync();

        // Dar un instante para que la tarea arranque y adquiera el semáforo
        await Task.Delay(20);

        var result = sut.WaRestartStatus() as JsonResult;
        var json   = JsonSerializer.Serialize(result!.Value);
        var doc    = JsonDocument.Parse(json);

        doc.RootElement.GetProperty("restarting").GetBoolean().Should().BeTrue(
            "IsRestarting debe ser true mientras RestartIfDeadAsync mantiene el semáforo");

        // Liberar el semáforo para que el test cierre limpiamente
        waService.Release();
        await restartTask;
    }

    /// <summary>
    /// Una vez que <c>RestartIfDeadAsync</c> ha terminado, <c>restarting</c> vuelve a <c>false</c>.
    /// </summary>
    [Fact]
    public async Task WaRestartStatus_AfterRestartCompletes_ReturnsRestartingFalse()
    {
        var (sut, waService, _) = BuildSut();

        var restartTask = waService.RestartIfDeadAsync();
        waService.Release();          // liberamos inmediatamente
        await restartTask;

        var result = sut.WaRestartStatus() as JsonResult;
        var json   = JsonSerializer.Serialize(result!.Value);
        var doc    = JsonDocument.Parse(json);

        doc.RootElement.GetProperty("restarting").GetBoolean().Should().BeFalse();
    }

    /// <summary>
    /// El campo <c>failCount</c> debe coincidir con el valor almacenado en caché.
    /// </summary>
    [Fact]
    public void WaRestartStatus_ReturnsFailCountFromCache()
    {
        var (sut, _, cache) = BuildSut();

        // Inyectamos directamente el valor en caché (clave interna del controlador)
        cache.Set("wa_fail_count", 2, TimeSpan.FromMinutes(2));

        var result = sut.WaRestartStatus() as JsonResult;
        var json   = JsonSerializer.Serialize(result!.Value);
        var doc    = JsonDocument.Parse(json);

        doc.RootElement.GetProperty("failCount").GetInt32().Should().Be(2,
            "failCount debe reflejar el valor almacenado en la caché wa_fail_count");
    }

    /// <summary>
    /// Cuando no hay ningún fallo previo en caché, <c>failCount</c> debe ser 0.
    /// </summary>
    [Fact]
    public void WaRestartStatus_NoFailCountInCache_ReturnsZero()
    {
        var (sut, _, _) = BuildSut();

        var result = sut.WaRestartStatus() as JsonResult;
        var json   = JsonSerializer.Serialize(result!.Value);
        var doc    = JsonDocument.Parse(json);

        doc.RootElement.GetProperty("failCount").GetInt32().Should().Be(0);
    }
}

// ????????????????????????????????????????????????????????????????????????????
// Disconnect
// ????????????????????????????????????????????????????????????????????????????

/// <summary>
/// Verifica el comportamiento de <c>Disconnect</c>: marca el canal como inactivo
/// en DB, limpia la caché offline y redirige a la página WhatsApp.
/// </summary>
public class DisconnectTests
{
    // ?? Sin negocio autenticado ? Unauthorized ???????????????????????????????

    [Fact]
    public async Task Disconnect_NoBusinessId_ReturnsUnauthorized()
    {
        using var fix = new ControllerFixture(businessId: null);

        var result = await fix.Sut.Disconnect();

        result.Should().BeOfType<UnauthorizedResult>();
    }

    // ?? Canal activo ? queda inactivo en DB ??????????????????????????????????

    [Fact]
    public async Task Disconnect_ActiveChannel_MarksInactiveInDb()
    {
        using var fix = new ControllerFixture(waNodeOnline: false);

        // Crear canal activo previo
        fix.Db.ChannelConfigs.Add(new ChannelConfig
        {
            BusinessId           = 1,
            ChannelType          = ChannelType.WhatsApp,
            IsActive             = true,
            ConnectedAt          = DateTime.UtcNow,
            AccessTokenEncrypted = string.Empty,
            WebhookVerifyToken   = "tok",
        });
        await fix.Db.SaveChangesAsync();

        await fix.Sut.Disconnect();

        var cfg = fix.Db.ChannelConfigs.First(c => c.BusinessId == 1 && c.ChannelType == ChannelType.WhatsApp);
        cfg.IsActive.Should().BeFalse("Disconnect debe desactivar el canal");
        cfg.ConnectedAt.Should().BeNull("Disconnect debe borrar la fecha de conexión");
    }

    // ?? Caché offline se limpia ???????????????????????????????????????????????

    [Fact]
    public async Task Disconnect_ClearsOfflineCache()
    {
        using var fix = new ControllerFixture(waNodeOnline: false);
        fix.Cache.Set("wa_offline", true, TimeSpan.FromSeconds(30));

        await fix.Sut.Disconnect();

        fix.Cache.TryGetValue("wa_offline", out _).Should().BeFalse(
            "Disconnect debe eliminar la clave wa_offline para que el próximo poll sea real");
    }

    // ?? Sin canal previo en DB ? no lanza excepción ??????????????????????????

    [Fact]
    public async Task Disconnect_NoExistingChannel_DoesNotThrow()
    {
        using var fix = new ControllerFixture(waNodeOnline: false);

        var act = () => fix.Sut.Disconnect();

        await act.Should().NotThrowAsync();
    }

    // ?? Redirige a WhatsApp tras desconectar ?????????????????????????????????

    [Fact]
    public async Task Disconnect_RedirectsToWhatsApp()
    {
        using var fix = new ControllerFixture(waNodeOnline: false);

        var result = await fix.Sut.Disconnect() as RedirectToActionResult;

        result.Should().NotBeNull();
        result!.ActionName.Should().Be("WhatsApp");
    }
}

// ????????????????????????????????????????????????????????????????????????????
// SaveQrConnection
// ????????????????????????????????????????????????????????????????????????????

/// <summary>
/// Verifica el comportamiento de <c>SaveQrConnection</c>: crea o actualiza el
/// canal WhatsApp en DB con el número de teléfono normalizado.
/// </summary>
public class SaveQrConnectionTests
{
    // ?? Sin negocio autenticado ? Unauthorized ???????????????????????????????

    [Fact]
    public async Task SaveQrConnection_NoBusinessId_ReturnsUnauthorized()
    {
        using var fix = new ControllerFixture(businessId: null);

        var result = await fix.Sut.SaveQrConnection(new QrConnectionViewModel { Phone = "34600000000" });

        result.Should().BeOfType<UnauthorizedResult>();
    }

    // ?? Canal nuevo ? se crea con IsActive=true ??????????????????????????????

    [Fact]
    public async Task SaveQrConnection_NewChannel_CreatesActiveRecord()
    {
        using var fix = new ControllerFixture();

        var result = (await fix.Sut.SaveQrConnection(new QrConnectionViewModel { Phone = "34600000000" })) as JsonResult;

        var json = JsonSerializer.Serialize(result!.Value);
        JsonDocument.Parse(json).RootElement.GetProperty("success").GetBoolean().Should().BeTrue();

        var cfg = fix.Db.ChannelConfigs.FirstOrDefault(c => c.BusinessId == 1 && c.ChannelType == ChannelType.WhatsApp);
        cfg.Should().NotBeNull();
        cfg!.IsActive.Should().BeTrue();
        cfg.PhoneNumber.Should().Be("34600000000");
        cfg.ConnectedAt.Should().NotBeNull();
    }

    // ?? Canal existente ? se actualiza, no se duplica ???????????????????????

    [Fact]
    public async Task SaveQrConnection_ExistingChannel_UpdatesWithoutDuplicate()
    {
        using var fix = new ControllerFixture();
        fix.Db.ChannelConfigs.Add(new ChannelConfig
        {
            BusinessId           = 1,
            ChannelType          = ChannelType.WhatsApp,
            IsActive             = false,
            AccessTokenEncrypted = string.Empty,
            WebhookVerifyToken   = "tok",
        });
        await fix.Db.SaveChangesAsync();

        await fix.Sut.SaveQrConnection(new QrConnectionViewModel { Phone = "34611111111" });

        var configs = fix.Db.ChannelConfigs.Where(c => c.BusinessId == 1 && c.ChannelType == ChannelType.WhatsApp).ToList();
        configs.Should().HaveCount(1, "no debe crearse un duplicado");
        configs[0].IsActive.Should().BeTrue();
        configs[0].PhoneNumber.Should().Be("34611111111");
    }

    // ?? Normalización del teléfono: quita +, espacios y @c.us ????????????????

    [Theory]
    [InlineData("+34 600 000 000", "34600000000")]
    [InlineData("34600000000@c.us", "34600000000")]
    [InlineData("+34600000000@c.us", "34600000000")]
    [InlineData("  34600000000  ", "34600000000")]
    public async Task SaveQrConnection_NormalizesPhoneNumber(string rawPhone, string expectedPhone)
    {
        using var fix = new ControllerFixture();

        await fix.Sut.SaveQrConnection(new QrConnectionViewModel { Phone = rawPhone });

        var cfg = fix.Db.ChannelConfigs.First(c => c.BusinessId == 1 && c.ChannelType == ChannelType.WhatsApp);
        cfg.PhoneNumber.Should().Be(expectedPhone,
            $"el teléfono '{rawPhone}' debe normalizarse a '{expectedPhone}'");
    }

    // ?? Phone nulo ? se guarda null sin lanzar ???????????????????????????????

    [Fact]
    public async Task SaveQrConnection_NullPhone_SavesNullWithoutError()
    {
        using var fix = new ControllerFixture();

        var act = () => fix.Sut.SaveQrConnection(new QrConnectionViewModel { Phone = null });

        await act.Should().NotThrowAsync();

        var cfg = fix.Db.ChannelConfigs.First(c => c.BusinessId == 1 && c.ChannelType == ChannelType.WhatsApp);
        cfg.PhoneNumber.Should().BeNull();
        cfg.IsActive.Should().BeTrue();
    }
}
