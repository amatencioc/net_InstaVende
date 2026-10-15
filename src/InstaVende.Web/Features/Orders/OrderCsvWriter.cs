using System.Globalization;
using System.Text;

namespace InstaVende.Web.Features.Orders;

/// <summary>
/// Generador CSV (RFC 4180) seguro para abrir en Excel/Sheets:
/// UTF-8 con BOM, separador coma, saltos CRLF, todos los campos de texto entre comillas
/// y protección contra inyección de fórmulas (CSV injection).
/// </summary>
internal sealed class OrderCsvWriter
{
    private readonly StringBuilder _sb = new();
    private bool _lineStarted;

    /// <summary>Campo de texto: escapa comillas y neutraliza fórmulas (= + - @ tab CR).</summary>
    public OrderCsvWriter Text(string? value)
    {
        Separator();
        var v = value ?? string.Empty;
        if (v.Length > 0 && v[0] is '=' or '+' or '-' or '@' or '\t' or '\r')
            v = "'" + v;
        _sb.Append('"').Append(v.Replace("\"", "\"\"")).Append('"');
        return this;
    }

    /// <summary>Importe con punto decimal invariante y 2 decimales (sin comillas para que sea numérico).</summary>
    public OrderCsvWriter Amount(decimal value)
    {
        Separator();
        _sb.Append(value.ToString("0.00", CultureInfo.InvariantCulture));
        return this;
    }

    public OrderCsvWriter Integer(int value)
    {
        Separator();
        _sb.Append(value.ToString(CultureInfo.InvariantCulture));
        return this;
    }

    public OrderCsvWriter EndLine()
    {
        _sb.Append("\r\n");
        _lineStarted = false;
        return this;
    }

    public byte[] ToBytes()
    {
        var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
        var preamble = encoding.GetPreamble();
        var body = encoding.GetBytes(_sb.ToString());
        var bytes = new byte[preamble.Length + body.Length];
        preamble.CopyTo(bytes, 0);
        body.CopyTo(bytes, preamble.Length);
        return bytes;
    }

    private void Separator()
    {
        if (_lineStarted) _sb.Append(',');
        _lineStarted = true;
    }
}
