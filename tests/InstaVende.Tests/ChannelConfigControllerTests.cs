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

        // ?? Controlador ???????????????????????????????????????????????????????
        Sut = new ChannelConfigController(Db, cu, dp, waOpts, httpFactory.Object, logger, Cache);
        Sut.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext,
        };
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
        var sut = new ChannelConfigController(fix.Db, cu, dp, waOpts, httpFactory.Object, logger, fix.Cache);
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

        var sut = new ChannelConfigController(fix.Db, cu, dp, waOpts, httpFactory.Object, logger, fix.Cache);
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

            var sut = new ChannelConfigController(fix.Db, cu, dp, waOpts, httpFactory.Object, logger, fix.Cache);
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

            var sut = new ChannelConfigController(fix.Db, cu, dp, waOpts, httpFactory.Object, logger, fix.Cache);
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
