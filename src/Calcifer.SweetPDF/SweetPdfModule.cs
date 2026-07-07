using Calcifer.Cathedra.Modules;
using Calcifer.SweetPDF.Endpoints;
using Calcifer.SweetPDF.Infrastructure.Configuration;
using Calcifer.SweetPDF.Parsers;
using Calcifer.SweetPDF.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Calcifer.SweetPDF;

/// <summary>
/// PDF-to-JSON extraction module built on PdfPig. Uploads at /api/v1/sweetpdf/* come back as the
/// platform envelope carrying metadata, per-page text, word bounding boxes, and detected fields.
/// </summary>
public sealed class SweetPdfModule : IModule
{
    public string Name => "SweetPDF";
    public string Version => "1.0.0";
    public int Order => 10;

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddOptions<PdfExtractionOptions>()
            .BindConfiguration("Cathedra:PdfExtraction");

        services.AddScoped<IPdfExtractionService, PdfExtractionService>();
        services.AddSingleton<IPdfTypeDetector, PdfTypeDetector>();
        services.AddSingleton<ITextStructureParser, TextStructureParser>();
    }

    public void MapEndpoints(WebApplication app)
    {
        app.MapSweetPdfEndpoints();
    }

    public Task InitializeAsync(IServiceProvider services, CancellationToken ct = default)
    {
        var logger = services.GetRequiredService<ILogger<SweetPdfModule>>();
        logger.LogInformation("SweetPDF module initialized");
        return Task.CompletedTask;
    }
}
