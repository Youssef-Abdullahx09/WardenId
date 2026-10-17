using System.Globalization;
using Net.Codecrete.QrCodeGenerator;

namespace WardenId.Protocol.Presentation;

/// <summary>A QR code as inline SVG markup (no image file, no System.Drawing).</summary>
public static class QrCodeSvg
{
    private const int QuietZone = 4;

    /// <summary>Safe to emit raw: the markup holds only numbers and fixed text, never <paramref name="text"/>.</summary>
    public static string Render(string text, string label)
    {
        var qr = QrCode.EncodeText(text, QrCode.Ecc.Medium);
        var size = (qr.Size + 2 * QuietZone).ToString(CultureInfo.InvariantCulture);
        return $"""<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {size} {size}" width="200" height="200" role="img" aria-label="{System.Net.WebUtility.HtmlEncode(label)}" shape-rendering="crispEdges"><rect width="{size}" height="{size}" fill="#fff"/><path d="{qr.ToGraphicsPath(QuietZone)}" fill="#000"/></svg>""";
    }
}
