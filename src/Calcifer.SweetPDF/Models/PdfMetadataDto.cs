namespace Calcifer.SweetPDF.Models;

/// <summary>
/// Document metadata read from the PDF's information dictionary. Creation/modification dates are
/// the raw PDF date strings (e.g. "D:20240115120000Z") because the format is not reliably ISO.
/// </summary>
public sealed record PdfMetadataDto(
    string? Title,
    string? Author,
    string? Subject,
    string? Keywords,
    string? Creator,
    string? Producer,
    string? CreationDate,
    string? ModificationDate,
    int PageCount,
    string? PdfVersion);
