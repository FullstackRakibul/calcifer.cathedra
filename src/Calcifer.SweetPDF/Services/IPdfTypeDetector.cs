using UglyToad.PdfPig;

namespace Calcifer.SweetPDF.Services;

/// <summary>
/// Classifies a PDF as digital (has a text layer) or scanned (image-only). Operates on an already
/// opened <see cref="PdfDocument"/> so the upload stream is parsed exactly once.
/// </summary>
public interface IPdfTypeDetector
{
    /// <summary>True when the document has a usable text layer.</summary>
    bool HasTextLayer(PdfDocument document);

    /// <summary>True when the document appears to be a scan (no meaningful text layer).</summary>
    bool IsScanned(PdfDocument document);
}
