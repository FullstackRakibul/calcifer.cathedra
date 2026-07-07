using Calcifer.Cathedra.Domain;
using Calcifer.SweetPDF.Models;

namespace Calcifer.SweetPDF.Services;

/// <summary>
/// PDF extraction operations. All methods return <see cref="Result{T}"/> so endpoints can hand the
/// outcome straight to <c>ApiResults.From</c>: parse failures become enveloped 4xx responses
/// instead of exceptions.
/// </summary>
public interface IPdfExtractionService
{
    /// <summary>Full extraction: metadata, per-page text, optional words, optional structured data.</summary>
    Task<Result<ExtractResponse>> ExtractAsync(
        Stream pdfStream, string fileName, CancellationToken ct = default);

    /// <summary>Lightweight extraction: the document's text only, in content order.</summary>
    Task<Result<string>> ExtractTextOnlyAsync(Stream pdfStream, CancellationToken ct = default);
}
