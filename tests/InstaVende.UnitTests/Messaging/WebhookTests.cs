using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using InstaVende.Core.Enums;
using InstaVende.Web.Services.Messaging;

namespace InstaVende.UnitTests.Messaging;

public class WebhookSignatureTests
{
    private const string Secret = "s3cr3t";

    private static string Sign(byte[] body) => "sha256=" + Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(Secret), body));

    [Fact]
    public void FirmaValida_SeAcepta()
    {
        var body = Encoding.UTF8.GetBytes("{\"a\":\"ñandú\"}");
        Assert.True(WebhookSignature.IsValid(body, Secret, Sign(body)));
    }

    [Fact]
    public void FirmaEnMayusculas_SeAcepta()
    {
        var body = Encoding.UTF8.GetBytes("{}");
        Assert.True(WebhookSignature.IsValid(body, Secret, Sign(body).ToUpperInvariant().Replace("SHA256=", "sha256=")));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("sha1=abc")]
    [InlineData("sha256=zz")]
    [InlineData("sha256=00")]
    public void FirmaAusenteOMalFormada_SeRechaza(string? header)
        => Assert.False(WebhookSignature.IsValid("{}"u8, Secret, header));

    [Fact]
    public void CuerpoAlterado_SeRechaza()
    {
        var header = Sign(Encoding.UTF8.GetBytes("{\"total\":10}"));
        Assert.False(WebhookSignature.IsValid(Encoding.UTF8.GetBytes("{\"total\":99}"), Secret, header));
    }

    [Fact]
    public void SinSecretoConfigurado_SeRechaza_FailClosed()
    {
        var body = Encoding.UTF8.GetBytes("{}");
        Assert.False(WebhookSignature.IsValid(body, null, Sign(body)));
    }
}

public class WebhookPayloadParserTests
{
    private static JsonElement Json(string json) => JsonDocument.Parse(json).RootElement;

    [Fact]
    public void WhatsAppCloud_ExtraeTextoNombreEId()
    {
        var root = Json("""
        {"entry":[{"changes":[{"value":{
          "contacts":[{"wa_id":"51999111222","profile":{"name":"María"}}],
          "messages":[
            {"from":"51999111222","id":"wamid.1","type":"text","text":{"body":" Hola "}},
            {"from":"51999111222","id":"wamid.2","type":"image","image":{}},
            {"from":"51999111222","id":"wamid.3","type":"interactive","interactive":{"button_reply":{"title":"Sí, quiero"}}}
          ]}}]}]}
        """);

        var messages = WebhookPayloadParser.ParseWhatsAppCloud(7, root).ToList();

        Assert.Equal(2, messages.Count);
        Assert.Equal(new InboundMessage(7, ChannelType.WhatsApp, "51999111222", "wamid.1", "Hola", "María"), messages[0]);
        Assert.Equal("Sí, quiero", messages[1].Text);
    }

    [Fact]
    public void WhatsAppCloud_EventosDeEstado_NoGeneranMensajes()
        => Assert.Empty(WebhookPayloadParser.ParseWhatsAppCloud(1, Json("""{"entry":[{"changes":[{"value":{"statuses":[{"id":"x"}]}}]}]}""")));

    [Fact]
    public void Messenger_IgnoraEcosYEventosSinTexto()
    {
        var root = Json("""
        {"object":"page","entry":[{"messaging":[
          {"sender":{"id":"psid-1"},"message":{"mid":"m1","text":"Precio?"}},
          {"sender":{"id":"page"},"message":{"mid":"m2","text":"eco","is_echo":true}},
          {"sender":{"id":"psid-1"},"read":{"watermark":1}}
        ]},{"standby":[{}]}]}
        """);

        var messages = WebhookPayloadParser.ParseMetaMessaging(3, root, ChannelType.FacebookMessenger).ToList();

        var only = Assert.Single(messages);
        Assert.Equal(new InboundMessage(3, ChannelType.FacebookMessenger, "psid-1", "m1", "Precio?", null), only);
    }

    [Fact]
    public void WaClientLocal_RequiereRemitenteYTexto()
    {
        Assert.Null(WebhookPayloadParser.ParseWhatsAppLocal(1, Json("""{"from":"51999","body":"  "}""")));

        var msg = WebhookPayloadParser.ParseWhatsAppLocal(1, Json("""{"from":"51999","body":"hola","pushname":"Ana","messageId":"false_51999@c.us_ABC"}"""));

        Assert.NotNull(msg);
        Assert.Equal("false_51999@c.us_ABC", msg.ExternalMessageId);
        Assert.Equal("Ana", msg.DisplayName);
    }

    [Fact]
    public void PayloadSinEntry_NoLanza()
    {
        Assert.Empty(WebhookPayloadParser.ParseWhatsAppCloud(1, Json("{}")));
        Assert.Empty(WebhookPayloadParser.ParseMetaMessaging(1, Json("""{"entry":"x"}"""), ChannelType.Instagram));
    }
}
