namespace Calcifer.SweetPDF.Infrastructure.Configuration;

/// <summary>
/// Options for PDF extraction, bound from the "Cathedra:PdfExtraction" configuration section.
/// </summary>
public sealed class PdfExtractionOptions
{
    /// <summary>Maximum accepted upload size in bytes (default 25 MB).</summary>
    public long MaxFileSizeBytes { get; set; } = 25 * 1024 * 1024;

    /// <summary>Include word-level bounding boxes per page.</summary>
    public bool IncludeWords { get; set; } = true;

    /// <summary>Run the structure parser (emails, dates, invoice numbers, amounts) over the text.</summary>
    public bool DetectStructure { get; set; } = false;

    /// <summary>Passwords tried, in order, for encrypted PDFs.</summary>
    public List<string> Passwords { get; set; } = new();

    /// <summary>Maximum number of pages to process; 0 means all pages.</summary>
    public int MaxPages { get; set; } = 0;
}
