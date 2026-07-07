namespace Calcifer.SweetPDF.Models;

/// <summary>Complete extraction result for one PDF document.</summary>
public sealed record ExtractResponse(
    string FileName,
    long FileSize,
    PdfMetadataDto Metadata,
    IReadOnlyList<PageContent> Pages,
    string RawText,
    DateTime ProcessedAtUtc,
    long ProcessingTimeMs,
    bool IsScanned,
    bool HasTextLayer,
    IReadOnlyDictionary<string, object>? StructuredData);
