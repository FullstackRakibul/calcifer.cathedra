using Calcifer.Cathedra.Domain;
using Calcifer.Cathedra.Http;
using Calcifer.SweetPDF.Infrastructure.Configuration;
using Calcifer.SweetPDF.Models;
using Calcifer.SweetPDF.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;

namespace Calcifer.SweetPDF.Endpoints;

internal static class SweetPdfEndpoints
{
    public static IEndpointRouteBuilder MapSweetPdfEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/v1/sweetpdf").WithTags("SweetPDF").DisableAntiforgery();

        group.MapPost("/extract", async (HttpContext ctx, IFormFile file,
                IPdfExtractionService svc, IOptions<PdfExtractionOptions> options) =>
            {
                var validation = ValidateUpload(file, options.Value);
                if (validation.IsFailure)
                    return ApiResults.From(ctx, Result<ExtractResponse>.Failure(validation.Error!));

                await using var stream = await BufferAsync(file, ctx.RequestAborted);
                var result = await svc.ExtractAsync(stream, file.FileName, ctx.RequestAborted);
                return ApiResults.From(ctx, result);
            })
            .WithName("ExtractPdf")
            .WithSummary("Upload a PDF and receive structured JSON: metadata, per-page text, words, and detected fields.")
            .Produces<ApiResponse<ExtractResponse>>();

        group.MapPost("/extract-text", async (HttpContext ctx, IFormFile file,
                IPdfExtractionService svc, IOptions<PdfExtractionOptions> options) =>
            {
                var validation = ValidateUpload(file, options.Value);
                if (validation.IsFailure)
                    return ApiResults.From(ctx, Result<string>.Failure(validation.Error!));

                await using var stream = await BufferAsync(file, ctx.RequestAborted);
                var result = await svc.ExtractTextOnlyAsync(stream, ctx.RequestAborted);
                return ApiResults.From(ctx, result);
            })
            .WithName("ExtractPdfTextOnly")
            .WithSummary("Upload a PDF and receive only its text, in reading order.")
            .Produces<ApiResponse<string>>();

        return routes;
    }

    private static Result ValidateUpload(IFormFile? file, PdfExtractionOptions options)
    {
        if (file is null || file.Length == 0)
            return Result.Failure("FILE_REQUIRED", "Please provide a PDF file in the 'file' form field.");

        if (file.Length > options.MaxFileSizeBytes)
            return Result.Failure("FILE_TOO_LARGE",
                $"File size exceeds the {options.MaxFileSizeBytes / 1024 / 1024} MB limit.");

        var extension = Path.GetExtension(file.FileName);
        if (!string.Equals(extension, ".pdf", StringComparison.OrdinalIgnoreCase))
            return Result.Failure("INVALID_FILE_TYPE", "Only .pdf files are supported.");

        return Result.Success();
    }

    /// <summary>
    /// PdfPig needs a seekable stream (it reads the trailer first); the multipart body stream is
    /// forward-only, so buffer the upload into memory. Uploads are already size-capped by
    /// <see cref="PdfExtractionOptions.MaxFileSizeBytes"/>.
    /// </summary>
    private static async Task<MemoryStream> BufferAsync(IFormFile file, CancellationToken ct)
    {
        var buffer = new MemoryStream(capacity: (int)file.Length);
        await file.CopyToAsync(buffer, ct);
        buffer.Position = 0;
        return buffer;
    }
}
