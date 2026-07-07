using Microsoft.Extensions.Logging;
using UglyToad.PdfPig;

namespace Calcifer.SweetPDF.Services;

public sealed class PdfTypeDetector : IPdfTypeDetector
{
    // A scan converted with a bad OCR layer can still contain a handful of stray letters, so a
    // page needs more than this many letters before it counts as having a real text layer.
    private const int MinLettersPerPage = 10;

    private readonly ILogger<PdfTypeDetector> _logger;

    public PdfTypeDetector(ILogger<PdfTypeDetector> logger)
    {
        _logger = logger;
    }

    public bool HasTextLayer(PdfDocument document) =>
        document.GetPages().Any(p => p.Letters.Count > MinLettersPerPage);

    public bool IsScanned(PdfDocument document)
    {
        var scanned = !HasTextLayer(document);
        if (scanned)
            _logger.LogDebug("PDF appears to be scanned (no text layer)");
        return scanned;
    }
}
