using System.Diagnostics;
using System.Text;
using Calcifer.Cathedra.Domain;
using Calcifer.Cathedra.Http;
using Calcifer.SweetPDF.Infrastructure.Configuration;
using Calcifer.SweetPDF.Models;
using Calcifer.SweetPDF.Parsers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Exceptions;

namespace Calcifer.SweetPDF.Services;

public sealed class PdfExtractionService : IPdfExtractionService
{
    private readonly ILogger<PdfExtractionService> _logger;
    private readonly IOptions<PdfExtractionOptions> _options;
    private readonly IPdfTypeDetector _typeDetector;
    private readonly ITextStructureParser _structureParser;

    public PdfExtractionService(
        ILogger<PdfExtractionService> logger,
        IOptions<PdfExtractionOptions> options,
        IPdfTypeDetector typeDetector,
        ITextStructureParser structureParser)
    {
        _logger = logger;
        _options = options;
        _typeDetector = typeDetector;
        _structureParser = structureParser;
    }

    public Task<Result<ExtractResponse>> ExtractAsync(
        Stream pdfStream, string fileName, CancellationToken ct = default)
    {
        // PdfPig parsing is CPU-bound; run it off the request thread so large documents don't
        // stall the pipeline.
        return Task.Run(() => Extract(pdfStream, fileName, ct), ct);
    }

    public Task<Result<string>> ExtractTextOnlyAsync(Stream pdfStream, CancellationToken ct = default)
    {
        return Task.Run<Result<string>>(() =>
        {
            try
            {
                using var document = Open(pdfStream);
                var text = new StringBuilder();
                foreach (var page in PagesToProcess(document))
                {
                    ct.ThrowIfCancellationRequested();
                    text.AppendLine(ExtractTextFromPage(page));
                }
                return text.ToString();
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                return MapOpenFailure(ex);
            }
        }, ct);
    }

    private Result<ExtractResponse> Extract(Stream pdfStream, string fileName, CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();
        var fileSize = pdfStream.CanSeek ? pdfStream.Length : 0;

        try
        {
            using var document = Open(pdfStream);

            var options = _options.Value;
            var metadata = ExtractMetadata(document, fileName);
            var hasTextLayer = _typeDetector.HasTextLayer(document);
            var isScanned = !hasTextLayer;

            var pages = new List<PageContent>();
            var allText = new StringBuilder();

            foreach (var page in PagesToProcess(document))
            {
                ct.ThrowIfCancellationRequested();

                var text = ExtractTextFromPage(page);
                allText.AppendLine(text);

                pages.Add(new PageContent(
                    PageNumber: page.Number,
                    Text: text,
                    Words: options.IncludeWords ? ExtractWords(page) : Array.Empty<WordDto>(),
                    Lines: text.Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries),
                    Width: page.Width,
                    Height: page.Height));
            }

            var rawText = allText.ToString();
            var structuredData = options.DetectStructure ? _structureParser.Parse(rawText) : null;

            stopwatch.Stop();
            _logger.LogInformation(
                "Extracted {PageCount} pages from {FileName} in {ElapsedMs}ms (scanned: {IsScanned})",
                pages.Count, fileName, stopwatch.ElapsedMilliseconds, isScanned);

            return new ExtractResponse(
                FileName: fileName,
                FileSize: fileSize,
                Metadata: metadata,
                Pages: pages,
                RawText: rawText,
                ProcessedAtUtc: DateTime.UtcNow,
                ProcessingTimeMs: stopwatch.ElapsedMilliseconds,
                IsScanned: isScanned,
                HasTextLayer: hasTextLayer,
                StructuredData: structuredData);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Failed to extract PDF {FileName}", fileName);
            return MapOpenFailure(ex);
        }
    }

    private PdfDocument Open(Stream pdfStream) =>
        PdfDocument.Open(pdfStream, new ParsingOptions
        {
            UseLenientParsing = true,
            Passwords = _options.Value.Passwords,
        });

    private IEnumerable<Page> PagesToProcess(PdfDocument document)
    {
        var pages = document.GetPages();
        var max = _options.Value.MaxPages;
        return max > 0 ? pages.Take(max) : pages;
    }

    private static ApiError MapOpenFailure(Exception ex) => ex switch
    {
        PdfDocumentEncryptedException =>
            new ApiError("PDF_ENCRYPTED", "The PDF is password-protected and no configured password opened it."),
        _ => new ApiError("PDF_EXTRACTION_FAILED", $"The file could not be parsed as a PDF: {ex.Message}"),
    };

    private static PdfMetadataDto ExtractMetadata(PdfDocument document, string fileName)
    {
        var info = document.Information;
        return new PdfMetadataDto(
            Title: string.IsNullOrWhiteSpace(info.Title) ? fileName : info.Title,
            Author: info.Author,
            Subject: info.Subject,
            Keywords: info.Keywords,
            Creator: info.Creator,
            Producer: info.Producer,
            CreationDate: info.CreationDate,
            ModificationDate: info.ModifiedDate,
            PageCount: document.NumberOfPages,
            PdfVersion: document.Version.ToString("0.0"));
    }

    private static string ExtractTextFromPage(Page page) => page.Text;

    private static IReadOnlyList<WordDto> ExtractWords(Page page)
    {
        try
        {
            return page.GetWords()
                .Select(w => new WordDto(
                    Text: w.Text,
                    Left: w.BoundingBox.Left,
                    Bottom: w.BoundingBox.Bottom,
                    Width: w.BoundingBox.Width,
                    Height: w.BoundingBox.Height,
                    FontSize: w.Letters.Count > 0 ? w.Letters[0].PointSize : 0,
                    FontName: w.FontName))
                .ToList();
        }
        catch
        {
            // Word segmentation is best-effort; page text is already captured.
            return Array.Empty<WordDto>();
        }
    }
}
